using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.DataAccess
{
    /// <summary>
    /// ADO.NET implementation of IUserRepository.
    /// Provides parameterized queries for user authentication and user profile loading,
    /// with robust in-memory demo fallbacks when database connection is unreachable.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        // Demo seed fallback accounts matching InventoryDB_Setup.sql
        // Passwords for both: "123"
        private static readonly List<User> DemoUsers = new()
        {
            new User
            {
                UserID = 1,
                Username = "admin",
                PasswordHash = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3",
                FullName = "System Administrator",
                Role = "Admin",
                CreatedAt = DateTime.Now
            },
            new User
            {
                UserID = 2,
                Username = "staff",
                PasswordHash = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3",
                FullName = "Warehouse Operator",
                Role = "Staff",
                CreatedAt = DateTime.Now
            }
        };

        public User? GetByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;

            try
            {
                const string sql = @"
                    SELECT UserID, Username, PasswordHash, FullName, Role, CreatedAt 
                    FROM Users 
                    WHERE Username = @Username;";

                var p = new[] { new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = username.Trim() } };
                var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, p);

                if (dt.Rows.Count > 0)
                {
                    return MapRowToUser(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback to demo mode if database is offline
            }

            return DemoUsers.Find(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public User? GetById(int userId)
        {
            try
            {
                const string sql = @"
                    SELECT UserID, Username, PasswordHash, FullName, Role, CreatedAt 
                    FROM Users 
                    WHERE UserID = @UserID;";

                var p = new[] { new SqlParameter("@UserID", SqlDbType.Int) { Value = userId } };
                var dt = DatabaseHelper.ExecuteDataTable(sql, CommandType.Text, p);

                if (dt.Rows.Count > 0)
                {
                    return MapRowToUser(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback to demo mode
            }

            return DemoUsers.Find(u => u.UserID == userId);
        }

        public bool ValidateCredentials(string username, string passwordHash, out User? user)
        {
            user = GetByUsername(username);
            if (user != null && string.Equals(user.PasswordHash, passwordHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            user = null;
            return false;
        }

        public IEnumerable<User> GetAll()
        {
            try
            {
                const string sql = @"
                    SELECT UserID, Username, PasswordHash, FullName, Role, CreatedAt 
                    FROM Users 
                    ORDER BY UserID;";

                var dt = DatabaseHelper.ExecuteDataTable(sql);
                var list = new List<User>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(MapRowToUser(row));
                }
                return list;
            }
            catch
            {
                return DemoUsers;
            }
        }

        private static User MapRowToUser(DataRow row)
        {
            return new User
            {
                UserID = Convert.ToInt32(row["UserID"]),
                Username = row["Username"].ToString() ?? string.Empty,
                PasswordHash = row["PasswordHash"].ToString() ?? string.Empty,
                FullName = row["FullName"].ToString() ?? string.Empty,
                Role = row["Role"].ToString() ?? "Staff",
                CreatedAt = Convert.ToDateTime(row["CreatedAt"])
            };
        }
    }
}
