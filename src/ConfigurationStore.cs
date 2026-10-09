using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace ServerBotTracker
{
    public sealed class ConfigurationStore
    {
        public string FilePath { get; private set; }

        public ConfigurationStore(string filePath)
        {
            FilePath = Path.GetFullPath(filePath);
        }

        public static ConfigurationStore ForCurrentUser()
        {
            return new ConfigurationStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuma", "ServerBotTracker", "servers.json"));
        }

        public AppConfiguration Load()
        {
            if (!File.Exists(FilePath))
                return new AppConfiguration();
            using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var serializer = new DataContractJsonSerializer(typeof(AppConfiguration));
                var configuration = (AppConfiguration)serializer.ReadObject(stream);
                if (configuration == null)
                    throw new InvalidDataException("ไม่พบข้อมูลการตั้งค่า");
                configuration.Validate();
                return configuration;
            }
        }

        public void Save(AppConfiguration configuration)
        {
            configuration.Validate();
            string directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppConfiguration));
                    serializer.WriteObject(stream, configuration);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath))
                    File.Replace(temporary, FilePath, FilePath + ".bak", true);
                else
                    File.Move(temporary, FilePath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
