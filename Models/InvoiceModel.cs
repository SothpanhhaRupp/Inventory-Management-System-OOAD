using System;
using System.Collections.Generic;

namespace Inventory_Management_System.Models
{
    /// <summary>
    /// Represents an individual line item on a customer invoice or movement receipt.
    /// </summary>
    public class InvoiceItem
    {
        public int ItemNumber { get; set; } = 1;
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount => Quantity * UnitPrice;
    }

    /// <summary>
    /// Complete domain model representing a printable enterprise invoice, sales dispatch receipt,
    /// or inventory movement voucher.
    /// </summary>
    public class Invoice
    {
        public string InvoiceNumber { get; set; } = "52148";
        public DateTime IssueDate { get; set; } = DateTime.Now;
        public string TransactionType { get; set; } = "OUT"; // 'OUT' (Sale), 'IN' (Intake), 'ADJUSTMENT'
        public string DocumentTitle { get; set; } = "INVOICE";
        
        // Brand & Company info
        public string CompanyName { get; set; } = "Brand Name";
        public string CompanyTagline { get; set; } = "TAGLINE SPACE HERE";
        
        // Customer / Billed To
        public string CustomerOrSupplier { get; set; } = "Dwyane Clark";
        public string CustomerAddressLine1 { get; set; } = "24 Dummy Street Area,";
        public string CustomerAddressLine2 { get; set; } = "Location, Lorem Ipsum,";
        public string CustomerAddressLine3 { get; set; } = "570xx59x";
        
        // Transaction meta
        public string ReferenceNo { get; set; } = string.Empty;
        public string HandledBy { get; set; } = "System Administrator";
        public string OperatorRole { get; set; } = "Sales Staff";
        public string Notes { get; set; } = string.Empty;
        
        // Payment & Terms (as shown in template)
        public string TermsConditions { get; set; } = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce dignissim pretium consectetur.";
        public string AccountNumber { get; set; } = "1234 5678 9012";
        public string AccountName { get; set; } = "Lorem Ipsum";
        public string BankDetails { get; set; } = "Add your bank details";
        
        // Footer contact
        public string Phone { get; set; } = "Phone #";
        public string Address { get; set; } = "Address";
        public string Website { get; set; } = "Website";

        public List<InvoiceItem> Items { get; set; } = new();

        public decimal Subtotal
        {
            get
            {
                decimal sum = 0;
                foreach (var it in Items) sum += it.TotalAmount;
                return sum;
            }
        }

        public decimal TaxRate { get; set; } = 0.00m; // 0.00%
        public decimal TaxAmount => Subtotal * TaxRate;
        public decimal GrandTotal => Subtotal + TaxAmount;

        /// <summary>
        /// Factory helper to build an Invoice from a StockTransaction record and optional Product details.
        /// </summary>
        public static Invoice FromTransaction(StockTransaction tx, Product? product = null, string? operatorRole = null)
        {
            string docTitle = tx.TransactionType switch
            {
                "OUT" => "CUSTOMER SALES INVOICE",
                "IN" => "PURCHASE & INTAKE RECEIPT",
                _ => "INVENTORY ADJUSTMENT VOUCHER"
            };

            string party = tx.TransactionType switch
            {
                "OUT" => !string.IsNullOrWhiteSpace(tx.ReferenceNo) ? $"Client Ref: {tx.ReferenceNo}" : "Walk-in Retail Customer",
                "IN" => product?.SupplierName ?? "Authorized Wholesale Vendor",
                _ => "Warehouse Internal Audit"
            };

            var invoice = new Invoice
            {
                InvoiceNumber = tx.TransactionID > 0 ? $"{tx.TransactionID:D5}" : "52148",
                IssueDate = tx.TransactionDate,
                TransactionType = tx.TransactionType,
                DocumentTitle = "INVOICE",
                CompanyName = "Brand Name",
                CompanyTagline = "TAGLINE SPACE HERE",
                CustomerOrSupplier = party,
                CustomerAddressLine1 = "24 Dummy Street Area,",
                CustomerAddressLine2 = "Location, Lorem Ipsum,",
                CustomerAddressLine3 = "570xx59x",
                ReferenceNo = tx.ReferenceNo ?? $"REF-{tx.TransactionID:D5}",
                HandledBy = !string.IsNullOrWhiteSpace(tx.CreatedByName) ? tx.CreatedByName : "Staff Operator",
                OperatorRole = operatorRole ?? "Sales Staff",
                Notes = !string.IsNullOrWhiteSpace(tx.Notes) ? tx.Notes : "Standard commercial dispatch. Goods inspected and confirmed.",
                TermsConditions = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce dignissim pretium consectetur.",
                AccountNumber = "1234 5678 9012",
                AccountName = "Lorem Ipsum",
                BankDetails = "Add your bank details",
                Phone = "Phone #",
                Address = "Address",
                Website = "Website",
                Items = new List<InvoiceItem>
                {
                    new InvoiceItem
                    {
                        ItemNumber = 1,
                        SKU = !string.IsNullOrWhiteSpace(tx.SKU) ? tx.SKU : (product?.SKU ?? "SKU-GEN"),
                        ProductName = !string.IsNullOrWhiteSpace(tx.ProductName) ? tx.ProductName : (product?.ProductName ?? "Lorem Ipsum Dolor"),
                        Category = product?.CategoryName ?? "General",
                        Quantity = tx.Quantity,
                        UnitPrice = tx.UnitPrice
                    }
                }
            };

            return invoice;
        }
    }
}
