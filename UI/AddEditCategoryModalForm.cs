using System;
using System.Drawing;
using System.Windows.Forms;
using Inventory_Management_System.BusinessLogic;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Modal dialog for adding new inventory categories or updating existing ones.
    /// Provides real-time field validation, uniqueness checks, and relationship context.
    /// </summary>
    public class AddEditCategoryModalForm : Form
    {
        private readonly IInventoryService _inventoryService;
        private readonly Category? _existingCategory;

        private TextBox _txtName = null!;
        private TextBox _txtDescription = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public Category? SavedCategory { get; private set; }

        public AddEditCategoryModalForm(IInventoryService inventoryService, Category? existingCategory = null)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _existingCategory = existingCategory;

            InitializeForm();
            PopulateData();
        }

        private void InitializeForm()
        {
            Text = _existingCategory == null ? "Add New Category" : "Edit Category Item";
            Size = new Size(540, 440);
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
                Text = _existingCategory == null ? "Register New Category" : $"Edit Category: {_existingCategory.CategoryName}",
                Font = Theme.FontHeadingMd,
                ForeColor = Theme.TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            panel.Controls.Add(lblTitle);
            y += 28;

            var lblSub = new Label
            {
                Text = "Organize catalog items, manage taxonomy, and maintain relational invariants",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            };
            panel.Controls.Add(lblSub);
            y += 30;

            var card = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(476, 260),
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

            if (_existingCategory != null)
            {
                var lblInfo = new Label
                {
                    Text = $"🏷️ Category ID: #{_existingCategory.CategoryID}  •  Linked Products: {_existingCategory.ProductCount} item(s)",
                    Font = Theme.FontCaptionBold,
                    ForeColor = Theme.Primary,
                    Location = new Point(16, cy),
                    AutoSize = true
                };
                card.Controls.Add(lblInfo);
                cy += 28;
            }

            // Category Name
            var lblName = new Label
            {
                Text = "Category Name *",
                Font = Theme.FontHeadingSm,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(lblName);
            cy += 24;

            _txtName = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(440, 30),
                Font = Theme.FontBody,
                PlaceholderText = "e.g., Electronics, Perishables, Office Supplies..."
            };
            card.Controls.Add(_txtName);
            cy += 40;

            // Description
            var lblDesc = new Label
            {
                Text = "Description (Optional)",
                Font = Theme.FontHeadingSm,
                ForeColor = Theme.TextDark,
                Location = new Point(16, cy),
                AutoSize = true
            };
            card.Controls.Add(lblDesc);
            cy += 24;

            _txtDescription = new TextBox
            {
                Location = new Point(16, cy),
                Size = new Size(440, 80),
                Font = Theme.FontBody,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Enter department usage or description..."
            };
            card.Controls.Add(_txtDescription);

            // Action Buttons
            int by = 350;
            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 38),
                Location = new Point(266, by)
            };
            Theme.ApplyFlatButton(_btnCancel, ColorTranslator.FromHtml("#E2E8F0"), Theme.TextDark);
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            panel.Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = _existingCategory == null ? "Save Category" : "Update Category",
                Size = new Size(140, 38),
                Location = new Point(386, by)
            };
            Theme.ApplyFlatButton(_btnSave, Theme.Primary, Color.White);
            _btnSave.Click += OnSaveClick;
            panel.Controls.Add(_btnSave);

            Controls.Add(panel);
        }

        private void PopulateData()
        {
            if (_existingCategory != null)
            {
                _txtName.Text = _existingCategory.CategoryName;
                _txtDescription.Text = _existingCategory.Description ?? string.Empty;
            }
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            string desc = _txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please specify a Category Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            try
            {
                if (_existingCategory == null)
                {
                    // Create new category
                    var category = new Category
                    {
                        CategoryName = name,
                        Description = string.IsNullOrWhiteSpace(desc) ? null : desc
                    };

                    int newId = _inventoryService.CreateCategory(category);
                    category.CategoryID = newId;
                    SavedCategory = category;

                    MessageBox.Show($"Category '{name}' created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Update existing category
                    _existingCategory.CategoryName = name;
                    _existingCategory.Description = string.IsNullOrWhiteSpace(desc) ? null : desc;

                    _inventoryService.UpdateCategory(_existingCategory);
                    SavedCategory = _existingCategory;

                    MessageBox.Show($"Category '{name}' updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Category Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while saving the category:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
