using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal sealed partial class ServerDialog : Form
    {
        private readonly ServerEntry original;
        private readonly TextBox serverName = new TextBox { Name = "ServerNameInput", Dock = DockStyle.Fill, MaxLength = 120 };
        private readonly TextBox host = new TextBox { Name = "HostInput", Dock = DockStyle.Fill, MaxLength = 253 };
        public ServerEntry Entry { get; private set; }

        public ServerDialog(ServerEntry entry)
        {
            original = entry.Copy();
            BuildServerEditor();
        }

        private void SaveEntry(object sender, EventArgs args)
        {
            ServerEntry candidate;
            try
            {
                candidate = ReadEntry();
                Entry = candidate;
                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException exception)
            {
                MessageBox.Show(this, exception.Message, "ตรวจสอบข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                MessageBox.Show(this, "Windows ป้องกันรหัสผ่านไม่ได้ กรุณาตรวจบัญชีผู้ใช้แล้วลองบันทึกใหม่", "บันทึกไม่ได้", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal sealed class BulkDialog : Form
    {
        private readonly TextBox hosts = new TextBox { Name = "BulkHostsInput", Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MaxLength = 65536 };
        public List<ServerEntry> Entries { get; private set; }

        public BulkDialog()
        {
            Theme.ApplyDialog(this, "เพิ่มหลายเซิร์ฟเวอร์", new Size(610, 460));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(Theme.Label("วาง IP หรือชื่อโฮสต์ บรรทัดละหนึ่งรายการ\nทุกรายการตรวจด้วย Ping"), 0, 0);
            layout.Controls.Add(hosts, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button add = Theme.Button("เพิ่มรายการ", true);
            Button cancel = Theme.Button("ยกเลิก", false);
            cancel.DialogResult = DialogResult.Cancel;
            add.Click += AddEntries;
            actions.Controls.Add(add);
            actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 2);
            Controls.Add(layout);
            CancelButton = cancel;
        }

        private void AddEntries(object sender, EventArgs args)
        {
            var entries = new List<ServerEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = hosts.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                foreach (string line in lines)
                {
                    string value = line.Trim();
                    if (value.Length == 0 || !seen.Add(value)) continue;
                    var entry = new ServerEntry { Name = value, Host = value };
                    entry.Validate();
                    entries.Add(entry);
                }
                if (entries.Count == 0)
                    throw new ArgumentException("กรุณาระบุอย่างน้อยหนึ่งเซิร์ฟเวอร์");
                Entries = entries;
                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException exception)
            {
                MessageBox.Show(this, exception.Message, "ตรวจสอบข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
