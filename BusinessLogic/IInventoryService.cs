using System.Collections.Generic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Contract defining high-level inventory domain operations and business policies.
    /// Acts as the boundary between Presentation Layer and Data Access Layer.
    /// </summary>
    public interface IInventoryService
    {
        DashboardMetrics GetDashboardSummary();
        IEnumerable<Product> GetAllProducts();
        Product? GetProductById(int productId);
        IEnumerable<Product> GetUrgentRestockList();
        int CreateProduct(Product product);
        bool UpdateProduct(Product product);
        bool DeleteProduct(int productId);
        long RecordStockTransaction(int productId, string transactionType, int quantity, decimal unitPrice, string? referenceNo, string? notes, int? userId);
        IEnumerable<StockTransaction> GetRecentTransactions(int limit = 100);
        IEnumerable<MonthlyMovementDto> GetMonthlyMovements(int monthsBack = 6);
        IEnumerable<CategoryValuationDto> GetCategoryValuations();
        IEnumerable<TopSellingProductDto> GetTopSellingProducts(int limit = 5);
        IEnumerable<StockTransaction> GetFilteredTransactions(int? year, int? month, int? categoryId, string? movementType, string? searchQuery);
        MonthlyReportSummaryDto GetMonthlyReportSummary(int? year, int? month, int? categoryId);
        ITelegramService TelegramService { get; }

        // Category Management Operations
        IEnumerable<Category> GetAllCategories();
        Category? GetCategoryById(int categoryId);
        int CreateCategory(Category category);
        bool UpdateCategory(Category category);
        bool DeleteCategory(int categoryId);
        int GetProductCountByCategoryId(int categoryId);
    }
}
