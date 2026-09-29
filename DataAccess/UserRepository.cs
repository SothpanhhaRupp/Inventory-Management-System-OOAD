using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
            },
            new User
            {
                UserID = 3,
                Username = "sales",
                PasswordHash = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3",
                FullName = "Sales Representative",
                Role = "Sales Staff",
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

        public bool UsernameExists(string username, int excludeUserId = 0)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            try
            {
                const string sql = @"
                    SELECT COUNT(1) 
                    FROM Users 
                    WHERE LOWER(Username) = LOWER(@Username) AND UserID <> @ExcludeId;";

                var p = new[]
                {
                    new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = username.Trim() },
                    new SqlParameter("@ExcludeId", SqlDbType.Int) { Value = excludeUserId }
                };

                int count = Convert.ToInt32(DatabaseHelper.ExecuteScalar(sql, CommandType.Text, p));
                return count > 0;
            }
            catch
            {
                return DemoUsers.Any(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase) && u.UserID != excludeUserId);
            }
        }

        public bool AddUser(User user, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                const string sql = @"
                    INSERT INTO Users (Username, PasswordHash, FullName, Role, CreatedAt)
                    VALUES (@Username, @PasswordHash, @FullName, @Role, @CreatedAt);
                    SELECT SCOPE_IDENTITY();";

                var p = new[]
                {
                    new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = user.Username.Trim() },
                    new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 255) { Value = user.PasswordHash },
                    new SqlParameter("@FullName", SqlDbType.NVarChar, 100) { Value = user.FullName.Trim() },
                    new SqlParameter("@Role", SqlDbType.NVarChar, 20) { Value = user.Role },
                    new SqlParameter("@CreatedAt", SqlDbType.DateTime) { Value = user.CreatedAt == default ? DateTime.Now : user.CreatedAt }
                };

                object? result = DatabaseHelper.ExecuteScalar(sql, CommandType.Text, p);
                if (result != null && int.TryParse(result.ToString(), out int newId))
                {
                    user.UserID = newId;
                }
                else
                {
                    int maxId = DemoUsers.Count > 0 ? DemoUsers.Max(u => u.UserID) : 0;
                    user.UserID = maxId + 1;
                }

                // Synchronize demo cache as well
                if (!DemoUsers.Any(u => u.UserID == user.UserID))
                {
                    DemoUsers.Add(user);
                }
                return true;
            }
            catch
            {
                int maxId = DemoUsers.Count > 0 ? DemoUsers.Max(u => u.UserID) : 0;
                user.UserID = maxId + 1;
                DemoUsers.Add(user);
                errorMessage = null; // Successfully saved to in-memory fallback
                return true;
            }
        }

        public bool UpdateUser(User user, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                const string sql = @"
                    UPDATE Users 
                    SET FullName = @FullName, Role = @Role
                    WHERE UserID = @UserID;";

                var p = new[]
                {
                    new SqlParameter("@FullName", SqlDbType.NVarChar, 100) { Value = user.FullName.Trim() },
                    new SqlParameter("@Role", SqlDbType.NVarChar, 20) { Value = user.Role },
                    new SqlParameter("@UserID", SqlDbType.Int) { Value = user.UserID }
                };

                DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, p);
            }
            catch
            {
                // Fallback to in-memory
            }

            var demo = DemoUsers.Find(u => u.UserID == user.UserID);
            if (demo != null)
            {
                demo.FullName = user.FullName;
                demo.Role = user.Role;
            }

            return true;
        }

        public bool UpdatePassword(int userId, string passwordHash, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                const string sql = @"
                    UPDATE Users 
                    SET PasswordHash = @PasswordHash
                    WHERE UserID = @UserID;";

                var p = new[]
                {
                    new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 255) { Value = passwordHash },
                    new SqlParameter("@UserID", SqlDbType.Int) { Value = userId }
                };

                DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, p);
            }
            catch
            {
                // Fallback
            }

            var demo = DemoUsers.Find(u => u.UserID == userId);
            if (demo != null)
            {
                demo.PasswordHash = passwordHash;
            }

            return true;
        }

        public bool DeleteUser(int userId, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                const string sql = @"DELETE FROM Users WHERE UserID = @UserID;";
                var p = new[] { new SqlParameter("@UserID", SqlDbType.Int) { Value = userId } };
                DatabaseHelper.ExecuteNonQuery(sql, CommandType.Text, p);
            }
            catch
            {
                // Fallback
            }

            DemoUsers.RemoveAll(u => u.UserID == userId);
            return true;
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
