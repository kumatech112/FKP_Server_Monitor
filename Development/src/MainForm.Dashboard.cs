using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ServerBotTracker
{
    public sealed partial class MainForm
    {
        private readonly FlowLayoutPanel cards = new FlowLayoutPanel { Name = "ServerCards", Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Margin = Padding.Empty };
        private readonly Dictionary<string, ServerCard> cardViews = new Dictionary<string, ServerCard>();
        private readonly Dictionary<string, List<long?>> histories = new Dictionary<string, List<long?>>();
        private readonly Label[] summaryValues = new Label[4];
        private readonly Button dashboardView = Theme.Button("แดชบอร์ด", true);
        private readonly Button listView = Theme.Button("รายการเซิร์ฟเวอร์", false);
        private readonly Button deliverySettingsButton = Theme.Button("ตั้งค่าการแจ้งเตือน", false);

        private FlowLayoutPanel BuildNavigation()
        {
            var navigation = new FlowLayoutPanel { Name = "TopMenu", Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0), Margin = Padding.Empty };
            foreach (Button button in new[] { dashboardView, listView, notificationButton, deliverySettingsButton, updateButton })
            {
                DashboardStyle.StyleButton(button, button == dashboardView); navigation.Controls.Add(button);
            }
            dashboardView.Click += delegate { ShowDashboard(true); };
            listView.Click += delegate { ShowDashboard(false); };
            deliverySettingsButton.Click += ShowAlertSettings;
            return navigation;
        }

        private void ShowDashboard(bool showCards)
        {
            cards.Visible = showCards; grid.Visible = !showCards;
            if (showCards) cards.BringToFront(); else grid.BringToFront();
            DashboardStyle.StyleButton(dashboardView, showCards); DashboardStyle.StyleButton(listView, !showCards);
        }

        private Control BuildSummary()
        {
            var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
            string[] labels = { "เซิร์ฟเวอร์ทั้งหมด", "ใช้งานได้", "ต้องตรวจสอบ", "ไม่ตอบกลับ" };
            Color[] colors = { DashboardStyle.Blue, DashboardStyle.Green, DashboardStyle.Amber, DashboardStyle.Red };
            for (int index = 0; index < 4; index++)
            {
                summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var tile = new Panel { Dock = DockStyle.Fill, BackColor = DashboardStyle.Surface, Margin = new Padding(0, 0, index == 3 ? 0 : 12, 0) };
                summaryValues[index] = new Label { Text = "0", Font = new Font("Tahoma", 22F, FontStyle.Bold), ForeColor = colors[index], Location = new Point(16, 30), AutoSize = true };
                tile.Controls.Add(new Label { Text = labels[index], ForeColor = DashboardStyle.Muted, Location = new Point(17, 10), AutoSize = true });
                tile.Controls.Add(summaryValues[index]); summary.Controls.Add(tile, index, 0);
            }
            return summary;
        }

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
                    List<long?> history;
                    if (!histories.TryGetValue(id, out history)) { history = new List<long?>(); histories[id] = history; }
                    var card = new ServerCard(server, history);
                    ServerResult result; if (results.TryGetValue(id, out result)) card.UpdateResult(result);
                    card.Selected = row.Selected;
                    card.SelectedByUser += delegate(object sender, EventArgs args) { SelectCard((ServerCard)sender, false); };
                    card.EditRequested += delegate(object sender, EventArgs args) { if (!busy) { SelectCard((ServerCard)sender, true); EditServer(sender, args); } };
                    cardViews[id] = card; cards.Controls.Add(card);
                }
                if (cardViews.Count == 0)
                    cards.Controls.Add(new Label { Name = "EmptyDashboard", Text = configuration.Servers.Count == 0 ? "เพิ่มเซิร์ฟเวอร์เพื่อเริ่มติดตาม\nผล Ping และ SQL Server จะแสดงเป็นการ์ดในหน้านี้" : "ไม่พบรายการที่ตรงกับคำค้นหา", ForeColor = DashboardStyle.Muted, AutoSize = true, Margin = new Padding(20, 45, 0, 0) });
                ResizeCards();
            }
            finally { cards.ResumeLayout(); }
        }

        private void SelectCard(ServerCard card, bool exclusive)
        {
            DataGridViewRow row;
            if (!visibleRows.TryGetValue(card.ServerId, out row)) return;
            bool previous = row.Selected;
            if (exclusive || (ModifierKeys & Keys.Control) == 0) grid.ClearSelection();
            row.Selected = exclusive || (ModifierKeys & Keys.Control) == 0 || !previous;
            SyncCardSelections(); ShowSelectedDetails();
        }

        private void SyncCardSelections()
        {
            foreach (KeyValuePair<string, ServerCard> card in cardViews)
            {
                DataGridViewRow row;
                card.Value.Selected = visibleRows.TryGetValue(card.Key, out row) && row.Selected;
            }
        }

        private void ResizeCards()
        {
            int available = Math.Max(260, cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            float scale = DeviceDpi / 96f;
            int columns = Math.Max(1, Math.Min(4, (int)(available / (290 * scale))));
            int width = available / columns - 14;
            foreach (ServerCard card in cardViews.Values) card.Size = new Size(width, (int)(236 * scale));
        }

        private void UpdateCard(ServerResult result)
        {
            List<long?> history;
            if (!histories.TryGetValue(result.ServerId, out history)) { history = new List<long?>(); histories[result.ServerId] = history; }
            history.Add(result.Ping != null && result.Ping.Success ? result.Ping.Milliseconds : null);
            if (history.Count > 40) history.RemoveAt(0);
            ServerCard card; if (cardViews.TryGetValue(result.ServerId, out card)) card.UpdateResult(result);
        }
    }
}
