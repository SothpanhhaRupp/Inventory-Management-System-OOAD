using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.Exceptions;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Interactive dialog for executing atomic stock operations (Stock In, Stock Out, Adjustment).
    /// Features dynamic visual product verification preview card with image, SKU, and barcode lookup
    /// to eliminate warehouse picking and dispatch errors.
    /// </summary>
    public class StockTransactionModalForm : Form
    {
        private readonly IInventoryService _inventoryService;
        private readonly int? _initialProductId;

        private TextBox _txtBarcodeScan = null!;
        private ComboBox _cboProduct = null!;
        private ComboBox _cboType = null!;
        private NumericUpDown _numQuantity = null!;
        private NumericUpDown _numUnitPrice = null!;
        private TextBox _txtReference = null!;
        private TextBox _txtNotes = null!;
        private Label _lblStockPreview = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        // Dynamic Product Verification Preview Card Controls
        private Panel _previewCard = null!;
        private PictureBox _picProductPreview = null!;
        private Label _lblPreviewName = null!;
        private Label _lblPreviewSku = null!;
        private Label _lblPreviewCat = null!;
        private Label _lblPreviewStockBadge = null!;

        private List<Product> _products = new();

        public StockTransactionModalForm(IInventoryService inventoryService, int? productId = null)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _initialProductId = productId;

            InitializeCustomComponents();
            LoadProductData();
        }

        private void InitializeCustomComponents()
        {
            Text = "Record Stock Movement & Verification";
            Size = new Size(780, 670);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.CanvasBg;
            Font = Theme.FontBody;

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = Theme.CanvasBg
            };

            int y = 10;

            // Header Title
            var lblHeader = new Label
            {
                Text = "Record Stock Movement & Picking Verification",
                Font = Theme.FontHeadingMd,
                ForeColor = Theme.TextDark,
                AutoSize = true,
                Location = new Point(24, y)
            };
            mainPanel.Controls.Add(lblHeader);
            y += 28;

            var lblSubtitle = new Label
            {
                Text = "Logs immutable transaction audit trail, updates inventory on-hand balance, and verifies visual product match",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(24, y)
            };
            mainPanel.Controls.Add(lblSubtitle);
            y += 35;

            // Form Card Panel
            var cardPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(716, 480),
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            cardPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, cardPanel.Width - 1, cardPanel.Height - 1);
            };
            mainPanel.Controls.Add(cardPanel);

            int cy = 16;

            // --- Left Column: Transaction Inputs (Width: 440) ---

            // Quick Barcode / SKU Scan Box
            var lblScan = new Label { Text = "⚡ Quick Barcode / SKU Scanner", Font = Theme.FontHeadingSm, ForeColor = Theme.Primary, Location = new Point(16, cy), AutoSize = true };
            cardPanel.Controls.Add(lblScan);
            cy += 22;

            _txtBarcodeScan = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(430, 30),
                Font = Theme.FontBody,
                PlaceholderText = "Scan barcode or type SKU and hit Enter..."
            };
            _txtBarcodeScan.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    HandleBarcodeScan(_txtBarcodeScan.Text.Trim());
                }
            };
            cardPanel.Controls.Add(_txtBarcodeScan);
            cy += 36;

            // 1. Target Product Selector
            var lblProd = new Label { Text = "Target Product *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            cardPanel.Controls.Add(lblProd);
            cy += 24;

            _cboProduct = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, cy),
                Size = new Size(430, 30),
                Font = Theme.FontBody
            };
            _cboProduct.SelectedIndexChanged += OnProductOrTypeChanged;
            cardPanel.Controls.Add(_cboProduct);
            cy += 38;

            // 2. Transaction Type & Quantity
            var lblType = new Label { Text = "Movement Type *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            var lblQty = new Label { Text = "Quantity *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(236, cy), AutoSize = true };
            cardPanel.Controls.Add(lblType);
            cardPanel.Controls.Add(lblQty);
            cy += 24;

            _cboType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, cy),
                Size = new Size(210, 30),
                Font = Theme.FontBody
            };
            _cboType.Items.AddRange(new object[] { "IN (Restock / Intake)", "OUT (Sale / Dispatch)", "ADJUSTMENT (Audit Count)" });
            _cboType.SelectedIndex = 0;
            _cboType.SelectedIndexChanged += OnProductOrTypeChanged;
            cardPanel.Controls.Add(_cboType);

            _numQuantity = new NumericUpDown
            {
                Location = new Point(236, cy),
                Size = new Size(210, 30),
                Font = Theme.FontBodyBold,
                Minimum = 1,
                Maximum = 100000,
                Value = 10
            };
            _numQuantity.ValueChanged += OnProductOrTypeChanged;
            cardPanel.Controls.Add(_numQuantity);
            cy += 38;

            // 3. Unit Price & Reference
            var lblPrice = new Label { Text = "Unit Price ($) *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            var lblRef = new Label { Text = "Reference / PO #", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(236, cy), AutoSize = true };
            cardPanel.Controls.Add(lblPrice);
            cardPanel.Controls.Add(lblRef);
            cy += 24;

            _numUnitPrice = new NumericUpDown
            {
                Location = new Point(16, cy),
                Size = new Size(210, 30),
                Font = Theme.FontBody,
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 1000000,
                Value = 10.00m
            };
            cardPanel.Controls.Add(_numUnitPrice);

            _txtReference = new TextBox
            {
                Location = new Point(236, cy),
                Size = new Size(210, 30),
                Font = Theme.FontBody,
                Text = $"TRX-{DateTime.Now:yyyyMMddHHmm}"
            };
            cardPanel.Controls.Add(_txtReference);
            cy += 38;

            // 4. Notes / Reason
            var lblNotes = new Label { Text = "Notes / Reason", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            cardPanel.Controls.Add(lblNotes);
            cy += 24;

            _txtNotes = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(430, 44),
                Font = Theme.FontBody,
                Multiline = true,
                Text = "Standard stock operation."
            };
            cardPanel.Controls.Add(_txtNotes);
            cy += 52;

            // Stock Impact Preview Banner
            _lblStockPreview = new Label
            {
                Location = new Point(16, cy),
                Size = new Size(430, 34),
                BackColor = Theme.SuccessLight,
                ForeColor = Theme.SuccessDark,
                Font = Theme.FontBodyBold,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Stock Preview: 0 -> 10 (+10 units)"
            };
            cardPanel.Controls.Add(_lblStockPreview);

            // --- Right Column: Dynamic Verification Card (Width: 230, X: 470) ---
            _previewCard = new Panel
            {
                Location = new Point(466, 16),
                Size = new Size(234, 445),
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                Padding = new Padding(12)
            };
            _previewCard.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, _previewCard.Width - 1, _previewCard.Height - 1);
            };

            var lblCardTitle = new Label
            {
                Text = "Visual Verification",
                Font = Theme.FontHeadingSm,
                ForeColor = Theme.TextDark,
                Location = new Point(12, 10),
                AutoSize = true
            };
            _previewCard.Controls.Add(lblCardTitle);

            var lblCardSub = new Label
            {
                Text = "Verify product appearance:",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(12, 30),
                AutoSize = true
            };
            _previewCard.Controls.Add(lblCardSub);

            // Dedicated PictureBox control (150x150 px, SizeMode = PictureBoxSizeMode.Zoom, subtle border)
            _picProductPreview = new PictureBox
            {
                Location = new Point(42, 54),
                Size = new Size(150, 150),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            _picProductPreview.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, _picProductPreview.Width - 1, _picProductPreview.Height - 1);
            };
            _previewCard.Controls.Add(_picProductPreview);

            _lblPreviewName = new Label
            {
                Text = "Product Name",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextDark,
                Location = new Point(12, 214),
                Size = new Size(210, 40),
                TextAlign = ContentAlignment.TopCenter
            };
            _previewCard.Controls.Add(_lblPreviewName);

            _lblPreviewSku = new Label
            {
                Text = "SKU: - | Barcode: -",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(12, 258),
                Size = new Size(210, 32),
                TextAlign = ContentAlignment.TopCenter
            };
            _previewCard.Controls.Add(_lblPreviewSku);

            _lblPreviewCat = new Label
            {
                Text = "Category: -",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(12, 294),
                Size = new Size(210, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _previewCard.Controls.Add(_lblPreviewCat);

            _lblPreviewStockBadge = new Label
            {
                Text = "Current Stock: 0 units",
                Font = Theme.FontBodyBold,
                BackColor = Theme.SuccessLight,
                ForeColor = Theme.SuccessDark,
                Location = new Point(20, 324),
                Size = new Size(194, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _previewCard.Controls.Add(_lblPreviewStockBadge);

            var lblSafetyNote = new Label
            {
                Text = "✓ Visual matching eliminates picking & dispatch errors.",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(12, 370),
                Size = new Size(210, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _previewCard.Controls.Add(lblSafetyNote);

            cardPanel.Controls.Add(_previewCard);

            // Action Buttons at bottom
            int by = 565;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 38),
                Location = new Point(496, by)
            };
            Theme.ApplyFlatButton(_btnCancel, ColorTranslator.FromHtml("#E2E8F0"), Theme.TextDark);
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            mainPanel.Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = "Commit Movement",
                Size = new Size(180, 38),
                Location = new Point(616, by)
            };
            Theme.ApplyFlatButton(_btnSave, Theme.Primary, Color.White);
            _btnSave.Click += OnSaveTransactionClick;
            mainPanel.Controls.Add(_btnSave);

            Controls.Add(mainPanel);
        }

        private void LoadProductData()
        {
            try
            {
                _products = _inventoryService.GetAllProducts().ToList();
                _cboProduct.DataSource = _products;
                _cboProduct.DisplayMember = "ProductName";
                _cboProduct.ValueMember = "ProductID";

                if (_initialProductId.HasValue)
                {
                    var selected = _products.FirstOrDefault(p => p.ProductID == _initialProductId.Value);
                    if (selected != null)
                    {
                        _cboProduct.SelectedItem = selected;
                    }
                }

                UpdateStockPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load products: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleBarcodeScan(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            var match = _products.FirstOrDefault(p =>
                string.Equals(p.Barcode, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.SKU, query, StringComparison.OrdinalIgnoreCase) ||
                p.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                _cboProduct.SelectedItem = match;
                _txtBarcodeScan.Text = string.Empty;
            }
            else
            {
                MessageBox.Show($"No product found matching barcode or SKU: '{query}'", "Scan Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnProductOrTypeChanged(object? sender, EventArgs e)
        {
            UpdateStockPreview(sender);
        }

        private void UpdateStockPreview(object? sender = null)
        {
            if (_cboProduct.SelectedItem is not Product prod)
            {
                _picProductPreview.Image?.Dispose();
                _picProductPreview.Image = ImageService.Instance.GetDefaultPlaceholder(150, 150);
                _lblPreviewName.Text = "No Product Selected";
                _lblPreviewSku.Text = "SKU: -";
                _lblPreviewCat.Text = "Category: -";
                _lblPreviewStockBadge.Text = "Current Stock: -";
                return;
            }

            // 1. Update Dynamic Verification Preview Card
            _picProductPreview.Image?.Dispose();
            _picProductPreview.Image = ImageService.Instance.LoadImage(prod.ImagePath);

            _lblPreviewName.Text = prod.ProductName;
            _lblPreviewSku.Text = $"SKU: {prod.SKU}\nBarcode: {(string.IsNullOrEmpty(prod.Barcode) ? "N/A" : prod.Barcode)}";
            _lblPreviewCat.Text = $"Category: {prod.CategoryName}";

            if (prod.IsOutOfStock())
            {
                _lblPreviewStockBadge.BackColor = Theme.DangerLight;
                _lblPreviewStockBadge.ForeColor = Theme.DangerDark;
                _lblPreviewStockBadge.Text = "Out of Stock (0 units)";
            }
            else if (prod.IsLowStock())
            {
                _lblPreviewStockBadge.BackColor = Theme.WarningLight;
                _lblPreviewStockBadge.ForeColor = Theme.WarningDark;
                _lblPreviewStockBadge.Text = $"Low Stock ({prod.CurrentStock} units)";
            }
            else
            {
                _lblPreviewStockBadge.BackColor = Theme.SuccessLight;
                _lblPreviewStockBadge.ForeColor = Theme.SuccessDark;
                _lblPreviewStockBadge.Text = $"In Stock ({prod.CurrentStock} units)";
            }

            // 2. Update stock delta calculations
            int qty = (int)_numQuantity.Value;
            string selectedTypeStr = _cboType.SelectedItem?.ToString() ?? "IN";
            string type = selectedTypeStr.StartsWith("IN") ? "IN" : (selectedTypeStr.StartsWith("OUT") ? "OUT" : "ADJUSTMENT");

            // Auto-populate default unit price based on movement type
            if (sender == _cboType || sender == _cboProduct)
            {
                _numUnitPrice.Value = type == "OUT" ? prod.SellingPrice : prod.CostPrice;
            }

            int newStock = prod.CurrentStock;
            if (type == "IN")
            {
                newStock += qty;
                _lblStockPreview.BackColor = Theme.SuccessLight;
                _lblStockPreview.ForeColor = Theme.SuccessDark;
                _lblStockPreview.Text = $"Stock Influx: {prod.CurrentStock} -> {newStock} (+{qty} units)";
            }
            else if (type == "OUT")
            {
                newStock -= qty;
                if (newStock < 0)
                {
                    _lblStockPreview.BackColor = Theme.DangerLight;
                    _lblStockPreview.ForeColor = Theme.DangerDark;
                    _lblStockPreview.Text = $"⚠️ Stock Deficit! Requested: {qty}, Available: {prod.CurrentStock}";
                }
                else
                {
                    _lblStockPreview.BackColor = Theme.WarningLight;
                    _lblStockPreview.ForeColor = Theme.WarningDark;
                    _lblStockPreview.Text = $"Stock Dispatch: {prod.CurrentStock} -> {newStock} (-{qty} units)";
                }
            }
            else // ADJUSTMENT
            {
                newStock = qty;
                _lblStockPreview.BackColor = Theme.SuccessLight;
                _lblStockPreview.ForeColor = Theme.SuccessDark;
                _lblStockPreview.Text = $"Physical Count Override: {prod.CurrentStock} -> {newStock}";
            }
        }

        private void OnSaveTransactionClick(object? sender, EventArgs e)
        {
            if (_cboProduct.SelectedItem is not Product prod)
            {
                MessageBox.Show("Please select a target product.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int qty = (int)_numQuantity.Value;
            decimal price = _numUnitPrice.Value;
            string selectedTypeStr = _cboType.SelectedItem?.ToString() ?? "IN";
            string type = selectedTypeStr.StartsWith("IN") ? "IN" : (selectedTypeStr.StartsWith("OUT") ? "OUT" : "ADJUSTMENT");
            string refNo = _txtReference.Text.Trim();
            string notes = _txtNotes.Text.Trim();

            try
            {
                long txId = _inventoryService.RecordStockTransaction(prod.ProductID, type, qty, price, refNo, notes, 1);
                
                MessageBox.Show($"Stock transaction #{txId} committed successfully!\nUpdated stock balance for '{prod.ProductName}'.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InsufficientStockException ex)
            {
                MessageBox.Show($"Transaction Aborted by Domain Rule:\n\n{ex.Message}", 
                    "Insufficient Stock Policy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _numQuantity.Focus();
            }
            catch (InventoryDomainException ex)
            {
                MessageBox.Show($"Inventory Domain Error:\n\n{ex.Message}", 
                    "Domain Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error executing transaction:\n{ex.Message}", 
                    "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
