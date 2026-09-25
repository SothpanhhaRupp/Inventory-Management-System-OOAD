using System;

namespace Inventory_Management_System.Exceptions
{
    /// <summary>
    /// Base exception for all domain-specific errors in the Inventory Management System.
    /// Ensures proper OOAD exception hierarchy and separation of domain errors from system faults.
    /// </summary>
    public class InventoryDomainException : Exception
    {
        public string ErrorCode { get; }

        public InventoryDomainException(string message, string errorCode = "DOMAIN_ERROR") 
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public InventoryDomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR") 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Thrown when an inventory deduction (Stock OUT) exceeds available CurrentStock.
    /// </summary>
    public class InsufficientStockException : InventoryDomainException
    {
        public int ProductId { get; }
        public string Sku { get; }
        public int AvailableStock { get; }
        public int RequestedQuantity { get; }

        public InsufficientStockException(int productId, string sku, int availableStock, int requestedQuantity)
            : base($"Insufficient stock for product '{sku}' (ID: {productId}). Available: {availableStock}, Requested: {requestedQuantity}.", "INSUFFICIENT_STOCK")
        {
            ProductId = productId;
            Sku = sku;
            AvailableStock = availableStock;
            RequestedQuantity = requestedQuantity;
        }
    }

    /// <summary>
    /// Thrown when an attempt is made to insert or update a product with an already existing SKU.
    /// </summary>
    public class DuplicateSkuException : InventoryDomainException
    {
        public string Sku { get; }

        public DuplicateSkuException(string sku)
            : base($"A product with SKU '{sku}' already exists in the system. SKU must be globally unique.", "DUPLICATE_SKU")
        {
            Sku = sku;
        }
    }

    /// <summary>
    /// Thrown when an entity (Product, Category, Supplier, User) is not found in the database.
    /// </summary>
    public class EntityNotFoundException : InventoryDomainException
    {
        public string EntityName { get; }
        public object Key { get; }

        public EntityNotFoundException(string entityName, object key)
            : base($"{entityName} with key '{key}' was not found.", "ENTITY_NOT_FOUND")
        {
            EntityName = entityName;
            Key = key;
        }
    }
}
