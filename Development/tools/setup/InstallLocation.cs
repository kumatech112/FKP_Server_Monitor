using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ServerBotTracker;

internal static class InstallLocation
{
    public static string DefaultDirectory
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FKP Server Monitor"); }
    }

    public static string Normalize(string directory)
    {
        if (String.IsNullOrWhiteSpace(directory)) throw new IOException("กรุณาเลือกโฟลเดอร์ติดตั้ง");
        string value = directory.Trim();
        bool drivePath = value.Length >= 3 && Char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/');
        bool networkPath = value.StartsWith(@"\\", StringComparison.Ordinal) && !value.StartsWith(@"\\?\", StringComparison.Ordinal) && !value.StartsWith(@"\\.\", StringComparison.Ordinal);
        if ((!drivePath && !networkPath) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new IOException("กรุณาระบุพาธเต็ม เช่น D:\\Programs\\FKP Server Monitor");
        string target;
        try { target = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception exception)
        {
            if (!(exception is ArgumentException) && !(exception is NotSupportedException) && !(exception is PathTooLongException)) throw;
            throw new IOException("พาธโฟลเดอร์ไม่ถูกต้อง", exception);
        }
        string root = Path.GetPathRoot(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (target.Length == 0 || String.Equals(target, root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("กรุณาเลือกโฟลเดอร์ย่อย ห้ามติดตั้งที่รากไดรฟ์หรือรากโฟลเดอร์แชร์");
        foreach (string part in target.Substring(root.Length).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal))
                throw new IOException("ชื่อโฟลเดอร์ไม่ถูกต้อง");
        if (File.Exists(target)) throw new IOException("ตำแหน่งที่เลือกเป็นไฟล์ กรุณาเลือกโฟลเดอร์");
        return target;
    }

    public static string ReadRemembered(string registryPath)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                string remembered = key == null ? null : key.GetValue("InstallLocation") as string;
                if (!String.IsNullOrWhiteSpace(remembered)) return Normalize(remembered);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
        return DefaultDirectory;
    }

    public static string ForDestination(string directory)
    {
        if (String.IsNullOrWhiteSpace(directory)) return Normalize(directory);
        string value = directory.Trim();
        if (value.Length == 2 && Char.IsLetter(value[0]) && value[1] == ':') value += "\\";
        try
        {
            string root = Path.GetPathRoot(value);
            if (!String.IsNullOrEmpty(root) && String.Equals(value.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return Normalize(Path.Combine(root, "FKP Server Monitor"));
        }
        catch (ArgumentException) { return Normalize(value); }
        return Normalize(value);
    }

    public static string ForBrowseSelection(string directory)
    {
        string value = directory.TrimEnd('\\', '/');
        if (String.Equals(Path.GetFileName(value), "FKP Server Monitor", StringComparison.OrdinalIgnoreCase)
            || File.Exists(Path.Combine(directory, ProductIdentity.LegacyApplicationFile))
            || File.Exists(Path.Combine(directory, "FKPServerMonitor.exe")) || File.Exists(Path.Combine(directory, "Uninstall.exe")))
            return ForDestination(directory);
        return Normalize(Path.Combine(directory, "FKP Server Monitor"));
    }

    public static bool Matches(string first, string second)
    {
        try { return String.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase); }
        catch (IOException) { return false; }
    }

    public static void ValidateProductFiles(string target)
    {
        foreach (string name in new[] { "FKPServerMonitor.exe", ProductIdentity.LegacyApplicationFile, "Uninstall.exe" })
        {
            string file = Path.Combine(target, name);
            if (Directory.Exists(file)) throw new IOException("โฟลเดอร์นี้มีรายการชื่อซ้ำกับไฟล์โปรแกรม กรุณาเลือกโฟลเดอร์อื่น");
            if (File.Exists(file) && !ProductIdentity.IsKnownProduct(FileVersionInfo.GetVersionInfo(file).ProductName))
                throw new IOException("โฟลเดอร์นี้มีไฟล์ของโปรแกรมอื่นชื่อ " + name + " กรุณาเลือกโฟลเดอร์อื่น");
        }
        foreach (string application in new[] { ProductIdentity.ApplicationFile, ProductIdentity.LegacyApplicationFile })
        {
            string configuration = Path.Combine(target, application + ".config");
            if (Directory.Exists(configuration) || (File.Exists(configuration) && !File.Exists(Path.Combine(target, application))))
                throw new IOException("โฟลเดอร์นี้มีไฟล์ตั้งค่าชื่อซ้ำ กรุณาเลือกโฟลเดอร์อื่น");
        }
    }
}
