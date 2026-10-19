using System.Drawing;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(243, 246, 251);
        public static readonly Color Ink = Color.FromArgb(30, 45, 66);
        public static readonly Color Blue = Color.FromArgb(37, 99, 235);
        public static readonly Color Green = Color.FromArgb(20, 120, 75);
        public static readonly Color Red = Color.FromArgb(180, 45, 50);

        public static Button Button(string text, bool primary)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(105, 36),
                Padding = new Padding(12, 3, 12, 3),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Blue : Color.White,
                ForeColor = primary ? Color.White : Ink,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = primary ? Blue : Color.FromArgb(212, 222, 235);
            return button;
        }

        public static Label Label(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Ink, Margin = new Padding(0, 8, 12, 0) };
        }

        public static void ApplyDialog(Form form, string title, Size size)
        {
            Branding.ApplyTo(form);
            form.Text = title;
            form.ClientSize = size;
            form.Font = new Font("Tahoma", 10F);
            form.ForeColor = Ink;
            form.BackColor = Background;
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }
    }
}
