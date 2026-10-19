using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using ServerBotTracker;

internal static class InstallationScopeVerification
{
    public static void Run(Assembly setup, string root, Action<bool, string> assert)
    {
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FKP Server Monitor");
        string actual = (string)setup.GetType("InstallLocation", true).GetProperty("DefaultDirectory").GetValue(null, null);
        string shortcut = (string)setup.GetType("InstallationScope", true).GetProperty("MachineShortcut").GetValue(null, null);
        assert(actual == expected && shortcut == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "FKP Server Monitor.lnk"), "Standard installation defaults to Program Files and the all-users Start menu");
        VerifyPreview(setup, root, actual, assert);
        VerifyElevationJob(root, assert);
        assert(UpdateInstaller.RequiresElevation(expected) && !UpdateInstaller.RequiresElevation(Path.Combine(root, "ordinary-user-install")), "Protected Program Files updates request elevation while ordinary user-folder updates keep their current flow");
        ReleaseManifest prepared = JsonData.Read<ReleaseManifest>(Path.Combine(root, "published-update.json"));
        ReleaseManifest official = JsonData.Read<ReleaseManifest>(Path.Combine(root, "published-update.json"));
        UpdateInstaller.VerifyOfficialRelease(prepared, official);
        official.Sha256 = new string('0', 64);
        bool rejected = false;
        try { UpdateInstaller.VerifyOfficialRelease(prepared, official); } catch (InvalidDataException) { rejected = true; }
        assert(rejected, "An elevated update cannot accept a locally changed package digest that differs from the official manifest");
        VerifyMachineEngine(setup, root, assert);
        if (ElevationBroker.IsAdministrator && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") == ReleaseChannel.OfficialRepository)
            VerifyMachineProcesses(setup, root, assert);
        VerifyLegacyMigration(setup, root, assert);
    }

    private static void VerifyPreview(Assembly setup, string root, string target, Action<bool, string> assert)
    {
        Exception failure = null;
        Action<string> operation = delegate { throw new InvalidOperationException("Preview must not install anything"); };
        using (var form = (Form)Activator.CreateInstance(setup.GetType("SetupForm", true), new object[] { target, operation, false }))
        {
            form.Shown += delegate
            {
                try
                {
                    ((Button)form.Controls.Find("WizardNext", true)[0]).PerformClick();
                    assert(form.Controls.Find("InstallLocation", true)[0].Text == target, "The real destination page displays the standard Program Files folder before installation");
                    using (var picture = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
                        picture.Save(Path.Combine(root, "setup-program-files.png"));
                    }
                }
                catch (Exception error) { failure = error; }
                finally { form.Close(); }
            };
            Application.Run(form);
        }
        if (failure != null) throw failure;
    }

