using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBotTracker
{
    public sealed class NetworkProbe
    {
        // DNS has its own budget. ICMP and TCP then run in parallel with individual budgets.
        public async Task<ServerResult> CheckAsync(ServerEntry server, int timeoutMs, CancellationToken cancellation)
        {
            server.Validate();
            cancellation.ThrowIfCancellationRequested();
            var result = new ServerResult { ServerId = server.Id, Address = "" };
            IPAddress address;
            try
            {
                if (!IPAddress.TryParse(server.Host, out address))
                {
                    IPAddress[] addresses = await WithinAsync(Dns.GetHostAddressesAsync(server.Host), timeoutMs, cancellation).ConfigureAwait(false);
                    address = addresses.OrderBy(item => item.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault();
                    if (address == null)
                        throw new SocketException((int)SocketError.HostNotFound);
                }
                result.Address = address.ToString();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                string message = exception is TimeoutException ? "DNS หมดเวลา" : "ค้นหาโฮสต์ไม่ได้";
                if (server.Mode != "TCP") result.Icmp = Failure(message);
                if (server.Mode != "ICMP") result.Tcp = Failure(message);
                result.CheckedAt = DateTime.Now;
                return result;
            }

            Task<ProbeResult> icmp = server.Mode == "TCP" ? Task.FromResult<ProbeResult>(null) : CheckIcmpAsync(address, timeoutMs, cancellation);
            Task<ProbeResult> tcp = server.Mode == "ICMP" ? Task.FromResult<ProbeResult>(null) : CheckTcpAsync(address, server.Port, timeoutMs, cancellation);
            await Task.WhenAll(icmp, tcp).ConfigureAwait(false);
            result.Icmp = await icmp.ConfigureAwait(false);
            result.Tcp = await tcp.ConfigureAwait(false);
            result.CheckedAt = DateTime.Now;
            return result;
        }

        public async Task<ProbeResult> CheckIcmpAsync(IPAddress address, int timeoutMs, CancellationToken cancellation)
        {
            using (var ping = new Ping())
            {
                try
                {
                    PingReply reply = await WithinAsync(ping.SendPingAsync(address, timeoutMs), timeoutMs, cancellation).ConfigureAwait(false);
                    if (reply.Status == IPStatus.Success)
                        return new ProbeResult { Success = true, Message = "ตอบกลับ", Milliseconds = reply.RoundtripTime };
                    return Failure(reply.Status == IPStatus.TimedOut ? "หมดเวลา" : "ไม่ตอบกลับ (" + reply.Status + ")");
                }
                catch (OperationCanceledException) { throw; }
                catch (TimeoutException) { return Failure("หมดเวลา"); }
                catch (Exception) { return Failure("ตรวจสอบ ICMP ไม่สำเร็จ"); }
            }
        }

        public async Task<ProbeResult> CheckTcpAsync(IPAddress address, int port, int timeoutMs, CancellationToken cancellation)
        {
            using (var client = new TcpClient(address.AddressFamily))
            {
                var elapsed = Stopwatch.StartNew();
                try
                {
                    await WithinAsync(ConnectAsync(client, address, port), timeoutMs, cancellation).ConfigureAwait(false);
                    return new ProbeResult { Success = true, Message = "พอร์ตเปิด", Milliseconds = elapsed.ElapsedMilliseconds };
                }
                catch (OperationCanceledException) { throw; }
                catch (TimeoutException) { return Failure("หมดเวลา"); }
                catch (SocketException exception)
                {
                    return Failure(exception.SocketErrorCode == SocketError.ConnectionRefused ? "ปฏิเสธการเชื่อมต่อ" : "เชื่อมต่อไม่ได้ (" + exception.SocketErrorCode + ")");
                }
                catch (Exception) { return Failure("ตรวจสอบ TCP ไม่สำเร็จ"); }
            }
        }

        private static async Task<bool> ConnectAsync(TcpClient client, IPAddress address, int port)
        {
            await client.ConnectAsync(address, port).ConfigureAwait(false);
            return true;
        }

        private static ProbeResult Failure(string message)
        {
            return new ProbeResult { Success = false, Message = message };
        }

        public static async Task<T> WithinAsync<T>(Task<T> operation, int timeoutMs, CancellationToken cancellation)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                Task delay = Task.Delay(timeoutMs, deadline.Token);
                Task completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);
                if (completed == operation)
                {
                    deadline.Cancel();
                    if (cancellation.IsCancellationRequested)
                        ObserveFailure(operation);
                    cancellation.ThrowIfCancellationRequested();
                    return await operation.ConfigureAwait(false);
                }
                // Observe late DNS/socket failures after a timeout or cancellation.
                ObserveFailure(operation);
                cancellation.ThrowIfCancellationRequested();
                throw new TimeoutException();
            }
        }

        private static void ObserveFailure(Task operation)
        {
            operation.ContinueWith(task => { var ignored = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
