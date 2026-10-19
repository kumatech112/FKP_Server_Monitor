using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using ServerBotTracker;

internal static class UninstallTransaction
{
    public static string Remove(string target, string shortcutPath, RegistryKey registryRoot, string registryPath)
    {
        var names = new List<string> { "FKPServerMonitor.exe", "FKPServerMonitor.exe.config", "Uninstall.exe" };
        string legacyExecutable = Path.Combine(target, ProductIdentity.LegacyApplicationFile);
        if (File.Exists(legacyExecutable))
        {
            if (!ProductIdentity.IsKnownProduct(System.Diagnostics.FileVersionInfo.GetVersionInfo(legacyExecutable).ProductName))
                throw new IOException("ยังไม่ได้ถอนการติดตั้ง: พบไฟล์ชื่อเดิมที่ไม่ใช่ของโปรแกรมนี้");
            names.Add(ProductIdentity.LegacyApplicationFile);
            names.Add(ProductIdentity.LegacyConfigurationFile);
        }
        var files = new List<string>();
        foreach (string name in names)
        {
            string file = Path.Combine(target, name);
            if (Directory.Exists(file)) throw new IOException("ยังไม่ได้ถอนการติดตั้ง: พบโฟลเดอร์ชื่อซ้ำกับไฟล์โปรแกรม " + name);
            if (File.Exists(file)) files.Add(file);
        }
        RegistryBackup registration = RegistryBackup.Capture(registryRoot, registryPath);
        if (!registration.MatchesTarget(target)) registration = null;
        bool removeShortcut = registration != null && File.Exists(shortcutPath);
        var checkedFiles = new List<string>(files);
        if (removeShortcut) checkedFiles.Add(shortcutPath);
        SetupFiles.CheckAvailable(checkedFiles);
        string stage = Path.Combine(target, ".uninstall-" + Guid.NewGuid().ToString("N"));
        var moved = new List<string>();
        bool shortcutRemoved = false;
        bool registryTouched = false;
        try
        {
            Directory.CreateDirectory(stage);
            if (removeShortcut) File.Copy(shortcutPath, Path.Combine(stage, "shortcut.bak"));
            foreach (string file in files)
            {
                File.Move(file, Path.Combine(stage, Path.GetFileName(file)));
                moved.Add(file);
            }
            if (removeShortcut) { File.Delete(shortcutPath); shortcutRemoved = true; }
            if (registration != null)
            {
                registryTouched = true;
                registryRoot.DeleteSubKeyTree(registryPath, false);
            }
        }
        catch (Exception removalError)
        {
            var errors = new List<Exception> { removalError };
            if (registryTouched)
            {
                try { registration.Restore(registryRoot, registryPath); }
                catch (Exception error) { errors.Add(error); }
            }
            if (shortcutRemoved)
            {
                try { File.Copy(Path.Combine(stage, "shortcut.bak"), shortcutPath, true); }
                catch (Exception error) { errors.Add(error); }
            }
            for (int index = moved.Count - 1; index >= 0; index--)
            {
                try { File.Move(Path.Combine(stage, Path.GetFileName(moved[index])), moved[index]); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 1)
                throw new AggregateException("ถอนการติดตั้งไม่สำเร็จและคืนไฟล์ได้ไม่ครบ กรุณาเก็บโฟลเดอร์สำรองนี้ไว้: " + stage, errors);
            CleanStage(stage);
            throw new IOException("ยังไม่ได้ถอนการติดตั้ง: คืนไฟล์และรายการโปรแกรมเดิมแล้ว\n" + removalError.Message, removalError);
        }
        string cleanupNotice = CleanStage(stage);
        try
        {
            if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length == 0) Directory.Delete(target, false);
        }
        catch (IOException) { cleanupNotice = "ถอนโปรแกรมแล้ว แต่ยังลบโฟลเดอร์ติดตั้งไม่ได้: " + target; }
        catch (UnauthorizedAccessException) { cleanupNotice = "ถอนโปรแกรมแล้ว แต่ยังลบโฟลเดอร์ติดตั้งไม่ได้: " + target; }
        return cleanupNotice;
    }

    private static string CleanStage(string stage)
    {
        try
        {
            if (Directory.Exists(stage))
            {
                foreach (string file in Directory.GetFiles(stage)) File.Delete(file);
                Directory.Delete(stage, false);
            }
            return String.Empty;
        }
        catch (IOException) { return "ถอนโปรแกรมแล้ว แต่ยังมีไฟล์ชั่วคราวที่: " + stage; }
        catch (UnauthorizedAccessException) { return "ถอนโปรแกรมแล้ว แต่ยังมีไฟล์ชั่วคราวที่: " + stage; }
    }

}
