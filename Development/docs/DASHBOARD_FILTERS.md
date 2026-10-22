# Dashboard status filters — FKP Server Monitor 1.8.2

Date: 2026-10-22, Asia/Bangkok. The owner authorized creating the missing GUI specification, then replaced the specification instructions with a single shared UNC location. The shared file was created at `\\10.0.0.97\kuma$\Z\Codex\GUI_SPEC.md`; the superseded project-local draft was moved into the dated Archive backup. No program-local GUI_SPEC.md remains.

## Analysis and data flow

`Program.Main` opens `MainForm`; the unchanged configuration store supplies server entries. `RunChecksAsync` snapshots configured entries, checks Ping and optional Microsoft SQL Server independently, stores each `ServerResult` and calls `UpdateCard`/`UpdateTotals`. The Dashboard now filters visibility using these existing health results. It does not change the entry snapshot, credentials, configuration schema, network probes, SQL query or alert trackers.

The existing count strip is now four native buttons: ทั้งหมด, เชื่อมต่อปกติ, ต้องตรวจสอบ, มีปัญหา. Normal means every enabled check passes. Review includes unchecked/pending results and mixed Ping/SQL outcomes. Problem means all enabled checks fail. Counts cover the complete list; search combines with the selected Dashboard group. Listing retains its independent detailed rows. Clicking a group from Listing opens the corresponding Dashboard view. Group selection is session-only and resets to All at launch.

Live results immediately update membership. A group change clears selection and hidden cards are deselected, preventing selected-only checks/deletion from acting on invisible selections. Check All still checks all configured entries. Empty groups show a Thai explanation. The compact dark top-menu layout is preserved as the existing project's explicitly stated override to the generic shared GUI defaults.

## Changes and why

| File | Function / change | Purpose |
| --- | --- | --- |
| `src/MainForm.Dashboard.cs` | state fields, `BuildSummary`, `SetDashboardFilter`, `MatchesDashboardFilter`, `ApplyDashboardFilter` | Four clickable groups, selected styling, classification and safe visibility |
| `src/MainForm.Dashboard.cs` | `ShowDashboard`, `RenderCards`, `UpdateCard` | Live filtering, empty-state updates and Listing navigation |
| `src/MainForm.cs` | `UpdateTotals` | Global group counts and actual displayed-card count |
| `src/AssemblyInfo.cs` | version attributes | App/setup version 1.8.2 |
| `tools/Package.ps1` | release notes | Describe the current filtering behavior |
| `tools/checks/MonitoringVerification.cs` | `Run`, `VerifyDashboardFilters` | Execute all four groups, search, pending/mixed states, live recovery, selection and actual hidden-server checks |
| `tools/checks/InstallationScopeVerification.cs` | `VerifyMachineProcesses` | First-unpublished-feed fixture follows the patch version; production validation is unchanged |
| `docs/USAGE.th.md`, root README | Thai instructions | Explain counts, groups, Listing and monitoring independence |
| Shared UNC `GUI_SPEC.md` | new shared document | Full GUI standard and initial light/dark token values; no pre-existing tokens changed |

Source backup: `Archive/backups/20261022-dashboard-filters-4b4075`. The retired local GUI draft is `GUI_SPEC_retired_20261022.md` there. The shared directory was reachable and its file absent, so creation used FileMode.CreateNew and UTF-8 with read-back comparison. There was no pre-existing shared file to back up. Any future shared modification requires a dated sibling backup; changing an existing token requires owner approval.

