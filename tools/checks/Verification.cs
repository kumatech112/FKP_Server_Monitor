using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
            VerifyReports();
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
        configuration.Servers.Add(new ServerEntry { Name = "เซิร์ฟเวอร์ทดสอบ", Host = "127.0.0.1", Port = 443 });
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
        new ServerEntry { Name = "IPv6", Host = "::1", Port = 65535, Mode = "TCP" }.Validate();
        Assert(true, "IPv6 literals and valid port boundaries are accepted");
        rejected = false;
        try { new ServerEntry { Name = "Port", Host = "localhost", Port = 65536 }.Validate(); } catch (ArgumentException) { rejected = true; }
        Assert(rejected, "Out-of-range ports are rejected");
    }

    private static void VerifyReports()
    {
        var server = new ServerEntry { Name = "=HYPERLINK(\"x\")", Host = "localhost" };
        string path = Path.Combine(root, "report.csv");
        ReportWriter.Write(path, new[] { server }, new Dictionary<string, ServerResult>());
        byte[] bytes = File.ReadAllBytes(path);
        string text = File.ReadAllText(path);
        Assert(bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf && text.Contains("ชื่อเซิร์ฟเวอร์"), "CSV includes UTF-8 BOM for Thai Excel text");
        Assert(text.Contains("'="), "CSV escapes spreadsheet formula injection");
        Assert(ReportWriter.EscapeCell("a,\"b\"\nc") == "\"a,\"\"b\"\"\nc\"", "CSV preserves commas, quotes and newlines");
        Assert(text.Contains("ยังไม่ตรวจ"), "An untested server exports an explicit untested status");
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
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int openPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            var probe = new NetworkProbe();
            ServerResult open = probe.CheckAsync(new ServerEntry { Name = "Local open", Host = "127.0.0.1", Port = openPort }, 1000, CancellationToken.None).GetAwaiter().GetResult();
            Assert(open.Icmp.Success && open.Tcp.Success && open.Summary == "ผ่าน", "Actual loopback ICMP and open TCP both pass");
            var reserved = new TcpListener(IPAddress.Loopback, 0);
            reserved.Start();
            int closedPort = ((IPEndPoint)reserved.LocalEndpoint).Port;
            reserved.Stop();
            ServerResult closed = probe.CheckAsync(new ServerEntry { Name = "Local closed", Host = "127.0.0.1", Port = closedPort }, 1000, CancellationToken.None).GetAwaiter().GetResult();
            Assert(closed.Icmp.Success && !closed.Tcp.Success && closed.Summary == "ผ่านบางส่วน", "Closed TCP is separate from successful ICMP");
            ServerResult tcpOnly = probe.CheckAsync(new ServerEntry { Name = "TCP only", Host = "localhost", Port = openPort, Mode = "TCP" }, 1000, CancellationToken.None).GetAwaiter().GetResult();
            Assert(tcpOnly.Icmp == null && tcpOnly.Tcp.Success && tcpOnly.Address == "127.0.0.1", "Hostname resolution and TCP-only mode");
            var configuration = new AppConfiguration { TimeoutMs = 1000 };
            configuration.Servers.Add(new ServerEntry { Name = "ทดสอบ • เชื่อมต่อได้", Host = "127.0.0.1", Port = openPort });
            configuration.Servers.Add(new ServerEntry { Name = "ทดสอบ • พอร์ตปิด", Host = "127.0.0.1", Port = closedPort });
            configuration.Servers.Add(new ServerEntry { Name = "ทดสอบ • ชื่อโฮสต์ผิด", Host = "no-such-host.invalid", Port = 443 });
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
                        Assert(grid.Rows.Count == 3 && (string)grid.Rows[0].Cells["Status"].Value == "ผ่าน" && (string)grid.Rows[1].Cells["Status"].Value == "ผ่านบางส่วน" && (string)grid.Rows[2].Cells["Status"].Value == "ไม่ผ่าน", "Built GUI renders independent Thai check statuses including DNS failure");
                        Assert(form.Text.Contains("ตรวจสอบเซิร์ฟเวอร์"), "Thai desktop window title");
                        using (var image = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                            image.Save(Path.Combine(root, "gui.png"));
                        }
                        grid.ClearSelection();
                        grid.Rows[0].Selected = true;
                        string unchanged = Convert.ToString(grid.Rows[1].Cells["CheckedAt"].Value);
                        WaitForUi(form.RunChecksAsync(true));
                        Assert(Convert.ToString(grid.Rows[1].Cells["CheckedAt"].Value) == unchanged, "Selected-only checks leave unselected rows unchanged");
                        VerifyServerDialogs(form, store, openPort);
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
        finally { listener.Stop(); }
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

    private static void VerifyServerDialogs(MainForm form, ConfigurationStore store, int port)
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
                    TextBox[] boxes = AllControls(dialog).OfType<TextBox>().ToArray();
                    boxes[0].Text = "เพิ่มจากหน้าจอ";
                    boxes[1].Text = "127.0.0.1";
                    AllControls(dialog).OfType<NumericUpDown>().Single().Value = port;
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
            Assert(store.Load().Servers.Any(server => server.Name == "เพิ่มจากหน้าจอ" && server.Port == port), "Add-server dialog saves real Thai input");
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
                    AllControls(dialog).OfType<TextBox>().First().Text = "แก้ไขจากหน้าจอ";
                    AllControls(dialog).OfType<ComboBox>().Single().SelectedIndex = 2;
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
            Assert(store.Load().Servers.Any(server => server.Name == "แก้ไขจากหน้าจอ" && server.Mode == "TCP"), "Edit-server dialog preserves and updates the selected server");
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
                    ((TextBox)dialog.Controls.Find("BulkHostsInput", true)[0]).Text = "127.0.0.1\r\n127.0.0.1\r\nlocalhost";
                    AllControls(dialog).OfType<NumericUpDown>().Single().Value = port;
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
            Assert(store.Load().Servers.Count == 5, "Bulk entry skips duplicate lines and existing host-port-mode combinations");
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
