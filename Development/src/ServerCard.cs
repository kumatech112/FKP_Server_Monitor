using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
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
        private readonly IList<long?> samples;
        private ServerResult result;
        private bool selected;
        public string ServerId { get { return server.Id; } }
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public event EventHandler SelectedByUser;
        public event EventHandler EditRequested;

        public ServerCard(ServerEntry entry, IList<long?> history)
        {
            server = entry; samples = history;
            Name = "ServerCard_" + entry.Id; Size = new Size(300, 236); Margin = new Padding(0, 0, 14, 14);
            BackColor = DashboardStyle.Background; Cursor = Cursors.Hand; TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AccessibleName = entry.Name;
        }

        public void UpdateResult(ServerResult value)
        {
            result = value; AccessibleDescription = value == null ? "ยังไม่ตรวจ" : value.Summary; Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            Focus(); base.OnClick(e); EventHandler handler = SelectedByUser; if (handler != null) handler(this, e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e); EventHandler handler = EditRequested; if (handler != null) handler(this, e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            if (e.KeyCode == Keys.Enter) { OnDoubleClick(EventArgs.Empty); e.Handled = true; }
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
            using (GraphicsPath outline = Rounded(new RectangleF(1, 1, width - 3, height - 3), 15))
            using (var fill = new SolidBrush(DashboardStyle.Surface))
            using (var border = new Pen(selected ? DashboardStyle.Blue : DashboardStyle.Border, selected ? 2 : 1))
            {
                e.Graphics.FillPath(fill, outline); e.Graphics.DrawPath(border, outline);
            }
            Color health = DashboardStyle.HealthColor(result);
            using (var dot = new SolidBrush(health)) e.Graphics.FillEllipse(dot, 18, 20, 9, 9);
            DrawText(e.Graphics, server.Name, 35, 15, width - 52, 26, 17, DashboardStyle.Ink, true);
            DrawText(e.Graphics, server.Host, 18, 43, width - 36, 23, 12, DashboardStyle.Muted, false);
            string latency = result != null && result.Ping != null && result.Ping.Milliseconds.HasValue ? result.Ping.Milliseconds.Value.ToString(CultureInfo.InvariantCulture) + " ms" : "—";
            DrawText(e.Graphics, latency, 18, 72, width - 36, 34, 28, health, true);
            DrawText(e.Graphics, result == null ? "ยังไม่ตรวจ" : result.Summary, 18, 108, width - 36, 24, 13, health, false);
            bool databaseEnabled = server.Database != null && server.Database.Enabled;
            string database = !databaseEnabled ? "SQL Server: ไม่ได้เปิดตรวจ" : result == null || result.Database == null ? "SQL Server: รอตรวจ" : result.Database.Success ? "SQL Server: ใช้งานได้" : "SQL Server: เชื่อมต่อไม่ได้";
            Color databaseColor = !databaseEnabled || result == null || result.Database == null ? DashboardStyle.Muted : result.Database.Success ? DashboardStyle.Green : DashboardStyle.Amber;
            DrawText(e.Graphics, database, 18, 133, width - 36, 22, 12, databaseColor, false);
            DrawTrend(e.Graphics, new RectangleF(18, 164, width - 36, Math.Max(20, height - 201)), health);
            string checkedAt = result == null ? "ยังไม่มีผลตรวจ" : "ตรวจล่าสุด " + result.CheckedAt.ToString("HH:mm:ss");
            DrawText(e.Graphics, checkedAt, 18, height - 30, width - 36, 20, 11, DashboardStyle.Muted, false);
        }

        private void DrawTrend(Graphics graphics, RectangleF area, Color color)
        {
            if (samples.Count == 0) { DrawText(graphics, "กราฟเวลาตอบกลับ Ping", area.X, area.Y, area.Width, area.Height, 11, DashboardStyle.Muted, false); return; }
            double maximum = Math.Max(1, samples.Where(item => item.HasValue).Select(item => (double)item.Value).DefaultIfEmpty(1).Max());
            using (var line = new Pen(color, 1.7f))
            using (var failed = new SolidBrush(DashboardStyle.Red))
            using (var point = new SolidBrush(color))
            {
                PointF? previous = null;
                for (int index = 0; index < samples.Count; index++)
                {
                    float x = area.Left + (samples.Count == 1 ? area.Width / 2 : area.Width * index / (samples.Count - 1));
                    if (!samples[index].HasValue) { graphics.FillEllipse(failed, x - 2, area.Bottom - 4, 4, 4); previous = null; continue; }
                    float y = area.Bottom - 3 - (float)(samples[index].Value / maximum) * (area.Height - 6);
                    var current = new PointF(x, y);
                    if (previous.HasValue) graphics.DrawLine(line, previous.Value, current);
                    else graphics.FillEllipse(point, x - 2, y - 2, 4, 4);
                    previous = current;
                }
            }
        }

        private static void DrawText(Graphics graphics, string value, float x, float y, float width, float height, float size, Color color, bool bold)
        {
            using (var font = new Font("Tahoma", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                graphics.DrawString(value, font, brush, new RectangleF(x, y, width, height), format);
        }

        private static GraphicsPath Rounded(RectangleF area, float radius)
        {
            var path = new GraphicsPath(); float diameter = radius * 2;
            path.AddArc(area.Left, area.Top, diameter, diameter, 180, 90);
            path.AddArc(area.Right - diameter, area.Top, diameter, diameter, 270, 90);
            path.AddArc(area.Right - diameter, area.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(area.Left, area.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
        }
    }
}
