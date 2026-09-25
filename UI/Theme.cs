using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Centralized UI design tokens and visual styling utility.
    /// Implements modern Dark-Navy / Flat Slate aesthetics, anti-aliased controls,
    /// and consistent enterprise typography.
    /// </summary>
    public static class Theme
    {
        // Color Palette
        public static readonly Color SidebarBg     = ColorTranslator.FromHtml("#1E293B"); // Slate 800
        public static readonly Color SidebarHover  = ColorTranslator.FromHtml("#334155"); // Slate 700
        public static readonly Color SidebarActive = ColorTranslator.FromHtml("#0F172A"); // Slate 900
        public static readonly Color CanvasBg      = ColorTranslator.FromHtml("#F8FAFC"); // Slate 50
        public static readonly Color CardBg        = Color.White;
        public static readonly Color CardBorder    = ColorTranslator.FromHtml("#E2E8F0"); // Slate 200
        public static readonly Color HeaderBg      = Color.White;

        // Accent & State Colors
        public static readonly Color Primary       = ColorTranslator.FromHtml("#3B82F6"); // Blue 500
        public static readonly Color PrimaryHover  = ColorTranslator.FromHtml("#2563EB"); // Blue 600
        public static readonly Color Danger        = ColorTranslator.FromHtml("#EF4444"); // Red 500
        public static readonly Color DangerLight   = ColorTranslator.FromHtml("#FEE2E2"); // Red 100
        public static readonly Color DangerDark    = ColorTranslator.FromHtml("#991B1B"); // Red 800
        public static readonly Color Warning       = ColorTranslator.FromHtml("#F59E0B"); // Amber 500
        public static readonly Color WarningLight  = ColorTranslator.FromHtml("#FEF3C7"); // Amber 100
        public static readonly Color WarningDark   = ColorTranslator.FromHtml("#92400E"); // Amber 800
        public static readonly Color Success       = ColorTranslator.FromHtml("#10B981"); // Emerald 500
        public static readonly Color SuccessLight  = ColorTranslator.FromHtml("#D1FAE5"); // Emerald 100
        public static readonly Color SuccessDark   = ColorTranslator.FromHtml("#065F46"); // Emerald 800

        // Typography Colors
        public static readonly Color TextDark      = ColorTranslator.FromHtml("#0F172A"); // Slate 900
        public static readonly Color TextMuted     = ColorTranslator.FromHtml("#64748B"); // Slate 500
        public static readonly Color TextLight     = ColorTranslator.FromHtml("#F8FAFC"); // Slate 50

        // Typography
        public static readonly Font FontHeadingLg  = new Font("Segoe UI", 16f, FontStyle.Bold);
        public static readonly Font FontHeadingMd  = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font FontHeadingSm  = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font FontKpiNumber  = new Font("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font FontBody       = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font FontBodyBold   = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontCaption    = new Font("Segoe UI", 8.5f, FontStyle.Regular);

        /// <summary>
        /// Applies modern flat styling to standard WinForms buttons.
        /// </summary>
        public static void ApplyFlatButton(Button btn, Color bg, Color fg, int borderRadius = 6)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.Font = FontBodyBold;
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Padding(12, 6, 12, 6);

            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = ControlPaint.Light(bg, 0.15f);
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = bg;
            };
        }

        /// <summary>
        /// Styles DataGridView for modern enterprise clarity with clean alternating rows and headers.
        /// </summary>
        public static void ApplyModernGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = ColorTranslator.FromHtml("#F1F5F9");
            dgv.RowHeadersVisible = false;
            dgv.EnableHeadersVisualStyles = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.RowTemplate.Height = 42;

            // Column Header Styling
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#475569");
            dgv.ColumnHeadersDefaultCellStyle.Font = FontHeadingSm;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            dgv.ColumnHeadersHeight = 40;

            // Default Cell Styling
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.ForeColor = TextDark;
            dgv.DefaultCellStyle.Font = FontBody;
            dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EFF6FF");
            dgv.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#1D4ED8");
            dgv.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            // Alternating Row Styling
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
        }

        /// <summary>
        /// Loads the standard application logo from Resources/logo.jpg with multi-path resolution
        /// and non-locking memory stream caching.
        /// </summary>
        public static Image? LoadLogoImage()
        {
            string[] candidatePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.jpg"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "logo.jpg"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.jpg"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logo.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logo.jpg")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        using var ms = new MemoryStream(bytes);
                        return Image.FromStream(ms);
                    }
                    catch
                    {
                        // Continue checking candidates
                    }
                }
            }
            return null;
        }
    }
}
