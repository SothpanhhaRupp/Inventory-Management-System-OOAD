-- ============================================================================
-- Academic & Enterprise Project: Inventory Management System (OOAD)
-- Database Engine: Microsoft SQL Server 2017+ / Azure SQL / LocalDB
-- Script: Complete Database Setup, Constraints, Views, Procedures & Seed Data
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'InventoryDB')
BEGIN
    CREATE DATABASE InventoryDB;
    PRINT 'Database InventoryDB created successfully.';
END
GO

USE InventoryDB;
GO

-- ============================================================================
-- 1. DROP EXISTING OBJECTS (FOR CLEAN RE-RUNNABILITY)
-- ============================================================================
IF OBJECT_ID('sp_RecordStockTransaction', 'P') IS NOT NULL DROP PROCEDURE sp_RecordStockTransaction;
IF OBJECT_ID('sp_GetMonthlyStockMovement', 'P') IS NOT NULL DROP PROCEDURE sp_GetMonthlyStockMovement;
IF OBJECT_ID('sp_GetCategoryValuationSummary', 'P') IS NOT NULL DROP PROCEDURE sp_GetCategoryValuationSummary;
IF OBJECT_ID('vw_ProductInventoryStatus', 'V') IS NOT NULL DROP VIEW vw_ProductInventoryStatus;
IF OBJECT_ID('StockTransactions', 'U') IS NOT NULL DROP TABLE StockTransactions;
IF OBJECT_ID('Products', 'U') IS NOT NULL DROP TABLE Products;
IF OBJECT_ID('Suppliers', 'U') IS NOT NULL DROP TABLE Suppliers;
IF OBJECT_ID('Categories', 'U') IS NOT NULL DROP TABLE Categories;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
GO

-- ============================================================================
-- 2. CREATE CORE ENTITY TABLES WITH CONSTRAINTS
-- ============================================================================

-- Users Table (Authentication and RBAC)
CREATE TABLE Users (
    UserID INT IDENTITY(1,1) NOT NULL,
    Username NVARCHAR(50) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE(),
    
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserID),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin', 'Staff', 'Sales Staff'))
);
GO

-- Categories Table
CREATE TABLE Categories (
    CategoryID INT IDENTITY(1,1) NOT NULL,
    CategoryName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255) NULL,
    
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryID),
    CONSTRAINT UQ_Categories_CategoryName UNIQUE (CategoryName)
);
GO

-- Suppliers Table
CREATE TABLE Suppliers (
    SupplierID INT IDENTITY(1,1) NOT NULL,
    CompanyName NVARCHAR(150) NOT NULL,
    ContactPerson NVARCHAR(100) NULL,
    Phone NVARCHAR(30) NULL,
    Email NVARCHAR(100) NULL,
    Address NVARCHAR(255) NULL,
    
    CONSTRAINT PK_Suppliers PRIMARY KEY CLUSTERED (SupplierID)
);
GO

-- Products Table (Inventory Master)
CREATE TABLE Products (
    ProductID INT IDENTITY(1,1) NOT NULL,
    SKU NVARCHAR(50) NOT NULL,
    Barcode NVARCHAR(50) NULL,
    ProductName NVARCHAR(150) NOT NULL,
    CategoryID INT NOT NULL,
    SupplierID INT NOT NULL,
    CostPrice DECIMAL(18,2) NOT NULL,
    SellingPrice DECIMAL(18,2) NOT NULL,
    CurrentStock INT NOT NULL CONSTRAINT DF_Products_CurrentStock DEFAULT 0,
    ReorderLevel INT NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT 10,
    ImagePath NVARCHAR(255) NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT GETDATE(),
    
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductID),
    CONSTRAINT UQ_Products_SKU UNIQUE (SKU),
    CONSTRAINT UQ_Products_Barcode UNIQUE (Barcode),
    CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID) ON UPDATE CASCADE,
    CONSTRAINT FK_Products_Suppliers FOREIGN KEY (SupplierID) REFERENCES Suppliers(SupplierID) ON UPDATE CASCADE,
    CONSTRAINT CK_Products_CostPrice CHECK (CostPrice >= 0),
    CONSTRAINT CK_Products_SellingPrice CHECK (SellingPrice >= 0),
    CONSTRAINT CK_Products_CurrentStock CHECK (CurrentStock >= 0),
    CONSTRAINT CK_Products_ReorderLevel CHECK (ReorderLevel >= 0)
);
GO