    private static void VerifyElevationJob(string root, Action<bool, string> assert)
    {
        string job = Path.Combine(root, "ServerBotTracker-elevation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(job);
        bool called = false;
        int result = ElevationBroker.Complete(job, delegate { called = true; }, delegate { return "Completion fixture notice"; });
        string text = File.ReadAllText(Path.Combine(job, "result.txt"));
        assert(ElevationBroker.IsAdministrator ? result == 0 && called && text.Contains("OK\nCompletion fixture notice") : result == 1 && !called && text.StartsWith("ERROR\n", StringComparison.Ordinal), ElevationBroker.IsAdministrator ? "An administrator job completes with an explicit success result and notice" : "An unprivileged administrator job is rejected before its operation runs");
        called = false;
        int repeat = ElevationBroker.Complete(job, delegate { called = true; }, null);
        int invalid = ElevationBroker.Complete(root, delegate { called = true; }, null);
        assert(repeat != 0 && invalid != 0 && !called, "Invalid elevation jobs and preexisting result files are rejected before mutation");
    }

    private static void VerifyMachineEngine(Assembly setup, string root, Action<bool, string> assert)
    {
        Type engine = setup.GetType("Setup", true);
        if (!ElevationBroker.IsAdministrator)
        {
            bool refused = false;
            string target = Path.Combine(root, "must-not-install-without-admin");
            try { engine.GetMethod("InstallMachine", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { target }); }
            catch (TargetInvocationException error) { refused = error.InnerException is UnauthorizedAccessException; }
            assert(refused && !Directory.Exists(target), "The machine installation engine cannot write without administrator permission");
            return;
        }
        string identity = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ServerBotTracker-Verification-" + identity);
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "ServerBotTracker-Verification-" + identity + ".lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + identity;
        try
        {
            engine.GetMethod("InstallAtScope", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { directory, shortcut, Registry.LocalMachine, registry });
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(File.Exists(Path.Combine(directory, "FKPServerMonitor.exe")) && File.Exists(shortcut) && key != null && (string)key.GetValue("InstallLocation") == directory, "The administrator runner performs a real Program Files install with a common shortcut and isolated HKLM registration");
            engine.GetMethod("RemoveAtScope", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { directory, shortcut, Registry.LocalMachine, registry });
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(!Directory.Exists(directory) && !File.Exists(shortcut) && key == null, "Machine uninstall removes only its isolated Program Files installation, common shortcut and HKLM test key");
        }
        finally
        {
            Registry.LocalMachine.DeleteSubKeyTree(registry, false);
            if (File.Exists(shortcut)) File.Delete(shortcut);
        }
    }

    private static void VerifyMachineProcesses(Assembly setup, string root, Action<bool, string> assert)
    {
        const string registry = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker";
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "FKP Server Monitor.lnk");
        using (RegistryKey existing = Registry.LocalMachine.OpenSubKey(registry))
            if (existing != null || File.Exists(shortcut)) throw new IOException("CI machine integration requires an empty product registration and shortcut");
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ServerBotTracker-ProcessVerification-" + Guid.NewGuid().ToString("N"));
        MethodInfo broker = setup.GetType("ServerBotTracker.ElevationBroker", true).GetMethod("Run");
        bool installed = false;
        try
        {
            broker.Invoke(null, new object[] { "--elevated-install", target });
            installed = true;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(key != null && (string)key.GetValue("InstallLocation") == target && File.Exists(shortcut), "Real runas setup process installs a disposable Program Files copy and registers it for all users");
            File.Copy(Path.Combine(root, "OldVersion.exe"), Path.Combine(target, ReleaseChannel.ApplicationFile), true);
            File.WriteAllText(Path.Combine(target, "keep-user-data.txt"), "Keep machine fixture data");
            bool liveAvailable = true;
            using (var channel = new ReleaseChannel())
            {
                try { channel.GetLatestAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult(); }
                catch (System.Net.Http.HttpRequestException error)
                {
                    if (ReleaseChannel.CurrentVersion != new Version(1, 8, 0, 0) || !error.Message.Contains("404")) throw;
                    liveAvailable = false;
                    Console.WriteLine("SKIP: The first renamed public release is still unpublished in 1.8; update-fkp.json is unavailable. Live official-feed elevation remains unverified. Testing the packaged update in Program Files instead.");
                }
            }
            ReleaseManifest applied = null;
            if (!liveAvailable)
            {
                string stage = Path.Combine(root, "first-renamed-machine-update");
                Directory.CreateDirectory(stage);
                File.Copy(Path.Combine(root, "published-update.json"), Path.Combine(stage, "manifest.json"));
                File.Copy(Path.Combine(root, "published-package.zip"), Path.Combine(stage, "package.zip"));
                applied = JsonData.Read<ReleaseManifest>(Path.Combine(stage, "manifest.json"));
                UpdateInstaller.Apply(stage, target);
            }
            for (int attempt = 0; liveAvailable && attempt < 3 && applied == null; attempt++)
            {
                string stage = Path.Combine(root, "live-machine-update-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                ReleaseManifest manifest;
                using (var channel = new ReleaseChannel())
                {
                    manifest = channel.GetLatestAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    using (var stream = File.Create(Path.Combine(stage, "package.zip")))
                    {
                        var download = (System.Threading.Tasks.Task)typeof(ReleaseChannel).GetMethod("DownloadAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(channel, new object[] { new Uri("https://github.com/" + ReleaseChannel.OfficialRepository + "/releases/download/" + manifest.Tag + "/FKPServerMonitor-portable.zip"), stream, manifest.SizeBytes, System.Threading.CancellationToken.None });
                        download.GetAwaiter().GetResult();
                    }
                }
                JsonData.WriteAtomic(Path.Combine(stage, "manifest.json"), manifest);
                JsonData.WriteAtomic(Path.Combine(stage, "job.json"), new UpdateJob { TargetDirectory = target });
                string worker = Path.Combine(stage, "UpdateWorker.exe");
                File.Copy(Path.Combine(root, "FKPServerMonitor.exe"), worker);
                File.Copy(Path.Combine(root, "FKPServerMonitor.exe.config"), worker + ".config");
                string result = Path.Combine(stage, "ServerBotTracker-elevation-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(result);
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry, true)) key.SetValue("DisplayVersion", "0.9.0.0");
                using (Process process = Process.Start(new ProcessStartInfo(worker, "--apply-update-admin \"" + Path.Combine(stage, "job.json") + "\" \"" + result + "\"") { UseShellExecute = true, Verb = "runas", WorkingDirectory = stage }))
                {
                    if (!process.WaitForExit(60000)) throw new TimeoutException("Elevated update fixture did not finish");
                    string outcome = File.ReadAllText(Path.Combine(result, "result.txt"));
                    if (process.ExitCode != 0)
                    {
                        if (attempt < 2 && outcome.Contains("ชุดอัปเดตไม่ตรง")) continue;
                        throw new IOException(outcome);
                    }
                    applied = manifest;
                }
            }
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(applied != null && FileVersionInfo.GetVersionInfo(Path.Combine(target, ReleaseChannel.ApplicationFile)).FileVersion == applied.Version && (string)key.GetValue("DisplayVersion") == applied.Version && File.Exists(Path.Combine(target, "Uninstall.exe")), liveAvailable ? "Real administrator update process verifies the live official package, replaces protected files and updates HKLM display version" : "Packaged update replaces protected Program Files binaries and updates HKLM metadata before the first renamed public release");
            broker.Invoke(null, new object[] { "--elevated-remove", target });
            installed = false;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(key == null && !File.Exists(shortcut) && !File.Exists(Path.Combine(target, "Uninstall.exe")) && File.ReadAllText(Path.Combine(target, "keep-user-data.txt")) == "Keep machine fixture data", "Real runas uninstaller removes the machine copy and registration while retaining unrelated data");
        }
        finally
        {
            if (installed) broker.Invoke(null, new object[] { "--elevated-remove", target });
            string data = Path.Combine(target, "keep-user-data.txt");
            if (File.Exists(data)) File.Delete(data);
            if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length == 0) Directory.Delete(target, false);
        }
    }

