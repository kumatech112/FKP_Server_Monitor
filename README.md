# FKP Server Monitor

โฟลเดอร์โปรเจกต์และ GitHub repository ใช้ชื่อ **FKP_Server_Monitor** ส่วนชื่อโปรแกรมยังเป็น **FKP Server Monitor**

ดับเบิลคลิก **FKP Server Monitor.lnk** ในโฟลเดอร์นี้เพื่อเปิดโปรแกรมภาษาไทย หรือเปิด **Program/FKPServerMonitor.exe** โดยตรง

| โฟลเดอร์ | ใช้เก็บอะไร |
| --- | --- |
| **Program** | โปรแกรมพร้อมใช้งาน ตัวติดตั้ง และคู่มือภาษาไทย |
| **Development** | ซอร์สโค้ด ไอคอน เอกสาร สคริปต์สร้างโปรแกรม และโค้ดตรวจสอบ |
| **Archive** | รุ่นก่อนหน้า ชุดเผยแพร่ ผลตรวจสอบ และไฟล์ระหว่างสร้างโปรแกรม |

หากต้องการติดตั้ง ให้เปิด **Program/FKPServerMonitor-Setup.exe** เลือกโฟลเดอร์ปลายทาง แล้วทำตามตัวช่วย เริ่มต้นที่ `C:\Program Files\FKP Server Monitor` ถอนการติดตั้งได้จาก Control Panel หรือ Uninstall.exe หลังติดตั้ง

หากใช้แบบ Portable ให้เก็บโฟลเดอร์ **Program** ทั้งชุดไว้ด้วยกัน โปรแกรมต้องใช้ Windows และ .NET Framework 4.8 ขึ้นไป ไม่ต้องติดตั้ง Python หรือเครื่องมือพัฒนา

รายการเซิร์ฟเวอร์และประวัติการแจ้งเตือนยังเก็บแยกที่ `%LOCALAPPDATA%\Kuma\ServerBotTracker` การจัดโฟลเดอร์ครั้งนี้ไม่ย้ายหรือลบข้อมูลดังกล่าว

- [คู่มือผู้ใช้ภาษาไทย](Program/USER_GUIDE_TH.md)
- [คู่มือผู้พัฒนา](Development/README.md)
- [รายละเอียดโฟลเดอร์และไฟล์/ฟังก์ชันที่ปรับ](Development/docs/FOLDER_LAYOUT.md)
- [ผลตรวจสอบหลังจัดโฟลเดอร์](Development/docs/FOLDER_LAYOUT_VERIFICATION.md)
- [วิธีเผยแพร่โปรแกรมและประกาศ](Development/docs/RELEASES.th.md)

Git เก็บซอร์สโค้ดและเอกสาร ส่วน **Program**, **Archive**, ทางลัด และไฟล์ชั่วคราวไม่ถูกนำเข้า Git การจัดโฟลเดอร์หรือ push โค้ดไม่ได้เผยแพร่อัปเดตหรือส่งประกาศให้ผู้ใช้โดยอัตโนมัติ