-- StockTransactions Table (Audit Trail of Stock In, Stock Out, Adjustments)
CREATE TABLE StockTransactions (
    TransactionID BIGINT IDENTITY(1,1) NOT NULL,
    ProductID INT NOT NULL,
    TransactionType NVARCHAR(10) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    ReferenceNo NVARCHAR(100) NULL,
    Notes NVARCHAR(255) NULL,
    CreatedBy INT NULL,
    TransactionDate DATETIME NOT NULL CONSTRAINT DF_StockTransactions_Date DEFAULT GETDATE(),
    
    CONSTRAINT PK_StockTransactions PRIMARY KEY CLUSTERED (TransactionID),
    CONSTRAINT FK_StockTransactions_Products FOREIGN KEY (ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE,
    CONSTRAINT FK_StockTransactions_Users FOREIGN KEY (CreatedBy) REFERENCES Users(UserID) ON DELETE SET NULL,
    CONSTRAINT CK_StockTransactions_Type CHECK (TransactionType IN ('IN', 'OUT', 'ADJUSTMENT')),
    CONSTRAINT CK_StockTransactions_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_StockTransactions_UnitPrice CHECK (UnitPrice >= 0)
);
GO

-- ============================================================================
-- 3. PERFORMANCE INDEXES
-- ============================================================================
CREATE NONCLUSTERED INDEX IX_Products_CategoryID ON Products(CategoryID);
CREATE NONCLUSTERED INDEX IX_Products_SupplierID ON Products(SupplierID);
CREATE NONCLUSTERED INDEX IX_Products_CurrentStock_ReorderLevel ON Products(CurrentStock, ReorderLevel);
CREATE NONCLUSTERED INDEX IX_StockTransactions_ProductID_Date ON StockTransactions(ProductID, TransactionDate);
CREATE NONCLUSTERED INDEX IX_StockTransactions_Type_Date ON StockTransactions(TransactionType, TransactionDate);
GO

-- ============================================================================
-- 4. VIEWS FOR REPORTING & REAL-TIME DASHBOARD
-- ============================================================================

-- View: vw_ProductInventoryStatus
-- Computes real-time StockStatus ('Out of Stock', 'Low Stock', 'Optimal') and Valuation
CREATE VIEW vw_ProductInventoryStatus
AS
SELECT 
    p.ProductID,
    p.SKU,
    p.Barcode,
    p.ProductName,
    c.CategoryID,
    c.CategoryName,
    s.SupplierID,
    s.CompanyName AS SupplierName,
    p.CostPrice,
    p.SellingPrice,
    p.CurrentStock,
    p.ReorderLevel,
    (p.CurrentStock * p.CostPrice) AS TotalValuation,
    CASE 
        WHEN p.CurrentStock <= 0 THEN 'Out of Stock'
        WHEN p.CurrentStock <= p.ReorderLevel THEN 'Low Stock'
        ELSE 'Optimal'
    END AS StockStatus,
    p.CreatedAt
FROM Products p
INNER JOIN Categories c ON p.CategoryID = c.CategoryID
INNER JOIN Suppliers s ON p.SupplierID = s.SupplierID;
GO

-- ============================================================================
-- 5. STORED PROCEDURES (ANALYTICS & ATOMIC TRANSACTION MANAGEMENT)
-- ============================================================================

-- Procedure: sp_GetMonthlyStockMovement
-- Aggregates monthly Stock In vs Stock Out for chart visualization
CREATE PROCEDURE sp_GetMonthlyStockMovement
    @MonthsBack INT = 6
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StartDate DATETIME = DATEADD(MONTH, -@MonthsBack, DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0));
    
    SELECT 
        YEAR(TransactionDate) AS [Year],
        MONTH(TransactionDate) AS [Month],
        DATENAME(MONTH, TransactionDate) + ' ' + CAST(YEAR(TransactionDate) AS VARCHAR(4)) AS MonthLabel,
        ISNULL(SUM(CASE WHEN TransactionType = 'IN' THEN Quantity ELSE 0 END), 0) AS StockInQuantity,
        ISNULL(SUM(CASE WHEN TransactionType = 'OUT' THEN Quantity ELSE 0 END), 0) AS StockOutQuantity,
        ISNULL(SUM(CASE WHEN TransactionType = 'IN' THEN Quantity WHEN TransactionType = 'OUT' THEN -Quantity ELSE 0 END), 0) AS NetMovement
    FROM StockTransactions
    WHERE TransactionDate >= @StartDate
    GROUP BY YEAR(TransactionDate), MONTH(TransactionDate), DATENAME(MONTH, TransactionDate)
    ORDER BY [Year] ASC, [Month] ASC;
