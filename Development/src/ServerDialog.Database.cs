using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal sealed partial class ServerDialog
    {
        private readonly CheckBox databaseEnabled = new CheckBox { Name = "DatabaseEnabled", Text = "ตรวจ SQL Server เพิ่มจาก Ping", AutoSize = true };
        private readonly TextBox sqlServer = new TextBox { Name = "SqlServerInput", Dock = DockStyle.Fill, MaxLength = 320 };
        private readonly TextBox databaseName = new TextBox { Name = "DatabaseNameInput", Dock = DockStyle.Fill, MaxLength = 128 };
        private readonly TextBox sqlUser = new TextBox { Name = "SqlUserInput", Dock = DockStyle.Fill, MaxLength = 128 };
        private readonly TextBox sqlPassword = new TextBox { Name = "SqlPasswordInput", Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 1024 };
        private readonly CheckBox trustCertificate = new CheckBox { Name = "TrustSqlCertificate", Text = "เชื่อถือใบรับรอง SQL Server ภายในที่องค์กรจัดการเอง", AutoSize = true };
        private readonly Label databaseStatus = Theme.Label("");
        private readonly CancellationTokenSource databaseCancellation = new CancellationTokenSource();
        private Button testDatabase;

        private void BuildServerEditor()
        {
            Theme.ApplyDialog(this, "รายละเอียดเซิร์ฟเวอร์", new Size(670, 520));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var serverPage = new TabPage("เซิร์ฟเวอร์") { BackColor = Theme.Background, Padding = new Padding(16) };
            var databasePage = new TabPage("SQL Server") { AutoScroll = true, BackColor = Theme.Background, Padding = new Padding(16) };
            var serverFields = FormFields.Create();
            FormFields.AddField(serverFields, "ชื่อเซิร์ฟเวอร์", serverName);
            FormFields.AddField(serverFields, "IP / ชื่อโฮสต์", host);
            FormFields.AddWide(serverFields, Theme.Label("ตรวจ Ping ตามปกติ • ตั้งค่าฐานข้อมูลเพิ่มเติมได้ในแท็บ SQL Server"));
            serverPage.Controls.Add(serverFields);
            var fields = FormFields.Create();
            FormFields.AddWide(fields, databaseEnabled);
            FormFields.AddField(fields, "SQL Server / Instance", sqlServer);
            FormFields.AddField(fields, "ชื่อฐานข้อมูล", databaseName);
            FormFields.AddField(fields, "ชื่อผู้ใช้ SQL", sqlUser);
            FormFields.AddField(fields, "รหัสผ่าน SQL", sqlPassword);
            FormFields.AddWide(fields, trustCertificate);
            FormFields.AddWide(fields, Theme.Label("เว้น SQL Server เพื่อใช้ IP/โฮสต์หลัก • ระบุ server\\instance ได้\nรหัสผ่านว่าง = ใช้รหัสเดิม • ตรวจด้วย SELECT 1 เท่านั้น ไม่แก้ไขข้อมูล"));
            testDatabase = Theme.Button("ทดสอบฐานข้อมูล", false);
            testDatabase.Name = "TestDatabaseButton";
            testDatabase.Click += TestDatabase;
            FormFields.AddWide(fields, testDatabase);
            FormFields.AddWide(fields, databaseStatus);
            databasePage.Controls.Add(fields);
            tabs.TabPages.Add(serverPage);
            tabs.TabPages.Add(databasePage);
            layout.Controls.Add(tabs, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button save = Theme.Button("บันทึก", true);
            Button cancel = Theme.Button("ยกเลิก", false);
            cancel.DialogResult = DialogResult.Cancel;
            save.Click += SaveEntry;
            actions.Controls.Add(save); actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 1);
            Controls.Add(layout);
            serverName.Text = original.Name; host.Text = original.Host;
            DatabaseOptions options = original.Database ?? new DatabaseOptions();
            databaseEnabled.Checked = options.Enabled;
            sqlServer.Text = options.DataSource ?? ""; databaseName.Text = options.DatabaseName ?? ""; sqlUser.Text = options.UserName ?? "";
            trustCertificate.Checked = options.TrustServerCertificate;
            databaseEnabled.CheckedChanged += delegate { SetDatabaseControls(); };
            SetDatabaseControls();
            AcceptButton = save; CancelButton = cancel;
            FormClosing += delegate { databaseCancellation.Cancel(); };
            FormClosed += delegate { databaseCancellation.Dispose(); };
        }

        private void SetDatabaseControls()
        {
            foreach (Control input in new Control[] { sqlServer, databaseName, sqlUser, sqlPassword, trustCertificate, testDatabase }) input.Enabled = databaseEnabled.Checked;
        }

        private ServerEntry ReadEntry()
        {
            var candidate = original.Copy();
            candidate.Name = serverName.Text.Trim(); candidate.Host = host.Text.Trim();
            candidate.Database.Enabled = databaseEnabled.Checked;
            candidate.Database.DataSource = sqlServer.Text.Trim(); candidate.Database.DatabaseName = databaseName.Text.Trim();
            candidate.Database.UserName = sqlUser.Text.Trim(); candidate.Database.TrustServerCertificate = trustCertificate.Checked;
            if (sqlPassword.Text.Length > 0) candidate.Database.ProtectedPassword = ProtectedSecrets.Protect(sqlPassword.Text);
            candidate.Validate();
            return candidate;
        }

        private async void TestDatabase(object sender, EventArgs args)
        {
            if (!databaseEnabled.Checked) return;
            testDatabase.Enabled = false;
            try
            {
                ServerEntry candidate = ReadEntry();
                databaseStatus.Text = "กำลังทดสอบฐานข้อมูล...";
                ProbeResult result = await new DatabaseProbe().CheckAsync(candidate, 5000, databaseCancellation.Token);
                if (!IsDisposed && !Disposing) { databaseStatus.Text = result.Message; databaseStatus.ForeColor = result.Success ? Theme.Green : Theme.Red; }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed && !Disposing) databaseStatus.Text = error.Message; }
            finally { if (!IsDisposed && !Disposing) testDatabase.Enabled = databaseEnabled.Checked; }
        }
    }
}
