using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.Exceptions;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Modal dialog for creating new product records or editing existing ones in the catalog.
    /// Features dedicated product image upload, non-locking visual preview, and automatic file storage.
    /// </summary>
    public class AddEditProductModalForm : Form
    {
        private readonly IInventoryService _inventoryService;
        private readonly Product? _existingProduct;

        private TextBox _txtSku = null!;
        private TextBox _txtBarcode = null!;
        private TextBox _txtName = null!;
        private ComboBox _cboCategory = null!;
        private ComboBox _cboSupplier = null!;
        private NumericUpDown _numCost = null!;
        private NumericUpDown _numPrice = null!;
        private NumericUpDown _numStock = null!;
        private NumericUpDown _numReorder = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        // Image Management Controls
        private PictureBox _picProductImage = null!;
        private Button _btnBrowseImage = null!;
        private Button _btnRemoveImage = null!;
        private string? _selectedImageSourcePath;
        private bool _imageRemoved;

        public AddEditProductModalForm(IInventoryService inventoryService, Product? existingProduct = null)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _existingProduct = existingProduct;

            InitializeForm();
            PopulateData();
        }

        private void InitializeForm()
        {
            Text = _existingProduct == null ? "Add New Product" : "Edit Product Catalog Item";
            Size = new Size(760, 680);
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

            int y = 10;
            var lblTitle = new Label
            {
                Text = _existingProduct == null ? "Register New Inventory Product" : $"Edit Product: {_existingProduct.SKU}",
                Font = Theme.FontHeadingMd,
                ForeColor = Theme.TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            panel.Controls.Add(lblTitle);
            y += 28;

            var lblSub = new Label
            {
                Text = "Enforces global SKU uniqueness, category association, pricing invariants & visual asset storage",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            };
            panel.Controls.Add(lblSub);
            y += 32;

            var card = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(696, 490),
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

            // --- Left Column: Product Data Fields (Width: 460) ---

            // SKU & Barcode
            var lblSku = new Label { Text = "SKU Code *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            var lblBarcode = new Label { Text = "Barcode / UPC", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(246, cy), AutoSize = true };
            card.Controls.Add(lblSku);
            card.Controls.Add(lblBarcode);
            cy += 24;

            _txtSku = new TextBox { Location = new Point(16, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            _txtBarcode = new TextBox { Location = new Point(246, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            card.Controls.Add(_txtSku);
            card.Controls.Add(_txtBarcode);
            cy += 38;

            // Product Name
            var lblName = new Label { Text = "Product Name *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            card.Controls.Add(lblName);
            cy += 24;

            _txtName = new TextBox { Location = new Point(16, cy), Size = new Size(440, 30), Font = Theme.FontBody };
            card.Controls.Add(_txtName);
            cy += 38;

            // Category & Supplier
            var lblCat = new Label { Text = "Category *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            var lblSup = new Label { Text = "Supplier *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(246, cy), AutoSize = true };
            card.Controls.Add(lblCat);
            card.Controls.Add(lblSup);
            cy += 24;

            _cboCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            _cboCategory.Items.AddRange(new object[] { "1 - Electronics", "2 - Beverages", "3 - Perishables", "4 - Office Supplies" });
            _cboCategory.SelectedIndex = 0;

            _cboSupplier = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(246, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            _cboSupplier.Items.AddRange(new object[] { "1 - TechDistro Global Inc.", "2 - FreshGoods Supply Co." });
            _cboSupplier.SelectedIndex = 0;

            card.Controls.Add(_cboCategory);
            card.Controls.Add(_cboSupplier);
            cy += 38;

            // Cost Price & Selling Price
            var lblCost = new Label { Text = "Cost Price ($) *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            var lblPrice = new Label { Text = "Selling Price ($) *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(246, cy), AutoSize = true };
            card.Controls.Add(lblCost);
            card.Controls.Add(lblPrice);
            cy += 24;

            _numCost = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 1000000, Value = 10.00m, Location = new Point(16, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            _numPrice = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 1000000, Value = 15.00m, Location = new Point(246, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            card.Controls.Add(_numCost);
            card.Controls.Add(_numPrice);
            cy += 38;

            // Current Stock & Reorder Level
            var lblStock = new Label { Text = "Initial Stock Units", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(16, cy), AutoSize = true };
            var lblReorder = new Label { Text = "Reorder Level *", Font = Theme.FontHeadingSm, ForeColor = Theme.TextDark, Location = new Point(246, cy), AutoSize = true };
            card.Controls.Add(lblStock);
            card.Controls.Add(lblReorder);
            cy += 24;

            _numStock = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 25, Location = new Point(16, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            _numReorder = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 10, Location = new Point(246, cy), Size = new Size(210, 30), Font = Theme.FontBody };
            if (_existingProduct != null)
            {
                // In edit mode, disable changing current stock directly (must use stock movements)
                _numStock.Enabled = false;
            }
            card.Controls.Add(_numStock);
            card.Controls.Add(_numReorder);

            // --- Right Column: Dedicated Image Upload Section ---
            int imgX = 500;
            var lblImageTitle = new Label
            {
                Text = "Product Image",
                Font = Theme.FontHeadingSm,
                ForeColor = Theme.TextDark,
                Location = new Point(imgX, 16),
                AutoSize = true
            };
            card.Controls.Add(lblImageTitle);

            // Dedicated PictureBox control (150x150 px, SizeMode = PictureBoxSizeMode.Zoom, subtle border)
            _picProductImage = new PictureBox
            {
                Location = new Point(imgX, 42),
                Size = new Size(150, 150),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ColorTranslator.FromHtml("#F8FAFC")
            };
            _picProductImage.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1.5f);
                e.Graphics.DrawRectangle(p, 0, 0, _picProductImage.Width - 1, _picProductImage.Height - 1);
            };
            card.Controls.Add(_picProductImage);

            // Browse Image Button (triggers OpenFileDialog filtered to .png, .jpg, .jpeg)
            _btnBrowseImage = new Button
            {
                Text = "📁  Browse Image",
                Location = new Point(imgX, 202),
                Size = new Size(150, 34),
                Cursor = Cursors.Hand
            };
            Theme.ApplyFlatButton(_btnBrowseImage, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            _btnBrowseImage.Click += OnBrowseImageClick;
            card.Controls.Add(_btnBrowseImage);

            // Remove Image Button (resets selection back to default placeholder)
            _btnRemoveImage = new Button
            {
                Text = "🗑️  Remove Image",
                Location = new Point(imgX, 244),
                Size = new Size(150, 30),
                Cursor = Cursors.Hand
            };
            Theme.ApplyFlatButton(_btnRemoveImage, ColorTranslator.FromHtml("#FEE2E2"), Theme.DangerDark);
            _btnRemoveImage.Click += OnRemoveImageClick;
            card.Controls.Add(_btnRemoveImage);

            // Image format & storage note
            var lblImageHint = new Label
            {
                Text = "Formats: .png, .jpg, .jpeg\nStored in /Uploads/Products",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(imgX, 284),
                Size = new Size(160, 40),
                TextAlign = ContentAlignment.TopCenter
            };
            card.Controls.Add(lblImageHint);

            // Form Action Buttons
            int by = 575;
            _btnCancel = new Button { Text = "Cancel", Size = new Size(110, 38), Location = new Point(486, by) };
            Theme.ApplyFlatButton(_btnCancel, ColorTranslator.FromHtml("#E2E8F0"), Theme.TextDark);
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            panel.Controls.Add(_btnCancel);

            _btnSave = new Button { Text = _existingProduct == null ? "Save Product" : "Update Product", Size = new Size(140, 38), Location = new Point(606, by) };
            Theme.ApplyFlatButton(_btnSave, Theme.Primary, Color.White);
            _btnSave.Click += OnSaveClick;
            panel.Controls.Add(_btnSave);

            Controls.Add(panel);
        }

        private void PopulateData()
        {
            if (_existingProduct == null)
            {
                _txtSku.Text = $"PROD-{DateTime.Now:fff}";
                _picProductImage.Image = ImageService.Instance.GetDefaultPlaceholder(150, 150);
                return;
            }

            _txtSku.Text = _existingProduct.SKU;
            _txtBarcode.Text = _existingProduct.Barcode ?? string.Empty;
            _txtName.Text = _existingProduct.ProductName;
            _numCost.Value = _existingProduct.CostPrice;
            _numPrice.Value = _existingProduct.SellingPrice;
            _numStock.Value = _existingProduct.CurrentStock;
            _numReorder.Value = _existingProduct.ReorderLevel;

            int catIndex = Math.Clamp(_existingProduct.CategoryID - 1, 0, _cboCategory.Items.Count - 1);
            _cboCategory.SelectedIndex = catIndex;

            int supIndex = Math.Clamp(_existingProduct.SupplierID - 1, 0, _cboSupplier.Items.Count - 1);
            _cboSupplier.SelectedIndex = supIndex;

            // Load existing product image or placeholder
            _picProductImage.Image = ImageService.Instance.LoadImage(_existingProduct.ImagePath);
        }

        private void OnBrowseImageClick(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Product Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|PNG Images (*.png)|*.png|JPEG Images (*.jpg;*.jpeg)|*.jpg;*.jpeg|All Files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    // Non-locking preview: read bytes into memory stream so file is not locked
                    byte[] bytes = File.ReadAllBytes(ofd.FileName);
                    using var ms = new MemoryStream(bytes);
                    using var original = Image.FromStream(ms);

                    // Deep-clone bitmap for PictureBox
                    _picProductImage.Image?.Dispose();
                    _picProductImage.Image = new Bitmap(original);

                    _selectedImageSourcePath = ofd.FileName;
                    _imageRemoved = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load selected image:\n\n{ex.Message}", "Image Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnRemoveImageClick(object? sender, EventArgs e)
        {
            _picProductImage.Image?.Dispose();
            _picProductImage.Image = ImageService.Instance.GetDefaultPlaceholder(150, 150);
            _selectedImageSourcePath = null;
            _imageRemoved = true;
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            string sku = _txtSku.Text.Trim();
            string name = _txtName.Text.Trim();

            if (string.IsNullOrWhiteSpace(sku))
            {
                MessageBox.Show("SKU is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtSku.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Product Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            int categoryId = _cboCategory.SelectedIndex + 1;
            int supplierId = _cboSupplier.SelectedIndex + 1;
            string categoryName = _cboCategory.SelectedItem?.ToString()?.Split('-')[1].Trim() ?? "General";
            string supplierName = _cboSupplier.SelectedItem?.ToString()?.Split('-')[1].Trim() ?? "Vendor";

            try
            {
                // Resolve ImagePath: handle new uploads, removals, or existing references
                string? finalImagePath = _existingProduct?.ImagePath;

                if (_imageRemoved)
                {
                    if (!string.IsNullOrWhiteSpace(_existingProduct?.ImagePath))
                    {
                        ImageService.Instance.DeleteImage(_existingProduct.ImagePath);
                    }
                    finalImagePath = null;
                }
                else if (!string.IsNullOrWhiteSpace(_selectedImageSourcePath))
                {
                    // Save image physical copy using unique GUID name
                    finalImagePath = ImageService.Instance.SaveImage(_selectedImageSourcePath);
                }

                if (_existingProduct == null)
                {
                    // Create
                    var newProd = new Product
                    {
                        SKU = sku,
                        Barcode = string.IsNullOrWhiteSpace(_txtBarcode.Text) ? null : _txtBarcode.Text.Trim(),
                        ProductName = name,
                        CategoryID = categoryId,
                        CategoryName = categoryName,
                        SupplierID = supplierId,
                        SupplierName = supplierName,
                        CostPrice = _numCost.Value,
                        SellingPrice = _numPrice.Value,
                        CurrentStock = (int)_numStock.Value,
                        ReorderLevel = (int)_numReorder.Value,
                        ImagePath = finalImagePath,
                        CreatedAt = DateTime.Now
                    };

                    _inventoryService.CreateProduct(newProd);
                    MessageBox.Show($"Product '{name}' (SKU: {sku}) created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Update
                    _existingProduct.SKU = sku;
                    _existingProduct.Barcode = string.IsNullOrWhiteSpace(_txtBarcode.Text) ? null : _txtBarcode.Text.Trim();
                    _existingProduct.ProductName = name;
                    _existingProduct.CategoryID = categoryId;
                    _existingProduct.CategoryName = categoryName;
                    _existingProduct.SupplierID = supplierId;
                    _existingProduct.SupplierName = supplierName;
                    _existingProduct.CostPrice = _numCost.Value;
                    _existingProduct.SellingPrice = _numPrice.Value;
                    _existingProduct.ReorderLevel = (int)_numReorder.Value;
                    _existingProduct.ImagePath = finalImagePath;

                    _inventoryService.UpdateProduct(_existingProduct);
                    MessageBox.Show($"Product '{name}' updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (DuplicateSkuException ex)
            {
                MessageBox.Show($"SKU Uniqueness Invariant Violation:\n\n{ex.Message}", "Duplicate SKU", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _txtSku.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save product:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
