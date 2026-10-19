using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal static class DashboardStyle
    {
        public static readonly Color Background = Color.FromArgb(20, 23, 27);
        public static readonly Color Surface = Color.FromArgb(34, 39, 44);
        public static readonly Color Border = Color.FromArgb(54, 62, 70);
        public static readonly Color Ink = Color.FromArgb(237, 242, 247);
        public static readonly Color Muted = Color.FromArgb(153, 165, 179);
        public static readonly Color Blue = Color.FromArgb(55, 139, 237);
        public static readonly Color Green = Color.FromArgb(46, 207, 146);
        public static readonly Color Amber = Color.FromArgb(235, 180, 75);
        public static readonly Color Red = Color.FromArgb(248, 105, 115);

        public static void StyleButton(Button button, bool primary)
        {
            button.BackColor = primary ? Blue : Surface;
            button.ForeColor = Ink;
            button.FlatAppearance.BorderColor = primary ? Blue : Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 65, 82);
        }

        public static Color HealthColor(ServerResult result)
        {
            if (result == null || result.Health == "pending") return Muted;
            return result.Health == "healthy" ? Green : result.Health == "warning" ? Amber : Red;
        }
    }

        public sealed class ServerCard : Control
    {
        private readonly ServerEntry server;
        private ServerResult result;
        private bool selected;

        public string ServerId
        {
            get { return server.Id; }
        }

        public string StatusText
        {
            get
            {
                if (result == null || result.Health == "pending") return "ยังไม่ตรวจ";
                if (result.Health == "healthy") return "ปกติ";
                if (result.Health == "warning") return "มีปัญหา";
                return "เชื่อมต่อไม่ได้";
            }
        }

        public bool Selected
        {
            get { return selected; }
            set
            {
                selected = value;
                Invalidate();
            }
        }

        public event EventHandler SelectedByUser;
        public event EventHandler EditRequested;

        public ServerCard(ServerEntry entry)
        {
            server = entry;
            Name = "ServerCard_" + entry.Id;
            Size = new Size(196, 84);
            Margin = new Padding(0, 0, 8, 8);
            BackColor = DashboardStyle.Background;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = entry.Name;
            AccessibleDescription = StatusText;
        }

        public void UpdateResult(ServerResult value)
        {
            result = value;
            AccessibleDescription = StatusText;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            base.OnClick(e);
            EventHandler handler = SelectedByUser;
            if (handler != null) handler(this, e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            EventHandler handler = EditRequested;
            if (handler != null) handler(this, e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Enter)
            {
                OnDoubleClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = e.Graphics.DpiX / 96f;
            e.Graphics.ScaleTransform(scale, scale);
            float width = ClientSize.Width / scale;
            float height = ClientSize.Height / scale;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath outline = Rounded(new RectangleF(1, 1, width - 3, height - 3), 9))
            using (var fill = new SolidBrush(DashboardStyle.Surface))
            using (var border = new Pen(selected || Focused ? DashboardStyle.Blue : DashboardStyle.Border, selected || Focused ? 2 : 1))
            {
                e.Graphics.FillPath(fill, outline);
                e.Graphics.DrawPath(border, outline);
            }
            Color health = DashboardStyle.HealthColor(result);
            using (var accent = new SolidBrush(health))
            {
                e.Graphics.FillRectangle(accent, 11, 16, 3, height - 32);
                e.Graphics.FillEllipse(accent, 24, 51, 7, 7);
            }
            DrawText(e.Graphics, server.Name, new RectangleF(24, 12, width - 38, 27), 15, DashboardStyle.Ink, true);
            DrawText(e.Graphics, StatusText, new RectangleF(38, 44, width - 52, 26), 14, health, false);
        }

        private static void DrawText(Graphics graphics, string value, RectangleF area, float size, Color color, bool bold)
        {
            using (var font = new Font("Tahoma", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                graphics.DrawString(value, font, brush, area, format);
            }
        }

        private static GraphicsPath Rounded(RectangleF area, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(area.Left, area.Top, diameter, diameter, 180, 90);
            path.AddArc(area.Right - diameter, area.Top, diameter, diameter, 270, 90);
            path.AddArc(area.Right - diameter, area.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(area.Left, area.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
