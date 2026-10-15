using System;
using System.Threading;
using System.Windows.Forms;
using System.Security.Principal;

namespace ServerBotTracker
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] arguments)
        {
            if (arguments.Length == 2 && arguments[0] == "--apply-update")
            {
                Environment.ExitCode = UpdateInstaller.RunWorker(arguments[1]);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string identity = WindowsIdentity.GetCurrent().User.Value;
            bool created;
            using (var instance = new Mutex(true, "Local\\Kuma.ServerBotTracker." + identity, out created))
            {
                if (!created)
                {
                    MessageBox.Show("โปรแกรมเปิดอยู่แล้ว กรุณาใช้หน้าต่างเดิม", "ตรวจสอบเซิร์ฟเวอร์", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    using (var form = new MainForm(ConfigurationStore.ForCurrentUser()))
                    {
                        if (arguments.Length == 1 && arguments[0] == "--resume-monitoring")
                            form.Shown += delegate { form.ResumeMonitoring(); };
                        Application.Run(form);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("เปิดโปรแกรมไม่สำเร็จ: " + exception.Message + "\n\nข้อมูลการตั้งค่าเดิมจะไม่ถูกเขียนทับ\nตำแหน่งข้อมูล: " + ConfigurationStore.ForCurrentUser().FilePath, "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { instance.ReleaseMutex(); }
            }
        }
    }
}
