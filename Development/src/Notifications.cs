using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class NotificationItem
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public string Kind { get; set; }
        [DataMember] public DateTime CreatedUtc { get; set; }
        [DataMember] public bool Read { get; set; }
    }

    [DataContract]
    public sealed class NotificationState
    {
        [DataMember] public bool AutomaticUpdates { get; set; }
        [DataMember] public bool ServerAlerts { get; set; }
        [DataMember] public List<NotificationItem> Items { get; set; }
        [DataMember] public List<string> SeenRemoteIds { get; set; }

        public NotificationState()
        {
            AutomaticUpdates = true;
            ServerAlerts = true;
            Items = new List<NotificationItem>();
            SeenRemoteIds = new List<string>();
        }
    }

    public sealed class NotificationCenter
    {
        private readonly string path;
        private bool storageReadOnly;
        public NotificationState State { get; private set; }
        public string StorageWarning { get; private set; }
        public event EventHandler Changed;

        public NotificationCenter(string filePath)
        {
            path = filePath;
            try
            {
                State = File.Exists(path) ? JsonData.Read<NotificationState>(path) : new NotificationState();
                if (State == null || State.Items == null || State.SeenRemoteIds == null || State.Items.Count > 100 || State.SeenRemoteIds.Count > 1000 || State.Items.Any(item => item == null || item.Id == null || item.Title == null || item.Message == null))
                    throw new InvalidDataException("ข้อมูลการแจ้งเตือนไม่ถูกต้อง");
            }
            catch (Exception)
            {
                State = new NotificationState();
                storageReadOnly = true;
                StorageWarning = "อ่านไฟล์การแจ้งเตือนเดิมไม่ได้ • ใช้ข้อมูลชั่วคราวโดยไม่เขียนทับไฟล์เดิม";
            }
        }

        public bool Add(string id, string title, string message, string kind, bool remote)
        {
            if (String.IsNullOrEmpty(id) || id.Length > 200 || String.IsNullOrEmpty(title) || message == null)
                throw new ArgumentException("ข้อมูลแจ้งเตือนไม่ถูกต้อง");
            if (title.Length > 160) title = title.Substring(0, 160);
            if (message.Length > 2000) message = message.Substring(0, 2000);
            if (remote && State.SeenRemoteIds.Contains(id))
                return false;
            if (State.Items.Any(item => item.Id == id))
                return false;
            State.Items.Insert(0, new NotificationItem { Id = id, Title = title, Message = message, Kind = kind, CreatedUtc = DateTime.UtcNow });
            if (State.Items.Count > 100)
                State.Items.RemoveAt(State.Items.Count - 1);
            if (remote)
            {
                State.SeenRemoteIds.Add(id);
                if (State.SeenRemoteIds.Count > 1000)
                    State.SeenRemoteIds.RemoveAt(0);
            }
            Save();
            return true;
        }

        public void MarkAllRead()
        {
            foreach (NotificationItem item in State.Items)
                item.Read = true;
            Save();
        }

        public void Save()
        {
            if (!storageReadOnly)
                JsonData.WriteAtomic(path, State);
            EventHandler handler = Changed;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }

    public sealed class ServerAlertTracker
    {
        private sealed class HealthState
        {
            public int Failures;
            public bool Down;
        }

        private readonly Dictionary<string, HealthState> states = new Dictionary<string, HealthState>();

        public string Observe(string serverId, bool success)
        {
            HealthState state;
            if (!states.TryGetValue(serverId, out state))
            {
                state = new HealthState();
                states.Add(serverId, state);
            }
            if (success)
            {
                bool recovered = state.Down;
                state.Failures = 0;
                state.Down = false;
                return recovered ? "recovered" : null;
            }
            state.Failures = Math.Min(2, state.Failures + 1);
            if (state.Failures < 2 || state.Down)
                return null;
            state.Down = true;
            return "down";
        }

        public void Forget(string serverId)
        {
            states.Remove(serverId);
        }
    }

    public static class JsonData
    {
        public static T Read<T>(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > 4 * 1024 * 1024)
                    throw new InvalidDataException("ไฟล์ข้อมูลใหญ่เกินกำหนด");
                return Deserialize<T>(stream);
            }
        }

        public static T Deserialize<T>(Stream stream)
        {
            long start = stream.Position;
            bool bom = stream.ReadByte() == 0xef && stream.ReadByte() == 0xbb && stream.ReadByte() == 0xbf;
            stream.Position = bom ? start + 3 : start;
            return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        public static void WriteAtomic<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, path + ".bak", true);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
