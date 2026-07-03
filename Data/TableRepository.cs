using Microsoft.Data.SqlClient;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace TrixCompareDb.Data
{
    public class TableRepository
    {
        private readonly IConfiguration _config;

        public TableRepository(IConfiguration config)
        {
            _config = config;
        }

        public async Task<List<Dictionary<string, object>>> GetTable(string dbName, string table)
        {
            var result = new List<Dictionary<string, object>>();

            var connString = _config.GetConnectionString(dbName);

            // If the provided dbName is not a connection-string key, try to find a connection
            // whose database/catalog matches the given name (e.g. 'trixCompareDb').
            if (string.IsNullOrEmpty(connString))
            {
                var section = _config.GetSection("ConnectionStrings");
                foreach (var child in section.GetChildren())
                {
                    var candidate = child.Value;
                    if (string.IsNullOrEmpty(candidate))
                        continue;
                    try
                    {
                        var builder = new SqlConnectionStringBuilder(candidate);
                        // InitialCatalog holds the database name
                        if (string.Equals(builder.InitialCatalog, dbName, StringComparison.OrdinalIgnoreCase))
                        {
                            connString = candidate;
                            break;
                        }
                    }
                    catch
                    {
                        // ignore invalid connection strings
                    }
                }
            }

            if (string.IsNullOrEmpty(connString))
            {
                var available = string.Join(", ", _config.GetSection("ConnectionStrings").GetChildren().Select(c => c.Key));
                throw new InvalidOperationException($"Connection string '{dbName}' not found. Available keys: {available}");
            }

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync();

            var query = $"SELECT * FROM {table}";

            await using var cmd = new SqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                result.Add(row);
            }

            return result;
        }

        public System.Collections.Generic.List<string> GetDatabaseKeys()
        {
            var section = _config.GetSection("ConnectionStrings");
            var keys = new System.Collections.Generic.List<string>();
            foreach (var child in section.GetChildren())
            {
                keys.Add(child.Key);
            }
            return keys;
        }

        public async Task<System.Collections.Generic.List<string>> GetTablesAsync(string dbName)
        {
            var tables = new System.Collections.Generic.List<string>();

            var connString = _config.GetConnectionString(dbName);
            if (string.IsNullOrEmpty(connString))
            {
                var section = _config.GetSection("ConnectionStrings");
                foreach (var child in section.GetChildren())
                {
                    var candidate = child.Value;
                    if (string.IsNullOrEmpty(candidate))
                        continue;
                    try
                    {
                        var builder = new SqlConnectionStringBuilder(candidate);
                        if (string.Equals(builder.InitialCatalog, dbName, StringComparison.OrdinalIgnoreCase))
                        {
                            connString = candidate;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(connString))
                throw new InvalidOperationException($"Connection string '{dbName}' not found.");

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync();

            var query = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME";
            await using var cmd = new SqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                // return schema-qualified name (e.g. dbo.Products)
                tables.Add($"{schema}.{name}");
            }

            return tables;
        }

        /// <summary>
        /// Resolves a database name/key to a connection string.
        /// Follows the same logic as GetTable to ensure consistent database identification.
        /// </summary>
        public string GetConnectionString(string dbName)
        {
            var connString = _config.GetConnectionString(dbName);

            if (string.IsNullOrEmpty(connString))
            {
                var section = _config.GetSection("ConnectionStrings");
                foreach (var child in section.GetChildren())
                {
                    var candidate = child.Value;
                    if (string.IsNullOrEmpty(candidate))
                        continue;
                    try
                    {
                        var builder = new SqlConnectionStringBuilder(candidate);
                        if (string.Equals(builder.InitialCatalog, dbName, StringComparison.OrdinalIgnoreCase))
                        {
                            connString = candidate;
                            break;
                        }
                    }
                    catch
                    {
                        // ignore invalid connection strings
                    }
                }
            }

            return connString;
        }
    }
}
