using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerBotTracker
{
    public sealed partial class MainForm
    {
        private NotificationCenter notifications;
        private readonly ServerAlertTracker alertTracker = new ServerAlertTracker();
        private readonly Button notificationButton = Theme.Button("การแจ้งเตือน", false);
        private readonly Button updateButton = Theme.Button("อัปเดต / เกี่ยวกับ", false);
        private readonly System.Windows.Forms.Timer releaseTimer = new System.Windows.Forms.Timer { Interval = 10 * 60 * 1000 };
        private readonly CancellationTokenSource releaseCancellation = new CancellationTokenSource();
        private ReleaseChannel releaseChannel;
        private PreparedUpdate preparedUpdate;
        private bool checkingRelease;
        private bool restartAfterUpdate;
        private string releaseStatus = "ยังไม่ได้ตรวจอัปเดต";
        private NotifyIcon tray;
        private Icon trayIcon;

        private void InitializeNotifications(bool onlineServices)
        {
            string directory = Path.GetDirectoryName(store.FilePath);
            notifications = new NotificationCenter(Path.Combine(directory, "notifications.json"));
            notifications.Changed += delegate { RefreshNotificationCount(); };
            notificationButton.Click += ShowNotifications;
            updateButton.Click += ShowUpdateDetails;
            RefreshNotificationCount();
            releaseChannel = new ReleaseChannel();
            trayIcon = Branding.CreateIcon();
            tray = new NotifyIcon { Icon = trayIcon, Text = "Server Bot Tracker", Visible = onlineServices };
            releaseTimer.Tick += async delegate { await CheckReleaseAsync(false); };
            if (onlineServices)
            {
                Shown += async delegate
                {
                    await CheckReleaseAsync(false);
                    if (!closing) releaseTimer.Start();
                };
            }
            FormClosing += ScheduleUpdateOnExit;
            FormClosed += delegate
            {
                releaseCancellation.Cancel();
                releaseTimer.Dispose();
                tray.Dispose();
                trayIcon.Dispose();
                releaseChannel.Dispose();
                releaseCancellation.Dispose();
            };
        }

        private void BuildNotificationButtons(Panel heading)
        {
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 315, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            actions.Controls.Add(updateButton);
            actions.Controls.Add(notificationButton);
            heading.Controls.Add(actions);
        }

        private void RefreshNotificationCount()
        {
            notificationButton.Text = "การแจ้งเตือน (" + notifications.State.Items.Count(item => !item.Read) + ")";
        }

        public void ResumeMonitoring()
        {
            automatic.Checked = true;
        }

        private void AddNotification(string id, string title, string message, string kind, bool remote)
        {
            try
            {
                if (notifications.Add(id, title, message, kind, remote) && tray.Visible)
                    tray.ShowBalloonTip(5000, title, message.Length > 200 ? message.Substring(0, 200) : message, kind == "down" ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                releaseStatus = "บันทึกการแจ้งเตือนไม่สำเร็จ: " + exception.Message;
                RefreshNotificationCount();
            }
        }

        private void PublishServerAlert(ServerEntry server, ServerResult result)
        {
            string transition = alertTracker.Observe(server.Id, result.Ping != null && result.Ping.Success);
            if (transition == null || !notifications.State.ServerAlerts)
                return;
            string title = transition == "down" ? "เซิร์ฟเวอร์ไม่ตอบกลับ" : "เซิร์ฟเวอร์กลับมาเชื่อมต่อได้";
            string detail = server.Name + " (" + server.Host + ") • " + (transition == "down" ? "Ping ไม่ผ่านสองรอบติดต่อกัน" : "ได้รับคำตอบ Ping แล้ว");
            AddNotification(Guid.NewGuid().ToString("N"), title, detail, transition, false);
        }

        public async Task CheckReleaseAsync(bool manual)
        {
            if (checkingRelease || closing) return;
            checkingRelease = true;
            updateButton.Enabled = false;
            releaseStatus = "กำลังตรวจอัปเดตและประกาศ...";
            try
            {
                try
                {
                    if (manual || notifications.State.AutomaticUpdates)
                    {
                        ReleaseManifest manifest = await releaseChannel.GetLatestAsync(releaseCancellation.Token);
                        if (closing) return;
                        if (ReleaseChannel.ParseVersion(manifest.Version) > ReleaseChannel.CurrentVersion)
                        {
                            if (preparedUpdate == null || preparedUpdate.Manifest.Version != manifest.Version)
                                preparedUpdate = await releaseChannel.PrepareAsync(manifest, Path.Combine(Path.GetDirectoryName(store.FilePath), "updates"), releaseCancellation.Token);
                            if (closing) return;
                            releaseStatus = "พร้อมอัปเดตเป็นรุ่น " + manifest.Version + " เมื่อปิดโปรแกรม";
                            updateButton.Text = "พร้อมอัปเดต " + manifest.Version;
                            AddNotification("update:" + manifest.Version, "มีอัปเดตพร้อมติดตั้ง", "รุ่น " + manifest.Version + " • ติดตั้งเมื่อปิดโปรแกรม หรือเลือกเริ่มใหม่จากหน้าต่างอัปเดต\n" + manifest.Notes, "update", true);
                        }
                        else
                        {
                            releaseStatus = preparedUpdate == null ? "เป็นรุ่นล่าสุดแล้ว" : "มีชุดอัปเดตพร้อมติดตั้งเมื่อปิดโปรแกรม";
                        }
                    }
                    else
                    {
                        releaseStatus = "ปิดการตรวจอัปเดตอัตโนมัติ • ตรวจเองได้จากหน้านี้";
                    }
                }
                catch (OperationCanceledException) { if (closing) return; releaseStatus = "ตรวจอัปเดตหมดเวลา"; }
                catch (Exception) { releaseStatus = "ติดต่อช่องทางอัปเดตไม่ได้ หรือยังไม่มีรุ่นเผยแพร่"; }
                try
                {
                    AnnouncementFeed feed = await releaseChannel.GetAnnouncementsAsync(releaseCancellation.Token);
                    if (closing) return;
                    foreach (Announcement item in feed.Items.Where(item => item.IsActive(DateTime.UtcNow)))
                        AddNotification("announcement:" + item.Id, item.Title, item.Message, "announcement", true);
                }
                catch (OperationCanceledException) { }
                catch (Exception) { releaseStatus += " • ประกาศออนไลน์ยังไม่พร้อมใช้งาน"; }
            }
            finally
            {
                checkingRelease = false;
                if (!closing) updateButton.Enabled = true;
            }
        }

        private void ShowNotifications(object sender, EventArgs args)
        {
            using (var dialog = new Form())
            {
                Theme.ApplyDialog(dialog, "การแจ้งเตือน", new Size(720, 470));
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 3 };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                var enabled = new CheckBox { Text = "แจ้งเตือนเซิร์ฟเวอร์เมื่อ Ping ไม่ผ่านสองรอบ และเมื่อกลับมาเชื่อมต่อได้", AutoSize = true, Checked = notifications.State.ServerAlerts };
                var text = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
                text.Text = (notifications.StorageWarning == null ? "" : notifications.StorageWarning + "\r\n\r\n") + (notifications.State.Items.Count == 0 ? "ยังไม่มีการแจ้งเตือน" : String.Join("\r\n\r\n", notifications.State.Items.Select(item => (item.Read ? "" : "[ใหม่] ") + item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "  " + item.Title + "\r\n" + item.Message)));
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                Button close = Theme.Button("ปิด", true);
                close.DialogResult = DialogResult.OK;
                Button read = Theme.Button("อ่านทั้งหมดแล้ว", false);
                read.Click += delegate { try { notifications.MarkAllRead(); read.Enabled = false; } catch (Exception exception) { MessageBox.Show(dialog, exception.Message, "บันทึกไม่ได้"); } };
                actions.Controls.Add(close);
                actions.Controls.Add(read);
                layout.Controls.Add(enabled, 0, 0);
                layout.Controls.Add(text, 0, 1);
                layout.Controls.Add(actions, 0, 2);
                dialog.Controls.Add(layout);
                dialog.ShowDialog(this);
                if (notifications.State.ServerAlerts != enabled.Checked)
                    foreach (ServerEntry server in configuration.Servers) alertTracker.Forget(server.Id);
                notifications.State.ServerAlerts = enabled.Checked;
                try { notifications.Save(); } catch (Exception exception) { MessageBox.Show(this, exception.Message, "บันทึกไม่ได้"); }
            }
        }

        private void ShowUpdateDetails(object sender, EventArgs args)
        {
            using (var dialog = new Form())
            {
                Theme.ApplyDialog(dialog, "อัปเดตและเกี่ยวกับโปรแกรม", new Size(680, 410));
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 4 };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.Controls.Add(Theme.Label("Server Bot Tracker  " + ReleaseChannel.CurrentVersion + "\nเผยแพร่ผ่าน GitHub • " + ReleaseChannel.OfficialRepository), 0, 0);
                var automaticUpdates = new CheckBox { Text = "ดาวน์โหลดอัปเดตอัตโนมัติ และติดตั้งเมื่อปิดโปรแกรม", AutoSize = true, Checked = notifications.State.AutomaticUpdates };
                layout.Controls.Add(automaticUpdates, 0, 1);
                var detail = new TextBox { ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Text = releaseStatus + (preparedUpdate == null ? "" : "\r\n\r\n" + preparedUpdate.Manifest.Notes) };
                layout.Controls.Add(detail, 0, 2);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                Button close = Theme.Button("ปิด", false);
                close.DialogResult = DialogResult.OK;
                Button check = Theme.Button("ตรวจอัปเดต", false);
                Button restart = Theme.Button("อัปเดตและเริ่มใหม่", true);
                restart.Enabled = preparedUpdate != null;
                check.Click += async delegate
                {
                    check.Enabled = false;
                    await CheckReleaseAsync(true);
                    if (!dialog.IsDisposed)
                    {
                        detail.Text = releaseStatus + (preparedUpdate == null ? "" : "\r\n\r\n" + preparedUpdate.Manifest.Notes);
                        check.Enabled = true;
                        restart.Enabled = preparedUpdate != null;
                    }
                };
                restart.Click += delegate { restartAfterUpdate = true; dialog.DialogResult = DialogResult.Retry; };
                actions.Controls.Add(close);
                actions.Controls.Add(restart);
                actions.Controls.Add(check);
                layout.Controls.Add(actions, 0, 3);
                dialog.Controls.Add(layout);
                DialogResult result = dialog.ShowDialog(this);
                notifications.State.AutomaticUpdates = automaticUpdates.Checked;
                try { notifications.Save(); } catch (Exception exception) { MessageBox.Show(this, exception.Message, "บันทึกไม่ได้"); }
                if (result == DialogResult.Retry) Close();
            }
        }

        private void ScheduleUpdateOnExit(object sender, FormClosingEventArgs args)
        {
            releaseCancellation.Cancel();
            releaseTimer.Stop();
            if (args.CloseReason == CloseReason.WindowsShutDown || args.CloseReason == CloseReason.TaskManagerClosing) return;
            if (preparedUpdate == null || (!notifications.State.AutomaticUpdates && !restartAfterUpdate)) return;
            try { UpdateInstaller.LaunchWorker(preparedUpdate, AppDomain.CurrentDomain.BaseDirectory, restartAfterUpdate, automatic.Checked); }
            catch (Exception exception) { MessageBox.Show(this, "ยังติดตั้งอัปเดตไม่ได้: " + exception.Message, "อัปเดตโปรแกรม", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
