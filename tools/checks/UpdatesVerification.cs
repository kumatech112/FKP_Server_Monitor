using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ServerBotTracker;

internal static class UpdatesVerification
{
    private sealed class ResponseHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            return Task.FromResult(Reply(request));
        }
    }

    private sealed class BlockingBody : Stream
    {
        private readonly TaskCompletionSource<int> pending = new TaskCompletionSource<int>();
        public bool WasDisposed;
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation) { return pending.Task; }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            pending.TrySetException(new ObjectDisposedException("Blocked response body"));
            base.Dispose(disposing);
        }
    }

    public static void Run(string root, Action<bool, string> assert)
    {
        VerifyNotifications(root, assert);
        VerifyChannel(root, assert);
        VerifyInstaller(root, assert);
    }

    private static void VerifyNotifications(string root, Action<bool, string> assert)
    {
        string path = Path.Combine(root, "notifications-fixture", "notifications.json");
        var center = new NotificationCenter(path);
        assert(center.Add("announcement:one", "ประกาศทดสอบ", "ข้อความภาษาไทย", "announcement", true) && !center.Add("announcement:one", "Repeated", "Repeated", "announcement", true), "Remote announcement is added once and not repeated");
        center.MarkAllRead();
        var reopened = new NotificationCenter(path);
        assert(reopened.State.Items.Count == 1 && reopened.State.Items[0].Read && !reopened.Add("announcement:one", "Repeated", "Repeated", "announcement", true), "Read status and announcement deduplication survive restart");
        File.WriteAllText(path, "corrupt inbox");
        var corrupt = new NotificationCenter(path);
        corrupt.Add("local", "Temporary", "Temporary", "down", false);
        assert(corrupt.StorageWarning != null && File.ReadAllText(path) == "corrupt inbox", "Corrupt inbox cannot break ping or overwrite original notification data");
        var health = new ServerAlertTracker();
        assert(health.Observe("one", false) == null && health.Observe("one", true) == null && health.Observe("one", false) == null && health.Observe("one", false) == "down", "Two consecutive failures trigger a single down transition");
        assert(health.Observe("one", false) == null && health.Observe("one", false) == null && health.Observe("two", true) == null, "Continued failures and unrelated servers do not repeat a down alert");
        assert(health.Observe("one", true) == "recovered" && health.Observe("one", true) == null, "Recovery is announced once after a confirmed down state");
        health.Forget("one");
        assert(health.Observe("one", true) == null, "Editing or deleting a server clears its alert baseline");
        var now = DateTime.UtcNow;
        var announcement = new Announcement { Id = "a", Title = "Thai", Message = "Message", PublishedUtc = now.AddHours(-1).ToString("o"), ExpiresUtc = now.AddHours(1).ToString("o") };
        var feed = new AnnouncementFeed { Schema = 1, Items = new System.Collections.Generic.List<Announcement> { announcement } };
        feed.Validate();
        assert(announcement.IsActive(now) && !announcement.IsActive(now.AddHours(2)) && !announcement.IsActive(now.AddHours(-2)), "Announcement publish and expiry windows use explicit UTC timestamps");
        feed.Items.Add(announcement);
        bool rejected = false;
        try { feed.Validate(); } catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Duplicate IDs in an announcement feed are rejected");
    }

    private static byte[] Serialize<T>(T value)
    {
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
            return stream.ToArray();
        }
    }

    private static void VerifyChannel(string root, Action<bool, string> assert)
    {
        using (var body = new BlockingBody())
        using (var cancellation = new CancellationTokenSource())
        using (var channel = new ReleaseChannel("test/releases", new ResponseHandler { Reply = request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) } }))
        {
            Task operation = channel.GetLatestAsync(cancellation.Token);
            cancellation.CancelAfter(100);
            bool completed = Task.WaitAny(new[] { operation }, 2000) == 0;
            if (!completed) body.Dispose();
            bool rejected = false;
            try { operation.GetAwaiter().GetResult(); } catch (Exception) { rejected = true; }
            assert(completed && rejected && body.WasDisposed, "Cancellation closes a stalled HTTP response body even when its stream ignores cancellation tokens");
        }
        string published = Path.Combine(root, "published-update.json");
        if (File.Exists(published))
        {
            string requestedPath = null;
            using (var channel = new ReleaseChannel("test/releases", new ResponseHandler { Reply = request =>
            {
                requestedPath = request.RequestUri.AbsolutePath;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(published)) };
            } }))
            {
                ReleaseManifest publishedManifest = channel.GetLatestAsync(CancellationToken.None).GetAwaiter().GetResult();
                ReleaseChannel.VerifyPackage(Path.Combine(root, "published-package.zip"), publishedManifest);
                assert(publishedManifest.Version == ReleaseChannel.CurrentVersion.ToString(), "PowerShell-generated publication JSON and ZIP are accepted by the real update client");
                assert(requestedPath == "/test/releases/releases/latest/download/update-fkp.json", "The renamed client requests its own manifest instead of the legacy client feed");
            }
        }
        var manifest = new ReleaseManifest { Schema = 1, Version = "9.0.0", Tag = "v9.0.0", Sha256 = new string('A', 64), SizeBytes = 100, Notes = "Test release" };
        using (var channel = new ReleaseChannel("test/releases", new ResponseHandler { Reply = request => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Serialize(manifest)) } }))
        {
            ReleaseManifest latest = channel.GetLatestAsync(CancellationToken.None).GetAwaiter().GetResult();
            assert(latest.Version == "9.0.0", "HTTPS release manifest parsing works with an isolated HTTP handler");
        }
        using (var channel = new ReleaseChannel("test/releases", new ResponseHandler { Reply = request => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("http://untrusted.example/package") } } }))
        {
            bool rejected = false;
            try { channel.GetLatestAsync(CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; }
            assert(rejected, "Update download rejects HTTP and untrusted redirect destinations");
        }
        using (var channel = new ReleaseChannel("test/releases", new ResponseHandler { Reply = request => new HttpResponseMessage(HttpStatusCode.NotFound) }))
        {
            bool rejected = false;
            try { channel.GetLatestAsync(CancellationToken.None).GetAwaiter().GetResult(); } catch (HttpRequestException) { rejected = true; }
            assert(rejected, "An unavailable release source is an error rather than a false up-to-date result");
        }
        assert(ReleaseChannel.ParseVersion("1.2.0") == ReleaseChannel.ParseVersion("1.2.0.0"), "Three- and four-part release versions compare consistently");
        manifest.Tag = "v8.0.0";
        bool badTag = false;
        try { manifest.Validate(); } catch (InvalidDataException) { badTag = true; }
        assert(badTag, "A version/tag mismatch cannot become an installable update");
        manifest.Tag = "v9.0.0";
        manifest.Version = ReleaseChannel.CurrentVersion.ToString();
        manifest.Tag = "v" + manifest.Version;
        using (var channel = new ReleaseChannel("test/releases", new ResponseHandler { Reply = request => { throw new InvalidOperationException("Must not download current version"); } }))
        {
            bool rejected = false;
            try { channel.PrepareAsync(manifest, root, CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
            assert(rejected, "The current version is rejected before any update download");
        }
    }

    private static ReleaseManifest CreateStage(string root, string stage)
    {
        Directory.CreateDirectory(stage);
        string package = Path.Combine(stage, "package.zip");
        using (ZipArchive zip = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(Path.Combine(root, ReleaseChannel.ApplicationFile), ReleaseChannel.ApplicationFile);
            zip.CreateEntryFromFile(Path.Combine(root, ReleaseChannel.ConfigurationFile), ReleaseChannel.ConfigurationFile);
        }
        ReleaseManifest manifest = ManifestFor(package);
        JsonData.WriteAtomic(Path.Combine(stage, "manifest.json"), manifest);
        return manifest;
    }

    private static ReleaseManifest ManifestFor(string package)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(package))
            return new ReleaseManifest { Schema = 1, Version = ReleaseChannel.CurrentVersion.ToString(), Tag = "v" + (ReleaseChannel.CurrentVersion.Revision > 0 ? ReleaseChannel.CurrentVersion.ToString(4) : ReleaseChannel.CurrentVersion.ToString(3)), Sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), SizeBytes = stream.Length, Notes = "Test" };
    }

    private static void CreateOldTarget(string root, string target)
    {
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(root, "OldVersion.exe"), Path.Combine(target, ReleaseChannel.ApplicationFile));
        File.WriteAllText(Path.Combine(target, ReleaseChannel.ConfigurationFile), "original runtime configuration");
        File.WriteAllText(Path.Combine(target, "keep-user-data.txt"), "preserve this file");
    }

    private static void VerifyInstaller(string root, Action<bool, string> assert)
    {
        string stage = Path.Combine(root, "apply-stage");
        ReleaseManifest manifest = CreateStage(root, stage);
        string package = Path.Combine(stage, "package.zip");
        ReleaseChannel.VerifyPackage(package, manifest);
        string badHash = manifest.Sha256;
        manifest.Sha256 = new string('0', 64);
        bool rejected = false;
        try { ReleaseChannel.VerifyPackage(package, manifest); } catch (InvalidDataException) { rejected = true; }
        assert(rejected, "A package hash mismatch blocks extraction and installation");
        manifest.Sha256 = badHash;
        string traversal = Path.Combine(root, "traversal.zip");
        using (ZipArchive zip = ZipFile.Open(traversal, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("../escape.exe").Open())) writer.Write("invalid");
            using (var writer = new StreamWriter(zip.CreateEntry(ReleaseChannel.ConfigurationFile).Open())) writer.Write("config");
        }
        rejected = false;
        try { ReleaseChannel.ExtractPackage(traversal, Path.Combine(root, "bad-extraction"), ManifestFor(traversal)); } catch (InvalidDataException) { rejected = true; }
        assert(rejected && !File.Exists(Path.Combine(root, "escape.exe")), "ZIP traversal cannot write outside the update payload directory");
        string target = Path.Combine(root, "old-install");
        CreateOldTarget(root, target);
        UpdateInstaller.Apply(stage, target);
        assert(FileVersionInfo.GetVersionInfo(Path.Combine(target, ReleaseChannel.ApplicationFile)).FileVersion == ReleaseChannel.CurrentVersion.ToString() && File.ReadAllText(Path.Combine(target, "keep-user-data.txt")) == "preserve this file", "The actual release updates an old install and preserves unrelated user files");
        string rollbackStage = Path.Combine(root, "rollback-stage");
        CreateStage(root, rollbackStage);
        string rollbackTarget = Path.Combine(root, "rollback-target");
        CreateOldTarget(root, rollbackTarget);
        rejected = false;
        using (var locked = new FileStream(Path.Combine(rollbackTarget, ReleaseChannel.ConfigurationFile), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { UpdateInstaller.Apply(rollbackStage, rollbackTarget); } catch (AggregateException) { rejected = true; }
        }
        assert(rejected && FileVersionInfo.GetVersionInfo(Path.Combine(rollbackTarget, ReleaseChannel.ApplicationFile)).FileVersion == "0.9.0.0" && Directory.GetFiles(rollbackTarget, "*.new-*").Length == 0, "A locked runtime config rolls the already-replaced EXE back and cleans sibling staging files");
        string workerStage = Path.Combine(root, "worker-stage");
        CreateStage(root, workerStage);
        string workerTarget = Path.Combine(root, "worker-target");
        CreateOldTarget(root, workerTarget);
        string worker = Path.Combine(workerStage, "UpdateWorker.exe");
        File.Copy(Path.Combine(root, ReleaseChannel.ApplicationFile), worker);
        File.Copy(Path.Combine(root, ReleaseChannel.ConfigurationFile), worker + ".config");
        using (var parent = Process.Start(new ProcessStartInfo(Path.Combine(root, "OldVersion.exe")) { UseShellExecute = false, CreateNoWindow = true }))
        {
            string jobPath = Path.Combine(workerStage, "job.json");
            JsonData.WriteAtomic(jobPath, new UpdateJob { TargetDirectory = workerTarget, ParentId = parent.Id, ParentStartedUtcTicks = parent.StartTime.ToUniversalTime().Ticks, Restart = false });
            using (var process = Process.Start(new ProcessStartInfo(worker, "--apply-update \"" + jobPath + "\"") { UseShellExecute = false, CreateNoWindow = true }))
            {
                bool exited = process.WaitForExit(10000);
                if (!exited) process.Kill();
                assert(exited && process.ExitCode == 0 && FileVersionInfo.GetVersionInfo(Path.Combine(workerTarget, ReleaseChannel.ApplicationFile)).FileVersion == ReleaseChannel.CurrentVersion.ToString(), "Real updater process waits for the parent and applies the verified release without launching another dashboard");
            }
        }
    }
}