## Verification — commands actually executed

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
& .\Development\tools\Build.ps1
& .\Development\tools\Package.ps1
& .\Development\tools\Verify.ps1 | Select-Object -Last 6
git diff --check
```

Baseline before source edits: `PASS: 178 checks`.

Final local output:

```text
Built: \\10.0.0.97\kuma$\Z\Projects\FKP_Server_Monitor\Program\FKPServerMonitor.exe
Prepared release only; nothing uploaded: \\10.0.0.97\kuma$\Z\Projects\FKP_Server_Monitor\Archive\releases\v1.8.2
PASS: Closing during a check cancels safely without accessing disposed controls
PASS: Distributable EXE launches its real entry point and closes normally
PASS: 190 checks
Verified release SHA256: 14DA63531C323E706C18D5758C4ECCDB0B7E6D21FBF6B2BABD28F2F4BC942084
Verified installer SHA256: 96B093292CC6E8B2A1BF3DD65E39372B3444B436BF58D7683FA76CB8EF3C80F5
Evidence in Archive: \\10.0.0.97\kuma$\Z\Projects\FKP_Server_Monitor\Archive\verification\verification-20261022-093527-33fb4425
Shared spec exists: True
Program-local spec exists: False
Packaged guide matches source: True
```

`git diff --check` produced no output and exited successfully. Compilation treats warnings as errors. Existing GUI/CRUD/cancellation, SQL isolation/timeout, mocked mail/ntfy delivery, installation/removal, package/update and rollback checks remain in the suite. New checks use a real GUI loop and actual loopback/invalid-DNS checks to establish that hidden servers are still monitored. Both filtered and All screenshots were rendered and visually reviewed. The portable ZIP still contains only app EXE + required runtime config; its SHA-256 is `0D7224B62BFE66A1123FD2315322A7CA110EAF1E9B29C6C32873A15794541A5F`.

Shared spec SHA-256 at creation: `04297365E2080BF0FE71D57D3F90F20A90F92F4D0EFC44FD7D1F71794F8FCE65`.

## Risks and limits

- Configuration/data formats, production SQL schema, credentials and alert thresholds are unchanged. All/selected-check scheduling remains unchanged. Status filtering itself never saves configuration.
- No new dependencies were added: existing .NET Framework 4.8 and Windows Forms remain the runtime.
- Live production MSSQL login, actual Outlook mail delivery and actual ntfy publication: **NOT RUN**, because this UI change uses no production accounts/endpoints. Existing controlled backend checks passed.
- Administrator-only Program Files/HKLM operations are not claimed for this non-elevated local process; the existing CI runner covers isolated elevated fixtures. An unpublished renamed official feed remains an explicit CI fixture limitation, not a relaxed production update rule.
- Converting this existing app to the generic sidebar/history/light-theme standard and auditing other applications against the new shared specification: **NOT RUN**, outside this filter-only task. Existing project-specific layout differences are documented in the shared spec.
- Source push does not publish a release or automatically update installed users.

## Complete added or modified functions

The following blocks contain the complete function bodies from the compiled source, identified by file and function.

### Development/src/MainForm.Dashboard.cs — ShowDashboard

```csharp
        private void ShowDashboard(bool showCards)
        {
            cards.Visible = showCards; grid.Visible = !showCards;
            if (showCards) cards.BringToFront(); else grid.BringToFront();
            DashboardStyle.StyleButton(dashboardView, showCards); DashboardStyle.StyleButton(listView, !showCards);
            if (showCards && !busy) progress.Text = "ภาพรวมสถานะเซิร์ฟเวอร์";
            if (showCards) ApplyDashboardFilter();
            UpdateTotals();
            ShowSelectedDetails();
        }
```

### Development/src/MainForm.Dashboard.cs — BuildSummary

```csharp
        private Control BuildSummary()
        {
            var summary = new TableLayoutPanel { Name = "StatusSummary", Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 0, 0, 6) };
            for (int index = 0; index < 4; index++)
            {
                summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                int filterIndex = index;
                Button button = Theme.Button(dashboardFilterLabels[index], false);
                button.Name = "DashboardFilter" + index;
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 0, index == 3 ? 0 : 8, 0);
                button.Click += delegate { SetDashboardFilter(filterIndex); };
                DashboardStyle.StyleButton(button, index == dashboardFilter);
                summaryFilters[index] = button;
                summary.Controls.Add(button, index, 0);
            }
            return summary;
        }
```

### Development/src/MainForm.Dashboard.cs — SetDashboardFilter

```csharp
        private void SetDashboardFilter(int index)
        {
            if (index < 0 || index >= dashboardFilterLabels.Length)
                throw new ArgumentOutOfRangeException("index");
            dashboardFilter = index;
            grid.ClearSelection();
            for (int item = 0; item < summaryFilters.Length; item++)
                DashboardStyle.StyleButton(summaryFilters[item], item == dashboardFilter);
            ShowDashboard(true);
        }
```

### Development/src/MainForm.Dashboard.cs — MatchesDashboardFilter

```csharp
        private bool MatchesDashboardFilter(string serverId)
        {
            if (dashboardFilter == 0) return true;
            ServerResult result;
            string health = results.TryGetValue(serverId, out result) ? result.Health : "pending";
            if (dashboardFilter == 1) return health == "healthy";
            if (dashboardFilter == 2) return health == "warning" || health == "pending";
            return health == "down";
        }
