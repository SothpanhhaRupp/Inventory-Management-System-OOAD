using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Inventory_Management_System.UI
{
    /// <summary>
    /// Custom Card control displaying key metric telemetry, prominent numerical values,
    /// and contextual status indicators.
    /// </summary>
    public class KpiCard : Panel
    {
        private readonly Label _lblTitle;
        private readonly Label _lblValue;
        private readonly Label _lblSubtitle;
        private readonly Label _lblIcon;
        private Color _accentColor = Theme.Primary;

        public KpiCard()
        {
            DoubleBuffered = true;
            BackColor = Theme.CardBg;
            Size = new Size(240, 115);
            Padding = new Padding(16, 12, 16, 12);
            Margin = new Padding(8);

            _lblIcon = new Label
            {
                Text = "📦",
                Font = new Font("Segoe UI Emoji", 20f, FontStyle.Regular),
                ForeColor = _accentColor,
                AutoSize = true,
                Location = new Point(190, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            _lblTitle = new Label
            {
                Text = "METRIC TITLE",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(16, 14)
            };

            _lblValue = new Label
            {
                Text = "0",
                Font = Theme.FontKpiNumber,
                ForeColor = Theme.TextDark,
                AutoSize = true,
                Location = new Point(14, 34)
            };

            _lblSubtitle = new Label
            {
                Text = "Real-time sync",
                Font = Theme.FontCaption,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(16, 82)
            };

            Controls.Add(_lblIcon);
            Controls.Add(_lblTitle);
            Controls.Add(_lblValue);
            Controls.Add(_lblSubtitle);
        }

        public void SetData(string title, string value, string subtitle, Color accentColor, string iconSymbol = "📊")
        {
            _accentColor = accentColor;
            _lblTitle.Text = title.ToUpperInvariant();
            _lblValue.Text = value;
            _lblSubtitle.Text = subtitle;
            _lblIcon.Text = iconSymbol;
            _lblIcon.ForeColor = accentColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw subtle border
            using var borderPen = new Pen(Theme.CardBorder, 1f);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            g.DrawRectangle(borderPen, rect);

            // Draw left accent bar
            using var accentBrush = new SolidBrush(_accentColor);
            g.FillRectangle(accentBrush, 0, 0, 5, Height);
        }
    }
}
