using System;
using System.Threading;
using System.Windows.Forms;
using System.Security.Principal;

namespace ServerBotTracker
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
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
                    Application.Run(new MainForm(ConfigurationStore.ForCurrentUser()));
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
