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
    }
}
