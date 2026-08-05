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

        /// <summary>
        /// Gets a table from a database using legacy connection string lookup.
        /// Kept for backward compatibility.
        /// </summary>
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

        /// <summary>
        /// Gets a table from a database using Azure AD Interactive authentication.
        /// Uses Active Directory Interactive for browser-based MFA.
        /// Includes retry logic for transient faults on Azure SQL.
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetTableWithAzureADAsync(
            string serverName,
            string databaseName,
            string userEmail,
            string tableName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("Server name cannot be empty.", nameof(serverName));
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));
            if (string.IsNullOrWhiteSpace(userEmail))
                throw new ArgumentException("User email cannot be empty.", nameof(userEmail));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty.", nameof(tableName));

            // Retry logic for transient faults - Azure SQL can disconnect temporarily
            const int maxRetries = 3;
            int attempt = 0;

            while (true)
            {
                attempt++;
                try
                {
                    return await GetTableWithAzureADInternalAsync(serverName, databaseName, userEmail, tableName);
                }
                catch (SqlException ex) when (IsTransientError(ex) && attempt < maxRetries)
                {
                    // Transient error - wait and retry
                    int delayMs = (int)Math.Pow(2, attempt) * 1000; // Exponential backoff: 2s, 4s
                    await Task.Delay(delayMs);
                    continue;
                }
            }
        }

        /// <summary>
        /// Internal method for Azure AD table retrieval (called by retry wrapper).
        /// </summary>
        private async Task<List<Dictionary<string, object>>> GetTableWithAzureADInternalAsync(
            string serverName,
            string databaseName,
            string userEmail,
            string tableName)
        {
            var result = new List<Dictionary<string, object>>();

            // Build connection string with Azure AD Interactive authentication
            // Format: Server=<serverName>;Database=<databaseName>;Authentication=Active Directory Interactive;
            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = databaseName,
                Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive,
                ConnectTimeout = 60,  // Increased timeout for MFA
                Encrypt = true,
                TrustServerCertificate = false
            };

            var connString = connectionStringBuilder.ConnectionString;

            try
            {
                await using var conn = new SqlConnection(connString);
                // This will trigger the interactive browser-based login
                await conn.OpenAsync();

                var query = $"SELECT * FROM {tableName}";

                await using var cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 120;  // Increased command timeout for large result sets
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
            }
            catch (SqlException ex) when (ex.Number == 18456)
            {
                // Login failed
                throw new InvalidOperationException(
                    $"Authentication failed for user '{userEmail}' on server '{serverName}'. Please verify your credentials and MFA.",
                    ex);
            }
            catch (SqlException ex) when (ex.Number == 40550 || ex.Number == 40551 || ex.Number == 40552 || ex.Number == 40553)
            {
                // Too many connections or session limit exceeded
                throw new InvalidOperationException(
                    $"Server '{serverName}' has reached connection limit. Please try again later.",
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                // User cancelled MFA or timeout
                throw new InvalidOperationException(
                    $"Authentication was cancelled or timed out. Please try again.",
                    ex);
            }
            catch (Exception ex)
            {
                // Generic error
                throw new InvalidOperationException(
                    $"Failed to connect to '{serverName}' database '{databaseName}': {ex.Message}",
                    ex);
            }

            return result;
        }

        /// <summary>
        /// Determines if a SQL exception represents a transient fault that can be retried.
        /// Azure SQL transient error codes: 40197, 40501, 40613, 64, 233, 20, 40540, 40544, 40549, 40550, 40551, 40552, 40553, etc.
        /// </summary>
        private bool IsTransientError(SqlException ex)
        {
            // List of Azure SQL transient error codes
            int[] transientErrorNumbers = new[]
            {
                40197,  // Temporary error
                40501,  // Service is busy
                40613,  // Database unavailable
                64,     // Communication link failure
                233,    // Connection init error
                20,     // Instance not found
                40540,  // Service has encountered an error
                40544,  // Resource limit reached
                40549,  // Session terminated (long transaction)
                40550,  // Session terminated (too many locks)
                40551,  // Session terminated (excessive log usage)
                40552,  // Session terminated (excessive transaction log space)
                40553,  // Session terminated (excessive memory usage)
                40540,  // Service has encountered an error
                40546,  // Service has encountered an error
                40547,  // Service has encountered an error
                40548,  // Service has encountered an error
            };

            return transientErrorNumbers.Contains(ex.Number);
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
        /// Gets list of tables from a database using Azure AD Interactive authentication.
        /// Includes retry logic for transient faults.
        /// </summary>
        public async Task<System.Collections.Generic.List<string>> GetTablesWithAzureADAsync(
            string serverName,
            string databaseName,
            string userEmail)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("Server name cannot be empty.", nameof(serverName));
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));
            if (string.IsNullOrWhiteSpace(userEmail))
                throw new ArgumentException("User email cannot be empty.", nameof(userEmail));

            // Retry logic for transient faults
            const int maxRetries = 3;
            int attempt = 0;

            while (true)
            {
                attempt++;
                try
                {
                    return await GetTablesWithAzureADInternalAsync(serverName, databaseName, userEmail);
                }
                catch (SqlException ex) when (IsTransientError(ex) && attempt < maxRetries)
                {
                    // Transient error - wait and retry
                    int delayMs = (int)Math.Pow(2, attempt) * 1000;
                    await Task.Delay(delayMs);
                    continue;
                }
            }
        }

        /// <summary>
        /// Internal method for table retrieval with Azure AD (called by retry wrapper).
        /// </summary>
        private async Task<System.Collections.Generic.List<string>> GetTablesWithAzureADInternalAsync(
            string serverName,
            string databaseName,
            string userEmail)
        {
            var tables = new System.Collections.Generic.List<string>();

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = databaseName,
                Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive,
                ConnectTimeout = 60,
                Encrypt = true,
                TrustServerCertificate = false
            };

            try
            {
                await using var conn = new SqlConnection(connectionStringBuilder.ConnectionString);
                await conn.OpenAsync();

                var query = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME";
                await using var cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 120;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var schema = reader.GetString(0);
                    var name = reader.GetString(1);
                    tables.Add($"{schema}.{name}");
                }
            }
            catch (SqlException ex) when (ex.Number == 18456)
            {
                throw new InvalidOperationException(
                    $"Authentication failed for user '{userEmail}' on server '{serverName}'. Please verify your credentials and MFA.",
                    ex);
            }
            catch (OperationCanceledException ex)
            {
                throw new InvalidOperationException(
                    $"Authentication was cancelled or timed out. Please try again.",
                    ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve tables from '{serverName}': {ex.Message}",
                    ex);
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
