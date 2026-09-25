using System;
using Inventory_Management_System.Exceptions;

namespace Inventory_Management_System.Models
{
    /// <summary>
    /// Represents a system user with role-based access control.
    /// </summary>
    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff"; // "Admin" or "Staff"
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Represents an inventory category entity.
    /// </summary>
    public class Category
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public override string ToString() => CategoryName;
    }

    /// <summary>
    /// Represents a vendor/supplier entity.
    /// </summary>
    public class Supplier
    {
        public int SupplierID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }

        public override string ToString() => CompanyName;
    }

    /// <summary>
    /// Domain Entity: Product
    /// Demonstrates encapsulation by maintaining stock state invariants and valuation logic.
    /// </summary>
    public class Product
    {
        public int ProductID { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; } = 10;
        public string? ImagePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Encapsulated Business Calculations & State Evaluation
        public decimal TotalValuation => CurrentStock * CostPrice;
        public decimal Margin => SellingPrice > 0 ? (SellingPrice - CostPrice) : 0m;
        public decimal MarginPercentage => CostPrice > 0 ? Math.Round(((SellingPrice - CostPrice) / CostPrice) * 100, 2) : 0m;

        public bool IsOutOfStock() => CurrentStock <= 0;

        public bool IsLowStock() => CurrentStock > 0 && CurrentStock <= ReorderLevel;

        public string EvaluateStockStatus()
        {
            if (CurrentStock <= 0) return "Out of Stock";
            if (CurrentStock <= ReorderLevel) return "Low Stock";
            return "Optimal";
        }

        public bool CanDeductStock(int quantity)
        {
            return quantity > 0 && CurrentStock >= quantity;
        }

        public void DeductStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity to deduct must be strictly positive.", nameof(quantity));

            if (!CanDeductStock(quantity))
                throw new InsufficientStockException(ProductID, SKU, CurrentStock, quantity);

            CurrentStock -= quantity;
        }

        public void AddStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("Quantity to add must be strictly positive.", nameof(quantity));

            CurrentStock += quantity;
        }
    }

    /// <summary>
    /// Represents an immutable inventory movement event (Stock In, Stock Out, or Stock Adjustment).
    /// </summary>
    public class StockTransaction
    {
        public long TransactionID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "IN"; // 'IN', 'OUT', 'ADJUSTMENT'
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount => Quantity * UnitPrice;
        public string? ReferenceNo { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Aggregated metrics for Executive & Operational Dashboards.
    /// </summary>
    public class DashboardMetrics
    {
        public int TotalInventoryCount { get; set; }
        public decimal TotalAssetValuation { get; set; }
        public int LowStockProductCount { get; set; }
        public int OutOfStockProductCount { get; set; }
        public int TodayStockIn { get; set; }
        public int TodayStockOut { get; set; }
        public int TodayNetMovement => TodayStockIn - TodayStockOut;
        public int TotalProductCount { get; set; }
    }

    /// <summary>
    /// DTO for monthly Inflow vs. Outflow trend charts.
    /// </summary>
    public class MonthlyMovementDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public int StockInQuantity { get; set; }
        public int StockOutQuantity { get; set; }
        public int NetMovement => StockInQuantity - StockOutQuantity;
    }

    /// <summary>
    /// DTO for category inventory valuation breakdown charts.
    /// </summary>
    public class CategoryValuationDto
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalUnits { get; set; }
        public decimal TotalValuation { get; set; }
    }
}
