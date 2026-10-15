using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServerBotTracker;

internal sealed class SetupForm : Form
{
    private enum WizardPage { Welcome, Destination, Ready, Working, Complete, Failed }
    private string targetDirectory;
    private readonly Action<string> operation;
    private readonly bool uninstalling;
    private readonly Button primary = new Button { Name = "WizardNext", Size = new Size(92, 28) };
    private readonly Button back = new Button { Name = "WizardBack", Text = "< ย้อนกลับ", Size = new Size(92, 28) };
    private readonly Button cancel = new Button { Name = "WizardCancel", Text = "ยกเลิก", Size = new Size(92, 28) };
    private readonly Label title = new Label { Name = "WizardTitle", Dock = DockStyle.Fill, Font = new Font("Tahoma", 14F, FontStyle.Bold) };
    private readonly Label detail = new Label { Name = "WizardDetail", Dock = DockStyle.Fill };
    private readonly Label status = new Label { Name = "WizardStatus", Dock = DockStyle.Fill };
    private readonly ProgressBar progress = new ProgressBar { Name = "WizardProgress", Dock = DockStyle.Fill };
    private readonly CheckBox launch = new CheckBox { Name = "LaunchAfterSetup", Text = "เปิด Server Bot Tracker", AutoSize = true, Checked = true };
    private readonly TableLayoutPanel content = new TableLayoutPanel();
    private readonly Panel location = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly TextBox destination = new TextBox { Name = "InstallLocation", Dock = DockStyle.Fill };
    private readonly Button browse = new Button { Name = "BrowseInstallLocation", Text = "เรียกดู...", Dock = DockStyle.Fill };
    private WizardPage page;

    public SetupForm(string target, Action<string> install) : this(target, install, false) { }

    public SetupForm(string target, Action<string> performOperation, bool uninstall)
    {
        targetDirectory = target;
        operation = performOperation;
        uninstalling = uninstall;
        Branding.ApplyTo(this);
        Text = "Server Bot Tracker " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + (uninstall ? " • ถอนการติดตั้ง" : " • ติดตั้ง");
        ClientSize = new Size(640, 420);
        Font = new Font("Tahoma", 9.5F);
        ForeColor = Color.Black;
        BackColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        primary.Click += RunPrimaryAction;
        back.Click += GoBack;
        browse.Click += BrowseDirectory;
        destination.TextChanged += delegate { if (page == WizardPage.Destination) status.Text = String.Empty; };
        cancel.Click += delegate { Close(); };
        FormClosing += PreventCloseDuringOperation;
        Shown += delegate { primary.Focus(); };
        AcceptButton = primary;
        CancelButton = cancel;
        ShowPage(WizardPage.Welcome);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        var artwork = new WizardArtwork { Name = "WizardArtwork", Dock = DockStyle.Fill, Margin = Padding.Empty };
        PictureBox logo = Branding.CreateLogoBox(new Size(142, 142));
        artwork.Controls.Add(logo);
        artwork.SizeChanged += delegate { logo.Location = new Point((artwork.Width - logo.Width) / 2, (artwork.Height - logo.Height) / 2); };
        root.Controls.Add(artwork, 0, 0);
        content.Dock = DockStyle.Fill;
        content.Padding = new Padding(21, 24, 21, 12);
        content.ColumnCount = 1;
        content.RowCount = 6;
        content.Margin = Padding.Empty;
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        title.Margin = Padding.Empty;
        detail.Margin = Padding.Empty;
        status.Margin = new Padding(0, 8, 0, 0);
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 32, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        destination.Text = targetDirectory;
        destination.Margin = new Padding(0, 4, 6, 0);
        browse.Margin = Padding.Empty;
        folderRow.Controls.Add(destination, 0, 0);
        folderRow.Controls.Add(browse, 1, 0);
        location.Controls.Add(folderRow);
        location.Controls.Add(new Label { Text = "โฟลเดอร์ติดตั้งโปรแกรม", Dock = DockStyle.Top, Height = 23 });
        content.Controls.Add(title, 0, 0);
        content.Controls.Add(detail, 0, 1);
        content.Controls.Add(location, 0, 2);
        content.Controls.Add(status, 0, 3);
        content.Controls.Add(progress, 0, 4);
        content.Controls.Add(launch, 0, 5);
        root.Controls.Add(content, 1, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(13, 13, 13, 11), BackColor = SystemColors.Control, Margin = Padding.Empty };
        footer.Paint += delegate(object sender, PaintEventArgs args) { args.Graphics.DrawLine(SystemPens.ControlDark, 0, 0, footer.Width, 0); };
        cancel.Margin = new Padding(9, 0, 0, 0);
        primary.Margin = Padding.Empty;
        back.Margin = new Padding(0, 0, 3, 0);
        footer.Controls.Add(cancel);
        footer.Controls.Add(primary);
        footer.Controls.Add(back);
        root.Controls.Add(footer, 0, 1);
        root.SetColumnSpan(footer, 2);
        Controls.Add(root);
    }

