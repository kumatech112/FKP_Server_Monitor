using System;

namespace ServerBotTracker
{
    // Public branding can change independently of identifiers used by older installations.
    public static class ProductIdentity
    {
        public const string DisplayName = "FKP Server Monitor";
        public const string LegacyDisplayName = "Server Bot Tracker";
        public const string ApplicationFile = "FKPServerMonitor.exe";
        public const string ConfigurationFile = "FKPServerMonitor.exe.config";
        public const string LegacyApplicationFile = "ServerBotTracker.exe";
        public const string LegacyConfigurationFile = "ServerBotTracker.exe.config";
        public const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker";

        public static bool IsKnownProduct(string name)
        {
            return String.Equals(name, DisplayName, StringComparison.Ordinal)
                || String.Equals(name, LegacyDisplayName, StringComparison.Ordinal);
        }
    }
}
