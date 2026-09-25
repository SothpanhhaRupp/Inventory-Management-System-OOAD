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
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin', 'Staff'))
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
('staff', 'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3', 'Warehouse Operator', 'Staff');
GO

-- 4 Categories
INSERT INTO Categories (CategoryName, Description) VALUES
('Electronics', 'High-value consumer and enterprise electronic hardware and accessories'),
('Beverages', 'Bottled, canned, and packaged drinks for wholesale distribution'),
('Perishables', 'Fresh food items, dairy, and cold-chain inventory'),
('Office Supplies', 'Stationery, paper, printer consumables, and general desk utilities');
GO

-- 2 Suppliers
INSERT INTO Suppliers (CompanyName, ContactPerson, Phone, Email, Address) VALUES
('TechDistro Global Inc.', 'Alex Rivera', '+1 (555) 234-5678', 'sales@techdistro.example.com', '100 Silicon Way, San Jose, CA 95134'),
('FreshGoods Supply Co.', 'Sarah Jenkins', '+1 (555) 876-5432', 'orders@freshgoods.example.com', '450 Agricultural Pkwy, Fresno, CA 93706');
GO

-- 8 Sample Products (deliberately set 2 items below ReorderLevel for alerts: Product 3 has 0, Product 4 has 3)
INSERT INTO Products (SKU, Barcode, ProductName, CategoryID, SupplierID, CostPrice, SellingPrice, CurrentStock, ReorderLevel) VALUES
-- Category 1: Electronics (Supplier 1)
('ELEC-LAP-001', '8901234567890', 'Dell Latitude Pro 15.6" Laptop', 1, 1, 650.00, 899.99, 25, 10),
('ELEC-MOU-002', '8901234567891', 'Logitech Wireless Ergonomic Mouse', 1, 1, 18.50, 34.99, 45, 15),

-- Category 2: Beverages (Supplier 2)
('BEV-ORG-003',  '8901234567892', 'Organic Cold-Pressed Orange Juice (1L)', 2, 2, 2.10, 4.50, 0, 15), -- [TRIGGER: Out of Stock, 0 stock]
('BEV-TEA-004',  '8901234567893', 'Matcha Green Tea Cans (12-Pack)', 2, 2, 14.00, 24.99, 3, 10),     -- [TRIGGER: Low Stock, 3 <= 10]

-- Category 3: Perishables (Supplier 2)
('PER-CHE-005',  '8901234567894', 'Artisan Aged Cheddar Cheese Block (500g)', 3, 2, 5.20, 9.75, 30, 10),
('PER-ALM-006',  '8901234567895', 'Raw Organic California Almonds (1kg)', 3, 2, 8.50, 15.00, 18, 10),

-- Category 4: Office Supplies (Supplier 1)
('OFF-PAP-007',  '8901234567896', 'Multipurpose A4 Copy Paper (5-Ream Box)', 4, 1, 18.00, 29.50, 50, 20),
('OFF-PEN-008',  '8901234567897', 'Retractable Gel Pens 0.7mm (Box of 24)', 4, 1, 6.20, 12.99, 35, 15);
GO

-- 10 Sample Stock Transactions across recent dates to populate analytics charts
INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, UnitPrice, ReferenceNo, Notes, CreatedBy, TransactionDate) VALUES
(1, 'IN',  30, 650.00, 'PO-2026-001', 'Initial stock intake from TechDistro', 1, DATEADD(DAY, -45, GETDATE())),
(1, 'OUT',  5, 899.99, 'SO-2026-010', 'Corporate workstation sales order', 2, DATEADD(DAY, -35, GETDATE())),
(2, 'IN',  50,  18.50, 'PO-2026-002', 'Bulk accessories intake', 1, DATEADD(DAY, -40, GETDATE())),
(2, 'OUT',  5,  34.99, 'SO-2026-015', 'Retail counter fulfillment', 2, DATEADD(DAY, -20, GETDATE())),
(3, 'IN',  20,   2.10, 'PO-2026-003', 'Fresh beverage replenishment', 1, DATEADD(DAY, -25, GETDATE())),
(3, 'OUT', 20,   4.50, 'SO-2026-022', 'Special event catering order - cleared stock', 2, DATEADD(DAY, -5, GETDATE())),
(4, 'IN',  15,  14.00, 'PO-2026-004', 'Specialty tea inventory shipment', 1, DATEADD(DAY, -30, GETDATE())),
(4, 'OUT', 12,  24.99, 'SO-2026-031', 'Wholesale store delivery', 2, DATEADD(DAY, -12, GETDATE())),
(5, 'IN',  35,   5.20, 'PO-2026-005', 'Refrigerated cheese consignment', 1, DATEADD(DAY, -15, GETDATE())),
(7, 'IN',  60,  18.00, 'PO-2026-006', 'Warehouse paper pallet intake', 1, DATEADD(DAY, -10, GETDATE())),
(7, 'OUT', 10,  29.50, 'SO-2026-045', 'Internal school supplies order', 2, DATEADD(DAY, -2, GETDATE())),
(8, 'IN',  40,   6.20, 'PO-2026-007', 'Stationery restock intake', 1, DATEADD(DAY, -8, GETDATE())),
(8, 'OUT',  5,  12.99, 'SO-2026-050', 'Desk bundle sales package', 2, DATEADD(DAY, 0, GETDATE()));
GO

PRINT '================================================================';
PRINT 'InventoryDB Database, Tables, Views, SPs, & Seed Data Completed!';
PRINT '================================================================';
GO
