using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// ADO.NET implementation of IProductRepository using parameterized SQL statements.
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        static ProductRepository()
        {
            EnsureSchema();
        }

        private static void EnsureSchema()
        {
            try
            {
                const string sql = @"
                    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM sys.columns 
                            WHERE object_id = OBJECT_ID('Products') AND name = 'ImagePath'
                        )
                        BEGIN
                            ALTER TABLE Products ADD ImagePath NVARCHAR(255) NULL;
                        END
                    END";
                DatabaseHelper.ExecuteNonQuery(sql);
            }
            catch
            {
                // Fallback gracefully if database is offline or read-only
            }
        }

        public IEnumerable<Product> GetAll()
        {
            const string sql = @"
                SELECT p.ProductID, p.SKU, p.Barcode, p.ProductName, p.CategoryID, c.CategoryName,
                       p.SupplierID, s.CompanyName AS SupplierName, p.CostPrice, p.SellingPrice, 
                       p.CurrentStock, p.ReorderLevel, p.ImagePath, p.CreatedAt
                FROM Products p
                INNER JOIN Categories c ON p.CategoryID = c.CategoryID
                INNER JOIN Suppliers s ON p.SupplierID = s.SupplierID
                ORDER BY p.ProductName ASC;";

            var dt = DatabaseHelper.ExecuteDataTable(sql);
            var list = new List<Product>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapDataRowToProduct(row));
            }

            return list;
        }

        public Product? GetById(int productId)
        {
            const string sql = @"
                SELECT p.ProductID, p.SKU, p.Barcode, p.ProductName, p.CategoryID, c.CategoryName,
                       p.SupplierID, s.CompanyName AS SupplierName, p.CostPrice, p.SellingPrice, 
                       p.CurrentStock, p.ReorderLevel, p.ImagePath, p.CreatedAt
                FROM Products p
                INNER JOIN Categories c ON p.CategoryID = c.CategoryID
                INNER JOIN Suppliers s ON p.SupplierID = s.SupplierID
                WHERE p.ProductID = @ProductID;";

            var parameters = new[] { new SqlParameter("@ProductID", SqlDbType.Int) { Value = productId } };
            var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, parameters);

            if (dt.Rows.Count == 0) return null;
            return MapDataRowToProduct(dt.Rows[0]);
        }

        public Product? GetBySku(string sku)
        {
            const string sql = @"
                SELECT p.ProductID, p.SKU, p.Barcode, p.ProductName, p.CategoryID, c.CategoryName,
                       p.SupplierID, s.CompanyName AS SupplierName, p.CostPrice, p.SellingPrice, 
                       p.CurrentStock, p.ReorderLevel, p.ImagePath, p.CreatedAt
                FROM Products p
                INNER JOIN Categories c ON p.CategoryID = c.CategoryID
                INNER JOIN Suppliers s ON p.SupplierID = s.SupplierID
                WHERE p.SKU = @SKU;";

            var parameters = new[] { new SqlParameter("@SKU", SqlDbType.NVarChar, 50) { Value = sku } };
            var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, parameters);

            if (dt.Rows.Count == 0) return null;
            return MapDataRowToProduct(dt.Rows[0]);
        }

        public IEnumerable<Product> GetLowStockProducts()
        {
            const string sql = @"
                SELECT p.ProductID, p.SKU, p.Barcode, p.ProductName, p.CategoryID, c.CategoryName,
                       p.SupplierID, s.CompanyName AS SupplierName, p.CostPrice, p.SellingPrice, 
                       p.CurrentStock, p.ReorderLevel, p.ImagePath, p.CreatedAt
                FROM Products p
                INNER JOIN Categories c ON p.CategoryID = c.CategoryID
                INNER JOIN Suppliers s ON p.SupplierID = s.SupplierID
                WHERE p.CurrentStock <= p.ReorderLevel
                ORDER BY p.CurrentStock ASC, p.ProductName ASC;";

            var dt = DatabaseHelper.ExecuteDataTable(sql);
            var list = new List<Product>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapDataRowToProduct(row));
            }

            return list;
        }

        public int Insert(Product product)
        {
            const string sql = @"
                INSERT INTO Products (SKU, Barcode, ProductName, CategoryID, SupplierID, CostPrice, SellingPrice, CurrentStock, ReorderLevel, ImagePath, CreatedAt)
                VALUES (@SKU, @Barcode, @ProductName, @CategoryID, @SupplierID, @CostPrice, @SellingPrice, @CurrentStock, @ReorderLevel, @ImagePath, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var parameters = new[]
            {
                new SqlParameter("@SKU", SqlDbType.NVarChar, 50) { Value = product.SKU },
                new SqlParameter("@Barcode", SqlDbType.NVarChar, 50) { Value = (object?)product.Barcode ?? DBNull.Value },
                new SqlParameter("@ProductName", SqlDbType.NVarChar, 150) { Value = product.ProductName },
                new SqlParameter("@CategoryID", SqlDbType.Int) { Value = product.CategoryID },
                new SqlParameter("@SupplierID", SqlDbType.Int) { Value = product.SupplierID },
                new SqlParameter("@CostPrice", SqlDbType.Decimal) { Value = product.CostPrice },
                new SqlParameter("@SellingPrice", SqlDbType.Decimal) { Value = product.SellingPrice },
                new SqlParameter("@CurrentStock", SqlDbType.Int) { Value = product.CurrentStock },
                new SqlParameter("@ReorderLevel", SqlDbType.Int) { Value = product.ReorderLevel },
                new SqlParameter("@ImagePath", SqlDbType.NVarChar, 255) { Value = (object?)product.ImagePath ?? DBNull.Value }
            };

            var result = DatabaseHelper.ExecuteScalar(sql, CommandType.Text, parameters);
            return Convert.ToInt32(result);
        }

        public bool Update(Product product)
        {
            const string sql = @"
                UPDATE Products
                SET SKU = @SKU,
                    Barcode = @Barcode,
                    ProductName = @ProductName,
                    CategoryID = @CategoryID,
                    SupplierID = @SupplierID,
                    CostPrice = @CostPrice,
                    SellingPrice = @SellingPrice,
                    ReorderLevel = @ReorderLevel,
                    ImagePath = @ImagePath
                WHERE ProductID = @ProductID;";

            var parameters = new[]
            {
                new SqlParameter("@ProductID", SqlDbType.Int) { Value = product.ProductID },
                new SqlParameter("@SKU", SqlDbType.NVarChar, 50) { Value = product.SKU },
                new SqlParameter("@Barcode", SqlDbType.NVarChar, 50) { Value = (object?)product.Barcode ?? DBNull.Value },
                new SqlParameter("@ProductName", SqlDbType.NVarChar, 150) { Value = product.ProductName },
                new SqlParameter("@CategoryID", SqlDbType.Int) { Value = product.CategoryID },
                new SqlParameter("@SupplierID", SqlDbType.Int) { Value = product.SupplierID },
                new SqlParameter("@CostPrice", SqlDbType.Decimal) { Value = product.CostPrice },
                new SqlParameter("@SellingPrice", SqlDbType.Decimal) { Value = product.SellingPrice },
                new SqlParameter("@ReorderLevel", SqlDbType.Int) { Value = product.ReorderLevel },
                new SqlParameter("@ImagePath", SqlDbType.NVarChar, 255) { Value = (object?)product.ImagePath ?? DBNull.Value }
            };

            return DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters) > 0;
        }

        public bool Delete(int productId)
        {
            const string sql = "DELETE FROM Products WHERE ProductID = @ProductID;";
            var parameters = new[] { new SqlParameter("@ProductID", SqlDbType.Int) { Value = productId } };
            return DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters) > 0;
        }

        public bool UpdateStockLevel(int productId, int newStock, SqlTransaction? transaction = null)
        {
            const string sql = "UPDATE Products SET CurrentStock = @NewStock WHERE ProductID = @ProductID;";
            var parameters = new[]
            {
                new SqlParameter("@ProductID", SqlDbType.Int) { Value = productId },
                new SqlParameter("@NewStock", SqlDbType.Int) { Value = newStock }
            };
            return DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters, transaction) > 0;
        }

        public bool IncrementStock(int productId, int deltaQuantity, SqlTransaction? transaction = null)
        {
            const string sql = "UPDATE Products SET CurrentStock = CurrentStock + @Delta WHERE ProductID = @ProductID;";
            var parameters = new[]
            {
                new SqlParameter("@ProductID", SqlDbType.Int) { Value = productId },
                new SqlParameter("@Delta", SqlDbType.Int) { Value = deltaQuantity }
            };
            return DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters, transaction) > 0;
        }

        public bool DecrementStock(int productId, int deltaQuantity, SqlTransaction? transaction = null)
        {
            const string sql = "UPDATE Products SET CurrentStock = CurrentStock - @Delta WHERE ProductID = @ProductID;";
            var parameters = new[]
            {
                new SqlParameter("@ProductID", SqlDbType.Int) { Value = productId },
                new SqlParameter("@Delta", SqlDbType.Int) { Value = deltaQuantity }
            };
            return DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters, transaction) > 0;
        }

        private static Product MapDataRowToProduct(DataRow row)
        {
            return new Product
            {
                ProductID = Convert.ToInt32(row["ProductID"]),
                SKU = row["SKU"].ToString() ?? string.Empty,
                Barcode = row["Barcode"] == DBNull.Value ? null : row["Barcode"].ToString(),
                ProductName = row["ProductName"].ToString() ?? string.Empty,
                CategoryID = Convert.ToInt32(row["CategoryID"]),
                CategoryName = row["CategoryName"].ToString() ?? string.Empty,
                SupplierID = Convert.ToInt32(row["SupplierID"]),
                SupplierName = row["SupplierName"].ToString() ?? string.Empty,
                CostPrice = Convert.ToDecimal(row["CostPrice"]),
                SellingPrice = Convert.ToDecimal(row["SellingPrice"]),
                CurrentStock = Convert.ToInt32(row["CurrentStock"]),
                ReorderLevel = Convert.ToInt32(row["ReorderLevel"]),
                ImagePath = row.Table.Columns.Contains("ImagePath") && row["ImagePath"] != DBNull.Value ? row["ImagePath"].ToString() : null,
                CreatedAt = Convert.ToDateTime(row["CreatedAt"])
            };
        }
    }
}
