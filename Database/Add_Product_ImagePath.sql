-- ============================================================================
-- Academic & Enterprise Project: Inventory Management System (OOAD)
-- Migration Script: Add ImagePath column to Products table
-- Database Engine: Microsoft SQL Server 2017+ / Azure SQL / LocalDB
-- ============================================================================

USE InventoryDB;
GO

-- 1. Idempotently add ImagePath column to Products
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('Products') 
      AND name = 'ImagePath'
)
BEGIN
    ALTER TABLE Products 
    ADD ImagePath NVARCHAR(255) NULL;
    
    PRINT 'Column ImagePath added to Products table successfully.';
END
ELSE
BEGIN
    PRINT 'Column ImagePath already exists on Products table.';
END
GO

-- ============================================================================
-- 2. PARAMETERIZED SQL EXAMPLES (APPLICATION USAGE REFERENCE)
-- ============================================================================

-- INSERT PARAMETERIZED QUERY:
/*
INSERT INTO Products (
    SKU, Barcode, ProductName, CategoryID, SupplierID, 
    CostPrice, SellingPrice, CurrentStock, ReorderLevel, ImagePath, CreatedAt
)
VALUES (
    @SKU, @Barcode, @ProductName, @CategoryID, @SupplierID, 
    @CostPrice, @SellingPrice, @CurrentStock, @ReorderLevel, @ImagePath, GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);
*/

-- UPDATE PARAMETERIZED QUERY:
/*
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
WHERE ProductID = @ProductID;
*/

-- SELECT PARAMETERIZED QUERY:
/*
SELECT p.ProductID, p.SKU, p.Barcode, p.ProductName, p.CategoryID, c.CategoryName,
       p.SupplierID, s.CompanyName AS SupplierName, p.CostPrice, p.SellingPrice, 
       p.CurrentStock, p.ReorderLevel, p.ImagePath, p.CreatedAt
FROM Products p
INNER JOIN Categories c ON p.CategoryID = c.CategoryID
INNER JOIN Suppliers s ON p.SupplierID = s.SupplierID
WHERE p.ProductID = @ProductID;
*/
GO
