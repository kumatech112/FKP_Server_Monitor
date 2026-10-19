using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBotTracker
{
    [DataContract]
    public sealed class DeviceAuthorization
    {
        [DataMember(Name = "device_code")] public string DeviceCode { get; set; }
        [DataMember(Name = "user_code")] public string UserCode { get; set; }
        [DataMember(Name = "expires_in")] public int ExpiresIn { get; set; }
        [DataMember(Name = "interval")] public int Interval { get; set; }
    }

    [DataContract]
    internal sealed class MicrosoftToken
    {
        [DataMember(Name = "access_token")] public string AccessToken { get; set; }
        [DataMember(Name = "refresh_token")] public string RefreshToken { get; set; }
        [DataMember(Name = "error")] public string Error { get; set; }
    }

    [DataContract] internal sealed class GraphAddress { [DataMember(Name = "address")] public string Address; }
    [DataContract] internal sealed class GraphRecipient { [DataMember(Name = "emailAddress")] public GraphAddress Email; }
    [DataContract] internal sealed class GraphBody { [DataMember(Name = "contentType")] public string Type = "Text"; [DataMember(Name = "content")] public string Text; }
    [DataContract] internal sealed class GraphMessage
    {
        [DataMember(Name = "subject")] public string Subject;
        [DataMember(Name = "body")] public GraphBody Body;
        [DataMember(Name = "toRecipients")] public List<GraphRecipient> Recipients;
    }
    [DataContract] internal sealed class GraphSend { [DataMember(Name = "message")] public GraphMessage Message; [DataMember(Name = "saveToSentItems")] public bool Save = true; }

    public sealed class MicrosoftMail : IDisposable
    {
        private const string Scope = "https://graph.microsoft.com/Mail.Send offline_access";
        private readonly HttpClient client;

        public MicrosoftMail() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }
        public MicrosoftMail(HttpMessageHandler handler) { client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) }; }

        private static string Endpoint(OutlookOptions options, string operation)
        {
            options.ValidateIdentity();
            return "https://login.microsoftonline.com/" + Uri.EscapeDataString(options.Tenant) + "/oauth2/v2.0/" + operation;
        }

        public async Task<DeviceAuthorization> BeginSignInAsync(OutlookOptions options, CancellationToken cancellation)
        {
            using (var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "client_id", options.ClientId }, { "scope", Scope } }))
            using (HttpResponseMessage response = await client.PostAsync(Endpoint(options, "devicecode"), content, cancellation).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("เริ่มลงชื่อเข้าใช้ไม่ได้ กรุณาตรวจ Client ID, Tenant และการเปิด public client flow");
                DeviceAuthorization code = await AlertHttp.ReadAsync<DeviceAuthorization>(response, cancellation).ConfigureAwait(false);
                if (code == null || String.IsNullOrEmpty(code.DeviceCode) || String.IsNullOrEmpty(code.UserCode) || code.ExpiresIn < 1 || code.ExpiresIn > 1800)
                    throw new InvalidOperationException("ข้อมูลลงชื่อเข้าใช้จาก Microsoft ไม่ถูกต้อง");
                return code;
            }
        }

        public async Task<string> CompleteSignInAsync(OutlookOptions options, DeviceAuthorization code, CancellationToken cancellation)
        {
            DateTime expires = DateTime.UtcNow.AddSeconds(code.ExpiresIn);
            int interval = Math.Max(1, Math.Min(30, code.Interval));
            while (DateTime.UtcNow < expires)
            {
                await Task.Delay(interval * 1000, cancellation).ConfigureAwait(false);
                using (var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "client_id", options.ClientId },
                    { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" }, { "device_code", code.DeviceCode } }))
                using (HttpResponseMessage response = await client.PostAsync(Endpoint(options, "token"), content, cancellation).ConfigureAwait(false))
                {
                    MicrosoftToken token = await AlertHttp.ReadAsync<MicrosoftToken>(response, cancellation).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode && token != null && !String.IsNullOrEmpty(token.RefreshToken)) return ProtectedSecrets.Protect(token.RefreshToken);
                    if (token != null && token.Error == "authorization_pending") continue;
                    if (token != null && token.Error == "slow_down") { interval = Math.Min(60, interval + 5); continue; }
                    throw new InvalidOperationException("ลงชื่อเข้าใช้ไม่สำเร็จหรือถูกยกเลิก กรุณาเริ่มใหม่และตรวจสิทธิ์ Mail.Send");
                }
            }
            throw new TimeoutException("รหัสลงชื่อเข้าใช้หมดอายุ กรุณาเริ่มใหม่");
        }

        public async Task<string> SendAsync(OutlookOptions options, HealthAlert alert, CancellationToken cancellation)
        {
            options.Validate();
            MicrosoftToken token;
            using (var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "client_id", options.ClientId }, { "scope", Scope },
                { "grant_type", "refresh_token" }, { "refresh_token", ProtectedSecrets.Unprotect(options.ProtectedRefreshToken) } }))
            using (HttpResponseMessage response = await client.PostAsync(Endpoint(options, "token"), content, cancellation).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Outlook ต้องลงชื่อเข้าใช้ใหม่ หรือองค์กรไม่อนุญาตให้ส่งอีเมล");
                token = await AlertHttp.ReadAsync<MicrosoftToken>(response, cancellation).ConfigureAwait(false);
                if (token == null || String.IsNullOrEmpty(token.AccessToken)) throw new InvalidOperationException("Microsoft ไม่ส่งข้อมูลอนุญาตให้ใช้อีเมล");
            }
            var recipients = new List<GraphRecipient>();
            foreach (string address in options.Addresses()) recipients.Add(new GraphRecipient { Email = new GraphAddress { Address = address } });
            var payload = new GraphSend { Message = new GraphMessage { Subject = "[FKP Server Monitor] " + alert.Title,
                Body = new GraphBody { Text = alert.Message }, Recipients = recipients } };
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/sendMail"))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
                request.Content = AlertHttp.Json(payload);
                using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                    if ((int)response.StatusCode != 202) throw new InvalidOperationException("Microsoft ปฏิเสธคำขอส่งอีเมล (HTTP " + (int)response.StatusCode + ")");
            }
            return String.IsNullOrEmpty(token.RefreshToken) ? options.ProtectedRefreshToken : ProtectedSecrets.Protect(token.RefreshToken);
        }

        public void Dispose() { client.Dispose(); }
    }
}
