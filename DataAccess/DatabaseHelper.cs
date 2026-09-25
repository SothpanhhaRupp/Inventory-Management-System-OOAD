using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// Core ADO.NET utility providing robust connection pooling, parameterized query execution,
    /// and atomic transaction lifecycle management.
    /// </summary>
    public class DatabaseHelper
    {
        private static string _connectionString = 
            "Server=MSI-GF63-THIN\\SQLEXPRESS;Database=InventoryDB;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;";

        public static string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value;
        }

        static DatabaseHelper()
        {
            // Allow override via app.config or environment variable
            string? configuredConn = ConfigurationManager.ConnectionStrings["InventoryDB"]?.ConnectionString;
            if (!string.IsNullOrWhiteSpace(configuredConn))
            {
                _connectionString = configuredConn;
            }
        }

        /// <summary>
        /// Creates and opens a new managed SqlConnection.
        /// </summary>
        public static SqlConnection CreateOpenConnection()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Tests if the database connection can be established.
        /// </summary>
        public static bool TestConnection(out string? errorMessage)
        {
            try
            {
                using var conn = CreateOpenConnection();
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Executes a non-query command (INSERT, UPDATE, DELETE).
        /// Supports ambient SqlTransaction for atomic multi-statement operations.
        /// </summary>
        public static int ExecuteNonQuery(string commandText, CommandType commandType = CommandType.Text, 
            SqlParameter[]? parameters = null, SqlTransaction? transaction = null)
        {
            if (transaction != null)
            {
                using var cmd = CreateCommand(transaction.Connection, commandText, commandType, parameters, transaction);
                return cmd.ExecuteNonQuery();
            }
            else
            {
                using var conn = CreateOpenConnection();
                using var cmd = CreateCommand(conn, commandText, commandType, parameters, null);
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a query and returns the first column of the first row (e.g. SCOPE_IDENTITY() or COUNT).
        /// </summary>
        public static object? ExecuteScalar(string commandText, CommandType commandType = CommandType.Text, 
            SqlParameter[]? parameters = null, SqlTransaction? transaction = null)
        {
            if (transaction != null)
            {
                using var cmd = CreateCommand(transaction.Connection, commandText, commandType, parameters, transaction);
                return cmd.ExecuteScalar();
            }
            else
            {
                using var conn = CreateOpenConnection();
                using var cmd = CreateCommand(conn, commandText, commandType, parameters, null);
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// Executes a SELECT command and fills a DataTable.
        /// </summary>
        public static DataTable ExecuteDataTable(string commandText, CommandType commandType = CommandType.Text, 
            SqlParameter[]? parameters = null, SqlTransaction? transaction = null)
        {
            var dataTable = new DataTable();

            if (transaction != null)
            {
                using var cmd = CreateCommand(transaction.Connection, commandText, commandType, parameters, transaction);
                using var adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dataTable);
            }
            else
            {
                using var conn = CreateOpenConnection();
                using var cmd = CreateCommand(conn, commandText, commandType, parameters, null);
                using var adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dataTable);
            }

            return dataTable;
        }

        /// <summary>
        /// Helper to construct and parameterize SqlCommand instances cleanly.
        /// </summary>
        private static SqlCommand CreateCommand(SqlConnection connection, string commandText, 
            CommandType commandType, SqlParameter[]? parameters, SqlTransaction? transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = commandText;
            cmd.CommandType = commandType;
            cmd.CommandTimeout = 30;

            if (transaction != null)
                cmd.Transaction = transaction;

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var p in parameters)
                {
                    if (p != null)
                    {
                        // Guard against DBNull assignment for null values
                        p.Value ??= DBNull.Value;
                        cmd.Parameters.Add(p);
                    }
                }
            }

            return cmd;
        }
    }
}
