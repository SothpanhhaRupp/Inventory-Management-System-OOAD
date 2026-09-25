using Inventory_Management_System.Models;

namespace Inventory_Management_System.BusinessLogic
{
    /// <summary>
    /// Authentication service contract handling credential validation,
    /// password hashing, and persistent 7-day 'Remember Me' session management.
    /// </summary>
    public interface IAuthService
    {
        bool Authenticate(string username, string password, bool rememberMe, out User? user, out string? errorMessage);
        bool TryAutoLogin(out User? rememberedUser);
        void ClearRememberMeSession();
        string HashPassword(string password);
    }
}
