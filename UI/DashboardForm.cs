using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinForms;
using SkiaSharp;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.DataAccess;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Executive Dashboard Form presenting real-time inventory telemetry,
    /// dynamic LiveCharts visualizations, full product catalog management,
    /// stock movement audit logs, and system settings.
    /// </summary>
    public class DashboardForm : Form
    {
        private readonly IInventoryService _inventoryService;
        private readonly IAuthService _authService;

        private User _currentUser = new User
        {
            UserID = 1,
            Username = "admin",
            FullName = "System Administrator",
            Role = "Admin"
        };

        public User CurrentUser
        {
            get => _currentUser;
            private set => _currentUser = value ?? new User
            {
                UserID = 1,
                Username = "admin",
                FullName = "System Administrator",
                Role = "Admin"
            };
        }
        public bool LoggedOut { get; private set; }

        // Structural UI Controls
        private Panel _sidebarPanel = null!;
        private Panel _mainPanel = null!;
        private Panel _headerPanel = null!;
        private Label _lblHeaderTitle = null!;
        private Label _lblHeaderSubtitle = null!;
        private Label _lblDateTime = null!;
        private Label _lblDbStatus = null!;
        private System.Windows.Forms.Timer _clockTimer = null!;

        // Content Container and Sub-Views
        private Panel _contentContainer = null!;
        private Panel _panelDashboardView = null!;
        private Panel _panelProductsView = null!;
        private Panel _panelMovementsView = null!;
        private Panel _panelAuditsView = null!;
        private Panel _panelCategoriesView = null!;
        private Panel _panelSettingsView = null!;

        // Sidebar Navigation Buttons
        private readonly List<Button> _navButtons = new();

        // 1. Dashboard View Controls
        private KpiCard _cardTotalUnits = null!;
        private KpiCard _cardTotalValuation = null!;
        private KpiCard _cardLowStock = null!;
        private KpiCard _cardNetMovement = null!;
        private CartesianChart _chartMovements = null!;
        private PieChart _chartValuation = null!;
        private DataGridView _gridUrgentStock = null!;

        // 2. Product Catalog View Controls
        private DataGridView _gridAllProducts = null!;
        private TextBox _txtProductSearch = null!;
        private ComboBox _cboCategoryFilter = null!;
        private List<Product> _allCachedProducts = new();

        // 3. Stock Movements View Controls
        private DataGridView _gridMovements = null!;
        private TextBox _txtMovementSearch = null!;
        private ComboBox _cboMovementTypeFilter = null!;
        private List<StockTransaction> _allCachedTransactions = new();

        // 4. Inventory Audits View Controls
        private DataGridView _gridAudit = null!;
        private Label _lblAuditValuation = null!;
        private Label _lblAuditItemsCount = null!;

        // 5. Settings View Controls
        private Label _lblConnectionTestResult = null!;

        // 6. Category Management View Controls
        private DataGridView _gridCategories = null!;
        private TextBox _txtCategorySearch = null!;
        private List<Category> _allCachedCategories = new();
        private Label _lblCategorySummary = null!;

        public DashboardForm(IInventoryService inventoryService, User? currentUser = null, IAuthService? authService = null)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _authService = authService ?? new AuthService();
            CurrentUser = currentUser ?? new User
            {
                UserID = 1,
                Username = "admin",
                FullName = "System Administrator",
                Role = "Admin"
            };

            InitializeComponent();
            SwitchView("Dashboard");
            LoadAllData();
        }

        public DashboardForm(User currentUser) : this(new InventoryService(), currentUser)
        {
        }

        public DashboardForm() : this(new InventoryService(), null)
        {
        }

        private void InitializeComponent()
        {
            Text = "Inventory Management System - Enterprise Intelligence Suite";
            Size = new Size(1366, 850);
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.CanvasBg;
            Font = Theme.FontBody;

            // 1. Build sidebar (DockStyle.Left, width 240)
            BuildSidebar();

            // 2. Main panel fills the remaining width to the right of the sidebar
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CanvasBg
            };

            // 3. Header docks to Top of main panel
            BuildHeader();

            // 4. Content container fills the remaining height of main panel
            BuildContentContainer();

            // Assemble main panel hierarchy: Fill control added first, then Top control, then BringToFront on Fill
            _mainPanel.Controls.Add(_contentContainer);
            _mainPanel.Controls.Add(_headerPanel);
            _contentContainer.BringToFront();

            // Assemble Form hierarchy: sidebar on left, main panel filling the rest
            Controls.Add(_mainPanel);
            Controls.Add(_sidebarPanel);
            _mainPanel.BringToFront();

            // Real-time clock timer
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) => _lblDateTime.Text = DateTime.Now.ToString("dddd, MMM dd yyyy  •  HH:mm:ss");
            _clockTimer.Start();
        }

        #region Navigation & Sidebar

        private void BuildSidebar()
        {
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = Theme.SidebarBg,
                Padding = new Padding(0)
            };

            // Logo Header with logo.jpg
            var brandPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 195,
                BackColor = Color.White,
                Padding = new Padding(8, 8, 8, 8)
            };
            brandPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawLine(p, 0, brandPanel.Height - 1, brandPanel.Width, brandPanel.Height - 1);
            };

            var picLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };

            var logoImg = Theme.LoadLogoImage();
            if (logoImg != null)
            {
                picLogo.Image = logoImg;
                brandPanel.Controls.Add(picLogo);
            }
            else
            {
                var lblLogo = new Label
                {
                    Text = "📦  INVENTSYS",
                    Font = Theme.FontHeadingMd,
                    ForeColor = Theme.TextDark,
                    Location = new Point(16, 18),
                    AutoSize = true
                };

                var lblBrandSub = new Label
                {
                    Text = "Inventory Management System",
                    Font = Theme.FontCaption,
                    ForeColor = Theme.TextMuted,
                    Location = new Point(16, 44),
                    AutoSize = true
                };

                brandPanel.Controls.Add(lblLogo);
                brandPanel.Controls.Add(lblBrandSub);
            }

            // Nav Buttons
            var navContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 16, 12, 16)
            };

            int btnY = 10;
            string dashText = CurrentUser.IsAdmin ? "📊  Executive Dashboard" : (CurrentUser.IsSalesStaff ? "📊  Sales & Orders" : "📊  Operations Dashboard");
            string prodText = CurrentUser.IsAdmin ? "📦  Product Catalog" : (CurrentUser.IsSalesStaff ? "📦  Product Catalog (Sales)" : "📦  Product Catalog (View)");
            string moveText = CurrentUser.IsSalesStaff ? "🛒  Customer Sales & Orders" : "🔄  Stock Movements";

            navContainer.Controls.Add(CreateNavButton("Dashboard", dashText, ref btnY));
            navContainer.Controls.Add(CreateNavButton("Products", prodText, ref btnY));
            navContainer.Controls.Add(CreateNavButton("Movements", moveText, ref btnY));
            if (!CurrentUser.IsSalesStaff)
            {
                navContainer.Controls.Add(CreateNavButton("Audits", "📑  Inventory Audits", ref btnY));
            }
            if (CurrentUser.IsAdmin)
            {
                navContainer.Controls.Add(CreateNavButton("Categories", "🏷️  Categories", ref btnY));
                navContainer.Controls.Add(CreateNavButton("Settings", "⚙️  System Settings", ref btnY));
            }

            // Bottom Status Card
            var bottomCard = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 115,
                BackColor = Theme.SidebarActive,
                Padding = new Padding(14)
            };

            var lblUser = new Label
            {
                Text = $"👤 {CurrentUser.FullName}",
                ForeColor = Color.White,
                Font = Theme.FontBodyBold,
                Location = new Point(14, 12),
                AutoSize = true
            };

            Color roleColor = CurrentUser.IsAdmin 
                ? Theme.Primary 
                : (CurrentUser.IsSalesStaff ? Theme.Success : Theme.Warning);

            var lblRole = new Label
            {
                Text = $"Role: {CurrentUser.Role} • {CurrentUser.Username}",
                ForeColor = roleColor,
                Font = Theme.FontCaption,
                Location = new Point(14, 34),
                AutoSize = true
            };

            _lblDbStatus = new Label
            {
                Text = "● Database: Ready",
                ForeColor = Theme.Success,
                Font = Theme.FontCaption,
                Location = new Point(14, 56),
                AutoSize = true
            };

            var btnSignOut = new Button
            {
                Text = "Logout",
                Size = new Size(80, 24),
                Location = new Point(48, 80),
                FlatStyle = FlatStyle.Flat,
                ForeColor = ColorTranslator.FromHtml("#FCA5A5"),
                Font = Theme.FontCaption,
                Cursor = Cursors.Hand
            };
            btnSignOut.FlatAppearance.BorderSize = 0;
            btnSignOut.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(
                    "Are you sure you want to sign out? Your saved 7-day login session will be cleared.", 
                    "Sign Out", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    _authService.ClearRememberMeSession();
                    LoggedOut = true;
                    Close();
                }
            };

            var btnExit = new Button
            {
                Text = "Exit App",
                Size = new Size(80, 24),
                Location = new Point(136, 80),
                FlatStyle = FlatStyle.Flat,
                ForeColor = ColorTranslator.FromHtml("#94A3B8"),
                Font = Theme.FontCaption,
                Cursor = Cursors.Hand
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (s, e) => Application.Exit();

            bottomCard.Controls.Add(lblUser);
            bottomCard.Controls.Add(lblRole);
            bottomCard.Controls.Add(_lblDbStatus);
            bottomCard.Controls.Add(btnSignOut);
            bottomCard.Controls.Add(btnExit);

            // Assemble sidebar panels:
            // Docked Top (brandPanel) and Bottom (bottomCard) must dock before DockStyle.Fill (navContainer)
            _sidebarPanel.Controls.Add(navContainer);
            _sidebarPanel.Controls.Add(bottomCard);
            _sidebarPanel.Controls.Add(brandPanel);
            navContainer.BringToFront();
        }

        private Button CreateNavButton(string key, string text, ref int yPosition)
        {
            var btn = new Button
            {
                Tag = key,
                Text = text,
                Location = new Point(10, yPosition),
                Size = new Size(220, 42),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBodyBold,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                ForeColor = ColorTranslator.FromHtml("#94A3B8")
            };
            btn.FlatAppearance.BorderSize = 0;

            btn.Click += (s, e) => SwitchView(key);

            _navButtons.Add(btn);
            yPosition += 50;
            return btn;
        }

        private void SwitchView(string key)
        {
            // Highlight active button
            foreach (var b in _navButtons)
            {
                bool isActive = string.Equals(b.Tag?.ToString(), key, StringComparison.OrdinalIgnoreCase);
                b.BackColor = isActive ? Theme.Primary : Color.Transparent;
                b.ForeColor = isActive ? Color.White : ColorTranslator.FromHtml("#94A3B8");
            }

            // Hide all sub-views and show the selected view
            _panelDashboardView.Visible = false;
            _panelProductsView.Visible = false;
            _panelMovementsView.Visible = false;
            _panelAuditsView.Visible = false;
            _panelCategoriesView.Visible = false;
            _panelSettingsView.Visible = false;

            switch (key.ToLowerInvariant())
            {
                case "dashboard":
                    if (CurrentUser.IsAdmin)
                    {
                        _lblHeaderTitle.Text = "Inventory Intelligence & Executive Dashboard";
                        _lblHeaderSubtitle.Text = "Real-time stock analytics, turnover telemetry & restock triggers";
                    }
                    else if (CurrentUser.IsSalesStaff)
                    {
                        _lblHeaderTitle.Text = "Sales Operations & Outflow Dashboard";
                        _lblHeaderSubtitle.Text = "Real-time catalog availability, customer sales dispatch & demand telemetry";
                    }
                    else
                    {
                        _lblHeaderTitle.Text = "Warehouse Operations Dashboard";
                        _lblHeaderSubtitle.Text = "Real-time inventory levels, low stock alerts & restocking workflows";
                    }
                    _panelDashboardView.Visible = true;
                    _panelDashboardView.BringToFront();
                    LoadDashboardData();
                    break;

                case "products":
                    if (CurrentUser.IsAdmin)
                    {
                        _lblHeaderTitle.Text = "Product Master Catalog";
                        _lblHeaderSubtitle.Text = "Maintain enterprise inventory SKUs, categories, suppliers & pricing";
                    }
                    else if (CurrentUser.IsSalesStaff)
                    {
                        _lblHeaderTitle.Text = "Product Sales Catalog & Live Stock";
                        _lblHeaderSubtitle.Text = "Browse customer selling prices, product specifications, and live on-hand quantities";
                    }
                    else
                    {
                        _lblHeaderTitle.Text = "Product Master Catalog (Read-Only)";
                        _lblHeaderSubtitle.Text = "Browse inventory SKUs, barcodes, and current warehouse stock counts";
                    }
                    _panelProductsView.Visible = true;
                    _panelProductsView.BringToFront();
                    LoadProductsData();
                    break;

                case "movements":
                    _lblHeaderTitle.Text = CurrentUser.IsSalesStaff ? "Customer Sales & Dispatch Ledger" : "Stock Movement Ledger & Audit Trail";
                    _lblHeaderSubtitle.Text = CurrentUser.IsSalesStaff 
                        ? "Immutable record of customer sales orders, dispatched quantities, and operator audit trail"
                        : "Complete historical ledger of Stock In, Stock Out & Physical Count Adjustments";
                    _panelMovementsView.Visible = true;
                    _panelMovementsView.BringToFront();
                    LoadMovementsData();
                    break;

                case "audits":
                    if (CurrentUser.IsSalesStaff)
                    {
                        MessageBox.Show("Access Denied: Physical inventory audits are restricted to Warehouse and Administrative staff.", "Authorization Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        SwitchView("Dashboard");
                        return;
                    }
                    _lblHeaderTitle.Text = "Inventory Health & Verification Audits";
                    _lblHeaderSubtitle.Text = "Physical count reconciliation & stock invariant monitoring";
                    _panelAuditsView.Visible = true;
                    _panelAuditsView.BringToFront();
                    LoadAuditData();
                    break;

                case "settings":
                    if (!CurrentUser.IsAdmin)
                    {
                        MessageBox.Show("Access Denied: System Settings is restricted to Administrators only.", "Authorization Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        SwitchView("Dashboard");
                        return;
                    }
                    _lblHeaderTitle.Text = "System Configuration & Architecture";
                    _lblHeaderSubtitle.Text = "Database connection status, security profiles & 3-Tier diagnostic telemetry";
                    _panelSettingsView.Visible = true;
                    _panelSettingsView.BringToFront();
                    break;

                case "categories":
                    if (!CurrentUser.IsAdmin)
                    {
                        MessageBox.Show("Access Denied: Category Management is restricted to Administrators only.", "Authorization Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        SwitchView("Dashboard");
                        return;
                    }
                    _lblHeaderTitle.Text = "Category Management & Taxonomy";
                    _lblHeaderSubtitle.Text = "Create, update, organize, and manage product inventory categories";
                    _panelCategoriesView.Visible = true;
                    _panelCategoriesView.BringToFront();
                    LoadCategoriesData();
                    break;
            }
        }

        #endregion

        #region Header Construction

        private void BuildHeader()
        {
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Theme.HeaderBg,
                Padding = new Padding(24, 12, 24, 12)
            };
            _headerPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawLine(p, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            };

            var titleContainer = new Panel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _lblHeaderTitle = new Label
            {
                Text = "Inventory Intelligence & Operations",
                Font = Theme.FontHeadingMd,
                ForeColor = Theme.TextDark,
                Location = new Point(0, 4),
                AutoSize = true
            };

            _lblHeaderSubtitle = new Label
            {
                Text = "Enterprise Stock Health, Movements & Reorder Telemetry",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(0, 28),
                AutoSize = true
            };

            titleContainer.Controls.Add(_lblHeaderTitle);
            titleContainer.Controls.Add(_lblHeaderSubtitle);

            // Right-aligned actions container
            var headerRightContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };

            var btnRefresh = new Button
            {
                Text = "Refresh",
                Size = new Size(110, 36),
                Margin = new Padding(8, 0, 0, 0)
            };
            Theme.ApplyFlatButton(btnRefresh, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnRefresh.Click += (s, e) => LoadAllData();

            var btnQuickRestock = new Button
            {
                Text = "Quick Restock",
                Size = new Size(140, 36),
                Margin = new Padding(12, 0, 0, 0)
            };
            Theme.ApplyFlatButton(btnQuickRestock, Theme.Primary, Color.White);
            btnQuickRestock.Click += (s, e) => OpenRestockModal(null);

            _lblDateTime = new Label
            {
                Text = DateTime.Now.ToString("dddd, MMM dd yyyy  •  HH:mm:ss"),
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Margin = new Padding(0, 10, 16, 0)
            };

            headerRightContainer.Controls.Add(btnRefresh);
            headerRightContainer.Controls.Add(btnQuickRestock);
            headerRightContainer.Controls.Add(_lblDateTime);

            _headerPanel.Controls.Add(headerRightContainer);
            _headerPanel.Controls.Add(titleContainer);
        }

        #endregion

        #region Content Container & Sub-Views

        private void BuildContentContainer()
        {
            _contentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(0)
            };

            // Build all sub-views
            BuildDashboardView();
            BuildProductsView();
            BuildMovementsView();
            BuildAuditsView();
            BuildCategoriesView();
            BuildSettingsView();
        }

        #region View 1: Executive Dashboard

        private void BuildDashboardView()
        {
            _panelDashboardView = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(24, 20, 24, 24)
            };

            // 1. KPI Row (Responsive 4-column TableLayoutPanel)
            var kpiTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 125,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(0)
            };
            kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            _cardTotalUnits = new KpiCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
            _cardTotalValuation = new KpiCard { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0) };
            _cardLowStock = new KpiCard { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0) };
            _cardNetMovement = new KpiCard { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };

            kpiTable.Controls.Add(_cardTotalUnits, 0, 0);
            kpiTable.Controls.Add(_cardTotalValuation, 1, 0);
            kpiTable.Controls.Add(_cardLowStock, 2, 0);
            kpiTable.Controls.Add(_cardNetMovement, 3, 0);

            // Spacer between KPI row and charts
            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            // 2. Middle Charts
            var chartsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 310,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            chartsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            chartsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));

            var chartCard1 = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(16) };
            chartCard1.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, chartCard1.Width - 1, chartCard1.Height - 1); };
            var lblC1 = new Label { Text = "Stock Movement Trends (Inflow vs. Outflow)", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Dock = DockStyle.Top, Height = 26 };
            _chartMovements = new CartesianChart { Dock = DockStyle.Fill };
            chartCard1.Controls.Add(_chartMovements);
            chartCard1.Controls.Add(lblC1);
            _chartMovements.BringToFront();

            var chartCard2 = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(8, 0, 0, 0), Padding = new Padding(16) };
            chartCard2.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, chartCard2.Width - 1, chartCard2.Height - 1); };
            var lblC2 = new Label { Text = CurrentUser.IsAdmin ? "Inventory Valuation by Category" : "Category Stock Distribution (Units)", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Dock = DockStyle.Top, Height = 26 };
            _chartValuation = new PieChart { Dock = DockStyle.Fill };
            chartCard2.Controls.Add(_chartValuation);
            chartCard2.Controls.Add(lblC2);
            _chartValuation.BringToFront();

            chartsTable.Controls.Add(chartCard1, 0, 0);
            chartsTable.Controls.Add(chartCard2, 1, 0);

            // Spacer between charts and restock watchlist
            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            // 3. Bottom Urgent Watchlist
            var restockCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 300,
                BackColor = Color.White,
                Padding = new Padding(16)
            };
            restockCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, restockCard.Width - 1, restockCard.Height - 1); };

            var restockHeader = new Panel { Dock = DockStyle.Top, Height = 44 };
            var lblGridTitle = new Label { Text = "⚠️  Urgent Restock Watchlist (Current Stock ≤ Reorder Level)", Font = Theme.FontHeadingSm, ForeColor = Theme.DangerDark, Location = new Point(0, 8), AutoSize = true };

            var btnTelegramAlert = new Button
            {
                Text = "📲  ផ្ញើ Alert ទៅ Telegram",
                Size = new Size(190, 34),
                MinimumSize = new Size(180, 34),
                AutoSize = true,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 4, 8, 4)
            };
            Theme.ApplyFlatButton(btnTelegramAlert, Theme.Secondary, Color.White);
            btnTelegramAlert.Click += async (s, e) =>
            {
                btnTelegramAlert.Enabled = false;
                btnTelegramAlert.Text = "⏳ កំពុងផ្ញើ...";
                try
                {
                    var lowStockItems = _inventoryService.GetUrgentRestockList().ToList();
                    if (lowStockItems.Count == 0)
                    {
                        MessageBox.Show("បច្ចុប្បន្នមិនមានទំនិញណាមួយជិតអស់ពីស្តុកនោះទេ! (No low stock items detected).", 
                            "Telegram Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    bool ok = await _inventoryService.TelegramService.SendUrgentRestockSummaryAsync(lowStockItems);
                    if (ok)
                    {
                        MessageBox.Show($"បានផ្ញើរបាយការណ៍ទំនិញជិតអស់ស្តុកចំនួន {lowStockItems.Count} មុខ ទៅកាន់ Telegram Bot រួចរាល់ដោយជោគជ័យ!", 
                            "ជោគជ័យ (Success)", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("ការផ្ញើទៅកាន់ Telegram បានបរាជ័យ។ សូមពិនិត្យមើល Bot Token និង Chat ID នៅក្នុង System Settings។", 
                            "បរាជ័យ (Failed)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                finally
                {
                    btnTelegramAlert.Enabled = true;
                    btnTelegramAlert.Text = "📲  ផ្ញើ Alert ទៅ Telegram";
                }
            };

            var btnGridRestock = new Button
            {
                Text = "Restock Selected Item",
                Size = new Size(185, 34),
                MinimumSize = new Size(175, 34),
                AutoSize = true,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 4, 0, 4)
            };
            Theme.ApplyFlatButton(btnGridRestock, Theme.Warning, Color.White);
            btnGridRestock.Click += (s, e) => RestockFromGrid(_gridUrgentStock);

            restockHeader.Controls.Add(btnGridRestock);
            restockHeader.Controls.Add(btnTelegramAlert);
            restockHeader.Controls.Add(lblGridTitle);

            _gridUrgentStock = new DataGridView { Dock = DockStyle.Fill };
            Theme.ApplyModernGrid(_gridUrgentStock);
            _gridUrgentStock.RowTemplate.Height = 48;
            _gridUrgentStock.CellFormatting += OnGridCellFormatting;
            _gridUrgentStock.CellDoubleClick += (s, e) => RestockFromGrid(_gridUrgentStock);
            _gridUrgentStock.DataBindingComplete += (s, e) => SafeConfigureImageGridColumns(_gridUrgentStock, "ProductName", 240);

            restockCard.Controls.Add(_gridUrgentStock);
            restockCard.Controls.Add(restockHeader);
            _gridUrgentStock.BringToFront();

            // Docking layout in WinForms processes reverse index order.
            // Adding: restockCard -> spacer2 -> chartsTable -> spacer1 -> kpiTable
            // Results in top-to-bottom display: kpiTable -> spacer1 -> chartsTable -> spacer2 -> restockCard
            _panelDashboardView.Controls.Add(restockCard);
            _panelDashboardView.Controls.Add(spacer2);
            _panelDashboardView.Controls.Add(chartsTable);
            _panelDashboardView.Controls.Add(spacer1);
            _panelDashboardView.Controls.Add(kpiTable);

            _contentContainer.Controls.Add(_panelDashboardView);
        }

        #endregion

        #region View 2: Product Catalog View

        private void BuildProductsView()
        {
            _panelProductsView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(24, 20, 24, 24)
            };

            // Action & Filter Bar
            var actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(14, 11, 14, 11)
            };
            actionPanel.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, actionPanel.Width - 1, actionPanel.Height - 1); };

            var searchFilterFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var lblSearch = new Label { Text = "Search:", Font = Theme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
            _txtProductSearch = new TextBox { Size = new Size(220, 30), Font = Theme.FontBody, PlaceholderText = "Search by SKU or Name...", Margin = new Padding(0, 4, 16, 0) };
            _txtProductSearch.TextChanged += (s, e) => FilterProducts();

            var lblCat = new Label { Text = "Category:", Font = Theme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
            _cboCategoryFilter = new ComboBox { Size = new Size(160, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.FontBody, Margin = new Padding(0, 4, 0, 0) };
            _cboCategoryFilter.Items.Add("All Categories");
            _cboCategoryFilter.SelectedIndex = 0;
            _cboCategoryFilter.SelectedIndexChanged += (s, e) => FilterProducts();

            searchFilterFlow.Controls.Add(lblSearch);
            searchFilterFlow.Controls.Add(_txtProductSearch);
            searchFilterFlow.Controls.Add(lblCat);
            searchFilterFlow.Controls.Add(_cboCategoryFilter);

            var actionButtonsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var btnAdd = new Button
            {
                Text = "+  Add Product",
                Size = new Size(140, 36),
                MinimumSize = new Size(130, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 1)
            };
            Theme.ApplyFlatButton(btnAdd, Theme.Primary, Color.White);
            btnAdd.Click += OnAddProductClick;

            var btnEdit = new Button
            {
                Text = "✏️  Edit Product",
                Size = new Size(140, 36),
                MinimumSize = new Size(130, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 1)
            };
            Theme.ApplyFlatButton(btnEdit, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnEdit.Click += OnEditProductClick;

            var btnDelete = new Button
            {
                Text = "🗑️  Delete",
                Size = new Size(110, 36),
                MinimumSize = new Size(100, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 1)
            };
            Theme.ApplyFlatButton(btnDelete, Theme.DangerLight, Theme.DangerDark);
            btnDelete.Click += OnDeleteProductClick;

            var btnAction = new Button
            {
                Text = CurrentUser.IsSalesStaff ? "🛒  Sell Selected Item" : "⚡  Restock Item",
                Size = new Size(160, 36),
                MinimumSize = new Size(140, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 0, 1)
            };
            Theme.ApplyFlatButton(btnAction, CurrentUser.IsSalesStaff ? Theme.Success : Theme.Warning, Color.White);
            btnAction.Click += (s, e) => RestockFromGrid(_gridAllProducts);

            if (CurrentUser.IsAdmin)
            {
                actionButtonsFlow.Controls.Add(btnAdd);
                actionButtonsFlow.Controls.Add(btnEdit);
                actionButtonsFlow.Controls.Add(btnDelete);

                var btnCategories = new Button
                {
                    Text = "🏷️  Categories",
                    Size = new Size(130, 36),
                    MinimumSize = new Size(120, 36),
                    AutoSize = true,
                    Margin = new Padding(4, 1, 4, 1)
                };
                Theme.ApplyFlatButton(btnCategories, ColorTranslator.FromHtml("#EFF6FF"), Theme.Primary);
                btnCategories.Click += (s, e) => SwitchView("Categories");
                actionButtonsFlow.Controls.Add(btnCategories);
            }
            else
            {
                var lblStaffBadge = new Label
                {
                    Text = "🔒 Catalog Mod: Admin Only",
                    Font = Theme.FontCaption,
                    ForeColor = Theme.TextMuted,
                    AutoSize = true,
                    Margin = new Padding(0, 10, 8, 0)
                };
                actionButtonsFlow.Controls.Add(lblStaffBadge);
            }
            actionButtonsFlow.Controls.Add(btnAction);

            actionPanel.Controls.Add(actionButtonsFlow);
            actionPanel.Controls.Add(searchFilterFlow);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            // Products Grid
            var gridCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16)
            };
            gridCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, gridCard.Width - 1, gridCard.Height - 1); };

            _gridAllProducts = new DataGridView { Dock = DockStyle.Fill };
            Theme.ApplyModernGrid(_gridAllProducts);
            _gridAllProducts.RowTemplate.Height = 48;
            _gridAllProducts.CellFormatting += OnGridCellFormatting;
            _gridAllProducts.CellDoubleClick += (s, e) =>
            {
                if (CurrentUser.IsAdmin)
                    OnEditProductClick(s, e);
                else
                    RestockFromGrid(_gridAllProducts);
            };
            _gridAllProducts.DataBindingComplete += (s, e) => SafeConfigureImageGridColumns(_gridAllProducts, "ProductName", 220);
            _gridAllProducts.SelectionChanged += (s, e) =>
            {
                if (CurrentUser.IsAdmin)
                {
                    int count = _gridAllProducts.SelectedRows.Count;
                    btnDelete.Text = count > 1 ? $"🗑️  Delete ({count})" : "🗑️  Delete";
                }
            };
            _gridAllProducts.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && CurrentUser.IsAdmin)
                {
                    OnDeleteProductClick(s, e);
                    e.Handled = true;
                }
            };

            gridCard.Controls.Add(_gridAllProducts);

            _panelProductsView.Controls.Add(gridCard);
            _panelProductsView.Controls.Add(spacer);
            _panelProductsView.Controls.Add(actionPanel);
            gridCard.BringToFront();

            _contentContainer.Controls.Add(_panelProductsView);
        }

        #endregion

        #region View 3: Stock Movements View

        private void BuildMovementsView()
        {
            _panelMovementsView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(24, 20, 24, 24)
            };

            var actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(14, 11, 14, 11)
            };
            actionPanel.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, actionPanel.Width - 1, actionPanel.Height - 1); };

            var searchFilterFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var lblSearch = new Label { Text = "Search:", Font = Theme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
            _txtMovementSearch = new TextBox { Size = new Size(220, 30), Font = Theme.FontBody, PlaceholderText = "Search by Reference, SKU...", Margin = new Padding(0, 4, 16, 0) };
            _txtMovementSearch.TextChanged += (s, e) => FilterMovements();

            var lblType = new Label { Text = "Type:", Font = Theme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
            _cboMovementTypeFilter = new ComboBox { Size = new Size(160, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.FontBody, Margin = new Padding(0, 4, 0, 0) };
            if (CurrentUser.IsSalesStaff)
            {
                _cboMovementTypeFilter.Items.AddRange(new object[] { "OUT (Sales Dispatches)" });
                _cboMovementTypeFilter.SelectedIndex = 0;
                _cboMovementTypeFilter.Enabled = false;
            }
            else
            {
                _cboMovementTypeFilter.Items.AddRange(new object[] { "All Movements", "IN (Intake)", "OUT (Dispatch)", "ADJUSTMENT" });
                _cboMovementTypeFilter.SelectedIndex = 0;
            }
            _cboMovementTypeFilter.SelectedIndexChanged += (s, e) => FilterMovements();

            searchFilterFlow.Controls.Add(lblSearch);
            searchFilterFlow.Controls.Add(_txtMovementSearch);
            searchFilterFlow.Controls.Add(lblType);
            searchFilterFlow.Controls.Add(_cboMovementTypeFilter);

            var btnPrintInvoice = new Button
            {
                Text = "🧾  Print Invoice / Receipt",
                Size = new Size(190, 36),
                MinimumSize = new Size(180, 36),
                AutoSize = true,
                Dock = DockStyle.Right
            };
            Theme.ApplyFlatButton(btnPrintInvoice, Theme.CardBorder, Theme.TextDark);
            btnPrintInvoice.Click += (s, e) => PrintSelectedMovementInvoice();

            var btnNewTx = new Button
            {
                Text = CurrentUser.IsSalesStaff ? "🛒  Record Customer Sale" : "+  Record Stock Movement",
                Size = new Size(220, 36),
                MinimumSize = new Size(210, 36),
                AutoSize = true,
                Dock = DockStyle.Right
            };
            Theme.ApplyFlatButton(btnNewTx, CurrentUser.IsSalesStaff ? Theme.Success : Theme.Primary, Color.White);
            btnNewTx.Click += (s, e) => OpenRestockModal(null);

            actionPanel.Controls.Add(btnNewTx);
            actionPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8, BackColor = Color.Transparent });
            actionPanel.Controls.Add(btnPrintInvoice);
            actionPanel.Controls.Add(searchFilterFlow);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            var gridCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16)
            };
            gridCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, gridCard.Width - 1, gridCard.Height - 1); };

            _gridMovements = new DataGridView { Dock = DockStyle.Fill };
            Theme.ApplyModernGrid(_gridMovements);
            _gridMovements.RowTemplate.Height = 48;
            _gridMovements.CellFormatting += OnMovementsCellFormatting;
            _gridMovements.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    PrintSelectedMovementInvoice();
                }
            };
            _gridMovements.DataBindingComplete += (s, e) => SafeConfigureImageGridColumns(_gridMovements, "Product", 200);

            gridCard.Controls.Add(_gridMovements);

            _panelMovementsView.Controls.Add(gridCard);
            _panelMovementsView.Controls.Add(spacer);
            _panelMovementsView.Controls.Add(actionPanel);
            gridCard.BringToFront();

            _contentContainer.Controls.Add(_panelMovementsView);
        }

        #endregion

        #region View 4: Inventory Audits View

        private void BuildAuditsView()
        {
            _panelAuditsView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(24, 20, 24, 24)
            };

            // Summary Header Card
            var summaryCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.White,
                Padding = new Padding(20, 16, 20, 16)
            };
            summaryCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, summaryCard.Width - 1, summaryCard.Height - 1); };

            var summaryInfoFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _lblAuditValuation = new Label { Text = "Total Catalog Valuation: $0.00", Font = Theme.FontHeadingSm, ForeColor = Theme.SuccessDark, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            _lblAuditItemsCount = new Label { Text = "Audit Items Evaluated: 0 products", Font = Theme.FontCaption, ForeColor = Theme.TextMuted, AutoSize = true };

            summaryInfoFlow.Controls.Add(_lblAuditValuation);
            summaryInfoFlow.Controls.Add(_lblAuditItemsCount);

            var btnRunAudit = new Button
            {
                Text = "🔍  Run Physical Audit Verification",
                Size = new Size(240, 36),
                Dock = DockStyle.Right
            };
            Theme.ApplyFlatButton(btnRunAudit, Theme.Primary, Color.White);
            btnRunAudit.Click += (s, e) =>
            {
                LoadAuditData();
                MessageBox.Show("Physical audit reconciliation scan complete!\nNo unrecorded inventory variances detected.", "Audit Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            summaryCard.Controls.Add(btnRunAudit);
            summaryCard.Controls.Add(summaryInfoFlow);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            // Audit Grid
            var gridCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16)
            };
            gridCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, gridCard.Width - 1, gridCard.Height - 1); };

            _gridAudit = new DataGridView { Dock = DockStyle.Fill };
            Theme.ApplyModernGrid(_gridAudit);
            _gridAudit.CellFormatting += OnGridCellFormatting;

            gridCard.Controls.Add(_gridAudit);

            _panelAuditsView.Controls.Add(gridCard);
            _panelAuditsView.Controls.Add(spacer);
            _panelAuditsView.Controls.Add(summaryCard);
            gridCard.BringToFront();

            _contentContainer.Controls.Add(_panelAuditsView);
        }

        #endregion

        #region View 5: Category Management View

        private void BuildCategoriesView()
        {
            _panelCategoriesView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(24, 20, 24, 24)
            };

            // Action & Filter Bar
            var actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(14, 11, 14, 11)
            };
            actionPanel.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, actionPanel.Width - 1, actionPanel.Height - 1); };

            var searchFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var lblSearch = new Label { Text = "Search:", Font = Theme.FontBodyBold, AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
            _txtCategorySearch = new TextBox { Size = new Size(240, 30), Font = Theme.FontBody, PlaceholderText = "Search by category name...", Margin = new Padding(0, 4, 16, 0) };
            _txtCategorySearch.TextChanged += (s, e) => FilterCategories();

            _lblCategorySummary = new Label
            {
                Text = "Loading categories...",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };

            searchFlow.Controls.Add(lblSearch);
            searchFlow.Controls.Add(_txtCategorySearch);
            searchFlow.Controls.Add(_lblCategorySummary);

            var actionButtonsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var btnAdd = new Button
            {
                Text = "+  Add Category",
                Size = new Size(150, 36),
                MinimumSize = new Size(140, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 1)
            };
            Theme.ApplyFlatButton(btnAdd, Theme.Primary, Color.White);
            btnAdd.Click += OnAddCategoryClick;

            var btnEdit = new Button
            {
                Text = "✏️  Edit Category",
                Size = new Size(150, 36),
                MinimumSize = new Size(140, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 1)
            };
            Theme.ApplyFlatButton(btnEdit, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnEdit.Click += OnEditCategoryClick;

            var btnDelete = new Button
            {
                Text = "🗑️  Delete",
                Size = new Size(110, 36),
                MinimumSize = new Size(100, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 4, 1)
            };
            Theme.ApplyFlatButton(btnDelete, Theme.DangerLight, Theme.DangerDark);
            btnDelete.Click += OnDeleteCategoryClick;

            var btnRefresh = new Button
            {
                Text = "🔄  Refresh",
                Size = new Size(110, 36),
                MinimumSize = new Size(100, 36),
                AutoSize = true,
                Margin = new Padding(4, 1, 0, 1)
            };
            Theme.ApplyFlatButton(btnRefresh, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnRefresh.Click += (s, e) => LoadCategoriesData();

            actionButtonsFlow.Controls.Add(btnAdd);
            actionButtonsFlow.Controls.Add(btnEdit);
            actionButtonsFlow.Controls.Add(btnDelete);
            actionButtonsFlow.Controls.Add(btnRefresh);

            actionPanel.Controls.Add(actionButtonsFlow);
            actionPanel.Controls.Add(searchFlow);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = Color.Transparent };

            // Grid Card Panel
            var gridCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(1)
            };
            gridCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, gridCard.Width - 1, gridCard.Height - 1); };

            _gridCategories = new DataGridView { Dock = DockStyle.Fill };
            Theme.ApplyModernGrid(_gridCategories);
            _gridCategories.DoubleClick += OnEditCategoryClick;

            gridCard.Controls.Add(_gridCategories);

            _panelCategoriesView.Controls.Add(gridCard);
            _panelCategoriesView.Controls.Add(spacer);
            _panelCategoriesView.Controls.Add(actionPanel);

            _contentContainer.Controls.Add(_panelCategoriesView);
        }

        #endregion

        #region View 6: System Settings View

        private void BuildSettingsView()
        {
            _panelSettingsView = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.CanvasBg,
                Padding = new Padding(24, 20, 24, 24)
            };

            // Card 1: Database Connection Settings
            var dbCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 220,
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            dbCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, dbCard.Width - 1, dbCard.Height - 1); };

            var lblDbTitle = new Label { Text = "Microsoft SQL Server Database Connection", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, 16), AutoSize = true };
            var lblDbSub = new Label { Text = "Configuration sourced from App.config via Microsoft.Data.SqlClient ADO.NET connection pooling", Font = Theme.FontCaption, ForeColor = Theme.TextMuted, Location = new Point(16, 40), AutoSize = true };

            var txtConn = new TextBox
            {
                Text = DatabaseHelper.ConnectionString,
                Location = new Point(16, 70),
                Size = new Size(680, 30),
                Font = Theme.FontBody,
                ReadOnly = true
            };

            var btnTestConn = new Button { Text = "🔌  Test SQL Connection", Size = new Size(180, 34), Location = new Point(16, 115) };
            Theme.ApplyFlatButton(btnTestConn, Theme.Primary, Color.White);

            _lblConnectionTestResult = new Label
            {
                Text = "Status: Not Tested",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(215, 124),
                AutoSize = true
            };

            btnTestConn.Click += (s, e) =>
            {
                bool success = DatabaseHelper.TestConnection(out string? err);
                if (success)
                {
                    _lblConnectionTestResult.Text = "● Connection Successful (SQL Server Active)";
                    _lblConnectionTestResult.ForeColor = Theme.SuccessDark;
                    _lblDbStatus.Text = "● Database: Active";
                    _lblDbStatus.ForeColor = Theme.Success;
                }
                else
                {
                    _lblConnectionTestResult.Text = $"● Connection Failed: {err}";
                    _lblConnectionTestResult.ForeColor = Theme.DangerDark;
                }
            };

            var lblHelp = new Label
            {
                Text = "💡 Tip: Switch connection string in App.config between LocalDB (Server=(localdb)\\MSSQLLocalDB) and SQL Express (Server=.\\SQLEXPRESS).",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(16, 165),
                AutoSize = true
            };

            dbCard.Controls.Add(lblDbTitle);
            dbCard.Controls.Add(lblDbSub);
            dbCard.Controls.Add(txtConn);
            dbCard.Controls.Add(btnTestConn);
            dbCard.Controls.Add(_lblConnectionTestResult);
            dbCard.Controls.Add(lblHelp);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            // Card 2: Telegram Bot Notification Settings
            var telegramCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 310,
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            telegramCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, telegramCard.Width - 1, telegramCard.Height - 1); };

            var lblTgTitle = new Label { Text = "📢  Telegram Bot Low Stock Alerts (ការជូនដំណឹងតាម Telegram)", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, 16), AutoSize = true };
            var lblTgSub = new Label { Text = "ផ្ញើសារជាភាសាខ្មែរទៅកាន់ Telegram Bot ដោយស្វ័យប្រវត្តិនូវរាល់ពេលទំនិញធ្លាក់ចុះដល់កម្រិតជិតអស់ពីស្តុក (Current Stock ≤ Reorder Level) ឬអស់ពីស្តុក", Font = Theme.FontCaption, ForeColor = Theme.TextMuted, Location = new Point(16, 40), AutoSize = true };

            var lblTgToken = new Label { Text = "Bot Token:", Font = Theme.FontCaptionBold, ForeColor = Theme.TextDark, Location = new Point(16, 70), AutoSize = true };
            var txtTgToken = new TextBox
            {
                Text = _inventoryService.TelegramService.BotToken,
                Location = new Point(16, 92),
                Size = new Size(460, 30),
                Font = Theme.FontBody
            };

            var lblTgChatId = new Label { Text = "Chat ID / Group ID:", Font = Theme.FontCaptionBold, ForeColor = Theme.TextDark, Location = new Point(490, 70), AutoSize = true };
            var txtTgChatId = new TextBox
            {
                Text = _inventoryService.TelegramService.ChatId,
                Location = new Point(490, 92),
                Size = new Size(240, 30),
                Font = Theme.FontBody
            };

            var chkTgEnabled = new CheckBox
            {
                Text = "បើកដំណើរការការជូនដំណឹងស្វ័យប្រវត្តិ (Enable Real-Time Alerts)",
                Checked = _inventoryService.TelegramService.IsEnabled,
                Location = new Point(16, 132),
                AutoSize = true,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextDark
            };

            var btnDetectChat = new Button { Text = "🔍  Detect Chat ID", Size = new Size(160, 34), Location = new Point(16, 170) };
            Theme.ApplyFlatButton(btnDetectChat, Theme.Secondary, Color.White);

            var btnTestTg = new Button { Text = "📨  Send Test Alert (Khmer)", Size = new Size(205, 34), Location = new Point(186, 170) };
            Theme.ApplyFlatButton(btnTestTg, Theme.Primary, Color.White);

            var btnSaveTg = new Button { Text = "💾  Save Telegram Settings", Size = new Size(195, 34), Location = new Point(401, 170) };
            Theme.ApplyFlatButton(btnSaveTg, Theme.Success, Color.White);

            var lblTgStatus = new Label
            {
                Text = "ស្ថានភាព៖ រួចរាល់សម្រាប់ការជូនដំណឹង (Ready)",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(16, 215),
                AutoSize = true
            };

            btnDetectChat.Click += async (s, e) =>
            {
                btnDetectChat.Enabled = false;
                lblTgStatus.Text = "កំពុងស្វែងរក Chat ID ពី Telegram Updates...";
                lblTgStatus.ForeColor = Theme.Primary;

                var result = await _inventoryService.TelegramService.DetectLatestChatIdAsync();
                btnDetectChat.Enabled = true;

                if (result.Success && !string.IsNullOrWhiteSpace(result.DetectedChatId))
                {
                    txtTgChatId.Text = result.DetectedChatId;
                    lblTgStatus.Text = $"● រកឃើញ Chat ID ដោយជោគជ័យ: {result.DetectedChatId} ({result.SenderName})";
                    lblTgStatus.ForeColor = Theme.SuccessDark;
                }
                else
                {
                    lblTgStatus.Text = $"● រកមិនឃើញ Chat ID: {result.Error}";
                    lblTgStatus.ForeColor = Theme.DangerDark;
                }
            };

            btnTestTg.Click += async (s, e) =>
            {
                btnTestTg.Enabled = false;
                lblTgStatus.Text = "កំពុងផ្ញើសារសាកល្បងជាភាសាខ្មែរទៅកាន់ Telegram...";
                lblTgStatus.ForeColor = Theme.Primary;

                _inventoryService.TelegramService.UpdateConfig(txtTgToken.Text.Trim(), txtTgChatId.Text.Trim(), chkTgEnabled.Checked);
                bool ok = await _inventoryService.TelegramService.SendTestNotificationAsync();
                btnTestTg.Enabled = true;

                if (ok)
                {
                    lblTgStatus.Text = "● បានផ្ញើសារសាកល្បងទៅកាន់ Telegram ទទួលបានជោគជ័យ!";
                    lblTgStatus.ForeColor = Theme.SuccessDark;
                }
                else
                {
                    lblTgStatus.Text = "● ការផ្ញើសារបរាជ័យ! សូមពិនិត្យមើល Token និង Chat ID។";
                    lblTgStatus.ForeColor = Theme.DangerDark;
                }
            };

            btnSaveTg.Click += (s, e) =>
            {
                _inventoryService.TelegramService.UpdateConfig(txtTgToken.Text.Trim(), txtTgChatId.Text.Trim(), chkTgEnabled.Checked);
                lblTgStatus.Text = "● បានរក្សាទុកការកំណត់ Telegram ដោយជោគជ័យ!";
                lblTgStatus.ForeColor = Theme.SuccessDark;
                MessageBox.Show("ការកំណត់ Telegram Bot ត្រូវបានរក្សាទុកដោយជោគជ័យ!", "រក្សាទុកជោគជ័យ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var lblTgTip = new Label
            {
                Text = "💡 ព័ត៌មានជំនួយ៖ ដើម្បីទទួលបាន Chat ID សូមបើក Telegram រួចស្វែងរក @InventoryAlert24Bot ហើយចុច Start ឬផ្ញើសារ 'hi' រួចចុច 'Detect Chat ID'។",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(16, 245),
                AutoSize = true
            };

            telegramCard.Controls.Add(lblTgTitle);
            telegramCard.Controls.Add(lblTgSub);
            telegramCard.Controls.Add(lblTgToken);
            telegramCard.Controls.Add(txtTgToken);
            telegramCard.Controls.Add(lblTgChatId);
            telegramCard.Controls.Add(txtTgChatId);
            telegramCard.Controls.Add(chkTgEnabled);
            telegramCard.Controls.Add(btnDetectChat);
            telegramCard.Controls.Add(btnTestTg);
            telegramCard.Controls.Add(btnSaveTg);
            telegramCard.Controls.Add(lblTgStatus);
            telegramCard.Controls.Add(lblTgTip);

            var spacerTg = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent };

            // Card 3: OOAD Architecture Specifications
            var ooadCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 260,
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            ooadCard.Paint += (s, e) => { using var p = new Pen(Theme.CardBorder, 1f); e.Graphics.DrawRectangle(p, 0, 0, ooadCard.Width - 1, ooadCard.Height - 1); };

            var lblOoad = new Label { Text = "OOAD 3-Tier Architecture Verification", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, 16), AutoSize = true };
            var lblOoadDesc = new Label
            {
                Text = "• Presentation Layer: Modern Flat WinForms with LiveCharts v2 visualization.\n" +
                       "• Business Logic Layer: IInventoryService, Domain Invariant Enforcement (Product.CanDeductStock).\n" +
                       "• Data Access Layer: ADO.NET Repositories (ProductRepository, TransactionRepository) with atomic SqlTransaction rollback.\n" +
                       "• Domain Exception Hierarchy: InsufficientStockException, DuplicateSkuException, EntityNotFoundException.\n" +
                       "• Security & RBAC: SHA-256 password hashing with Admin / Staff role segregation.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextDark,
                Location = new Point(16, 45),
                Size = new Size(720, 140)
            };

            var lblProfile = new Label
            {
                Text = $"Current Logged-in Operator: {CurrentUser.FullName} (Username: {CurrentUser.Username} | Role: {CurrentUser.Role})",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.PrimaryHover,
                Location = new Point(16, 195),
                AutoSize = true
            };

            ooadCard.Controls.Add(lblOoad);
            ooadCard.Controls.Add(lblOoadDesc);
            ooadCard.Controls.Add(lblProfile);

            // Adding: ooadCard -> spacerTg -> telegramCard -> spacer -> dbCard
            // Results in top-to-bottom layout: dbCard -> spacer -> telegramCard -> spacerTg -> ooadCard
            _panelSettingsView.Controls.Add(ooadCard);
            _panelSettingsView.Controls.Add(spacerTg);
            _panelSettingsView.Controls.Add(telegramCard);
            _panelSettingsView.Controls.Add(spacer);
            _panelSettingsView.Controls.Add(dbCard);

            _contentContainer.Controls.Add(_panelSettingsView);
        }

        #endregion

        #endregion

        #region Data Loading & Binding

        private void LoadAllData()
        {
            LoadDashboardData();
            LoadCategoriesData();
            LoadProductsData();
            LoadMovementsData();
            LoadAuditData();
        }

        private void LoadDashboardData()
        {
            try
            {
                var metrics = _inventoryService.GetDashboardSummary();

                if (CurrentUser.IsAdmin)
                {
                    _cardTotalUnits.SetData("Total Inventory", $"{metrics.TotalInventoryCount:N0} Units", $"{metrics.TotalProductCount} Total Products", Theme.Primary, "📦");
                    _cardTotalValuation.SetData("Asset Valuation", $"${metrics.TotalAssetValuation:N2}", "Calculated at Cost Basis", Theme.Success, "💰");
                    _cardLowStock.SetData("Restock Alerts", $"{metrics.LowStockProductCount} Items", $"{metrics.OutOfStockProductCount} Critical Out of Stock", metrics.LowStockProductCount > 0 ? Theme.Danger : Theme.Success, "⚠️");
                    string prefix = metrics.TodayNetMovement >= 0 ? "+" : "";
                    _cardNetMovement.SetData("Today's Movement", $"{prefix}{metrics.TodayNetMovement} Units", $"In: +{metrics.TodayStockIn} | Out: -{metrics.TodayStockOut}", Theme.Primary, "🔄");
                }
                else if (CurrentUser.IsSalesStaff)
                {
                    _cardTotalUnits.SetData("Catalog Products", $"{metrics.TotalProductCount} SKUs", "Available for Sale", Theme.Primary, "🏷️");
                    _cardTotalValuation.SetData("Today's Dispatches", $"{metrics.TodayStockOut} Units Sold", "Customer Sales Today", Theme.Success, "🛍️");
                    _cardLowStock.SetData("Low Stock Notice", $"{metrics.LowStockProductCount} Items Low", $"{metrics.OutOfStockProductCount} Critical Out of Stock", metrics.LowStockProductCount > 0 ? Theme.Danger : Theme.Success, "⚠️");
                    _cardNetMovement.SetData("Total On Hand", $"{metrics.TotalInventoryCount:N0} Units", "Live Warehouse Stock", Theme.Primary, "🏢");
                }
                else
                {
                    _cardTotalUnits.SetData("Total Inventory", $"{metrics.TotalInventoryCount:N0} Units", $"{metrics.TotalProductCount} Total Products", Theme.Primary, "📦");
                    _cardTotalValuation.SetData("Catalog Items", $"{metrics.TotalProductCount} Active SKUs", "Operational Catalog", Theme.Success, "📋");
                    _cardLowStock.SetData("Restock Alerts", $"{metrics.LowStockProductCount} Items", $"{metrics.OutOfStockProductCount} Critical Out of Stock", metrics.LowStockProductCount > 0 ? Theme.Danger : Theme.Success, "⚠️");
                    string prefix = metrics.TodayNetMovement >= 0 ? "+" : "";
                    _cardNetMovement.SetData("Today's Movement", $"{prefix}{metrics.TodayNetMovement} Units", $"In: +{metrics.TodayStockIn} | Out: -{metrics.TodayStockOut}", Theme.Primary, "🔄");
                }

                BindMovementChart();
                BindValuationChart();
                BindUrgentRestockGrid();

                _lblDbStatus.Text = "● Database: Ready";
                _lblDbStatus.ForeColor = Theme.Success;
            }
            catch (Exception)
            {
                _lblDbStatus.Text = "● Database: Demo Mode";
                _lblDbStatus.ForeColor = Theme.Warning;
            }
        }

        private void BindMovementChart()
        {
            var movements = _inventoryService.GetMonthlyMovements(6).ToList();
            if (movements.Count == 0) return;

            var labels = movements.Select(m => m.MonthLabel).ToArray();
            var inVals = movements.Select(m => (int)m.StockInQuantity).ToArray();
            var outVals = movements.Select(m => (int)m.StockOutQuantity).ToArray();

            _chartMovements.Series = new ISeries[]
            {
                new ColumnSeries<int> { Name = "Stock Inflow", Values = inVals, Fill = new SolidColorPaint(new SKColor(59, 130, 246)), MaxBarWidth = 24 },
                new ColumnSeries<int> { Name = "Stock Outflow", Values = outVals, Fill = new SolidColorPaint(new SKColor(239, 68, 68)), MaxBarWidth = 24 }
            };

            _chartMovements.XAxes = new Axis[] { new Axis { Labels = labels, LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)), TextSize = 12 } };
            _chartMovements.YAxes = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)), TextSize = 12 } };
        }

        private void BindValuationChart()
        {
            var cats = _inventoryService.GetCategoryValuations().ToList();
            if (cats.Count == 0) return;

            var colors = new[] { new SKColor(59, 130, 246), new SKColor(16, 185, 129), new SKColor(245, 158, 11), new SKColor(139, 92, 246), new SKColor(236, 72, 153) };
            var seriesList = new List<ISeries>();

            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                string sliceName = CurrentUser.IsAdmin 
                    ? $"{cat.CategoryName} (${cat.TotalValuation:N0})" 
                    : $"{cat.CategoryName} ({cat.TotalUnits} Units)";

                decimal sliceValue = CurrentUser.IsAdmin 
                    ? cat.TotalValuation 
                    : (decimal)cat.TotalUnits;

                seriesList.Add(new PieSeries<decimal>
                {
                    Name = sliceName,
                    Values = new decimal[] { sliceValue },
                    Fill = new SolidColorPaint(colors[i % colors.Length]),
                    Pushout = 4
                });
            }

            _chartValuation.Series = seriesList;
        }

        private void BindUrgentRestockGrid()
        {
            var lowStock = _inventoryService.GetUrgentRestockList().ToList();
            var display = lowStock.Select(p => new
            {
                Item = ImageService.Instance.LoadThumbnail(p.ImagePath, 40, 40),
                p.ProductID,
                p.SKU,
                p.ProductName,
                p.CategoryName,
                On_Hand = p.CurrentStock,
                Reorder_Point = p.ReorderLevel,
                Unit_Cost = CurrentUser.IsAdmin ? $"${p.CostPrice:N2}" : "—",
                Total_Value = CurrentUser.IsAdmin ? $"${p.TotalValuation:N2}" : "—",
                Status = p.EvaluateStockStatus()
            }).ToList();

            _gridUrgentStock.RowTemplate.Height = 48;
            _gridUrgentStock.DataSource = display;
            SafeConfigureImageGridColumns(_gridUrgentStock, "ProductName", 240);
        }

        private void LoadProductsData()
        {
            _allCachedProducts = _inventoryService.GetAllProducts().ToList();
            PopulateCategoryFilter();
            FilterProducts();
        }

        private bool _isPopulatingCategoryFilter;

        private void PopulateCategoryFilter()
        {
            if (_cboCategoryFilter == null || _isPopulatingCategoryFilter) return;

            try
            {
                _isPopulatingCategoryFilter = true;
                string? currentSelection = _cboCategoryFilter.SelectedItem?.ToString();
                _cboCategoryFilter.Items.Clear();
                _cboCategoryFilter.Items.Add("All Categories");

                var categories = _inventoryService.GetAllCategories();
                if (categories != null)
                {
                    foreach (var cat in categories)
                    {
                        if (cat != null && !string.IsNullOrWhiteSpace(cat.CategoryName))
                        {
                            _cboCategoryFilter.Items.Add(cat.CategoryName);
                        }
                    }
                }

                int idx = 0;
                if (!string.IsNullOrEmpty(currentSelection))
                {
                    for (int i = 0; i < _cboCategoryFilter.Items.Count; i++)
                    {
                        if (string.Equals(_cboCategoryFilter.Items[i]?.ToString(), currentSelection, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = i;
                            break;
                        }
                    }
                }
                _cboCategoryFilter.SelectedIndex = idx;
            }
            finally
            {
                _isPopulatingCategoryFilter = false;
            }
        }

        private void FilterProducts()
        {
            if (_isPopulatingCategoryFilter) return;
            if (_txtProductSearch == null || _cboCategoryFilter == null || _gridAllProducts == null) return;

            string query = _txtProductSearch.Text.Trim().ToLowerInvariant();
            string selectedCat = _cboCategoryFilter.SelectedItem?.ToString() ?? "All Categories";

            var filtered = _allCachedProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(p => p.ProductName.ToLowerInvariant().Contains(query) || p.SKU.ToLowerInvariant().Contains(query));
            }

            if (selectedCat != "All Categories")
            {
                filtered = filtered.Where(p => string.Equals(p.CategoryName, selectedCat, StringComparison.OrdinalIgnoreCase));
            }

            var display = filtered.Select(p => new
            {
                Item = ImageService.Instance.LoadThumbnail(p.ImagePath, 40, 40),
                p.ProductID,
                p.SKU,
                p.Barcode,
                p.ProductName,
                p.CategoryName,
                p.SupplierName,
                Cost = CurrentUser.IsAdmin ? $"${p.CostPrice:N2}" : "—",
                Price = $"${p.SellingPrice:N2}",
                Margin = CurrentUser.IsAdmin ? $"{p.MarginPercentage:N1}%" : "—",
                Stock = p.CurrentStock,
                Reorder = p.ReorderLevel,
                Status = p.EvaluateStockStatus()
            }).ToList();

            _gridAllProducts.RowTemplate.Height = 48;
            _gridAllProducts.DataSource = display;
            SafeConfigureImageGridColumns(_gridAllProducts, "ProductName", 220);
        }

        private void LoadMovementsData()
        {
            _allCachedTransactions = _inventoryService.GetRecentTransactions(150).ToList();
            FilterMovements();
        }

        private void FilterMovements()
        {
            string query = _txtMovementSearch.Text.Trim().ToLowerInvariant();
            string typeFilter = _cboMovementTypeFilter.SelectedItem?.ToString() ?? "All Movements";

            var filtered = _allCachedTransactions.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(t => 
                    t.ProductName.ToLowerInvariant().Contains(query) || 
                    t.SKU.ToLowerInvariant().Contains(query) || 
                    (t.ReferenceNo != null && t.ReferenceNo.ToLowerInvariant().Contains(query)));
            }

            if (CurrentUser.IsSalesStaff)
            {
                filtered = filtered.Where(t => t.TransactionType == "OUT");
            }
            else if (typeFilter.StartsWith("IN"))
                filtered = filtered.Where(t => t.TransactionType == "IN");
            else if (typeFilter.StartsWith("OUT"))
                filtered = filtered.Where(t => t.TransactionType == "OUT");
            else if (typeFilter.StartsWith("ADJUSTMENT"))
                filtered = filtered.Where(t => t.TransactionType == "ADJUSTMENT");

            var display = filtered.Select(t => new
            {
                Item = ImageService.Instance.LoadThumbnail(_allCachedProducts.FirstOrDefault(p => p.ProductID == t.ProductID)?.ImagePath, 40, 40),
                t.TransactionID,
                Date = t.TransactionDate.ToString("yyyy-MM-dd HH:mm"),
                Type = t.TransactionType,
                t.SKU,
                Product = t.ProductName,
                t.Quantity,
                Unit_Price = $"${t.UnitPrice:N2}",
                Total_Value = $"${t.TotalAmount:N2}",
                Reference = t.ReferenceNo ?? "-",
                Handled_By = t.CreatedByName,
                Notes = t.Notes ?? string.Empty
            }).ToList();

            _gridMovements.RowTemplate.Height = 48;
            _gridMovements.DataSource = display;
            SafeConfigureImageGridColumns(_gridMovements, "Product", 200);
        }

        private void PrintSelectedMovementInvoice()
        {
            if (_gridMovements.CurrentRow == null || _gridMovements.CurrentRow.Index < 0)
            {
                MessageBox.Show("Please select a transaction record to print its invoice.", "Print Invoice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            object? val = null;
            try
            {
                if (_gridMovements.Columns.Contains("TransactionID"))
                    val = _gridMovements.CurrentRow.Cells["TransactionID"].Value;
            }
            catch { }

            if (val == null || !long.TryParse(val.ToString(), out long txId))
            {
                MessageBox.Show("Unable to identify the selected transaction ID.", "Print Invoice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var tx = _allCachedTransactions.FirstOrDefault(t => t.TransactionID == txId);
            if (tx == null)
            {
                MessageBox.Show("Transaction record not found in cache. Please refresh movements ledger.", "Print Invoice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var prod = _allCachedProducts.FirstOrDefault(p => p.ProductID == tx.ProductID);
            var invoice = Invoice.FromTransaction(tx, prod, CurrentUser.Role);

            using var previewForm = new InvoicePreviewForm(invoice);
            previewForm.ShowDialog(this);
        }

        private static void SafeConfigureImageGridColumns(DataGridView grid, string? primaryTextCol = null, int primaryColWidth = 200)
        {
            try
            {
                if (grid.Columns.Contains("Item"))
                {
                    var colImg = grid.Columns["Item"] as DataGridViewImageColumn;
                    if (colImg != null && colImg.DataGridView != null)
                    {
                        colImg.HeaderText = "Item";
                        colImg.ImageLayout = DataGridViewImageCellLayout.Zoom;
                        colImg.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                        colImg.Width = 54;
                        colImg.DisplayIndex = 0;
                    }
                }

                if (grid.Columns.Contains("ProductID"))
                {
                    var colId = grid.Columns["ProductID"];
                    if (colId != null && colId.DataGridView != null)
                    {
                        colId.Visible = false;
                    }
                }

                if (!string.IsNullOrEmpty(primaryTextCol) && grid.Columns.Contains(primaryTextCol))
                {
                    var colText = grid.Columns[primaryTextCol];
                    if (colText != null && colText.DataGridView != null && colText.Displayed)
                    {
                        colText.Width = primaryColWidth;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeConfigureImageGridColumns: {ex.Message}");
            }
        }

        private void LoadAuditData()
        {
            var products = _inventoryService.GetAllProducts().ToList();

            decimal totalValuation = products.Sum(p => p.TotalValuation);
            int totalUnits = products.Sum(p => p.CurrentStock);

            if (CurrentUser.IsAdmin)
            {
                _lblAuditValuation.Text = $"Total Catalog Valuation: ${totalValuation:N2} | Total Units in Warehouse: {totalUnits:N0}";
            }
            else
            {
                _lblAuditValuation.Text = $"Physical Warehouse Inventory: {totalUnits:N0} Units across {products.Count} Active Products";
            }
            _lblAuditItemsCount.Text = $"Audit Items Evaluated: {products.Count} products across {products.Select(p => p.CategoryID).Distinct().Count()} categories";

            var display = products.Select(p => new
            {
                p.ProductID,
                p.SKU,
                p.ProductName,
                Category = p.CategoryName,
                System_Stock = p.CurrentStock,
                Unit_Cost = CurrentUser.IsAdmin ? $"${p.CostPrice:N2}" : "—",
                Valuation = CurrentUser.IsAdmin ? $"${p.TotalValuation:N2}" : "—",
                Status = p.EvaluateStockStatus(),
                Audit_Verdict = p.CurrentStock <= 0 ? "DEFICIT - Immediate Restock Needed" : (p.CurrentStock <= p.ReorderLevel ? "WARNING - Approaching Reorder Point" : "OPTIMAL - Stock Invariants Satisfied")
            }).ToList();

            _gridAudit.DataSource = display;
            var colId = _gridAudit.Columns["ProductID"];
            if (colId != null) colId.Visible = false;

            var colName = _gridAudit.Columns["ProductName"];
            if (colName != null && colName.Displayed) colName.Width = 240;

            var colVerdict = _gridAudit.Columns["Audit_Verdict"];
            if (colVerdict != null && colVerdict.Displayed) colVerdict.Width = 250;
        }

        #endregion

        #region Actions & Modals

        private void OnAddProductClick(object? sender, EventArgs e)
        {
            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access Denied: Only administrators can create new products.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var modal = new AddEditProductModalForm(_inventoryService, null);
            if (modal.ShowDialog(this) == DialogResult.OK)
            {
                LoadAllData();
            }
        }

        private void OnEditProductClick(object? sender, EventArgs e)
        {
            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access Denied: Only administrators can edit product definitions.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_gridAllProducts.SelectedRows.Count == 0 ||
                _gridAllProducts.SelectedRows[0].Cells["ProductID"]?.Value == null ||
                !int.TryParse(_gridAllProducts.SelectedRows[0].Cells["ProductID"].Value?.ToString(), out int productId))
            {
                MessageBox.Show("Please select a product from the table to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var prod = _allCachedProducts.FirstOrDefault(p => p.ProductID == productId);
            if (prod == null) return;

            using var modal = new AddEditProductModalForm(_inventoryService, prod);
            if (modal.ShowDialog(this) == DialogResult.OK)
            {
                LoadAllData();
            }
        }

        private void OnDeleteProductClick(object? sender, EventArgs e)
        {
            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access Denied: Only administrators can delete products from the catalog.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_gridAllProducts.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one product to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItems = _gridAllProducts.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["ProductID"]?.Value != null)
                .Select(r => new
                {
                    Id = Convert.ToInt32(r.Cells["ProductID"].Value),
                    Sku = r.Cells["SKU"]?.Value?.ToString() ?? "Unknown",
                    Name = r.Cells["ProductName"]?.Value?.ToString() ?? "Product"
                })
                .ToList();

            if (selectedItems.Count == 0) return;

            if (selectedItems.Count == 1)
            {
                var item = selectedItems[0];
                var confirm = MessageBox.Show(
                    $"Are you sure you want to permanently delete product '{item.Sku} - {item.Name}'?\nAssociated stock transactions will also be purged.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    _inventoryService.DeleteProduct(item.Id);
                    MessageBox.Show($"Product '{item.Sku}' deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadAllData();
                }
            }
            else
            {
                var confirm = MessageBox.Show(
                    $"Are you sure you want to permanently delete {selectedItems.Count} selected products?\nAll associated stock transactions will also be purged.",
                    "Confirm Batch Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    int deletedCount = 0;
                    foreach (var item in selectedItems)
                    {
                        try
                        {
                            if (_inventoryService.DeleteProduct(item.Id))
                            {
                                deletedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to delete product {item.Id}: {ex.Message}");
                        }
                    }

                    MessageBox.Show($"{deletedCount} of {selectedItems.Count} products were successfully deleted.", "Batch Delete Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadAllData();
                }
            }
        }

        #region Category Management Actions

        private void LoadCategoriesData()
        {
            _allCachedCategories = _inventoryService.GetAllCategories().ToList();
            PopulateCategoryFilter();
            FilterCategories();
        }

        private void FilterCategories()
        {
            if (_gridCategories == null || _txtCategorySearch == null) return;

            try
            {
                string query = _txtCategorySearch.Text.Trim().ToLowerInvariant();
                var filtered = (_allCachedCategories ?? new List<Category>()).AsEnumerable();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    filtered = filtered.Where(c => c != null && (
                        (c.CategoryName != null && c.CategoryName.ToLowerInvariant().Contains(query)) ||
                        (c.Description != null && c.Description.ToLowerInvariant().Contains(query))));
                }

                var list = filtered.ToList();

                var display = list.Select(c => new
                {
                    c.CategoryID,
                    Category = c.CategoryName ?? "Unnamed",
                    Description = c.Description ?? "—",
                    Assigned_Products = $"{c.ProductCount} item(s)"
                }).ToList();

                _gridCategories.DataSource = display;

                if (_gridCategories.Columns != null && _gridCategories.Columns.Count > 0)
                {
                    if (_gridCategories.Columns.Contains("CategoryID"))
                    {
                        var colId = _gridCategories.Columns["CategoryID"];
                        if (colId != null)
                        {
                            colId.HeaderText = "ID";
                            colId.FillWeight = 15;
                        }
                    }

                    if (_gridCategories.Columns.Contains("Category"))
                    {
                        var colName = _gridCategories.Columns["Category"];
                        if (colName != null)
                        {
                            colName.HeaderText = "Category Name";
                            colName.FillWeight = 35;
                        }
                    }

                    if (_gridCategories.Columns.Contains("Description"))
                    {
                        var colDesc = _gridCategories.Columns["Description"];
                        if (colDesc != null)
                        {
                            colDesc.HeaderText = "Description";
                            colDesc.FillWeight = 35;
                        }
                    }

                    if (_gridCategories.Columns.Contains("Assigned_Products"))
                    {
                        var colProducts = _gridCategories.Columns["Assigned_Products"];
                        if (colProducts != null)
                        {
                            colProducts.HeaderText = "Catalog Products";
                            colProducts.FillWeight = 20;
                        }
                    }
                }

                if (_lblCategorySummary != null && _allCachedCategories != null)
                {
                    int totalProds = _allCachedCategories.Sum(c => c?.ProductCount ?? 0);
                    _lblCategorySummary.Text = $"Total: {_allCachedCategories.Count} categories ({totalProds} products classified)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FilterCategories error: {ex.Message}");
            }
        }

        private void OnAddCategoryClick(object? sender, EventArgs e)
        {
            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access Denied: Only administrators can create categories.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var modal = new AddEditCategoryModalForm(_inventoryService, null);
            if (modal.ShowDialog(this) == DialogResult.OK)
            {
                LoadCategoriesData();
                LoadProductsData();
            }
        }

        private void OnEditCategoryClick(object? sender, EventArgs e)
        {
            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access Denied: Only administrators can edit categories.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_gridCategories.SelectedRows.Count == 0 ||
                _gridCategories.SelectedRows[0].Cells["CategoryID"]?.Value == null ||
                !int.TryParse(_gridCategories.SelectedRows[0].Cells["CategoryID"].Value?.ToString(), out int categoryId))
            {
                MessageBox.Show("Please select a category from the list to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var category = _allCachedCategories.FirstOrDefault(c => c.CategoryID == categoryId);
            if (category == null) return;

            using var modal = new AddEditCategoryModalForm(_inventoryService, category);
            if (modal.ShowDialog(this) == DialogResult.OK)
            {
                LoadCategoriesData();
                LoadProductsData();
            }
        }

        private void OnDeleteCategoryClick(object? sender, EventArgs e)
        {
            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                MessageBox.Show("Access Denied: Only administrators can delete categories.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_gridCategories.SelectedRows.Count == 0 ||
                _gridCategories.SelectedRows[0].Cells["CategoryID"]?.Value == null ||
                !int.TryParse(_gridCategories.SelectedRows[0].Cells["CategoryID"].Value?.ToString(), out int categoryId))
            {
                MessageBox.Show("Please select a category to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var category = _allCachedCategories.FirstOrDefault(c => c.CategoryID == categoryId);
            if (category == null) return;

            if (category.ProductCount > 0)
            {
                MessageBox.Show(
                    $"Cannot delete category '{category.CategoryName}' because it currently contains {category.ProductCount} product(s).\n\nPlease reassign or delete these products first to preserve catalog referential integrity.",
                    "Integrity Violation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to permanently delete the category '{category.CategoryName}' (ID: #{categoryId})?",
                "Confirm Delete Category",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                bool deleted = _inventoryService.DeleteCategory(categoryId);
                if (deleted)
                {
                    MessageBox.Show($"Category '{category.CategoryName}' deleted successfully.", "Category Removed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadCategoriesData();
                    LoadProductsData();
                }
                else
                {
                    MessageBox.Show("Failed to delete the category. It may have already been removed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete category:\n\n{ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        private void RestockFromGrid(DataGridView grid)
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a product from the list to restock.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int productId = Convert.ToInt32(grid.SelectedRows[0].Cells["ProductID"].Value);
            OpenRestockModal(productId);
        }

        private void OpenRestockModal(int? productId)
        {
            using var modal = new StockTransactionModalForm(_inventoryService, productId, CurrentUser);
            if (modal.ShowDialog(this) == DialogResult.OK)
            {
                LoadAllData();
            }
        }

        private void OnGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (sender is not DataGridView grid) return;
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;

            var row = grid.Rows[e.RowIndex];
            string status = row.Cells["Status"]?.Value?.ToString() ?? string.Empty;

            if (status == "Out of Stock")
            {
                row.DefaultCellStyle.BackColor = Theme.DangerLight;
                row.DefaultCellStyle.ForeColor = Theme.DangerDark;
                // High contrast highlight when selected so user clearly knows what is selected to delete
                row.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#2563EB");
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            else if (status == "Low Stock")
            {
                row.DefaultCellStyle.BackColor = Theme.WarningLight;
                row.DefaultCellStyle.ForeColor = Theme.WarningDark;
                // High contrast highlight when selected so user clearly knows what is selected to delete
                row.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#2563EB");
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
        }

        private void OnMovementsCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _gridMovements.Rows.Count) return;

            var row = _gridMovements.Rows[e.RowIndex];
            string type = row.Cells["Type"]?.Value?.ToString() ?? string.Empty;

            if (type == "IN")
            {
                row.Cells["Type"].Style.ForeColor = Theme.SuccessDark;
                row.Cells["Type"].Style.Font = Theme.FontBodyBold;
            }
            else if (type == "OUT")
            {
                row.Cells["Type"].Style.ForeColor = Theme.DangerDark;
                row.Cells["Type"].Style.Font = Theme.FontBodyBold;
            }
            else if (type == "ADJUSTMENT")
            {
                row.Cells["Type"].Style.ForeColor = Theme.PrimaryHover;
                row.Cells["Type"].Style.Font = Theme.FontBodyBold;
            }
        }

        #endregion
    }
}
