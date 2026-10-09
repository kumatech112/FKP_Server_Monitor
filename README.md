# Server Bot Tracker

A Thai Windows GUI for checking many servers without entering individual ping commands. Each saved entry supports ICMP, TCP, or both; use multiple entries to check multiple ports on the same host.

## Run

Double-click `release/ServerBotTracker.exe`. Keep its `.exe.config` alongside it. Requires Windows with .NET Framework 4.8 or newer. No administrator privileges, Python, .NET SDK, external NuGet packages, or console commands are needed to run it.

- Add, edit, delete, or paste multiple IP addresses / hostnames.
- Check all servers or selected rows; filter by name or host.
- Set a 500–15,000 ms timeout and a 5–3,600 second automatic interval.
- See separate ICMP/TCP results, elapsed times, the resolved address, and check timestamps.
- Stop an active cycle, or close the window safely while checks run.
- Export the latest results for all entries to a Thai UTF-8 CSV.

Automatic checking is off on every launch. Enabling it checks immediately, then waits the configured interval **after a cycle finishes**. Cycles do not overlap. Search only changes what is visible: “check all” and CSV export still include the entire list.

## Keep the project clean

```text
Server_Bot_Tracker/
  src/                 Application source and Windows manifest
  docs/                Thai user guide, architecture and verification record
  tools/               Build and verification scripts
    checks/            Maintained verification source
  release/             Ready-to-run EXE and runtime config (Git-ignored)
  README.md
  .gitignore
  .editorconfig
```

Only source, documentation, scripts, and the release guide belong in Git. Tests are maintained under `tools/checks`; removing them would make later changes harder to verify. Build staging is created outside the project and removed after each build. Screenshots, test fixtures, and verification reports stay outside the project at `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-*`.

User server lists and preferences live at `%LOCALAPPDATA%\Kuma\ServerBotTracker\servers.json`; `.bak` contains the preceding saved version. Saving uses an atomic replacement. Unreadable or corrupt existing data produces a Thai startup error and is preserved. Close the app before copying / restoring a settings file.

The release folder is a copyable application folder, not an installer and not a backup of user settings. Git can reproduce the source and build; the ignored release binaries must be built or distributed separately.

## Build and verify

From the project folder on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verify.ps1
```

The execution-policy option applies only to these PowerShell processes. The machine's execution policy is not changed. The build uses Windows' installed Framework C# compiler, treats compiler warnings as errors, and copies only the executable and runtime config to `release`. Verification loads a byte-identical copy of the release executable, exercises real loopback networking and GUI workflows, records screenshots, and launches the real entry point.

## Reading results

ICMP success means the selected IP responds to an echo request. TCP success means a TCP connection to the selected IP and port completed. Neither alone confirms application health. Blocked ICMP, a firewall, DNS failure, a closed port, or a timeout can cause failed checks; the app does not claim the server is powered off.

DNS resolution gets one timeout budget; the ICMP and TCP checks then run concurrently with their own budgets. A hostname check can therefore take up to about twice the configured timeout, excluding queue wait. At most eight server entries are active per cycle. A hostname uses its first IPv4 address if available, otherwise its first returned address; this is displayed in row tooltips/details. The app does not probe every address of a load-balanced hostname.

Results are the latest **completed** checks, with timestamps. Stop preserves earlier results for unfinished entries. CSV exports the current snapshot, including older results and untested entries, rather than a historical log. Results themselves are not saved between launches.

See [Thai user guide](docs/USAGE.th.md), [complete data flow and function index](docs/ARCHITECTURE.md), and [delivery verification](docs/VERIFICATION.md).
