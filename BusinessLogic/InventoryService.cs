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

        public IEnumerable<TopSellingProductDto> GetTopSellingProducts(int limit = 5)
        {
            try
            {
                var list = _transactionRepository.GetTopSellingProducts(limit).ToList();
                if (list.Count > 0) return list;
            }
            catch { /* fallback on connection failure */ }

            return GetFallbackTopSellingProducts(limit);
        }

        public IEnumerable<StockTransaction> GetFilteredTransactions(int? year, int? month, int? categoryId, string? movementType, string? searchQuery)
        {
            try
            {
                return _transactionRepository.GetFilteredTransactions(year, month, categoryId, movementType, searchQuery);
            }
            catch
            {
                // Fallback in-memory filter
                var all = GetRecentTransactions(300);
                if (year.HasValue && year.Value > 0)
                    all = all.Where(t => t.TransactionDate.Year == year.Value);
                if (month.HasValue && month.Value > 0)
                    all = all.Where(t => t.TransactionDate.Month == month.Value);
                if (!string.IsNullOrWhiteSpace(movementType) && !string.Equals(movementType, "ALL", StringComparison.OrdinalIgnoreCase))
                    all = all.Where(t => string.Equals(t.TransactionType, movementType, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    string q = searchQuery.Trim().ToLowerInvariant();
                    all = all.Where(t => t.ProductName.ToLowerInvariant().Contains(q) || t.SKU.ToLowerInvariant().Contains(q) || (t.ReferenceNo != null && t.ReferenceNo.ToLowerInvariant().Contains(q)));
                }
                return all.ToList();
            }
        }

        public MonthlyReportSummaryDto GetMonthlyReportSummary(int? year, int? month, int? categoryId)
        {
            var txs = GetFilteredTransactions(year, month, categoryId, null, null).ToList();

            string label = (month.HasValue && month.Value >= 1 && month.Value <= 12)
                ? new DateTime(year ?? DateTime.Now.Year, month.Value, 1).ToString("MMMM yyyy")
                : (year.HasValue ? $"Year {year.Value}" : "All Historical Data");

            return new MonthlyReportSummaryDto
            {
                Year = year ?? DateTime.Now.Year,
                Month = month ?? DateTime.Now.Month,
                MonthLabel = label,
                TotalTransactions = txs.Count,
                TotalInUnits = txs.Where(t => t.TransactionType == "IN").Sum(t => t.Quantity),
                TotalOutUnits = txs.Where(t => t.TransactionType == "OUT").Sum(t => t.Quantity),
                TotalSalesRevenue = txs.Where(t => t.TransactionType == "OUT").Sum(t => t.TotalAmount),
                TotalInflowCost = txs.Where(t => t.TransactionType == "IN").Sum(t => t.TotalAmount)
            };
        }

        private IEnumerable<TopSellingProductDto> GetFallbackTopSellingProducts(int limit)
        {
            var products = GetAllProducts().ToList();
            return products
                .OrderByDescending(p => p.CurrentStock)
                .Take(limit)
                .Select(p => new TopSellingProductDto
                {
                    ProductID = p.ProductID,
                    SKU = p.SKU,
                    ProductName = p.ProductName,
                    CategoryName = "General",
                    UnitsSold = Math.Max(5, 50 - p.CurrentStock),
                    TotalRevenue = Math.Max(5, 50 - p.CurrentStock) * p.SellingPrice
                })
                .OrderByDescending(t => t.UnitsSold);
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
                TotalInventoryCount = 273,
                TotalAssetValuation = 67855.00m,
                LowStockProductCount = 1,
                OutOfStockProductCount = 1,
                TodayStockIn = 55,
                TodayStockOut = 10,
                TotalProductCount = 11
            };
        }

        private static List<Product> GetFallbackProducts()
        {
            return new List<Product>
            {
                new Product { ProductID = 1, SKU = "LAP-ROG-001", Barcode = "8901234567890", ProductName = "ASUS ROG Zephyrus G16 Gaming Laptop (i9, 32GB, 1TB)", CategoryID = 1, CategoryName = "Laptops & Ultrabooks", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 1450.00m, SellingPrice = 1899.99m, CurrentStock = 15, ReorderLevel = 5 },
                new Product { ProductID = 2, SKU = "LAP-XPS-002", Barcode = "8901234567891", ProductName = "Dell XPS 13 OLED Ultrabook (Ultra 7, 16GB, 512GB)", CategoryID = 1, CategoryName = "Laptops & Ultrabooks", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 890.00m, SellingPrice = 1199.99m, CurrentStock = 22, ReorderLevel = 8 },
                new Product { ProductID = 3, SKU = "PC-MSI-003",  Barcode = "8901234567892", ProductName = "MSI Aegis RS Gaming Desktop (Core i7, RTX 4070, 32GB)", CategoryID = 2, CategoryName = "PC & Workstations", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 1200.00m, SellingPrice = 1599.99m, CurrentStock = 8, ReorderLevel = 4 },
                new Product { ProductID = 4, SKU = "GPU-NV-004",  Barcode = "8901234567893", ProductName = "NVIDIA GeForce RTX 4080 Super 16GB GDDR6X", CategoryID = 3, CategoryName = "Graphics Cards (GPU)", SupplierID = 2, SupplierName = "CyberCore Components Co.", CostPrice = 820.00m, SellingPrice = 1049.99m, CurrentStock = 0, ReorderLevel = 6 },
                new Product { ProductID = 5, SKU = "GPU-AMD-005", Barcode = "8901234567894", ProductName = "AMD Radeon RX 7800 XT 16GB OC Edition", CategoryID = 3, CategoryName = "Graphics Cards (GPU)", SupplierID = 2, SupplierName = "CyberCore Components Co.", CostPrice = 420.00m, SellingPrice = 539.99m, CurrentStock = 3, ReorderLevel = 8 },
                new Product { ProductID = 6, SKU = "RAM-COR-006", Barcode = "8901234567895", ProductName = "Corsair Vengeance RGB DDR5 32GB (2x16GB) 6000MHz", CategoryID = 4, CategoryName = "Memory & Storage", SupplierID = 2, SupplierName = "CyberCore Components Co.", CostPrice = 78.00m, SellingPrice = 119.99m, CurrentStock = 45, ReorderLevel = 15 },
                new Product { ProductID = 7, SKU = "SSD-SAM-007", Barcode = "8901234567896", ProductName = "Samsung 990 PRO 2TB NVMe M.2 PCIe 4.0 SSD", CategoryID = 4, CategoryName = "Memory & Storage", SupplierID = 2, SupplierName = "CyberCore Components Co.", CostPrice = 125.00m, SellingPrice = 179.99m, CurrentStock = 35, ReorderLevel = 10 },
                new Product { ProductID = 8, SKU = "MOU-LOG-008", Barcode = "8901234567897", ProductName = "Logitech MX Master 3S Wireless Performance Mouse", CategoryID = 5, CategoryName = "Peripherals & Mice", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 62.00m, SellingPrice = 99.99m, CurrentStock = 40, ReorderLevel = 12 },
                new Product { ProductID = 9, SKU = "MOU-RAZ-009", Barcode = "8901234567898", ProductName = "Razer Viper V2 Pro Ultra-Lightweight Wireless Mouse", CategoryID = 5, CategoryName = "Peripherals & Mice", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 85.00m, SellingPrice = 139.99m, CurrentStock = 25, ReorderLevel = 10 },
                new Product { ProductID = 10, SKU = "ACC-ANK-010", Barcode = "8901234567899", ProductName = "Anker 10-in-1 Dual 4K USB-C Laptop Docking Station", CategoryID = 6, CategoryName = "Laptop Accessories", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 75.00m, SellingPrice = 129.99m, CurrentStock = 30, ReorderLevel = 10 },
                new Product { ProductID = 11, SKU = "ACC-CLG-011", Barcode = "8901234567800", ProductName = "Cooler Master Notepal Ergonomic Laptop Cooling Pad", CategoryID = 6, CategoryName = "Laptop Accessories", SupplierID = 1, SupplierName = "TechDistro Global Inc.", CostPrice = 18.50m, SellingPrice = 34.99m, CurrentStock = 50, ReorderLevel = 15 }
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
                new CategoryValuationDto { CategoryID = 1, CategoryName = "Laptops & Ultrabooks", ProductCount = 2, TotalUnits = 37, TotalValuation = 41330.00m },
                new CategoryValuationDto { CategoryID = 2, CategoryName = "PC & Workstations", ProductCount = 1, TotalUnits = 8, TotalValuation = 9600.00m },
                new CategoryValuationDto { CategoryID = 4, CategoryName = "Memory & Storage", ProductCount = 2, TotalUnits = 80, TotalValuation = 7885.00m },
                new CategoryValuationDto { CategoryID = 5, CategoryName = "Peripherals & Mice", ProductCount = 2, TotalUnits = 65, TotalValuation = 4605.00m },
                new CategoryValuationDto { CategoryID = 6, CategoryName = "Laptop Accessories", ProductCount = 2, TotalUnits = 80, TotalValuation = 3175.00m },
                new CategoryValuationDto { CategoryID = 3, CategoryName = "Graphics Cards (GPU)", ProductCount = 2, TotalUnits = 3, TotalValuation = 1260.00m }
            };
        }

        private static List<StockTransaction> GetFallbackStockTransactions()
        {
            return new List<StockTransaction>
            {
                new StockTransaction { TransactionID = 101, ProductID = 1, SKU = "LAP-ROG-001", ProductName = "ASUS ROG Zephyrus G16 Gaming Laptop (i9, 32GB, 1TB)", TransactionType = "IN", Quantity = 18, UnitPrice = 1450.00m, ReferenceNo = "PO-2026-001", Notes = "Initial intake", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-45) },
                new StockTransaction { TransactionID = 102, ProductID = 1, SKU = "LAP-ROG-001", ProductName = "ASUS ROG Zephyrus G16 Gaming Laptop (i9, 32GB, 1TB)", TransactionType = "OUT", Quantity = 3, UnitPrice = 1899.99m, ReferenceNo = "SO-2026-010", Notes = "Multimedia client purchase", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-35) },
                new StockTransaction { TransactionID = 103, ProductID = 4, SKU = "GPU-NV-004", ProductName = "NVIDIA GeForce RTX 4080 Super 16GB GDDR6X", TransactionType = "IN", Quantity = 10, UnitPrice = 820.00m, ReferenceNo = "PO-2026-004", Notes = "GPU stock intake", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-25) },
                new StockTransaction { TransactionID = 104, ProductID = 4, SKU = "GPU-NV-004", ProductName = "NVIDIA GeForce RTX 4080 Super 16GB GDDR6X", TransactionType = "OUT", Quantity = 10, UnitPrice = 1049.99m, ReferenceNo = "SO-2026-022", Notes = "AI lab order - cleared stock", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-5) },
                new StockTransaction { TransactionID = 105, ProductID = 5, SKU = "GPU-AMD-005", ProductName = "AMD Radeon RX 7800 XT 16GB OC Edition", TransactionType = "OUT", Quantity = 12, UnitPrice = 539.99m, ReferenceNo = "SO-2026-031", Notes = "Esports arena order", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-12) },
                new StockTransaction { TransactionID = 106, ProductID = 6, SKU = "RAM-COR-006", ProductName = "Corsair Vengeance RGB DDR5 32GB (2x16GB) 6000MHz", TransactionType = "IN", Quantity = 50, UnitPrice = 78.00m, ReferenceNo = "PO-2026-006", Notes = "DDR5 wholesale shipment", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-15) },
                new StockTransaction { TransactionID = 107, ProductID = 8, SKU = "MOU-LOG-008", ProductName = "Logitech MX Master 3S Wireless Performance Mouse", TransactionType = "OUT", Quantity = 10, UnitPrice = 99.99m, ReferenceNo = "SO-2026-045", Notes = "Corporate ergonomics order", CreatedByName = "Warehouse Operator", TransactionDate = DateTime.Now.AddDays(-2) },
                new StockTransaction { TransactionID = 108, ProductID = 11, SKU = "ACC-CLG-011", ProductName = "Cooler Master Notepal Ergonomic Laptop Cooling Pad", TransactionType = "IN", Quantity = 55, UnitPrice = 18.50m, ReferenceNo = "PO-2026-010", Notes = "Cooling pads replenishment", CreatedByName = "System Administrator", TransactionDate = DateTime.Now.AddDays(-4) }
            };
        }

        private static List<Category> GetFallbackCategories()
        {
            return new List<Category>
            {
                new Category { CategoryID = 1, CategoryName = "Laptops & Ultrabooks", Description = "High-performance enterprise laptops, ultrabooks, and portable workstations", ProductCount = 2 },
                new Category { CategoryID = 2, CategoryName = "PC & Workstations", Description = "Custom gaming rigs, business desktop towers, and all-in-one workstations", ProductCount = 1 },
                new Category { CategoryID = 3, CategoryName = "Graphics Cards (GPU)", Description = "Dedicated graphics processing units, workstation cards, and visual accelerators", ProductCount = 2 },
                new Category { CategoryID = 4, CategoryName = "Memory & Storage", Description = "High-speed DDR5/DDR4 RAM modules, NVMe M.2 SSDs, and external storage", ProductCount = 2 },
                new Category { CategoryID = 5, CategoryName = "Peripherals & Mice", Description = "Ergonomic gaming mice, mechanical keyboards, webcams, and headsets", ProductCount = 2 },
                new Category { CategoryID = 6, CategoryName = "Laptop Accessories", Description = "USB-C docking stations, cooling pads, fast chargers, and laptop sleeves", ProductCount = 2 }
            };
        }

        #endregion
    }
}
