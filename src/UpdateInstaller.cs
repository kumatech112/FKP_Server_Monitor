using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.Win32;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class UpdateJob
    {
        [DataMember] public string TargetDirectory { get; set; }
        [DataMember] public int ParentId { get; set; }
        [DataMember] public long ParentStartedUtcTicks { get; set; }
        [DataMember] public bool Restart { get; set; }
        [DataMember] public bool ResumeMonitoring { get; set; }
    }

    public static class UpdateInstaller
    {
        public static void LaunchWorker(PreparedUpdate prepared, string targetDirectory, bool restart, bool resumeMonitoring)
        {
            string worker = Path.Combine(prepared.DirectoryPath, "UpdateWorker.exe");
            File.Copy(typeof(UpdateInstaller).Assembly.Location, worker, true);
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ReleaseChannel.ConfigurationFile), worker + ".config", true);
            using (Process parent = Process.GetCurrentProcess())
            {
                var job = new UpdateJob { TargetDirectory = Path.GetFullPath(targetDirectory), ParentId = parent.Id, ParentStartedUtcTicks = parent.StartTime.ToUniversalTime().Ticks, Restart = restart, ResumeMonitoring = resumeMonitoring };
                string jobPath = Path.Combine(prepared.DirectoryPath, "job.json");
                JsonData.WriteAtomic(jobPath, job);
                Process.Start(new ProcessStartInfo(worker, "--apply-update \"" + jobPath + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = prepared.DirectoryPath });
            }
        }

        public static int RunWorker(string jobPath)
        {
            string stage = Path.GetDirectoryName(Path.GetFullPath(jobPath));
            string resultPath = Path.Combine(stage, "result.json");
            UpdateJob job = null;
            bool restartAllowed = false;
            try
            {
                if (Path.GetDirectoryName(typeof(UpdateInstaller).Assembly.Location) != stage || Path.GetFileName(jobPath) != "job.json")
                    throw new InvalidDataException("Update worker must run beside its prepared job");
                job = JsonData.Read<UpdateJob>(jobPath);
                string existing = Path.Combine(job.TargetDirectory, ReleaseChannel.ApplicationFile);
                restartAllowed = File.Exists(existing) && FileVersionInfo.GetVersionInfo(existing).ProductName == "FKP Server Monitor";
                if (!restartAllowed) throw new InvalidDataException("ไม่พบโปรแกรมเดิมที่ถูกต้องในตำแหน่งอัปเดต");
                try
                {
                    using (Process parent = Process.GetProcessById(job.ParentId))
                    {
                        if (parent.StartTime.ToUniversalTime().Ticks != job.ParentStartedUtcTicks || !parent.WaitForExit(60000))
                            throw new IOException("โปรแกรมเดิมยังไม่ปิด จึงยังไม่ติดตั้งอัปเดต");
                    }
                }
                catch (ArgumentException) { /* The parent has already exited. */ }
                if (RequiresElevation(job.TargetDirectory) && !ElevationBroker.IsAdministrator)
                    ElevationBroker.Run("--apply-update-admin", jobPath);
                else Apply(stage, job.TargetDirectory);
                JsonData.WriteAtomic(resultPath, "อัปเดตสำเร็จ");
                return 0;
            }
            catch (Exception exception)
            {
                JsonData.WriteAtomic(resultPath, "อัปเดตไม่สำเร็จ: " + exception.Message);
                try
                {
                    string data = Path.GetDirectoryName(ConfigurationStore.ForCurrentUser().FilePath);
                    new NotificationCenter(Path.Combine(data, "notifications.json")).Add("update-failed:" + Guid.NewGuid().ToString("N"), "ยังติดตั้งอัปเดตไม่ได้", exception.Message, "update", false);
                }
                catch (Exception) { /* The stage result still records the failure if inbox storage is unavailable. */ }
                return 1;
            }
            finally
            {
                if (job != null && job.Restart && restartAllowed)
                {
                    try { Process.Start(new ProcessStartInfo(Path.Combine(job.TargetDirectory, ReleaseChannel.ApplicationFile), job.ResumeMonitoring ? "--resume-monitoring" : "") { UseShellExecute = true, WorkingDirectory = job.TargetDirectory }); }
                    catch (Exception) { /* A completed update remains installed if Windows cannot restart its window. */ }
                }
            }
        }

        public static bool RequiresElevation(string targetDirectory)
        {
            string target = Path.GetFullPath(targetDirectory).TrimEnd('\\', '/');
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker"))
                if (key != null && MatchesInstallLocation(key.GetValue("InstallLocation") as string, target)) return true;
            foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string root = Environment.GetFolderPath(folder);
                if (!String.IsNullOrEmpty(root) && target.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void ApplyElevated(string jobPath)
        {
            string stage = Path.GetDirectoryName(Path.GetFullPath(jobPath));
            if (!ElevationBroker.IsAdministrator || Path.GetDirectoryName(typeof(UpdateInstaller).Assembly.Location) != stage || Path.GetFileName(jobPath) != "job.json")
                throw new InvalidDataException("ตำแหน่งหรือสิทธิ์ตัวอัปเดตไม่ถูกต้อง");
            UpdateJob job = JsonData.Read<UpdateJob>(jobPath);
            bool registered = false;
            foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker"))
                if (key != null && MatchesInstallLocation(key.GetValue("InstallLocation") as string, job.TargetDirectory)) registered = true;
            if (!registered) throw new InvalidDataException("ไม่พบรายการติดตั้งที่ตรงกับตำแหน่งอัปเดต กรุณาเปิด Setup รุ่นล่าสุด");
            ReleaseManifest prepared = JsonData.Read<ReleaseManifest>(Path.Combine(stage, "manifest.json"));
            using (var channel = new ReleaseChannel())
            {
                ReleaseManifest official = channel.GetLatestAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                VerifyOfficialRelease(prepared, official);
            }
            Apply(stage, job.TargetDirectory);
        }

        public static void VerifyOfficialRelease(ReleaseManifest prepared, ReleaseManifest official)
        {
            prepared.Validate();
            official.Validate();
            if (prepared.Version != official.Version || prepared.Tag != official.Tag || prepared.SizeBytes != official.SizeBytes || !String.Equals(prepared.Sha256, official.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ชุดอัปเดตไม่ตรงกับรุ่นทางการล่าสุด กรุณาดาวน์โหลดอัปเดตใหม่");
        }

        public static void Apply(string stage, string targetDirectory)
        {
            ReleaseManifest manifest = JsonData.Read<ReleaseManifest>(Path.Combine(stage, "manifest.json"));
            manifest.Validate();
            string target = Path.GetFullPath(targetDirectory);
            string executable = Path.Combine(target, ReleaseChannel.ApplicationFile);
            if (!File.Exists(executable))
                throw new InvalidDataException("ไม่พบโปรแกรมเดิมในตำแหน่งที่ต้องการอัปเดต");
            var current = FileVersionInfo.GetVersionInfo(executable);
            if (current.ProductName != "FKP Server Monitor" || ReleaseChannel.ParseVersion(current.FileVersion) >= ReleaseChannel.ParseVersion(manifest.Version))
                throw new InvalidDataException("ตำแหน่งโปรแกรมหรือหมายเลขรุ่นไม่ถูกต้อง");
            string payload = Path.Combine(stage, "verified-" + Guid.NewGuid().ToString("N"));
            ReleaseChannel.ExtractPackage(Path.Combine(stage, "package.zip"), payload, manifest);
            string backup = Path.Combine(stage, "backup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            string[] files = { ReleaseChannel.ApplicationFile, ReleaseChannel.ConfigurationFile };
            foreach (string file in files)
            {
                if (!File.Exists(Path.Combine(target, file)))
                    throw new InvalidDataException("ไฟล์โปรแกรมเดิมไม่ครบ");
                File.Copy(Path.Combine(target, file), Path.Combine(backup, file), false);
            }
            var applied = new List<string>();
            var staged = new List<string>();
            try
            {
                foreach (string file in files)
                {
                    // File.Replace requires source and target to be on the same volume.
                    string sibling = Path.Combine(target, file + ".new-" + Guid.NewGuid().ToString("N"));
                    staged.Add(sibling);
                    File.Copy(Path.Combine(payload, file), sibling, false);
                    File.Replace(sibling, Path.Combine(target, file), null, true);
                    applied.Add(file);
                }
            }
            catch (Exception installationError)
            {
                var errors = new List<Exception> { installationError };
                foreach (string file in applied)
                {
                    try { File.Copy(Path.Combine(backup, file), Path.Combine(target, file), true); }
                    catch (Exception rollbackError) { errors.Add(rollbackError); }
                }
                throw new AggregateException("ติดตั้งไม่สำเร็จ • มีสำรองโปรแกรมเดิมที่ " + backup, errors);
            }
            finally
            {
                foreach (string file in staged)
                    if (File.Exists(file)) File.Delete(file);
            }
            // Keep Windows' installed-app version in sync, without touching a different installation.
            try
            {
                foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
                {
                    bool matches;
                    using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker"))
                        matches = key != null && MatchesInstallLocation(key.GetValue("InstallLocation") as string, target);
                    if (!matches) continue;
                    using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker", true))
                        if (key != null) key.SetValue("DisplayVersion", manifest.Version);
                }
            }
            catch (UnauthorizedAccessException) { /* The updated program remains usable if Windows metadata is read-only. */ }
            catch (System.Security.SecurityException) { /* Do not fail a verified binary update for optional display metadata. */ }
            catch (IOException) { /* Registry display metadata cannot turn a committed binary update into a failure. */ }
        }

        public static bool MatchesInstallLocation(string registered, string target)
        {
            if (String.IsNullOrWhiteSpace(registered) || String.IsNullOrWhiteSpace(target)) return false;
            try
            {
                string first = Path.GetFullPath(registered).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string second = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return String.Equals(first, second, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }
    }
}
