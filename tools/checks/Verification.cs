using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServerBotTracker;

internal static class Verification
{
    private static readonly List<string> evidence = new List<string>();
    private static string root;

    [STAThread]
    private static int Main(string[] arguments)
    {
        root = arguments[0];
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Control.CheckForIllegalCrossThreadCalls = true;
            VerifyPersistence();
            VerifyValidation();
            VerifyLegacyConfiguration();
            VerifyDeadline();
            VerifyNetworkAndGui();
            VerifyLaunch();
            File.WriteAllLines(Path.Combine(root, "verification.txt"), evidence, new UTF8Encoding(true));
            Console.WriteLine("PASS: " + evidence.Count + " checks");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            evidence.Add("FAIL: " + exception);
            File.WriteAllLines(Path.Combine(root, "verification.txt"), evidence, new UTF8Encoding(true));
            return 1;
        }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        evidence.Add("PASS: " + description);
        Console.WriteLine("PASS: " + description);
    }

    private static void VerifyPersistence()
    {
        var store = new ConfigurationStore(Path.Combine(root, "fixture", "servers.json"));
        AppConfiguration configuration = store.Load();
        Assert(configuration.Servers.Count == 0, "First launch starts empty without writing settings");
        configuration.Servers.Add(new ServerEntry { Name = "เซิร์ฟเวอร์ทดสอบ", Host = "127.0.0.1" });
        store.Save(configuration);
        Assert(store.Load().Servers[0].Name == "เซิร์ฟเวอร์ทดสอบ", "Thai server list JSON round trip");
        configuration.TimeoutMs = 1500;
        store.Save(configuration);
        Assert(new ConfigurationStore(store.FilePath + ".bak").Load().TimeoutMs == 3000, "Atomic save retains the previous configuration backup");
        string original = File.ReadAllText(store.FilePath);
        configuration.Servers.Add(configuration.Servers[0].Copy());
        bool rejected = false;
        try { store.Save(configuration); } catch (ArgumentException) { rejected = true; }
        Assert(rejected && File.ReadAllText(store.FilePath) == original, "Invalid duplicate IDs cannot overwrite saved configuration");
        File.WriteAllText(store.FilePath, "corrupt JSON");
        rejected = false;
        try { store.Load(); } catch (Exception) { rejected = true; }
        Assert(rejected && File.ReadAllText(store.FilePath) == "corrupt JSON", "Corrupt settings are reported and remain untouched");
        Assert(Directory.GetFiles(Path.GetDirectoryName(store.FilePath), "*.tmp").Length == 0, "Atomic save leaves no temporary files");
    }

    private static void VerifyValidation()
    {
        bool rejected = false;
        try { new ServerEntry { Name = "Bad URL", Host = "https://example.com" }.Validate(); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "URL input is rejected where an IP or hostname is required");
        new ServerEntry { Name = "IPv6", Host = "::1" }.Validate();
        Assert(true, "IPv6 literals are accepted without a port");
        rejected = false;
        try { new ServerEntry { Name = "Invalid host", Host = "localhost:443" }.Validate(); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "A hostname with an appended port is rejected");
        Assert(new ServerResult().Summary == "ยังไม่ตรวจ", "A result without a completed ping cannot falsely report pass");
    }

    private static void VerifyLegacyConfiguration()
    {
        string path = Path.Combine(root, "legacy.json");
        string legacy = "{\"Version\":1,\"TimeoutMs\":3000,\"IntervalSeconds\":30,\"Servers\":[{\"Id\":\"old-one\",\"Name\":\"รายการเดิมหนึ่ง\",\"Host\":\"127.0.0.1\",\"Port\":443,\"Mode\":\"Both\"},{\"Id\":\"old-two\",\"Name\":\"รายการเดิมสอง\",\"Host\":\"127.0.0.1\",\"Port\":80,\"Mode\":\"TCP\"}]}";
        File.WriteAllText(path, legacy, new UTF8Encoding(false));
        var store = new ConfigurationStore(path);
        AppConfiguration migrated = store.Load();
        Assert(migrated.Version == 2 && migrated.Servers.Count == 2 && migrated.Servers[0].Id == "old-one" && migrated.Servers[0].Name == "รายการเดิมหนึ่ง" && migrated.Servers[1].Id == "old-two" && migrated.Servers.All(server => server.Host == "127.0.0.1"), "Legacy settings retain all IDs, Thai names and duplicate-host entries");
        Assert(File.ReadAllText(path) == legacy, "Loading legacy settings does not rewrite the original file");
        store.Save(migrated);
        string saved = File.ReadAllText(path);
        Assert(store.Load().Servers.Count == 2 && !saved.Contains("\"Port\"") && !saved.Contains("\"Mode\"") && File.ReadAllText(path + ".bak") == legacy, "Saving migration removes obsolete fields and backs up the original configuration");
        string future = legacy.Replace("\"Version\":1", "\"Version\":99");
        File.WriteAllText(path, future);
        bool rejected = false;
        try { store.Load(); } catch (ArgumentException) { rejected = true; }
        Assert(rejected && File.ReadAllText(path) == future, "Unsupported future schema remains untouched");
    }

    private static void VerifyDeadline()
    {
        var pending = new TaskCompletionSource<int>();
        var elapsed = Stopwatch.StartNew();
        bool timedOut = false;
        try { NetworkProbe.WithinAsync(pending.Task, 150, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (TimeoutException) { timedOut = true; }
        Assert(timedOut && elapsed.ElapsedMilliseconds < 1500, "A stalled operation has a bounded deadline");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.CancelAfter(100);
            bool cancelled = false;
            try { NetworkProbe.WithinAsync(pending.Task, 5000, cancellation.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert(cancelled, "Cancellation interrupts a pending operation");
        }
        pending.SetException(new InvalidOperationException("Late DNS failure fixture"));
    }

    private static void VerifyNetworkAndGui()
    {
        var probe = new NetworkProbe();
        ServerResult local = probe.CheckAsync(new ServerEntry { Name = "Loopback", Host = "127.0.0.1" }, 1000, CancellationToken.None).GetAwaiter().GetResult();
        Assert(local.Ping.Success && local.Ping.Milliseconds.HasValue && local.Summary == "ผ่าน", "Actual loopback ping passes and reports latency without a service port");
        ServerResult hostname = probe.CheckAsync(new ServerEntry { Name = "Hostname", Host = "localhost" }, 1000, CancellationToken.None).GetAwaiter().GetResult();
        Assert(hostname.Ping.Success && hostname.Address == "127.0.0.1", "Hostname resolution followed by ping succeeds");
        ServerResult unknown = probe.CheckAsync(new ServerEntry { Name = "Unknown host", Host = "no-such-host.invalid" }, 1000, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!unknown.Ping.Success && unknown.Summary == "ไม่ผ่าน", "Unresolvable host reports fail with no partial-pass state");
        var configuration = new AppConfiguration { TimeoutMs = 1000 };
        configuration.Servers.Add(new ServerEntry { Name = "ทดสอบ • เชื่อมต่อได้", Host = "127.0.0.1" });
        configuration.Servers.Add(new ServerEntry { Name = "ทดสอบ • ชื่อโฮสต์", Host = "localhost" });
        configuration.Servers.Add(new ServerEntry { Name = "ทดสอบ • ชื่อโฮสต์ผิด", Host = "no-such-host.invalid" });
        var store = new ConfigurationStore(Path.Combine(root, "gui-fixture", "servers.json"));
        store.Save(configuration);
        using (var form = new MainForm(store))
        {
            Exception guiFailure = null;
            form.Shown += delegate
            {
                try
                {
                    var responsiveness = Stopwatch.StartNew();
                    Task checks = form.RunChecksAsync(false);
                    Assert(responsiveness.ElapsedMilliseconds < 500, "GUI check starts without blocking the UI");
                    WaitForUi(checks);
                    var grid = (DataGridView)form.Controls.Find("ServerGrid", true)[0];
                    Assert(grid.Rows.Count == 3 && (string)grid.Rows[0].Cells["Status"].Value == "ผ่าน" && (string)grid.Rows[1].Cells["Status"].Value == "ผ่าน" && (string)grid.Rows[2].Cells["Status"].Value == "ไม่ผ่าน", "Built dashboard renders green pass and red fail for ping results");
                    Assert(grid.Columns.Count == 5 && !grid.Columns.Contains("Port") && !grid.Columns.Contains("Mode") && !grid.Columns.Contains("Tcp") && !AllControls(form).OfType<Button>().Any(button => button.Text.Contains("CSV")), "Dashboard has no port, check-method, TCP or export controls");
                    Assert(form.Text.Contains("ตรวจสอบเซิร์ฟเวอร์"), "Thai desktop window title");
                    using (var image = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                        image.Save(Path.Combine(root, "gui.png"));
                    }
                    grid.ClearSelection();
                    grid.Rows[0].Selected = true;
                    string unchanged = Convert.ToString(grid.Rows[1].Cells["CheckedAt"].Value);
                    string previouslySelected = Convert.ToString(grid.Rows[0].Cells["CheckedAt"].Value);
                    WaitForUi(Task.Delay(1100));
                    WaitForUi(form.RunChecksAsync(true));
                    Assert(Convert.ToString(grid.Rows[1].Cells["CheckedAt"].Value) == unchanged && Convert.ToString(grid.Rows[0].Cells["CheckedAt"].Value) != previouslySelected, "Selected-only checks update the selected row and leave unselected rows unchanged");
                    VerifyServerDialogs(form, store);
                    var auto = form.Controls.OfType<Control>().SelectMany(AllControls).OfType<CheckBox>().First(item => item.Text == "ตรวจอัตโนมัติ");
                    var settings = form.Controls.OfType<Control>().SelectMany(AllControls).OfType<NumericUpDown>().ToArray();
                    settings.First(item => item.Minimum == 5).Value = 5;
                    auto.Checked = true;
                    WaitForUiIdle(form);
                    Assert(store.Load().IntervalSeconds == 5, "GUI settings are saved outside the executable folder");
                    string beforeAuto = Convert.ToString(grid.Rows[0].Cells["CheckedAt"].Value);
                    var autoDeadline = Stopwatch.StartNew();
                    while (Convert.ToString(grid.Rows[0].Cells["CheckedAt"].Value) == beforeAuto && autoDeadline.ElapsedMilliseconds < 15000)
                    {
                        Application.DoEvents();
                        Thread.Sleep(10);
                    }
                    if (Convert.ToString(grid.Rows[0].Cells["CheckedAt"].Value) == beforeAuto)
                    {
                        var scheduler = (System.Windows.Forms.Timer)typeof(MainForm).GetField("timer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(form);
                        Console.WriteLine("Auto diagnostic: checked=" + auto.Checked + ", timer=" + scheduler.Enabled + ", interval=" + scheduler.Interval + ", check button=" + FindButton(form, "ตรวจทั้งหมด").Enabled + ", before=" + beforeAuto + ", now=" + grid.Rows[0].Cells["CheckedAt"].Value);
                        using (var image = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                            image.Save(Path.Combine(root, "auto-failure.png"));
                        }
                    }
                    Assert(Convert.ToString(grid.Rows[0].Cells["CheckedAt"].Value) != beforeAuto, "Automatic timer performs another real GUI check cycle");
                    auto.Checked = false;
                    WaitForUiIdle(form);
                    Task cancelledCycle = form.RunChecksAsync(false);
                    FindButton(form, "หยุด").PerformClick();
                    WaitForUi(cancelledCycle);
                    Assert(FindButton(form, "ตรวจทั้งหมด").Enabled && !auto.Checked, "GUI Stop cancels the cycle and restores controls");
                    Task closingCycle = form.RunChecksAsync(false);
                    form.Close();
                    WaitForUi(closingCycle);
                    Assert(form.IsDisposed, "Closing during a check cancels safely without accessing disposed controls");
                }
                catch (Exception exception)
                {
                    guiFailure = exception;
                    form.Close();
                }
            };
            Application.Run(form);
            if (guiFailure != null)
                throw guiFailure;
        }
    }

    private static IEnumerable<Control> AllControls(Control control)
    {
        yield return control;
        foreach (Control child in control.Controls)
            foreach (Control descendant in AllControls(child))
                yield return descendant;
    }

    private static Button FindButton(Form form, string text)
    {
        return AllControls(form).OfType<Button>().First(button => button.Text == text);
    }

    private static void VerifyServerDialogs(MainForm form, ConfigurationStore store)
    {
        Exception failure = null;
        using (var interaction = new System.Windows.Forms.Timer { Interval = 150 })
        {
            interaction.Tick += delegate
            {
                interaction.Stop();
                Form dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(item => item.Text == "รายละเอียดเซิร์ฟเวอร์");
                try
                {
                    if (dialog == null) throw new InvalidOperationException("Add dialog did not open");
                    ((TextBox)dialog.Controls.Find("ServerNameInput", true)[0]).Text = "เพิ่มจากหน้าจอ";
                    ((TextBox)dialog.Controls.Find("HostInput", true)[0]).Text = "127.0.0.1";
                    Assert(!AllControls(dialog).OfType<NumericUpDown>().Any() && !AllControls(dialog).OfType<ComboBox>().Any(), "Single-server dialog asks only for name and host");
                    using (var image = new Bitmap(dialog.Width, dialog.Height))
                    {
                        dialog.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                        image.Save(Path.Combine(root, "server-dialog.png"));
                    }
                    FindButton(dialog, "บันทึก").PerformClick();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    if (dialog != null) dialog.Close();
                }
            };
            interaction.Start();
            FindButton(form, "เพิ่มเซิร์ฟเวอร์").PerformClick();
            if (failure != null) throw failure;
            Assert(store.Load().Servers.Any(server => server.Name == "เพิ่มจากหน้าจอ" && server.Host == "127.0.0.1"), "Add-server dialog saves real Thai input");
        }
        var grid = (DataGridView)form.Controls.Find("ServerGrid", true)[0];
        grid.ClearSelection();
        grid.Rows[3].Selected = true;
        using (var interaction = new System.Windows.Forms.Timer { Interval = 150 })
        {
            interaction.Tick += delegate
            {
                interaction.Stop();
                Form dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(item => item.Text == "รายละเอียดเซิร์ฟเวอร์");
                try
                {
                    if (dialog == null) throw new InvalidOperationException("Edit dialog did not open");
                    ((TextBox)dialog.Controls.Find("ServerNameInput", true)[0]).Text = "แก้ไขจากหน้าจอ";
                    FindButton(dialog, "บันทึก").PerformClick();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    if (dialog != null) dialog.Close();
                }
            };
            interaction.Start();
            FindButton(form, "แก้ไข").PerformClick();
            if (failure != null) throw failure;
            Assert(store.Load().Servers.Any(server => server.Name == "แก้ไขจากหน้าจอ" && server.Host == "127.0.0.1"), "Edit-server dialog preserves and updates the selected server");
        }
        using (var interaction = new System.Windows.Forms.Timer { Interval = 150 })
        {
            interaction.Tick += delegate
            {
                interaction.Stop();
                Form dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(item => item.Text == "เพิ่มหลายเซิร์ฟเวอร์");
                try
                {
                    if (dialog == null) throw new InvalidOperationException("Bulk dialog did not open");
                    ((TextBox)dialog.Controls.Find("BulkHostsInput", true)[0]).Text = "127.0.0.1\r\n127.0.0.1\r\nlocalhost\r\n127.0.0.2";
                    Assert(!AllControls(dialog).OfType<NumericUpDown>().Any(), "Bulk entry has no port field");
                    FindButton(dialog, "เพิ่มรายการ").PerformClick();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    if (dialog != null) dialog.Close();
                }
            };
            interaction.Start();
            FindButton(form, "วางหลายรายการ").PerformClick();
            if (failure != null) throw failure;
            Assert(store.Load().Servers.Count == 5, "Bulk entry skips duplicate lines and existing hosts");
        }
        var search = (TextBox)form.Controls.Find("SearchBox", true)[0];
        search.Text = "แก้ไขจากหน้าจอ";
        Assert(grid.Rows.Count == 1, "GUI search filters Thai server names");
        search.Clear();
        Assert(grid.Rows.Count == 5, "Clearing GUI search restores all server rows");
    }

    private static void WaitForUiIdle(MainForm form)
    {
        var elapsed = Stopwatch.StartNew();
        while (!FindButton(form, "ตรวจทั้งหมด").Enabled && elapsed.ElapsedMilliseconds < 15000)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        if (!FindButton(form, "ตรวจทั้งหมด").Enabled)
            throw new TimeoutException("GUI remained busy");
    }

    private static void WaitForUi(Task operation)
    {
        var elapsed = Stopwatch.StartNew();
        while (!operation.IsCompleted && elapsed.ElapsedMilliseconds < 15000)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        if (!operation.IsCompleted) throw new TimeoutException("GUI cycle did not finish");
        operation.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private static void VerifyLaunch()
    {
        string executable = Path.Combine(root, "ServerBotTracker.exe");
        using (var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = root }))
        {
            bool idle = process.WaitForInputIdle(10000);
            process.Refresh();
            var elapsed = Stopwatch.StartNew();
            while (process.MainWindowHandle == IntPtr.Zero && !process.HasExited && elapsed.ElapsedMilliseconds < 5000)
            {
                Thread.Sleep(100);
                process.Refresh();
            }
            bool title = process.MainWindowTitle.Contains("Server Bot Tracker");
            if (!process.HasExited) process.CloseMainWindow();
            bool closed = process.WaitForExit(5000);
            if (!closed) process.Kill();
            Assert(idle && title && closed && process.ExitCode == 0, "Distributable EXE launches its real entry point and closes normally");
        }
    }
}