    private void ShowPage(WizardPage next)
    {
        page = next;
        bool working = page == WizardPage.Working;
        bool complete = page == WizardPage.Complete;
        bool ready = page == WizardPage.Ready;
        bool selecting = page == WizardPage.Destination;
        bool failed = page == WizardPage.Failed;
        primary.Enabled = !working;
        cancel.Enabled = !working;
        cancel.Visible = !complete;
        back.Visible = !complete && !working;
        back.Enabled = selecting || ready || failed;
        location.Visible = selecting || ready;
        destination.ReadOnly = !selecting;
        destination.Multiline = !selecting;
        destination.ScrollBars = selecting ? ScrollBars.None : ScrollBars.Vertical;
        destination.BackColor = Color.White;
        destination.TabStop = selecting;
        browse.Visible = selecting;
        browse.Parent.Height = selecting ? 32 : 78;
        ((TableLayoutPanel)browse.Parent).ColumnStyles[1].Width = selecting ? 82 : 0;
        status.Visible = selecting || working || complete || failed;
        if (selecting) status.Text = String.Empty;
        progress.Visible = working || complete;
        launch.Visible = complete && !uninstalling;
        content.RowStyles[2].Height = selecting ? 76 : (ready ? 106 : 0);
        content.RowStyles[3].Height = status.Visible ? 70 : 0;
        content.RowStyles[4].Height = progress.Visible ? 24 : 0;
        content.RowStyles[5].Height = launch.Visible ? 32 : 0;
        primary.Text = complete ? "เสร็จสิ้น" : (failed ? "ลองอีกครั้ง" : (ready ? (uninstalling ? "ถอนการติดตั้ง" : "ติดตั้ง") : "ถัดไป >"));
        if (page == WizardPage.Welcome)
        {
            title.Text = uninstalling ? "ยินดีต้อนรับสู่ตัวช่วย\nถอนการติดตั้ง" : "ยินดีต้อนรับสู่การติดตั้ง\nServer Bot Tracker";
            detail.Text = uninstalling
                ? "ตัวช่วยนี้จะนำ Server Bot Tracker ออกจากเครื่องของคุณ\n\nกรุณาปิดโปรแกรมก่อนดำเนินการ\nรายการเซิร์ฟเวอร์และการตั้งค่าส่วนตัวจะยังคงอยู่\n\nคลิก ถัดไป เพื่อดำเนินการต่อ"
                : "ตัวช่วยนี้จะแนะนำขั้นตอนการติดตั้ง Server Bot Tracker\n\nกรุณาปิดโปรแกรมก่อนเริ่มติดตั้ง เพื่อให้สามารถอัปเดตไฟล์โปรแกรมได้ครบถ้วน\n\nคลิก ถัดไป เพื่อดำเนินการต่อ";
        }
        else if (selecting)
        {
            title.Text = "เลือกโฟลเดอร์ติดตั้ง";
            detail.Text = "พิมพ์ตำแหน่งที่ต้องการ หรือคลิก เรียกดู...\n\nเลือกโฟลเดอร์ที่บัญชี Windows ของคุณเขียนไฟล์ได้\nโปรแกรมจะติดตั้งลงในโฟลเดอร์ที่เลือกโดยตรง\n\nคลิก ถัดไป เพื่อยืนยันตำแหน่ง";
        }
        else if (ready)
        {
            title.Text = uninstalling ? "พร้อมถอนการติดตั้ง" : "พร้อมติดตั้งโปรแกรม";
            detail.Text = uninstalling ? "คลิก ถอนการติดตั้ง เพื่อนำโปรแกรมและทางลัดออก\nไฟล์รายการเซิร์ฟเวอร์และการตั้งค่าจะไม่ถูกลบ" : "โปรแกรมจะติดตั้งสำหรับผู้ใช้ Windows คนปัจจุบัน\nและเพิ่มทางลัดใน Start menu\n\nคลิก ติดตั้ง เพื่อเริ่มดำเนินการ";
        }
        else if (working)
        {
            title.Text = uninstalling ? "กำลังถอนการติดตั้ง" : "กำลังติดตั้งโปรแกรม";
            detail.Text = "กรุณารอสักครู่จนกว่าขั้นตอนจะเสร็จสมบูรณ์";
            status.Text = uninstalling ? "กำลังนำไฟล์โปรแกรมและทางลัดออก..." : "กำลังคัดลอกไฟล์และสร้างทางลัด...";
            progress.Style = ProgressBarStyle.Marquee;
        }
        else if (complete)
        {
            title.Text = uninstalling ? "ถอนการติดตั้งเสร็จสมบูรณ์" : "ติดตั้งเสร็จสมบูรณ์";
            detail.Text = uninstalling ? "นำ Server Bot Tracker ออกจากเครื่องเรียบร้อยแล้ว\n\nคลิก เสร็จสิ้น เพื่อปิดตัวช่วย" : "พร้อมใช้งาน Server Bot Tracker\nเปิดโปรแกรมได้จาก Start menu\n\nคลิก เสร็จสิ้น เพื่อปิดตัวช่วย";
            status.Text = "รายการเซิร์ฟเวอร์และการตั้งค่าเดิมยังคงอยู่";
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 100;
        }
        else
        {
            title.Text = uninstalling ? "ยังถอนการติดตั้งไม่สำเร็จ" : "ยังติดตั้งไม่สำเร็จ";
            detail.Text = "ตรวจสอบข้อความด้านล่าง แล้วคลิก ลองอีกครั้ง\nหรือคลิก ย้อนกลับ เพื่อทบทวนขั้นตอน";
        }
    }

