using System.Collections.Generic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// Contract defining Category data persistence operations.
    /// Supports querying, creation, updating, deletion, and relationship integrity checking.
    /// </summary>
    public interface ICategoryRepository
    {
        IEnumerable<Category> GetAll();
        Category? GetById(int categoryId);
        Category? GetByName(string categoryName);
        int Insert(Category category);
        bool Update(Category category);
        bool Delete(int categoryId);
        int GetProductCount(int categoryId);
    }
}
