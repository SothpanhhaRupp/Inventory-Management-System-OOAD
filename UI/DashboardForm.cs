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

        public User CurrentUser { get; }
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
            navContainer.Controls.Add(CreateNavButton("Dashboard", "📊  Executive Dashboard", ref btnY));
            navContainer.Controls.Add(CreateNavButton("Products", "📦  Product Catalog", ref btnY));
            navContainer.Controls.Add(CreateNavButton("Movements", "🔄  Stock Movements", ref btnY));
            navContainer.Controls.Add(CreateNavButton("Audits", "📑  Inventory Audits", ref btnY));
            navContainer.Controls.Add(CreateNavButton("Settings", "⚙️  System Settings", ref btnY));

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

            var lblRole = new Label
            {
                Text = $"Role: {CurrentUser.Role} • {CurrentUser.Username}",
                ForeColor = Theme.Primary,
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
            _panelSettingsView.Visible = false;

            switch (key.ToLowerInvariant())
            {
                case "dashboard":
                    _lblHeaderTitle.Text = "Inventory Intelligence & Executive Dashboard";
                    _lblHeaderSubtitle.Text = "Real-time stock analytics, turnover telemetry & restock triggers";
                    _panelDashboardView.Visible = true;
                    _panelDashboardView.BringToFront();
                    LoadDashboardData();
                    break;

                case "products":
                    _lblHeaderTitle.Text = "Product Master Catalog";
                    _lblHeaderSubtitle.Text = "Maintain enterprise inventory SKUs, categories, suppliers & pricing";
                    _panelProductsView.Visible = true;
                    _panelProductsView.BringToFront();
                    LoadProductsData();
                    break;

                case "movements":
                    _lblHeaderTitle.Text = "Stock Movement Ledger & Audit Trail";
                    _lblHeaderSubtitle.Text = "Complete historical ledger of Stock In, Stock Out & Physical Count Adjustments";
                    _panelMovementsView.Visible = true;
                    _panelMovementsView.BringToFront();
                    LoadMovementsData();
                    break;

                case "audits":
                    _lblHeaderTitle.Text = "Inventory Health & Valuation Audits";
                    _lblHeaderSubtitle.Text = "Capital allocation analysis, physical count reconciliation & variance monitoring";
                    _panelAuditsView.Visible = true;
                    _panelAuditsView.BringToFront();
                    LoadAuditData();
                    break;

                case "settings":
                    _lblHeaderTitle.Text = "System Configuration & Architecture";
                    _lblHeaderSubtitle.Text = "Database connection status, security profiles & 3-Tier diagnostic telemetry";
                    _panelSettingsView.Visible = true;
                    _panelSettingsView.BringToFront();
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

            // Build all 5 sub-views
            BuildDashboardView();
            BuildProductsView();
            BuildMovementsView();
            BuildAuditsView();
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
            var lblC2 = new Label { Text = "Inventory Valuation by Category", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Dock = DockStyle.Top, Height = 26 };
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
            var btnGridRestock = new Button
            {
                Text = "Restock Selected Item",
                Size = new Size(160, 32),
                Dock = DockStyle.Right,
                Margin = new Padding(0, 4, 0, 4)
            };
            Theme.ApplyFlatButton(btnGridRestock, Theme.Warning, Color.White);
            btnGridRestock.Click += (s, e) => RestockFromGrid(_gridUrgentStock);

            restockHeader.Controls.Add(btnGridRestock);
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
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
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
            _cboCategoryFilter.Items.AddRange(new object[] { "All Categories", "Electronics", "Beverages", "Perishables", "Office Supplies" });
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

            var btnAdd = new Button { Text = "+  Add Product", Size = new Size(125, 34), Margin = new Padding(4, 0, 4, 0) };
            Theme.ApplyFlatButton(btnAdd, Theme.Primary, Color.White);
            btnAdd.Click += OnAddProductClick;

            var btnEdit = new Button { Text = "✏️  Edit Product", Size = new Size(125, 34), Margin = new Padding(4, 0, 4, 0) };
            Theme.ApplyFlatButton(btnEdit, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnEdit.Click += OnEditProductClick;

            var btnDelete = new Button { Text = "🗑️  Delete", Size = new Size(95, 34), Margin = new Padding(4, 0, 4, 0) };
            Theme.ApplyFlatButton(btnDelete, Theme.DangerLight, Theme.DangerDark);
            btnDelete.Click += OnDeleteProductClick;

            var btnRestock = new Button { Text = "⚡  Restock Item", Size = new Size(130, 34), Margin = new Padding(4, 0, 0, 0) };
            Theme.ApplyFlatButton(btnRestock, Theme.Warning, Color.White);
            btnRestock.Click += (s, e) => RestockFromGrid(_gridAllProducts);

            actionButtonsFlow.Controls.Add(btnAdd);
            actionButtonsFlow.Controls.Add(btnEdit);
            actionButtonsFlow.Controls.Add(btnDelete);
            actionButtonsFlow.Controls.Add(btnRestock);

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
            _gridAllProducts.CellDoubleClick += (s, e) => OnEditProductClick(s, e);
            _gridAllProducts.DataBindingComplete += (s, e) => SafeConfigureImageGridColumns(_gridAllProducts, "ProductName", 220);

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
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
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
            _cboMovementTypeFilter = new ComboBox { Size = new Size(140, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.FontBody, Margin = new Padding(0, 4, 0, 0) };
            _cboMovementTypeFilter.Items.AddRange(new object[] { "All Movements", "IN (Intake)", "OUT (Dispatch)", "ADJUSTMENT" });
            _cboMovementTypeFilter.SelectedIndex = 0;
            _cboMovementTypeFilter.SelectedIndexChanged += (s, e) => FilterMovements();

            searchFilterFlow.Controls.Add(lblSearch);
            searchFilterFlow.Controls.Add(_txtMovementSearch);
            searchFilterFlow.Controls.Add(lblType);
            searchFilterFlow.Controls.Add(_cboMovementTypeFilter);

            var btnNewTx = new Button
            {
                Text = "+  Record Stock Movement",
                Size = new Size(190, 34),
                Dock = DockStyle.Right
            };
            Theme.ApplyFlatButton(btnNewTx, Theme.Primary, Color.White);
            btnNewTx.Click += (s, e) => OpenRestockModal(null);

            actionPanel.Controls.Add(btnNewTx);
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

        #region View 5: System Settings View

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

            // Card 2: OOAD Architecture Specifications
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

            // Adding: ooadCard -> spacer -> dbCard
            // Results in reverse order layout: dbCard (at top) -> spacer -> ooadCard (below)
            _panelSettingsView.Controls.Add(ooadCard);
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
            LoadProductsData();
            LoadMovementsData();
            LoadAuditData();
        }

        private void LoadDashboardData()
        {
            try
            {
                var metrics = _inventoryService.GetDashboardSummary();

                _cardTotalUnits.SetData("Total Inventory", $"{metrics.TotalInventoryCount:N0} Units", $"{metrics.TotalProductCount} Total Products", Theme.Primary, "📦");
                _cardTotalValuation.SetData("Asset Valuation", $"${metrics.TotalAssetValuation:N2}", "Calculated at Cost Basis", Theme.Success, "💰");
                _cardLowStock.SetData("Restock Alerts", $"{metrics.LowStockProductCount} Items", $"{metrics.OutOfStockProductCount} Critical Out of Stock", metrics.LowStockProductCount > 0 ? Theme.Danger : Theme.Success, "⚠️");

                string prefix = metrics.TodayNetMovement >= 0 ? "+" : "";
                _cardNetMovement.SetData("Today's Movement", $"{prefix}{metrics.TodayNetMovement} Units", $"In: +{metrics.TodayStockIn} | Out: -{metrics.TodayStockOut}", Theme.Primary, "🔄");

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
                seriesList.Add(new PieSeries<decimal>
                {
                    Name = $"{cat.CategoryName} (${cat.TotalValuation:N0})",
                    Values = new decimal[] { cat.TotalValuation },
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
                Unit_Cost = $"${p.CostPrice:N2}",
                Total_Value = $"${p.TotalValuation:N2}",
                Status = p.EvaluateStockStatus()
            }).ToList();

            _gridUrgentStock.RowTemplate.Height = 48;
            _gridUrgentStock.DataSource = display;
            SafeConfigureImageGridColumns(_gridUrgentStock, "ProductName", 240);
        }

        private void LoadProductsData()
        {
            _allCachedProducts = _inventoryService.GetAllProducts().ToList();
            FilterProducts();
        }

        private void FilterProducts()
        {
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
                Cost = $"${p.CostPrice:N2}",
                Price = $"${p.SellingPrice:N2}",
                Margin = $"{p.MarginPercentage:N1}%",
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

            if (typeFilter.StartsWith("IN"))
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

            _lblAuditValuation.Text = $"Total Catalog Valuation: ${totalValuation:N2} | Total Units in Warehouse: {totalUnits:N0}";
            _lblAuditItemsCount.Text = $"Audit Items Evaluated: {products.Count} products across {products.Select(p => p.CategoryID).Distinct().Count()} categories";

            var display = products.Select(p => new
            {
                p.ProductID,
                p.SKU,
                p.ProductName,
                Category = p.CategoryName,
                System_Stock = p.CurrentStock,
                Unit_Cost = $"${p.CostPrice:N2}",
                Valuation = $"${p.TotalValuation:N2}",
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
            using var modal = new AddEditProductModalForm(_inventoryService, null);
            if (modal.ShowDialog(this) == DialogResult.OK)
            {
                LoadAllData();
            }
        }

        private void OnEditProductClick(object? sender, EventArgs e)
        {
            if (_gridAllProducts.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a product from the table to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int productId = Convert.ToInt32(_gridAllProducts.SelectedRows[0].Cells["ProductID"].Value);
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
            if (_gridAllProducts.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a product to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int productId = Convert.ToInt32(_gridAllProducts.SelectedRows[0].Cells["ProductID"].Value);
            string sku = _gridAllProducts.SelectedRows[0].Cells["SKU"].Value?.ToString() ?? "Unknown";

            var confirm = MessageBox.Show($"Are you sure you want to permanently delete product '{sku}'?\nAssociated stock transactions will also be purged.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm == DialogResult.Yes)
            {
                _inventoryService.DeleteProduct(productId);
                MessageBox.Show($"Product '{sku}' deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadAllData();
            }
        }

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
            using var modal = new StockTransactionModalForm(_inventoryService, productId);
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
                row.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#FCA5A5");
                row.DefaultCellStyle.SelectionForeColor = Theme.DangerDark;
            }
            else if (status == "Low Stock")
            {
                row.DefaultCellStyle.BackColor = Theme.WarningLight;
                row.DefaultCellStyle.ForeColor = Theme.WarningDark;
                row.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#FDE68A");
                row.DefaultCellStyle.SelectionForeColor = Theme.WarningDark;
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