    private async void RunPrimaryAction(object sender, EventArgs args)
    {
        if (page == WizardPage.Working) return;
        if (page == WizardPage.Welcome) { ShowPage(uninstalling ? WizardPage.Ready : WizardPage.Destination); return; }
        if (page == WizardPage.Destination)
        {
            try
            {
                string selected = InstallLocation.Normalize(destination.Text);
                InstallLocation.ValidateProductFiles(selected);
                targetDirectory = selected;
                destination.Text = selected;
                ShowPage(WizardPage.Ready);
            }
            catch (Exception exception) { status.Text = exception.Message; destination.Focus(); }
            return;
        }
        if (page == WizardPage.Complete)
        {
            try
            {
                if (!uninstalling && launch.Checked)
                    Process.Start(new ProcessStartInfo(Path.Combine(targetDirectory, "ServerBotTracker.exe")) { UseShellExecute = true, WorkingDirectory = targetDirectory });
                Close();
            }
            catch (Exception exception) { status.Text = "เปิดโปรแกรมไม่ได้: " + exception.Message; }
            return;
        }
        ShowPage(WizardPage.Working);
        try
        {
            string selectedDirectory = targetDirectory;
            await RunOperationAsync(delegate { operation(selectedDirectory); });
            ShowPage(WizardPage.Complete);
        }
        catch (Exception exception)
        {
            ShowPage(WizardPage.Failed);
            status.Text = exception.Message;
        }
    }

    private void GoBack(object sender, EventArgs args)
    {
        if (page == WizardPage.Ready || page == WizardPage.Failed)
            ShowPage(uninstalling ? WizardPage.Welcome : WizardPage.Destination);
        else if (page == WizardPage.Destination)
            ShowPage(WizardPage.Welcome);
    }

    private void BrowseDirectory(object sender, EventArgs args)
    {
        using (var picker = new FolderBrowserDialog { Description = "เลือกโฟลเดอร์ติดตั้ง Server Bot Tracker", ShowNewFolderButton = true })
        {
            if (Directory.Exists(destination.Text)) picker.SelectedPath = destination.Text;
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                destination.Text = picker.SelectedPath;
                status.Text = String.Empty;
            }
        }
    }

    private static Task RunOperationAsync(Action performOperation)
    {
        var completion = new TaskCompletionSource<bool>();
        var worker = new Thread(delegate()
        {
            try { performOperation(); completion.SetResult(true); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        worker.IsBackground = true;
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        return completion.Task;
    }

    private void PreventCloseDuringOperation(object sender, FormClosingEventArgs args)
    {
        if (page == WizardPage.Working) args.Cancel = true;
    }

    private sealed class WizardArtwork : Panel
    {
        public WizardArtwork() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            args.Graphics.Clear(Color.FromArgb(15, 25, 54));
            using (var top = new SolidBrush(Color.FromArgb(25, 43, 91)))
                args.Graphics.FillPolygon(top, new[] { new Point(0, 0), new Point(Width, 0), new Point(Width, Height * 3 / 5), new Point(0, Height * 2 / 5) });
            using (var stripe = new SolidBrush(Color.FromArgb(40, 65, 128)))
                args.Graphics.FillPolygon(stripe, new[] { new Point(0, Height * 2 / 5), new Point(Width, Height * 3 / 5), new Point(Width, Height * 3 / 4), new Point(0, Height * 2 / 3) });
        }
    }
}
