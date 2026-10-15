using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text;

namespace ServerBotTracker
{
    public static class ElevationBroker
    {
        public static bool IsAdministrator
        {
            get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
        }

        public static string Run(string mode, string value)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ServerBotTracker-elevation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                if (mode.IndexOf('"') >= 0 || value.IndexOf('"') >= 0) throw new IOException("พารามิเตอร์ขอสิทธิ์ไม่ถูกต้อง");
                string executable = Assembly.GetExecutingAssembly().Location;
                if (mode == "--elevated-install" || mode == "--elevated-remove")
                {
                    string worker = Path.Combine(directory, "ServerBotTracker-SetupWorker.exe");
                    File.Copy(executable, worker);
                    if (File.Exists(executable + ".config")) File.Copy(executable + ".config", worker + ".config");
                    executable = worker;
                }
                var start = new ProcessStartInfo(executable,
                    mode + " \"" + value + "\" \"" + directory + "\"") { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(executable) };
                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                    string resultFile = Path.Combine(directory, "result.txt");
                    string result = String.Empty;
                    try { if (File.Exists(resultFile)) result = File.ReadAllText(resultFile, Encoding.UTF8); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    if (process.ExitCode != 0)
                        throw new IOException(result.StartsWith("ERROR\n", StringComparison.Ordinal) ? result.Substring(6) : "ขั้นตอนที่ขอสิทธิ์ผู้ดูแลระบบไม่สำเร็จ");
                    return result.StartsWith("OK\n", StringComparison.Ordinal) ? result.Substring(3) : "ดำเนินการเสร็จสมบูรณ์ แต่ไม่สามารถอ่านรายละเอียดผลลัพธ์ได้";
                }
            }
            catch (Win32Exception error)
            {
                if (error.NativeErrorCode == 1223) throw new IOException("ยกเลิกการอนุญาตสิทธิ์ผู้ดูแลระบบ จึงยังไม่ได้เปลี่ยนไฟล์โปรแกรม", error);
                throw;
            }
            finally
            {
                try
                {
                    string resultFile = Path.Combine(directory, "result.txt");
                    if (File.Exists(resultFile)) File.Delete(resultFile);
                    foreach (string name in new[] { "ServerBotTracker-SetupWorker.exe", "ServerBotTracker-SetupWorker.exe.config" })
                    {
                        string file = Path.Combine(directory, name);
                        if (File.Exists(file)) File.Delete(file);
                    }
                    if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory, false);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static int Complete(string directory, Action operation, Func<string> notice)
        {
            string full = Path.GetFullPath(directory).TrimEnd('\\', '/');
            string name = Path.GetFileName(full);
            Guid identity;
            const string prefix = "ServerBotTracker-elevation-";
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name.Substring(prefix.Length), "N", out identity)
                || !Directory.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                return 1;
            int outcome = 1;
            try
            {
                using (var stream = new FileStream(Path.Combine(full, "result.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    byte[] pending = Encoding.UTF8.GetBytes("PENDING\n");
                    stream.Write(pending, 0, pending.Length);
                    stream.Flush(true);
                    string result;
                    try
                    {
                        if (!IsAdministrator) throw new UnauthorizedAccessException("ต้องได้รับสิทธิ์ผู้ดูแลระบบก่อนดำเนินการ");
                        operation();
                        outcome = 0;
                        string message = String.Empty;
                        try { message = notice == null ? String.Empty : notice(); }
                        catch (Exception) { message = "ดำเนินการเสร็จแล้ว แต่ไม่สามารถอ่านรายละเอียดเพิ่มเติมได้"; }
                        result = "OK\n" + message;
                    }
                    catch (Exception error) { result = "ERROR\n" + error.Message; outcome = 1; }
                    try
                    {
                        byte[] contents = Encoding.UTF8.GetBytes(result);
                        stream.Position = 0;
                        stream.SetLength(0);
                        stream.Write(contents, 0, contents.Length);
                        stream.Flush(true);
                    }
                    catch (IOException) { /* Exit code remains authoritative after a committed operation. */ }
                    return outcome;
                }
            }
            catch (IOException) { return outcome; }
            catch (UnauthorizedAccessException) { return outcome; }
        }
    }
}
