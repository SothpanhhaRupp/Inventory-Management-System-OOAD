# Test Credentials & Role-Based Access Control (RBAC)

## 1. Administrator Account (`Admin`)
- **Username**: `admin`
- **Password**: `123`
- **Privileges**:
  - Full CRUD authority: Add new products, edit catalog definitions, delete obsolete products
  - View financial asset valuations, product cost prices, and profit margins
  - Full access to Executive Dashboard and System Settings & diagnostics
  - Batch deletion and physical audit oversight

---

## 2. Warehouse Operator Account (`Staff`)
- **Username**: `staff`
- **Password**: `123`
- **Privileges**:
  - Operational Warehouse Dashboard (physical stock counts & restock alerts)
  - Read-Only Product Catalog (browse SKUs, barcodes, on-hand inventory levels)
  - Sensitive financial figures (Supplier Cost & Profit Margins) are masked (`"—"`)
  - Catalog mutation (Add, Edit, Delete) is restricted to Administrators only
  - Double-clicking products triggers Quick Restock directly for warehouse efficiency
  - Record all inventory movements (Stock In / Intake, Stock Out / Dispatch, Physical Adjustments) attributed to operator ID
  - Run physical audit verification scans
  - System Settings tab is hidden from navigation

---

## 3. Sales Representative Account (`Sales Staff`)
- **Username**: `sales`
- **Password**: `123`
- **Privileges**:
  - Dedicated Sales & Outflow Dashboard (catalog availability, today's dispatched units, live on-hand quantities)
  - Product Sales Catalog (browse SKUs, retail Selling Prices, barcodes, and live stock)
  - Confidential supplier cost prices and profit margins are masked (`"—"`)
  - Catalog definitions are protected: cannot Add, Edit, or Delete products (Admin only)
  - Double-clicking any product or clicking "🛒 Sell Selected Item" opens the Customer Sales Dispatch modal directly
  - Movement recording is strictly focused on **"OUT (Sale / Dispatch)"**; supplier intake ("IN") and count overrides ("ADJUSTMENT") are restricted to warehouse/admin personnel
  - All committed customer sales are logged in the immutable audit trail with `Handled_By: sales`
  - System Settings and Inventory Audits tabs are hidden from navigation
