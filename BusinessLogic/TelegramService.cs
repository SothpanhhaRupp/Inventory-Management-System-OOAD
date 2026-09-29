using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Thread-safe service for sending low-stock alerts to Telegram Bot in Khmer.
    /// Employs Singleton / Service pattern with async HTTP dispatching to avoid blocking UI and database operations.
    /// </summary>
    public class TelegramService : ITelegramService
    {
        private static readonly Lazy<TelegramService> _lazyInstance = new(() => new TelegramService());
        public static TelegramService Instance => _lazyInstance.Value;

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Cache recently alerted product ID -> (LastStock, AlertTimestamp) to avoid notification floods
        private readonly ConcurrentDictionary<int, (int Stock, DateTime Timestamp)> _alertCooldowns = new();

        public string BotToken { get; private set; } = "8983227565:AAGcRru8Ndl2zze-PdSvqjGfKKFI_PSNAfY";
        public string ChatId { get; private set; } = "1712932157";
        public bool IsEnabled { get; set; } = true;

        public TelegramService()
        {
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                string? token = ConfigurationManager.AppSettings["TelegramBotToken"];
                if (!string.IsNullOrWhiteSpace(token))
                {
                    BotToken = token.Trim();
                }

                string? chatId = ConfigurationManager.AppSettings["TelegramChatId"];
                if (!string.IsNullOrWhiteSpace(chatId))
                {
                    ChatId = chatId.Trim();
                }

                string? enabled = ConfigurationManager.AppSettings["TelegramAlertsEnabled"];
                if (bool.TryParse(enabled, out bool isAlertEnabled))
                {
                    IsEnabled = isAlertEnabled;
                }
            }
            catch
            {
                // Fall back to pre-configured defaults
            }
        }

        public void UpdateConfig(string botToken, string chatId, bool isEnabled)
        {
            if (!string.IsNullOrWhiteSpace(botToken))
            {
                BotToken = botToken.Trim();
            }

            if (!string.IsNullOrWhiteSpace(chatId))
            {
                ChatId = chatId.Trim();
            }

            IsEnabled = isEnabled;

            try
            {
                // Persist updates to App.config
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                
                SetAppSetting(config, "TelegramBotToken", BotToken);
                SetAppSetting(config, "TelegramChatId", ChatId);
                SetAppSetting(config, "TelegramAlertsEnabled", IsEnabled ? "true" : "false");

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch
            {
                // App.config write could fail if run from restricted directory; memory state is still updated
            }
        }

        private static void SetAppSetting(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] == null)
            {
                config.AppSettings.Settings.Add(key, value);
            }
            else
            {
                config.AppSettings.Settings[key].Value = value;
            }
        }

        public async Task<bool> SendLowStockAlertAsync(Product product, bool force = false)
        {
            if (!IsEnabled || product == null || string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChatId))
            {
                return false;
            }

            // Anti-spam cooldown check: skip if already alerted at the same stock level within 15 minutes, unless forced (e.g. sale movement)
            if (!force && _alertCooldowns.TryGetValue(product.ProductID, out var lastAlert))
            {
                if (lastAlert.Stock == product.CurrentStock && (DateTime.Now - lastAlert.Timestamp).TotalMinutes < 15)
                {
                    return false;
                }
            }

            string categoryDisplay = string.IsNullOrWhiteSpace(product.CategoryName) ? "ទូទៅ (General)" : EscapeHtml(product.CategoryName);
            string skuDisplay = EscapeHtml(product.SKU);
            string nameDisplay = EscapeHtml(product.ProductName);

            string message =
                $"⚠️ <b>ការជូនដំណឹង៖ ទំនិញជិតអស់ពីស្តុក! (Low Stock Alert)</b>\n" +
                $"━━━━━━━━━━━━━━━━━━━━\n" +
                $"📦 <b>ឈ្មោះទំនិញ៖</b> {nameDisplay}\n" +
                $"🏷️ <b>លេខកូដ (SKU)៖</b> <code>{skuDisplay}</code>\n" +
                $"📂 <b>ប្រភេទទំនិញ៖</b> {categoryDisplay}\n" +
                $"📊 <b>ចំនួននៅសល់ក្នុងស្តុក៖</b> <b>{product.CurrentStock}</b>\n" +
                $"📉 <b>កម្រិតកំណត់ត្រូវទិញបន្ថែម (Reorder Level)៖</b> {product.ReorderLevel}\n" +
                $"💵 <b>តម្លៃលក់៖</b> ${product.SellingPrice:N2}\n" +
                $"🕒 <b>កាលបរិច្ឆេទ៖</b> {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n\n" +
                $"🔔 <i>សូមមេត្តាពិនិត្យ និងបញ្ជាទិញទំនិញបន្ថែមជាបន្ទាន់ ដើម្បីជៀសវាងការដាច់ស្តុក!</i>";

            bool sent = await SendRawTelegramMessageAsync(message);
            if (sent)
            {
                _alertCooldowns[product.ProductID] = (product.CurrentStock, DateTime.Now);
            }

            return sent;
        }

        public async Task<bool> SendOutOfStockAlertAsync(Product product, bool force = false)
        {
            if (!IsEnabled || product == null || string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChatId))
            {
                return false;
            }

            if (!force && _alertCooldowns.TryGetValue(product.ProductID, out var lastAlert))
            {
                if (lastAlert.Stock == 0 && (DateTime.Now - lastAlert.Timestamp).TotalMinutes < 15)
                {
                    return false;
                }
            }

            string categoryDisplay = string.IsNullOrWhiteSpace(product.CategoryName) ? "ទូទៅ (General)" : EscapeHtml(product.CategoryName);
            string skuDisplay = EscapeHtml(product.SKU);
            string nameDisplay = EscapeHtml(product.ProductName);

            string message =
                $"🚨 <b>ការជូនដំណឹងបន្ទាន់៖ ទំនិញអស់ពីស្តុកហើយ! (Out of Stock)</b>\n" +
                $"━━━━━━━━━━━━━━━━━━━━\n" +
                $"📦 <b>ឈ្មោះទំនិញ៖</b> {nameDisplay}\n" +
                $"🏷️ <b>លេខកូដ (SKU)៖</b> <code>{skuDisplay}</code>\n" +
                $"📂 <b>ប្រភេទទំនិញ៖</b> {categoryDisplay}\n" +
                $"❌ <b>ស្ថានភាពស្តុក៖</b> <b>អស់ពីស្តុក (0)</b>\n" +
                $"📉 <b>កម្រិតកំណត់ (Reorder Level)៖</b> {product.ReorderLevel}\n" +
                $"💵 <b>តម្លៃលក់៖</b> ${product.SellingPrice:N2}\n" +
                $"🕒 <b>កាលបរិច្ឆេទ៖</b> {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n\n" +
                $"⛔ <i>ទំនិញនេះត្រូវបានលក់អស់ទាំងស្រុងហើយ សូមទាក់ទងអ្នកផ្គត់ផ្គង់ដើម្បីនាំចូលជាបន្ទាន់!</i>";

            bool sent = await SendRawTelegramMessageAsync(message);
            if (sent)
            {
                _alertCooldowns[product.ProductID] = (0, DateTime.Now);
            }

            return sent;
        }

        public async Task<bool> SendUrgentRestockSummaryAsync(IEnumerable<Product> urgentProducts)
        {
            if (!IsEnabled || urgentProducts == null || string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChatId))
            {
                return false;
            }

            var list = urgentProducts.ToList();
            if (list.Count == 0)
            {
                return false;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📋 <b>របាយការណ៍សង្ខេប៖ ទំនិញត្រូវបំពេញស្តុកបន្ទាន់ (សរុប {list.Count} មុខ)</b>");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━");

            int index = 1;
            foreach (var item in list.Take(15))
            {
                string statusEmoji = item.CurrentStock <= 0 ? "❌ [ដាច់ស្តុក]" : "⚠️ [ជិតអស់]";
                sb.AppendLine($"{index++}. <b>{EscapeHtml(item.ProductName)}</b> (<code>{EscapeHtml(item.SKU)}</code>)");
                sb.AppendLine($"   {statusEmoji} នៅសល់: <b>{item.CurrentStock}</b> | កម្រិតកំណត់: {item.ReorderLevel} | តម្លៃ: ${item.SellingPrice:N2}");
            }

            if (list.Count > 15)
            {
                sb.AppendLine($"\n<i>... និងមានទំនិញផ្សេងទៀតចំនួន {list.Count - 15} មុខទៀតក្នុងប្រព័ន្ធ។</i>");
            }

            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"🕒 <b>កាលបរិច្ឆេទ៖</b> {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine("🏢 <b>ប្រព័ន្ធគ្រប់គ្រងស្តុកទំនិញ (Inventory Management System)</b>");

            return await SendRawTelegramMessageAsync(sb.ToString());
        }

        public async Task<bool> SendTestNotificationAsync()
        {
            if (string.IsNullOrWhiteSpace(BotToken) || string.IsNullOrWhiteSpace(ChatId))
            {
                return false;
            }

            string testMsg =
                $"✅ <b>ប្រព័ន្ធគ្រប់គ្រងស្តុកទំនិញ (Inventory Management System)</b>\n" +
                $"━━━━━━━━━━━━━━━━━━━━\n" +
                $"🟢 <b>ការតភ្ជាប់ជាមួយ Telegram Bot ទទួលបានជោគជ័យ!</b>\n\n" +
                $"🤖 <b>Bot៖</b> Inventory Alert Bot\n" +
                $"💬 <b>Chat ID៖</b> <code>{ChatId}</code>\n" +
                $"🔔 <b>មុខងារជូនដំណឹង៖</b> បានបើកដំណើរការ (Active)\n\n" +
                $"ប្រព័ន្ធនឹងផ្ញើសារជូនដំណឹងជាភាសាខ្មែរដោយស្វ័យប្រវត្តិនូវរាល់ពេលដែលទំនិញធ្លាក់ចុះដល់កម្រិតជិតអស់ពីស្តុក (Current Stock ≤ Reorder Level) ឬអស់ពីស្តុក។\n" +
                $"━━━━━━━━━━━━━━━━━━━━\n" +
                $"🕒 <b>កាលបរិច្ឆេទ៖</b> {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

            return await SendRawTelegramMessageAsync(testMsg);
        }

        public async Task<(bool Success, string? DetectedChatId, string? SenderName, string? Error)> DetectLatestChatIdAsync()
        {
            if (string.IsNullOrWhiteSpace(BotToken))
            {
                return (false, null, null, "Bot token is empty.");
            }

            try
            {
                string url = $"https://api.telegram.org/bot{BotToken}/getUpdates?offset=-1";
                using var response = await _httpClient.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.GetProperty("ok").GetBoolean())
                {
                    return (false, null, null, "Telegram API returned ok: false");
                }

                var results = root.GetProperty("result");
                if (results.GetArrayLength() == 0)
                {
                    return (false, null, null, "No messages found. Please send '/start' or any message to your Telegram Bot first.");
                }

                var lastUpdate = results[results.GetArrayLength() - 1];
                if (lastUpdate.TryGetProperty("message", out var msgElement) &&
                    msgElement.TryGetProperty("chat", out var chatElement))
                {
                    long id = chatElement.GetProperty("id").GetInt64();
                    string detectedId = id.ToString();
                    
                    string senderName = "User";
                    if (chatElement.TryGetProperty("first_name", out var fn))
                    {
                        senderName = fn.GetString() ?? "User";
                    }
                    if (chatElement.TryGetProperty("last_name", out var ln) && !string.IsNullOrWhiteSpace(ln.GetString()))
                    {
                        senderName += " " + ln.GetString();
                    }

                    return (true, detectedId, senderName, null);
                }

                return (false, null, null, "Could not find chat ID in latest update.");
            }
            catch (Exception ex)
            {
                return (false, null, null, ex.Message);
            }
        }

        private async Task<bool> SendRawTelegramMessageAsync(string htmlText)
        {
            try
            {
                string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";

                var payload = new
                {
                    chat_id = ChatId,
                    text = htmlText,
                    parse_mode = "HTML"
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // Resilient Fallback: If Telegram rejected HTML parsing, strip tags and send as plain text
                string plainText = StripHtml(htmlText);
                var fallbackPayload = new
                {
                    chat_id = ChatId,
                    text = plainText
                };

                string fallbackJson = JsonSerializer.Serialize(fallbackPayload);
                using var fallbackContent = new StringContent(fallbackJson, Encoding.UTF8, "application/json");
                using var fallbackResponse = await _httpClient.PostAsync(url, fallbackContent);
                return fallbackResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static string StripHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var clean = System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
            return clean.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        }

        private static string EscapeHtml(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");
        }
    }
}