    private static void VerifyLegacyMigration(Assembly setup, string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "legacy-user-install");
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(root, "LegacyVersion.exe"), Path.Combine(target, ProductIdentity.LegacyApplicationFile));
        File.Copy(Path.Combine(root, "FKPServerMonitor.exe.config"), Path.Combine(target, ProductIdentity.LegacyConfigurationFile));
        File.Copy(Path.Combine(root, "FKPServerMonitor-Setup.exe"), Path.Combine(target, "Uninstall.exe"));
        File.WriteAllText(Path.Combine(target, "keep-settings.json"), "Keep legacy user data");
        string shortcut = Path.Combine(root, "legacy-user-install.lnk");
        File.WriteAllText(shortcut, "Legacy fixture shortcut");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root) + "-legacy";
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry))
        {
            key.SetValue("DisplayName", ProductIdentity.LegacyDisplayName);
            key.SetValue("InstallLocation", target);
        }
        try
        {
            MethodInfo remove = setup.GetType("Setup", true).GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static);
            Action<string, string, string> cleanup = delegate(string directory, string link, string key) { remove.Invoke(null, new object[] { directory, link, key }); };
            string notice = (string)setup.GetType("InstallationScope", true).GetMethod("CleanLegacyRegistration").Invoke(null, new object[] { Path.Combine(root, "new-machine-target"), Registry.CurrentUser, registry, shortcut, cleanup });
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(String.IsNullOrEmpty(notice) && key == null && !File.Exists(shortcut) && !File.Exists(Path.Combine(target, "Uninstall.exe"))
                    && !File.Exists(Path.Combine(target, ProductIdentity.LegacyApplicationFile)) && !File.Exists(Path.Combine(target, ProductIdentity.LegacyConfigurationFile))
                    && File.ReadAllText(Path.Combine(target, "keep-settings.json")) == "Keep legacy user data", "Migration removes the former product's per-user registration and old filenames while preserving unrelated legacy data");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
    }
}
