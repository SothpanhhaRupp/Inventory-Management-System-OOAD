using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Also provides administrative user lifecycle and RBAC account provisioning.
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

        public IEnumerable<User> GetAllUsers()
        {
            return _userRepository.GetAll();
        }

        public bool CreateUser(User user, string plainPassword, out string? errorMessage)
        {
            errorMessage = null;
            if (user == null)
            {
                errorMessage = "User information cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.Username))
            {
                errorMessage = "Username is required.";
                return false;
            }

            if (user.Username.Trim().Length < 3)
            {
                errorMessage = "Username must be at least 3 characters long.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                errorMessage = "Full Name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(plainPassword))
            {
                errorMessage = "Password is required for new user accounts.";
                return false;
            }

            if (plainPassword.Length < 3)
            {
                errorMessage = "Password must be at least 3 characters long.";
                return false;
            }

            if (_userRepository.UsernameExists(user.Username))
            {
                errorMessage = $"The username '{user.Username.Trim()}' is already taken. Please choose another username.";
                return false;
            }

            // Normalize role
            string role = user.Role?.Trim() ?? "Staff";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, "Sales Staff", StringComparison.OrdinalIgnoreCase))
            {
                role = "Staff";
            }
            user.Role = role;
            user.PasswordHash = HashPassword(plainPassword);
            user.CreatedAt = DateTime.Now;

            return _userRepository.AddUser(user, out errorMessage);
        }

        public bool UpdateUser(User user, string? newPlainPassword, out string? errorMessage)
        {
            errorMessage = null;
            if (user == null)
            {
                errorMessage = "User cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                errorMessage = "Full Name is required.";
                return false;
            }

            // Check if username changed and collides
            if (_userRepository.UsernameExists(user.Username, user.UserID))
            {
                errorMessage = $"The username '{user.Username.Trim()}' is already taken by another account.";
                return false;
            }

            // Normalize role
            string role = user.Role?.Trim() ?? "Staff";
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, "Sales Staff", StringComparison.OrdinalIgnoreCase))
            {
                role = "Staff";
            }
            user.Role = role;

            bool success = _userRepository.UpdateUser(user, out errorMessage);
            if (!success) return false;

            if (!string.IsNullOrWhiteSpace(newPlainPassword))
            {
                if (newPlainPassword.Length < 3)
                {
                    errorMessage = "Password must be at least 3 characters long.";
                    return false;
                }
                string newHash = HashPassword(newPlainPassword);
                return _userRepository.UpdatePassword(user.UserID, newHash, out errorMessage);
            }

            return true;
        }

        public bool DeleteUser(int userId, int currentAdminUserId, out string? errorMessage)
        {
            errorMessage = null;

            if (userId == currentAdminUserId)
            {
                errorMessage = "Security Restriction: You cannot delete your own currently logged-in administrator account.";
                return false;
            }

            var allUsers = _userRepository.GetAll().ToList();
            var target = allUsers.FirstOrDefault(u => u.UserID == userId);
            if (target == null)
            {
                errorMessage = "Selected user could not be found in the system.";
                return false;
            }

            if (target.IsAdmin)
            {
                int adminCount = allUsers.Count(u => u.IsAdmin);
                if (adminCount <= 1)
                {
                    errorMessage = "Protection Rule: Cannot delete the last remaining Administrator account. The system requires at least one Admin.";
                    return false;
                }
            }

            return _userRepository.DeleteUser(userId, out errorMessage);
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
