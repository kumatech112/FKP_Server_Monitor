using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

internal sealed class RegistryBackup
{
    private bool existed;
    private readonly Dictionary<string, object> values = new Dictionary<string, object>();
    private readonly Dictionary<string, RegistryValueKind> kinds = new Dictionary<string, RegistryValueKind>();

    public static RegistryBackup Capture(RegistryKey root, string path)
    {
        var backup = new RegistryBackup();
        using (RegistryKey key = root.OpenSubKey(path, true))
        {
            if (key == null) return backup;
            if (key.SubKeyCount != 0) throw new IOException("รายการโปรแกรมมีข้อมูลย่อยที่ไม่รู้จัก จึงยังไม่เปลี่ยนไฟล์โปรแกรม");
            backup.existed = true;
            foreach (string name in key.GetValueNames())
            {
                backup.values.Add(name, key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                backup.kinds.Add(name, key.GetValueKind(name));
            }
        }
        return backup;
    }

    public bool MatchesTarget(string target)
    {
        object value;
        return existed && values.TryGetValue("InstallLocation", out value) && InstallLocation.Matches(value as string, target);
    }

    public void Restore(RegistryKey root, string path)
    {
        root.DeleteSubKeyTree(path, false);
        if (!existed) return;
        using (RegistryKey key = root.CreateSubKey(path))
            foreach (KeyValuePair<string, object> value in values) key.SetValue(value.Key, value.Value, kinds[value.Key]);
    }
}