```

### Development/src/MainForm.Dashboard.cs — ApplyDashboardFilter

```csharp
        private void ApplyDashboardFilter()
        {
            bool anyMatch = false;
            cards.SuspendLayout();
            try
            {
                foreach (KeyValuePair<string, ServerCard> item in cardViews)
                {
                    bool matches = MatchesDashboardFilter(item.Key);
                    item.Value.Visible = matches;
                    if (matches) anyMatch = true;
                    DataGridViewRow row;
                    if (!matches && cards.Visible && visibleRows.TryGetValue(item.Key, out row) && row.Selected)
                        row.Selected = false;
                }
                if (emptyDashboard != null)
                {
                    emptyDashboard.Text = configuration.Servers.Count == 0 ? "เพิ่มเซิร์ฟเวอร์เพื่อเริ่มติดตาม"
                        : dashboardFilter == 0 ? "ไม่พบรายการที่ตรงกับคำค้นหา" : "ไม่พบเซิร์ฟเวอร์ในกลุ่มที่เลือก";
                    emptyDashboard.Visible = !anyMatch;
                }
            }
            finally { cards.ResumeLayout(); }
        }
```

### Development/src/MainForm.Dashboard.cs — RenderCards

```csharp
        private void RenderCards()
        {
            cards.SuspendLayout();
            try
            {
                foreach (Control control in cards.Controls.Cast<Control>().ToArray()) control.Dispose();
                cards.Controls.Clear(); cardViews.Clear();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    string id = (string)row.Tag;
                    ServerEntry server = configuration.Servers.First(item => item.Id == id);
                    var card = new ServerCard(server);
                    ServerResult result; if (results.TryGetValue(id, out result)) card.UpdateResult(result);
                    card.Selected = row.Selected;
                    card.SelectedByUser += delegate(object sender, EventArgs args) { SelectCard((ServerCard)sender, false); };
                    card.EditRequested += delegate(object sender, EventArgs args) { if (!busy) { SelectCard((ServerCard)sender, true); EditServer(sender, args); } };
                    cardViews[id] = card; cards.Controls.Add(card);
                }
                emptyDashboard = new Label { Name = "EmptyDashboard", ForeColor = DashboardStyle.Muted, AutoSize = true, Margin = new Padding(12, 24, 0, 0) };
                cards.Controls.Add(emptyDashboard);
                ResizeCards();
                ApplyDashboardFilter();
            }
            finally { cards.ResumeLayout(); }
        }
```

### Development/src/MainForm.Dashboard.cs — UpdateCard

```csharp
        private void UpdateCard(ServerResult result)
        {
            ServerCard card;
            if (cardViews.TryGetValue(result.ServerId, out card)) card.UpdateResult(result);
            ApplyDashboardFilter();
        }
```

### Development/src/MainForm.cs — UpdateTotals

```csharp
        private void UpdateTotals()
        {
            int passed = results.Values.Count(result => result.Health == "healthy");
            int failed = results.Values.Count(result => result.Health == "down");
            int[] counts = { configuration.Servers.Count, passed, configuration.Servers.Count - passed - failed, failed };
            for (int index = 0; index < counts.Length; index++)
                if (summaryFilters[index] != null)
                    summaryFilters[index].Text = dashboardFilterLabels[index] + " (" + counts[index].ToString(CultureInfo.InvariantCulture) + ")";
            int shown = cards.Visible ? cardViews.Keys.Count(MatchesDashboardFilter) : grid.Rows.Count;
            totals.Text = String.Format("ทั้งหมด {0}     ปกติ {1}     ต้องตรวจสอบ {2}     มีปัญหา {3}     ยังไม่ตรวจ {4}     แสดง {5}", configuration.Servers.Count, passed, counts[2], failed, configuration.Servers.Count - results.Count, shown);
        }
