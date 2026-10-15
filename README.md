# Server Bot Tracker

A Thai Windows dashboard for checking multiple servers with ping. Add a name and IP/hostname, then read the green **ผ่าน** or red **ไม่ผ่าน** result. No service-port setup or report export.

## Run

Download the [Windows installer](https://github.com/kumatech112/Server_Bot_Tracker/releases/latest/download/ServerBotTracker-Setup.exe) or [portable ZIP](https://github.com/kumatech112/Server_Bot_Tracker/releases/latest/download/ServerBotTracker-portable.zip) from the [official GitHub Releases](https://github.com/kumatech112/Server_Bot_Tracker/releases/latest).

For a standard installation, open `release/ServerBotTracker-Setup.exe`. It installs for the current Windows user, adds a Start menu shortcut and a Windows uninstall entry. The portable option remains: double-click `release/ServerBotTracker.exe` and keep its `.exe.config` beside it. Requires Windows with .NET Framework 4.8 or newer. No Python, SDK, administrator rights or terminal is needed to run the app.

- Add, edit, delete or paste multiple server addresses.
- Check all entries or selected rows; search by name or host.
- See pass/fail, round-trip time and last-check timestamp.
- Enable automatic checking or stop an active cycle.
- Configure a 500–15,000 ms timeout and a 5–3,600 second interval.

One ICMP echo request is sent per server per cycle, using the same protocol as ordinary Windows ping. Automatic checking is off at launch. Enabling it checks immediately, then waits the configured interval after each cycle finishes. Cycles never overlap. Search filters the view; “check all” still checks the entire list.

## Project layout

```text
Server_Bot_Tracker/
  src/                 Complete application source
  assets/              Original product icons embedded during build
  docs/                Thai guide, data flow and verification
  tools/               Build and verification scripts
    checks/            Maintained verification source
  release/             Ready-to-run app; binaries are Git-ignored
  README.md
  .gitignore
  .gitattributes
  .editorconfig
```

Build staging is created outside the project and removed after compilation. Screenshots, verification binaries and fixtures stay at `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-*`. Git tracks source, docs and developer tools; it excludes generated files and user settings.

## Updates and notifications (1.2)

The public source and distribution repository is `kumatech112/Server_Bot_Tracker`, following the owner's decision to make it public. Stable GitHub Releases provide updates; the separate `announcements` pre-release provides the announcement feed. No GitHub credentials are embedded in the app.

- Background checks run at launch and every ten minutes.
- New versions download automatically; SHA-256, size, ZIP contents and executable version are verified.
- Installation waits until the app closes. “อัปเดตและเริ่มใหม่” installs immediately after closing and restarts the app; active automatic ping monitoring resumes.
- Existing program files are backed up and a failed replacement rolls back the files already changed.
- Public announcements appear once in a persistent inbox, with publish/expiry windows.
- Two consecutive failed ping cycles trigger one local down alert. The next successful check triggers one recovery alert. Continued failures do not repeat the warning within the session.
- Notifications are retained in `notifications.json` beside the user's settings, with a 100-item history. A damaged inbox falls back to memory without replacing the original.
- An unreachable release source never stops ping monitoring. Server lists and alert contents are not uploaded to the release channel.

Automatic downloads and local server alerts can be toggled in the Thai notification/update dialogs. Windows may suppress tray popups; inbox messages remain available. Version 1.1 users must install 1.2 once to gain the updater. The current installer is unsigned; Windows publisher signing is a separate owner-supplied certificate step.

Operator instructions: [Thai publishing guide](docs/RELEASES.th.md). A code commit by itself does not publish an update.

## Branding and guided setup (1.3)

The supplied robot icon is embedded in the application, setup and uninstall EXEs, and appears in the dashboard, window/taskbar icons, tray and Start menu shortcut. Dialogs use the same product icon. The existing light/blue colors and ping-only layout remain.

Setup now presents a branded welcome screen, installation progress, a completion screen with optional launch, and a visible retry state on failure. Installation runs on a dedicated STA thread so the window stays responsive; closing is blocked while files are being installed. This uses the existing per-user installation engine and needs no additional installer framework.

## Saved server lists

Names, addresses and preferences are stored per Windows user at `%LOCALAPPDATA%\Kuma\ServerBotTracker\servers.json`. Atomic save retains the preceding version as `servers.json.bak`. Corrupt or unsupported settings are reported and preserved.

Version 1.1 reads the previous version's saved lists without discarding IDs, names or addresses. Old port/method fields are ignored. Loading does not rewrite the file; the next normal save uses schema version 2 and backs up the original. Existing duplicate-host entries are preserved; bulk entry skips new duplicate hosts.

Copy the release folder to distribute the app. Transfer settings separately, with the app closed, if the destination should use the same list. Results are memory-only and start as “ยังไม่ตรวจ” after each launch.

## Build and verify

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Package.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verify.ps1
```

These commands change no machine execution-policy setting. The build uses Windows' installed Framework C# compiler and treats warnings as errors. Verification exercises a byte-identical copy of the release executable under a real GUI message loop, then launches its actual entry point.

## Result meaning

**ผ่าน** means an ICMP echo reply was received; **ไม่ผ่าน** means no successful reply was received, including DNS errors and timeouts. This dashboard measures ping reachability. A firewall blocking echo requests can produce a failed result even when a service is accessible.

DNS and ping each receive one timeout budget; hostname checks may take about twice the timeout, excluding queue wait. At most eight entries are checked concurrently. Hostnames use their first IPv4 address, or first returned address if no IPv4 is available. The chosen IP and detailed response appear in row tooltips/details.

Results represent the latest completed check and its timestamp. Stop preserves earlier results for unfinished entries.

See [Thai guide](docs/USAGE.th.md), [data flow and function index](docs/ARCHITECTURE.md), and [verification](docs/VERIFICATION.md).
