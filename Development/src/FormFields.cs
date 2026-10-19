using System.Windows.Forms;

namespace ServerBotTracker
{
    internal static class FormFields
    {
        public static TableLayoutPanel Create()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        public static void AddField(TableLayoutPanel table, string label, Control input)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Theme.Label(label), 0, row);
            input.Margin = new Padding(0, 6, 0, 6);
            table.Controls.Add(input, 1, row);
        }

        public static void AddWide(TableLayoutPanel table, Control input)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            input.Margin = new Padding(0, 5, 0, 5);
            table.Controls.Add(input, 0, row);
            table.SetColumnSpan(input, 2);
        }
    }
}