```

### Development/tools/checks/MonitoringVerification.cs — Run

```csharp
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
```

### Development/tools/checks/MonitoringVerification.cs — VerifyDashboardFilters

```csharp
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
```

### Development/tools/checks/InstallationScopeVerification.cs — VerifyMachineProcesses

```csharp
    private static void VerifyMachineProcesses(Assembly setup, string root, Action<bool, string> assert)
    {
        const string registry = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KumaServerBotTracker";
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "FKP Server Monitor.lnk");
        using (RegistryKey existing = Registry.LocalMachine.OpenSubKey(registry))
            if (existing != null || File.Exists(shortcut)) throw new IOException("CI machine integration requires an empty product registration and shortcut");
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ServerBotTracker-ProcessVerification-" + Guid.NewGuid().ToString("N"));
        MethodInfo broker = setup.GetType("ServerBotTracker.ElevationBroker", true).GetMethod("Run");
        bool installed = false;
        try
        {
            broker.Invoke(null, new object[] { "--elevated-install", target });
            installed = true;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(key != null && (string)key.GetValue("InstallLocation") == target && File.Exists(shortcut), "Real runas setup process installs a disposable Program Files copy and registers it for all users");
            File.Copy(Path.Combine(root, "OldVersion.exe"), Path.Combine(target, ReleaseChannel.ApplicationFile), true);
            File.WriteAllText(Path.Combine(target, "keep-user-data.txt"), "Keep machine fixture data");
            bool liveAvailable = true;
            using (var channel = new ReleaseChannel())
            {
                try { channel.GetLatestAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult(); }
                catch (System.Net.Http.HttpRequestException error)
                {
                    if (ReleaseChannel.CurrentVersion != new Version(1, 8, 2, 0) || !error.Message.Contains("404")) throw;
                    liveAvailable = false;
                    Console.WriteLine("SKIP: The first renamed public release is still unpublished in 1.8; update-fkp.json is unavailable. Live official-feed elevation remains unverified. Testing the packaged update in Program Files instead.");
                }
            }
            ReleaseManifest applied = null;
            if (!liveAvailable)
            {
                string stage = Path.Combine(root, "first-renamed-machine-update");
                Directory.CreateDirectory(stage);
                File.Copy(Path.Combine(root, "published-update.json"), Path.Combine(stage, "manifest.json"));
                File.Copy(Path.Combine(root, "published-package.zip"), Path.Combine(stage, "package.zip"));
                applied = JsonData.Read<ReleaseManifest>(Path.Combine(stage, "manifest.json"));
                UpdateInstaller.Apply(stage, target);
            }
            for (int attempt = 0; liveAvailable && attempt < 3 && applied == null; attempt++)
            {
                string stage = Path.Combine(root, "live-machine-update-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                ReleaseManifest manifest;
                using (var channel = new ReleaseChannel())
                {
                    manifest = channel.GetLatestAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    using (var stream = File.Create(Path.Combine(stage, "package.zip")))
                    {
                        var download = (System.Threading.Tasks.Task)typeof(ReleaseChannel).GetMethod("DownloadAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(channel, new object[] { new Uri("https://github.com/" + ReleaseChannel.OfficialRepository + "/releases/download/" + manifest.Tag + "/FKPServerMonitor-portable.zip"), stream, manifest.SizeBytes, System.Threading.CancellationToken.None });
                        download.GetAwaiter().GetResult();
                    }
                }
                JsonData.WriteAtomic(Path.Combine(stage, "manifest.json"), manifest);
                JsonData.WriteAtomic(Path.Combine(stage, "job.json"), new UpdateJob { TargetDirectory = target });
                string worker = Path.Combine(stage, "UpdateWorker.exe");
                File.Copy(Path.Combine(root, "FKPServerMonitor.exe"), worker);
                File.Copy(Path.Combine(root, "FKPServerMonitor.exe.config"), worker + ".config");
                string result = Path.Combine(stage, "ServerBotTracker-elevation-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(result);
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry, true)) key.SetValue("DisplayVersion", "0.9.0.0");
                using (Process process = Process.Start(new ProcessStartInfo(worker, "--apply-update-admin \"" + Path.Combine(stage, "job.json") + "\" \"" + result + "\"") { UseShellExecute = true, Verb = "runas", WorkingDirectory = stage }))
                {
                    if (!process.WaitForExit(60000)) throw new TimeoutException("Elevated update fixture did not finish");
                    string outcome = File.ReadAllText(Path.Combine(result, "result.txt"));
                    if (process.ExitCode != 0)
                    {
                        if (attempt < 2 && outcome.Contains("ชุดอัปเดตไม่ตรง")) continue;
                        throw new IOException(outcome);
                    }
                    applied = manifest;
                }
            }
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(applied != null && FileVersionInfo.GetVersionInfo(Path.Combine(target, ReleaseChannel.ApplicationFile)).FileVersion == applied.Version && (string)key.GetValue("DisplayVersion") == applied.Version && File.Exists(Path.Combine(target, "Uninstall.exe")), liveAvailable ? "Real administrator update process verifies the live official package, replaces protected files and updates HKLM display version" : "Packaged update replaces protected Program Files binaries and updates HKLM metadata before the first renamed public release");
            broker.Invoke(null, new object[] { "--elevated-remove", target });
            installed = false;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registry))
                assert(key == null && !File.Exists(shortcut) && !File.Exists(Path.Combine(target, "Uninstall.exe")) && File.ReadAllText(Path.Combine(target, "keep-user-data.txt")) == "Keep machine fixture data", "Real runas uninstaller removes the machine copy and registration while retaining unrelated data");
        }
        finally
        {
            if (installed) broker.Invoke(null, new object[] { "--elevated-remove", target });
            string data = Path.Combine(target, "keep-user-data.txt");
            if (File.Exists(data)) File.Delete(data);
            if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length == 0) Directory.Delete(target, false);
        }
    }
