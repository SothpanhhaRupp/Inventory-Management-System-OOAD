using System;
using System.Windows.Forms;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.Models;
using Inventory_Management_System.UI;

namespace Inventory_Management_System
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the enterprise inventory application.
        /// Enforces authentication: checks for an active 7-day Remember-Me session,
        /// or presents the interactive LoginForm before launching the Dashboard.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Initialize High-DPI and modern WinForms rendering
            ApplicationConfiguration.Initialize();

            var authService = new AuthService();

            while (true)
            {
                User? currentUser = null;

                // 1. Check for active, non-expired 7-day Remember-Me session
                if (authService.TryAutoLogin(out var rememberedUser) && rememberedUser != null)
                {
                    currentUser = rememberedUser;
                }
                else
                {
                    // 2. No valid session: present modern login screen
                    using var loginForm = new LoginForm(authService);
                    if (loginForm.ShowDialog() != DialogResult.OK || loginForm.AuthenticatedUser == null)
                    {
                        // User cancelled or closed the login dialog: terminate cleanly
                        break;
                    }

                    currentUser = loginForm.AuthenticatedUser;
                }

                // 3. Launch Dashboard with authenticated user
                using var dashboard = new DashboardForm(new InventoryService(), currentUser, authService);
                Application.Run(dashboard);

                // If user did not click "Logout", exit the application
                if (!dashboard.LoggedOut)
                {
                    break;
                }
            }
        }
    }
}