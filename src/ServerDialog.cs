using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ServerBotTracker
{
    internal sealed class ServerDialog : Form
    {
        private readonly ServerEntry original;
        private readonly TextBox serverName = new TextBox { Dock = DockStyle.Fill, MaxLength = 120 };
        private readonly TextBox host = new TextBox { Dock = DockStyle.Fill, MaxLength = 253 };
        private readonly NumericUpDown port = new NumericUpDown { Minimum = 1, Maximum = 65535, Width = 130 };
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        public ServerEntry Entry { get; private set; }

        public ServerDialog(ServerEntry entry)
        {
            original = entry.Copy();
            Theme.ApplyDialog(this, "รายละเอียดเซิร์ฟเวอร์", new Size(530, 310));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 2, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int index = 0; index < 4; index++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.Controls.Add(Theme.Label("ชื่อเซิร์ฟเวอร์"), 0, 0);
            layout.Controls.Add(serverName, 1, 0);
            layout.Controls.Add(Theme.Label("IP / ชื่อโฮสต์"), 0, 1);
            layout.Controls.Add(host, 1, 1);
            layout.Controls.Add(Theme.Label("วิธีตรวจสอบ"), 0, 2);
            layout.Controls.Add(mode, 1, 2);
            layout.Controls.Add(Theme.Label("พอร์ต TCP"), 0, 3);
            layout.Controls.Add(port, 1, 3);
            var help = Theme.Label("ระบุชื่อโฮสต์โดยไม่ใส่ http:// หรือ https://");
            layout.Controls.Add(help, 0, 4);
            layout.SetColumnSpan(help, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button save = Theme.Button("บันทึก", true);
            Button cancel = Theme.Button("ยกเลิก", false);
            cancel.DialogResult = DialogResult.Cancel;
            save.Click += SaveEntry;
            actions.Controls.Add(save);
            actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 5);
            layout.SetColumnSpan(actions, 2);
            Controls.Add(layout);
            mode.Items.AddRange(new object[] { "ICMP + TCP", "ICMP เท่านั้น", "TCP เท่านั้น" });
            serverName.Text = original.Name;
            host.Text = original.Host;
            port.Value = original.Port;
            mode.SelectedIndex = original.Mode == "ICMP" ? 1 : original.Mode == "TCP" ? 2 : 0;
            mode.SelectedIndexChanged += delegate { port.Enabled = mode.SelectedIndex != 1; };
            port.Enabled = mode.SelectedIndex != 1;
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void SaveEntry(object sender, EventArgs args)
        {
            var candidate = original.Copy();
            candidate.Name = serverName.Text.Trim();
            candidate.Host = host.Text.Trim();
            candidate.Port = (int)port.Value;
            candidate.Mode = mode.SelectedIndex == 1 ? "ICMP" : mode.SelectedIndex == 2 ? "TCP" : "Both";
            try
            {
                candidate.Validate();
                Entry = candidate;
                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException exception)
            {
                MessageBox.Show(this, exception.Message, "ตรวจสอบข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    internal sealed class BulkDialog : Form
    {
        private readonly TextBox hosts = new TextBox { Name = "BulkHostsInput", Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MaxLength = 65536 };
        private readonly NumericUpDown port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 443, Width = 110 };
        public List<ServerEntry> Entries { get; private set; }

        public BulkDialog()
        {
            Theme.ApplyDialog(this, "เพิ่มหลายเซิร์ฟเวอร์", new Size(610, 460));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(Theme.Label("วาง IP หรือชื่อโฮสต์ บรรทัดละหนึ่งรายการ\nทุกรายการใช้ ICMP + TCP และพอร์ตที่เลือกด้านล่าง"), 0, 0);
            layout.Controls.Add(hosts, 0, 1);
            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            settings.Controls.Add(Theme.Label("พอร์ต TCP"));
            settings.Controls.Add(port);
            layout.Controls.Add(settings, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button add = Theme.Button("เพิ่มรายการ", true);
            Button cancel = Theme.Button("ยกเลิก", false);
            cancel.DialogResult = DialogResult.Cancel;
            add.Click += AddEntries;
            actions.Controls.Add(add);
            actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 3);
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
                    var entry = new ServerEntry { Name = value, Host = value, Port = (int)port.Value };
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
