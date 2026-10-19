using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBotTracker
{
    public sealed class HealthAlert
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool Recovery { get; set; }
    }

    public sealed class ChannelDelivery
    {
        public string Channel { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ProtectedRefreshToken { get; set; }
    }

    [DataContract]
    internal sealed class NtfyMessage
    {
        [DataMember(Name = "topic")] public string Topic;
        [DataMember(Name = "title")] public string Title;
        [DataMember(Name = "message")] public string Message;
        [DataMember(Name = "priority")] public int Priority;
        [DataMember(Name = "tags")] public string[] Tags;
    }

    public sealed class NtfySender : IDisposable
    {
        private readonly HttpClient client;
        public NtfySender() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }
        public NtfySender(HttpMessageHandler handler) { client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) }; }

        public async Task SendAsync(NtfyOptions options, HealthAlert alert, CancellationToken cancellation)
        {
            options.Validate();
            using (var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint()))
            {
                if (!String.IsNullOrEmpty(options.ProtectedToken))
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ProtectedSecrets.Unprotect(options.ProtectedToken));
                request.Content = AlertHttp.Json(new NtfyMessage { Topic = options.Topic, Title = alert.Title,
                    Message = alert.Message, Priority = alert.Recovery ? 3 : 4, Tags = new[] { alert.Recovery ? "white_check_mark" : "warning" } });
                using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("ntfy ปฏิเสธข้อความ (HTTP " + (int)response.StatusCode + ")");
            }
        }

        public void Dispose() { client.Dispose(); }
    }

    public sealed class AlertDeliveryService : IDisposable
    {
        private readonly MicrosoftMail mail;
        private readonly NtfySender ntfy;

        public AlertDeliveryService() : this(new MicrosoftMail(), new NtfySender()) { }
        public AlertDeliveryService(MicrosoftMail mailSender, NtfySender ntfySender) { mail = mailSender; ntfy = ntfySender; }

        public async Task<ChannelDelivery[]> SendAsync(AlertSettings settings, HealthAlert alert, CancellationToken cancellation)
        {
            var operations = new List<Task<ChannelDelivery>>();
            if (settings.Outlook.Enabled) operations.Add(SendOutlookAsync(settings.Outlook, alert, cancellation));
            if (settings.Ntfy.Enabled) operations.Add(SendNtfyAsync(settings.Ntfy, alert, cancellation));
            return await Task.WhenAll(operations).ConfigureAwait(false);
        }

        private async Task<ChannelDelivery> SendOutlookAsync(OutlookOptions options, HealthAlert alert, CancellationToken cancellation)
        {
            try
            {
                string token = await mail.SendAsync(options, alert, cancellation).ConfigureAwait(false);
                return new ChannelDelivery { Channel = "Outlook", Success = true, Message = "Microsoft รับคำขอส่งอีเมลแล้ว", ProtectedRefreshToken = token };
            }
            catch (OperationCanceledException) { cancellation.ThrowIfCancellationRequested(); return Failed("Outlook", "ส่งอีเมลหมดเวลา"); }
            catch (InvalidOperationException error) { return Failed("Outlook", error.Message); }
            catch (Exception) { return Failed("Outlook", "ส่งอีเมลไม่ได้ กรุณาตรวจเครือข่ายและการตั้งค่า"); }
        }

        private async Task<ChannelDelivery> SendNtfyAsync(NtfyOptions options, HealthAlert alert, CancellationToken cancellation)
        {
            try
            {
                await ntfy.SendAsync(options, alert, cancellation).ConfigureAwait(false);
                return new ChannelDelivery { Channel = "ntfy", Success = true, Message = "เซิร์ฟเวอร์ ntfy รับข้อความแล้ว" };
            }
            catch (OperationCanceledException) { cancellation.ThrowIfCancellationRequested(); return Failed("ntfy", "ส่ง ntfy หมดเวลา"); }
            catch (InvalidOperationException error) { return Failed("ntfy", error.Message); }
            catch (Exception) { return Failed("ntfy", "ส่ง ntfy ไม่ได้ กรุณาตรวจเครือข่ายและการตั้งค่า"); }
        }

        private static ChannelDelivery Failed(string channel, string message) { return new ChannelDelivery { Channel = channel, Message = message }; }
        public void Dispose() { mail.Dispose(); ntfy.Dispose(); }
    }
}
