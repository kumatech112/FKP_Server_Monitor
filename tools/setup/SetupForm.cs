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
    private readonly string targetDirectory;
    private readonly Action installAction;
    private readonly Button primary = Theme.Button("ติดตั้ง", true);
    private readonly Button cancel = Theme.Button("ยกเลิก", false);
    private readonly Label title = Theme.Label("พร้อมติดตั้งโปรแกรม");
    private readonly Label detail = Theme.Label("ตรวจสอบเซิร์ฟเวอร์หลายเครื่องในหน้าจอเดียว\nด้วย Ping พร้อมอัปเดตและการแจ้งเตือน");
    private readonly Label status = Theme.Label("ติดตั้งสำหรับผู้ใช้ Windows คนปัจจุบัน");
    private readonly ProgressBar progress = new ProgressBar { Name = "InstallProgress", Dock = DockStyle.Fill, Visible = false };
    private readonly CheckBox launch = new CheckBox { Name = "LaunchAfterSetup", Text = "เปิด Server Bot Tracker เมื่อกดเสร็จสิ้น", AutoSize = true, Checked = true, Visible = false };
    private bool installing;
    private bool complete;

    public SetupForm(string target, Action install)
    {
        targetDirectory = target;
        installAction = install;
        Theme.ApplyDialog(this, "ติดตั้ง Server Bot Tracker", new Size(780, 490));
        ShowInTaskbar = true;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        primary.Name = "InstallAction";
        cancel.Name = "CancelSetup";
        primary.Click += RunPrimaryAction;
        cancel.Click += delegate { Close(); };
        FormClosing += PreventCloseDuringInstallation;
        Shown += delegate { primary.Focus(); };
        AcceptButton = primary;
        CancelButton = cancel;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(25, 32, 25, 22), ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 188));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        PictureBox logo = Branding.CreateLogoBox(new Size(174, 174));
        logo.Anchor = AnchorStyles.Top;
        brand.Controls.Add(logo, 0, 0);
        Label product = Theme.Label("Server Bot\nTracker");
        product.Dock = DockStyle.Fill;
        product.Font = new Font("Tahoma", 20F, FontStyle.Bold);
        product.TextAlign = ContentAlignment.MiddleCenter;
        product.Margin = Padding.Empty;
        brand.Controls.Add(product, 0, 1);
        Label version = Theme.Label("รุ่น " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
        version.Dock = DockStyle.Fill;
        version.TextAlign = ContentAlignment.MiddleCenter;
        version.ForeColor = Theme.Blue;
        brand.Controls.Add(version, 0, 2);
        Label tagline = Theme.Label("ตรวจด้วย Ping\nรู้ผลได้ในหน้าจอเดียว");
        tagline.Dock = DockStyle.Fill;
        tagline.TextAlign = ContentAlignment.TopCenter;
        brand.Controls.Add(tagline, 0, 3);
        root.Controls.Add(brand, 0, 0);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30, 35, 30, 18), ColumnCount = 1, RowCount = 7, Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        title.Name = "InstallTitle";
        title.Font = new Font("Tahoma", 18F, FontStyle.Bold);
        title.Dock = DockStyle.Fill;
        title.Margin = Padding.Empty;
        content.Controls.Add(title, 0, 0);
        detail.Name = "InstallDetail";
        detail.Dock = DockStyle.Fill;
        detail.Margin = Padding.Empty;
        content.Controls.Add(detail, 0, 1);
        content.Controls.Add(Theme.Label("ตำแหน่งติดตั้ง"), 0, 2);
        var location = new TextBox { Name = "InstallLocation", Text = targetDirectory, ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Margin = new Padding(0, 0, 0, 12), TabStop = false };
        content.Controls.Add(location, 0, 3);
        status.Name = "InstallStatus";
        status.Dock = DockStyle.Fill;
        status.Margin = Padding.Empty;
        content.Controls.Add(status, 0, 4);
        content.Controls.Add(progress, 0, 5);
        launch.Margin = new Padding(0, 12, 0, 0);
        content.Controls.Add(launch, 0, 6);
        root.Controls.Add(content, 1, 0);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(24, 18, 24, 16), BackColor = Color.White, Margin = Padding.Empty };
        primary.MinimumSize = new Size(140, 40);
        cancel.MinimumSize = new Size(110, 40);
        footer.Controls.Add(primary);
        footer.Controls.Add(cancel);
        root.Controls.Add(footer, 0, 1);
        root.SetColumnSpan(footer, 2);
        Controls.Add(root);
    }

    private async void RunPrimaryAction(object sender, EventArgs args)
    {
        if (installing) return;
        if (complete)
        {
            try
            {
                if (launch.Checked)
                    Process.Start(new ProcessStartInfo(Path.Combine(targetDirectory, "ServerBotTracker.exe")) { UseShellExecute = true, WorkingDirectory = targetDirectory });
                Close();
            }
            catch (Exception exception)
            {
                status.Text = "เปิดโปรแกรมไม่ได้: " + exception.Message;
            }
            return;
        }
        installing = true;
        primary.Enabled = false;
        cancel.Enabled = false;
        title.Text = "กำลังติดตั้งโปรแกรม";
        status.Text = "กำลังคัดลอกไฟล์และสร้างทางลัด...";
        progress.Style = ProgressBarStyle.Marquee;
        progress.Visible = true;
        try
        {
            await RunInstallationAsync(installAction);
            complete = true;
            title.Text = "ติดตั้งเรียบร้อยแล้ว";
            detail.Text = "พร้อมใช้งาน Server Bot Tracker\nเปิดได้จาก Start menu หรือกดเสร็จสิ้น";
            status.Text = "รายการเซิร์ฟเวอร์และการตั้งค่าเดิมยังคงอยู่";
            primary.Text = "เสร็จสิ้น";
            launch.Visible = true;
            cancel.Visible = false;
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 100;
        }
        catch (Exception exception)
        {
            title.Text = "ยังติดตั้งไม่สำเร็จ";
            status.Text = exception.Message;
            primary.Text = "ลองอีกครั้ง";
            progress.Visible = false;
        }
        finally
        {
            installing = false;
            primary.Enabled = true;
            cancel.Enabled = true;
        }
    }

    private static Task RunInstallationAsync(Action install)
    {
        var completion = new TaskCompletionSource<bool>();
        var worker = new Thread(delegate()
        {
            try { install(); completion.SetResult(true); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        worker.IsBackground = true;
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        return completion.Task;
    }

    private void PreventCloseDuringInstallation(object sender, FormClosingEventArgs args)
    {
        if (installing) args.Cancel = true;
    }
}
