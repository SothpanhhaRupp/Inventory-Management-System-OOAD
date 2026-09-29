using System;
using System.Drawing;
using System.Windows.Forms;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Modal dialog for creating new user accounts or updating existing credentials and roles.
    /// Provides real-time RBAC privilege previews, credential verification, and safety validations.
    /// </summary>
    public class AddEditUserModalForm : Form
    {
        private readonly IAuthService _authService;
        private readonly User? _existingUser;

        private TextBox _txtUsername = null!;
        private TextBox _txtFullName = null!;
        private ComboBox _cboRole = null!;
        private Label _lblRoleHelp = null!;
        private TextBox _txtPassword = null!;
        private TextBox _txtConfirmPassword = null!;
        private Label _lblPasswordHint = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public User? SavedUser { get; private set; }

        public AddEditUserModalForm(IAuthService authService, User? existingUser = null)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _existingUser = existingUser;

            InitializeForm();
            PopulateData();
        }

        private void InitializeForm()
        {
            Text = _existingUser == null ? "Create New System User" : $"Edit User: {_existingUser.Username}";
            Size = new Size(560, 620);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.CanvasBg;
            Font = Theme.FontBody;

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = Theme.CanvasBg
            };

            int y = 14;
            var lblTitle = new Label
            {
                Text = _existingUser == null ? "👤  Register New User Account" : $"👤  Edit User: {_existingUser.FullName}",
                Font = Theme.FontHeadingMd,
                ForeColor = Theme.TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            panel.Controls.Add(lblTitle);
            y += 28;

            var lblSub = new Label
            {
                Text = "Configure user credentials, display name, and Role-Based Access Control (RBAC) tier",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            };
            panel.Controls.Add(lblSub);
            y += 28;

            var card = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(496, 440),
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            card.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            panel.Controls.Add(card);

            int cy = 16;

            // Username
            var lblUsername = new Label
            {
                Text = _existingUser == null ? "Username *" : "Username (Immutable Identifier)",
                Font = Theme.FontCaptionBold,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(lblUsername);
            cy += 22;

            _txtUsername = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(460, 30),
                Font = Theme.FontBody,
                PlaceholderText = "e.g., panha, john_sales, warehouse_op..."
            };
            if (_existingUser != null)
            {
                _txtUsername.ReadOnly = true;
                _txtUsername.BackColor = ColorTranslator.FromHtml("#F1F5F9");
            }
            card.Controls.Add(_txtUsername);
            cy += 38;

            // Full Name
            var lblFullName = new Label
            {
                Text = "Full Name *",
                Font = Theme.FontCaptionBold,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(lblFullName);
            cy += 22;

            _txtFullName = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(460, 30),
                Font = Theme.FontBody,
                PlaceholderText = "e.g., Soth Panha, John Doe..."
            };
            card.Controls.Add(_txtFullName);
            cy += 38;

            // Role
            var lblRole = new Label
            {
                Text = "Security Role (RBAC Level) *",
                Font = Theme.FontCaptionBold,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(lblRole);
            cy += 22;

            _cboRole = new ComboBox
            {
                Location = new Point(16, cy),
                Size = new Size(460, 30),
                Font = Theme.FontBody,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboRole.Items.AddRange(new object[] { "Admin", "Staff", "Sales Staff" });
            _cboRole.SelectedIndex = 1; // Default to Staff
            _cboRole.SelectedIndexChanged += OnRoleSelectionChanged;
            card.Controls.Add(_cboRole);
            cy += 34;

            // Role Help Text Badge
            _lblRoleHelp = new Label
            {
                Location = new Point(16, cy),
                Size = new Size(460, 36),
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Text = "Warehouse Operator: Can manage inventory stock, view products, and perform counts."
            };
            card.Controls.Add(_lblRoleHelp);
            cy += 42;

            // Password
            _lblPasswordHint = new Label
            {
                Text = _existingUser == null ? "Password * (Minimum 3 characters)" : "New Password (Leave blank to keep existing password)",
                Font = Theme.FontCaptionBold,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(_lblPasswordHint);
            cy += 22;

            _txtPassword = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(460, 30),
                Font = Theme.FontBody,
                UseSystemPasswordChar = true,
                PlaceholderText = _existingUser == null ? "Enter secure password..." : "Leave blank to preserve current password"
            };
            card.Controls.Add(_txtPassword);
            cy += 38;

            // Confirm Password
            var lblConfirmPassword = new Label
            {
                Text = "Confirm Password",
                Font = Theme.FontCaptionBold,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(lblConfirmPassword);
            cy += 22;

            _txtConfirmPassword = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(460, 30),
                Font = Theme.FontBody,
                UseSystemPasswordChar = true,
                PlaceholderText = "Re-enter password for verification..."
            };
            card.Controls.Add(_txtConfirmPassword);

            // Action Buttons
            int by = 530;
            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 38),
                Location = new Point(286, by)
            };
            Theme.ApplyFlatButton(_btnCancel, ColorTranslator.FromHtml("#E2E8F0"), Theme.TextDark);
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            panel.Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = _existingUser == null ? "Create User" : "Update Profile",
                Size = new Size(140, 38),
                Location = new Point(406, by)
            };
            Theme.ApplyFlatButton(_btnSave, Theme.Primary, Color.White);
            _btnSave.Click += OnSaveClick;
            panel.Controls.Add(_btnSave);

            Controls.Add(panel);
        }

        private void OnRoleSelectionChanged(object? sender, EventArgs e)
        {
            string selected = _cboRole.SelectedItem?.ToString() ?? "Staff";
            switch (selected)
            {
                case "Admin":
                    _lblRoleHelp.Text = "🛡️ Administrator: Full system access including Executive Dashboard, Monthly Reports, User Provisioning, System Settings & Audits.";
                    _lblRoleHelp.ForeColor = Theme.PrimaryHover;
                    break;
                case "Sales Staff":
                    _lblRoleHelp.Text = "🛒 Sales Representative: Access to Customer Sales Catalog, live stock viewing, and sales dispatch orders. (Dashboard & Reports hidden).";
                    _lblRoleHelp.ForeColor = Theme.SuccessDark;
                    break;
                case "Staff":
                default:
                    _lblRoleHelp.Text = "📦 Warehouse Staff: Stock In, Stock Out, inventory reconciliations and product catalog viewing. (Dashboard & Reports hidden).";
                    _lblRoleHelp.ForeColor = Theme.WarningDark;
                    break;
            }
        }

        private void PopulateData()
        {
            if (_existingUser != null)
            {
                _txtUsername.Text = _existingUser.Username;
                _txtFullName.Text = _existingUser.FullName;

                if (string.Equals(_existingUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                    _cboRole.SelectedItem = "Admin";
                else if (string.Equals(_existingUser.Role, "Sales Staff", StringComparison.OrdinalIgnoreCase) || string.Equals(_existingUser.Role, "Sales", StringComparison.OrdinalIgnoreCase))
                    _cboRole.SelectedItem = "Sales Staff";
                else
                    _cboRole.SelectedItem = "Staff";
            }
            else
            {
                _cboRole.SelectedItem = "Staff";
            }
            OnRoleSelectionChanged(null, EventArgs.Empty);
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            string username = _txtUsername.Text.Trim();
            string fullName = _txtFullName.Text.Trim();
            string role = _cboRole.SelectedItem?.ToString() ?? "Staff";
            string password = _txtPassword.Text;
            string confirmPassword = _txtConfirmPassword.Text;

            // Form validations
            if (_existingUser == null)
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("Please enter a username.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtUsername.Focus();
                    return;
                }

                if (username.Length < 3)
                {
                    MessageBox.Show("Username must be at least 3 characters long.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtUsername.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Please enter a password for the new user.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtPassword.Focus();
                    return;
                }

                if (password.Length < 3)
                {
                    MessageBox.Show("Password must be at least 3 characters long.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtPassword.Focus();
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Please enter the user's Full Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtFullName.Focus();
                return;
            }

            // If password was entered (on create or edit), verify confirmation
            if (!string.IsNullOrEmpty(password))
            {
                if (password != confirmPassword)
                {
                    MessageBox.Show("Password and Confirm Password do not match. Please re-enter.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtConfirmPassword.Focus();
                    return;
                }
            }

            try
            {
                if (_existingUser == null)
                {
                    // Create mode
                    var newUser = new User
                    {
                        Username = username,
                        FullName = fullName,
                        Role = role
                    };

                    bool created = _authService.CreateUser(newUser, password, out string? err);
                    if (!created)
                    {
                        MessageBox.Show(err ?? "Failed to create user account.", "Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    SavedUser = newUser;
                    MessageBox.Show($"User '{username}' ({role}) created successfully!", "User Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Edit mode
                    _existingUser.FullName = fullName;
                    _existingUser.Role = role;

                    string? passToUpdate = string.IsNullOrEmpty(password) ? null : password;
                    bool updated = _authService.UpdateUser(_existingUser, passToUpdate, out string? err);
                    if (!updated)
                    {
                        MessageBox.Show(err ?? "Failed to update user profile.", "Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    SavedUser = _existingUser;
                    MessageBox.Show($"User '{_existingUser.Username}' updated successfully!", "User Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
