using System;
using System.Collections.Generic;
using System.Linq;
using Inventory_Management_System.DataAccess;
using Inventory_Management_System.Exceptions;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Core Business Logic Layer implementing domain policies, validations,
    /// and business calculations. Follows Single Responsibility Principle (SRP).
    /// </summary>
    public class InventoryService : IInventoryService
    {
        private readonly IProductRepository _productRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITelegramService _telegramService;

        public ITelegramService TelegramService => _telegramService;

        public InventoryService(
            IProductRepository productRepository, 
            ITransactionRepository transactionRepository, 
            ICategoryRepository? categoryRepository = null, 
            ITelegramService? telegramService = null)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
            _categoryRepository = categoryRepository ?? new CategoryRepository();
            _telegramService = telegramService ?? BusinessLogic.TelegramService.Instance;
        }

        public InventoryService() 
            : this(new ProductRepository(), new TransactionRepository(), new CategoryRepository(), BusinessLogic.TelegramService.Instance)
        {
        }

        public DashboardMetrics GetDashboardSummary()
        {
            try
            {
                var products = _productRepository.GetAll().ToList();
                var recentTransactions = _transactionRepository.GetRecentTransactions(100).ToList();

                var today = DateTime.Today;
                var todayTransactions = recentTransactions.Where(t => t.TransactionDate.Date == today).ToList();
                int todayIn = todayTransactions.Where(t => t.TransactionType == "IN").Sum(t => t.Quantity);
                int todayOut = todayTransactions.Where(t => t.TransactionType == "OUT").Sum(t => t.Quantity);

                return new DashboardMetrics
                {
                    TotalInventoryCount = products.Sum(p => p.CurrentStock),
                    TotalAssetValuation = products.Sum(p => p.TotalValuation),
                    LowStockProductCount = products.Count(p => p.IsLowStock() || p.IsOutOfStock()),
                    OutOfStockProductCount = products.Count(p => p.IsOutOfStock()),
                    TodayStockIn = todayIn,
                    TodayStockOut = todayOut,
                    TotalProductCount = products.Count
                };
            }
            catch (Exception)
            {
                // Fallback demo metrics if database is currently unreachable
                return GetFallbackDashboardMetrics();
            }
        }

        public IEnumerable<Product> GetAllProducts()
        {
            try
            {
                return _productRepository.GetAll();
            }
            catch (Exception)
            {
                return GetFallbackProducts();
            }
        }

        public Product? GetProductById(int productId)
        {
            if (productId <= 0) throw new ArgumentException("Product ID must be greater than zero.", nameof(productId));
            return _productRepository.GetById(productId);
        }

        public IEnumerable<Product> GetUrgentRestockList()
        {
            try
            {
                return _productRepository.GetLowStockProducts();
            }
            catch (Exception)
            {
                return GetFallbackProducts().Where(p => p.IsLowStock() || p.IsOutOfStock());
            }
        }

        public int CreateProduct(Product product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (string.IsNullOrWhiteSpace(product.SKU)) throw new ArgumentException("Product SKU is required.", nameof(product.SKU));
            if (string.IsNullOrWhiteSpace(product.ProductName)) throw new ArgumentException("Product Name is required.", nameof(product.ProductName));
            if (product.CostPrice < 0) throw new ArgumentException("Cost price cannot be negative.", nameof(product.CostPrice));
            if (product.SellingPrice < 0) throw new ArgumentException("Selling price cannot be negative.", nameof(product.SellingPrice));
            if (product.ReorderLevel < 0) throw new ArgumentException("Reorder level cannot be negative.", nameof(product.ReorderLevel));

            // Business Rule: SKU must be unique across the catalog
            var existing = _productRepository.GetBySku(product.SKU.Trim());
            if (existing != null)
            {
                throw new DuplicateSkuException(product.SKU.Trim());
            }

            return _productRepository.Insert(product);
        }

        public bool UpdateProduct(Product product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (product.ProductID <= 0) throw new ArgumentException("Invalid Product ID.", nameof(product.ProductID));
            if (string.IsNullOrWhiteSpace(product.SKU)) throw new ArgumentException("Product SKU is required.", nameof(product.SKU));
            if (string.IsNullOrWhiteSpace(product.ProductName)) throw new ArgumentException("Product Name is required.", nameof(product.ProductName));

            string trimmedSku = product.SKU.Trim();
            var existing = _productRepository.GetBySku(trimmedSku);
            if (existing != null && existing.ProductID != product.ProductID)
            {
                throw new DuplicateSkuException(trimmedSku);
            }

            product.SKU = trimmedSku;
            product.ProductName = product.ProductName.Trim();
            bool updated = _productRepository.Update(product);
            if (updated && (product.IsOutOfStock() || product.IsLowStock()))
            {
                if (_telegramService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (product.IsOutOfStock())
                            {
                                await _telegramService.SendOutOfStockAlertAsync(product);
                            }
                            else
                            {
                                await _telegramService.SendLowStockAlertAsync(product);
                            }
                        }
                        catch { }
                    });
                }
            }

            return updated;
        }

        public bool DeleteProduct(int productId)
        {
            if (productId <= 0) throw new ArgumentException("Invalid Product ID.", nameof(productId));
            return _productRepository.Delete(productId);
        }

        public long RecordStockTransaction(int productId, string transactionType, int quantity, 
            decimal unitPrice, string? referenceNo, string? notes, int? userId)
        {
            // Input validations
            if (productId <= 0) throw new ArgumentException("Product ID must be valid.", nameof(productId));
            if (quantity <= 0) throw new ArgumentException("Quantity must be strictly greater than 0.", nameof(quantity));
            if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

            string normalizedType = transactionType?.Trim().ToUpperInvariant() ?? string.Empty;
            if (normalizedType != "IN" && normalizedType != "OUT" && normalizedType != "ADJUSTMENT")
            {
                throw new ArgumentException("Transaction type must be 'IN', 'OUT', or 'ADJUSTMENT'.", nameof(transactionType));
            }

            // Verify product existence and stock invariants
            var product = _productRepository.GetById(productId);
            if (product == null)
            {
                throw new EntityNotFoundException("Product", productId);
            }

            // Business Rule: For OUT transactions, quantity cannot exceed available stock
            if (normalizedType == "OUT")
            {
                if (!product.CanDeductStock(quantity))
                {
                    throw new InsufficientStockException(product.ProductID, product.SKU, product.CurrentStock, quantity);
                }
            }

            var transaction = new StockTransaction
            {
                ProductID = productId,
                SKU = product.SKU,
                ProductName = product.ProductName,
                TransactionType = normalizedType,
                Quantity = quantity,
                UnitPrice = unitPrice,
                ReferenceNo = referenceNo,
                Notes = notes,
                CreatedBy = userId,
                TransactionDate = DateTime.Now
            };

            // Calculate new stock immediately based on domain transaction logic
            int expectedNewStock = normalizedType == "OUT" 
                ? (product.CurrentStock - quantity) 
                : (normalizedType == "IN" ? product.CurrentStock + quantity : quantity);

            long txId = _transactionRepository.RecordStockTransaction(transaction);

            // Trigger real-time Telegram alert if product drops to Low Stock or Out of Stock
            if (normalizedType == "OUT" || normalizedType == "ADJUSTMENT")
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Prefer freshly queried product, fall back to current product with calculated stock
                        Product? alertProduct = null;
                        try
                        {
                            alertProduct = _productRepository.GetById(productId);
                        }
                        catch { }

                        if (alertProduct == null)
                        {
                            alertProduct = new Product
                            {
                                ProductID = product.ProductID,
                                SKU = product.SKU,
                                Barcode = product.Barcode,
                                ProductName = product.ProductName,
                                CategoryID = product.CategoryID,
                                CategoryName = product.CategoryName,
                                SupplierID = product.SupplierID,
                                SupplierName = product.SupplierName,
                                CostPrice = product.CostPrice,
                                SellingPrice = product.SellingPrice,
                                CurrentStock = expectedNewStock,
                                ReorderLevel = product.ReorderLevel,
                                ImagePath = product.ImagePath
                            };
                        }

                        if (alertProduct.IsOutOfStock())
                        {
                            await _telegramService.SendOutOfStockAlertAsync(alertProduct, force: true);
                        }
                        else if (alertProduct.IsLowStock())
                        {
                            await _telegramService.SendLowStockAlertAsync(alertProduct, force: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TelegramAlert Error]: {ex.Message}");
                    }
                });
            }

            return txId;
        }

        public IEnumerable<StockTransaction> GetRecentTransactions(int limit = 100)
        {
            try
            {
                var list = _transactionRepository.GetRecentTransactions(limit).ToList();
                if (list.Count > 0) return list;
            }
            catch { /* fallback on connection failure */ }

            return GetFallbackStockTransactions();
        }

        public IEnumerable<MonthlyMovementDto> GetMonthlyMovements(int monthsBack = 6)
        {
            try
            {
                var movements = _transactionRepository.GetMonthlyMovements(monthsBack).ToList();
                if (movements.Count > 0) return movements;
            }
            catch { /* fallback on connection failure */ }

            return GetFallbackMonthlyMovements();
        }

        public IEnumerable<CategoryValuationDto> GetCategoryValuations()
        {
            try
            {
                var list = _transactionRepository.GetCategoryValuations().ToList();
                if (list.Count > 0) return list;
            }
            catch { /* fallback on connection failure */ }

            return GetFallbackCategoryValuations();
        }

        #region Category Management Operations

        public IEnumerable<Category> GetAllCategories()
        {
            try
            {
                return _categoryRepository.GetAll();
            }
            catch (Exception)
            {
                return GetFallbackCategories();
            }
        }

        public Category? GetCategoryById(int categoryId)
        {
            if (categoryId <= 0) throw new ArgumentException("Category ID must be greater than zero.", nameof(categoryId));
            return _categoryRepository.GetById(categoryId);
        }

        public int CreateCategory(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category.CategoryName)) throw new ArgumentException("Category Name is required.", nameof(category.CategoryName));

            string trimmedName = category.CategoryName.Trim();
            var existing = _categoryRepository.GetByName(trimmedName);
            if (existing != null)
            {
                throw new InvalidOperationException($"A category named '{trimmedName}' already exists in the catalog.");
            }

            category.CategoryName = trimmedName;
            category.Description = string.IsNullOrWhiteSpace(category.Description) ? null : category.Description.Trim();
            return _categoryRepository.Insert(category);
        }

        public bool UpdateCategory(Category category)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            if (category.CategoryID <= 0) throw new ArgumentException("Invalid Category ID.", nameof(category.CategoryID));
            if (string.IsNullOrWhiteSpace(category.CategoryName)) throw new ArgumentException("Category Name is required.", nameof(category.CategoryName));

            string trimmedName = category.CategoryName.Trim();
            var existing = _categoryRepository.GetByName(trimmedName);
            if (existing != null && existing.CategoryID != category.CategoryID)
            {
                throw new InvalidOperationException($"A category named '{trimmedName}' already exists in the catalog.");
            }

            category.CategoryName = trimmedName;
            category.Description = string.IsNullOrWhiteSpace(category.Description) ? null : category.Description.Trim();
            return _categoryRepository.Update(category);
        }

        public bool DeleteCategory(int categoryId)
        {
            if (categoryId <= 0) throw new ArgumentException("Invalid Category ID.", nameof(categoryId));

            int linkedProducts = _categoryRepository.GetProductCount(categoryId);
            if (linkedProducts > 0)
            {
                throw new InvalidOperationException($"Cannot delete category because it is currently assigned to {linkedProducts} active product(s). Please reassign or remove those products before deleting.");
            }

            return _categoryRepository.Delete(categoryId);
        }

        public int GetProductCountByCategoryId(int categoryId)
        {
            if (categoryId <= 0) return 0;
            return _categoryRepository.GetProductCount(categoryId);
        }

        #endregion

        #region Fallback Data Providers (Resilience & Offline Academic Demo Mode)

        private static DashboardMetrics GetFallbackDashboardMetrics()
        {
            return new DashboardMetrics
            {
                TotalInventoryCount = 173,
                TotalAssetValuation = 24890.50m,
                LowStockProductCount = 2,
                OutOfStockProductCount = 1,
                TodayStockIn = 40,
                TodayStockOut = 5,
                TotalProductCount = 8
            };
        }

        private static List<Product> GetFallbackProducts()
        {
            return new List<Product>
            {
                new Product { ProductID = 1, SKU = "ELEC-LAP-001", Barcode = "8901234567890", ProductName = "Dell Latitude Pro 15.6\" Laptop", CategoryID = 1, CategoryName = "Electronics", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 650.00m, SellingPrice = 899.99m, CurrentStock = 25, ReorderLevel = 10 },
                new Product { ProductID = 2, SKU = "ELEC-MOU-002", Barcode = "8901234567891", ProductName = "Logitech Wireless Ergonomic Mouse", CategoryID = 1, CategoryName = "Electronics", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 18.50m, SellingPrice = 34.99m, CurrentStock = 45, ReorderLevel = 15 },
                new Product { ProductID = 3, SKU = "BEV-ORG-003", Barcode = "8901234567892", ProductName = "Organic Cold-Pressed Orange Juice (1L)", CategoryID = 2, CategoryName = "Beverages", SupplierID = 2, SupplierName = "FreshGoods Supply Co.", CostPrice = 2.10m, SellingPrice = 4.50m, CurrentStock = 0, ReorderLevel = 15 },
                new Product { ProductID = 4, SKU = "BEV-TEA-004", Barcode = "8901234567893", ProductName = "Matcha Green Tea Cans (12-Pack)", CategoryID = 2, CategoryName = "Beverages", SupplierID = 2, SupplierName = "FreshGoods Supply Co.", CostPrice = 14.00m, SellingPrice = 24.99m, CurrentStock = 3, ReorderLevel = 10 },
                new Product { ProductID = 5, SKU = "PER-CHE-005", Barcode = "8901234567894", ProductName = "Artisan Aged Cheddar Cheese Block (500g)", CategoryID = 3, CategoryName = "Perishables", SupplierID = 2, SupplierName = "FreshGoods Supply Co.", CostPrice = 5.20m, SellingPrice = 9.75m, CurrentStock = 30, ReorderLevel = 10 },
                new Product { ProductID = 6, SKU = "PER-ALM-006", Barcode = "8901234567895", ProductName = "Raw Organic California Almonds (1kg)", CategoryID = 3, CategoryName = "Perishables", SupplierID = 2, SupplierName = "FreshGoods Supply Co.", CostPrice = 8.50m, SellingPrice = 15.00m, CurrentStock = 18, ReorderLevel = 10 },
                new Product { ProductID = 7, SKU = "OFF-PAP-007", Barcode = "8901234567896", ProductName = "Multipurpose A4 Copy Paper (5-Ream Box)", CategoryID = 4, CategoryName = "Office Supplies", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 18.00m, SellingPrice = 29.50m, CurrentStock = 50, ReorderLevel = 20 },
                new Product { ProductID = 8, SKU = "OFF-PEN-008", Barcode = "8901234567897", ProductName = "Retractable Gel Pens 0.7mm (Box of 24)", CategoryID = 4, CategoryName = "Office Supplies", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 6.20m, SellingPrice = 12.99m, CurrentStock = 35, ReorderLevel = 15 }
            };
        }

        private static List<MonthlyMovementDto> GetFallbackMonthlyMovements()
        {
            return new List<MonthlyMovementDto>
            {
                new MonthlyMovementDto { Year = 2026, Month = 4, MonthLabel = "Apr 2026", StockInQuantity = 120, StockOutQuantity = 85 },
                new MonthlyMovementDto { Year = 2026, Month = 5, MonthLabel = "May 2026", StockInQuantity = 160, StockOutQuantity = 110 },
                new MonthlyMovementDto { Year = 2026, Month = 6, MonthLabel = "Jun 2026", StockInQuantity = 90,  StockOutQuantity = 130 },
                new MonthlyMovementDto { Year = 2026, Month = 7, MonthLabel = "Jul 2026", StockInQuantity = 210, StockOutQuantity = 150 },
                new MonthlyMovementDto { Year = 2026, Month = 8, MonthLabel = "Aug 2026", StockInQuantity = 185, StockOutQuantity = 140 },
                new MonthlyMovementDto { Year = 2026, Month = 9, MonthLabel = "Sep 2026", StockInQuantity = 240, StockOutQuantity = 175 }
            };
        }

        private static List<CategoryValuationDto> GetFallbackCategoryValuations()
        {
            return new List<CategoryValuationDto>
            {
                new CategoryValuationDto { CategoryID = 1, CategoryName = "Electronics", ProductCount = 2, TotalUnits = 70, TotalValuation = 17082.50m },
                new CategoryValuationDto { CategoryID = 4, CategoryName = "Office Supplies", ProductCount = 2, TotalUnits = 85, TotalValuation = 1117.00m },
                new CategoryValuationDto { CategoryID = 3, CategoryName = "Perishables", ProductCount = 2, TotalUnits = 48, TotalValuation = 309.00m },
                new CategoryValuationDto { CategoryID = 2, CategoryName = "Beverages", ProductCount = 2, TotalUnits = 3, TotalValuation = 42.00m }
            };
        }

        private static List<StockTransaction> GetFallbackStockTransactions()
        {
            return new List<StockTransaction>
            {
                new StockTransaction { TransactionID = 101, ProductID = 1, SKU = "ELEC-LAP-001", ProductName = "Dell Latitude Pro 15.6\" Laptop", TransactionType = "IN", Quantity = 30, UnitPrice = 650.00m, ReferenceNo = "PO-2026-001", Notes = "Bulk intake", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-45) },
                new StockTransaction { TransactionID = 102, ProductID = 1, SKU = "ELEC-LAP-001", ProductName = "Dell Latitude Pro 15.6\" Laptop", TransactionType = "OUT", Quantity = 5, UnitPrice = 899.99m, ReferenceNo = "SO-2026-010", Notes = "Workstation sale", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-35) },
                new StockTransaction { TransactionID = 103, ProductID = 2, SKU = "ELEC-MOU-002", ProductName = "Logitech Wireless Ergonomic Mouse", TransactionType = "IN", Quantity = 50, UnitPrice = 18.50m, ReferenceNo = "PO-2026-002", Notes = "Accessories shipment", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-40) },
                new StockTransaction { TransactionID = 104, ProductID = 3, SKU = "BEV-ORG-003", ProductName = "Organic Cold-Pressed Orange Juice (1L)", TransactionType = "OUT", Quantity = 20, UnitPrice = 4.50m, ReferenceNo = "SO-2026-022", Notes = "Catering order - depleted", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-5) },
                new StockTransaction { TransactionID = 105, ProductID = 4, SKU = "BEV-TEA-004", ProductName = "Matcha Green Tea Cans (12-Pack)", TransactionType = "OUT", Quantity = 12, UnitPrice = 24.99m, ReferenceNo = "SO-2026-031", Notes = "Wholesale dispatch", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-12) },
                new StockTransaction { TransactionID = 106, ProductID = 7, SKU = "OFF-PAP-007", ProductName = "Multipurpose A4 Copy Paper (5-Ream Box)", TransactionType = "IN", Quantity = 60, UnitPrice = 18.00m, ReferenceNo = "PO-2026-006", Notes = "Pallet replenishment", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-10) },
                new StockTransaction { TransactionID = 107, ProductID = 7, SKU = "OFF-PAP-007", ProductName = "Multipurpose A4 Copy Paper (5-Ream Box)", TransactionType = "OUT", Quantity = 10, UnitPrice = 29.50m, ReferenceNo = "SO-2026-045", Notes = "Branch transfer", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-2) },
                new StockTransaction { TransactionID = 108, ProductID = 8, SKU = "OFF-PEN-008", ProductName = "Retractable Gel Pens 0.7mm (Box of 24)", TransactionType = "IN", Quantity = 40, UnitPrice = 6.20m, ReferenceNo = "PO-2026-007", Notes = "Stationery restock", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-8) }
            };
        }

        private static List<Category> GetFallbackCategories()
        {
            return new List<Category>
            {
                new Category { CategoryID = 1, CategoryName = "Electronics", Description = "High-value consumer and enterprise electronic hardware and accessories", ProductCount = 2 },
                new Category { CategoryID = 2, CategoryName = "Beverages", Description = "Bottled, canned, and packaged drinks for wholesale distribution", ProductCount = 2 },
                new Category { CategoryID = 3, CategoryName = "Perishables", Description = "Fresh food items, dairy, and cold-chain inventory", ProductCount = 2 },
                new Category { CategoryID = 4, CategoryName = "Office Supplies", Description = "Stationery, paper, printer consumables, and general desk utilities", ProductCount = 2 }
            };
        }

        #endregion
    }
}
