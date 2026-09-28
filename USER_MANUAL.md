# Inventory Management System — User Manual & Operations Guide

> **Enterprise Stock Intelligence & Warehouse Inventory Control Suite**  
> **Framework:** .NET 9.0 (C# 13.0, Windows Forms)  
> **Database:** Microsoft SQL Server (ADO.NET, Microsoft.Data.SqlClient)  
> **Architecture:** 3-Tier Enterprise Architecture (Presentation, Business Logic, Data Access)  

---

## Table of Contents
1. [System Overview & Key Features](#1-system-overview--key-features)
2. [Prerequisites & Initial Setup](#2-prerequisites--initial-setup)
   - [Step 1: Database Initialization (T-SQL Script)](#step-1-database-initialization-t-sql-script)
   - [Step 2: Configure Database Connection (`App.config`)](#step-2-configure-database-connection-appconfig)
   - [Step 3: Launching the Application](#step-3-launching-the-application)
   - [Fallback: Resilient Academic Demo Mode](#fallback-resilient-academic-demo-mode)
3. [User Authentication & Access Control](#3-user-authentication--access-control)
   - [Default Test Credentials](#default-test-credentials)
   - [Signing In](#signing-in)
   - [7-Day "Remember Me" Session & Logout](#7-day-remember-me-session--logout)
4. [Executive Dashboard Navigation](#4-executive-dashboard-navigation)
   - [Real-Time KPI Cards](#real-time-kpi-cards)
   - [Interactive Analytics Charts](#interactive-analytics-charts)
   - [Urgent Restock Deficit Monitor](#urgent-restock-deficit-monitor)
5. [Product Catalog Management](#5-product-catalog-management)
   - [Searching & Category Filtering](#searching--category-filtering)
   - [Adding a New Product (with Image Upload)](#adding-a-new-product-with-image-upload)
   - [Editing Product Details](#editing-product-details)
   - [Deleting a Product](#deleting-a-product)
   - [1-Click Restock from Catalog](#1-click-restock-from-catalog)
6. [Stock Movement Ledger & Verification](#6-stock-movement-ledger--verification)
   - [Recording Movements (Stock In, Stock Out, Adjustment)](#recording-movements-stock-in-stock-out-adjustment)
   - [Barcode & SKU Instant Scanner](#barcode--sku-instant-scanner)
   - [Visual Product Verification Card](#visual-product-verification-card)
   - [Live Stock Impact Preview](#live-stock-impact-preview)
   - [Filtering Historical Movements](#filtering-historical-movements)
   - [Printable Invoices & Receipt Generation (Modern Template with logo.png)](#-printable-invoices--receipt-generation-modern-template-with-logopng)
7. [Inventory Health & Valuation Audits](#7-inventory-health--valuation-audits)
   - [Catalog Valuation & Capital Summary](#catalog-valuation--capital-summary)
   - [Automated Audit Verdicts](#automated-audit-verdicts)
   - [Physical Audit Verification Scan](#physical-audit-verification-scan)
8. [System Settings & Diagnostics](#8-system-settings--diagnostics)
   - [SQL Connection Testing Tool](#sql-connection-testing-tool)
   - [Architecture & Operator Telemetry](#architecture--operator-telemetry)
9. [Troubleshooting & Common Questions](#9-troubleshooting--common-questions)

---

## 1. System Overview & Key Features

The **Inventory Management System** is an enterprise-grade desktop inventory control platform designed for retail, wholesale, and warehouse management. It provides end-to-end visibility of stock levels, turnover velocity, and financial capital allocation.

### Core Capabilities:
- **Real-Time Stock Intelligence**: Immediate KPI computation for total units, cost valuation, and low-stock alerts.
- **Visual Warehouse Picking Verification**: Eliminates picking and dispatch errors by displaying high-resolution item imagery, SKU, category, and barcode verification previews in modal dialogs.
- **ACID Transaction Enforcement**: All stock movements (Inflow, Outflow, Count Adjustments) execute within atomic transactions with row-level locks (`UPDLOCK`) to prevent race conditions and inventory deficits.
- **Live Visual Analytics**: Interactive SkiaSharp/LiveCharts v2 visualizations for monthly inflow vs. outflow and category capital distribution.
- **Role-Based Access Control (RBAC)**: Secure SHA-256 salted password hashing supporting distinct `Admin` and `Staff` roles.
- **Seamless Image Pipeline**: Embedded asset manager with non-locking disk I/O, automatic aspect-ratio thumbnailing, and persistent storage.

---

## 2. Prerequisites & Initial Setup

### System Requirements:
- **Operating System:** Windows 10 / Windows 11 (64-bit)
- **Runtime:** [.NET 9.0 SDK or Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Database Engine:** Microsoft SQL Server 2017+, SQL Server Express, or Visual Studio LocalDB

---

### Step 1: Database Initialization (T-SQL Script)
1. Launch **SQL Server Management Studio (SSMS)** or **Azure Data Studio**.
2. Connect to your SQL Server instance (e.g. `localhost`, `.\SQLEXPRESS`, or `(localdb)\MSSQLLocalDB`).
3. Open and run the standalone SQL script provided in the project:
   ```text
   Database/InventoryDB_Setup.sql
   ```
4. The script automatically:
   - Creates the `InventoryDB` database.
   - Creates tables: `Users`, `Categories`, `Suppliers`, `Products`, and `StockTransactions`.
   - Establishes relational foreign keys, unique SKU/Barcode constraints, and check constraints (`Stock >= 0`).
   - Creates view `vw_ProductInventoryStatus` and stored procedures (`sp_RecordStockTransaction`, `sp_GetMonthlyStockMovement`, `sp_GetCategoryValuationSummary`).
   - Populates initial demonstration data: 2 users, 4 categories, 2 suppliers, 8 pre-seeded products, and 10+ historical stock movements.

---

### Step 2: Configure Database Connection (`App.config`)
Open [App.config](file:///c:/Users/Panha/source/repos/Inventory%20Management%20System/Inventory%20Management%20System/App.config) in the root project directory and adjust the `connectionString` attribute to match your database instance:

#### For SQL Server Express (Default configuration):
```xml
<connectionStrings>
  <add name="InventoryDB" 
       connectionString="Server=.\SQLEXPRESS;Database=InventoryDB;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;" 
       providerName="Microsoft.Data.SqlClient" />
</connectionStrings>
```

#### For Visual Studio LocalDB:
```xml
<connectionStrings>
  <add name="InventoryDB" 
       connectionString="Server=(localdb)\MSSQLLocalDB;Database=InventoryDB;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;" 
       providerName="Microsoft.Data.SqlClient" />
</connectionStrings>
```

#### For SQL Server with Username & Password (SQL Authentication):
```xml
<connectionStrings>
  <add name="InventoryDB" 
       connectionString="Server=127.0.0.1;Database=InventoryDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;Connect Timeout=30;" 
       providerName="Microsoft.Data.SqlClient" />
</connectionStrings>
```

---

### Step 3: Launching the Application
You can launch the application via Visual Studio or via PowerShell / Terminal:

```powershell
# Navigate to the project root directory
cd "c:\Users\Panha\source\repos\Inventory Management System\Inventory Management System"

# Build the solution
dotnet build

# Run the application
dotnet run
```

---

### Fallback: Resilient Academic Demo Mode
If SQL Server is offline, unreachable, or undergoing maintenance, the application **does not crash**. Instead, the system automatically activates **Resilient Academic Demo Mode**:
- The status indicator in the sidebar turns yellow (`● Database: Demo Mode`).
- Pre-seeded in-memory mock products and transactions load seamlessly.
- You can test, browse, and evaluate all UI screens without interruption.

---

## 3. User Authentication & Access Control

When launching the application, you are presented with the **Account Authentication** window.

### Default Test Credentials

| Role | Username | Password | Access Rights |
| :--- | :--- | :--- | :--- |
| **Administrator** | `admin` | `123` | **Full Authority**: Add/Edit/Delete products, Cost/Margin visibility, Asset Valuation analytics, Record stock movements, Run audits, System Settings |
| **Warehouse Staff** | `staff` | `123` | **Operational Warehouse**: Stock In / Out / Adjustments, Physical count audits, Read-Only catalog browsing, Quick restock workflows. *Costs, Margins, Catalog Editing, and System Settings are restricted.* |
| **Sales Staff** | `sales` | `123` | **Customer Sales & Dispatches**: Dedicated Sales Dashboard, Catalog browsing with retail selling prices, Customer Sales order dispatch (`OUT`), sales history ledger. *Costs, Margins, IN/ADJUSTMENT movements, Audits, and Settings are restricted.* |

---

### Signing In
- Enter your **Username** (`admin`, `staff`, or `sales`).
- Enter your **Password** (`123`).
- Click the eye icon (**👁️**) to toggle password visibility.
- Click **Sign In ➔** or press **Enter**.

---

### 7-Day "Remember Me" Session & Logout
- **Remember Me for 7 Days**: When this box is checked, your session token is securely cached on the local machine. Reopening the application bypasses the login screen directly to the dashboard.
- **Logging Out**:
  1. Click the red **`Logout`** link in the sidebar's bottom profile card.
  2. Confirm the logout prompt. Your cached session token will be cleared and the sign-in screen will reappear.
- **Exiting the App**: Click **`Exit App`** or the window's close button (`X`) to terminate the application.

---

## 4. Executive Dashboard Navigation

After logging in, the **Executive Dashboard** displays real-time inventory telemetry.

```
┌────────────────────────────────────────────────────────────────────────┐
│  INVENTSYS  │  Executive Dashboard                     [Refresh] [Quick Restock]
├─────────────┼──────────────────────────────────────────────────────────┤
│ 📊 Dashboard│  [Total Units]   [Asset Valuation]  [Restock Alerts] [Net Movement]
│ 📦 Products │  ┌─────────────────────────┐  ┌────────────────────────┐ │
│ 🔄 Movements│  │ Monthly In/Out Chart    │  │ Category Valuation Pie │ │
│ 📑 Audits   │  └─────────────────────────┘  └────────────────────────┘ │
│ ⚙️ Settings  │  ┌────────────────────────────────────────────────────┐ │
│             │  │ Urgent Restock Deficit Monitor (Critical Items)     │ │
│ 👤 admin    │  └────────────────────────────────────────────────────┘ │
└─────────────┴──────────────────────────────────────────────────────────┘
```

### Real-Time KPI Cards
At the top of the dashboard are 4 interactive summary cards:
1. **Total Inventory**: Displays total units in warehouse and unique SKU count.
2. **Asset Valuation**: Total inventory valuation calculated at cost basis (`CurrentStock * CostPrice`).
3. **Restock Alerts**: Number of items currently at or below their reorder threshold, plus critical zero-stock items.
4. **Today's Movement**: Net movement balance for the current calendar day (`Stock In` minus `Stock Out`).

### Interactive Analytics Charts
- **Stock Movement Inflow vs Outflow**: A dual-bar column chart illustrating inventory intake (blue) vs dispatch (red) across the last 6 months.
- **Valuation by Category**: A segmented pie chart visualizing capital allocation across categories (e.g. *Electronics*, *Beverages*, *Perishables*, *Office Supplies*).

### Urgent Restock Deficit Monitor
The bottom table highlights products requiring immediate procurement:
- **Red Rows**: Products with **`Out of Stock`** status (`Stock = 0`).
- **Yellow Rows**: Products with **`Low Stock`** status (`Stock <= Reorder Level`).
- **Quick Action**: Double-click any row to immediately open the restock dialog for that item.

---

## 5. Product Catalog Management

Click **`📦  Product Catalog`** in the left sidebar to open the master product inventory table.

### Searching & Category Filtering
- **Keyword Search**: Type any SKU code (e.g. `ELEC`) or product name (e.g. `Laptop`) into the search bar. The table filters instantly in real time.
- **Category Filter**: Select a specific category from the dropdown (*All Categories*, *Electronics*, *Beverages*, *Perishables*, *Office Supplies*) to isolate items.

---

### Adding a New Product (with Image Upload)
1. Click the blue **`+  Add Product`** button in the top-right toolbar.
2. Fill in the required fields in the modal form:
   - **SKU Code\***: Unique product identifier (e.g. `TECH-MON-009`).
   - **Barcode / UPC**: Optional barcode scan value.
   - **Product Name\***: Full descriptive product name.
   - **Category\***: Select the parent category.
   - **Supplier\***: Select the vendor/supplier.
   - **Cost Price ($)\***: Wholesale purchase cost (e.g. `120.00`).
   - **Selling Price ($)\***: Retail sale price (e.g. `189.99`). The dialog automatically calculates and displays the profit margin percentage.
   - **Initial Stock**: Starting quantity on hand.
   - **Reorder Level**: Minimum stock threshold before replenishment alert triggers (default: `10`).
3. **Upload Product Image**:
   - In the right-hand preview panel, click **`📁  Browse Image...`**.
   - Select any standard image file (`.jpg`, `.jpeg`, `.png`, `.bmp`, `.webp`).
   - The image is safely copied to the application's storage repository and previewed in high resolution.
   - To clear the picture, click **`✕  Remove Image`**.
4. Click **`💾  Save Product`**. The catalog updates and recalculates total valuation immediately.

---

### Editing Product Details
1. Click to select a row in the product grid, then click **`✏️  Edit Product`** (or double-click the row).
2. Modify prices, names, suppliers, reorder levels, or replace the product image.
3. Click **`💾  Save Changes`** to commit the updates.

---

### Deleting a Product
1. Select the product row you wish to remove.
2. Click the red **`🗑️  Delete`** button.
3. A warning dialog will ask you to confirm deletion. Confirming removes the product and associated transaction records.

---

### 1-Click Restock from Catalog
Select any product from the table and click **`⚡  Restock Item`** to open the movement modal with that product pre-selected.

---

## 6. Stock Movement Ledger & Verification

Click **`🔄  Stock Movements`** in the sidebar to review the immutable audit trail of all warehouse operations.

### Recording Movements (Stock In, Stock Out, Adjustment)
To record a new inventory change, click **`+  Record Stock Movement`** or the top header button **`Quick Restock`**.

```
┌────────────────────────────────────────────────────────────────────────┐
│ Record Stock Movement & Picking Verification                    [ X ] │
├──────────────────────────────────────┬─────────────────────────────────┤
│ ⚡ Quick Barcode / SKU Scanner:       │  Visual Verification:           │
│ [ Scan barcode or SKU... ] [Enter]   │  ┌───────────────────────────┐  │
│                                      │  │                           │  │
│ Target Product *:                    │  │      [Product Image]      │  │
│ [ Dell Latitude Pro 15.6"  ▼ ]       │  │                           │  │
│                                      │  └───────────────────────────┘  │
│ Movement Type *:      Quantity *:    │   Dell Latitude Pro 15.6"       │
│ [ IN (Restock)    ▼ ] [ 25       ▲▼] │   SKU: ELEC-LAP-001             │
│                                      │   Category: Electronics         │
│ Unit Price ($) *:     Reference #:   │   [ Optimal: 25 in stock ]      │
│ [ 650.00        ▲▼]   [ PO-2026-001] │                                 │
│                                      │                                 │
│ Notes / Reason:                      │                                 │
│ [ Initial stock intake             ] │                                 │
│                                      │                                 │
│ [ Stock Preview: 25 -> 50 (+25 units)                                ] │
├──────────────────────────────────────┴─────────────────────────────────┤
│                          [ Confirm & Record Movement ]    [ Cancel ]   │
└────────────────────────────────────────────────────────────────────────┘
```

#### Movement Types Explained:
1. **`IN (Restock / Intake)`**:
   - Used for supplier deliveries, replenishment purchase orders, and customer returns.
   - Increases on-hand stock: `CurrentStock = CurrentStock + Quantity`.
2. **`OUT (Sale / Dispatch)`**:
   - Used for customer order fulfillment, store deliveries, and write-offs.
   - Decreases on-hand stock: `CurrentStock = CurrentStock - Quantity`.
   - **Protection Rule**: The system blocks dispatch if requested quantity exceeds available stock, throwing an `InsufficientStockException`.
3. **`ADJUSTMENT (Audit Count)`**:
   - Used during physical cycle counts to correct stock discrepancies.
   - Overwrites stock balance to the newly counted total: `CurrentStock = Quantity`.

---

### Barcode & SKU Instant Scanner
At the top of the modal, place your cursor into the **`⚡ Quick Barcode / SKU Scanner`** box:
- Scan an item's barcode using a handheld USB/Bluetooth scanner, or type a SKU (e.g. `PER-CHE-005`) and press **Enter**.
- The form instantly selects the matched product in the dropdown and displays its visual verification card.

---

### Visual Product Verification Card
The right side of the transaction window displays the **Visual Verification Panel**:
- Shows the actual stored image of the item.
- Shows the exact product name, SKU, and category.
- Shows current stock badge color (Green for Optimal, Yellow for Low Stock, Red for Out of Stock).
- **Benefit**: Warehouse operators can visually cross-check the physical item in their hand against the screen before clicking confirm, preventing mislabeled dispatches.

---

### Live Stock Impact Preview
As you adjust the **Movement Type** or **Quantity**, the colored banner at the bottom updates live:
- Example (*Stock In*): `Stock Preview: 25 -> 50 (+25 units)`
- Example (*Stock Out*): `Stock Preview: 25 -> 20 (-5 units)`
- If a stock out exceeds available units, the banner turns red and warns: `Insufficient Stock! Available: 5, Requested: 10`.

Click **`Confirm & Record Movement`** to execute the ACID transaction.

---

### Filtering Historical Movements
In the **Stock Movements** view:
- **Search**: Search by PO / Reference Number, Product Name, or SKU.
- **Type Filter**: Filter by `All Movements`, `IN (Intake)`, `OUT (Dispatch)`, or `ADJUSTMENT`.
- Colors indicate transaction types: **Green** for `IN`, **Red** for `OUT`, and **Blue** for `ADJUSTMENT`.

---

### 🧾 Printable Invoices & Receipt Generation (Modern Template with `logo.png`)
The system features an enterprise-grade GDI+ vector printing engine adhering to the modern yellow/charcoal brand template:

#### 1. Instant Invoice Print Upon Transaction Commit:
- When a customer sale or stock movement is confirmed in the transaction dialog, the system prompts:  
  *`"Would you like to preview and print the invoice / receipt voucher now?"`*
- Clicking **`Yes`** immediately opens the **High-DPI Invoice Viewer**.

#### 2. On-Demand Printing from Stock Movements Ledger:
- Navigate to **`🔄  Stock Movements`**.
- Select any transaction record in the ledger table.
- Click the **`🧾  Print Invoice / Receipt`** button (or **double-click** the transaction row).

#### 3. Invoice Document Layout & Brand Elements:
- **Company Branding (`logo.png`)**: Automatically loads and embeds `Resources/logo.png` at high bicubic resolution with your company brand title and tagline.
- **Vibrant Split Ribbon**: Distinctive yellow/amber horizontal header band featuring bold condensed `INVOICE` typography.
- **Two-Column Metadata**:
  - **Left**: `Invoice to:` (Customer name, street address, and location details).
  - **Right**: `Invoice#` (e.g. `52148`) and formatted `Date` (`MM / dd / yyyy`).
- **Framed Items Ledger Table**:
  - Dark slate header with white columns: `SL.`, `Item Description`, `Price`, `Qty.`, `Total`.
  - Elegant ledger border box enclosing line items and amounts.
- **Terms & Payment Information**:
  - Customer thank you message: *"Thank you for your business"*.
  - Policy terms & conditions.
  - Payment details (`Account #`, `A/C Name`, `Bank Details`).
- **Highlighted Total Box**:
  - Solid yellow accent block emphasizing `Sub Total`, `Tax: 0.00%`, and final `Total: $XXX.XX`.
- **Authorization & Contact Footer**:
  - Yellow baseline accent with physical / digital signature line above **`Authorised Sign`**.
  - Footer contact line: `Phone #   |   Address   |   Website`.
- **Integrated Print Controls**:
  - 🖨️ **Print Invoice...**: Select physical printer or configure page settings.
  - 📄 **Quick PDF / Direct Print**: One-click output to Microsoft Print to PDF.
  - 🔍 **Zoom +/- & Fit Page**: High-resolution anti-aliased zooming up to 250%.

---

## 7. Inventory Health & Valuation Audits

Click **`📑  Inventory Audits`** in the left sidebar to open the capital valuation and discrepancy audit view.

### Catalog Valuation & Capital Summary
The top card displays:
- **Total Catalog Valuation**: Total monetary capital invested in physical stock across all items.
- **Total Warehouse Units**: Aggregate unit count on hand.
- **Category Coverage**: Total count of active categories evaluated.

### Automated Audit Verdicts
The audit table assesses every SKU against business rules and prints an automated status verdict:
- **`OPTIMAL - Stock Invariants Satisfied`**: Stock is comfortably above reorder levels.
- **`WARNING - Approaching Reorder Point`**: Stock is near or below the safe operating buffer.
- **`DEFICIT - Immediate Restock Needed`**: Product is completely exhausted (`Stock = 0`).

### Physical Audit Verification Scan
Click **`🔍  Run Physical Audit Verification`** to re-scan the entire database catalog, verify calculation invariants, and report if any unrecorded stock deviations exist.

---

## 8. System Settings & Diagnostics

Click **`⚙️  System Settings`** in the sidebar to review system configurations and connectivity.

### SQL Connection Testing Tool
- Displays the current active connection string retrieved from `App.config`.
- Click **`🔌  Test SQL Connection`**:
  - If reachable, returns: `● Connection Successful (SQL Server Active)` with green indicators.
  - If unreachable, displays the detailed SQL error message to assist in diagnosis.

### Architecture & Operator Telemetry
- Lists 3-Tier Enterprise Architecture verification points.
- Displays the currently authenticated user's **Full Name**, **Username**, and **Security Role** (`Admin` or `Staff`).

---

## 9. Troubleshooting & Common Questions

### Q1: The database status shows "● Database: Demo Mode". How do I connect to SQL Server?
1. Ensure your SQL Server instance is started in Windows Services (`services.msc` -> `SQL Server (SQLEXPRESS)` or `MSSQLSERVER`).
2. Run `Database/InventoryDB_Setup.sql` in SSMS to create `InventoryDB`.
3. Check [App.config](file:///c:/Users/Panha/source/repos/Inventory%20Management%20System/Inventory%20Management%20System/App.config) and ensure the `Server=` parameter matches your computer name (e.g. `Server=MSI-GF63-THIN\SQLEXPRESS` or `Server=.\SQLEXPRESS`).
4. Go to **System Settings** in the app and click **Test SQL Connection**.

---

### Q2: What happens if an operator enters duplicate SKU codes?
The system enforces SKU uniqueness at both the Application and Database layers. If an operator tries to register a product with an existing SKU, the system catches the constraint violation and displays a friendly error:  
`"Product SKU '[SKU]' already exists in inventory. Please choose a unique SKU."`

---

### Q3: Why does the system prevent me from recording a Stock Out?
The software enforces the business invariant that stock quantities cannot drop below zero. If you try to dispatch 15 units of an item when only 10 exist, the system blocks the transaction and warns:  
`"Insufficient stock for Product ID X. Available: 10, Requested: 15."`

---

### Q4: How do I switch accounts or log out?
Click the red **`Logout`** button at the bottom of the left sidebar. This invalidates your cached 7-day token and returns you to the login screen where you can sign in as a different user.

---

### Q5: Where are uploaded product images stored?
When an image is uploaded through the Add/Edit Product modal, it is copied and stored under the application's local asset directory:
```text
bin\Debug\net9.0-windows\ProductImages\
```
The file is given a unique timestamped name and loaded via memory streams so disk files are never locked during operation.
