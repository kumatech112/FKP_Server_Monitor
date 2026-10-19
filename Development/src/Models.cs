using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class ServerEntry
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Host { get; set; }
        [DataMember] public DatabaseOptions Database { get; set; }

        public ServerEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "";
            Host = "";
            Database = new DatabaseOptions();
        }

        public ServerEntry Copy()
        {
            return new ServerEntry { Id = Id, Name = Name, Host = Host, Database = Database == null ? new DatabaseOptions() : Database.Copy() };
        }

        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(Id) || String.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("กรุณาระบุชื่อเซิร์ฟเวอร์");
            if (Name.Length > 120)
                throw new ArgumentException("ชื่อเซิร์ฟเวอร์ยาวได้ไม่เกิน 120 ตัวอักษร");
            if (String.IsNullOrWhiteSpace(Host) || Host.Length > 253 || Host != Host.Trim())
                throw new ArgumentException("กรุณาระบุ IP หรือชื่อโฮสต์ที่ถูกต้อง");
            IPAddress parsed;
            if (!IPAddress.TryParse(Host, out parsed) && Uri.CheckHostName(Host) != UriHostNameType.Dns)
                throw new ArgumentException("ระบุเฉพาะ IP หรือชื่อโฮสต์ ไม่ต้องใส่ URL หรือพอร์ต");
            if (Database != null) Database.Validate();
        }
    }

    [DataContract]
    public sealed class AppConfiguration
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public List<ServerEntry> Servers { get; set; }
        [DataMember] public int TimeoutMs { get; set; }
        [DataMember] public int IntervalSeconds { get; set; }

        public AppConfiguration()
        {
            Version = 3;
            Servers = new List<ServerEntry>();
            TimeoutMs = 3000;
            IntervalSeconds = 30;
        }

        public AppConfiguration Copy()
        {
            var copy = new AppConfiguration { Version = Version, TimeoutMs = TimeoutMs, IntervalSeconds = IntervalSeconds };
            foreach (ServerEntry server in Servers)
                copy.Servers.Add(server.Copy());
            return copy;
        }

        public void Validate()
        {
            if (Version != 3 || Servers == null || TimeoutMs < 500 || TimeoutMs > 15000 || IntervalSeconds < 5 || IntervalSeconds > 3600)
                throw new ArgumentException("รูปแบบข้อมูลหรือการตั้งค่าไม่ถูกต้อง");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ServerEntry server in Servers)
            {
                if (server == null)
                    throw new ArgumentException("พบรายการเซิร์ฟเวอร์ว่าง");
                server.Validate();
                if (!ids.Add(server.Id))
                    throw new ArgumentException("พบรหัสรายการซ้ำ");
            }
        }
    }

    public sealed class ProbeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long? Milliseconds { get; set; }
    }

    public sealed class ServerResult
    {
        public string ServerId { get; set; }
        public string Address { get; set; }
        public DateTime CheckedAt { get; set; }
        public ProbeResult Ping { get; set; }
        public bool DatabaseEnabled { get; set; }
        public ProbeResult Database { get; set; }

        public string Health
        {
            get
            {
                if (Ping == null || (DatabaseEnabled && Database == null)) return "pending";
                if (DatabaseEnabled && Ping.Success != Database.Success) return "warning";
                return Ping.Success ? "healthy" : "down";
            }
        }

        public string Summary
        {
            get
            {
                if (Health == "pending")
                    return "ยังไม่ตรวจ";
                if (Health == "warning") return Ping.Success ? "ฐานข้อมูลมีปัญหา" : "DB ใช้ได้ / Ping ไม่ตอบ";
                return Ping.Success ? "ผ่าน" : "ไม่ผ่าน";
            }
        }
    }
}
