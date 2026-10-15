using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

internal static class SetupFiles
{
    public static void CheckAvailable(IEnumerable<string> files)
    {
        var handles = new List<FileStream>();
        try
        {
            foreach (string file in files)
            {
                if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) throw new IOException("ไฟล์เป็นแบบอ่านอย่างเดียว: " + file);
                handles.Add(File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
            }
        }
        catch (Exception error) { throw new IOException("ยังไม่ได้เปลี่ยนไฟล์โปรแกรม: มีไฟล์ถูกใช้งานหรือไม่มีสิทธิ์แก้ไข กรุณาปิดโปรแกรมแล้วลองอีกครั้ง\n" + error.Message, error); }
        finally { foreach (FileStream handle in handles) handle.Dispose(); }
    }

    public static bool SameContents(string first, string second)
    {
        if (!File.Exists(first) || !File.Exists(second)) return false;
        using (var hash = SHA256.Create())
        using (Stream original = File.OpenRead(first))
        using (Stream backup = File.OpenRead(second))
            return original.Length == backup.Length && BitConverter.ToString(hash.ComputeHash(original)) == BitConverter.ToString(hash.ComputeHash(backup));
    }
}
