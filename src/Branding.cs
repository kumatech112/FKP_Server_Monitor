using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal static class Branding
    {
        public static Icon CreateIcon()
        {
            using (Stream stream = typeof(Branding).Assembly.GetManifestResourceStream("ServerBotTracker.Branding.Icon"))
            using (var icon = new Icon(stream))
                return (Icon)icon.Clone();
        }

        public static void ApplyTo(Form form)
        {
            Icon icon = CreateIcon();
            form.Icon = icon;
            form.ShowIcon = true;
            form.Disposed += delegate { icon.Dispose(); };
        }

        public static PictureBox CreateLogoBox(Size size)
        {
            Image logo;
            using (Stream stream = typeof(Branding).Assembly.GetManifestResourceStream("ServerBotTracker.Branding.Logo"))
            using (Image original = Image.FromStream(stream))
                logo = new Bitmap(original);
            var box = new PictureBox { Name = "ProductLogo", Size = size, SizeMode = PictureBoxSizeMode.Zoom, Image = logo, BackColor = Color.Transparent, TabStop = false };
            box.Disposed += delegate { logo.Dispose(); };
            return box;
        }
    }
}
