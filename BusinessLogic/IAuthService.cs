using System.Collections.Generic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Authentication service contract handling credential validation,
    /// password hashing, and persistent 7-day 'Remember Me' session management.
    /// Also provides administrative user lifecycle and RBAC account provisioning.
    /// </summary>
    public interface IAuthService
    {
        bool Authenticate(string username, string password, bool rememberMe, out User? user, out string? errorMessage);
        bool TryAutoLogin(out User? rememberedUser);
        void ClearRememberMeSession();
        string HashPassword(string password);

        IEnumerable<User> GetAllUsers();
        bool CreateUser(User user, string plainPassword, out string? errorMessage);
        bool UpdateUser(User user, string? newPlainPassword, out string? errorMessage);
        bool DeleteUser(int userId, int currentAdminUserId, out string? errorMessage);
    }
}
