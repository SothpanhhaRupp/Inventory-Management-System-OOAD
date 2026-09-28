using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// ADO.NET SQL Server implementation of ICategoryRepository.
    /// Manages category persistence and provides offline demo fallback resilience.
    /// </summary>
    public class CategoryRepository : ICategoryRepository
    {
        // Thread-safe fallback cache for offline / demo mode
        private static readonly List<Category> DemoCategories = new()
        {
            new Category { CategoryID = 1, CategoryName = "Electronics", Description = "High-value consumer and enterprise electronic hardware and accessories", ProductCount = 2 },
            new Category { CategoryID = 2, CategoryName = "Beverages", Description = "Bottled, canned, and packaged drinks for wholesale distribution", ProductCount = 2 },
            new Category { CategoryID = 3, CategoryName = "Perishables", Description = "Fresh food items, dairy, and cold-chain inventory", ProductCount = 2 },
            new Category { CategoryID = 4, CategoryName = "Office Supplies", Description = "Stationery, paper, printer consumables, and general desk utilities", ProductCount = 2 }
        };

        public IEnumerable<Category> GetAll()
        {
            const string sql = @"
                SELECT c.CategoryID, c.CategoryName, c.Description, COUNT(p.ProductID) AS ProductCount
                FROM Categories c
                LEFT JOIN Products p ON c.CategoryID = p.CategoryID
                GROUP BY c.CategoryID, c.CategoryName, c.Description
                ORDER BY c.CategoryName ASC;";

            try
            {
                var dt = DatabaseHelper.ExecuteDataTable(sql);
                var list = new List<Category>();

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(MapDataRowToCategory(row));
                }

                return list;
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    return DemoCategories.OrderBy(c => c.CategoryName).Select(c => new Category
                    {
                        CategoryID = c.CategoryID,
                        CategoryName = c.CategoryName,
                        Description = c.Description,
                        ProductCount = c.ProductCount
                    }).ToList();
                }
            }
        }

        public Category? GetById(int categoryId)
        {
            const string sql = @"
                SELECT c.CategoryID, c.CategoryName, c.Description, COUNT(p.ProductID) AS ProductCount
                FROM Categories c
                LEFT JOIN Products p ON c.CategoryID = p.CategoryID
                WHERE c.CategoryID = @CategoryID
                GROUP BY c.CategoryID, c.CategoryName, c.Description;";

            try
            {
                var parameters = new[] { new SqlParameter("@CategoryID", SqlDbType.Int) { Value = categoryId } };
                var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, parameters);

                if (dt.Rows.Count == 0) return null;
                return MapDataRowToCategory(dt.Rows[0]);
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    var found = DemoCategories.FirstOrDefault(c => c.CategoryID == categoryId);
                    if (found == null) return null;
                    return new Category
                    {
                        CategoryID = found.CategoryID,
                        CategoryName = found.CategoryName,
                        Description = found.Description,
                        ProductCount = found.ProductCount
                    };
                }
            }
        }

        public Category? GetByName(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return null;

            const string sql = @"
                SELECT c.CategoryID, c.CategoryName, c.Description, COUNT(p.ProductID) AS ProductCount
                FROM Categories c
                LEFT JOIN Products p ON c.CategoryID = p.CategoryID
                WHERE LOWER(c.CategoryName) = LOWER(@CategoryName)
                GROUP BY c.CategoryID, c.CategoryName, c.Description;";

            try
            {
                var parameters = new[] { new SqlParameter("@CategoryName", SqlDbType.NVarChar, 100) { Value = categoryName.Trim() } };
                var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, parameters);

                if (dt.Rows.Count == 0) return null;
                return MapDataRowToCategory(dt.Rows[0]);
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    var found = DemoCategories.FirstOrDefault(c => string.Equals(c.CategoryName, categoryName.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (found == null) return null;
                    return new Category
                    {
                        CategoryID = found.CategoryID,
                        CategoryName = found.CategoryName,
                        Description = found.Description,
                        ProductCount = found.ProductCount
                    };
                }
            }
        }

        public int Insert(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));

            const string sql = @"
                INSERT INTO Categories (CategoryName, Description)
                VALUES (@CategoryName, @Description);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@CategoryName", SqlDbType.NVarChar, 100) { Value = category.CategoryName.Trim() },
                    new SqlParameter("@Description", SqlDbType.NVarChar, 255) { Value = (object?)category.Description?.Trim() ?? DBNull.Value }
                };

                object? result = DatabaseHelper.ExecuteScalar(sql, CommandType.Text, parameters);
                category.CategoryID = result != null ? Convert.ToInt32(result) : 0;
                return category.CategoryID;
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    int nextId = DemoCategories.Count > 0 ? DemoCategories.Max(c => c.CategoryID) + 1 : 1;
                    category.CategoryID = nextId;
                    DemoCategories.Add(new Category
                    {
                        CategoryID = nextId,
                        CategoryName = category.CategoryName.Trim(),
                        Description = category.Description?.Trim(),
                        ProductCount = 0
                    });
                    return nextId;
                }
            }
        }

        public bool Update(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));

            const string sql = @"
                UPDATE Categories
                SET CategoryName = @CategoryName,
                    Description = @Description
                WHERE CategoryID = @CategoryID;";

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@CategoryID", SqlDbType.Int) { Value = category.CategoryID },
                    new SqlParameter("@CategoryName", SqlDbType.NVarChar, 100) { Value = category.CategoryName.Trim() },
                    new SqlParameter("@Description", SqlDbType.NVarChar, 255) { Value = (object?)category.Description?.Trim() ?? DBNull.Value }
                };

                int rowsAffected = DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters);
                return rowsAffected > 0;
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    var found = DemoCategories.FirstOrDefault(c => c.CategoryID == category.CategoryID);
                    if (found != null)
                    {
                        found.CategoryName = category.CategoryName.Trim();
                        found.Description = category.Description?.Trim();
                        return true;
                    }
                    return false;
                }
            }
        }

        public bool Delete(int categoryId)
        {
            const string sql = @"DELETE FROM Categories WHERE CategoryID = @CategoryID;";

            try
            {
                var parameters = new[] { new SqlParameter("@CategoryID", SqlDbType.Int) { Value = categoryId } };
                int rowsAffected = DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, parameters);
                return rowsAffected > 0;
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    int removed = DemoCategories.RemoveAll(c => c.CategoryID == categoryId);
                    return removed > 0;
                }
            }
        }

        public int GetProductCount(int categoryId)
        {
            const string sql = @"SELECT COUNT(*) FROM Products WHERE CategoryID = @CategoryID;";

            try
            {
                var parameters = new[] { new SqlParameter("@CategoryID", SqlDbType.Int) { Value = categoryId } };
                object? result = DatabaseHelper.ExecuteScalar(sql, CommandType.Text, parameters);
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception)
            {
                lock (DemoCategories)
                {
                    var found = DemoCategories.FirstOrDefault(c => c.CategoryID == categoryId);
                    return found?.ProductCount ?? 0;
                }
            }
        }

        private static Category MapDataRowToCategory(DataRow row)
        {
            return new Category
            {
                CategoryID = Convert.ToInt32(row["CategoryID"]),
                CategoryName = Convert.ToString(row["CategoryName"]) ?? string.Empty,
                Description = row["Description"] == DBNull.Value ? null : Convert.ToString(row["Description"]),
                ProductCount = row.Table.Columns.Contains("ProductCount") && row["ProductCount"] != DBNull.Value
                    ? Convert.ToInt32(row["ProductCount"])
                    : 0
            };
        }
    }
}
