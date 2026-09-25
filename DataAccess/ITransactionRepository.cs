using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// Repository interface managing inventory audit log transactions and chart aggregations.
    /// </summary>
    public interface ITransactionRepository
    {
        long RecordStockTransaction(StockTransaction transaction, SqlTransaction? transactionScope = null);
        IEnumerable<StockTransaction> GetRecentTransactions(int limit = 50);
        IEnumerable<MonthlyMovementDto> GetMonthlyMovements(int monthsBack = 6);
        IEnumerable<CategoryValuationDto> GetCategoryValuations();
    }
}
