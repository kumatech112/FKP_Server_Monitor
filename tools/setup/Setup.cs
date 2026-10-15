using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using ServerBotTracker;

internal static class Setup
{
    private const string Product = "Server Bot Tracker";
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker";
    private static string removalNotice = String.Empty;
    private static string installationNotice = String.Empty;
    private static string Target { get { return InstallationScope.InitialDirectory; } }
    private static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Server Bot Tracker.lnk"); } }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--elevated-install")
                return ElevationBroker.Complete(args[2], delegate { InstallMachine(args[1]); }, delegate { return installationNotice; });
            if (args.Length == 3 && args[0] == "--elevated-remove")
                return ElevationBroker.Complete(args[2], delegate { RemoveMachine(args[1]); }, delegate { return removalNotice; });
            bool directUninstall = args.Length == 0 && String.Equals(Path.GetFileName(Assembly.GetExecutingAssembly().Location), "Uninstall.exe", StringComparison.OrdinalIgnoreCase);
            if (directUninstall || (args.Length == 1 && args[0] == "--uninstall"))
            {
                StartUninstallWizard();
                return 0;
            }
            if (args.Length == 4 && args[0] == "--uninstall-wizard")
            {
                WaitForLauncher(args[1], args[2]);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new SetupForm(InstallLocation.Normalize(args[3]), Remove, true, delegate { return removalNotice; }))
                    Application.Run(form);
                return 0;
            }
            if (args.Length == 1 && args[0] == "/silent")
            {
                Install(Target);
                return 0;
            }
            if (args.Length != 0) throw new ArgumentException("พารามิเตอร์ตัวติดตั้งไม่ถูกต้อง");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new SetupForm(Target, Install, false, delegate { return installationNotice; }))
                Application.Run(form);
            return 0;
        }
        catch (Exception exception)
        {
            if (args.Length == 0 || args[0] != "/silent") MessageBox.Show(exception.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void StartUninstallWizard()
    {
        string helper = Path.Combine(Path.GetTempPath(), "ServerBotTracker-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Assembly.GetExecutingAssembly().Location, helper);
        using (Process launcher = Process.GetCurrentProcess())
        {
            string target = InstallLocation.Normalize(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            string arguments = "--uninstall-wizard " + launcher.Id.ToString(CultureInfo.InvariantCulture) + " " + launcher.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) + " \"" + target + "\"";
            Process.Start(new ProcessStartInfo(helper, arguments) { UseShellExecute = false, CreateNoWindow = true });
        }
    }

    private static void WaitForLauncher(string id, string started)
    {
        int processId = Int32.Parse(id, CultureInfo.InvariantCulture);
        long ticks = Int64.Parse(started, CultureInfo.InvariantCulture);
        try
        {
            using (Process launcher = Process.GetProcessById(processId))
            {
                if (launcher.StartTime.ToUniversalTime().Ticks != ticks || !launcher.WaitForExit(10000))
                    throw new IOException("ตัวเริ่มถอนการติดตั้งยังไม่ปิด กรุณาลองใหม่");
            }
        }
        catch (ArgumentException) { /* The launcher has already exited. */ }
    }

    private static Mutex AcquireInstance()
    {
        var mutex = new Mutex(false, "Local\\Kuma.ServerBotTracker." + WindowsIdentity.GetCurrent().User.Value);
        bool acquired;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
        {
            mutex.Dispose();
            throw new IOException("กรุณาปิด Server Bot Tracker ก่อนติดตั้งหรือถอนการติดตั้ง");
        }
        return mutex;
    }

    private static void Install(string targetDirectory)
    {
        installationNotice = String.Empty;
        string target = InstallLocation.Normalize(targetDirectory);
        using (Mutex instance = AcquireInstance()) instance.ReleaseMutex();
        if (ElevationBroker.IsAdministrator) InstallMachine(target);
        else installationNotice = ElevationBroker.Run("--elevated-install", target);
        string legacyNotice = InstallationScope.CleanLegacyUserRegistration(target, RemoveInstallation);
        if (!String.IsNullOrEmpty(legacyNotice)) installationNotice += "\n" + legacyNotice;
    }

    private static void InstallMachine(string targetDirectory)
    {
        if (!ElevationBroker.IsAdministrator) throw new UnauthorizedAccessException("การติดตั้งสำหรับทุกคนต้องได้รับสิทธิ์ผู้ดูแลระบบ");
        InstallAtScope(targetDirectory, InstallationScope.MachineShortcut, Registry.LocalMachine, RegistryKey);
    }

    private static void InstallAt(string targetDirectory, string shortcutPath, string registryPath)
    {
        InstallAtScope(targetDirectory, shortcutPath, Registry.CurrentUser, registryPath);
    }

    private static void InstallAtScope(string targetDirectory, string shortcutPath, RegistryKey registryRoot, string registryPath)
    {
        string target = InstallLocation.Normalize(targetDirectory);
        using (Mutex instance = AcquireInstance())
        {
            string stage = Path.Combine(target, ".setup-" + Guid.NewGuid().ToString("N"));
            string[] files = { "ServerBotTracker.exe", "ServerBotTracker.exe.config", "Uninstall.exe" };
            var applied = new System.Collections.Generic.List<string>();
            RegistryBackup registration = null;
            byte[] shortcutBefore = null;
            bool metadataTouched = false;
            bool keepStage = false;
            try
            {
                registration = RegistryBackup.Capture(registryRoot, registryPath);
                shortcutBefore = File.Exists(shortcutPath) ? File.ReadAllBytes(shortcutPath) : null;
                InstallLocation.ValidateProductFiles(target);
                var existing = new System.Collections.Generic.List<string>();
                foreach (string file in files) if (File.Exists(Path.Combine(target, file))) existing.Add(Path.Combine(target, file));
                if (shortcutBefore != null) existing.Add(shortcutPath);
                SetupFiles.CheckAvailable(existing);
                Directory.CreateDirectory(stage);
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count != 2) throw new InvalidDataException("ชุดติดตั้งไม่ถูกต้อง");
                    foreach (string file in files)
                    {
                        if (File.Exists(Path.Combine(target, file))) File.Copy(Path.Combine(target, file), Path.Combine(stage, file + ".bak"));
                        if (file == "Uninstall.exe") continue;
                        ZipArchiveEntry entry = archive.GetEntry(file);
                        if (entry == null || entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("ไฟล์ติดตั้งไม่ครบ");
                        entry.ExtractToFile(Path.Combine(stage, file));
                    }
                }
                File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(stage, "Uninstall.exe"));
                foreach (string file in files)
                {
                    applied.Add(file);
                    File.Copy(Path.Combine(stage, file), Path.Combine(target, file), true);
                }
                metadataTouched = true;
                DesktopShortcut.Create(shortcutPath, Path.Combine(target, "ServerBotTracker.exe"), target, Product);
                using (RegistryKey key = registryRoot.CreateSubKey(registryPath))
                {
                    key.SetValue("DisplayName", Product);
                    key.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(Path.Combine(target, "ServerBotTracker.exe")).FileVersion);
                    key.SetValue("Publisher", "Kuma");
                    key.SetValue("InstallLocation", target);
                    key.SetValue("DisplayIcon", Path.Combine(target, "ServerBotTracker.exe"));
                    key.SetValue("UninstallString", "\"" + Path.Combine(target, "Uninstall.exe") + "\" --uninstall");
                    key.SetValue("URLInfoAbout", "https://github.com/kumatech112/Server_Bot_Tracker");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("InstallDate", DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    long installedBytes = new FileInfo(Path.Combine(target, "ServerBotTracker.exe")).Length + new FileInfo(Path.Combine(target, "Uninstall.exe")).Length + new FileInfo(Path.Combine(target, "ServerBotTracker.exe.config")).Length;
                    key.SetValue("EstimatedSize", (int)((installedBytes + 1023) / 1024), RegistryValueKind.DWord);
                }
            }
            catch (Exception installationError)
            {
                var errors = new System.Collections.Generic.List<Exception> { installationError };
                if (metadataTouched)
                {
                    try { registration.Restore(registryRoot, registryPath); }
                    catch (Exception error) { errors.Add(error); }
                    try
                    {
                        if (shortcutBefore == null) File.Delete(shortcutPath);
                        else File.WriteAllBytes(shortcutPath, shortcutBefore);
                    }
                    catch (Exception error) { errors.Add(error); }
                }
                foreach (string file in applied)
                {
                    string backup = Path.Combine(stage, file + ".bak");
                    try
                    {
                        if (File.Exists(backup))
                        {
                            if (!SetupFiles.SameContents(Path.Combine(target, file), backup)) File.Copy(backup, Path.Combine(target, file), true);
                        }
                        else File.Delete(Path.Combine(target, file));
                    }
                    catch (Exception error) { errors.Add(error); }
                }
                if (errors.Count > 1) { keepStage = true; throw new AggregateException("ติดตั้งไม่สำเร็จและคืนค่าได้ไม่ครบ กรุณาเก็บโฟลเดอร์สำรองไว้: " + stage, errors); }
                throw;
            }
            finally
            {
                try
                {
                    if (!keepStage && Directory.Exists(stage))
                    {
                        foreach (string file in Directory.GetFiles(stage)) File.Delete(file);
                        Directory.Delete(stage, false);
                    }
                }
                catch (IOException) { installationNotice = "ติดตั้งโปรแกรมแล้ว แต่ยังมีไฟล์ชั่วคราวที่: " + stage; }
                catch (UnauthorizedAccessException) { installationNotice = "ติดตั้งโปรแกรมแล้ว แต่ยังมีไฟล์ชั่วคราวที่: " + stage; }
                finally { instance.ReleaseMutex(); }
            }
        }
    }

    private static void Remove(string targetDirectory)
    {
        removalNotice = String.Empty;
        using (Mutex instance = AcquireInstance()) instance.ReleaseMutex();
        if (InstallationScope.RequiresMachinePrivileges(targetDirectory))
        {
            if (ElevationBroker.IsAdministrator) RemoveMachine(targetDirectory);
            else removalNotice = ElevationBroker.Run("--elevated-remove", targetDirectory);
        }
        else RemoveInstallation(targetDirectory, Shortcut, RegistryKey);
    }

    private static void RemoveMachine(string targetDirectory)
    {
        if (!ElevationBroker.IsAdministrator) throw new UnauthorizedAccessException("การถอนโปรแกรมสำหรับทุกคนต้องได้รับสิทธิ์ผู้ดูแลระบบ");
        RemoveAtScope(targetDirectory, InstallationScope.MachineShortcut, Registry.LocalMachine, RegistryKey);
    }

    private static void RemoveInstallation(string targetDirectory, string shortcutPath, string registryPath)
    {
        RemoveAtScope(targetDirectory, shortcutPath, Registry.CurrentUser, registryPath);
    }

    private static void RemoveAtScope(string targetDirectory, string shortcutPath, RegistryKey registryRoot, string registryPath)
    {
        using (Mutex instance = AcquireInstance())
        {
            try
            {
                string target = InstallLocation.Normalize(targetDirectory);
                if (String.Equals(Path.GetFullPath(Assembly.GetExecutingAssembly().Location), Path.Combine(target, "Uninstall.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("ถอนการติดตั้งต้องทำงานผ่านตัวช่วยชั่วคราว");
                removalNotice = UninstallTransaction.Remove(target, shortcutPath, registryRoot, registryPath);
            }
            finally { instance.ReleaseMutex(); }
        }
    }
}
