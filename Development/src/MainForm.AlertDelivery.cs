using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerBotTracker
{
    public sealed partial class MainForm
    {
        private readonly ServerAlertTracker databaseAlertTracker = new ServerAlertTracker();
        private readonly AlertDeliveryService alertDelivery = new AlertDeliveryService();
        private readonly CancellationTokenSource deliveryCancellation = new CancellationTokenSource();
        private readonly Queue<HealthAlert> deliveryQueue = new Queue<HealthAlert>();
        private AlertSettingsStore alertSettings;
        private bool deliveryRunning;
        private bool externalDeliveryEnabled;

        private void InitializeAlertDelivery(bool onlineServices)
        {
            externalDeliveryEnabled = onlineServices;
            alertSettings = new AlertSettingsStore(Path.Combine(Path.GetDirectoryName(store.FilePath), "alert-settings.json"));
            if (alertSettings.Warning != null) AddNotification(Guid.NewGuid().ToString("N"), "การตั้งค่าแจ้งเตือนยังไม่พร้อม", alertSettings.Warning, "delivery", false);
            FormClosed += delegate { deliveryCancellation.Cancel(); alertDelivery.Dispose(); deliveryCancellation.Dispose(); };
        }

        private void ShowAlertSettings(object sender, EventArgs args)
        {
            using (var dialog = new AlertSettingsDialog(alertSettings.Settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { alertSettings.Save(dialog.Settings); progress.Text = "บันทึกการตั้งค่าแจ้งเตือนแล้ว"; }
                catch (Exception error) { MessageBox.Show(this, error.Message, "บันทึกการแจ้งเตือนไม่ได้", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void PublishHealthTransition(ServerEntry server, ProbeResult check, bool database)
        {
            if (check == null) return;
            string transition = (database ? databaseAlertTracker : alertTracker).Observe(server.Id, check.Success);
            if (transition == null || !notifications.State.ServerAlerts) return;
            bool recovered = transition == "recovered";
            string title = database ? (recovered ? "ฐานข้อมูลกลับมาใช้งานได้" : "เชื่อมต่อฐานข้อมูลไม่ได้")
                : (recovered ? "เซิร์ฟเวอร์กลับมาเชื่อมต่อได้" : "เซิร์ฟเวอร์ไม่ตอบกลับ");
            string detail = server.Name + " (" + server.Host + ")";
            if (database) detail += " • ฐานข้อมูล " + server.Database.DatabaseName;
            detail += "\n" + (database ? "SQL Server: " : "Ping: ") + check.Message;
            detail += recovered ? "\nตรวจกลับมาใช้งานได้แล้ว" : "\nตรวจไม่ผ่าน 2 รอบติดต่อกัน";
            detail += "\nเวลา " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            AddNotification(Guid.NewGuid().ToString("N"), title, detail, database ? "database-" + transition : transition, false);
            QueueExternalAlert(new HealthAlert { Title = title, Message = detail, Recovery = recovered });
        }

        private async void QueueExternalAlert(HealthAlert alert)
        {
            if (!externalDeliveryEnabled || closing || (!alertSettings.Settings.Outlook.Enabled && !alertSettings.Settings.Ntfy.Enabled)) return;
            if (deliveryQueue.Count >= 100)
            {
                AddNotification(Guid.NewGuid().ToString("N"), "คิวแจ้งเตือนภายนอกเต็ม", "เก็บเหตุการณ์ไว้ในกล่องแจ้งเตือน แต่ยังไม่ส่งข้อความนี้ออกภายนอก", "delivery", false);
                return;
            }
            deliveryQueue.Enqueue(alert);
            if (deliveryRunning) return;
            deliveryRunning = true;
            CancellationToken ct = deliveryCancellation.Token;
            try
            {
                while (deliveryQueue.Count > 0 && !closing)
                {
                    HealthAlert next = deliveryQueue.Dequeue();
                    AlertSettings sending = alertSettings.Settings.Copy();
                    ChannelDelivery[] reports = await alertDelivery.SendAsync(sending, next, ct);
                    if (closing) return;
                    foreach (ChannelDelivery report in reports)
                    {
                        if (report.Success && report.ProtectedRefreshToken != null
                            && alertSettings.Settings.Outlook.ClientId == sending.Outlook.ClientId
                            && alertSettings.Settings.Outlook.ProtectedRefreshToken == sending.Outlook.ProtectedRefreshToken)
                        {
                            AlertSettings updated = alertSettings.Settings.Copy();
                            updated.Outlook.ProtectedRefreshToken = report.ProtectedRefreshToken;
                            try { alertSettings.Save(updated); }
                            catch (Exception) { AddNotification(Guid.NewGuid().ToString("N"), "บันทึกบัญชีอีเมลไม่ได้", "ข้อความถูกส่งให้ Microsoft แล้ว แต่บันทึกข้อมูลลงชื่อเข้าใช้ที่ปรับใหม่ไม่ได้ กรุณาตรวจการตั้งค่า", "delivery", false); }
                        }
                        if (!report.Success) AddNotification(Guid.NewGuid().ToString("N"), "ส่งแจ้งเตือน " + report.Channel + " ไม่สำเร็จ", next.Title + "\n" + report.Message, "delivery", false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!closing) AddNotification(Guid.NewGuid().ToString("N"), "ส่งแจ้งเตือนภายนอกไม่ได้", "เหตุการณ์ยังอยู่ในกล่องแจ้งเตือน กรุณาตรวจการตั้งค่าเครือข่ายและบัญชี", "delivery", false); }
            finally { deliveryRunning = false; }
        }
    }
}
