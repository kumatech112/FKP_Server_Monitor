using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal sealed class AlertSettingsDialog : Form
    {
        private readonly AlertSettings original;
        private readonly CheckBox emailEnabled = new CheckBox { Name = "OutlookEnabled", Text = "ส่งอีเมลเมื่อพบปัญหาและเมื่อกลับมาใช้งานได้", AutoSize = true };
        private readonly TextBox clientId = Input("OutlookClientId", 40);
        private readonly TextBox tenant = Input("OutlookTenant", 253);
        private readonly TextBox recipients = Input("OutlookRecipients", 2000);
        private readonly CheckBox ntfyEnabled = new CheckBox { Name = "NtfyEnabled", Text = "ส่งการแจ้งเตือนผ่าน ntfy", AutoSize = true };
        private readonly TextBox ntfyUrl = Input("NtfyServer", 1000);
        private readonly TextBox topic = Input("NtfyTopic", 64);
        private readonly TextBox token = Input("NtfyToken", 4096);
        private readonly TextBox status = new TextBox { Name = "AlertSettingsStatus", ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
        private readonly Label account = Theme.Label("");
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly MicrosoftMail microsoft = new MicrosoftMail();
        private readonly AlertDeliveryService delivery = new AlertDeliveryService();
        private Button signIn;
        private Button testEmail;
        private Button testNtfy;
        private Button save;
        private bool working;
        private string protectedRefreshToken;
        private string userCode;
        public AlertSettings Settings { get; private set; }

        public AlertSettingsDialog(AlertSettings settings)
        {
            original = settings.Copy();
            protectedRefreshToken = original.Outlook.ProtectedRefreshToken;
            Theme.ApplyDialog(this, "ตั้งค่าการแจ้งเตือน", new Size(780, 650));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            BuildOutlook(); BuildNtfy();
            layout.Controls.Add(tabs, 0, 0); layout.Controls.Add(status, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            save = Theme.Button("บันทึก", true); save.Click += SaveSettings;
            Button cancel = Theme.Button("ยกเลิก", false); cancel.DialogResult = DialogResult.Cancel;
            Button copy = Theme.Button("คัดลอกรหัส", false);
            copy.Click += delegate { if (!String.IsNullOrEmpty(userCode)) Clipboard.SetText(userCode); };
            actions.Controls.Add(save); actions.Controls.Add(cancel); actions.Controls.Add(copy);
            layout.Controls.Add(actions, 0, 2); Controls.Add(layout); CancelButton = cancel;
            emailEnabled.Checked = original.Outlook.Enabled; clientId.Text = original.Outlook.ClientId ?? "";
            tenant.Text = original.Outlook.Tenant ?? "common"; recipients.Text = original.Outlook.Recipients ?? "";
            ntfyEnabled.Checked = original.Ntfy.Enabled; ntfyUrl.Text = original.Ntfy.ServerUrl ?? "https://ntfy.sh"; topic.Text = original.Ntfy.Topic ?? "";
            token.UseSystemPasswordChar = true;
            clientId.TextChanged += IdentityChanged; tenant.TextChanged += IdentityChanged;
            ShowAccountState();
            status.Text = "ยังไม่ส่งข้อความใด ๆ • บันทึกเพื่อใช้งาน หรือกดทดสอบเพื่อส่งข้อความทดสอบ\r\nแจ้งเตือนเมื่อไม่ผ่าน 2 รอบติดต่อกัน และแจ้งครั้งเดียวเมื่อกลับมาใช้งานได้";
            FormClosing += delegate { cancellation.Cancel(); };
            FormClosed += delegate { microsoft.Dispose(); delivery.Dispose(); cancellation.Dispose(); };
        }

        private static TextBox Input(string name, int maximum)
        {
            return new TextBox { Name = name, Dock = DockStyle.Fill, MaxLength = maximum };
        }

        private void BuildOutlook()
        {
            var page = new TabPage("Outlook / Microsoft 365") { BackColor = Theme.Background, Padding = new Padding(16), AutoScroll = true };
            var fields = FormFields.Create();
            FormFields.AddWide(fields, emailEnabled);
            FormFields.AddField(fields, "Application (client) ID", clientId);
            FormFields.AddField(fields, "Tenant / องค์กร", tenant);
            FormFields.AddField(fields, "อีเมลผู้รับ", recipients);
            FormFields.AddWide(fields, Theme.Label("ผู้รับหลายคนคั่นด้วย ; • common ใช้ได้ทั้งบัญชีส่วนตัวและองค์กร\nไม่ต้องกรอกรหัสผ่าน Outlook ในโปรแกรม"));
            FormFields.AddWide(fields, account);
            signIn = Theme.Button("ลงชื่อเข้าใช้ Microsoft", true); signIn.Click += SignIn;
            testEmail = Theme.Button("ส่งอีเมลทดสอบ", false); testEmail.Click += async delegate { await TestChannel(true); };
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(signIn); buttons.Controls.Add(testEmail);
            FormFields.AddWide(fields, buttons);
            FormFields.AddWide(fields, Theme.Label("ตั้งค่าภายหลังได้ โดยยังไม่เปิดช่องส่งอีเมล\nผู้ดูแลต้องเตรียมแอป Microsoft แบบ public client และสิทธิ์ Mail.Send\nบัญชีองค์กรอาจต้องได้รับความยินยอมจากผู้ดูแลระบบ"));
            page.Controls.Add(fields); tabs.TabPages.Add(page);
        }

        private void BuildNtfy()
        {
            var page = new TabPage("ntfy") { BackColor = Theme.Background, Padding = new Padding(16), AutoScroll = true };
            var fields = FormFields.Create();
            FormFields.AddWide(fields, ntfyEnabled);
            FormFields.AddField(fields, "เซิร์ฟเวอร์ ntfy", ntfyUrl);
            FormFields.AddField(fields, "Topic", topic);
            FormFields.AddField(fields, "Access token (ถ้ามี)", token);
            FormFields.AddWide(fields, Theme.Label("เว้น token ว่างเพื่อใช้ค่าเดิม • ใช้ HTTPS เมื่อระบุ token\nสมัครรับ topic เดียวกันในแอป ntfy บนโทรศัพท์\nTopic สาธารณะควรคาดเดายาก หรือใช้เซิร์ฟเวอร์ที่กำหนดสิทธิ์ไว้"));
            var clear = new CheckBox { Name = "ClearNtfyToken", Text = "ลบ token เดิมเมื่อบันทึก", AutoSize = true };
            FormFields.AddWide(fields, clear);
            testNtfy = Theme.Button("ส่ง ntfy ทดสอบ", false); testNtfy.Click += async delegate { await TestChannel(false); };
            FormFields.AddWide(fields, testNtfy);
            page.Controls.Add(fields); tabs.TabPages.Add(page);
        }

        private void IdentityChanged(object sender, EventArgs args)
        {
            protectedRefreshToken = ""; ShowAccountState();
        }

        private void ShowAccountState()
        {
            account.Text = String.IsNullOrEmpty(protectedRefreshToken) ? "บัญชี: ยังไม่ได้ลงชื่อเข้าใช้" : "บัญชี: มีข้อมูลลงชื่อเข้าใช้ที่บันทึกไว้";
        }

        private AlertSettings ReadSettings()
        {
            AlertSettings value = original.Copy();
            value.Outlook.Enabled = emailEnabled.Checked; value.Outlook.ClientId = clientId.Text.Trim();
            value.Outlook.Tenant = tenant.Text.Trim(); value.Outlook.Recipients = recipients.Text.Trim();
            value.Outlook.ProtectedRefreshToken = protectedRefreshToken;
            value.Ntfy.Enabled = ntfyEnabled.Checked; value.Ntfy.ServerUrl = ntfyUrl.Text.Trim(); value.Ntfy.Topic = topic.Text.Trim();
            var clear = (CheckBox)Controls.Find("ClearNtfyToken", true).Single();
            if (clear.Checked) value.Ntfy.ProtectedToken = "";
            if (token.Text.Length > 0) value.Ntfy.ProtectedToken = ProtectedSecrets.Protect(token.Text);
            return value;
        }

        private void SetWorking(bool value)
        {
            working = value; tabs.Enabled = !value; save.Enabled = !value;
        }

        private async void SignIn(object sender, EventArgs args)
        {
            if (working) return;
            SetWorking(true);
            CancellationToken ct = cancellation.Token;
            try
            {
                OutlookOptions options = ReadSettings().Outlook;
                options.ValidateIdentity(); status.Text = "กำลังขอรหัสลงชื่อเข้าใช้ Microsoft...";
                DeviceAuthorization code = await microsoft.BeginSignInAsync(options, ct);
                if (IsDisposed || Disposing) return;
                userCode = code.UserCode;
                status.Text = "รหัสลงชื่อเข้าใช้: " + userCode + "\r\nเปิด https://microsoft.com/devicelogin แล้วกรอกรหัสนี้\r\nกด คัดลอกรหัส เพื่อคัดลอก • กำลังรอให้ลงชื่อเข้าใช้เสร็จ...";
                try { Process.Start(new ProcessStartInfo("https://microsoft.com/devicelogin") { UseShellExecute = true }); }
                catch (Exception) { status.AppendText("\r\nเปิดลิงก์ข้างต้นในเบราว์เซอร์ด้วยตนเอง"); }
                string connected = await microsoft.CompleteSignInAsync(options, code, ct);
                if (IsDisposed || Disposing) return;
                protectedRefreshToken = connected; ShowAccountState();
                status.Text = "ลงชื่อเข้าใช้แล้ว • กดบันทึกเพื่อเก็บข้อมูล หรือส่งอีเมลทดสอบ";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed && !Disposing) status.Text = Friendly(error); }
            finally { if (!IsDisposed && !Disposing) SetWorking(false); }
        }

        private async System.Threading.Tasks.Task TestChannel(bool email)
        {
            if (working) return;
            SetWorking(true);
            CancellationToken ct = cancellation.Token;
            try
            {
                AlertSettings value = ReadSettings();
                value.Outlook.Enabled = email; value.Ntfy.Enabled = !email;
                value.Validate(); status.Text = "กำลังส่งข้อความทดสอบ...";
                ChannelDelivery[] results = await delivery.SendAsync(value, new HealthAlert { Title = "ทดสอบการแจ้งเตือน", Message = "ข้อความทดสอบจาก FKP Server Monitor", Recovery = true }, ct);
                if (IsDisposed || Disposing) return;
                foreach (ChannelDelivery result in results)
                {
                    status.Text = result.Channel + ": " + result.Message;
                    if (result.Success && result.ProtectedRefreshToken != null) protectedRefreshToken = result.ProtectedRefreshToken;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed && !Disposing) status.Text = Friendly(error); }
            finally { if (!IsDisposed && !Disposing) SetWorking(false); }
        }

        private void SaveSettings(object sender, EventArgs args)
        {
            try { AlertSettings candidate = ReadSettings(); candidate.Validate(); Settings = candidate; DialogResult = DialogResult.OK; }
            catch (Exception error) { status.Text = Friendly(error); }
        }

        private static string Friendly(Exception error)
        {
            return error is ArgumentException || error is InvalidOperationException || error is TimeoutException ? error.Message : "ติดต่อบริการไม่ได้ กรุณาตรวจเครือข่ายและการตั้งค่า";
        }
    }
}
