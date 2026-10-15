using System;
using System.IO;
using Microsoft.Win32;
using ServerBotTracker;

internal static class InstallationScope
{
    public const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker";

    public static string MachineShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Server Bot Tracker.lnk"); }
    }

    public static string UserShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Server Bot Tracker.lnk"); }
    }

    public static string InitialDirectory
    {
        get { return ReadMachineDirectory() ?? InstallLocation.DefaultDirectory; }
    }

    public static bool IsMachineTarget(string target)
    {
        return InstallLocation.Matches(ReadMachineDirectory(), target);
    }

    public static bool RequiresMachinePrivileges(string target)
    {
        if (IsMachineTarget(target)) return true;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            if (key != null && InstallLocation.Matches(key.GetValue("InstallLocation") as string, target)) return false;
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\') + "\\";
        return InstallLocation.Normalize(target).StartsWith(programFiles, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMachineDirectory()
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryPath))
            {
                if (key == null || !String.Equals(key.GetValue("DisplayName") as string, "Server Bot Tracker", StringComparison.Ordinal)) return null;
                string value = key.GetValue("InstallLocation") as string;
                return String.IsNullOrWhiteSpace(value) ? null : InstallLocation.Normalize(value);
            }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (System.Security.SecurityException) { return null; }
    }

    public static string CleanLegacyUserRegistration(string installedTarget, Action<string, string, string> remove)
    {
        return CleanLegacyRegistration(installedTarget, Registry.CurrentUser, RegistryPath, UserShortcut, remove);
    }

    public static string CleanLegacyRegistration(string installedTarget, RegistryKey registryRoot, string registryPath, string shortcut, Action<string, string, string> remove)
    {
        string legacy = String.Empty;
        try
        {
            using (RegistryKey key = registryRoot.OpenSubKey(registryPath))
            {
                if (key == null || !String.Equals(key.GetValue("DisplayName") as string, "Server Bot Tracker", StringComparison.Ordinal)) return String.Empty;
                legacy = key.GetValue("InstallLocation") as string;
            }
            string target = InstallLocation.Normalize(legacy);
            if (InstallLocation.Matches(target, installedTarget))
            {
                if (File.Exists(shortcut)) File.Delete(shortcut);
                registryRoot.DeleteSubKeyTree(registryPath, false);
            }
            else
            {
                InstallLocation.ValidateProductFiles(target);
                remove(target, shortcut, registryPath);
            }
            return String.Empty;
        }
        catch (Exception error) { return "ติดตั้งรุ่นใหม่แล้ว แต่ยังเก็บสำเนาเดิมไว้ที่ " + legacy + "\n" + error.Message; }
    }
}