END
GO

-- Procedure: sp_GetCategoryValuationSummary
-- Aggregates category valuation for Donut / Pie Chart
CREATE PROCEDURE sp_GetCategoryValuationSummary
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        c.CategoryID,
        c.CategoryName,
        COUNT(p.ProductID) AS ProductCount,
        ISNULL(SUM(p.CurrentStock), 0) AS TotalUnits,
        ISNULL(SUM(p.CurrentStock * p.CostPrice), 0.00) AS TotalValuation
    FROM Categories c
    LEFT JOIN Products p ON c.CategoryID = p.CategoryID
    GROUP BY c.CategoryID, c.CategoryName
    ORDER BY TotalValuation DESC;
END
GO

-- Procedure: sp_RecordStockTransaction
-- Enforces ACID transaction: updates product stock balance and logs transaction
CREATE PROCEDURE sp_RecordStockTransaction
    @ProductID INT,
    @TransactionType NVARCHAR(10),
    @Quantity INT,
    @UnitPrice DECIMAL(18,2),
    @ReferenceNo NVARCHAR(100) = NULL,
    @Notes NVARCHAR(255) = NULL,
    @CreatedBy INT = NULL,
    @NewTransactionID BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Lock product row for update to prevent race conditions
        DECLARE @CurrentStock INT;
        SELECT @CurrentStock = CurrentStock 
        FROM Products WITH (UPDLOCK, ROWLOCK)
        WHERE ProductID = @ProductID;
        
        IF @CurrentStock IS NULL
        BEGIN
            THROW 50001, 'Target product does not exist in inventory.', 1;
        END
        
        -- Validate stock reduction
        IF @TransactionType = 'OUT' AND @CurrentStock < @Quantity
        BEGIN
            DECLARE @ErrMsg NVARCHAR(255) = CONCAT('Insufficient stock for Product ID: ', @ProductID, '. Available: ', @CurrentStock, ', Requested: ', @Quantity);
            THROW 50002, @ErrMsg, 1;
        END
        
        -- Update CurrentStock balance
        IF @TransactionType = 'IN'
        BEGIN
            UPDATE Products 
            SET CurrentStock = CurrentStock + @Quantity
            WHERE ProductID = @ProductID;
        END
        ELSE IF @TransactionType = 'OUT'
        BEGIN
            UPDATE Products 
            SET CurrentStock = CurrentStock - @Quantity
            WHERE ProductID = @ProductID;
        END
        ELSE IF @TransactionType = 'ADJUSTMENT'
        BEGIN
            -- In adjustment, Quantity replaces current stock or adjusts (convention: quantity is new balance)
            UPDATE Products 
            SET CurrentStock = @Quantity
            WHERE ProductID = @ProductID;
        END
        
        -- Insert Transaction Log
        INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, UnitPrice, ReferenceNo, Notes, CreatedBy, TransactionDate)
        VALUES (@ProductID, @TransactionType, @Quantity, @UnitPrice, @ReferenceNo, @Notes, @CreatedBy, GETDATE());
        
        SET @NewTransactionID = SCOPE_IDENTITY();
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- ============================================================================
-- 6. SEED / DUMMY DATA FOR DEMONSTRATION & TESTING
-- ============================================================================

