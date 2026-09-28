using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using Inventory_Management_System.Models;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Interactive Print Preview and Invoice Dispatch Dialog.
    /// Provides pixel-perfect vector rendering matching the custom modern yellow/charcoal brand template,
    /// with live zooming, company logo embedding, and direct PDF / physical printer output.
    /// </summary>
    public class InvoicePreviewForm : Form
    {
        private readonly Invoice _invoice;
        private readonly PrintDocument _printDocument;
        private PrintPreviewControl _previewControl = null!;
        private double _zoomFactor = 1.0;

        public InvoicePreviewForm(Invoice invoice)
        {
            _invoice = invoice ?? throw new ArgumentNullException(nameof(invoice));
            _printDocument = new PrintDocument();
            _printDocument.DocumentName = $"Invoice_{_invoice.InvoiceNumber}";
            _printDocument.DefaultPageSettings.Margins = new Margins(45, 45, 45, 45);
            _printDocument.PrintPage += OnPrintPage;

            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            Text = $"Invoice Viewer - #{_invoice.InvoiceNumber}";
            Size = new Size(950, 900);
            MinimumSize = new Size(780, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ColorTranslator.FromHtml("#F1F5F9");
            Font = Theme.FontBody;

            // 1. Top Modern Action Toolbar
            var toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Color.White,
                Padding = new Padding(16, 10, 16, 10)
            };
            toolbarPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Theme.CardBorder, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, toolbarPanel.Width - 1, toolbarPanel.Height - 1);
            };

            var flowLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var btnPrint = new Button
            {
                Text = "🖨️  Print Invoice...",
                Size = new Size(140, 36),
                AutoSize = true,
                Margin = new Padding(0, 0, 8, 0)
            };
            Theme.ApplyFlatButton(btnPrint, Theme.Primary, Color.White);
            btnPrint.Click += OnPrintClick;
            flowLeft.Controls.Add(btnPrint);

            var btnQuickPdf = new Button
            {
                Text = "📄  Quick PDF / Direct Print",
                Size = new Size(180, 36),
                AutoSize = true,
                Margin = new Padding(0, 0, 8, 0)
            };
            Theme.ApplyFlatButton(btnQuickPdf, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnQuickPdf.Click += OnQuickPdfClick;
            flowLeft.Controls.Add(btnQuickPdf);

            var flowRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var btnZoomIn = new Button
            {
                Text = "🔍  Zoom +",
                Size = new Size(95, 36),
                AutoSize = true,
                Margin = new Padding(4, 0, 4, 0)
            };
            Theme.ApplyFlatButton(btnZoomIn, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnZoomIn.Click += (s, e) => ChangeZoom(0.15);
            flowRight.Controls.Add(btnZoomIn);

            var btnZoomOut = new Button
            {
                Text = "🔍  Zoom -",
                Size = new Size(95, 36),
                AutoSize = true,
                Margin = new Padding(4, 0, 4, 0)
            };
            Theme.ApplyFlatButton(btnZoomOut, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnZoomOut.Click += (s, e) => ChangeZoom(-0.15);
            flowRight.Controls.Add(btnZoomOut);

            var btnFit = new Button
            {
                Text = "Fit Page",
                Size = new Size(80, 36),
                AutoSize = true,
                Margin = new Padding(4, 0, 8, 0)
            };
            Theme.ApplyFlatButton(btnFit, ColorTranslator.FromHtml("#F1F5F9"), Theme.TextDark);
            btnFit.Click += (s, e) => { _zoomFactor = 1.0; _previewControl.AutoZoom = true; };
            flowRight.Controls.Add(btnFit);

            var btnClose = new Button
            {
                Text = "✖  Close",
                Size = new Size(85, 36),
                AutoSize = true,
                Margin = new Padding(4, 0, 0, 0)
            };
            Theme.ApplyFlatButton(btnClose, ColorTranslator.FromHtml("#E2E8F0"), Theme.TextDark);
            btnClose.Click += (s, e) => Close();
            flowRight.Controls.Add(btnClose);

            toolbarPanel.Controls.Add(flowRight);
            toolbarPanel.Controls.Add(flowLeft);

            // 2. High-DPI PrintPreviewControl Container
            _previewControl = new PrintPreviewControl
            {
                Dock = DockStyle.Fill,
                Document = _printDocument,
                AutoZoom = true,
                BackColor = ColorTranslator.FromHtml("#CBD5E1"),
                UseAntiAlias = true
            };

            Controls.Add(_previewControl);
            Controls.Add(toolbarPanel);
        }

        private void ChangeZoom(double delta)
        {
            _previewControl.AutoZoom = false;
            _zoomFactor = Math.Clamp(_zoomFactor + delta, 0.4, 2.5);
            _previewControl.Zoom = _zoomFactor;
        }

        private void OnPrintClick(object? sender, EventArgs e)
        {
            using var printDialog = new PrintDialog
            {
                Document = _printDocument,
                UseEXDialog = true,
                AllowSomePages = false
            };

            if (printDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    _printDocument.Print();
                    MessageBox.Show("Invoice document sent to printer successfully.", "Print Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to print invoice: {ex.Message}", "Printing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnQuickPdfClick(object? sender, EventArgs e)
        {
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase))
                    {
                        _printDocument.PrinterSettings.PrinterName = printer;
                        break;
                    }
                }

                _printDocument.Print();
                MessageBox.Show("Invoice printed / saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception)
            {
                OnPrintClick(sender, e);
            }
        }

        /// <summary>
        /// Locates and loads logo.png from project Resources or runtime directories safely.
        /// </summary>
        private static Image? LoadLogoImage()
        {
            try
            {
                string[] candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "logo.png"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo.png"),
                    Path.Combine(Directory.GetCurrentDirectory(), "logo.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.jpg"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "logo.jpg")
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Image.FromStream(fs);
                    }
                }
            }
            catch
            {
                // Fall back to programmatic vector branding if file access is restricted
            }
            return null;
        }

        /// <summary>
        /// Renders the exact custom invoice layout requested:
        /// - Top left Brand Logo & Name
        /// - Split horizontal yellow ribbon with bold condensed INVOICE title
        /// - Two-column metadata (Invoice to vs Invoice# and Date)
        /// - Dark slate header table with clean boxed ledger frame
        /// - Terms & conditions and payment info on left
        /// - Subtotal, tax, and highlighted solid yellow Total box on right
        /// - Yellow bottom accent bar with Authorised Sign block and contact footer
        /// </summary>
        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            if (g == null) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Page dimensions
            var bounds = e.MarginBounds;
            int left = Math.Max(bounds.Left, 48);
            int top = Math.Max(bounds.Top, 45);
            int width = Math.Min(bounds.Width, 720);
            int right = left + width;

            // Brand Color Palette matching user's template
            var cYellow = ColorTranslator.FromHtml("#FDB813");    // Vibrant Amber Yellow
            var cDarkHead = ColorTranslator.FromHtml("#2C3038");  // Table Header Dark Charcoal
            var cTextDark = ColorTranslator.FromHtml("#1F242E");  // Primary Black/Charcoal Text
            var cTextMuted = ColorTranslator.FromHtml("#6C757D"); // Secondary Muted Grey Text
            var cBorder = ColorTranslator.FromHtml("#CBD5E1");    // Table & Card Outer Border
            var cLineLight = ColorTranslator.FromHtml("#ECEEF2");  // Row Divider Line

            using var fontBrand = new Font("Segoe UI", 16f, FontStyle.Bold);
            using var fontTagline = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var fontInvoiceTitle = new Font("Segoe UI", 28f, FontStyle.Bold);
            using var fontSectionTitle = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var fontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            using var fontBodyBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fontMetaBold = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var fontTableHead = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fontTermsHead = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var fontTermsBody = new Font("Segoe UI", 8f, FontStyle.Regular);
            using var fontPayment = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var fontTotalBox = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var fontSign = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var fontFooter = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            using var bDark = new SolidBrush(cTextDark);
            using var bMuted = new SolidBrush(cTextMuted);
            using var bWhite = new SolidBrush(Color.White);
            using var bYellow = new SolidBrush(cYellow);
            using var bHead = new SolidBrush(cDarkHead);
            using var pYellow = new Pen(cYellow, 2.5f);
            using var pBorder = new Pen(cBorder, 1f);
            using var pLineLight = new Pen(cLineLight, 1f);

            int y = top;

            // ==========================================
            // 1. TOP HEADER: LOGO & BRAND NAME
            // ==========================================
            int logoSize = 48;
            using (var logoImg = LoadLogoImage())
            {
                if (logoImg != null)
                {
                    // Render user's logo.png smoothly with aspect ratio maintained
                    float aspect = (float)logoImg.Width / Math.Max(1, logoImg.Height);
                    int drawW = aspect >= 1f ? logoSize : (int)(logoSize * aspect);
                    int drawH = aspect <= 1f ? logoSize : (int)(logoSize / aspect);
                    int offY = (logoSize - drawH) / 2;
                    g.DrawImage(logoImg, new Rectangle(left, y + offY, drawW, drawH));
                }
                else
                {
                    // Fallback geometric logo icon matching template style
                    using var path = new GraphicsPath();
                    path.AddPolygon(new[]
                    {
                        new Point(left + 20, y + 2),
                        new Point(left + 40, y + 22),
                        new Point(left + 20, y + 42),
                        new Point(left, y + 22)
                    });
                    using var pIcon = new Pen(cTextDark, 3f);
                    g.DrawPath(pIcon, path);
                }
            }

            int brandTextX = left + logoSize + 14;
            g.DrawString(string.IsNullOrWhiteSpace(_invoice.CompanyName) ? "Brand Name" : _invoice.CompanyName, fontBrand, bDark, brandTextX, y + 2);
            g.DrawString(string.IsNullOrWhiteSpace(_invoice.CompanyTagline) ? "TAGLINE SPACE HERE" : _invoice.CompanyTagline.ToUpperInvariant(), fontTagline, bMuted, brandTextX, y + 28);

            y += logoSize + 22;

            // ==========================================
            // 2. SPLIT YELLOW RIBBON & "INVOICE" TITLE
            // ==========================================
            int ribbonH = 30;
            string titleText = "INVOICE";
            var titleSize = g.MeasureString(titleText, fontInvoiceTitle);
            int titleW = (int)titleSize.Width;
            int titleH = (int)titleSize.Height;

            // Position title towards the right side of the split ribbon
            int titleX = right - titleW - 90;
            int titleY = y - (titleH - ribbonH) / 2 - 2;

            // Left yellow ribbon extending up to title
            int leftRibbonW = titleX - left - 18;
            if (leftRibbonW > 0)
            {
                g.FillRectangle(bYellow, new Rectangle(left, y, leftRibbonW, ribbonH));
            }

            // Bold "INVOICE" text
            g.DrawString(titleText, fontInvoiceTitle, bDark, titleX, titleY);

            // Right yellow ribbon from title to right margin
            int rightRibbonX = titleX + titleW + 12;
            int rightRibbonW = right - rightRibbonX;
            if (rightRibbonW > 0)
            {
                g.FillRectangle(bYellow, new Rectangle(rightRibbonX, y, rightRibbonW, ribbonH));
            }

            y += ribbonH + 32;

            // ==========================================
            // 3. TWO-COLUMN METADATA SECTION
            // ==========================================
            // Left Column: "Invoice to:"
            g.DrawString("Invoice to:", fontSectionTitle, bDark, left, y);
            int metaLeftY = y + 24;
            g.DrawString(_invoice.CustomerOrSupplier, fontMetaBold, bDark, left, metaLeftY);
            metaLeftY += 20;
            g.DrawString(_invoice.CustomerAddressLine1, fontBody, bMuted, left, metaLeftY);
            metaLeftY += 18;
            g.DrawString(_invoice.CustomerAddressLine2, fontBody, bMuted, left, metaLeftY);
            metaLeftY += 18;
            g.DrawString(_invoice.CustomerAddressLine3, fontBody, bMuted, left, metaLeftY);

            // Right Column: Invoice# and Date
            int metaRightLabelX = right - 220;
            int metaRightY = y + 24;
            using var sfFar = new StringFormat { Alignment = StringAlignment.Far };

            // Invoice#
            g.DrawString("Invoice#", fontMetaBold, bDark, metaRightLabelX, metaRightY);
            g.DrawString(_invoice.InvoiceNumber, fontBody, bDark, right, metaRightY, sfFar);
            metaRightY += 24;

            // Date
            g.DrawString("Date", fontMetaBold, bDark, metaRightLabelX, metaRightY);
            g.DrawString(_invoice.IssueDate.ToString("MM / dd / yyyy"), fontBody, bDark, right, metaRightY, sfFar);

            y = Math.Max(metaLeftY + 36, metaRightY + 40);

            // ==========================================
            // 4. ITEM DESCRIPTION TABLE & FRAME
            // ==========================================
            int tableFrameH = 260; // Clean box framing height matching user's image
            var tableFrameRect = new Rectangle(left, y, width, tableFrameH);
            g.DrawRectangle(pBorder, tableFrameRect);

            // Table Header Bar (Dark Slate)
            int tableHeaderH = 34;
            var tableHeadRect = new Rectangle(left, y, width, tableHeaderH);
            g.FillRectangle(bHead, tableHeadRect);

            // Column Widths:
            // SL. (55), Item Description (335), Price (110), Qty. (80), Total (140)
            int colSlW = 55;
            int colDescW = 335;
            int colPriceW = 110;
            int colQtyW = 80;
            int colTotalW = width - (colSlW + colDescW + colPriceW + colQtyW);

            int xSl = left;
            int xDesc = xSl + colSlW;
            int xPrice = xDesc + colDescW;
            int xQty = xPrice + colPriceW;
            int xTotal = xQty + colQtyW;

            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var sfNear = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            using var sfFarCell = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            // Draw Header Text
            g.DrawString("SL.", fontTableHead, bWhite, new Rectangle(xSl, y, colSlW, tableHeaderH), sfCenter);
            g.DrawString("Item Description", fontTableHead, bWhite, new Rectangle(xDesc + 8, y, colDescW, tableHeaderH), sfNear);
            g.DrawString("Price", fontTableHead, bWhite, new Rectangle(xPrice, y, colPriceW - 14, tableHeaderH), sfFarCell);
            g.DrawString("Qty.", fontTableHead, bWhite, new Rectangle(xQty, y, colQtyW, tableHeaderH), sfCenter);
            g.DrawString("Total", fontTableHead, bWhite, new Rectangle(xTotal, y, colTotalW - 18, tableHeaderH), sfFarCell);

            // Table Item Rows
            int rowH = 42;
            int rowY = y + tableHeaderH;

            for (int i = 0; i < _invoice.Items.Count; i++)
            {
                var item = _invoice.Items[i];

                // Row bottom divider line
                g.DrawLine(pLineLight, left, rowY + rowH, right, rowY + rowH);

                // Row cells
                g.DrawString((i + 1).ToString(), fontBodyBold, bDark, new Rectangle(xSl, rowY, colSlW, rowH), sfCenter);
                g.DrawString(item.ProductName, fontBody, bDark, new Rectangle(xDesc + 8, rowY, colDescW, rowH), sfNear);
                g.DrawString($"${item.UnitPrice:N2}", fontBody, bDark, new Rectangle(xPrice, rowY, colPriceW - 14, rowH), sfFarCell);
                g.DrawString($"{item.Quantity:N0}", fontBody, bDark, new Rectangle(xQty, rowY, colQtyW, rowH), sfCenter);
                g.DrawString($"${item.TotalAmount:N2}", fontBodyBold, bDark, new Rectangle(xTotal, rowY, colTotalW - 18, rowH), sfFarCell);

                rowY += rowH;
            }

            y += tableFrameH + 24;

            // ==========================================
            // 5. SUMMARY & NOTES SECTION (BELOW TABLE)
            // ==========================================
            // Left Column: Thank You, Terms, Payment Info
            int leftNotesY = y;
            g.DrawString("Thank you for your business", fontSectionTitle, bDark, left, leftNotesY);
            leftNotesY += 26;

            g.DrawString("Terms & Conditions", fontTermsHead, bDark, left, leftNotesY);
            leftNotesY += 16;
            var termsRect = new RectangleF(left, leftNotesY, 320, 36);
            g.DrawString(_invoice.TermsConditions, fontTermsBody, bMuted, termsRect);
            leftNotesY += 38;

            g.DrawString("Payment Info:", fontTermsHead, bDark, left, leftNotesY);
            leftNotesY += 18;
            g.DrawString($"Account #:      {_invoice.AccountNumber}", fontPayment, bDark, left, leftNotesY);
            leftNotesY += 16;
            g.DrawString($"A/C Name:       {_invoice.AccountName}", fontPayment, bDark, left, leftNotesY);
            leftNotesY += 16;
            g.DrawString($"Bank Details:   {_invoice.BankDetails}", fontPayment, bDark, left, leftNotesY);

            // Right Column: Sub Total, Tax, and Highlighted Solid Yellow Total Box
            int rightSummaryY = y;
            int summaryLabelX = right - 240;

            // Sub Total
            g.DrawString("Sub Total:", fontBodyBold, bDark, summaryLabelX, rightSummaryY);
            g.DrawString($"${_invoice.Subtotal:N2}", fontBodyBold, bDark, right - 12, rightSummaryY, sfFar);
            rightSummaryY += 24;

            // Tax
            g.DrawString("Tax:", fontBodyBold, bDark, summaryLabelX, rightSummaryY);
            g.DrawString($"{_invoice.TaxRate:P2}", fontBody, bDark, right - 12, rightSummaryY, sfFar);
            rightSummaryY += 30;

            // Highlighted Total Yellow Box
            int totalBoxH = 36;
            int totalBoxW = 240;
            int totalBoxX = right - totalBoxW;
            var totalBoxRect = new Rectangle(totalBoxX, rightSummaryY, totalBoxW, totalBoxH);
            g.FillRectangle(bYellow, totalBoxRect);

            // Inside Total Box Text
            g.DrawString("Total:", fontTotalBox, bDark, new Rectangle(totalBoxX + 16, rightSummaryY, 90, totalBoxH), sfNear);
            g.DrawString($"${_invoice.GrandTotal:N2}", fontTotalBox, bDark, new Rectangle(right - 140, rightSummaryY, 126, totalBoxH), sfFarCell);

            // ==========================================
            // 6. BOTTOM FOOTER & AUTHORISED SIGN
            // ==========================================
            int footerY = bounds.Bottom - 50;

            // Yellow bottom accent line across the left portion
            int yellowLineW = width - 240;
            g.DrawLine(pYellow, left, footerY, left + yellowLineW, footerY);

            // Authorised Sign Block on the right
            int signBlockX = right - 200;
            int signLineY = footerY;
            using var pSignLine = new Pen(cTextDark, 1.2f);
            g.DrawLine(pSignLine, signBlockX, signLineY, right - 40, signLineY);

            // Small yellow dash to the right of signature line matching template
            g.DrawLine(pYellow, right - 32, signLineY, right, signLineY);

            // Text "Authorised Sign" centered under signature line
            int signTextCenterX = (signBlockX + right - 40) / 2;
            var signTextSize = g.MeasureString("Authorised Sign", fontSign);
            g.DrawString("Authorised Sign", fontSign, bDark, signTextCenterX - (signTextSize.Width / 2), signLineY + 6);

            // Bottom Contact line below yellow bar
            string contactInfo = $"{_invoice.Phone}     |     {_invoice.Address}     |     {_invoice.Website}";
            g.DrawString(contactInfo, fontFooter, bDark, left + 4, footerY + 18);

            e.HasMorePages = false;
        }
    }
}
