using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServerBotTracker;

internal static class MonitoringVerification
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Action<bool, string> assert;

    public static void Run(string root, Action<bool, string> check)
    {
        assert = check;
        VerifySettings(root);
        VerifyDatabaseAsync().GetAwaiter().GetResult();
        VerifyDeliveryAsync().GetAwaiter().GetResult();
        VerifyDialogs(root);
        VerifyTransitions(root);
        VerifyDashboardFilters(root);
    }

    private static ServerEntry SqlServer()
    {
        return new ServerEntry
        {
            Name = "เครื่องทดสอบ SQL",
            Host = "127.0.0.1",
            Database = new DatabaseOptions
            {
                Enabled = true,
                DatabaseName = "Monitoring;Database",
                UserName = "monitor_user",
                ProtectedPassword = ProtectedSecrets.Protect("fixture;sql=password")
            }
        };
    }

    private static void VerifySettings(string root)
    {
        ServerEntry entry = SqlServer();
        var options = new SqlConnectionStringBuilder(DatabaseProbe.CreateConnectionString(entry, 700));
        assert(options.InitialCatalog == "Monitoring;Database" && options.Password == "fixture;sql=password"
            && options.UserID == "monitor_user" && options.DataSource == "127.0.0.1",
            "SQL credentials and database names containing separators survive builder escaping");
        assert(options.Encrypt && !options.TrustServerCertificate && !options.Pooling && !options.PersistSecurityInfo
            && !options.IntegratedSecurity && options.ConnectRetryCount == 0,
            "SQL authentication encrypts connections, validates certificates and opens a fresh connection per check");
        entry.Database.TrustServerCertificate = true;
        entry.Database.DataSource = "sql-host\\INSTANCE";
        options = new SqlConnectionStringBuilder(DatabaseProbe.CreateConnectionString(entry, 700));
        assert(options.TrustServerCertificate && options.DataSource == "sql-host\\INSTANCE", "Explicit SQL instance and internal certificate opt-in are retained");
        entry.Copy().Database.DatabaseName = "changed";
        assert(entry.Database.DatabaseName == "Monitoring;Database", "Editing a copied SQL entry cannot mutate saved settings");

        string sqlPath = Path.Combine(root, "monitoring", "servers.json");
        var configuration = new AppConfiguration();
        configuration.Servers.Add(entry);
        new ConfigurationStore(sqlPath).Save(configuration);
        new ConfigurationStore(sqlPath).Save(configuration);
        assert(!File.ReadAllText(sqlPath).Contains("fixture;sql=password") && !File.ReadAllText(sqlPath + ".bak").Contains("fixture;sql=password"),
            "SQL passwords are DPAPI protected in configuration and its backup");
        assert(ProtectedSecrets.Unprotect(new ConfigurationStore(sqlPath).Load().Servers[0].Database.ProtectedPassword) == "fixture;sql=password",
            "Protected SQL credentials reopen under the same Windows profile");
        bool rejected = false;
        try { ProtectedSecrets.Unprotect("not-base64"); } catch (InvalidOperationException) { rejected = true; }
        assert(rejected, "Unreadable secrets require re-entry instead of falling back to plaintext");

        string legacyPath = Path.Combine(root, "monitoring", "v2.json");
        const string legacy = "{\"Version\":2,\"TimeoutMs\":3000,\"IntervalSeconds\":30,\"Servers\":[{\"Id\":\"preserve-id\",\"Name\":\"เครื่องเดิม\",\"Host\":\"localhost\"}]}";
        File.WriteAllText(legacyPath, legacy);
        AppConfiguration migrated = new ConfigurationStore(legacyPath).Load();
        assert(migrated.Version == 3 && migrated.Servers.Single().Id == "preserve-id" && !migrated.Copy().Servers[0].Database.Enabled
            && File.ReadAllText(legacyPath) == legacy && !File.Exists(legacyPath + ".bak"),
            "Version 2 settings migrate in memory with IDs preserved and SQL monitoring disabled");

        string alertPath = Path.Combine(root, "monitoring", "alerts.json");
        AlertSettings settings = ValidSettings();
        var store = new AlertSettingsStore(alertPath);
        store.Save(settings);
        store.Save(settings);
        assert(!File.ReadAllText(alertPath).Contains("fixture-refresh") && !File.ReadAllText(alertPath).Contains("fixture-ntfy")
            && !File.ReadAllText(alertPath + ".bak").Contains("fixture-refresh"), "OAuth and ntfy credentials remain protected on disk and in backups");
        File.WriteAllText(alertPath, "corrupt alert settings");
        store = new AlertSettingsStore(alertPath);
        rejected = false;
        try { store.Save(settings); } catch (IOException) { rejected = true; }
        assert(store.Warning != null && !store.Settings.Outlook.Enabled && !store.Settings.Ntfy.Enabled && rejected
            && File.ReadAllText(alertPath) == "corrupt alert settings", "Corrupt alert settings disable sending and are preserved for recovery");
        settings.Ntfy.ServerUrl = "http://example.com";
        rejected = false;
        try { settings.Ntfy.Validate(); } catch (ArgumentException) { rejected = true; }
        assert(rejected, "ntfy access tokens cannot be sent to an unencrypted remote server");
        settings.Outlook.Tenant = "common/../../other";
        rejected = false;
        try { settings.Outlook.ValidateIdentity(); } catch (ArgumentException) { rejected = true; }
        assert(rejected, "Microsoft tenant input cannot redirect credentials to another endpoint");
    }

    private sealed class FixtureDatabase : IDatabaseProbe
    {
        public bool Success;
        public int Calls;

        public Task<ProbeResult> CheckAsync(ServerEntry server, int timeoutMs, CancellationToken cancellation)
        {
            Calls++;
            return Task.FromResult(new ProbeResult { Success = Success, Message = "isolated database fixture", Milliseconds = Success ? (long?)12 : null });
        }
    }

    private static async Task VerifyDatabaseAsync()
    {
        ServerEntry entry = SqlServer();
        var database = new FixtureDatabase();
        var probe = new NetworkProbe(database);
        ServerResult result = await probe.CheckAsync(entry, 1000, CancellationToken.None);
        assert(result.Ping.Success && !result.Database.Success && result.Health == "warning" && result.Summary == "ฐานข้อมูลมีปัญหา",
            "A real successful loopback ping cannot conceal a failed database probe");
        entry.Host = "no-such-monitoring-host.invalid";
        database.Success = true;
        result = await probe.CheckAsync(entry, 1000, CancellationToken.None);
        assert(!result.Ping.Success && result.Database.Success && result.Health == "warning" && database.Calls == 2,
            "A failed DNS or ping result does not suppress an independent SQL check");
        entry.Database.Enabled = false;
        await probe.CheckAsync(entry, 1000, CancellationToken.None);
        assert(database.Calls == 2, "Ping-only entries make no SQL connection attempt");

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        TcpClient accepted = null;
        try
        {
            entry = SqlServer();
            entry.Database.DataSource = "tcp:127.0.0.1," + ((IPEndPoint)listener.LocalEndpoint).Port;
            Task<TcpClient> accept = listener.AcceptTcpClientAsync();
            var watch = Stopwatch.StartNew();
            Task<ProbeResult> checking = new DatabaseProbe().CheckAsync(entry, 700, CancellationToken.None);
            Task finished = await Task.WhenAny(checking, Task.Delay(8000));
            if (accept.IsCompleted) accepted = await accept;
            assert(finished == checking, "A real SQL handshake stalled by a local TCP fixture finishes within a bounded deadline");
            ProbeResult failure = await checking;
            assert(!failure.Success && watch.ElapsedMilliseconds < 8000 && !failure.Message.Contains("fixture;sql=password")
                && !failure.Message.Contains("monitor_user"), "SQL handshake failures report unavailable without exposing connection credentials");
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                bool observed = false;
                try { await new DatabaseProbe().CheckAsync(entry, 700, canceled.Token); } catch (OperationCanceledException) { observed = true; }
                assert(observed, "Canceling monitoring propagates through the database probe");
            }
        }
        finally
        {
            if (accepted != null) accepted.Close();
            listener.Stop();
        }
    }

    private static AlertSettings ValidSettings()
    {
        var settings = new AlertSettings();
        settings.Outlook.Enabled = true;
        settings.Outlook.ClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        settings.Outlook.Recipients = "one@example.com;two@example.com;ONE@example.com";
        settings.Outlook.ProtectedRefreshToken = ProtectedSecrets.Protect("fixture-refresh");
        settings.Ntfy.Enabled = true;
        settings.Ntfy.ServerUrl = "https://ntfy.example.com";
        settings.Ntfy.Topic = "monitor-fixture";
        settings.Ntfy.ProtectedToken = ProtectedSecrets.Protect("fixture-ntfy");
        return settings;
    }

    private sealed class RequestRecord
    {
        public Uri Uri;
        public string Method;
        public string Authorization;
        public string Body;
    }

    private sealed class FixtureHttp : HttpMessageHandler
    {
        public readonly List<RequestRecord> Requests = new List<RequestRecord>();
        public Func<int, HttpResponseMessage> Response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests.Add(new RequestRecord { Uri = request.RequestUri, Method = request.Method.Method,
                Authorization = request.Headers.Authorization == null ? "" : request.Headers.Authorization.ToString(),
                Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync() });
            return Response(Requests.Count);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static async Task VerifyDeliveryAsync()
    {
        AlertSettings settings = ValidSettings();
        var alert = new HealthAlert { Title = "ฐานข้อมูลมีปัญหา", Message = "เครื่องทดสอบเชื่อมต่อฐานข้อมูลไม่ได้" };
        var microsoft = new FixtureHttp { Response = number => number == 1
            ? Json(HttpStatusCode.OK, "{\"access_token\":\"fixture-access\",\"refresh_token\":\"fixture-rotated\"}")
            : Json(HttpStatusCode.Accepted, "") };
        var ntfy = new FixtureHttp { Response = number => Json(HttpStatusCode.ServiceUnavailable, "unavailable") };
        using (var service = new AlertDeliveryService(new MicrosoftMail(microsoft), new NtfySender(ntfy)))
        {
            ChannelDelivery[] reports = await service.SendAsync(settings, alert, CancellationToken.None);
            assert(reports.Single(item => item.Channel == "Outlook").Success && !reports.Single(item => item.Channel == "ntfy").Success,
                "A failed ntfy channel cannot suppress Outlook delivery");
            assert(ProtectedSecrets.Unprotect(reports.Single(item => item.Channel == "Outlook").ProtectedRefreshToken) == "fixture-rotated",
                "A rotated Microsoft refresh token returns protected for persistence");
            assert(microsoft.Requests.Count == 2 && microsoft.Requests[0].Uri.Host == "login.microsoftonline.com"
                && microsoft.Requests[0].Body.Contains("grant_type=refresh_token") && microsoft.Requests[0].Body.Contains("fixture-refresh")
                && microsoft.Requests[1].Uri.AbsoluteUri == "https://graph.microsoft.com/v1.0/me/sendMail"
                && microsoft.Requests[1].Authorization == "Bearer fixture-access", "Outlook refreshes authorization and uses the delegated Microsoft Graph sendMail endpoint");
            string mail = microsoft.Requests[1].Body;
            assert(mail.Contains(alert.Message) && mail.Contains("one@example.com") && mail.Contains("two@example.com")
                && !mail.Contains("fixture-refresh") && !mail.Contains("fixture-access") && !mail.Contains("ONE@example.com"),
                "Mail JSON preserves Thai text, deduplicates recipients and excludes credentials");
            RequestRecord publish = ntfy.Requests.Single();
            assert(publish.Method == "POST" && publish.Uri.AbsoluteUri == "https://ntfy.example.com/"
                && publish.Authorization == "Bearer fixture-ntfy" && publish.Body.Contains(alert.Message)
                && publish.Body.Contains("\"topic\":\"monitor-fixture\"") && publish.Body.Contains("\"priority\":4"),
                "ntfy publishes UTF-8 JSON to the server root with the configured topic and authorization");
        }

        microsoft = new FixtureHttp { Response = number => Json(HttpStatusCode.Unauthorized, "fixture-refresh must never appear in errors") };
        ntfy = new FixtureHttp { Response = number => Json(HttpStatusCode.OK, "{}") };
        using (var service = new AlertDeliveryService(new MicrosoftMail(microsoft), new NtfySender(ntfy)))
        {
            ChannelDelivery[] reports = await service.SendAsync(settings, alert, CancellationToken.None);
            assert(!reports.Single(item => item.Channel == "Outlook").Success && reports.Single(item => item.Channel == "ntfy").Success
                && !reports.Any(item => item.Message.Contains("fixture-refresh")), "Outlook authorization failure is sanitized while ntfy still succeeds");
        }

        ntfy = new FixtureHttp { Response = number => new HttpResponseMessage(HttpStatusCode.Redirect)
            { Headers = { Location = new Uri("https://other.example.com/") } } };
        using (var sender = new NtfySender(ntfy))
        {
            bool rejected = false;
            try { await sender.SendAsync(settings.Ntfy, alert, CancellationToken.None); } catch (InvalidOperationException) { rejected = true; }
            assert(rejected && ntfy.Requests.Count == 1, "ntfy rejects redirects without retrying or forwarding tokens");
        }

        microsoft = new FixtureHttp { Response = number => Json(HttpStatusCode.OK, "{}") };
        ntfy = new FixtureHttp { Response = number => Json(HttpStatusCode.OK, "{}") };
        using (var service = new AlertDeliveryService(new MicrosoftMail(microsoft), new NtfySender(ntfy)))
        {
            ChannelDelivery[] reports = await service.SendAsync(new AlertSettings(), alert, CancellationToken.None);
            assert(reports.Length == 0 && microsoft.Requests.Count == 0 && ntfy.Requests.Count == 0,
                "Default disabled alert channels cause zero outgoing requests");
        }

        microsoft = new FixtureHttp { Response = number => number == 1
            ? Json(HttpStatusCode.OK, "{\"device_code\":\"fixture-device\",\"user_code\":\"FIXTURE\",\"expires_in\":60,\"interval\":1}")
            : number == 2 ? Json(HttpStatusCode.BadRequest, "{\"error\":\"authorization_pending\"}")
            : Json(HttpStatusCode.OK, "{\"refresh_token\":\"fixture-device-refresh\"}") };
        using (var sender = new MicrosoftMail(microsoft))
        {
            DeviceAuthorization code = await sender.BeginSignInAsync(settings.Outlook, CancellationToken.None);
            string secret = await sender.CompleteSignInAsync(settings.Outlook, code, CancellationToken.None);
            assert(code.UserCode == "FIXTURE" && ProtectedSecrets.Unprotect(secret) == "fixture-device-refresh" && microsoft.Requests.Count == 3
                && microsoft.Requests[0].Body.Contains("offline_access") && microsoft.Requests[0].Body.Contains("Mail.Send")
                && microsoft.Requests[2].Body.Contains("fixture-device"), "Microsoft device sign-in handles authorization_pending and protects the resulting refresh token");
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                bool observed = false;
                try { await sender.CompleteSignInAsync(settings.Outlook, code, canceled.Token); } catch (OperationCanceledException) { observed = true; }
                assert(observed && microsoft.Requests.Count == 3, "Canceling Microsoft sign-in stops polling without another request");
            }
        }
    }

    private static Control Find(Control parent, string name)
    {
        return parent.Controls.Find(name, true).Single();
    }

    private static void Capture(Form form, string path)
    {
        Application.DoEvents();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(path);
        }
    }

    private static void LayoutPreview(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) LayoutPreview(child);
    }

    private static void VerifyDialogs(string root)
    {
        Assembly app = typeof(MainForm).Assembly;
        using (var dialog = (Form)Activator.CreateInstance(app.GetType("ServerBotTracker.ServerDialog"), new object[] { SqlServer() }))
        {
            dialog.Show();
            var tabs = dialog.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            Capture(dialog, Path.Combine(root, "sql-settings.png"));
            var password = (TextBox)Find(dialog, "SqlPasswordInput");
            assert(password.UseSystemPasswordChar && password.Text == "" && Find(dialog, "TestDatabaseButton").Visible,
                "SQL editor masks new passwords and leaves the saved password concealed");
            var read = dialog.GetType().GetMethod("ReadEntry", Instance);
            ServerEntry value = (ServerEntry)read.Invoke(dialog, null);
            assert(ProtectedSecrets.Unprotect(value.Database.ProtectedPassword) == "fixture;sql=password", "Leaving the SQL password blank preserves its protected value");
            dialog.Close();
        }
        using (var dialog = (Form)Activator.CreateInstance(app.GetType("ServerBotTracker.AlertSettingsDialog"), new object[] { new AlertSettings() }))
        {
            dialog.Show();
            Capture(dialog, Path.Combine(root, "outlook-settings.png"));
            var tabs = dialog.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            ((TextBox)Find(dialog, "NtfyTopic")).Text = "later-configuration";
            ((TextBox)Find(dialog, "NtfyToken")).Text = "dialog-fixture-token";
            Capture(dialog, Path.Combine(root, "ntfy-settings.png"));
            var read = dialog.GetType().GetMethod("ReadSettings", Instance);
            AlertSettings value = (AlertSettings)read.Invoke(dialog, null);
            assert(!value.Outlook.Enabled && !value.Ntfy.Enabled && value.Ntfy.Topic == "later-configuration"
                && ProtectedSecrets.Unprotect(value.Ntfy.ProtectedToken) == "dialog-fixture-token"
                && ((TextBox)Find(dialog, "NtfyToken")).UseSystemPasswordChar,
                "Alert settings can be prepared with channels disabled and token input protected");
            dialog.Close();
        }
    }

    private static void VerifyDashboardFilters(string root)
    {
        string path = Path.Combine(root, "dashboard-filter-fixture", "servers.json");
        var configuration = new AppConfiguration { TimeoutMs = 1000 };
        foreach (string name in new[] { "กลุ่มปกติ", "SQL ต้องตรวจสอบ", "เครื่องมีปัญหา", "ยังไม่ตรวจ", "รอผล SQL" })
            configuration.Servers.Add(new ServerEntry { Name = name, Host = "127.0.0.1" });
        configuration.Servers[2].Host = "no-such-filter-host.invalid";
        var store = new ConfigurationStore(path);
        store.Save(configuration);
        string savedConfiguration = File.ReadAllText(path);
        using (var form = new MainForm(store, false))
        {
            Exception failure = null;
            form.Shown += async delegate
            {
                try
                {
                    var results = (Dictionary<string, ServerResult>)typeof(MainForm).GetField("results", Instance).GetValue(form);
                    string normal = configuration.Servers[0].Id;
                    string mixed = configuration.Servers[1].Id;
                    string down = configuration.Servers[2].Id;
                    string pending = configuration.Servers[4].Id;
                    DateTime previousCheck = DateTime.Now.AddMinutes(-5);
                    results[normal] = new ServerResult { ServerId = normal, CheckedAt = previousCheck, Ping = new ProbeResult { Success = true } };
                    results[mixed] = new ServerResult { ServerId = mixed, CheckedAt = previousCheck, Ping = new ProbeResult { Success = true },
                        DatabaseEnabled = true, Database = new ProbeResult { Success = false } };
                    results[down] = new ServerResult { ServerId = down, CheckedAt = previousCheck, Ping = new ProbeResult { Success = false } };
                    results[pending] = new ServerResult { ServerId = pending, CheckedAt = previousCheck, Ping = new ProbeResult { Success = true }, DatabaseEnabled = true };
                    typeof(MainForm).GetMethod("RenderRows", Instance).Invoke(form, null);
                    var cards = (FlowLayoutPanel)Find(form, "ServerCards");
                    var grid = (DataGridView)Find(form, "ServerGrid");
                    var filters = Enumerable.Range(0, 4).Select(index => (Button)Find(form, "DashboardFilter" + index)).ToArray();
                    assert(cards.Controls.OfType<ServerCard>().Count(item => item.Visible) == 5
                        && filters[0].Text == "ทั้งหมด (5)", "Dashboard starts with every server and the requested clickable Thai filter labels");
                    filters[1].PerformClick();
                    assert(cards.Controls.OfType<ServerCard>().Where(item => item.Visible).Single().ServerId == normal
                        && filters[1].Text == "เชื่อมต่อปกติ (1)", "Normal filter includes only completed healthy results");
                    filters[2].PerformClick();
                    assert(cards.Controls.OfType<ServerCard>().Count(item => item.Visible) == 3
                        && filters[2].Text == "ต้องตรวจสอบ (3)", "Review filter includes mixed SQL/Ping, unchecked and pending results without calling them healthy");
                    filters[3].PerformClick();
                    assert(cards.Controls.OfType<ServerCard>().Where(item => item.Visible).Single().ServerId == down
                        && filters[3].Text == "มีปัญหา (1)", "Problem filter includes failed connectivity results");
                    var search = (TextBox)Find(form, "SearchBox");
                    search.Text = "รอผล";
                    assert(!cards.Controls.OfType<ServerCard>().Any(item => item.Visible) && Find(form, "EmptyDashboard").Visible,
                        "An empty combination of status filter and search displays a clear empty state");
                    filters[2].PerformClick();
                    assert(cards.Controls.OfType<ServerCard>().Where(item => item.Visible).Single().ServerId == pending
                        && filters[2].Text == "ต้องตรวจสอบ (3)", "Search combines with the selected group while counters retain global totals");
                    search.Text = "";
                    grid.Rows[0].Selected = true;
                    filters[3].PerformClick();
                    assert(grid.SelectedRows.Count == 0, "Changing status groups clears selections so hidden servers are not selected accidentally");
                    filters[2].PerformClick();
                    results[mixed] = new ServerResult { ServerId = mixed, CheckedAt = DateTime.Now, Ping = new ProbeResult { Success = true },
                        DatabaseEnabled = true, Database = new ProbeResult { Success = true } };
                    typeof(MainForm).GetMethod("UpdateCard", Instance).Invoke(form, new object[] { results[mixed] });
                    typeof(MainForm).GetMethod("UpdateTotals", Instance).Invoke(form, null);
                    assert(cards.Controls.OfType<ServerCard>().Count(item => item.Visible) == 2 && filters[2].Text == "ต้องตรวจสอบ (2)",
                        "Live recovery immediately removes a server from the review filter and updates its count");
                    Capture(form, Path.Combine(root, "dashboard-review-filter.png"));
                    typeof(MainForm).GetMethod("ShowDashboard", Instance).Invoke(form, new object[] { false });
                    assert(grid.Visible && grid.Rows.Count == 5 && !cards.Visible, "Listing remains independent of Dashboard status filters");
                    filters[1].PerformClick();
                    assert(cards.Visible && !grid.Visible, "Clicking a status group from Listing opens the Dashboard group");
                    Task checking = form.RunChecksAsync(false);
                    filters[3].PerformClick();
                    await checking;
                    assert(results.Count == 5 && results[down].CheckedAt > previousCheck && !results[down].Ping.Success
                        && results.Values.Count(item => item.Health == "healthy") == 4,
                        "Check All still performs real checks for hidden servers while the filter can change during monitoring");
                    assert(File.ReadAllText(path) == savedConfiguration && !File.Exists(path + ".bak"),
                        "Dashboard filters never rewrite the server list, SQL settings or configuration schema");
                    filters[0].PerformClick();
                    Capture(form, Path.Combine(root, "dashboard-all-filter.png"));
                }
                catch (Exception error) { failure = error; }
                finally { form.Close(); }
            };
            Application.Run(form);
            if (failure != null) throw failure;
        }
    }

    private static void VerifyTransitions(string root)
    {
        string path = Path.Combine(root, "transition-fixture", "servers.json");
        var store = new ConfigurationStore(path);
        var configuration = new AppConfiguration();
        ServerEntry entry = SqlServer();
        configuration.Servers.Add(entry);
        for (int index = 2; index <= 36; index++)
            configuration.Servers.Add(new ServerEntry { Name = "เครื่องทดสอบ " + index.ToString("00"), Host = "127.0.0.1" });
        store.Save(configuration);
        using (var previewHost = new Form())
        using (var form = new MainForm(store, false))
        {
            // A child preview can exercise a wide layout even on a small headless desktop.
            form.TopLevel = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = Point.Empty;
            previewHost.Controls.Add(form);
            previewHost.Show();
            form.Show();
            float uiScale = form.DeviceDpi / 96f;
            form.ClientSize = new Size((int)(1260 * uiScale), (int)(820 * uiScale));
            Application.DoEvents();
            LayoutPreview(form);
            var cards = (FlowLayoutPanel)Find(form, "ServerCards");
            var card = cards.Controls.OfType<ServerCard>().Single(item => item.ServerId == entry.Id);
            typeof(ServerCard).GetMethod("OnClick", Instance).Invoke(card, new object[] { EventArgs.Empty });
            var grid = (DataGridView)typeof(MainForm).GetField("grid", Instance).GetValue(form);
            assert(card.Selected && grid.SelectedRows.Count == 1, "Selecting a dashboard card selects the same server for edit and check actions");
            MethodInfo showDashboard = typeof(MainForm).GetMethod("ShowDashboard", Instance);
            showDashboard.Invoke(form, new object[] { false });
            assert(grid.Visible && !cards.Visible, "Top navigation switches to the server list without changing entries");
            showDashboard.Invoke(form, new object[] { true });
            assert(cards.Visible && !grid.Visible, "Top navigation restores the dashboard view");
            LayoutPreview(form);
            string dimensions = "client=" + form.ClientSize + ", layout=" + form.Controls[0].Size + ", content=" + cards.Parent.Size + ", panel=" + cards.ClientSize + ", dpi=" + form.DeviceDpi
                + ", card=" + card.Size + ", columns=" + cards.Controls.OfType<ServerCard>().Select(item => item.Left).Distinct().Count();
            Console.WriteLine("Compact layout reference: " + dimensions);
            assert(cards.Controls.OfType<ServerCard>().All(item => item.Height <= form.DeviceDpi / 96f * 90)
                && cards.Controls.OfType<ServerCard>().Select(item => item.Left).Distinct().Count() >= 6,
                "Compact dashboard fits at least six small cards per row at the reference window size: " + dimensions);
            assert(card.StatusText == "ยังไม่ตรวจ", "An unchecked compact card cannot falsely report normal");
            var center = (NotificationCenter)typeof(MainForm).GetField("notifications", Instance).GetValue(form);
            MethodInfo publish = typeof(MainForm).GetMethod("PublishServerAlert", Instance);
            var result = new ServerResult { Ping = new ProbeResult { Success = true, Message = "ping passes" }, DatabaseEnabled = true,
                Database = new ProbeResult { Success = false, Message = "database unavailable" } };
            publish.Invoke(form, new object[] { entry, result });
            assert(center.State.Items.Count == 0, "A single database failure does not issue an outage alert");
            publish.Invoke(form, new object[] { entry, result });
            publish.Invoke(form, new object[] { entry, result });
            assert(center.State.Items.Count(item => item.Kind == "database-down") == 1 && !center.State.Items.Any(item => item.Kind == "down"),
                "Two database failures produce one database alert while a healthy server stays healthy");
            result.Database.Success = true;
            publish.Invoke(form, new object[] { entry, result });
            publish.Invoke(form, new object[] { entry, result });
            assert(center.State.Items.Count(item => item.Kind == "database-recovered") == 1, "Database recovery produces one separate recovery alert");
            MethodInfo updateCard = typeof(MainForm).GetMethod("UpdateCard", Instance);
            result.ServerId = entry.Id;
            result.CheckedAt = DateTime.Now;
            updateCard.Invoke(form, new object[] { result });
            assert(card.StatusText == "ปกติ", "Compact cards use the requested normal status when all enabled checks pass");
            result.Database.Success = false;
            updateCard.Invoke(form, new object[] { result });
            assert(card.StatusText == "มีปัญหา", "Compact cards show a problem when Ping passes but SQL fails");
            result.Ping.Success = false;
            updateCard.Invoke(form, new object[] { result });
            assert(card.StatusText == "เชื่อมต่อไม่ได้", "Compact cards show disconnected when both checks fail");
            int fixtureIndex = 0;
            var fixtureResults = (Dictionary<string, ServerResult>)typeof(MainForm).GetField("results", Instance).GetValue(form);
            MethodInfo updateRow = typeof(MainForm).GetMethod("UpdateRow", Instance);
            foreach (ServerEntry fixture in configuration.Servers)
            {
                var fixtureResult = new ServerResult
                {
                    ServerId = fixture.Id, CheckedAt = DateTime.Now,
                    Ping = new ProbeResult { Success = fixtureIndex % 9 != 7, Message = "UI fixture" },
                    DatabaseEnabled = fixtureIndex % 9 == 4,
                    Database = new ProbeResult { Success = false, Message = "UI fixture" }
                };
                fixtureResults[fixture.Id] = fixtureResult;
                updateCard.Invoke(form, new object[] { fixtureResult });
                updateRow.Invoke(form, new object[] { grid.Rows[fixtureIndex], fixtureResult });
                fixtureIndex++;
            }
            typeof(MainForm).GetMethod("UpdateTotals", Instance).Invoke(form, null);
            Capture(form, Path.Combine(root, "compact-dashboard.png"));
            form.ClientSize = new Size((int)(1900 * uiScale), (int)(960 * uiScale));
            Application.DoEvents();
            LayoutPreview(form);
            assert(cards.Controls.OfType<ServerCard>().Select(item => item.Left).Distinct().Count() >= 9,
                "Widening the dashboard adds columns instead of inflating cards or limiting them to four columns");
            Capture(form, Path.Combine(root, "compact-dashboard-wide.png"));
            showDashboard.Invoke(form, new object[] { false });
            Capture(form, Path.Combine(root, "listing-wide.png"));
            form.Close();
            previewHost.Close();
        }
    }
}
