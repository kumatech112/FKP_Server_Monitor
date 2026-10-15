# การเผยแพร่รุ่นใหม่และประกาศ

## โครงสร้างที่เลือก

- โค้ด ตัวติดตั้ง และอัปเดตอยู่ใน public repository เดียวกัน: `kumatech112/Server_Bot_Tracker` ตามที่เจ้าของเปลี่ยนเป็นสาธารณะ
- โปรแกรมอ่านข้อมูลผ่าน HTTPS โดยไม่ฝัง GitHub token
- รุ่นก่อน 1.2 ยังไม่มีตัวอัปเดต ต้องติดตั้ง 1.2 หนึ่งครั้งก่อนจึงจะรับอัปเดตรุ่นถัดไปได้

ต้องมี stable release ที่เผยแพร่แล้วจึงจะดาวน์โหลดอัปเดตได้ หากติดต่อช่องทางไม่ได้ Dashboard Ping ยังทำงานตามปกติ

## สร้างรุ่นโปรแกรม

1. ปรับ `AssemblyVersion` และ `AssemblyFileVersion` ใน `src/AssemblyInfo.cs` ให้สูงกว่ารุ่นที่เผยแพร่แล้ว เช่น `1.7.0.0`
2. รันคำสั่งต่อไปนี้จากโฟลเดอร์โครงการ:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Package.ps1 -Notes 'รายละเอียดการเปลี่ยนแปลงรุ่นนี้'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verify.ps1
```

3. ตรวจผลทดสอบและเปิดดูรายละเอียดรุ่นที่ `release\publish\v<รุ่น>`
4. ใน public repository สร้าง Release ด้วย tag ที่ตรงกับ `Tag` ใน `update.json` เช่น `v1.7.0`
5. แนบเฉพาะสามไฟล์จากโฟลเดอร์รุ่น:

   - `ServerBotTracker-Setup.exe`
   - `ServerBotTracker-portable.zip`
   - `update.json`

6. ใส่รายละเอียดรุ่น ตรวจสอบอีกครั้ง แล้วเผยแพร่เป็นรุ่นปกติและกำหนดให้เป็น Latest

ห้ามนำโฟลเดอร์โครงการ ข้อมูลผู้ใช้ ไฟล์ `servers.json` หรือผลการทดสอบไปแนบ โปรแกรมดาวน์โหลด ZIP ที่มีเฉพาะ EXE และ runtime config สองไฟล์ และตรวจชื่อไฟล์ ขนาด SHA-256 ชื่อผลิตภัณฑ์ และหมายเลขรุ่นก่อนติดตั้ง

การแก้โค้ดหรือ commit อย่างเดียว **ไม่ใช่การเผยแพร่อัปเดต** ต้องสร้างแพ็กเกจ ทดสอบ และเผยแพร่ release ด้วย โปรแกรมตรวจตอนเปิดและทุกสิบนาที ดาวน์โหลดรุ่นใหม่อัตโนมัติ แล้วติดตั้งเมื่อปิดโปรแกรม หรือกด **อัปเดตและเริ่มใหม่**

## ประกาศที่ไม่ต้องออกรุ่นโปรแกรมใหม่

สร้าง release สำหรับประกาศด้วย tag `announcements` และกำหนดเป็น **Pre-release** เพื่อไม่ให้กลายเป็น Latest ของโปรแกรม แนบไฟล์ชื่อ `announcements.json` เสมอ

ครั้งแรก หากยังไม่ต้องการส่งข้อความ ให้ใช้ `tools/announcements.empty.json` โดยเปลี่ยนชื่อไฟล์ที่แนบเป็น `announcements.json` ไฟล์นี้ไม่มีข้อความและไม่ส่งการแจ้งเตือนใด ๆ

เตรียมข้อความใหม่ด้วย:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Prepare-Announcement.ps1 -Id 'notice-20261015-01' -Title 'หัวข้อประกาศ' -Message 'ข้อความที่ต้องการแจ้งผู้ใช้งาน' -Days 14
```

คำสั่งนี้สร้าง `release\publish\announcements\announcements.json` เท่านั้น **ยังไม่ได้ส่งข้อความ** ตรวจเนื้อหาแล้วจึงนำไปแทนที่ asset ชื่อเดิมใน release `announcements` ด้วยตนเอง

