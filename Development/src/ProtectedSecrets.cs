using System;
using System.Security.Cryptography;
using System.Text;

namespace ServerBotTracker
{
    public static class ProtectedSecrets
    {
        private static readonly byte[] entropy = Encoding.UTF8.GetBytes("FKP.ServerMonitor.Secrets.v1");

        public static string Protect(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            byte[] clear = Encoding.UTF8.GetBytes(value);
            try { return Convert.ToBase64String(ProtectedData.Protect(clear, entropy, DataProtectionScope.CurrentUser)); }
            finally { Array.Clear(clear, 0, clear.Length); }
        }

        public static string Unprotect(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            try
            {
                byte[] clear = ProtectedData.Unprotect(Convert.FromBase64String(value), entropy, DataProtectionScope.CurrentUser);
                try { return Encoding.UTF8.GetString(clear); }
                finally { Array.Clear(clear, 0, clear.Length); }
            }
            catch (Exception error)
            {
                if (!(error is CryptographicException) && !(error is FormatException)) throw;
                throw new InvalidOperationException("อ่านข้อมูลลับไม่ได้ กรุณากรอกรหัสหรือลงชื่อเข้าใช้ใหม่บนผู้ใช้ Windows นี้");
            }
        }
    }
}
