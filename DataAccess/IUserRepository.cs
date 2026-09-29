using System.Collections.Generic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// Repository abstraction for User identity and authentication persistence.
    /// </summary>
    public interface IUserRepository
    {
        User? GetByUsername(string username);
        User? GetById(int userId);
        bool ValidateCredentials(string username, string passwordHash, out User? user);
        IEnumerable<User> GetAll();
        bool AddUser(User user, out string? errorMessage);
        bool UpdateUser(User user, out string? errorMessage);
        bool DeleteUser(int userId, out string? errorMessage);
        bool UpdatePassword(int userId, string passwordHash, out string? errorMessage);
        bool UsernameExists(string username, int excludeUserId = 0);
    }
}
