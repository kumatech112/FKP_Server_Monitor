using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class OutlookOptions
    {
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public string ClientId { get; set; }
        [DataMember] public string Tenant { get; set; }
        [DataMember] public string Recipients { get; set; }
        [DataMember] public string ProtectedRefreshToken { get; set; }

        public OutlookOptions() { Tenant = "common"; }
        public OutlookOptions Copy() { return (OutlookOptions)MemberwiseClone(); }

        public void ValidateIdentity()
        {
            Guid client;
            if (!Guid.TryParse(ClientId, out client) || client == Guid.Empty)
                throw new ArgumentException("กรุณาระบุ Application (client) ID ของ Microsoft ให้ถูกต้อง");
            if (String.IsNullOrWhiteSpace(Tenant) || Tenant.Length > 253 || !Regex.IsMatch(Tenant, @"\A[a-zA-Z0-9.-]+\z"))
                throw new ArgumentException("Tenant ต้องเป็น common, organizations, consumers หรือรหัส/โดเมนองค์กร");
        }

        public List<string> Addresses()
        {
            string[] items = (Recipients ?? "").Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (items.Length == 0 || items.Length > 20) throw new ArgumentException("กรุณาระบุอีเมลผู้รับ 1–20 รายการ");
            try { return items.Select(item => new MailAddress(item.Trim()).Address).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); }
            catch (FormatException) { throw new ArgumentException("รูปแบบอีเมลผู้รับไม่ถูกต้อง"); }
        }

        public void Validate()
        {
            if (!Enabled) return;
            ValidateIdentity();
            Addresses();
            if (String.IsNullOrEmpty(ProtectedRefreshToken) || ProtectedRefreshToken.Length > 131072)
                throw new ArgumentException("กรุณาลงชื่อเข้าใช้บัญชี Microsoft ก่อนเปิดใช้อีเมล");
        }
    }

    [DataContract]
    public sealed class NtfyOptions
    {
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public string ServerUrl { get; set; }
        [DataMember] public string Topic { get; set; }
        [DataMember] public string ProtectedToken { get; set; }

        public NtfyOptions() { ServerUrl = "https://ntfy.sh"; }
        public NtfyOptions Copy() { return (NtfyOptions)MemberwiseClone(); }

        public Uri Endpoint()
        {
            Uri uri;
            if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http")
                || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("กรุณาระบุ URL เซิร์ฟเวอร์ ntfy แบบ http หรือ https โดยไม่ใส่ topic ต่อท้าย");
            if (!String.IsNullOrEmpty(ProtectedToken) && uri.Scheme != "https" && !uri.IsLoopback)
                throw new ArgumentException("เซิร์ฟเวอร์ ntfy ที่ใช้ access token ต้องใช้ HTTPS");
            return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        }

        public void Validate()
        {
            if (!Enabled) return;
            Endpoint();
            if (Topic == null || !Regex.IsMatch(Topic, @"\A[a-zA-Z0-9_-]{1,64}\z"))
                throw new ArgumentException("Topic ใช้ตัวอักษรอังกฤษ ตัวเลข ขีดกลาง หรือขีดล่าง สูงสุด 64 ตัวอักษร");
            if (ProtectedToken != null && ProtectedToken.Length > 65536) throw new ArgumentException("ข้อมูล token ยาวเกินกำหนด");
        }
    }

    [DataContract]
    public sealed class AlertSettings
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public OutlookOptions Outlook { get; set; }
        [DataMember] public NtfyOptions Ntfy { get; set; }

        public AlertSettings() { Version = 1; Outlook = new OutlookOptions(); Ntfy = new NtfyOptions(); }
        public AlertSettings Copy() { return new AlertSettings { Version = Version, Outlook = Outlook.Copy(), Ntfy = Ntfy.Copy() }; }
        public void Validate()
        {
            if (Version != 1 || Outlook == null || Ntfy == null) throw new ArgumentException("รูปแบบการตั้งค่าแจ้งเตือนไม่ถูกต้อง");
            Outlook.Validate();
            Ntfy.Validate();
        }
    }

    public sealed class AlertSettingsStore
    {
        private readonly string path;
        public string Warning { get; private set; }
        public AlertSettings Settings { get; private set; }

        public AlertSettingsStore(string filePath)
        {
            path = filePath;
            try
            {
                Settings = File.Exists(path) ? JsonData.Read<AlertSettings>(path) : new AlertSettings();
                if (Settings == null) throw new InvalidDataException();
                Settings.Validate();
            }
            catch (Exception)
            {
                Settings = new AlertSettings();
                Warning = "อ่านการตั้งค่าแจ้งเตือนไม่ได้ ปิดช่องทางภายนอกชั่วคราวและเก็บไฟล์เดิมไว้: " + path;
            }
        }

        public void Save(AlertSettings settings)
        {
            if (Warning != null) throw new IOException(Warning);
            settings.Validate();
            JsonData.WriteAtomic(path, settings);
            Settings = settings;
        }
    }
}
