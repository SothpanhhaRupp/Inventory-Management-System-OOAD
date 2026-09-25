using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Inventory_Management_System.Exceptions;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// ADO.NET implementation of ITransactionRepository.
    /// Manages ACID-compliant atomic transactions across Products and StockTransactions tables.
    /// </summary>
    public class TransactionRepository : ITransactionRepository
    {
        /// <summary>
        /// Atomically updates Product.CurrentStock and inserts a record into StockTransactions.
        /// Rolls back fully if any step fails.
        /// </summary>
        public long RecordStockTransaction(StockTransaction transaction, SqlTransaction? externalTransaction = null)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            // If an external transaction is provided, participate in it; otherwise manage our own local transaction
            bool isLocalTransaction = (externalTransaction == null);
            SqlConnection conn = isLocalTransaction ? DatabaseHelper.CreateOpenConnection() : externalTransaction!.Connection;
            SqlTransaction sqlTx = isLocalTransaction ? conn.BeginTransaction(IsolationLevel.RepeatableRead) : externalTransaction!;

            try
            {
                // 1. Lock and retrieve current product stock to prevent concurrent race conditions
                const string queryStockSql = @"
                    SELECT SKU, CurrentStock 
                    FROM Products WITH (UPDLOCK, ROWLOCK) 
                    WHERE ProductID = @ProductID;";

                string sku = transaction.SKU;
                int currentStock = 0;

                using (var cmdStock = new SqlCommand(queryStockSql, conn, sqlTx))
                {
                    cmdStock.Parameters.Add(new SqlParameter("@ProductID", SqlDbType.Int) { Value = transaction.ProductID });
                    using var reader = cmdStock.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new EntityNotFoundException("Product", transaction.ProductID);
                    }
                    sku = reader["SKU"].ToString() ?? sku;
                    currentStock = Convert.ToInt32(reader["CurrentStock"]);
                }

                // 2. Business validation within the transaction boundary
                if (transaction.TransactionType == "OUT")
                {
                    if (currentStock < transaction.Quantity)
                    {
                        throw new InsufficientStockException(transaction.ProductID, sku, currentStock, transaction.Quantity);
                    }
                }

                // 3. Update Product stock balance
                string updateStockSql;
                if (transaction.TransactionType == "IN")
                {
                    updateStockSql = "UPDATE Products SET CurrentStock = CurrentStock + @Quantity WHERE ProductID = @ProductID;";
                }
                else if (transaction.TransactionType == "OUT")
                {
                    updateStockSql = "UPDATE Products SET CurrentStock = CurrentStock - @Quantity WHERE ProductID = @ProductID;";
                }
                else // ADJUSTMENT
                {
                    updateStockSql = "UPDATE Products SET CurrentStock = @Quantity WHERE ProductID = @ProductID;";
                }

                using (var cmdUpdate = new SqlCommand(updateStockSql, conn, sqlTx))
                {
                    cmdUpdate.Parameters.Add(new SqlParameter("@ProductID", SqlDbType.Int) { Value = transaction.ProductID });
                    cmdUpdate.Parameters.Add(new SqlParameter("@Quantity", SqlDbType.Int) { Value = transaction.Quantity });
                    cmdUpdate.ExecuteNonQuery();
                }

                // 4. Insert into StockTransactions table
                const string insertTxSql = @"
                    INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, UnitPrice, ReferenceNo, Notes, CreatedBy, TransactionDate)
                    VALUES (@ProductID, @TransactionType, @Quantity, @UnitPrice, @ReferenceNo, @Notes, @CreatedBy, GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                long newTxId;
                using (var cmdInsert = new SqlCommand(insertTxSql, conn, sqlTx))
                {
                    cmdInsert.Parameters.Add(new SqlParameter("@ProductID", SqlDbType.Int) { Value = transaction.ProductID });
                    cmdInsert.Parameters.Add(new SqlParameter("@TransactionType", SqlDbType.NVarChar, 10) { Value = transaction.TransactionType });
                    cmdInsert.Parameters.Add(new SqlParameter("@Quantity", SqlDbType.Int) { Value = transaction.Quantity });
                    cmdInsert.Parameters.Add(new SqlParameter("@UnitPrice", SqlDbType.Decimal) { Value = transaction.UnitPrice });
                    cmdInsert.Parameters.Add(new SqlParameter("@ReferenceNo", SqlDbType.NVarChar, 100) { Value = (object?)transaction.ReferenceNo ?? DBNull.Value });
                    cmdInsert.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, 255) { Value = (object?)transaction.Notes ?? DBNull.Value });
                    cmdInsert.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.Int) { Value = (object?)transaction.CreatedBy ?? DBNull.Value });

                    newTxId = Convert.ToInt64(cmdInsert.ExecuteScalar());
                }

                // Commit if this was locally initiated
                if (isLocalTransaction)
                {
                    sqlTx.Commit();
                }

                return newTxId;
            }
            catch
            {
                if (isLocalTransaction && sqlTx != null && sqlTx.Connection != null)
                {
                    try { sqlTx.Rollback(); } catch { /* Suppress secondary rollback exceptions */ }
                }
                throw;
            }
            finally
            {
                if (isLocalTransaction && conn != null)
                {
                    conn.Dispose();
                }
            }
        }

        public IEnumerable<StockTransaction> GetRecentTransactions(int limit = 50)
        {
            string sql = $@"
                SELECT TOP ({limit})
                    t.TransactionID, t.ProductID, p.ProductName, p.SKU,
                    t.TransactionType, t.Quantity, t.UnitPrice, t.ReferenceNo,
                    t.Notes, t.CreatedBy, ISNULL(u.FullName, 'System') AS CreatedByName,
                    t.TransactionDate
                FROM StockTransactions t
                INNER JOIN Products p ON t.ProductID = p.ProductID
                LEFT JOIN Users u ON t.CreatedBy = u.UserID
                ORDER BY t.TransactionDate DESC, t.TransactionID DESC;";

            var dt = DatabaseHelper.ExecuteDataTable(sql);
            var list = new List<StockTransaction>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new StockTransaction
                {
                    TransactionID = Convert.ToInt64(row["TransactionID"]),
                    ProductID = Convert.ToInt32(row["ProductID"]),
                    ProductName = row["ProductName"].ToString() ?? string.Empty,
                    SKU = row["SKU"].ToString() ?? string.Empty,
                    TransactionType = row["TransactionType"].ToString() ?? string.Empty,
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    UnitPrice = Convert.ToDecimal(row["UnitPrice"]),
                    ReferenceNo = row["ReferenceNo"] == DBNull.Value ? null : row["ReferenceNo"].ToString(),
                    Notes = row["Notes"] == DBNull.Value ? null : row["Notes"].ToString(),
                    CreatedBy = row["CreatedBy"] == DBNull.Value ? null : Convert.ToInt32(row["CreatedBy"]),
                    CreatedByName = row["CreatedByName"].ToString() ?? "System",
                    TransactionDate = Convert.ToDateTime(row["TransactionDate"])
                });
            }

            return list;
        }

        public IEnumerable<MonthlyMovementDto> GetMonthlyMovements(int monthsBack = 6)
        {
            // Execute stored procedure or fallback inline query
            const string sql = @"
                SELECT 
                    YEAR(TransactionDate) AS [Year],
                    MONTH(TransactionDate) AS [Month],
                    DATENAME(MONTH, TransactionDate) + ' ' + CAST(YEAR(TransactionDate) AS VARCHAR(4)) AS MonthLabel,
                    ISNULL(SUM(CASE WHEN TransactionType = 'IN' THEN Quantity ELSE 0 END), 0) AS StockInQuantity,
                    ISNULL(SUM(CASE WHEN TransactionType = 'OUT' THEN Quantity ELSE 0 END), 0) AS StockOutQuantity
                FROM StockTransactions
                WHERE TransactionDate >= DATEADD(MONTH, -@MonthsBack, GETDATE())
                GROUP BY YEAR(TransactionDate), MONTH(TransactionDate), DATENAME(MONTH, TransactionDate)
                ORDER BY [Year] ASC, [Month] ASC;";

            var parameters = new[] { new SqlParameter("@MonthsBack", SqlDbType.Int) { Value = monthsBack } };
            var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, parameters);
            var list = new List<MonthlyMovementDto>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new MonthlyMovementDto
                {
                    Year = Convert.ToInt32(row["Year"]),
                    Month = Convert.ToInt32(row["Month"]),
                    MonthLabel = row["MonthLabel"].ToString() ?? string.Empty,
                    StockInQuantity = Convert.ToInt32(row["StockInQuantity"]),
                    StockOutQuantity = Convert.ToInt32(row["StockOutQuantity"])
                });
            }

            return list;
        }

        public IEnumerable<CategoryValuationDto> GetCategoryValuations()
        {
            const string sql = @"
                SELECT 
                    c.CategoryID,
                    c.CategoryName,
                    COUNT(p.ProductID) AS ProductCount,
                    ISNULL(SUM(p.CurrentStock), 0) AS TotalUnits,
                    ISNULL(SUM(p.CurrentStock * p.CostPrice), 0.00) AS TotalValuation
                FROM Categories c
                LEFT JOIN Products p ON c.CategoryID = p.CategoryID
                GROUP BY c.CategoryID, c.CategoryName
                ORDER BY TotalValuation DESC;";

            var dt = DatabaseHelper.ExecuteDataTable(sql);
            var list = new List<CategoryValuationDto>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new CategoryValuationDto
                {
                    CategoryID = Convert.ToInt32(row["CategoryID"]),
                    CategoryName = row["CategoryName"].ToString() ?? string.Empty,
                    ProductCount = Convert.ToInt32(row["ProductCount"]),
                    TotalUnits = Convert.ToInt32(row["TotalUnits"]),
                    TotalValuation = Convert.ToDecimal(row["TotalValuation"])
                });
            }

            return list;
        }
    }
}