-- Default Users (Password: '123' hashed with SHA-256: a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3)
INSERT INTO Users (Username, PasswordHash, FullName, Role) VALUES 
('admin', 'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3', 'System Administrator', 'Admin'),
('staff', 'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3', 'Warehouse Operator', 'Staff'),
('sales', 'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3', 'Sales Representative', 'Sales Staff');
GO

-- 6 Categories (Computer Systems, Components & Accessories)
INSERT INTO Categories (CategoryName, Description) VALUES
('Laptops & Ultrabooks', 'High-performance enterprise laptops, ultrabooks, and portable workstations'),
('PC & Workstations', 'Custom gaming rigs, business desktop towers, and all-in-one workstations'),
('Graphics Cards (GPU)', 'Dedicated graphics processing units, workstation cards, and visual accelerators'),
('Memory & Storage', 'High-speed DDR5/DDR4 RAM modules, NVMe M.2 SSDs, and external storage'),
('Peripherals & Mice', 'Ergonomic gaming mice, mechanical keyboards, webcams, and headsets'),
('Laptop Accessories', 'USB-C docking stations, cooling pads, fast chargers, and laptop sleeves');
GO

-- 2 Hardware Distributors / Suppliers
INSERT INTO Suppliers (CompanyName, ContactPerson, Phone, Email, Address) VALUES
('TechDistro Global Inc.', 'Alex Rivera', '+1 (555) 234-5678', 'sales@techdistro.example.com', '100 Silicon Way, San Jose, CA 95134'),
('CyberCore Components Co.', 'Sarah Jenkins', '+1 (555) 876-5432', 'orders@cybercore.example.com', '450 Semiconductor Pkwy, Austin, TX 78701');
GO

-- 11 Computer Hardware & Accessories Products
-- (Deliberately set 2 items below ReorderLevel: Product 4 has 0 stock, Product 5 has 3 stock)
INSERT INTO Products (SKU, Barcode, ProductName, CategoryID, SupplierID, CostPrice, SellingPrice, CurrentStock, ReorderLevel) VALUES
-- Category 1: Laptops & Ultrabooks (Supplier 1)
('LAP-ROG-001', '8901234567890', 'ASUS ROG Zephyrus G16 Gaming Laptop (i9, 32GB, 1TB)', 1, 1, 1450.00, 1899.99, 15, 5),
('LAP-XPS-002', '8901234567891', 'Dell XPS 13 OLED Ultrabook (Ultra 7, 16GB, 512GB)', 1, 1, 890.00, 1199.99, 22, 8),

-- Category 2: PC & Workstations (Supplier 1)
('PC-MSI-003',  '8901234567892', 'MSI Aegis RS Gaming Desktop (Core i7, RTX 4070, 32GB)', 2, 1, 1200.00, 1599.99, 8, 4),

-- Category 3: Graphics Cards (GPU) (Supplier 2)
('GPU-NV-004',  '8901234567893', 'NVIDIA GeForce RTX 4080 Super 16GB GDDR6X', 3, 2, 820.00, 1049.99, 0, 6),     -- [TRIGGER: Out of Stock, 0 stock]
('GPU-AMD-005', '8901234567894', 'AMD Radeon RX 7800 XT 16GB OC Edition', 3, 2, 420.00, 539.99, 3, 8),         -- [TRIGGER: Low Stock, 3 <= 8]

-- Category 4: Memory & Storage (Supplier 2)
('RAM-COR-006', '8901234567895', 'Corsair Vengeance RGB DDR5 32GB (2x16GB) 6000MHz', 4, 2, 78.00, 119.99, 45, 15),
('SSD-SAM-007', '8901234567896', 'Samsung 990 PRO 2TB NVMe M.2 PCIe 4.0 SSD', 4, 2, 125.00, 179.99, 35, 10),

