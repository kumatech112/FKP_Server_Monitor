using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using ServerBotTracker;

internal static class SetupVerification
{
    public static void Run(string root, Action<bool, string> assert)
    {
        string setupPath = Path.Combine(root, "ServerBotTracker-Setup.exe");
        Assembly setupAssembly = Assembly.LoadFrom(setupPath);
        VerifyBranding(typeof(MainForm).Assembly, Path.Combine(root, "ServerBotTracker.exe"), root, "application", assert);
        VerifyBranding(setupAssembly, setupPath, root, "setup", assert);
        assert(setupAssembly.GetName().Version == typeof(MainForm).Assembly.GetName().Version, "Installer and application versions match");
        VerifyInstallFlow(setupAssembly, root, assert);
        VerifyRetry(setupAssembly, root, assert);
    }

    private static void VerifyBranding(Assembly assembly, string executable, string root, string label, Action<bool, string> assert)
    {
        using (Stream resource = assembly.GetManifestResourceStream("ServerBotTracker.Branding.Icon"))
        using (Stream original = File.OpenRead(Path.Combine(root, "branding.ico")))
            assert(Hash(resource) == Hash(original), "Original supplied ICO is embedded unchanged in the " + label);
        using (Stream resource = assembly.GetManifestResourceStream("ServerBotTracker.Branding.Logo"))
        using (Stream original = File.OpenRead(Path.Combine(root, "branding.png")))
            assert(Hash(resource) == Hash(original), "Original supplied PNG is embedded unchanged in the " + label);
        using (Stream resource = assembly.GetManifestResourceStream("ServerBotTracker.Branding.Icon"))
        using (var expected = new Icon(resource, 32, 32))
        using (Icon actual = Icon.ExtractAssociatedIcon(executable))
        using (Bitmap expectedImage = RenderIcon(expected))
        using (Bitmap actualImage = RenderIcon(actual))
        {
            actualImage.Save(Path.Combine(root, label + "-file-icon.png"));
            expectedImage.Save(Path.Combine(root, label + "-expected-icon.png"));
            assert(SameImage(expectedImage, actualImage), "Windows EXE file icon matches the supplied robot for the " + label);
        }
    }

    private static string Hash(Stream stream)
    {
        if (stream == null) throw new InvalidDataException("A branding resource is missing");
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream));
    }

    private static bool SameImage(Bitmap first, Bitmap second)
    {
        if (first.Size != second.Size) return false;
        for (int y = 0; y < first.Height; y++)
            for (int x = 0; x < first.Width; x++)
                if (first.GetPixel(x, y).ToArgb() != second.GetPixel(x, y).ToArgb()) return false;
        return true;
    }

    private static Bitmap RenderIcon(Icon icon)
    {
        var image = new Bitmap(icon.Width, icon.Height);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
            graphics.DrawIcon(icon, new Rectangle(Point.Empty, image.Size));
        }
        return image;
    }

    private static Form CreateForm(Assembly setupAssembly, string root, Action install)
    {
        Type formType = setupAssembly.GetType("SetupForm", true);
        return (Form)Activator.CreateInstance(formType, new object[] { Path.Combine(root, "setup-ui-fixture"), install });
    }

    private static void VerifyInstallFlow(Assembly setupAssembly, string root, Action<bool, string> assert)
    {
        Exception failure = null;
        ApartmentState apartment = ApartmentState.Unknown;
        using (var release = new ManualResetEventSlim(false))
        using (Form form = CreateForm(setupAssembly, root, delegate
        {
            apartment = Thread.CurrentThread.GetApartmentState();
            if (!release.Wait(10000)) throw new TimeoutException("Setup fixture was not released");
        }))
        {
            form.Shown += delegate
            {
                Button action = (Button)form.Controls.Find("InstallAction", true).Single();
                try
                {
                    assert(form.Icon != null && form.Controls.Find("ProductLogo", true).Length == 1 && form.BackColor == Color.FromArgb(243, 246, 251), "Branded Thai setup preserves the existing light colors and displays the robot");
                    Capture(form, root, "setup-welcome.png");
                    action.PerformClick();
                    assert(!action.Enabled && !form.Controls.Find("CancelSetup", true).Single().Enabled && form.Controls.Find("InstallProgress", true).Single().Visible, "Installer shows progress and disables duplicate installation while its worker runs");
                    form.Close();
                    assert(!form.IsDisposed, "Closing during installation cannot interrupt file replacement");
                    Capture(form, root, "setup-installing.png");
                    release.Set();
                    WaitUntilEnabled(action);
                    assert(apartment == ApartmentState.STA && form.Controls.Find("InstallTitle", true).Single().Text == "ติดตั้งเรียบร้อยแล้ว", "Installer finishes on the UI thread after its dedicated STA worker completes");
                    var launch = (CheckBox)form.Controls.Find("LaunchAfterSetup", true).Single();
                    assert(launch.Visible && launch.Checked && action.Text == "เสร็จสิ้น", "Completion screen offers optional app launch without another confirmation dialog");
                    Capture(form, root, "setup-complete.png");
                    launch.Checked = false;
                    action.PerformClick();
                    assert(form.IsDisposed && !Directory.Exists(Path.Combine(root, "setup-ui-fixture")), "Finish without launch closes safely and the isolated UI fixture writes no installation files");
                }
                catch (Exception exception)
                {
                    failure = exception;
                    release.Set();
                    if (!form.IsDisposed) { WaitUntilEnabled(action); form.Close(); }
                }
            };
            Application.Run(form);
        }
        if (failure != null) throw failure;
    }

    private static void VerifyRetry(Assembly setupAssembly, string root, Action<bool, string> assert)
    {
        Exception failure = null;
        int attempts = 0;
        using (Form form = CreateForm(setupAssembly, root, delegate
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new IOException("ทดสอบข้อผิดพลาดในการติดตั้ง");
        }))
        {
            form.Shown += delegate
            {
                Button action = (Button)form.Controls.Find("InstallAction", true).Single();
                try
                {
                    action.PerformClick();
                    WaitUntilEnabled(action);
                    assert(action.Text == "ลองอีกครั้ง" && form.Controls.Find("InstallStatus", true).Single().Text.Contains("ทดสอบข้อผิดพลาด"), "An installation failure stays visible and permits retry without claiming success");
                    action.PerformClick();
                    WaitUntilEnabled(action);
                    assert(attempts == 2 && action.Text == "เสร็จสิ้น", "Retry completes after an installation failure");
                    ((CheckBox)form.Controls.Find("LaunchAfterSetup", true).Single()).Checked = false;
                    action.PerformClick();
                }
                catch (Exception exception) { failure = exception; if (!form.IsDisposed) form.Close(); }
            };
            Application.Run(form);
        }
        if (failure != null) throw failure;
    }

    private static void WaitUntilEnabled(Button action)
    {
        var deadline = Stopwatch.StartNew();
        while (!action.Enabled && deadline.ElapsedMilliseconds < 5000)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        if (!action.Enabled) throw new TimeoutException("Installer UI did not finish");
        Application.DoEvents();
    }

    private static void Capture(Form form, string root, string name)
    {
        using (var image = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save(Path.Combine(root, name));
        }
    }
}
