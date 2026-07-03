using Microsoft.Data.SqlClient;
using TrixCompareDb.Data;
using TrixCompareDb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TrixCompareDb.Services
{
    public class UpdateTableService
    {
        private readonly TableRepository _repo;

        public UpdateTableService(TableRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Performs a full mirror sync of the target table to match the source table.
        /// Uses Id column as the primary key to match rows.
        /// Wrapped in a transaction for atomicity.
        /// </summary>
        public async Task<UpdateResult> UpdateTargetTableAsync(
            string dbNameSource,
            string dbNameTarget,
            string tableName)
        {
            try
            {
                // Fetch source and target data
                var sourceRows = await _repo.GetTable(dbNameSource, tableName);
                var targetRows = await _repo.GetTable(dbNameTarget, tableName);

                // Get connection strings for both databases
                var targetConnString = _repo.GetConnectionString(dbNameTarget);
                if (string.IsNullOrEmpty(targetConnString))
                    throw new InvalidOperationException($"Connection string for '{dbNameTarget}' not found.");

                // Perform the sync logic
                await using var connection = new SqlConnection(targetConnString);
                await connection.OpenAsync();

                await using var transaction = connection.BeginTransaction();
                try
                {
                    // Build dictionaries keyed by Id
                    var sourceDict = sourceRows
                        .Where(d => d.ContainsKey("Id") && d["Id"] != null)
                        .ToDictionary(x => x["Id"].ToString());
                    var targetDict = targetRows
                        .Where(d => d.ContainsKey("Id") && d["Id"] != null)
                        .ToDictionary(x => x["Id"].ToString());

                    // Get all columns from source (these are the columns we care about)
                    var columns = sourceDict.FirstOrDefault().Value?.Keys.ToList() ?? new List<string>();
                    if (columns.Count == 0)
                        throw new InvalidOperationException("Source table has no columns.");

                    // Determine if Id column is an identity column in the target table (once, before loop)
                    bool idIsIdentity = await IsIdentityColumnAsync(connection, transaction, tableName, "Id");

                    int deleted = 0;
                    int inserted = 0;
                    int updated = 0;

                    // 1. Delete rows from target whose Id doesn't exist in source
                    var targetIdsToDelete = targetDict.Keys.Except(sourceDict.Keys).ToList();
                    foreach (var idStr in targetIdsToDelete)
                    {
                        var deleteQuery = $"DELETE FROM {tableName} WHERE Id = @id";
                        await using var deleteCmd = new SqlCommand(deleteQuery, connection, transaction);
                        deleteCmd.Parameters.AddWithValue("@id", idStr);
                        deleted += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    // 2. For rows in both source and target: delete target row and re-insert from source
                    var commonIds = sourceDict.Keys.Intersect(targetDict.Keys).ToList();
                    foreach (var idStr in commonIds)
                    {
                        var sourceRow = sourceDict[idStr];
                        var targetRow = targetDict[idStr];

                        // Check if they differ
                        bool rowsDiffer = RowsDiffer(sourceRow, targetRow);

                        if (rowsDiffer)
                        {
                            // Delete from target
                            var deleteQuery = $"DELETE FROM {tableName} WHERE Id = @id";
                            await using var deleteCmd = new SqlCommand(deleteQuery, connection, transaction);
                            deleteCmd.Parameters.AddWithValue("@id", idStr);
                            await deleteCmd.ExecuteNonQueryAsync();

                            // Re-insert from source
                            await InsertRowAsync(connection, transaction, tableName, sourceRow, columns, idIsIdentity);
                            updated++;
                        }
                    }

                    // 3. Insert rows from source that don't exist in target
                    var sourceIdsToInsert = sourceDict.Keys.Except(targetDict.Keys).ToList();
                    foreach (var idStr in sourceIdsToInsert)
                    {
                        var sourceRow = sourceDict[idStr];
                        await InsertRowAsync(connection, transaction, tableName, sourceRow, columns, idIsIdentity);
                        inserted++;
                    }

                    await transaction.CommitAsync();

                    return new UpdateResult
                    {
                        Success = true,
                        Message = $"Table '{tableName}' updated successfully. Deleted: {deleted}, Inserted: {inserted}, Updated: {updated}",
                        RowsDeleted = deleted,
                        RowsInserted = inserted,
                        RowsUpdated = updated
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new UpdateResult
                {
                    Success = false,
                    Message = $"Error updating table: {ex.Message}",
                    Exception = ex.Message
                };
            }
        }

        /// <summary>
        /// Inserts a row into the target table, handling identity columns.
        /// </summary>
        private async Task InsertRowAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string tableName,
            Dictionary<string, object> row,
            List<string> columns,
            bool idIsIdentity)
        {
            var columnNames = columns.Where(c => row.ContainsKey(c)).ToList();
            if (columnNames.Count == 0)
                return;

            if (idIsIdentity)
            {
                // Enable identity insert for the target table
                var enableIdentityCmd = new SqlCommand($"SET IDENTITY_INSERT {tableName} ON", connection, transaction);
                await enableIdentityCmd.ExecuteNonQueryAsync();
            }

            try
            {
                var columnList = string.Join(", ", columnNames);
                var parameterList = string.Join(", ", columnNames.Select(c => $"@{c}"));
                var insertQuery = $"INSERT INTO {tableName} ({columnList}) VALUES ({parameterList})";

                await using var insertCmd = new SqlCommand(insertQuery, connection, transaction);

                foreach (var colName in columnNames)
                {
                    var value = row[colName] ?? DBNull.Value;
                    insertCmd.Parameters.AddWithValue($"@{colName}", value);
                }

                await insertCmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (idIsIdentity)
                {
                    // Disable identity insert
                    var disableIdentityCmd = new SqlCommand($"SET IDENTITY_INSERT {tableName} OFF", connection, transaction);
                    await disableIdentityCmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Checks if a column is an identity column in the database.
        /// </summary>
        private async Task<bool> IsIdentityColumnAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string tableName,
            string columnName)
        {
            try
            {
                var query = $"SELECT COLUMNPROPERTY(OBJECT_ID(@tableName), @columnName, 'IsIdentity')";
                await using var cmd = new SqlCommand(query, connection, transaction);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                cmd.Parameters.AddWithValue("@columnName", columnName);

                var result = await cmd.ExecuteScalarAsync();

                // If result is DBNull or null, the column doesn't exist or is not an identity column
                if (result is DBNull || result == null)
                    return false;

                // COLUMNPROPERTY returns 1 if the column is an identity, 0 otherwise
                return result.Equals(1);
            }
            catch
            {
                // On error, assume it's not an identity column to be safe
                return false;
            }
        }

        /// <summary>
        /// Compares two rows for differences.
        /// </summary>
        private bool RowsDiffer(Dictionary<string, object> row1, Dictionary<string, object> row2)
        {
            var allKeys = row1.Keys.Union(row2.Keys);

            foreach (var key in allKeys)
            {
                row1.TryGetValue(key, out var val1);
                row2.TryGetValue(key, out var val2);

                // Normalize DBNull to null
                if (val1 is DBNull) val1 = null;
                if (val2 is DBNull) val2 = null;

                // Compare values
                if (val1 == null && val2 == null) continue;
                if (val1 == null || val2 == null) return true;
                if (!val1.Equals(val2)) return true;
            }

            return false;
        }
    }

    public class UpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int RowsDeleted { get; set; }
        public int RowsInserted { get; set; }
        public int RowsUpdated { get; set; }
        public string Exception { get; set; }
    }
}
