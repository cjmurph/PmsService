using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PlexServiceTray
{
    /// <summary>
    /// Flat context menu that follows the light/dark setting of the tray app
    /// </summary>
    internal class TrayMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _back;
        private readonly Color _fore;
        private readonly Color _disabled;
        private readonly Color _hover;
        private readonly Color _border;
        private readonly Color _separator;

        public TrayMenuRenderer(bool dark) : base(new TrayColorTable(dark))
        {
            RoundedEdges = false;
            _back = dark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
            _fore = dark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(26, 26, 26);
            _disabled = dark ? Color.FromArgb(150, 150, 150) : Color.FromArgb(120, 120, 120);
            _hover = dark ? Color.FromArgb(61, 61, 61) : Color.FromArgb(232, 232, 232);
            _border = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(214, 214, 214);
            _separator = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(224, 224, 224);
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(System.IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// Rounded corners like the native Windows 11 menus, ignored on older Windows versions
        /// </summary>
        public static void RoundCorners(ToolStripDropDown dropDown)
        {
            dropDown.Opened += (_, _) =>
            {
                try
                {
                    var preference = 3; // DWMWCP_ROUNDSMALL
                    DwmSetWindowAttribute(dropDown.Handle, 33, ref preference, sizeof(int));
                }
                catch
                {
                    // not supported
                }
            };
        }

        public Color BackColor => _back;
        public Color ForeColor => _fore;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(_back);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(_border);
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(_back);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using var path = RoundedRect(rect, 4);
            using var brush = new SolidBrush(_hover);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? (e.Item.Tag as Color?) ?? _fore : _disabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _fore;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(_separator);
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private class TrayColorTable : ProfessionalColorTable
        {
            private readonly Color _back;

            public TrayColorTable(bool dark)
            {
                UseSystemColors = false;
                _back = dark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
            }

            public override Color ToolStripDropDownBackground => _back;
            public override Color ImageMarginGradientBegin => _back;
            public override Color ImageMarginGradientMiddle => _back;
            public override Color ImageMarginGradientEnd => _back;
        }
    }
}
