using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Modern authentication dialog presenting enterprise login credentials,
    /// a 7-day Remember Me option, and instant demo credentials.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly IAuthService _authService;

        private TextBox _txtUsername = null!;
        private TextBox _txtPassword = null!;
        private CheckBox _chkRememberMe = null!;
        private Button _btnLogin = null!;
        private Label _lblError = null!;
        private Panel _errorPanel = null!;
        private Button _btnTogglePassword = null!;

        public User? AuthenticatedUser { get; private set; }

        public LoginForm(IAuthService? authService = null)
        {
            _authService = authService ?? new AuthService();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Sign In - Inventory Intelligence OS";
            Size = new Size(480, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.CanvasBg;
            Font = Theme.FontBody;

            var container = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(32, 28, 32, 28),
                BackColor = Theme.CanvasBg
            };

            // Main Card
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(28, 24, 28, 24)
            };
            card.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1.5f);
                e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            int y = 12;

            // Brand Header with official logo
            var logoImg = Theme.LoadLogoImage();
            if (logoImg != null)
            {
                var picLogo = new PictureBox
                {
                    Image = logoImg,
                    Location = new Point(24, y),
                    Size = new Size(card.Width - 48, 70),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                card.Controls.Add(picLogo);
                y += 82;
            }
            else
            {
                var lblLogoIcon = new Label
                {
                    Text = "📦",
                    Font = new Font("Segoe UI Emoji", 26f, FontStyle.Regular),
                    Location = new Point(24, y),
                    AutoSize = true
                };
                card.Controls.Add(lblLogoIcon);

                var lblLogoText = new Label
                {
                    Text = "INVENTSYS",
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = Theme.TextDark,
                    Location = new Point(80, y + 4),
                    AutoSize = true
                };
                card.Controls.Add(lblLogoText);

                var lblLogoSub = new Label
                {
                    Text = "Enterprise Intelligence & Stock Control",
                    Font = Theme.FontCaption,
                    ForeColor = Theme.TextMuted,
                    Location = new Point(82, y + 36),
                    AutoSize = true
                };
                card.Controls.Add(lblLogoSub);
                y += 75;
            }

            // Separator
            var separator = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(card.Width - 48, 1),
                BackColor = Theme.CardBorder,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(separator);
            y += 18;

            // Welcome Text
            var lblWelcome = new Label
            {
                Text = "Account Authentication",
                Font = Theme.FontHeadingMd,
                ForeColor = Theme.TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            card.Controls.Add(lblWelcome);
            y += 24;

            var lblInstruction = new Label
            {
                Text = "Please enter your credentials to access system telemetry.",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            };
            card.Controls.Add(lblInstruction);
            y += 28;

            // Error Panel (Hidden by default)
            _errorPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(card.Width - 48, 38),
                BackColor = Theme.DangerLight,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _errorPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.Danger, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, _errorPanel.Width - 1, _errorPanel.Height - 1);
            };

            _lblError = new Label
            {
                Text = "⚠️ Invalid credentials",
                Font = Theme.FontCaption,
                ForeColor = Theme.DangerDark,
                Location = new Point(12, 10),
                AutoSize = true
            };
            _errorPanel.Controls.Add(_lblError);
            card.Controls.Add(_errorPanel);
            y += 44;

            // Username Label & Input
            var lblUsername = new Label
            {
                Text = "USERNAME",
                Font = Theme.FontHeadingSm,
                ForeColor = Theme.TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            };
            card.Controls.Add(lblUsername);
            y += 22;

            _txtUsername = new TextBox
            {
                Location = new Point(24, y),
                Size = new Size(card.Width - 48, 32),
                Font = Theme.FontBody,
                PlaceholderText = "e.g. admin or staff",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_txtUsername);
            y += 40;

            // Password Label & Input
            var lblPassword = new Label
            {
                Text = "PASSWORD",
                Font = Theme.FontHeadingSm,
                ForeColor = Theme.TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            };
            card.Controls.Add(lblPassword);
            y += 22;

            _txtPassword = new TextBox
            {
                Location = new Point(24, y),
                Size = new Size(card.Width - 92, 32),
                Font = Theme.FontBody,
                UseSystemPasswordChar = true,
                PlaceholderText = "••••••••",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnTogglePassword = new Button
            {
                Text = "👁️",
                Size = new Size(38, 28),
                Location = new Point(card.Width - 62, y),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Emoji", 10f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnTogglePassword.FlatAppearance.BorderSize = 0;
            _btnTogglePassword.Click += (s, e) =>
            {
                _txtPassword.UseSystemPasswordChar = !_txtPassword.UseSystemPasswordChar;
                _btnTogglePassword.Text = _txtPassword.UseSystemPasswordChar ? "👁️" : "🔒";
            };

            card.Controls.Add(_txtPassword);
            card.Controls.Add(_btnTogglePassword);
            y += 40;

            // Remember Me 7-day Checkbox
            _chkRememberMe = new CheckBox
            {
                Text = "Remember me for 7 days",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextDark,
                Location = new Point(26, y),
                AutoSize = true,
                Checked = true,
                Cursor = Cursors.Hand
            };
            card.Controls.Add(_chkRememberMe);
            y += 34;

            // Submit Button
            _btnLogin = new Button
            {
                Text = "Sign In  ➔",
                Location = new Point(24, y),
                Size = new Size(card.Width - 48, 42),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Theme.ApplyFlatButton(_btnLogin, Theme.Primary, Color.White);
            _btnLogin.Click += OnLoginClick;
            card.Controls.Add(_btnLogin);
            AcceptButton = _btnLogin;
            y += 54;

            // Quick demo accounts panel
            var demoPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(card.Width - 48, 55),
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            demoPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f) { DashStyle = DashStyle.Dash };
                e.Graphics.DrawRectangle(p, 0, 0, demoPanel.Width - 1, demoPanel.Height - 1);
            };

            var lblDemo = new Label
            {
                Text = "Demo Accounts (Click to auto-fill):",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(10, 6),
                AutoSize = true
            };

            var btnDemoAdmin = new Button
            {
                Text = "Admin (admin / 123)",
                Size = new Size(160, 24),
                Location = new Point(10, 24),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontCaption,
                Cursor = Cursors.Hand
            };
            Theme.ApplyFlatButton(btnDemoAdmin, ColorTranslator.FromHtml("#EFF6FF"), Theme.PrimaryHover);
            btnDemoAdmin.Click += (s, e) =>
            {
                _txtUsername.Text = "admin";
                _txtPassword.Text = "123";
                _errorPanel.Visible = false;
                _txtPassword.Focus();
            };

            var btnDemoStaff = new Button
            {
                Text = "Staff (staff / 123)",
                Size = new Size(160, 24),
                Location = new Point(180, 24),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontCaption,
                Cursor = Cursors.Hand
            };
            Theme.ApplyFlatButton(btnDemoStaff, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnDemoStaff.Click += (s, e) =>
            {
                _txtUsername.Text = "staff";
                _txtPassword.Text = "123";
                _errorPanel.Visible = false;
                _txtPassword.Focus();
            };

            demoPanel.Controls.Add(lblDemo);
            demoPanel.Controls.Add(btnDemoAdmin);
            demoPanel.Controls.Add(btnDemoStaff);
            card.Controls.Add(demoPanel);

            container.Controls.Add(card);
            Controls.Add(container);
        }

        private void OnLoginClick(object? sender, EventArgs e)
        {
            string username = _txtUsername.Text.Trim();
            string password = _txtPassword.Text;
            bool rememberMe = _chkRememberMe.Checked;

            _btnLogin.Enabled = false;
            _btnLogin.Text = "Authenticating...";

            try
            {
                bool success = _authService.Authenticate(username, password, rememberMe, out var user, out var err);

                if (success && user != null)
                {
                    AuthenticatedUser = user;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    _lblError.Text = $"⚠️ {err ?? "Invalid login credentials."}";
                    _errorPanel.Visible = true;
                    _txtPassword.SelectAll();
                    _txtPassword.Focus();
                }
            }
            finally
            {
                _btnLogin.Enabled = true;
                _btnLogin.Text = "Sign In  ➔";
            }
        }
    }
}
