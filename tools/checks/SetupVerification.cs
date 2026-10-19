using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using ServerBotTracker;

internal static class SetupVerification
{
    public static void Run(string root, Action<bool, string> assert)
    {
        string setupPath = Path.Combine(root, "FKPServerMonitor-Setup.exe");
        Assembly setupAssembly = Assembly.LoadFrom(setupPath);
        VerifyBranding(typeof(MainForm).Assembly, Path.Combine(root, "FKPServerMonitor.exe"), root, "application", assert);
        VerifyBranding(setupAssembly, setupPath, root, "setup", assert);
        assert(setupAssembly.GetName().Version == typeof(MainForm).Assembly.GetName().Version, "Installer and application versions match");
        InstallationScopeVerification.Run(setupAssembly, root, assert);
        RenameVerification.Run(setupAssembly, root, assert);
        VerifyInstallFlow(setupAssembly, root, assert);
        VerifyRetry(setupAssembly, root, assert);
        VerifyCustomInstallation(setupAssembly, root, assert);
        VerifyUninstall(setupAssembly, root, assert);
        VerifyUninstallFailures(setupAssembly, root, assert);
        VerifyUninstallCleanupNotice(setupAssembly, root, assert);
        VerifyUninstallEntryPoint(root, assert);
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

    private static Form CreateForm(Assembly setupAssembly, string root, Action<string> install, bool uninstall = false)
    {
        Type formType = setupAssembly.GetType("SetupForm", true);
        return (Form)Activator.CreateInstance(formType, new object[] { Path.Combine(root, "setup-ui-fixture"), install, uninstall });
    }

    private static void VerifyInstallFlow(Assembly setupAssembly, string root, Action<bool, string> assert)
    {
        Exception failure = null;
        ApartmentState apartment = ApartmentState.Unknown;
        int operations = 0;
        string receivedTarget = null;
        string selectedTarget = Path.Combine(root, "โฟลเดอร์ที่เลือก", "FKP Server Monitor");
        using (var release = new ManualResetEventSlim(false))
        using (Form form = CreateForm(setupAssembly, root, delegate(string selected)
        {
            receivedTarget = selected;
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
                    var destination = (TextBox)form.Controls.Find("InstallLocation", true).Single();
                    var browse = (Button)form.Controls.Find("BrowseInstallLocation", true).Single();
                    assert(operations == 0 && back.Enabled && !destination.ReadOnly && browse.Visible && browse.Enabled, "Next opens the editable installation folder page with Browse before any writes");
                    destination.Text = "relative-folder";
                    action.PerformClick();
                    assert(operations == 0 && !destination.ReadOnly && form.Controls.Find("WizardStatus", true).Single().Text.Length > 0, "An invalid relative destination stays on the folder page with a visible error");
                    destination.Text = selectedTarget;
                    Capture(form, root, "setup-destination.png");
                    action.PerformClick();
                    assert(operations == 0 && destination.ReadOnly && !browse.Visible && destination.Text == selectedTarget && action.Text == "ติดตั้ง", "Confirmation displays the selected Thai path without creating the directory");
                    back.PerformClick();
                    assert(!destination.ReadOnly && destination.Text == selectedTarget && browse.Visible, "Back from confirmation preserves the chosen folder for editing");
                    back.PerformClick();
                    assert(operations == 0 && !back.Enabled && action.Text == "ถัดไป >", "Back from folder selection returns to welcome without side effects");
                    action.PerformClick();
                    action.PerformClick();
                    Capture(form, root, "setup-ready.png");
                    action.PerformClick();
                    assert(!action.Enabled && !form.Controls.Find("WizardCancel", true).Single().Enabled && form.Controls.Find("WizardProgress", true).Single().Visible, "Installer shows progress and disables duplicate installation while its worker runs");
                    form.Close();
                    assert(!form.IsDisposed, "Closing during installation cannot interrupt file replacement");
                    Capture(form, root, "setup-installing.png");
                    release.Set();
                    WaitUntilEnabled(action);
                    assert(apartment == ApartmentState.STA && receivedTarget == selectedTarget && form.Controls.Find("WizardTitle", true).Single().Text == "ติดตั้งเสร็จสมบูรณ์", "Installer passes the exact selected folder to its dedicated STA worker and finishes on the UI thread");
                    var launch = (CheckBox)form.Controls.Find("LaunchAfterSetup", true).Single();
                    assert(launch.Visible && launch.Checked && action.Text == "เสร็จสิ้น", "Completion screen offers optional app launch without another confirmation dialog");
                    Capture(form, root, "setup-complete.png");
                    launch.Checked = false;
                    action.PerformClick();
                    assert(form.IsDisposed && !Directory.Exists(selectedTarget), "Finish without launch closes safely and the isolated UI fixture writes no installation files");
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

    private static void VerifyCustomInstallation(Assembly assembly, string root, Action<bool, string> assert)
    {
        Type setup = assembly.GetType("Setup", true);
        Type locations = assembly.GetType("InstallLocation", true);
        MethodInfo normalize = locations.GetMethod("Normalize");
        MethodInfo remembered = locations.GetMethod("ReadRemembered");
        MethodInfo destination = locations.GetMethod("ForDestination");
        MethodInfo browseSelection = locations.GetMethod("ForBrowseSelection");
        string target = Path.Combine(root, "ติดตั้งภาษาไทย", "FKP Server Monitor");
        string second = Path.Combine(root, "ตำแหน่งใหม่", "FKP Server Monitor");
        string shortcut = Path.Combine(root, "custom-install.lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root) + "-custom";
        MethodInfo install = setup.GetMethod("InstallAt", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo remove = setup.GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            bool rejectsInvalid = true;
            foreach (string path in new[] { "", "relative-folder", @"C:relative", @"\relative", Path.GetPathRoot(target), @"\\server\share", @"C:\bad|folder" })
            {
                try { normalize.Invoke(null, new object[] { path }); rejectsInvalid = false; }
                catch (TargetInvocationException exception) { if (!(exception.InnerException is IOException)) throw; }
            }
            assert(rejectsInvalid, "Destination validation rejects blank, relative, drive/share roots and invalid paths");
            assert((string)normalize.Invoke(null, new object[] { @"\\server\share\FKP Server Monitor\" }) == @"\\server\share\FKP Server Monitor", "An absolute UNC subfolder is retained without writing to the share");
            assert((string)destination.Invoke(null, new object[] { @"D:\" }) == @"D:\FKP Server Monitor" && (string)destination.Invoke(null, new object[] { "D:" }) == @"D:\FKP Server Monitor", "Choosing a drive creates a product subfolder path instead of installing into the drive root");
            string parent = Path.Combine(root, "เลือกโฟลเดอร์หลัก");
            string productFolder = Path.Combine(parent, "FKP Server Monitor");
            assert((string)browseSelection.Invoke(null, new object[] { parent }) == productFolder && (string)browseSelection.Invoke(null, new object[] { productFolder }) == productFolder, "Browse adds the product folder once and preserves an already selected product folder");
            string collision = Path.Combine(root, "unrelated-install");
            Directory.CreateDirectory(collision);
            string unrelated = Path.Combine(collision, "Uninstall.exe");
            File.WriteAllText(unrelated, "Unrelated program");
            bool rejected = false;
            try { install.Invoke(null, new object[] { collision, shortcut, registry }); }
            catch (TargetInvocationException exception) { rejected = exception.InnerException is IOException; }
            assert(rejected && File.ReadAllText(unrelated) == "Unrelated program" && !File.Exists(Path.Combine(collision, "FKPServerMonitor.exe")) && !File.Exists(shortcut), "Selecting another program's files fails before replacement or registration");
            install.Invoke(null, new object[] { target + Path.DirectorySeparatorChar, shortcut, registry });
            using (Stream expected = File.OpenRead(Path.Combine(root, "FKPServerMonitor.exe")))
            using (Stream actual = File.OpenRead(Path.Combine(target, "FKPServerMonitor.exe")))
                assert(Hash(expected) == Hash(actual) && File.Exists(Path.Combine(target, "FKPServerMonitor.exe.config")) && File.Exists(Path.Combine(target, "Uninstall.exe")), "The real installer extracts its payload into the exact selected Thai folder with spaces");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert((string)key.GetValue("InstallLocation") == target && (string)key.GetValue("UninstallString") == "\"" + Path.Combine(target, "Uninstall.exe") + "\" --uninstall" && (string)key.GetValue("DisplayIcon") == Path.Combine(target, "FKPServerMonitor.exe"), "Control Panel metadata uses the custom folder and correctly quotes its uninstall command");
            assert((string)remembered.Invoke(null, new object[] { registry }) == target, "A later Setup run remembers the registered custom installation folder");
            object shortcutInstance = new DesktopShortcut.ShellLink();
            try
            {
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)shortcutInstance).Load(shortcut, 0);
                var link = (DesktopShortcut.IShellLinkW)shortcutInstance;
                var executable = new StringBuilder(32768);
                var working = new StringBuilder(32768);
                var icon = new StringBuilder(32768);
                int iconIndex;
                link.GetPath(executable, executable.Capacity, IntPtr.Zero, 0);
                link.GetWorkingDirectory(working, working.Capacity);
                link.GetIconLocation(icon, icon.Capacity, out iconIndex);
                assert(String.Equals(executable.ToString(), Path.Combine(target, "FKPServerMonitor.exe"), StringComparison.OrdinalIgnoreCase) && String.Equals(working.ToString(), target, StringComparison.OrdinalIgnoreCase) && iconIndex == 0 && String.Equals(icon.ToString(), executable.ToString(), StringComparison.OrdinalIgnoreCase), "The actual Start menu shortcut retains the Unicode executable, working folder and product icon");
            }
            finally { Marshal.FinalReleaseComObject(shortcutInstance); }
            string stage = Path.Combine(root, "custom-update-stage");
            Directory.CreateDirectory(stage);
            File.Copy(Path.Combine(root, "published-update.json"), Path.Combine(stage, "manifest.json"));
            File.Copy(Path.Combine(root, "published-package.zip"), Path.Combine(stage, "package.zip"));
            File.Copy(Path.Combine(root, "OldVersion.exe"), Path.Combine(target, "FKPServerMonitor.exe"), true);
            File.WriteAllText(Path.Combine(target, "servers.json"), "Preserve custom folder data");
            UpdateInstaller.Apply(stage, target);
            assert(FileVersionInfo.GetVersionInfo(Path.Combine(target, "FKPServerMonitor.exe")).FileVersion == ReleaseChannel.CurrentVersion.ToString() && File.ReadAllText(Path.Combine(target, "servers.json")) == "Preserve custom folder data" && File.Exists(Path.Combine(target, "Uninstall.exe")), "Automatic update applies in the custom folder and preserves the uninstaller and unrelated data");
            install.Invoke(null, new object[] { second, shortcut, registry });
            remove.Invoke(null, new object[] { target, shortcut, registry });
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(key != null && (string)key.GetValue("InstallLocation") == second && File.Exists(shortcut) && File.Exists(Path.Combine(second, "FKPServerMonitor.exe")), "An old copy's removal preserves the newer installation's registry entry and shortcut");
            remove.Invoke(null, new object[] { second, shortcut, registry });
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(key == null && !File.Exists(shortcut) && !Directory.Exists(second) && File.ReadAllText(Path.Combine(target, "servers.json")) == "Preserve custom folder data", "Custom-folder uninstall removes its own files and registration while keeping unrelated data");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
    }

    private static void VerifyUninstall(Assembly setupAssembly, string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "uninstall-fixture");
        string shortcut = Path.Combine(root, "uninstall-fixture.lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root);
        Directory.CreateDirectory(target);
        foreach (string file in new[] { "FKPServerMonitor.exe", "FKPServerMonitor.exe.config", "Uninstall.exe" })
            File.WriteAllText(Path.Combine(target, file), "Owned fixture file");
        File.WriteAllText(Path.Combine(target, "servers.json"), "Preserved fixture settings");
        Directory.CreateDirectory(Path.Combine(target, "user-folder"));
        File.WriteAllText(Path.Combine(target, "user-folder", "keep.txt"), "Unrelated content");
        File.WriteAllText(shortcut, "Fixture shortcut");
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry))
        {
            key.SetValue("DisplayName", "Isolated removal fixture");
            key.SetValue("InstallLocation", target);
        }
        MethodInfo remove = setupAssembly.GetType("Setup", true).GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static);
        Action<string> uninstall = delegate(string selected)
        {
            try { remove.Invoke(null, new object[] { selected, shortcut, registry }); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        };
        bool rootRejected = false;
        try { remove.Invoke(null, new object[] { Path.GetPathRoot(target), shortcut, registry }); }
        catch (TargetInvocationException exception) { rootRejected = exception.InnerException is IOException; }
        assert(rootRejected && File.Exists(shortcut), "Uninstaller refuses a volume root before deleting files or registration");
        Exception failure = null;
        FileStream locked = File.Open(Path.Combine(target, "FKPServerMonitor.exe.config"), FileMode.Open, FileAccess.Read, FileShare.None);
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
                        assert(action.Text == "ถอนการติดตั้ง" && File.Exists(Path.Combine(target, "FKPServerMonitor.exe")), "Uninstall wizard requires confirmation before removal");
                        Capture(form, root, "uninstall-ready.png");
                        action.PerformClick();
                        WaitUntilEnabled(action);
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                            assert(action.Text == "ลองอีกครั้ง" && key != null && File.Exists(Path.Combine(target, "FKPServerMonitor.exe")) && File.Exists(Path.Combine(target, "FKPServerMonitor.exe.config")) && File.Exists(Path.Combine(target, "Uninstall.exe")) && File.Exists(shortcut), "A locked second file blocks uninstall before any product file, shortcut or registration disappears");
                        locked.Dispose();
                        locked = null;
                        action.PerformClick();
                        WaitUntilEnabled(action);
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                            assert(action.Text == "เสร็จสิ้น" && key == null && !File.Exists(shortcut) && !File.Exists(Path.Combine(target, "Uninstall.exe")) && !File.Exists(Path.Combine(target, "FKPServerMonitor.exe")) && !File.Exists(Path.Combine(target, "FKPServerMonitor.exe.config")), "Uninstall retry removes only product files, shortcut and its isolated registration");
                        assert(File.ReadAllText(Path.Combine(target, "servers.json")) == "Preserved fixture settings" && File.ReadAllText(Path.Combine(target, "user-folder", "keep.txt")) == "Unrelated content", "Uninstall preserves user settings and unrelated files and directories");
                        assert(!form.Controls.Find("LaunchAfterSetup", true).Single().Visible, "Uninstall completion never offers to launch the removed app");
                        string emptyTarget = Path.Combine(root, "uninstall-empty-fixture");
                        Directory.CreateDirectory(emptyTarget);
                        File.WriteAllText(Path.Combine(emptyTarget, "FKPServerMonitor.exe"), "Owned fixture file");
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

    private static void VerifyUninstallFailures(Assembly assembly, string root, Action<bool, string> assert)
    {
        MethodInfo remove = assembly.GetType("Setup", true).GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static);
        string[] names = { "FKPServerMonitor.exe", "FKPServerMonitor.exe.config", "Uninstall.exe" };
        foreach (string lockedName in new[] { "Uninstall.exe", "shortcut.lnk", "move-denied" })
        {
            string target = Path.Combine(root, "removal-rollback-" + lockedName);
            string shortcut = Path.Combine(root, "removal-rollback-" + lockedName + ".lnk");
            string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root) + "-" + lockedName;
            Directory.CreateDirectory(target);
            foreach (string name in names) File.WriteAllText(Path.Combine(target, name), "Keep " + name);
            File.WriteAllText(shortcut, "Keep shortcut");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry))
            {
                key.SetValue("InstallLocation", target);
                key.SetValue("DisplayVersion", "Fixture original version");
            }
            FileStream locked = null;
            DirectorySecurity directorySecurity = null;
            FileSecurity fileSecurity = null;
            string deniedFile = Path.Combine(target, "FKPServerMonitor.exe.config");
            try
            {
                if (lockedName == "move-denied")
                {
                    SecurityIdentifier identity = WindowsIdentity.GetCurrent().User;
                    directorySecurity = Directory.GetAccessControl(target);
                    var deniedDirectory = Directory.GetAccessControl(target);
                    deniedDirectory.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Deny));
                    Directory.SetAccessControl(target, deniedDirectory);
                    fileSecurity = File.GetAccessControl(deniedFile);
                    var denied = File.GetAccessControl(deniedFile);
                    denied.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Delete, AccessControlType.Deny));
                    File.SetAccessControl(deniedFile, denied);
                }
                else
                    locked = File.Open(lockedName == "shortcut.lnk" ? shortcut : Path.Combine(target, lockedName), FileMode.Open, FileAccess.Read, FileShare.None);
                bool failed = false;
                try { remove.Invoke(null, new object[] { target, shortcut, registry }); }
                catch (TargetInvocationException error) { failed = error.InnerException is IOException; }
                bool filesPreserved = names.All(name => File.Exists(Path.Combine(target, name)) && (name == lockedName || File.ReadAllText(Path.Combine(target, name)) == "Keep " + name));
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                    assert(failed && filesPreserved && File.Exists(shortcut) && key != null && (string)key.GetValue("DisplayVersion") == "Fixture original version", lockedName == "move-denied" ? "A later move denied by Windows permissions rolls the earlier EXE move back and preserves registration" : "A locked " + lockedName + " cannot leave a partially removed program");
            }
            catch { Registry.CurrentUser.DeleteSubKeyTree(registry, false); throw; }
            finally
            {
                if (locked != null) locked.Dispose();
                if (fileSecurity != null)
                {
                    var restored = new FileSecurity();
                    restored.SetSecurityDescriptorBinaryForm(fileSecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                    File.SetAccessControl(deniedFile, restored);
                }
                if (directorySecurity != null)
                {
                    var restored = new DirectorySecurity();
                    restored.SetSecurityDescriptorBinaryForm(directorySecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                    Directory.SetAccessControl(target, restored);
                }
            }
            try
            {
                remove.Invoke(null, new object[] { target, shortcut, registry });
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                    assert(!Directory.Exists(target) && !File.Exists(shortcut) && key == null, "Uninstall succeeds completely after the " + lockedName + " obstruction is released");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
        }
    }

    private static void VerifyUninstallCleanupNotice(Assembly assembly, string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "removal-cleanup-notice");
        string shortcut = Path.Combine(root, "removal-cleanup-notice.lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root) + "-cleanup";
        string[] names = { "FKPServerMonitor.exe", "FKPServerMonitor.exe.config", "Uninstall.exe" };
        Directory.CreateDirectory(target);
        foreach (string name in names) File.WriteAllText(Path.Combine(target, name), "Owned fixture file");
        File.WriteAllText(shortcut, "Owned fixture shortcut");
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry)) key.SetValue("InstallLocation", target);
        DirectorySecurity directorySecurity = Directory.GetAccessControl(target);
        FileSecurity fileSecurity = File.GetAccessControl(Path.Combine(target, names[0]));
        try
        {
            SecurityIdentifier identity = WindowsIdentity.GetCurrent().User;
            var deniedDirectory = Directory.GetAccessControl(target);
            deniedDirectory.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.DeleteSubdirectoriesAndFiles, InheritanceFlags.ContainerInherit, PropagationFlags.InheritOnly, AccessControlType.Deny));
            Directory.SetAccessControl(target, deniedDirectory);
            var deniedFile = File.GetAccessControl(Path.Combine(target, names[0]));
            deniedFile.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Delete, AccessControlType.Deny));
            File.SetAccessControl(Path.Combine(target, names[0]), deniedFile);
            assembly.GetType("Setup", true).GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { target, shortcut, registry });
            string notice = (string)assembly.GetType("Setup", true).GetField("removalNotice", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(names.All(name => !File.Exists(Path.Combine(target, name))) && !File.Exists(shortcut) && key == null && notice.Contains("ถอนโปรแกรมแล้ว") && Directory.GetDirectories(target, ".uninstall-*").Length == 1, "Post-uninstall cleanup failure reports completed removal with a cleanup notice instead of a false uninstall error");
        }
        finally
        {
            var restoredDirectory = new DirectorySecurity();
            restoredDirectory.SetSecurityDescriptorBinaryForm(directorySecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
            Directory.SetAccessControl(target, restoredDirectory);
            foreach (string stage in Directory.GetDirectories(target, ".uninstall-*"))
            {
                foreach (string name in names)
                {
                    string file = Path.Combine(stage, name);
                    if (!File.Exists(file)) continue;
                    var restoredFile = new FileSecurity();
                    restoredFile.SetSecurityDescriptorBinaryForm(fileSecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                    File.SetAccessControl(file, restoredFile);
                    File.Delete(file);
                }
                string backupShortcut = Path.Combine(stage, "shortcut.bak");
                if (File.Exists(backupShortcut)) File.Delete(backupShortcut);
                Directory.Delete(stage, false);
            }
            string original = Path.Combine(target, names[0]);
            if (File.Exists(original))
            {
                var restoredFile = new FileSecurity();
                restoredFile.SetSecurityDescriptorBinaryForm(fileSecurity.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                File.SetAccessControl(original, restoredFile);
            }
            Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        }
    }

    private static void VerifyUninstallEntryPoint(string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "ถอนการติดตั้งโดยตรง", "FKP Server Monitor");
        Directory.CreateDirectory(target);
        string uninstaller = Path.Combine(target, "Uninstall.exe");
        File.Copy(Path.Combine(root, "FKPServerMonitor-Setup.exe"), uninstaller);
        File.WriteAllText(Path.Combine(target, "servers.json"), "Keep direct-launch fixture data");
        foreach (string arguments in new[] { String.Empty, "--uninstall" })
        {
            Process helper = null;
            string helperPath = null;
            using (Process launcher = Process.Start(new ProcessStartInfo(uninstaller, arguments) { UseShellExecute = false, WorkingDirectory = target }))
            {
                try
                {
                    var elapsed = Stopwatch.StartNew();
                    bool targetForwarded = false;
                    while (helper == null && elapsed.ElapsedMilliseconds < 10000)
                    {
                        using (var query = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE ParentProcessId = " + launcher.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        using (ManagementObjectCollection children = query.Get())
                        {
                            foreach (ManagementObject child in children)
                            using (child)
                            {
                                string path = child["ExecutablePath"] as string;
                                string command = child["CommandLine"] as string;
                                if (path != null && command != null && Path.GetFileName(path).StartsWith("FKPServerMonitor-uninstall-", StringComparison.OrdinalIgnoreCase) && command.Contains("--uninstall-wizard") && command.Contains("\"" + target + "\""))
                                {
                                    helperPath = path;
                                    targetForwarded = true;
                                    helper = Process.GetProcessById(Convert.ToInt32(child["ProcessId"], System.Globalization.CultureInfo.InvariantCulture));
                                    if (helper.Handle == IntPtr.Zero) throw new InvalidOperationException("Cannot retain the fixture helper process handle");
                                    break;
                                }
                            }
                        }
                        if (helper == null) Thread.Sleep(100);
                    }
                    bool launcherExited = launcher.WaitForExit(10000);
                    bool uninstallWindow = false;
                    if (helper != null)
                    {
                        helper.WaitForInputIdle(10000);
                        elapsed.Restart();
                        while (!helper.HasExited && elapsed.ElapsedMilliseconds < 5000)
                        {
                            helper.Refresh();
                            if (helper.MainWindowTitle.Contains("FKP Server Monitor") && helper.MainWindowTitle.Contains("ถอนการติดตั้ง")) { uninstallWindow = true; break; }
                            Thread.Sleep(100);
                        }
                    }
                    assert(targetForwarded && launcherExited && launcher.ExitCode == 0 && uninstallWindow, arguments.Length == 0 ? "Double-clicking the real Uninstall.exe opens the temporary uninstall wizard for its own Thai folder" : "Control Panel's --uninstall route still opens the same uninstall wizard");
                    bool closed = helper.CloseMainWindow() && helper.WaitForExit(5000);
                    assert(closed && helper.ExitCode == 0 && File.Exists(uninstaller) && !File.Exists(Path.Combine(target, "FKPServerMonitor.exe")) && File.ReadAllText(Path.Combine(target, "servers.json")) == "Keep direct-launch fixture data", "Cancelling the real uninstall welcome screen exits cleanly without installing or removing fixture files");
                }
                finally
                {
                    if (helper != null)
                    {
                        if (!helper.HasExited) { helper.CloseMainWindow(); if (!helper.WaitForExit(3000)) helper.Kill(); }
                        helper.Dispose();
                    }
                    if (!launcher.HasExited) { launcher.CloseMainWindow(); if (!launcher.WaitForExit(3000)) launcher.Kill(); }
                    if (helperPath != null && File.Exists(helperPath)) File.Delete(helperPath);
                }
            }
        }
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