โปรแกรมที่เปิดอยู่ตรวจประกาศทุกสิบนาที ข้อความจะแสดงในกล่องการแจ้งเตือนและแจ้งเตือนบน Windows หาก Windows อนุญาต ต้องใช้ `Id` ใหม่เมื่ออยากส่งประกาศใหม่ การใช้ `Id` เดิมจะไม่แจ้งซ้ำ แม้ผู้ใช้อ่านแล้วหรือเปิดโปรแกรมใหม่ ข้อความมีวันเริ่มและวันหมดอายุเป็น UTC ส่วนหน้าจอแสดงเวลาท้องถิ่น

ไฟล์ตัวอย่างมีหนึ่งประกาศ หากต้องคงหลายประกาศให้เพิ่มใน `Items` โดยใช้ ID ไม่ซ้ำ สูงสุด 50 รายการ และไฟล์รวมต้องไม่เกิน 256 KB ข้อความแสดงเป็นข้อความธรรมดา ไม่มี HTML หรือคำสั่งให้เครื่องปลายทางรัน

## GitHub Actions

workflow ใน `.github/workflows/release.yml` จะ build, package และ verify ก่อนเผยแพร่ เมื่อ push tag รุ่น เช่น `v1.7.0` จะตรวจว่าตรงกับรุ่นโปรแกรมแล้วสร้าง stable release และตั้งเป็น Latest ส่วนการกด Run workflow เองจะสร้าง **Draft release** ซึ่งต้อง Publish จึงจะส่งอัปเดต

การ push เข้า main จะ build และทดสอบโดยไม่สร้างรุ่นโปรแกรมใหม่ พร้อมตรวจว่ามีช่องทางประกาศแล้ว ขั้นตอนประกาศใช้ PowerShell 7 เพื่อจัดการกรณียังไม่มี release ได้ถูกต้อง

ใช้ `GITHUB_TOKEN` ที่ GitHub สร้างให้ workflow โดยให้สิทธิ์ Contents write ใน repository นี้ ไม่ต้องเพิ่ม `RELEASES_TOKEN` และไม่ฝังข้อมูลรับรองลงในโปรแกรม

เมื่อยังไม่มี release `announcements` workflow จะสร้างช่องทางพร้อมไฟล์ว่าง จึงไม่มีข้อความประกาศส่งถึงผู้ใช้ การ commit/push โค้ดเข้า main อย่างเดียวไม่เผยแพร่อัปเดต ต้อง push tag รุ่นด้วย

## ลายเซ็น Windows

ตัวติดตั้งที่สร้างครั้งนี้ยังไม่ได้เซ็น Authenticode การมีชื่อผู้พัฒนาในหน้าต่างติดตั้งหรือการตรวจ SHA-256 ไม่ได้แทนที่ใบรับรองผู้เผยแพร่ของ Windows

เมื่อเจ้าของมีใบรับรอง code-signing ที่เชื่อถือได้อยู่ใน Certificate Store สามารถใช้ `tools/Sign.ps1` เซ็น EXE โปรแกรม **ก่อน Package** แล้วเซ็น EXE ตัวติดตั้ง **หลัง Package** ได้ ตัวอย่าง:

```powershell
.\tools\Sign.ps1 -CertificateThumbprint '<thumbprint>' -Files '.\release\ServerBotTracker.exe'
.\tools\Package.ps1 -Notes 'รายละเอียดรุ่น'
.\tools\Sign.ps1 -CertificateThumbprint '<thumbprint>' -Files '.\release\publish\v1.6.0\ServerBotTracker-Setup.exe'
```

ให้แจกตัวติดตั้งจากโฟลเดอร์รุ่นที่เซ็นแล้ว และทดสอบแพ็กเกจใหม่เสมอ ไม่มีการสร้างหรือซื้อใบรับรองแทนเจ้าของในงานนี้

## แหล่งอ้างอิง

- [FxSound: หน้า Download และ release notes](https://www.fxsound.com/download)
- [Microsoft: แนวทางเผยแพร่แอป Windows](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-distribution-path)
- [GitHub: Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
