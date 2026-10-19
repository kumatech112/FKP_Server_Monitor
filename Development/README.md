# FKP Server Monitor

A Thai Windows dashboard with compact CCTV-style server cards showing names and status, optional SQL Server connection checks, and configurable Outlook/ntfy outage and recovery alerts. Basic entries still need only a name and IP/hostname. No report export or generic TCP-port setup.

## Run

Download the [Windows installer](https://github.com/kumatech112/FKP_Server_Monitor/releases/latest/download/FKPServerMonitor-Setup.exe) or [portable ZIP](https://github.com/kumatech112/FKP_Server_Monitor/releases/latest/download/FKPServerMonitor-portable.zip) from the [official GitHub Releases](https://github.com/kumatech112/FKP_Server_Monitor/releases/latest).

For a standard installation, open `Program/FKPServerMonitor-Setup.exe`. It defaults to Program Files, installs for everyone on the computer, and creates a common Start menu shortcut and Control Panel entry. Windows requests administrator approval when installation begins. The dashboard runs with normal user permissions. The portable option remains: double-click `Program/FKPServerMonitor.exe` and keep its `.exe.config` beside it. Requires Windows with .NET Framework 4.8 or newer. No Python, SDK or terminal is needed to run the app.

- Add, edit, delete or paste multiple server addresses.
- Check all entries or selected rows; search by name or host.
- See pass/fail, round-trip time and last-check timestamp.
- Enable automatic checking or stop an active cycle.
- Configure a 500–15,000 ms timeout and a 5–3,600 second interval.

One ICMP echo request is sent per server per cycle, using the same protocol as ordinary Windows ping. Automatic checking is off at launch. Enabling it checks immediately, then waits the configured interval after each cycle finishes. Cycles never overlap. Search filters the view; “check all” still checks the entire list.

## Compact dashboard and alerts (1.8.1)

Use the compact top bar to switch cards/list or open notification settings. Small cards show only the server name and normal/problem/disconnected state; full IP, SQL and response-time details remain in Listing. Widening the window fits additional columns. SQL monitoring is optional per server, with SQL username/password, encrypted connections and a read-only SELECT 1 check. Outlook uses Microsoft Graph delegated sign-in with an owner-provided public-client application registration; it does not reuse an Outlook desktop profile. ntfy uses a server, topic and optional access token. Both start disabled. Keep the app open and automatic monitoring enabled for continuous alerts. Passwords/tokens are DPAPI-protected to the current Windows profile; re-enter them on another machine/profile.

See [compact dashboard changes and verification](docs/COMPACT_DASHBOARD.md), [monitoring data flow](docs/DASHBOARD_1.8.md), [Thai account setup](docs/USAGE.th.md), and [1.8 verification](docs/DASHBOARD_1.8_VERIFICATION.md).

## Project layout

```text
FKP_Server_Monitor/
  FKP Server Monitor.lnk  Launch shortcut
  Program/               Current executable, runtime config, installer and Thai guide
  Development/           Complete source, assets, docs and developer tools
    src/
    assets/
    docs/
    tools/
      checks/
      launcher/
      setup/
  Archive/               Versioned packages, previous builds and verification evidence
  README.md              Short Thai entry guide
```

Temporary compilation stays in `Archive/builds` and is removed after compilation. Screenshots, verification binaries and fixtures stay in `Archive/verification`. Git tracks source, docs and developer tools; it excludes generated files and user settings.

## Updates and notifications (1.2)

The public source and distribution repository is `kumatech112/FKP_Server_Monitor`, following the owner's decision to make it public. Stable GitHub Releases provide updates; the separate `announcements` pre-release provides the announcement feed. No GitHub credentials are embedded in the app.

- Background checks run at launch and every ten minutes.
- New versions download automatically; SHA-256, size, ZIP contents and executable version are verified.
- Installation waits until the app closes. “อัปเดตและเริ่มใหม่” installs immediately after closing and restarts the app; active automatic ping monitoring resumes.
- Existing program files are backed up and a failed replacement rolls back the files already changed.
- Public announcements appear once in a persistent inbox, with publish/expiry windows.
- Two consecutive failed ping or database cycles trigger one separate health alert. Enabled Outlook/ntfy channels deliver these health events independently; accounts can be configured later. The next successful check triggers one recovery alert. Continued failures do not repeat the warning within the session.
- Notifications are retained in `notifications.json` beside the user's settings, with a 100-item history. A damaged inbox falls back to memory without replacing the original.
- An unreachable release source never stops ping monitoring. Server lists and alert contents are not uploaded to the release channel.

Automatic downloads and local server alerts can be toggled in the Thai notification/update dialogs. Windows may suppress tray popups; inbox messages remain available. Users of Server Bot Tracker 1.6 and earlier must run the renamed Setup once; subsequent updates use the FKP Server Monitor channel. The current installer is unsigned; Windows publisher signing is a separate owner-supplied certificate step.

Operator instructions: [Thai publishing guide](docs/RELEASES.th.md). A code commit by itself does not publish an update.

## Installation and removal (1.6)

The supplied robot icon is embedded in the application, setup and uninstall EXEs, and appears in the dashboard, window/taskbar icons, tray and Start menu shortcut. Dialogs use the same product icon. The dashboard uses dark cards and a top navigation bar; the classic light/blue setup and removal wizard retains its existing appearance.

Setup follows a classic wizard layout with blue robot artwork, white pages and standard Next/Back/Cancel buttons: welcome, **choose installation folder**, confirmation, progress and completion. Click **เรียกดู... (Browse)** and choose a drive or parent folder: Setup adds `FKP Server Monitor` and creates it during installation. Choosing D: gives `D:\FKP Server Monitor`. An existing product folder is retained without appending another copy. A typed full custom path is also supported; typing a drive root adds the product folder. Confirmation shows the final path before file writes. Setup remembers the machine installation path. A matching uninstall wizard opens from **Control Panel > Programs and Features > FKP Server Monitor > Uninstall/Change** or the installed **Uninstall.exe**. Both routes request administrator approval before changing a machine installation.

The default for a first install is `%ProgramFiles%\FKP Server Monitor`, normally `C:\Program Files\FKP Server Monitor`. Registration is in HKLM and the shortcut is in the common Start menu. After a successful machine install, the original user's older per-user registration, shortcut and known program files are removed using the reversible removal engine; unrelated files and settings remain. A legacy-cleanup obstruction is reported as a completion notice while retaining the old copy. A registration belonging to a different target is preserved during removal.

The removal engine checks all known program files and the owned shortcut, stages reversible file moves, and backs up registration values. A later failure restores moved files and metadata. Once removal has committed, a temporary-cleanup problem is a completion notice rather than a misleading uninstall failure. Server lists, settings and unrelated files remain. Error details can be scrolled and copied.

Installation and removal run on a dedicated STA worker so the wizard stays responsive and cannot close during file changes. The original user window launches a separate administrator helper for the protected operation. Install rollback restores files, shortcut and original registry values. Run Setup 1.6 once to migrate to the standard machine installation and current uninstaller; portable ZIP updates still replace only the app and runtime config.

Machine-installed updates download as before, then request Windows approval before replacing protected files. The administrator worker checks the registered target and rechecks the prepared digest/version/size against the live official manifest. The original user worker restarts the dashboard with normal permissions. Declined/failed updates retain the existing program and leave a local inbox message; a requested restart reopens that existing version. Updating a writable legacy per-user copy remains supported without migration.

Direct launch remains fixed: double-clicking the installed `Uninstall.exe` opens the uninstall wizard without command-line arguments. Control Panel's `--uninstall` command follows the same route. Earlier copies incorrectly entered installation mode when opened directly and could try to overwrite themselves. Run the current Setup to replace an older uninstaller; automatic app updates do not replace that file.

## Saved server lists

Names, addresses and preferences are stored per Windows user at `%LOCALAPPDATA%\Kuma\ServerBotTracker\servers.json`. Atomic save retains the preceding version as `servers.json.bak`. Corrupt or unsupported settings are reported and preserved.

Version 1.1 reads the previous version's saved lists without discarding IDs, names or addresses. Old port/method fields are ignored. Loading does not rewrite the file; the next normal save uses schema version 3 and backs up the original. Existing duplicate-host entries are preserved; bulk entry skips new duplicate hosts.

Copy the release folder to distribute the app. Transfer settings separately, with the app closed, if the destination should use the same list. Results are memory-only and start as “ยังไม่ตรวจ” after each launch.

## Build and verify

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Development\tools\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Development\tools\Package.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Development\tools\Verify.ps1
```

These commands change no machine execution-policy setting. The build uses Windows' installed Framework C# compiler and treats warnings as errors. Verification exercises a byte-identical copy of the release executable under a real GUI message loop, then launches its actual entry point.

## Result meaning

**ผ่าน** means an ICMP echo reply was received; **ไม่ผ่าน** means no successful reply was received, including DNS errors and timeouts. Ping measures ICMP reachability; optional SQL checks separately confirm login to the specified database and SELECT 1 execution. A disagreement produces a warning. Neither check proves complete application health. A firewall blocking echo requests can produce a failed result even when a service is accessible.

DNS and ping each receive one timeout budget; hostname checks may take about twice the timeout, excluding queue wait. At most eight entries are checked concurrently. Hostnames use their first IPv4 address, or first returned address if no IPv4 is available. The chosen IP and detailed response appear in row tooltips/details.

Results represent the latest completed check and its timestamp. Stop preserves earlier results for unfinished entries.

See [Thai guide](docs/USAGE.th.md), [data flow and function index](docs/ARCHITECTURE.md), and [verification](docs/VERIFICATION.md).

## Product rename (1.7)

The app, installer, executable, default installation folder, shortcuts and Control Panel display name are now FKP Server Monitor. Existing custom installation paths are remembered. Server lists and inbox history keep their existing storage location. The repository now uses FKP_Server_Monitor; internal compatibility identifiers and per-user data paths remain stable.

The new updater uses update-fkp.json. The older update.json feed remains pinned to the compatible 1.6.0 package. Install the new Setup once to migrate from the former name. See [rename details and changed functions](docs/RENAME.md).
