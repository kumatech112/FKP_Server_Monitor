using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Setup
{
    private const string Product = "Server Bot Tracker";
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker";
    private static string Target { get { return InstallLocation.ReadRemembered(RegistryKey); } }
    private static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Server Bot Tracker.lnk"); } }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--uninstall")
            {
                StartUninstallWizard();
                return 0;
            }
            if (args.Length == 4 && args[0] == "--uninstall-wizard")
            {
                WaitForLauncher(args[1], args[2]);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new SetupForm(InstallLocation.Normalize(args[3]), Remove, true))
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
            using (var form = new SetupForm(Target, Install))
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
        InstallAt(targetDirectory, Shortcut, RegistryKey);
    }

    private static void InstallAt(string targetDirectory, string shortcutPath, string registryPath)
    {
        string target = InstallLocation.Normalize(targetDirectory);
        using (Mutex instance = AcquireInstance())
        {
            string stage = Path.Combine(target, ".setup-" + Guid.NewGuid().ToString("N"));
            string[] files = { "ServerBotTracker.exe", "ServerBotTracker.exe.config", "Uninstall.exe" };
            var applied = new System.Collections.Generic.List<string>();
            try
            {
                InstallLocation.ValidateProductFiles(target);
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
                    File.Copy(Path.Combine(stage, file), Path.Combine(target, file), true);
                    applied.Add(file);
                }
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = Path.Combine(target, "ServerBotTracker.exe");
                shortcut.IconLocation = Path.Combine(target, "ServerBotTracker.exe") + ",0";
                shortcut.WorkingDirectory = target;
                shortcut.Description = Product;
                shortcut.Save();
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath))
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
            catch
            {
                foreach (string file in applied)
                {
                    string backup = Path.Combine(stage, file + ".bak");
                    if (File.Exists(backup)) File.Copy(backup, Path.Combine(target, file), true);
                    else File.Delete(Path.Combine(target, file));
                }
                throw;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stage))
                    {
                        foreach (string file in Directory.GetFiles(stage)) File.Delete(file);
                        Directory.Delete(stage, false);
                    }
                }
                finally { instance.ReleaseMutex(); }
            }
        }
    }

    private static void Remove(string targetDirectory)
    {
        RemoveInstallation(targetDirectory, Shortcut, RegistryKey);
    }

    private static void RemoveInstallation(string targetDirectory, string shortcutPath, string registryPath)
    {
        using (Mutex instance = AcquireInstance())
        {
            try
            {
                string target = InstallLocation.Normalize(targetDirectory);
                if (String.Equals(Path.GetFullPath(Assembly.GetExecutingAssembly().Location), Path.Combine(target, "Uninstall.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("ถอนการติดตั้งต้องทำงานผ่านตัวช่วยชั่วคราว");
                foreach (string file in new[] { "ServerBotTracker.exe", "ServerBotTracker.exe.config", "Uninstall.exe" })
                    if (File.Exists(Path.Combine(target, file))) File.Delete(Path.Combine(target, file));
                if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length == 0) Directory.Delete(target, false);
                bool ownsRegistration;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                    ownsRegistration = key != null && InstallLocation.Matches(key.GetValue("InstallLocation") as string, target);
                if (ownsRegistration)
                {
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                    Registry.CurrentUser.DeleteSubKeyTree(registryPath, false);
                }
            }
            finally { instance.ReleaseMutex(); }
        }
    }
}
