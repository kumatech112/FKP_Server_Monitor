using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    private static string Target { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ServerBotTracker"); } }
    private static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Server Bot Tracker.lnk"); } }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--remove")
            {
                Remove();
                return 0;
            }
            if (args.Length == 1 && args[0] == "--uninstall")
            {
                Application.EnableVisualStyles();
                if (MessageBox.Show("ถอนการติดตั้ง Server Bot Tracker?\nรายการเซิร์ฟเวอร์และข้อมูลผู้ใช้จะยังคงอยู่", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                string helper = Path.Combine(Path.GetTempPath(), "ServerBotTracker-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Assembly.GetExecutingAssembly().Location, helper);
                Process.Start(new ProcessStartInfo(helper, "--remove") { UseShellExecute = false, CreateNoWindow = true });
                return 0;
            }
            if (args.Length == 1 && args[0] == "/silent")
            {
                Install();
                return 0;
            }
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

    private static void Install()
    {
        using (Mutex instance = AcquireInstance())
        {
            Directory.CreateDirectory(Target);
            string stage = Path.Combine(Target, ".setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string[] files = { "ServerBotTracker.exe", "ServerBotTracker.exe.config" };
            var applied = new System.Collections.Generic.List<string>();
            try
            {
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count != 2) throw new InvalidDataException("ชุดติดตั้งไม่ถูกต้อง");
                    foreach (string file in files)
                    {
                        ZipArchiveEntry entry = archive.GetEntry(file);
                        if (entry == null || entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("ไฟล์ติดตั้งไม่ครบ");
                        entry.ExtractToFile(Path.Combine(stage, file));
                        if (File.Exists(Path.Combine(Target, file))) File.Copy(Path.Combine(Target, file), Path.Combine(stage, file + ".bak"));
                    }
                }
                foreach (string file in files)
                {
                    File.Copy(Path.Combine(stage, file), Path.Combine(Target, file), true);
                    applied.Add(file);
                }
                File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(Target, "Uninstall.exe"), true);
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(Shortcut);
                shortcut.TargetPath = Path.Combine(Target, "ServerBotTracker.exe");
                shortcut.IconLocation = Path.Combine(Target, "ServerBotTracker.exe") + ",0";
                shortcut.WorkingDirectory = Target;
                shortcut.Description = Product;
                shortcut.Save();
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                {
                    key.SetValue("DisplayName", Product);
                    key.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(Path.Combine(Target, "ServerBotTracker.exe")).FileVersion);
                    key.SetValue("Publisher", "Kuma");
                    key.SetValue("InstallLocation", Target);
                    key.SetValue("DisplayIcon", Path.Combine(Target, "ServerBotTracker.exe"));
                    key.SetValue("UninstallString", "\"" + Path.Combine(Target, "Uninstall.exe") + "\" --uninstall");
                    key.SetValue("URLInfoAbout", "https://github.com/kumatech112/Server_Bot_Tracker");
                }
            }
            catch
            {
                foreach (string file in applied)
                {
                    string backup = Path.Combine(stage, file + ".bak");
                    if (File.Exists(backup)) File.Copy(backup, Path.Combine(Target, file), true);
                    else File.Delete(Path.Combine(Target, file));
                }
                throw;
            }
            finally
            {
                foreach (string file in Directory.GetFiles(stage)) File.Delete(file);
                Directory.Delete(stage, false);
                instance.ReleaseMutex();
            }
        }
    }

    private static void Remove()
    {
        using (Mutex instance = AcquireInstance())
        {
            if (String.Equals(Path.GetFullPath(Assembly.GetExecutingAssembly().Location), Path.Combine(Target, "Uninstall.exe"), StringComparison.OrdinalIgnoreCase))
                throw new IOException("ถอนการติดตั้งต้องทำงานผ่านตัวช่วยชั่วคราว");
            foreach (string file in new[] { "ServerBotTracker.exe", "ServerBotTracker.exe.config", "Uninstall.exe" })
                if (File.Exists(Path.Combine(Target, file))) File.Delete(Path.Combine(Target, file));
            if (Directory.Exists(Target) && Directory.GetFileSystemEntries(Target).Length == 0) Directory.Delete(Target, false);
            if (File.Exists(Shortcut)) File.Delete(Shortcut);
            Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false);
            instance.ReleaseMutex();
        }
    }
}
