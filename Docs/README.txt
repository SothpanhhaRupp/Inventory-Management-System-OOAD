================================================================================
INVENTORY MANAGEMENT SYSTEM (OOAD COURSE CAPSTONE PROJECT)
================================================================================

Target Framework: .NET 9.0 (Windows Forms)
Language: C# 13.0
Database: Microsoft SQL Server 2017+ / Azure SQL / LocalDB
Architecture: Strict 3-Tier Enterprise Architecture (Presentation, BLL, DAL)

--------------------------------------------------------------------------------
1. QUICK START & DATABASE SETUP
--------------------------------------------------------------------------------
STEP 1: Run the Database Setup Script
  - Open SQL Server Management Studio (SSMS) or Azure Data Studio.
  - Connect to your SQL Server instance (e.g., localhost, .\SQLEXPRESS, or (localdb)\MSSQLLocalDB).
  - Open and execute the script located at:
      [Database/InventoryDB_Setup.sql]
  - This script creates 'InventoryDB', defines all tables, constraints, foreign keys,
    indexes, views (vw_ProductInventoryStatus), stored procedures, and 
    seeds 4 categories, 2 suppliers, 8 products (with low-stock triggers), 
    and 10+ historical stock movement transactions.

STEP 2: Configure the Connection String
  - Open `App.config` in the root folder of the project.
  - Ensure the connection string matches your local SQL Server instance:
    
    * For LocalDB (Visual Studio Default):
      connectionString="Server=(localdb)\MSSQLLocalDB;Database=InventoryDB;Integrated Security=True;TrustServerCertificate=True;"
      
    * For SQL Express:
      connectionString="Server=.\SQLEXPRESS;Database=InventoryDB;Integrated Security=True;TrustServerCertificate=True;"
      
    * For Default Local Instance / Developer Edition:
      connectionString="Server=.;Database=InventoryDB;Integrated Security=True;TrustServerCertificate=True;"

    * For SQL Server Authentication:
      connectionString="Server=.;Database=InventoryDB;User Id=sa;Password=YourStrongPassword;TrustServerCertificate=True;"

STEP 3: Compile and Launch the Application
  - Open the solution in Visual Studio 2022+ or run via CLI:
      dotnet build
      dotnet run
  - Note: The system features an automatic "Resilient Academic Demo Mode". If the database
    is not yet running, the system safely renders seeded domain model fallbacks without
    crashing, ensuring uninterrupted evaluation and demonstration.

--------------------------------------------------------------------------------
2. DEFAULT TEST USER CREDENTIALS
--------------------------------------------------------------------------------
Role            Username      Password (Plaintext)    Hash (SHA-256)
--------------------------------------------------------------------------------
Administrator   admin         123                     a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3
Staff / Operator staff        123                     a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3

--------------------------------------------------------------------------------
3. ARCHITECTURAL HIGHLIGHTS & OOAD CONCEPTS
--------------------------------------------------------------------------------
1. Encapsulation:
   - Product.cs encapsulates stock state invariants (CurrentStock >= 0).
   - Domain methods evaluate status: IsLowStock(), IsOutOfStock(), EvaluateStockStatus().
   - DeductStock() enforces that stock cannot drop below 0 at the domain level.

2. Abstraction & Interfaces:
   - IProductRepository: Contracts for CRUD and low-stock queries.
   - ITransactionRepository: Contracts for ACID stock movement transactions.
   - IInventoryService: High-level business coordinator contract.

3. ACID Transactions & Concurrency:
   - TransactionRepository.RecordStockTransaction executes within an atomic 
     SqlTransaction with RepeatableRead isolation and row-level locking (UPDLOCK).
   - If stock decrement or audit insert fails, the transaction rolls back cleanly.

4. Domain Exception Hierarchy:
   - InventoryDomainException (Base)
     - InsufficientStockException (thrown when stock < requested quantity)
     - DuplicateSkuException (thrown when a SKU already exists)
     - EntityNotFoundException (thrown when record ID is missing)

5. Modern UI/UX:
   - Dark-Navy Slate design palette (#1E293B, #F8FAFC, #3B82F6, #EF4444).
   - High-DPI aware, flat modern controls, zero legacy beveled styling.
   - Interactive LiveCharts telemetry for Monthly Inflow/Outflow & Category Valuation.
   - Conditional color-coded DataGridView for urgent restocks.

--------------------------------------------------------------------------------
4. DELIVERABLES DIRECTORY STRUCTURE
--------------------------------------------------------------------------------
Inventory Management System/
│
├── Database/
│   └── InventoryDB_Setup.sql        <- Standalone executable T-SQL script
│
├── Models/
│   └── DomainModels.cs              <- POCO domain entities & DTOs
│
├── Exceptions/
│   └── DomainExceptions.cs          <- OOAD domain exception hierarchy
│
├── DataAccess/
│   ├── DatabaseHelper.cs            <- ADO.NET connection & command manager
│   ├── IProductRepository.cs        <- Product DAL abstraction
│   ├── ProductRepository.cs         <- Parameterized ADO.NET implementation
│   ├── ITransactionRepository.cs    <- Transaction DAL abstraction
│   └── TransactionRepository.cs     <- Atomic SqlTransaction implementation
│
├── BusinessLogic/
│   ├── IInventoryService.cs         <- Service abstraction
│   └── InventoryService.cs          <- Business rule validations & KPI metrics
│
├── UI/
│   ├── Theme.cs                     <- Color palette, typography, control styling
│   ├── KpiCard.cs                   <- Custom KPI metrics widget
│   ├── DashboardForm.cs             <- WinForms executive dashboard
│   └── StockTransactionModalForm.cs <- Quick restock modal dialog
│
├── Docs/
│   ├── README.txt                   <- Submission instructions & documentation
│   ├── OOAD_Project_Report.md       <- Full academic report outline & content
│   └── Slide_Deck_Presentation.md   <- 15-slide defense deck script & outline
│
├── App.config                       <- Database connection configuration
└── Program.cs                       <- Application entry point
================================================================================
