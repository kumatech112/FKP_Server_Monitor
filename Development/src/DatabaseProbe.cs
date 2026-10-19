using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class DatabaseOptions
    {
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public string DataSource { get; set; }
        [DataMember] public string DatabaseName { get; set; }
        [DataMember] public string UserName { get; set; }
        [DataMember] public string ProtectedPassword { get; set; }
        [DataMember] public bool TrustServerCertificate { get; set; }

        public DatabaseOptions Copy()
        {
            return new DatabaseOptions { Enabled = Enabled, DataSource = DataSource, DatabaseName = DatabaseName,
                UserName = UserName, ProtectedPassword = ProtectedPassword, TrustServerCertificate = TrustServerCertificate };
        }

        public void Validate()
        {
            if (!Enabled) return;
            if (String.IsNullOrWhiteSpace(DatabaseName) || DatabaseName.Length > 128 || String.IsNullOrWhiteSpace(UserName) || UserName.Length > 128
                || String.IsNullOrEmpty(ProtectedPassword) || ProtectedPassword.Length > 65536 || (DataSource != null && DataSource.Length > 320))
                throw new ArgumentException("กรุณาระบุชื่อฐานข้อมูล ชื่อผู้ใช้ SQL Server และรหัสผ่านให้ครบ");
        }
    }

    public interface IDatabaseProbe
    {
        Task<ProbeResult> CheckAsync(ServerEntry server, int timeoutMs, CancellationToken cancellation);
    }

    public sealed class DatabaseProbe : IDatabaseProbe
    {
        public static string CreateConnectionString(ServerEntry server, int timeoutMs)
        {
            DatabaseOptions options = server.Database;
            if (options == null || !options.Enabled) throw new ArgumentException("ยังไม่ได้เปิดตรวจฐานข้อมูล");
            options.Validate();
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = String.IsNullOrWhiteSpace(options.DataSource) ? server.Host : options.DataSource.Trim(),
                InitialCatalog = options.DatabaseName.Trim(), UserID = options.UserName.Trim(),
                Password = ProtectedSecrets.Unprotect(options.ProtectedPassword), IntegratedSecurity = false,
                Encrypt = true, TrustServerCertificate = options.TrustServerCertificate,
                Pooling = false, PersistSecurityInfo = false, ConnectRetryCount = 0,
                ConnectTimeout = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0)), ApplicationName = "FKP Server Monitor"
            };
            return builder.ConnectionString;
        }

        public async Task<ProbeResult> CheckAsync(ServerEntry server, int timeoutMs, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(timeoutMs);
                var elapsed = Stopwatch.StartNew();
                try
                {
                    using (var connection = new SqlConnection(CreateConnectionString(server, timeoutMs)))
                    {
                        await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
                        using (SqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT 1";
                            command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0));
                            object answer = await command.ExecuteScalarAsync(deadline.Token).ConfigureAwait(false);
                            if (Convert.ToInt32(answer) != 1) throw new InvalidOperationException("ผลตรวจฐานข้อมูลไม่ถูกต้อง");
                        }
                    }
                    return new ProbeResult { Success = true, Message = "เชื่อมต่อและตรวจคำสั่งสำเร็จ", Milliseconds = elapsed.ElapsedMilliseconds };
                }
                catch (OperationCanceledException)
                {
                    cancellation.ThrowIfCancellationRequested();
                    return Failure("ฐานข้อมูลหมดเวลา");
                }
                catch (SqlException error)
                {
                    if (deadline.IsCancellationRequested) { cancellation.ThrowIfCancellationRequested(); return Failure("ฐานข้อมูลหมดเวลา"); }
                    if (error.Number == 18456) return Failure("เข้าสู่ระบบ SQL Server ไม่สำเร็จ");
                    if (error.Number == 4060) return Failure("เปิดฐานข้อมูลที่ระบุไม่ได้");
                    if (error.Number == -2) return Failure("ฐานข้อมูลหมดเวลา");
                    return Failure("เชื่อมต่อ SQL Server ไม่สำเร็จ (รหัส " + error.Number + ")");
                }
                catch (Exception) { return Failure("ตรวจฐานข้อมูลไม่ได้ กรุณาตรวจการเชื่อมต่อ บัญชี และใบรับรอง"); }
            }
        }

        private static ProbeResult Failure(string message)
        {
            return new ProbeResult { Success = false, Message = message };
        }
    }
}
