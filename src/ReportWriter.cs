using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ServerBotTracker
{
    public static class ReportWriter
    {
        public static void Write(string path, IEnumerable<ServerEntry> servers, IDictionary<string, ServerResult> results)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("ชื่อเซิร์ฟเวอร์,โฮสต์,พอร์ต,วิธีตรวจสอบ,สถานะ,ICMP,ICMP ms,TCP,TCP ms,IP ที่ตรวจ,ตรวจล่าสุด");
                foreach (ServerEntry server in servers)
                {
                    ServerResult result;
                    results.TryGetValue(server.Id, out result);
                    string[] cells = {
                        server.Name, server.Host, server.Port.ToString(CultureInfo.InvariantCulture), server.Mode,
                        result == null ? "ยังไม่ตรวจ" : result.Summary,
                        result == null ? "ยังไม่ตรวจ" : result.Icmp == null ? "ไม่ได้เลือก" : result.Icmp.Message,
                        result == null || result.Icmp == null ? "" : Convert.ToString(result.Icmp.Milliseconds, CultureInfo.InvariantCulture),
                        result == null ? "ยังไม่ตรวจ" : result.Tcp == null ? "ไม่ได้เลือก" : result.Tcp.Message,
                        result == null || result.Tcp == null ? "" : Convert.ToString(result.Tcp.Milliseconds, CultureInfo.InvariantCulture),
                        result == null ? "" : result.Address,
                        result == null ? "" : result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    };
                    for (int index = 0; index < cells.Length; index++)
                        cells[index] = EscapeCell(cells[index]);
                    writer.WriteLine(String.Join(",", cells));
                }
            }
        }

        public static string EscapeCell(string value)
        {
            value = value ?? "";
            // Keep a server name from becoming an Excel formula when the CSV is opened.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0)
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
