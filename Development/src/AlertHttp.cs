using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBotTracker
{
    internal static class AlertHttp
    {
        public static ByteArrayContent Json<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                var content = new ByteArrayContent(stream.ToArray());
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                return content;
            }
        }

        public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellation)
        {
            using (cancellation.Register(() => response.Dispose()))
            using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var bytes = new byte[8192];
                int count;
                while ((count = await source.ReadAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false)) != 0)
                {
                    if (buffer.Length + count > 1024 * 1024) throw new InvalidDataException("ข้อมูลตอบกลับใหญ่เกินกำหนด");
                    buffer.Write(bytes, 0, count);
                }
                buffer.Position = 0;
                return JsonData.Deserialize<T>(buffer);
            }
        }
    }
}