```

### Development/src/MainForm.Dashboard.cs — added/replaced state fields

```csharp
private readonly Button[] summaryFilters = new Button[4];
private readonly string[] dashboardFilterLabels = { "ทั้งหมด", "เชื่อมต่อปกติ", "ต้องตรวจสอบ", "มีปัญหา" };
private int dashboardFilter;
private Label emptyDashboard;
```

### Development/src/AssemblyInfo.cs — complete file

```csharp
using System.Reflection;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("FKP Server Monitor")]
[assembly: AssemblyDescription("Thai server and SQL Server dashboard with Outlook and ntfy alerts")]
[assembly: AssemblyCompany("FKP")]
[assembly: AssemblyProduct("FKP Server Monitor")]
[assembly: AssemblyVersion("1.8.2.0")]
[assembly: AssemblyFileVersion("1.8.2.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8")]
```

### Development/tools/Package.ps1 — complete file

```powershell
[CmdletBinding()]
param([string]$Notes = 'Click Dashboard counters to filter all, normal, review or problem servers. Pending and mixed Ping/SQL results stay in review. Search combines with the selected group; Check All, detailed Listing, SQL monitoring and alert delivery remain independent.')

$ErrorActionPreference = 'Stop'
$developmentRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent $developmentRoot
$programRoot = Join-Path $projectRoot 'Program'
$executablePath = Join-Path $programRoot 'FKPServerMonitor.exe'
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).FileVersion
$versionValue = [version]$version
$tag = 'v' + $(if ($versionValue.Revision -gt 0) { $versionValue.ToString(4) } else { $versionValue.ToString(3) })
$outputRoot = Join-Path $projectRoot ('Archive\releases\' + $tag)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$package = Join-Path $outputRoot 'FKPServerMonitor-portable.zip'
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
Compress-Archive -LiteralPath @($executablePath, (Join-Path $programRoot 'FKPServerMonitor.exe.config')) -DestinationPath $package
$manifest = [ordered]@{ Schema = 1; Version = $version; Tag = $tag; Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash; SizeBytes = (Get-Item -LiteralPath $package).Length; Notes = $Notes }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'update-fkp.json') -Encoding UTF8
# Older clients require the old filenames and product metadata. Keep their feed on
# the last compatible release instead of advertising a package they cannot load.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'legacy-update.json') -Destination (Join-Path $outputRoot 'update.json') -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setup = Join-Path $outputRoot 'FKPServerMonitor-Setup.exe'
$brandingRoot = Join-Path $developmentRoot 'assets\branding'
$arguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/warnaserror+',
    '/utf8output', '/codepage:65001', ('/out:' + $setup),
    ('/win32manifest:' + (Join-Path $developmentRoot 'src\app.manifest')),
    ('/win32icon:' + (Join-Path $brandingRoot 'server-tracker.ico')),
    ('/resource:' + $package + ',payload.zip'),
    ('/resource:' + (Join-Path $brandingRoot 'server-tracker.ico') + ',ServerBotTracker.Branding.Icon'),
    ('/resource:' + (Join-Path $brandingRoot 'server-tracker.png') + ',ServerBotTracker.Branding.Logo'),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
    (Join-Path $PSScriptRoot 'setup\Setup.cs'),
    (Join-Path $PSScriptRoot 'setup\SetupForm.cs'),
    (Join-Path $PSScriptRoot 'setup\InstallLocation.cs'),
    (Join-Path $PSScriptRoot 'setup\DesktopShortcut.cs'),
    (Join-Path $PSScriptRoot 'setup\UninstallTransaction.cs'),
    (Join-Path $PSScriptRoot 'setup\InstallationScope.cs'),
    (Join-Path $PSScriptRoot 'setup\RegistryBackup.cs'),
    (Join-Path $PSScriptRoot 'setup\SetupFiles.cs'),
    (Join-Path $developmentRoot 'src\AssemblyInfo.cs'),
    (Join-Path $developmentRoot 'src\ProductIdentity.cs'),
    (Join-Path $developmentRoot 'src\Branding.cs'),
    (Join-Path $developmentRoot 'src\ElevationBroker.cs'),
    (Join-Path $developmentRoot 'src\Theme.cs')
)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Copy-Item -LiteralPath $setup -Destination (Join-Path $programRoot 'FKPServerMonitor-Setup.exe') -Force
Get-FileHash -LiteralPath $setup,$package -Algorithm SHA256 | Select-Object Hash,Path
Write-Output ('Prepared release only; nothing uploaded: ' + $outputRoot)
```

Short commit message: feat: add live dashboard status filters
