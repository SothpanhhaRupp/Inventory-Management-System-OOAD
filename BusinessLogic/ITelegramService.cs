using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Service contract for dispatching real-time notifications to Telegram Bot in Khmer.
    /// Follows Dependency Inversion Principle (DIP) and Single Responsibility Principle (SRP).
    /// </summary>
    public interface ITelegramService
    {
        string BotToken { get; }
        string ChatId { get; }
        bool IsEnabled { get; set; }

        void UpdateConfig(string botToken, string chatId, bool isEnabled);
        Task<bool> SendLowStockAlertAsync(Product product, bool force = false);
        Task<bool> SendOutOfStockAlertAsync(Product product, bool force = false);
        Task<bool> SendUrgentRestockSummaryAsync(IEnumerable<Product> urgentProducts);
        Task<bool> SendTestNotificationAsync();
        Task<(bool Success, string? DetectedChatId, string? SenderName, string? Error)> DetectLatestChatIdAsync();
    }
}
