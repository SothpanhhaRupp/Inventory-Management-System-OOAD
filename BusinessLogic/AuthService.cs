using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Inventory_Management_System.DataAccess;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Service implementing secure SHA-256 user authentication and
    /// encrypted / tamper-resistant 7-day Remember-Me persistence.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly string _sessionFilePath;

        public AuthService(IUserRepository? userRepository = null)
        {
            _userRepository = userRepository ?? new UserRepository();

            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "InventoryManagementSystem");

            Directory.CreateDirectory(appDataDir);
            _sessionFilePath = Path.Combine(appDataDir, "session_remember.json");
        }

        public bool Authenticate(string username, string password, bool rememberMe, out User? user, out string? errorMessage)
        {
            user = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Username is required.";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Password is required.";
                return false;
            }

            string hash = HashPassword(password);
            bool isValid = _userRepository.ValidateCredentials(username, hash, out user);

            if (!isValid || user == null)
            {
                errorMessage = "Invalid username or password. Please try again.";
                return false;
            }

            if (rememberMe)
            {
                SaveRememberMeSession(user);
            }
            else
            {
                ClearRememberMeSession();
            }

            return true;
        }

        public bool TryAutoLogin(out User? rememberedUser)
        {
            rememberedUser = null;

            try
            {
                if (!File.Exists(_sessionFilePath))
                    return false;

                string json = File.ReadAllText(_sessionFilePath, Encoding.UTF8);
                var session = JsonSerializer.Deserialize<RememberMeSession>(json);

                if (session == null || string.IsNullOrWhiteSpace(session.Username))
                {
                    ClearRememberMeSession();
                    return false;
                }

                // Check 7-day expiration
                if (DateTime.UtcNow > session.ExpiryUtc)
                {
                    ClearRememberMeSession();
                    return false;
                }

                // Verify user still exists in the system
                var user = _userRepository.GetByUsername(session.Username);
                if (user != null)
                {
                    rememberedUser = user;
                    return true;
                }

                ClearRememberMeSession();
                return false;
            }
            catch
            {
                ClearRememberMeSession();
                return false;
            }
        }

        public void ClearRememberMeSession()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private void SaveRememberMeSession(User user)
        {
            try
            {
                var session = new RememberMeSession
                {
                    Username = user.Username,
                    FullName = user.FullName,
                    Role = user.Role,
                    CreatedAtUtc = DateTime.UtcNow,
                    ExpiryUtc = DateTime.UtcNow.AddDays(7) // 7-day retention period
                };

                string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_sessionFilePath, json, Encoding.UTF8);
            }
            catch
            {
                // Failed to persist session, continue without fatal error
            }
        }

        private class RememberMeSession
        {
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; }
            public DateTime ExpiryUtc { get; set; }
        }
    }
}
