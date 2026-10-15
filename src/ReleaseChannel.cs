using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class ReleaseManifest
    {
        [DataMember] public int Schema { get; set; }
        [DataMember] public string Version { get; set; }
        [DataMember] public string Tag { get; set; }
        [DataMember] public string Sha256 { get; set; }
        [DataMember] public long SizeBytes { get; set; }
        [DataMember] public string Notes { get; set; }

        public void Validate()
        {
            if (Schema != 1 || String.IsNullOrEmpty(Tag) || !Regex.IsMatch(Tag, @"\Av[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\z") || ReleaseChannel.ParseVersion(Version) != ReleaseChannel.ParseVersion(Tag.Substring(1)))
                throw new InvalidDataException("ข้อมูลรุ่นอัปเดตไม่ถูกต้อง");
            if (Sha256 == null || !Regex.IsMatch(Sha256, @"\A[0-9a-fA-F]{64}\z") || SizeBytes < 1 || SizeBytes > ReleaseChannel.MaximumPackageBytes)
                throw new InvalidDataException("ข้อมูลตรวจสอบไฟล์อัปเดตไม่ถูกต้อง");
            if (Notes != null && Notes.Length > 10000)
                throw new InvalidDataException("รายละเอียดรุ่นยาวเกินกำหนด");
        }
    }

    [DataContract]
    public sealed class AnnouncementFeed
    {
        [DataMember] public int Schema { get; set; }
        [DataMember] public List<Announcement> Items { get; set; }

        public void Validate()
        {
            if (Schema != 1 || Items == null || Items.Count > 50)
                throw new InvalidDataException("ข้อมูลประกาศไม่ถูกต้อง");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Announcement item in Items)
            {
                if (item == null || String.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 100 || !ids.Add(item.Id) || String.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 160 || String.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 4000 || item.ExpiresTime <= item.PublishedTime || item.ExpiresTime - item.PublishedTime > TimeSpan.FromDays(366))
                    throw new InvalidDataException("รายละเอียดประกาศไม่ถูกต้อง");
            }
        }
    }

    [DataContract]
    public sealed class Announcement
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public string PublishedUtc { get; set; }
        [DataMember] public string ExpiresUtc { get; set; }

        public DateTime PublishedTime { get { return ParseUtc(PublishedUtc); } }
        public DateTime ExpiresTime { get { return ParseUtc(ExpiresUtc); } }

        private static DateTime ParseUtc(string value)
        {
            DateTimeOffset date;
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date))
                throw new InvalidDataException("วันเวลาประกาศไม่ถูกต้อง");
            return date.UtcDateTime;
        }

        public bool IsActive(DateTime utcNow)
        {
            return PublishedTime <= utcNow && utcNow < ExpiresTime;
        }
    }

    public sealed class PreparedUpdate
    {
        public string DirectoryPath { get; set; }
        public ReleaseManifest Manifest { get; set; }
    }

    public sealed class ReleaseChannel : IDisposable
    {
        public const string OfficialRepository = "kumatech112/Server_Bot_Tracker";
        public const string PackageName = "ServerBotTracker-portable.zip";
        public const long MaximumPackageBytes = 20 * 1024 * 1024;
        public const string ApplicationFile = "ServerBotTracker.exe";
        public const string ConfigurationFile = "ServerBotTracker.exe.config";
        private readonly HttpClient client;
        private readonly string repository;

        public ReleaseChannel() : this(OfficialRepository, new HttpClientHandler { AllowAutoRedirect = false }) { }

        public ReleaseChannel(string repositoryName, HttpMessageHandler handler)
        {
            if (!Regex.IsMatch(repositoryName ?? "", @"\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z"))
                throw new ArgumentException("Invalid release repository");
            repository = repositoryName;
            client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ServerBotTracker/" + CurrentVersion);
        }

        public static Version CurrentVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public static Version ParseVersion(string value)
        {
            Version version;
            if (!System.Version.TryParse(value, out version) || version.Major < 0 || version.Minor < 0)
                throw new InvalidDataException("หมายเลขรุ่นไม่ถูกต้อง");
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        public async Task<ReleaseManifest> GetLatestAsync(CancellationToken cancellation)
        {
            ReleaseManifest manifest = await ReadJsonAsync<ReleaseManifest>("/releases/latest/download/update.json", cancellation).ConfigureAwait(false);
            manifest.Validate();
            return manifest;
        }

        public async Task<AnnouncementFeed> GetAnnouncementsAsync(CancellationToken cancellation)
        {
            AnnouncementFeed feed = await ReadJsonAsync<AnnouncementFeed>("/releases/download/announcements/announcements.json", cancellation).ConfigureAwait(false);
            feed.Validate();
            return feed;
        }

        private async Task<T> ReadJsonAsync<T>(string suffix, CancellationToken cancellation)
        {
            using (var bytes = new MemoryStream())
            {
                await DownloadAsync(new Uri("https://github.com/" + repository + suffix), bytes, 256 * 1024, cancellation).ConfigureAwait(false);
                bytes.Position = 0;
                return JsonData.Deserialize<T>(bytes);
            }
        }

        public async Task<PreparedUpdate> PrepareAsync(ReleaseManifest manifest, string updateRoot, CancellationToken cancellation)
        {
            manifest.Validate();
            if (ParseVersion(manifest.Version) <= CurrentVersion)
                throw new InvalidOperationException("ไม่อนุญาตให้ติดตั้งรุ่นเก่าหรือรุ่นเดิม");
            string directory = Path.Combine(updateRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string package = Path.Combine(directory, "package.zip");
            try
            {
                using (var stream = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    await DownloadAsync(new Uri("https://github.com/" + repository + "/releases/download/" + manifest.Tag + "/" + PackageName), stream, manifest.SizeBytes, cancellation).ConfigureAwait(false);
                VerifyPackage(package, manifest);
                ExtractPackage(package, Path.Combine(directory, "payload"), manifest);
                JsonData.WriteAtomic(Path.Combine(directory, "manifest.json"), manifest);
                return new PreparedUpdate { DirectoryPath = directory, Manifest = manifest };
            }
            catch
            {
                // A failed download is never executable. Retain small diagnostics, not the partial package.
                if (File.Exists(package)) File.Delete(package);
                throw;
            }
        }

        private async Task DownloadAsync(Uri uri, Stream destination, long limit, CancellationToken cancellation)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                cancellation = deadline.Token;
                for (int redirect = 0; redirect <= 5; redirect++)
                {
                    if (uri.Scheme != Uri.UriSchemeHttps || (uri.Host != "github.com" && uri.Host != "release-assets.githubusercontent.com" && uri.Host != "objects.githubusercontent.com"))
                        throw new InvalidDataException("ปลายทางดาวน์โหลดไม่ใช่ GitHub ที่อนุญาต");
                    using (HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                    {
                        int status = (int)response.StatusCode;
                        if (status >= 300 && status < 400 && response.Headers.Location != null)
                        {
                            uri = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(uri, response.Headers.Location);
                            continue;
                        }
                        response.EnsureSuccessStatusCode();
                        using (CancellationTokenRegistration abortBody = cancellation.Register(() => response.Dispose()))
                        {
                            if (response.Content.Headers.ContentLength > limit)
                                throw new InvalidDataException("ไฟล์ดาวน์โหลดใหญ่เกินกำหนด");
                            using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                var buffer = new byte[81920];
                                long total = 0;
                                int count;
                                while ((count = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) != 0)
                                {
                                    total += count;
                                    if (total > limit) throw new InvalidDataException("ไฟล์ดาวน์โหลดใหญ่เกินกำหนด");
                                    await destination.WriteAsync(buffer, 0, count, cancellation).ConfigureAwait(false);
                                }
                            }
                            return;
                        }
                    }
                }
                throw new InvalidDataException("เปลี่ยนปลายทางดาวน์โหลดหลายครั้งเกินกำหนด");
            }
        }

        public static void VerifyPackage(string package, ReleaseManifest manifest)
        {
            manifest.Validate();
            using (var stream = File.OpenRead(package))
            using (var sha = SHA256.Create())
            {
                string digest = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (stream.Length != manifest.SizeBytes || !String.Equals(digest, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("ไฟล์อัปเดตไม่ตรงกับ SHA-256 หรือขนาดที่ประกาศ");
            }
        }

        public static void ExtractPackage(string package, string destination, ReleaseManifest manifest)
        {
            VerifyPackage(package, manifest);
            Directory.CreateDirectory(destination);
            var expected = new HashSet<string>(StringComparer.Ordinal) { ApplicationFile, ConfigurationFile };
            using (ZipArchive archive = ZipFile.OpenRead(package))
            {
                if (archive.Entries.Count != 2)
                    throw new InvalidDataException("ชุดอัปเดตต้องมีเฉพาะไฟล์โปรแกรมและ runtime config");
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!expected.Remove(entry.FullName) || entry.Length > MaximumPackageBytes || entry.Length == 0)
                        throw new InvalidDataException("พบชื่อไฟล์หรือขนาดไฟล์ที่ไม่อนุญาตในชุดอัปเดต");
                    entry.ExtractToFile(Path.Combine(destination, entry.FullName), false);
                }
            }
            var information = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(destination, ApplicationFile));
            if (ParseVersion(information.FileVersion) != ParseVersion(manifest.Version) || information.ProductName != "Server Bot Tracker")
                throw new InvalidDataException("โปรแกรมในชุดอัปเดตไม่ตรงกับรุ่นที่ประกาศ");
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
