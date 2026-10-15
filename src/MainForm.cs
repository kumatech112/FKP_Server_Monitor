using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerBotTracker
{
    public sealed partial class MainForm : Form
    {
        private readonly ConfigurationStore store;
        private AppConfiguration configuration;
        private readonly NetworkProbe probe = new NetworkProbe();
        private readonly Dictionary<string, ServerResult> results = new Dictionary<string, ServerResult>();
        private readonly Dictionary<string, DataGridViewRow> visibleRows = new Dictionary<string, DataGridViewRow>();
        private readonly DataGridView grid = new DataGridView { Name = "ServerGrid" };
        private readonly Label totals = Theme.Label("");
        private readonly Label progress = Theme.Label("พร้อมตรวจสอบ");
        private readonly TextBox search = new TextBox { Name = "SearchBox", Width = 235 };
        private readonly NumericUpDown timeout = new NumericUpDown { Minimum = 500, Maximum = 15000, Increment = 500, Width = 85 };
        private readonly NumericUpDown interval = new NumericUpDown { Minimum = 5, Maximum = 3600, Width = 70 };
        private readonly CheckBox automatic = new CheckBox { Text = "ตรวจอัตโนมัติ", AutoSize = true, Margin = new Padding(8, 7, 12, 0) };
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly List<Button> editButtons = new List<Button>();
        private Button checkAll;
        private Button checkSelected;
        private Button stop;
        private CancellationTokenSource currentCheck;
        private bool busy;
        private bool closing;
        private bool resettingSettings;

        public MainForm(ConfigurationStore configurationStore) : this(configurationStore, true) { }

        public MainForm(ConfigurationStore configurationStore, bool onlineServices)
        {
            store = configurationStore;
            configuration = store.Load();
            Branding.ApplyTo(this);
            Text = "ตรวจสอบเซิร์ฟเวอร์ | Server Bot Tracker";
            Font = new Font("Tahoma", 10F);
            ForeColor = Theme.Ink;
            BackColor = Theme.Background;
            ClientSize = new Size(1120, 700);
            MinimumSize = new Size(980, 600);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildLayout();
            InitializeNotifications(onlineServices);
            RenderRows();
            timer.Tick += async delegate { await RunChecksAsync(false); };
            FormClosing += OnClosing;
            FormClosed += delegate { timer.Dispose(); };
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 16), ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var heading = new Panel { Dock = DockStyle.Fill };
            PictureBox logo = Branding.CreateLogoBox(new Size(62, 64));
            logo.Location = new Point(0, 0);
            heading.Controls.Add(logo);
            heading.Controls.Add(new Label { Text = "ตรวจสอบเซิร์ฟเวอร์", Font = new Font("Tahoma", 22F, FontStyle.Bold), ForeColor = Theme.Ink, AutoSize = true, Location = new Point(76, 0) });
            heading.Controls.Add(new Label { Text = "ตรวจด้วย Ping • ดูผลผ่าน / ไม่ผ่านในหน้าจอเดียว", AutoSize = true, ForeColor = Color.FromArgb(93, 111, 136), Location = new Point(78, 48) });
            BuildNotificationButtons(heading);
            layout.Controls.Add(heading, 0, 0);
            var summary = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 10, 14, 10), Margin = new Padding(0, 0, 0, 10) };
            totals.Dock = DockStyle.Fill;
            totals.Font = new Font("Tahoma", 11F, FontStyle.Bold);
            totals.TextAlign = ContentAlignment.MiddleLeft;
            summary.Controls.Add(totals);
            layout.Controls.Add(summary, 0, 1);
            layout.Controls.Add(BuildToolbar(), 0, 2);
            layout.Controls.Add(BuildSettings(), 0, 3);
            BuildGrid();
            layout.Controls.Add(grid, 0, 4);
            progress.Dock = DockStyle.Fill;
            progress.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(progress, 0, 5);
            var explanation = Theme.Label("ผ่าน = ตอบกลับ Ping • ไม่ผ่าน = ไม่ได้รับคำตอบ • ดูเวลาตรวจล่าสุดประกอบ\nเลือกแถวเพื่อดูรายละเอียด • Ctrl / Shift เลือกหลายรายการ");
            explanation.Dock = DockStyle.Fill;
            explanation.ForeColor = Color.FromArgb(93, 111, 136);
            layout.Controls.Add(explanation, 0, 6);
            Controls.Add(layout);
        }

        private FlowLayoutPanel BuildToolbar()
        {
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            AddEditButton(toolbar, "เพิ่มเซิร์ฟเวอร์", AddServer);
            AddEditButton(toolbar, "วางหลายรายการ", AddMany);
            AddEditButton(toolbar, "แก้ไข", EditServer);
            AddEditButton(toolbar, "ลบ", DeleteServers);
            checkAll = Theme.Button("ตรวจทั้งหมด", true);
            checkSelected = Theme.Button("ตรวจที่เลือก", false);
            stop = Theme.Button("หยุด", false);
            stop.Enabled = false;
            checkAll.Click += async delegate { await RunChecksAsync(false); };
            checkSelected.Click += async delegate { await RunChecksAsync(true); };
            stop.Click += delegate { automatic.Checked = false; if (currentCheck != null) currentCheck.Cancel(); };
            toolbar.Controls.Add(checkAll);
            toolbar.Controls.Add(checkSelected);
            toolbar.Controls.Add(stop);
            return toolbar;
        }

        private void AddEditButton(FlowLayoutPanel toolbar, string text, EventHandler handler)
        {
            Button button = Theme.Button(text, false);
            button.Click += handler;
            editButtons.Add(button);
            toolbar.Controls.Add(button);
        }

        private FlowLayoutPanel BuildSettings()
        {
            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 5, 0, 0) };
            settings.Controls.Add(Theme.Label("ค้นหา"));
            settings.Controls.Add(search);
            settings.Controls.Add(Theme.Label("รอสูงสุด (ms)"));
            timeout.Value = configuration.TimeoutMs;
            settings.Controls.Add(timeout);
            settings.Controls.Add(automatic);
            settings.Controls.Add(Theme.Label("รอบตรวจ (วินาที)"));
            interval.Value = configuration.IntervalSeconds;
            settings.Controls.Add(interval);
            search.TextChanged += delegate { RenderRows(); };
            timeout.ValueChanged += SaveSettings;
            interval.ValueChanged += SaveSettings;
            automatic.CheckedChanged += async delegate
            {
                timer.Stop();
                if (automatic.Checked && !busy)
                    await RunChecksAsync(false);
            };
            return settings;
        }

        private void BuildGrid()
        {
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(231, 238, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Tahoma", 10F, FontStyle.Bold);
            grid.ColumnHeadersHeight = 42;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 40;
            grid.GridColor = Color.FromArgb(232, 237, 245);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
            grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
            AddColumn("ServerName", "ชื่อเซิร์ฟเวอร์", 200);
            AddColumn("Host", "IP / โฮสต์", 220);
            AddColumn("Status", "ผล Ping", 115);
            AddColumn("Latency", "เวลาตอบกลับ (ms)", 160);
            AddColumn("CheckedAt", "ตรวจล่าสุด", 180);
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs args) { if (args.RowIndex >= 0 && !busy) EditServer(sender, EventArgs.Empty); };
            grid.SelectionChanged += delegate { ShowSelectedDetails(); };
        }

        private void AddColumn(string name, string title, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = title, FillWeight = width, MinimumWidth = width, SortMode = DataGridViewColumnSortMode.Automatic });
        }

        private void RenderRows()
        {
            var selection = new HashSet<string>(SelectedIds());
            visibleRows.Clear();
            grid.Rows.Clear();
            string term = search.Text.Trim();
            foreach (ServerEntry server in configuration.Servers)
            {
                if (term.Length > 0 && server.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0 && server.Host.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                int index = grid.Rows.Add(server.Name, server.Host, "ยังไม่ตรวจ", "—", "—");
                DataGridViewRow row = grid.Rows[index];
                row.Tag = server.Id;
                visibleRows[server.Id] = row;
                ServerResult result;
                if (results.TryGetValue(server.Id, out result)) UpdateRow(row, result);
            }
            grid.ClearSelection();
            foreach (DataGridViewRow row in grid.Rows)
                row.Selected = selection.Contains((string)row.Tag);
            UpdateTotals();
        }

        private List<string> SelectedIds()
        {
            return grid.SelectedRows.Cast<DataGridViewRow>().Where(row => row.Tag != null).Select(row => (string)row.Tag).ToList();
        }

        private void UpdateRow(DataGridViewRow row, ServerResult result)
        {
            row.Cells["Latency"].Value = result.Ping != null && result.Ping.Milliseconds.HasValue ? result.Ping.Milliseconds.Value.ToString(CultureInfo.InvariantCulture) : "—";
            row.Cells["Status"].Value = result.Summary;
            row.Cells["CheckedAt"].Value = result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Color color = result.Summary == "ผ่าน" ? Theme.Green : Theme.Red;
            row.Cells["Status"].Style.ForeColor = color;
            row.Cells["Status"].Style.SelectionForeColor = color;
            string detail = "IP ที่ตรวจ: " + result.Address + " | Ping: " + ProbeText(result.Ping);
            foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = detail;
        }

        private static string ProbeText(ProbeResult result)
        {
            if (result == null) return "ยังไม่ตรวจ";
            return result.Message + (result.Milliseconds.HasValue ? " / " + result.Milliseconds.Value + " ms" : "");
        }

        private void UpdateTotals()
        {
            int passed = results.Values.Count(result => result.Summary == "ผ่าน");
            int failed = results.Values.Count(result => result.Summary == "ไม่ผ่าน");
            totals.Text = String.Format("ทั้งหมด {0}     ผ่าน {1}     ไม่ผ่าน {2}     ยังไม่ตรวจ {3}     แสดง {4}", configuration.Servers.Count, passed, failed, configuration.Servers.Count - results.Count, grid.Rows.Count);
        }

        private void ShowSelectedDetails()
        {
            if (busy || grid.SelectedRows.Count != 1) return;
            string id = grid.SelectedRows[0].Tag as string;
            ServerResult result;
            if (id != null && results.TryGetValue(id, out result))
                progress.Text = "IP ที่ตรวจ: " + result.Address + "   |   Ping: " + ProbeText(result.Ping);
        }

        public async Task RunChecksAsync(bool selectedOnly)
        {
            if (busy || closing) return;
            timer.Stop();
            var selected = new HashSet<string>(SelectedIds());
            List<ServerEntry> servers = configuration.Servers.Where(server => !selectedOnly || selected.Contains(server.Id)).Select(server => server.Copy()).ToList();
            if (servers.Count == 0)
            {
                progress.Text = selectedOnly ? "กรุณาเลือกเซิร์ฟเวอร์ที่ต้องการตรวจ" : "เพิ่มเซิร์ฟเวอร์ก่อนเริ่มตรวจสอบ";
                automatic.Checked = false;
                return;
            }
            currentCheck = new CancellationTokenSource();
            CancellationToken cancellation = currentCheck.Token;
            SetBusy(true);
            int completed = 0;
            int timeoutMs = configuration.TimeoutMs;
            progress.Text = "กำลังตรวจสอบ 0 / " + servers.Count;
            using (var concurrency = new SemaphoreSlim(8))
            {
                try
                {
                    var checks = servers.Select(async server =>
                    {
                        await concurrency.WaitAsync(cancellation);
                        try
                        {
                            ServerResult result = await probe.CheckAsync(server, timeoutMs, cancellation);
                            if (closing) return;
                            results[server.Id] = result;
                            PublishServerAlert(server, result);
                            DataGridViewRow row;
                            if (visibleRows.TryGetValue(server.Id, out row)) UpdateRow(row, result);
                            completed++;
                            progress.Text = "กำลังตรวจสอบ " + completed + " / " + servers.Count;
                            UpdateTotals();
                        }
                        finally { concurrency.Release(); }
                    }).ToArray();
                    await Task.WhenAll(checks);
                    if (!closing) progress.Text = "ตรวจเสร็จ " + completed + " รายการ เวลา " + DateTime.Now.ToString("HH:mm:ss");
                }
                catch (OperationCanceledException)
                {
                    if (!closing) progress.Text = "หยุดแล้ว • ตรวจเสร็จ " + completed + " / " + servers.Count + " • รายการที่ยังไม่เสร็จคงผลก่อนหน้า";
                }
                catch (Exception exception)
                {
                    if (!closing) MessageBox.Show(this, "ตรวจสอบไม่สำเร็จ: " + exception.Message, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    currentCheck.Dispose();
                    currentCheck = null;
                    if (!closing)
                    {
                        SetBusy(false);
                        if (automatic.Checked)
                        {
                            timer.Interval = configuration.IntervalSeconds * 1000;
                            timer.Start();
                            progress.Text += " • ตรวจรอบถัดไปใน " + configuration.IntervalSeconds + " วินาที";
                        }
                    }
                }
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            foreach (Button button in editButtons) button.Enabled = !value;
            checkAll.Enabled = !value;
            checkSelected.Enabled = !value;
            stop.Enabled = value;
            timeout.Enabled = !value;
            interval.Enabled = !value;
        }

        private bool CommitConfiguration(AppConfiguration candidate)
        {
            try
            {
                store.Save(candidate);
                configuration = candidate;
                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "บันทึกไม่ได้: " + exception.Message + "\nข้อมูลเดิมยังคงอยู่", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void AddServer(object sender, EventArgs args)
        {
            using (var dialog = new ServerDialog(new ServerEntry()))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                AppConfiguration candidate = configuration.Copy();
                candidate.Servers.Add(dialog.Entry);
                if (CommitConfiguration(candidate)) RenderRows();
            }
        }

        private void AddMany(object sender, EventArgs args)
        {
            using (var dialog = new BulkDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                AppConfiguration candidate = configuration.Copy();
                int skipped = 0;
                foreach (ServerEntry entry in dialog.Entries)
                {
                    bool exists = candidate.Servers.Any(server => String.Equals(server.Host, entry.Host, StringComparison.OrdinalIgnoreCase));
                    if (exists) { skipped++; continue; }
                    candidate.Servers.Add(entry);
                }
                if (CommitConfiguration(candidate))
                {
                    RenderRows();
                    progress.Text = "เพิ่ม " + (dialog.Entries.Count - skipped) + " รายการ • ข้ามรายการซ้ำ " + skipped;
                }
            }
        }

        private void EditServer(object sender, EventArgs args)
        {
            List<string> ids = SelectedIds();
            if (ids.Count != 1) { progress.Text = "เลือกหนึ่งรายการเพื่อแก้ไข"; return; }
            ServerEntry entry = configuration.Servers.First(server => server.Id == ids[0]);
            using (var dialog = new ServerDialog(entry))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                AppConfiguration candidate = configuration.Copy();
                candidate.Servers[candidate.Servers.FindIndex(server => server.Id == entry.Id)] = dialog.Entry;
                if (CommitConfiguration(candidate))
                {
                    results.Remove(entry.Id);
                    alertTracker.Forget(entry.Id);
                    RenderRows();
                }
            }
        }

        private void DeleteServers(object sender, EventArgs args)
        {
            var ids = new HashSet<string>(SelectedIds());
            if (ids.Count == 0) { progress.Text = "เลือกเซิร์ฟเวอร์ที่ต้องการลบ"; return; }
            if (MessageBox.Show(this, "ลบรายการที่เลือก " + ids.Count + " รายการ?", "ยืนยันการลบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            AppConfiguration candidate = configuration.Copy();
            candidate.Servers.RemoveAll(server => ids.Contains(server.Id));
            if (CommitConfiguration(candidate))
            {
                foreach (string id in ids)
                {
                    results.Remove(id);
                    alertTracker.Forget(id);
                }
                RenderRows();
            }
        }

        private void SaveSettings(object sender, EventArgs args)
        {
            if (resettingSettings) return;
            AppConfiguration candidate = configuration.Copy();
            candidate.TimeoutMs = (int)timeout.Value;
            candidate.IntervalSeconds = (int)interval.Value;
            if (!CommitConfiguration(candidate))
            {
                resettingSettings = true;
                timeout.Value = configuration.TimeoutMs;
                interval.Value = configuration.IntervalSeconds;
                resettingSettings = false;
            }
            if (automatic.Checked && !busy)
            {
                timer.Stop();
                timer.Interval = configuration.IntervalSeconds * 1000;
                timer.Start();
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs args)
        {
            closing = true;
            timer.Stop();
            if (currentCheck != null) currentCheck.Cancel();
        }
    }
}
