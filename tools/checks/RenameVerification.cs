using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using ServerBotTracker;

internal static class RenameVerification
{
    public static void Run(Assembly setup, string root, Action<bool, string> assert)
    {
        assert(FileVersionInfo.GetVersionInfo(Path.Combine(root, ReleaseChannel.ApplicationFile)).ProductName == ProductIdentity.DisplayName
            && FileVersionInfo.GetVersionInfo(Path.Combine(root, "FKPServerMonitor-Setup.exe")).CompanyName == "FKP",
            "Application and installer expose the new product and publisher metadata");
        string expectedSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuma", "ServerBotTracker", "servers.json");
        assert(ConfigurationStore.ForCurrentUser().FilePath == expectedSettings, "Renaming retains the existing server list and notification storage location");
        ReleaseManifest legacy = JsonData.Read<ReleaseManifest>(Path.Combine(root, "legacy-update.json"));
        legacy.Validate();
        assert(legacy.Version == "1.6.0.0" && legacy.Tag == "v1.6.0" && legacy.SizeBytes == 363177
            && legacy.Sha256 == "D2B54C6EBEC89C0D74317BA55DB8636573FDE763EB95C5F3A867EBB66C75DA54",
            "The legacy update feed retains the original compatible public package");
        VerifyMigration(setup, root, assert);
        VerifyRollback(setup, root, assert);
    }

    private static void CreateLegacy(string target, string shortcut, string registry, string root)
    {
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(root, "LegacyVersion.exe"), Path.Combine(target, ProductIdentity.LegacyApplicationFile));
        File.Copy(Path.Combine(root, "FKPServerMonitor.exe.config"), Path.Combine(target, ProductIdentity.LegacyConfigurationFile));
        File.Copy(Path.Combine(root, "LegacyVersion.exe"), Path.Combine(target, "Uninstall.exe"));
        File.WriteAllText(Path.Combine(target, "keep-user-data.txt"), "Keep existing data");
        DesktopShortcut.Create(shortcut, Path.Combine(target, ProductIdentity.LegacyApplicationFile), target, ProductIdentity.LegacyDisplayName);
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry))
        {
            key.SetValue("DisplayName", ProductIdentity.LegacyDisplayName);
            key.SetValue("InstallLocation", target);
            key.SetValue("DisplayVersion", "1.6.0.0");
        }
    }

    private static void VerifyMigration(Assembly setup, string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "rename-migration", ProductIdentity.DisplayName);
        string shortcut = Path.Combine(root, "rename-migration", ProductIdentity.DisplayName + ".lnk");
        string legacyShortcut = Path.Combine(Path.GetDirectoryName(shortcut), ProductIdentity.LegacyDisplayName + ".lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root) + "-rename";
        Type engine = setup.GetType("Setup", true);
        MethodInfo install = engine.GetMethod("InstallAt", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo remove = engine.GetMethod("RemoveInstallation", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            CreateLegacy(target, legacyShortcut, registry, root);
            bool blocked = false;
            using (FileStream locked = File.Open(Path.Combine(target, ProductIdentity.LegacyConfigurationFile), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { install.Invoke(null, new object[] { target, shortcut, registry }); }
                catch (TargetInvocationException error) { blocked = error.InnerException is IOException; }
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(blocked && File.Exists(legacyShortcut) && !File.Exists(Path.Combine(target, ProductIdentity.ApplicationFile))
                    && (string)key.GetValue("DisplayName") == ProductIdentity.LegacyDisplayName,
                    "A locked legacy config blocks migration before files, shortcuts or registration change");
            install.Invoke(null, new object[] { target, shortcut, registry });
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(File.Exists(Path.Combine(target, ProductIdentity.ApplicationFile)) && File.Exists(shortcut)
                    && !File.Exists(legacyShortcut) && !File.Exists(Path.Combine(target, ProductIdentity.LegacyApplicationFile))
                    && !File.Exists(Path.Combine(target, ProductIdentity.LegacyConfigurationFile))
                    && (string)key.GetValue("DisplayName") == ProductIdentity.DisplayName
                    && (string)key.GetValue("Publisher") == "FKP"
                    && File.ReadAllText(Path.Combine(target, "keep-user-data.txt")) == "Keep existing data",
                    "Setup replaces the old executable and shortcut, renames Control Panel metadata and preserves user data");
            remove.Invoke(null, new object[] { target, shortcut, registry });
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(key == null && !File.Exists(shortcut) && !File.Exists(Path.Combine(target, ProductIdentity.ApplicationFile))
                    && File.Exists(Path.Combine(target, "keep-user-data.txt")),
                    "The migrated installation uninstalls cleanly while retaining unrelated data");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
    }

    private static void VerifyRollback(Assembly setup, string root, Action<bool, string> assert)
    {
        string target = Path.Combine(root, "rename-rollback", ProductIdentity.DisplayName);
        string shortcut = Path.Combine(root, "rename-rollback", ProductIdentity.DisplayName + ".lnk");
        string legacyShortcut = Path.Combine(Path.GetDirectoryName(shortcut), ProductIdentity.LegacyDisplayName + ".lnk");
        string registry = @"Software\Kuma\ServerBotTrackerDevelopment\" + Path.GetFileName(root) + "-rename-rollback";
        Type engine = setup.GetType("Setup", true);
        MethodInfo install = engine.GetMethod("InstallAt", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            CreateLegacy(target, legacyShortcut, registry, root);
            // An existing directory obstructs shortcut creation after old files have been staged.
            Directory.CreateDirectory(shortcut);
            bool failed = false;
            try { install.Invoke(null, new object[] { target, shortcut, registry }); }
            catch (TargetInvocationException) { failed = true; }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                assert(failed && File.Exists(legacyShortcut) && File.Exists(Path.Combine(target, ProductIdentity.LegacyApplicationFile))
                    && File.Exists(Path.Combine(target, ProductIdentity.LegacyConfigurationFile))
                    && FileVersionInfo.GetVersionInfo(Path.Combine(target, "Uninstall.exe")).ProductName == ProductIdentity.LegacyDisplayName
                    && !File.Exists(Path.Combine(target, ProductIdentity.ApplicationFile))
                    && Directory.GetDirectories(target, ".setup-*").Length == 0 && Directory.Exists(shortcut)
                    && (string)key.GetValue("DisplayName") == ProductIdentity.LegacyDisplayName,
                    "A failure after staging restores the old executable, config, uninstaller, shortcut and registration");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registry, false); }
    }
}
