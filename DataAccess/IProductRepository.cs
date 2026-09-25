using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// Repository abstraction for Product persistence operations.
    /// Follows the Repository Pattern to decouple Business Logic from SQL commands.
    /// </summary>
    public interface IProductRepository
    {
        IEnumerable<Product> GetAll();
        Product? GetById(int productId);
        Product? GetBySku(string sku);
        IEnumerable<Product> GetLowStockProducts();
        int Insert(Product product);
        bool Update(Product product);
        bool Delete(int productId);
        bool UpdateStockLevel(int productId, int newStock, SqlTransaction? transaction = null);
        bool IncrementStock(int productId, int deltaQuantity, SqlTransaction? transaction = null);
        bool DecrementStock(int productId, int deltaQuantity, SqlTransaction? transaction = null);
    }
}