-- Category 5: Peripherals & Mice (Supplier 1)
('MOU-LOG-008', '8901234567897', 'Logitech MX Master 3S Wireless Performance Mouse', 5, 1, 62.00, 99.99, 40, 12),
('MOU-RAZ-009', '8901234567898', 'Razer Viper V2 Pro Ultra-Lightweight Wireless Mouse', 5, 1, 85.00, 139.99, 25, 10),

-- Category 6: Laptop Accessories (Supplier 1)
('ACC-ANK-010', '8901234567899', 'Anker 10-in-1 Dual 4K USB-C Laptop Docking Station', 6, 1, 75.00, 129.99, 30, 10),
('ACC-CLG-011', '8901234567800', 'Cooler Master Notepal Ergonomic Laptop Cooling Pad', 6, 1, 18.50, 34.99, 50, 15);
GO

-- 12 Sample Stock Transactions to populate analytics charts & audit history
INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, UnitPrice, ReferenceNo, Notes, CreatedBy, TransactionDate) VALUES
(1,  'IN',  18, 1450.00, 'PO-2026-001', 'Initial consignment of ASUS gaming laptops', 1, DATEADD(DAY, -45, GETDATE())),
(1,  'OUT',  3, 1899.99, 'SO-2026-010', 'Corporate multimedia workstation purchase', 2, DATEADD(DAY, -35, GETDATE())),
(2,  'IN',  25,  890.00, 'PO-2026-002', 'Dell ultrabook intake shipment', 1, DATEADD(DAY, -40, GETDATE())),
(3,  'IN',  10, 1200.00, 'PO-2026-003', 'MSI prebuilt workstation stock intake', 1, DATEADD(DAY, -28, GETDATE())),
(3,  'OUT',  2, 1599.99, 'SO-2026-018', 'Design studio desktop deployment', 2, DATEADD(DAY, -14, GETDATE())),
(4,  'IN',  10,  820.00, 'PO-2026-004', 'NVIDIA GPU stock intake from CyberCore', 1, DATEADD(DAY, -25, GETDATE())),
(4,  'OUT', 10, 1049.99, 'SO-2026-022', 'Bulk AI rendering lab order - depleted stock', 2, DATEADD(DAY, -5, GETDATE())),
(5,  'IN',  15,  420.00, 'PO-2026-005', 'AMD GPU delivery consignment', 1, DATEADD(DAY, -30, GETDATE())),
(5,  'OUT', 12,  539.99, 'SO-2026-031', 'Esports arena upgrade sales order', 2, DATEADD(DAY, -12, GETDATE())),
(6,  'IN',  50,   78.00, 'PO-2026-006', 'DDR5 memory modules wholesale shipment', 1, DATEADD(DAY, -15, GETDATE())),
(7,  'IN',  40,  125.00, 'PO-2026-007', 'Samsung NVMe SSD warehouse pallet intake', 1, DATEADD(DAY, -10, GETDATE())),
(8,  'IN',  50,   62.00, 'PO-2026-008', 'Logitech performance mouse bulk intake', 1, DATEADD(DAY, -8, GETDATE())),
(8,  'OUT', 10,   99.99, 'SO-2026-045', 'Enterprise ergonomics package fulfillment', 2, DATEADD(DAY, -2, GETDATE())),
(10, 'IN',  35,   75.00, 'PO-2026-009', 'USB-C docking stations intake', 1, DATEADD(DAY, -6, GETDATE())),
(11, 'IN',  55,   18.50, 'PO-2026-010', 'Laptop cooling pads shipment', 1, DATEADD(DAY, -4, GETDATE()));
GO

PRINT '================================================================';
PRINT 'InventoryDB Database, Tables, Views, SPs, & Seed Data Completed!';
PRINT '================================================================';
GO
