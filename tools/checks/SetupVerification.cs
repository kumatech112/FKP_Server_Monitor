using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
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
        VerifyUninstall(setupAssembly, root, assert);
        assert(UpdateInstaller.MatchesInstallLocation(root, root.ToUpperInvariant() + Path.DirectorySeparatorChar), "Control Panel version matching accepts equivalent paths with a trailing separator");
        assert(!UpdateInstaller.MatchesInstallLocation(null, root) && !UpdateInstaller.MatchesInstallLocation(root, root + "-other"), "Control Panel version matching rejects absent and different installations");
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

    private static Form CreateForm(Assembly setupAssembly, string root, Action install, bool uninstall = false)
    {
        Type formType = setupAssembly.GetType("SetupForm", true);
        return (Form)Activator.CreateInstance(formType, new object[] { Path.Combine(root, "setup-ui-fixture"), install, uninstall });
    }

    private static void VerifyInstallFlow(Assembly setupAssembly, string root, Action<bool, string> assert)
    {
        Exception failure = null;
        ApartmentState apartment = ApartmentState.Unknown;
        int operations = 0;
        using (var release = new ManualResetEventSlim(false))
        using (Form form = CreateForm(setupAssembly, root, delegate
        {
            apartment = Thread.CurrentThread.GetApartmentState();
            Interlocked.Increment(ref operations);
            if (!release.Wait(10000)) throw new TimeoutException("Setup fixture was not released");
        }))
        {
            form.Shown += delegate
            {
                Button action = (Button)form.Controls.Find("WizardNext", true).Single();
                try
                {
                    assert(form.Icon != null && form.Controls.Find("ProductLogo", true).Length == 1 && form.Controls.Find("WizardArtwork", true).Length == 1 && form.BackColor == Color.White, "Classic Thai wizard has a robot artwork panel and white content pages");
                    Capture(form, root, "setup-welcome.png");
                    action.PerformClick();
                    Button back = (Button)form.Controls.Find("WizardBack", true).Single();
                    assert(operations == 0 && back.Enabled && action.Text == "ติดตั้ง", "Next advances to confirmation without installing");
                    back.PerformClick();
                    assert(operations == 0 && !back.Enabled && action.Text == "ถัดไป >", "Back returns to welcome without side effects");
                    action.PerformClick();
                    Capture(form, root, "setup-ready.png");
                    action.PerformClick();
                    assert(!action.Enabled && !form.Controls.Find("WizardCancel", true).Single().Enabled && form.Controls.Find("WizardProgress", true).Single().Visible, "Installer shows progress and disables duplicate installation while its worker runs");
                    form.Close();
                    assert(!form.IsDisposed, "Closing during installation cannot interrupt file replacement");
                    Capture(form, root, "setup-installing.png");
                    release.Set();
                    WaitUntilEnabled(action);
                    assert(apartment == ApartmentState.STA && form.Controls.Find("WizardTitle", true).Single().Text == "ติดตั้งเสร็จสมบูรณ์", "Installer finishes on the UI thread after its dedicated STA worker completes");
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
                Button action = (Button)form.Controls.Find("WizardNext", true).Single();
                try
                {
                    action.PerformClick();
                    action.PerformClick();
                    WaitUntilEnabled(action);
                    assert(action.Text == "ลองอีกครั้ง" && form.Controls.Find("WizardStatus", true).Single().Text.Contains("ทดสอบข้อผิดพลาด"), "An installation failure stays visible and permits retry without claiming success");
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

    private static void VerifyUninstall(Assembly setupAssembly, string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "uninstall-fixture");
        string shortcut = Path.Combine(root, "uninstall-fixture.lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root);
        Directory.CreateDirectory(target);
        foreach (string file in new[] { "ServerBotTracker.exe", "ServerBotTracker.exe.config", "Uninstall.exe" })
            File.WriteAllText(Path.Combine(target, file), "Owned fixture file");
        File.WriteAllText(Path.Combine(target, "servers.json"), "Preserved fixture settings");
        Directory.CreateDirectory(Path.Combine(target, "user-folder"));
        File.WriteAllText(Path.Combine(target, "user-folder", "keep.txt"), "Unrelated content");
        File.WriteAllText(shortcut, "Fixture shortcut");
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry))
            key.SetValue("DisplayName", "Isolated removal fixture");
        MethodInfo remove = setupAssembly.GetType("Setup", true).GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static);
        Action uninstall = delegate
        {
            try { remove.Invoke(null, new object[] { target, shortcut, registry }); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        };
        bool rootRejected = false;
        try { remove.Invoke(null, new object[] { Path.GetPathRoot(target), shortcut, registry }); }
        catch (TargetInvocationException exception) { rootRejected = exception.InnerException is IOException; }
        assert(rootRejected && File.Exists(shortcut), "Uninstaller refuses a volume root before deleting files or registration");
        Exception failure = null;
        FileStream locked = File.Open(Path.Combine(target, "ServerBotTracker.exe"), FileMode.Open, FileAccess.Read, FileShare.None);
        try
        {
            using (var form = (Form)Activator.CreateInstance(setupAssembly.GetType("SetupForm", true), new object[] { target, uninstall, true }))
            {
                form.Shown += delegate
                {
                    Button action = (Button)form.Controls.Find("WizardNext", true).Single();
                    try
                    {
                        Capture(form, root, "uninstall-welcome.png");
                        action.PerformClick();
                        assert(action.Text == "ถอนการติดตั้ง" && File.Exists(Path.Combine(target, "ServerBotTracker.exe")), "Uninstall wizard requires confirmation before removal");
                        Capture(form, root, "uninstall-ready.png");
                        action.PerformClick();
                        WaitUntilEnabled(action);
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                            assert(action.Text == "ลองอีกครั้ง" && key != null, "A locked program file blocks uninstall, retains registration and allows retry");
                        locked.Dispose();
                        locked = null;
                        action.PerformClick();
                        WaitUntilEnabled(action);
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                            assert(action.Text == "เสร็จสิ้น" && key == null && !File.Exists(shortcut) && !File.Exists(Path.Combine(target, "Uninstall.exe")) && !File.Exists(Path.Combine(target, "ServerBotTracker.exe")) && !File.Exists(Path.Combine(target, "ServerBotTracker.exe.config")), "Uninstall retry removes only product files, shortcut and its isolated registration");
                        assert(File.ReadAllText(Path.Combine(target, "servers.json")) == "Preserved fixture settings" && File.ReadAllText(Path.Combine(target, "user-folder", "keep.txt")) == "Unrelated content", "Uninstall preserves user settings and unrelated files and directories");
                        assert(!form.Controls.Find("LaunchAfterSetup", true).Single().Visible, "Uninstall completion never offers to launch the removed app");
                        string emptyTarget = Path.Combine(root, "uninstall-empty-fixture");
                        Directory.CreateDirectory(emptyTarget);
                        File.WriteAllText(Path.Combine(emptyTarget, "ServerBotTracker.exe"), "Owned fixture file");
                        remove.Invoke(null, new object[] { emptyTarget, shortcut, registry });
                        assert(!Directory.Exists(emptyTarget), "Uninstall removes an installation directory when no unrelated files remain");
                        Capture(form, root, "uninstall-complete.png");
                        action.PerformClick();
                    }
                    catch (Exception exception) { failure = exception; if (locked != null) { locked.Dispose(); locked = null; } if (!form.IsDisposed) form.Close(); }
                };
                Application.Run(form);
            }
        }
        finally
        {
            if (locked != null) locked.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(registry, false);
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
