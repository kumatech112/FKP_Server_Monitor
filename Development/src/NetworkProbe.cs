using System;
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
        // One ICMP echo per server per cycle, using the same protocol as Windows ping.
        // DNS and the echo request each have a bounded wait.
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
                result.Ping = Failure(message);
                result.CheckedAt = DateTime.Now;
                return result;
            }

            result.Ping = await CheckPingAsync(address, timeoutMs, cancellation).ConfigureAwait(false);
            result.CheckedAt = DateTime.Now;
            return result;
        }

        public async Task<ProbeResult> CheckPingAsync(IPAddress address, int timeoutMs, CancellationToken cancellation)
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
                catch (Exception) { return Failure("Ping ไม่สำเร็จ"); }
            }
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
                // Observe late DNS/ping failures after a timeout or cancellation.
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
