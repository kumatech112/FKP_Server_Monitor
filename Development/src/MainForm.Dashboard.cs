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
        private readonly Button[] summaryFilters = new Button[4];
        private readonly string[] dashboardFilterLabels = { "ทั้งหมด", "เชื่อมต่อปกติ", "ต้องตรวจสอบ", "มีปัญหา" };
        private int dashboardFilter;
        private Label emptyDashboard;
        private readonly Button dashboardView = Theme.Button("แดชบอร์ด", true);
        private readonly Button listView = Theme.Button("รายการเซิร์ฟเวอร์", false);
        private readonly Button deliverySettingsButton = Theme.Button("ตั้งค่าการแจ้งเตือน", false);

        private Control BuildNavigation()
        {
            var bar = new TableLayoutPanel { Name = "TopBar", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var identity = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            PictureBox logo = Branding.CreateLogoBox(new Size(32, 34));
            logo.Location = new Point(0, 3);
            identity.Controls.Add(logo);
            identity.Controls.Add(new Label
            {
                Name = "ProductHeading", Text = ProductIdentity.DisplayName,
                Font = new Font("Tahoma", 13F, FontStyle.Bold), ForeColor = DashboardStyle.Ink,
                AutoSize = true, Location = new Point(40, 9)
            });
            bar.Controls.Add(identity, 0, 0);
            var navigation = new FlowLayoutPanel { Name = "TopMenu", Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 3, 0, 0), Margin = Padding.Empty };
            foreach (Button button in new[] { dashboardView, listView, notificationButton, deliverySettingsButton, updateButton })
            {
                DashboardStyle.StyleButton(button, button == dashboardView);
                navigation.Controls.Add(button);
            }
            dashboardView.Click += delegate { ShowDashboard(true); };
            listView.Click += delegate { ShowDashboard(false); };
            deliverySettingsButton.Click += ShowAlertSettings;
            bar.Controls.Add(navigation, 1, 0);
            return bar;
        }

        private void ShowDashboard(bool showCards)
        {
            cards.Visible = showCards; grid.Visible = !showCards;
            if (showCards) cards.BringToFront(); else grid.BringToFront();
            DashboardStyle.StyleButton(dashboardView, showCards); DashboardStyle.StyleButton(listView, !showCards);
            if (showCards && !busy) progress.Text = "ภาพรวมสถานะเซิร์ฟเวอร์";
            if (showCards) ApplyDashboardFilter();
            UpdateTotals();
            ShowSelectedDetails();
        }

        private Control BuildSummary()
        {
            var summary = new TableLayoutPanel { Name = "StatusSummary", Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 0, 0, 6) };
            for (int index = 0; index < 4; index++)
            {
                summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                int filterIndex = index;
                Button button = Theme.Button(dashboardFilterLabels[index], false);
                button.Name = "DashboardFilter" + index;
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 0, index == 3 ? 0 : 8, 0);
                button.Click += delegate { SetDashboardFilter(filterIndex); };
                DashboardStyle.StyleButton(button, index == dashboardFilter);
                summaryFilters[index] = button;
                summary.Controls.Add(button, index, 0);
            }
            return summary;
        }

        private void SetDashboardFilter(int index)
        {
            if (index < 0 || index >= dashboardFilterLabels.Length)
                throw new ArgumentOutOfRangeException("index");
            dashboardFilter = index;
            grid.ClearSelection();
            for (int item = 0; item < summaryFilters.Length; item++)
                DashboardStyle.StyleButton(summaryFilters[item], item == dashboardFilter);
            ShowDashboard(true);
        }

        private bool MatchesDashboardFilter(string serverId)
        {
            if (dashboardFilter == 0) return true;
            ServerResult result;
            string health = results.TryGetValue(serverId, out result) ? result.Health : "pending";
            if (dashboardFilter == 1) return health == "healthy";
            if (dashboardFilter == 2) return health == "warning" || health == "pending";
            return health == "down";
        }

        private void ApplyDashboardFilter()
        {
            bool anyMatch = false;
            cards.SuspendLayout();
            try
            {
                foreach (KeyValuePair<string, ServerCard> item in cardViews)
                {
                    bool matches = MatchesDashboardFilter(item.Key);
                    item.Value.Visible = matches;
                    if (matches) anyMatch = true;
                    DataGridViewRow row;
                    if (!matches && cards.Visible && visibleRows.TryGetValue(item.Key, out row) && row.Selected)
                        row.Selected = false;
                }
                if (emptyDashboard != null)
                {
                    emptyDashboard.Text = configuration.Servers.Count == 0 ? "เพิ่มเซิร์ฟเวอร์เพื่อเริ่มติดตาม"
                        : dashboardFilter == 0 ? "ไม่พบรายการที่ตรงกับคำค้นหา" : "ไม่พบเซิร์ฟเวอร์ในกลุ่มที่เลือก";
                    emptyDashboard.Visible = !anyMatch;
                }
            }
            finally { cards.ResumeLayout(); }
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
                    var card = new ServerCard(server);
                    ServerResult result; if (results.TryGetValue(id, out result)) card.UpdateResult(result);
                    card.Selected = row.Selected;
                    card.SelectedByUser += delegate(object sender, EventArgs args) { SelectCard((ServerCard)sender, false); };
                    card.EditRequested += delegate(object sender, EventArgs args) { if (!busy) { SelectCard((ServerCard)sender, true); EditServer(sender, args); } };
                    cardViews[id] = card; cards.Controls.Add(card);
                }
                emptyDashboard = new Label { Name = "EmptyDashboard", ForeColor = DashboardStyle.Muted, AutoSize = true, Margin = new Padding(12, 24, 0, 0) };
                cards.Controls.Add(emptyDashboard);
                ResizeCards();
                ApplyDashboardFilter();
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
            float scale = DeviceDpi / 96f;
            int gap = Math.Max(1, (int)(8 * scale));
            int available = Math.Max(1, cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            int columns = Math.Max(1, (int)(available / (192 * scale)));
            int width = Math.Max(1, Math.Min((int)(230 * scale), available / columns - gap));
            foreach (ServerCard card in cardViews.Values)
            {
                card.Margin = new Padding(0, 0, gap, gap);
                card.Size = new Size(width, (int)(84 * scale));
            }
        }

        private void UpdateCard(ServerResult result)
        {
            ServerCard card;
            if (cardViews.TryGetValue(result.ServerId, out card)) card.UpdateResult(result);
            ApplyDashboardFilter();
        }
    }
}
