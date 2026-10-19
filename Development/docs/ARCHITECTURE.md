# Code review, data flow and function index

The current repository uses Program, Development and Archive. See [folder layout and build data flow](FOLDER_LAYOUT.md) for source, packaging, local verification and archived-evidence paths. Production C# functions are unchanged by the folder reorganization.

## Current 1.8 extension

The owner has now explicitly extended the original ping-only scope with a dark card dashboard, optional independent SQL Server authentication checks, and configurable Outlook/ntfy health alerts. The complete current flow and file/function map are in [DASHBOARD_1.8.md](DASHBOARD_1.8.md). The following sections describe the retained baseline; references to the initial ping-only contract apply to versions before 1.8. Schema 3 now reads versions 1/2 in memory and stores optional SQL settings. The installer, updater, elevation and removal architecture below remains in use.

## Initial inspection and scope

The requested workspace was empty on 2026-10-09 and was not inside a Git repository. There was no existing application code, database, configuration, or packaging to refactor. This delivery creates an isolated Windows Forms application. It does not change other projects, install services, edit firewalls, or invoke command-line ping. Reachability checking still uses DNS and ICMP echo only. Version 1.2 adds separate HTTPS connections to a public GitHub release channel for program updates and announcements. Port/method configuration, TCP connections, partial-pass status and report export were removed at the user's request.

The app uses C# and the Windows .NET Framework 4.8 libraries. No package manager or third-party runtime dependencies are introduced. The source tree contains complete readable source; no generated/minified implementation is tracked.

## Data flow

```mermaid
flowchart TD
    Entry[Program.Main: instance guard] --> Load[ConfigurationStore.Load]
    Load --> Legacy[Read prior schema in memory; preserve existing file]
    Legacy --> Window[MainForm: Thai ping dashboard]
    Window --> Edit[ServerDialog / BulkDialog: name and host only]
    Edit --> Validate[Validate entry and configuration]
    Validate --> Save[Atomic JSON save and prior-version backup]
    Save --> Window
    Window --> Trigger[All / selected rows / automatic timer]
    Trigger --> Queue[Snapshot entries; maximum 8 active checks]
    Queue --> DNS[Resolve one IP with bounded wait]
    DNS --> Ping[One asynchronous ICMP echo]
    Ping --> Result[Pass/fail, latency and timestamp]
    Result --> UI[Update dashboard on UI thread]
    UI --> Timer[Wait interval after cycle finishes]
    Timer --> Trigger
    Window --> Cancel[Stop / close: cancel current cycle]
    Cancel --> Queue
    Cancel --> DNS
    Cancel --> Ping
```

### Startup and persistence

`Program.Main` initializes Windows Forms and takes a session-local, per-user mutex. A second instance in the same Windows session displays a Thai message. `MainForm` loads the per-user JSON file through an injected `ConfigurationStore`; a first launch returns default settings without creating a data file.

Both loading and saving validate schema version, names, hosts, IDs and settings ranges. Loading a version-1 file promotes its schema to version 2 in memory; the serializer ignores the obsolete port/method fields. It preserves every entry, including duplicate hosts, and does not rewrite the original file. The next normal save backs up the previous file and writes only the current fields. A failed load stops startup and reports the existing file location. A save writes a unique temporary file in the same data directory, flushes it, then renames/replaces it atomically. Replacement retains the previous file as `.bak`. `MainForm.CommitConfiguration` updates its in-memory configuration only after persistence succeeds. Server edits invalidate their old check result; deletion removes matching result records.

The mutex is session-local: do not edit one user's settings concurrently from multiple Windows sessions or external tools. Server lists have no cross-machine synchronization or shared central database. Public release and announcement feeds are separate from server-list storage.

### Check scheduling and threading

`MainForm.RunChecksAsync` snapshots the chosen entries and timeout, stops the timer, and disables mutation controls. A semaphore permits at most eight active server entries. DNS gets its own timeout; a successful resolution selects the first IPv4 address or first available address. One ICMP echo request is sent to the selected IP per entry per cycle. A successful echo reply produces a passed result; DNS errors, timeout and other unsuccessful replies produce a failed result. DNS and echo waits have separate timeout budgets.

`NetworkProbe` uses `ConfigureAwait(false)` internally. `MainForm` awaits the probe on the Windows Forms synchronization context, so grid, counter, and dictionary updates occur on the UI thread. Search can safely rebuild visible rows while a check is running: completed results are stored by stable entry ID and applied only to currently visible matching rows.

`WithinAsync` races the operation against a linked cancellable delay. Ping resources are disposed after completion, timeout or cancellation. Late DNS/ping faults are explicitly observed; Framework DNS itself cannot be forcibly cancelled, so a timed-out DNS operation may finish in the background. Its result is discarded and never applied after cancellation.

Cancellation preserves completed results and previous results for unfinished entries. Closing marks the window as closing before cancelling; subsequent continuations skip UI work. The next automatic cycle starts only after all tasks in the current cycle settle and the configured delay elapses. Turning off automatic mode stops future cycles; the Stop button also cancels the current cycle.

### Dashboard result meaning

The results dictionary is memory-only and keyed by entry ID. Rows display the latest completed ping status, response duration and completion timestamp. Untested entries have an explicit status. A missing ping result cannot be classified as passed. There is no partial-pass state, report writer, export workflow or historical result storage.

Results describe ICMP echo reachability. A firewall dropping echo packets can produce a failed check. Multiple hostname DNS records are not exhaustively checked. The actual chosen IP and detailed ping response appear in row detail/tooltip.


## Update and notification data flow (1.2)

```mermaid
flowchart TD
    Window[Dashboard starts] --> Poll[Independent 10-minute release timer]
    Poll --> HTTPS[GitHub HTTPS release files]
    HTTPS --> Manifest[Validate update-fkp.json and compare versions]
    Manifest --> Download[Download bounded ZIP to per-user staging]
    Download --> Check[Verify SHA-256, size, exact file names and binary version]
    Check --> Ready[Notify update ready; continue monitoring]
    Ready --> Exit[Normal close or Update and restart]
    Exit --> Worker[Copied worker waits for original process exit]
    Worker --> Backup[Back up only the two program files]
    Backup --> Replace[Replace from sibling staging on same volume]
    Replace --> Success[Optional restart and resume automatic ping]
    Replace --> Failure[Restore already-replaced files on error]
    HTTPS --> Feed[Validate announcement dates and IDs]
    Feed --> Inbox[Persistent local inbox; suppress duplicate IDs]
    Ping[Completed ping result] --> Tracker[Two consecutive failures or recovery]
    Tracker --> Inbox
    Inbox --> Popup[Optional Windows tray popup]
```

The official public source and release repository is compiled as `kumatech112/FKP_Server_Monitor`, following the owner's visibility change. Public URLs are fixed to stable release assets, not arbitrary commands or publisher-supplied executable URLs. HTTPS redirects are limited to GitHub's known release hosts, with a five-redirect limit, a 45-second whole-transfer cancellation deadline and byte limits. Cancellation disposes an active response body so stalled streams cannot hold the check open. Missing releases and invalid responses never count as an up-to-date result.

`PrepareAsync` rejects the current/older version, downloads into a unique user-data directory, verifies a manifest hash and size, and validates an exact two-file ZIP before inspecting the PE version/product name. ZIP paths are an allowlist, preventing nested paths, traversal, extra entries and duplicate names. Payloads must match the declared version. DataContract JSON accepts UTF-8 with or without a BOM so the Windows PowerShell publisher interoperates correctly.

A worker copy runs from staging before the main process closes. It uses a job with parent process ID/start time and waits up to 60 seconds for exit; it does not kill a process. It independently revalidates the package before installation. Replacement files are copied beside the target first so `File.Replace` stays on one volume, including when the portable target is a network share. Backups live in the user's update staging directory. If a later replacement fails, each already-changed program file is restored. Only the EXE and runtime config are replaced; server data and unrelated files are untouched. Results are recorded as `updates/<id>/result.json`. Optional Windows uninstall-version metadata is updated only when its install path matches the target.

A normal close applies a prepared update only when automatic updates remain enabled. The explicit restart action may also install it. Windows shutdown and Task Manager closure skip worker launch. Restart resumes automatic monitoring only if it was active before the update. Closing also cancels HTTP polling. There is no forced mid-session restart and no repeated network request while another release check is active.

Notification state is independent of `servers.json`: automatic-update preference, local-alert preference, last 100 messages, read state, and up to 1,000 seen IDs are persisted in `notifications.json`. A corrupt inbox is preserved read-only while the app uses memory-only state. Each server's alert baseline is session-local: two consecutive failed completed checks trigger down once, a later successful check triggers recovery once, and edits/deletions clear the baseline. Turning alert preferences on/off resets baselines. Announcement IDs suppress repeat notices across launches; published/expiry times are checked in UTC. Text is plain text, with no HTML, embedded actions or remote code execution. All mutations occur on the UI thread.

The native Windows Forms installer embeds the two-file ZIP and compiles separated sources under `Development/tools/setup`. Version 1.6 defaults to Program Files and installs for all users through an administrator helper, a common Start menu shortcut and HKLM uninstall registration. The GUI and dashboard remain asInvoker. Earlier per-user installations are migrated after machine installation succeeds. Settings remain per-user. No third-party build-tool dependency was added.

## Standard machine installation and elevation (1.6)

The owner chose Program Files and all-users installation. InstallationScope reads the remembered HKLM target or defaults to `%ProgramFiles%\FKP Server Monitor`. Browse accepts a drive/parent and creates a product subfolder path; typed full custom paths remain supported. Confirmation displays the exact destination. The ordinary user wizard stays responsive while its STA worker requests `runas` through ElevationBroker. Setup is copied to a unique local temporary job directory before elevation, so an elevated account does not need access to a network/mapped-drive source. Only the protected operation runs elevated; optional app launch remains in the original user process.

`InstallMachine` requires an administrator token and calls `InstallAtScope` with HKLM and CommonPrograms. The retained `InstallAt` wrapper selects HKCU for isolated verification. Shared file preflight checks every existing product file/shortcut before replacement. RegistryBackup captures original registry values and kinds; file backups and original shortcut bytes allow rollback of a failed install. Failed recovery retains the staging directory and identifies it. Cleanup-only failures are completion notices. Successful machine installation then removes the original user's legacy HKCU registration, shortcut and known files through the reversible removal engine; unrelated legacy data remains. A legacy cleanup failure does not turn the completed machine install into a failure.

Both direct Uninstall.exe and Control Panel launch the same temporary wizard. Remove chooses HKLM/common shortcut for a machine installation and HKCU/user shortcut for a recognized legacy installation. The original user's running-app mutex is checked before asking for elevation, and the administrator engine checks again. UninstallTransaction handles file preflight, reversible moves, registry/shortcut rollback and cleanup notices. There is no recursive deletion of product/user folders.

ElevationBroker creates a unique result directory, opens a new result file before any administrator operation, verifies the token, and records success/error. Existing result files and reparse job directories are refused. Process exit code remains authoritative after a committed operation even if writing/reading extra completion details fails. UAC cancellation is an error before product mutation. Temporary helper files stay outside the repository and cleanup is best effort.

Program Files and registered machine updates require elevation. The normal updater waits for the parent, asks for administrator approval, and retains responsibility for restarting the app with the original user token. The administrator worker checks its stage/job identity, checks that the target is registered, downloads the live official manifest and compares version/tag/hash/size with the prepared package before applying it. File replacement/rollback stays limited to the original two runtime files. Optional display-version updates cover matching HKCU and HKLM records and cannot fail an already committed binary update. Failed/declined updates leave the old program and a local inbox notice; an explicitly requested restart reopens it. No always-elevated dashboard or background service was introduced.

The detailed 1.2–1.5 discussion below records the earlier design; the 1.6 scope and privilege behavior above supersede its per-user/default-location statements.

## Product branding and classic wizard flow (1.4)

The owner supplied `server-tracker.ico` and a 512-pixel PNG. Unmodified copies live under `Development/assets/branding`; both are embedded into both EXEs. The ICO is also passed to the compiler as `/win32icon`. `Branding` loads resources from its own assembly, allowing the same readable source to compile into the app and setup. The app sets its window/taskbar icon, adds a header logo, assigns the tray icon, and gives dialogs the product icon through `Theme.ApplyDialog`. Owned icon/image instances are disposed with their window or control. Start menu shortcuts explicitly use the application EXE's icon, and the uninstall copy inherits the setup icon. The portable ZIP remains exactly two files.

The dashboard retains its existing light/blue palette. Setup uses the owner's OBS-style reference: a navy artwork panel, white pages and native navigation buttons. Installation states are welcome -> destination -> confirmation -> working -> complete; uninstall skips destination. Failure allows retry or Back to the appropriate review page. Next only validates/advances and performs no file operation. Operation execution uses a dedicated STA worker and prevents close/reentry while files change. Launch is offered only after installation, never after removal. No OBS artwork or implementation was copied.

Control Panel registration includes the quoted uninstall command, version, publisher, icon, NoModify/NoRepair, installation date and estimated size. Its `--uninstall` entry copies the EXE to Temp and launches `--uninstall-wizard` with the launcher's PID, start time and normalized original EXE directory. The helper waits for that specific launcher to exit before showing the wizard, so the installed Uninstall.exe can later be removed. No files are removed until confirmation. `RemoveInstallation` owns the product mutex, refuses volume/share roots and dispatches to UninstallTransaction. The shortcut and registry key are removed only if InstallLocation still matches that target. Its mutex is released on success/failure. User data and unrelated files/directories remain. Tests invoke the same private engines with isolated file and registry paths.

### Selected installation folder (1.5)

Current Setup reads the machine directory through InstallationScope; InstallLocation.ReadRemembered remains available for legacy/isolated HKCU checks. The destination page accepts typed absolute paths or a native folder selection. In 1.6, `ForDestination` turns a typed drive/share root into a ServerBotTracker subfolder, while `ForBrowseSelection` appends that folder to a parent selection and retains an existing product folder. `Normalize` still rejects direct root targets, blank, relative, device and invalid paths at the file-engine boundary. `ValidateProductFiles` prevents overwriting unrelated colliding files. Confirmation shows the resolved destination; advancing creates no directories or write probes.

Confirmation freezes the normalized folder passed to `Action<string>`. `Install` delegates to `InstallAt`, which takes the product mutex, revalidates ownership, creates same-folder staging, backs up all three owned files, extracts the exact two-file payload, copies the uninstaller, and creates the shortcut/Control Panel entry using the same target. Staging cleanup and mutex release also run after directory creation errors. Completion launches the selected EXE with its folder as WorkingDirectory. Choosing a different folder leaves the previous copy in place and redirects the single product registration; no recursive move/delete is performed. Version 1.5 uninstallers preserve registration belonging to another folder; legacy uninstallers retain their older behavior.

The updater already takes the running application's actual base directory and uses same-volume replacement; no updater implementation change is required. User settings stay at the original LocalAppData data path. Setup runs as the current user and requires a writable destination; it does not automatically elevate for protected folders. The portable archive remains exactly two files, preserving compatibility with the original 1.2 updater.

`DesktopShortcut.Create` uses native Unicode `IShellLinkW` and `IPersistFile`, with explicit path, working directory, description and icon and guaranteed COM release. This replaces the WScript.Shell dynamic shortcut call after the English Windows CI runner rejected shortcut creation during real custom-folder installation. The verification runner compiles the same interop declarations, then independently loads the saved .lnk through IPersistFile and reads its target, working directory and icon. Thai paths are retained without depending on the machine's legacy script-host code page.

### Direct uninstaller launch correction (1.5.2)

`Setup.Main` now treats zero arguments and the executable basename `Uninstall.exe` (case-insensitive) as an uninstall launch. It uses the same temporary helper, original-directory handoff and confirmation wizard as the explicit `--uninstall` route. Previously only that flag selected uninstall mode; double-clicking the installed copy incorrectly opened Setup and could attempt to overwrite its own running file. The regular FKPServerMonitor-Setup.exe welcome/destination flow, silent install, mutex guard, removal engine and user data paths remain unchanged.

`SetupVerification.VerifyUninstallEntryPoint` copies the real setup to Uninstall.exe inside an isolated Thai folder with spaces. It launches that EXE both without arguments and with the Control Panel flag, identifies only its child helper through a parent-PID WMI query and matching target argument, verifies the actual uninstall window title, then closes the welcome screen and checks clean exit and unchanged fixture files. No removal is confirmed against the user's installation. System.Management is used only by developer verification, not the shipped app/setup.

### Reversible uninstall and accurate completion (1.6)

The old engine deleted EXE, config, uninstaller, shortcut and registration sequentially. A later locked file or permission error could leave earlier items removed despite a failure page. UninstallTransaction captures matching registration values, rejects unknown registry subkeys, and holds exclusive read/write handles for all existing known files and the owned shortcut during preflight. A directory colliding with an owned file name is rejected before any move.

After preflight it creates a uniquely named .uninstall staging folder inside the target, copies the shortcut backup, and moves the three known files into same-volume staging. It then removes the owned shortcut and registration. An error before commit restores registry values/kinds, shortcut and moved files in reverse order. If restoration itself fails, backups are retained and the error identifies the recovery folder; the UI does not claim that nothing changed.

After commit, staging cleanup and deletion of an empty product directory are best effort. An I/O/access failure returns a completion notice instead of throwing a false removal error. Setup passes this notice through an injected Func<string> to SetupForm; normal completion and actual failure remain separate states. Status uses a read-only text box so full error/cleanup details can be scrolled and copied. Settings and unknown files/directories are never included in staging or recursively deleted.

Verification locks the second product file, the uninstaller and shortcut separately, then retries. A deny-delete ACL on the second file deliberately fails after an earlier move and verifies rollback. Another isolated ACL permits moves but prevents post-commit staging cleanup and verifies a completion notice with registration already removed. Fixture ACLs are explicitly restored, and no production ACL is changed. This is an in-process rollback mechanism, not a filesystem/registry transaction surviving a forced process termination or power loss.

The app updater still validates and replaces exactly two program files. It does not replace Uninstall.exe, so upgrading the installer UI requires running the new Setup once. Optional Control Panel version updates now compare normalized install paths, allowing trailing directory separators without matching a different installation. Ping behavior, settings schema and the dashboard layout are unchanged.

`Development/tools/Package.ps1` creates the ZIP, matching manifest and setup under the ignored release tree. `Prepare-Announcement.ps1` prepares text locally and sends nothing. The GitHub workflow builds and verifies the package, then publishes three assets in the same public repository when a matching version tag is pushed. A manual run creates a draft. It uses GitHub's built-in workflow token with Contents write permission; clients receive no credential. The workflow initializes an empty announcement feed if needed. Compiler input is explicitly UTF-8 to preserve Thai on English Windows runners. Windows Authenticode signing is optional owner infrastructure via `Development/tools/Sign.ps1`; HTTPS/SHA-256 validation is not a Windows publisher certificate.

## Function index

All current application functions are listed below. Methods with the same name belong to the explicitly identified class. Core dashboard behavior remains in `MainForm.cs`; update/notification UI is isolated in the partial class `MainForm.Notifications.cs`.

| File / class | Function | Responsibility |
| --- | --- | --- |
| `Development/src/Program.cs` / Program | `Main` | Route update-worker mode, initialize GUI, guard duplicate launch and resume monitoring after an update |
| `Development/src/Models.cs` / ServerEntry | constructor | Initialize stable ID and entry defaults |
| `Development/src/Models.cs` / ServerEntry | `Copy` | Create independent editable / check snapshot |
| `Development/src/Models.cs` / ServerEntry | `Validate` | Validate name and host |
| `Development/src/Models.cs` / AppConfiguration | constructor | Initialize schema, empty list and settings defaults |
| `Development/src/Models.cs` / AppConfiguration | `Copy` | Deep-copy entries before mutation |
| `Development/src/Models.cs` / AppConfiguration | `Validate` | Validate schema/settings/entries and reject duplicate IDs |
| `Development/src/Models.cs` / ServerResult | `Summary` getter | Compute ping pass/fail or untested status |
| `Development/src/ConfigurationStore.cs` / ConfigurationStore | constructor | Normalize an explicit storage file path |
| `Development/src/ConfigurationStore.cs` / ConfigurationStore | `ForCurrentUser` | Resolve the per-user LocalAppData data path |
| `Development/src/ConfigurationStore.cs` / ConfigurationStore | `Load` | Read validated data, adapt schema 1 in memory and preserve unreadable files |
| `Development/src/ConfigurationStore.cs` / ConfigurationStore | `Save` | Validate and atomically persist with backup and temporary-file cleanup |
| `Development/src/NetworkProbe.cs` / NetworkProbe | `CheckAsync` | Resolve address, send ping, assemble completed result |
| `Development/src/NetworkProbe.cs` / NetworkProbe | `CheckPingAsync` | ICMP echo, latency and timeout/error translation |
| `Development/src/NetworkProbe.cs` / NetworkProbe | `Failure` | Create a failed result with an explicit message |
| `Development/src/NetworkProbe.cs` / NetworkProbe | `WithinAsync` | Bound waits, prioritize cancellation, and observe abandoned faults |
| `Development/src/NetworkProbe.cs` / NetworkProbe | `ObserveFailure` | Consume a late task exception without UI side effects |
| `Development/src/Theme.cs` / Theme | `Button` | Consistent button colors, dimensions and padding |
| `Development/src/Theme.cs` / Theme | `Label` | Consistent label styling |
| `Development/src/Theme.cs` / Theme | `ApplyDialog` | Apply product icon, Thai font, DPI behavior and modal window styling |
| `Development/src/ServerDialog.cs` / ServerDialog | constructor | Build/populate single-entry editor |
| `Development/src/ServerDialog.cs` / ServerDialog | `SaveEntry` | Validate editor input and return an independent entry |
| `Development/src/ServerDialog.cs` / BulkDialog | constructor | Build multiline entry dialog |
| `Development/src/ServerDialog.cs` / BulkDialog | `AddEntries` | Parse one host per line, validate all and deduplicate lines |
| `Development/src/MainForm.cs` / MainForm | constructor | Load data, initialize window and attach lifecycle/timer handlers |
| `Development/src/MainForm.cs` / MainForm | `BuildLayout` | Arrange branded heading, totals, tools, grid and status |
| `Development/src/MainForm.cs` / MainForm | `BuildToolbar` | Bind CRUD, checking and Stop actions |
| `Development/src/MainForm.cs` / MainForm | `AddEditButton` | Register buttons disabled during checks |
| `Development/src/MainForm.cs` / MainForm | `BuildSettings` | Wire filter, timeout, interval and automatic checking |
| `Development/src/MainForm.cs` / MainForm | `BuildGrid` | Define result table styling and interactions |
| `Development/src/MainForm.cs` / MainForm | `AddColumn` | Define a named sortable read-only result column |
| `Development/src/MainForm.cs` / MainForm | `RenderRows` | Filter/rebuild visible rows, restore selection and apply saved results |
| `Development/src/MainForm.cs` / MainForm | `SelectedIds` | Map row selection to stable entry IDs |
| `Development/src/MainForm.cs` / MainForm | `UpdateRow` | Render ping status, latency, color, timestamp and tooltip |
| `Development/src/MainForm.cs` / MainForm | `ProbeText` | Format probe message and optional duration |
| `Development/src/MainForm.cs` / MainForm | `UpdateTotals` | Count latest completed status and visible/untested entries |
| `Development/src/MainForm.cs` / MainForm | `ShowSelectedDetails` | Display resolved IP and complete ping message |
| `Development/src/MainForm.cs` / MainForm | `RunChecksAsync` | Snapshot, schedule, cancel, collect and complete one bounded check cycle |
| `Development/src/MainForm.cs` / MainForm | `SetBusy` | Toggle checking and mutation controls |
| `Development/src/MainForm.cs` / MainForm | `CommitConfiguration` | Persist before changing in-memory configuration |
| `Development/src/MainForm.cs` / MainForm | `AddServer` | Add a validated entry through the editor |
| `Development/src/MainForm.cs` / MainForm | `AddMany` | Add validated bulk entries and skip existing hosts |
| `Development/src/MainForm.cs` / MainForm | `EditServer` | Replace one selected entry and invalidate its old result |
| `Development/src/MainForm.cs` / MainForm | `DeleteServers` | Confirm and persist deletion of selected entries |
| `Development/src/MainForm.cs` / MainForm | `SaveSettings` | Persist interval/timeout, revert controls on failure and reschedule timer |
| `Development/src/MainForm.cs` / MainForm | `OnClosing` | Stop scheduling, cancel current cycle and suppress later UI updates |

`Development/src/AssemblyInfo.cs` supplies version and Framework metadata; `Development/src/app.manifest` declares user-level execution and DPI awareness. `Development/tools/FKPServerMonitor.exe.config` is the tracked source for the release runtime config.


### Added update, notification and installer functions

| File / class | Function | Responsibility |
| --- | --- | --- |
| `Development/src/MainForm.Notifications.cs / MainForm` | `InitializeNotifications` | Load independent inbox state, wire timer, tray, lifecycle and preferences |
| `Development/src/MainForm.Notifications.cs / MainForm` | `BuildNotificationButtons` | Add notification and update actions to the heading |
| `Development/src/MainForm.Notifications.cs / MainForm` | `RefreshNotificationCount` | Display unread count |
| `Development/src/MainForm.Notifications.cs / MainForm` | `ResumeMonitoring` | Resume automatic checks after updater-requested restart |
| `Development/src/MainForm.Notifications.cs / MainForm` | `AddNotification` | Persist/de-duplicate notice and optionally display tray balloon |
| `Development/src/MainForm.Notifications.cs / MainForm` | `PublishServerAlert` | Translate confirmed ping transitions into local notices |
| `Development/src/MainForm.Notifications.cs / MainForm` | `CheckReleaseAsync` | Poll update and announcement feeds without overlapping checks |
| `Development/src/MainForm.Notifications.cs / MainForm` | `ShowNotifications` | Present inbox, read-all action and local alert preference |
| `Development/src/MainForm.Notifications.cs / MainForm` | `ShowUpdateDetails` | Present version, notes, automatic-update preference and restart action |
| `Development/src/MainForm.Notifications.cs / MainForm` | `ScheduleUpdateOnExit` | Cancel polling and launch a prepared worker when allowed |
| `Development/src/Notifications.cs / NotificationState` | `constructor` | Initialize preferences and empty notification lists |
| `Development/src/Notifications.cs / NotificationCenter` | `constructor` | Load independent local state or preserve unreadable data |
| `Development/src/Notifications.cs / NotificationCenter` | `Add` | Bound history, suppress duplicate IDs and save |
| `Development/src/Notifications.cs / NotificationCenter` | `MarkAllRead` | Persist read state |
| `Development/src/Notifications.cs / NotificationCenter` | `Save` | Persist atomically and raise UI change event |
| `Development/src/Notifications.cs / ServerAlertTracker` | `Observe` | Track independent consecutive failures and recovery |
| `Development/src/Notifications.cs / ServerAlertTracker` | `Forget` | Reset state for an edited/deleted server or preference change |
| `Development/src/Notifications.cs / JsonData` | `Read` | Read size-bounded local JSON |
| `Development/src/Notifications.cs / JsonData` | `Deserialize` | Read seekable UTF-8 JSON with optional BOM |
| `Development/src/Notifications.cs / JsonData` | `WriteAtomic` | Flush temporary JSON, replace and retain backup |
| `Development/src/ReleaseChannel.cs / ReleaseManifest` | `Validate` | Check schema, version/tag, digest, size and notes |
| `Development/src/ReleaseChannel.cs / AnnouncementFeed` | `Validate` | Check item bounds, unique IDs, text and date ranges |
| `Development/src/ReleaseChannel.cs / Announcement` | `PublishedTime getter` | Normalize the publish timestamp to UTC |
| `Development/src/ReleaseChannel.cs / Announcement` | `ExpiresTime getter` | Normalize the expiry timestamp to UTC |
| `Development/src/ReleaseChannel.cs / Announcement` | `ParseUtc` | Validate ISO-compatible timestamp data |
| `Development/src/ReleaseChannel.cs / Announcement` | `IsActive` | Apply publish/expiry window |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `ReleaseChannel()` | Bind the official repository and native HTTP handler |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `ReleaseChannel(string, HttpMessageHandler)` | Bind an isolated channel/handler for verification without credentials |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `CurrentVersion getter` | Read the application's assembly version |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `ParseVersion` | Normalize three/four-part versions for comparisons |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `GetLatestAsync` | Read and validate stable update manifest |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `GetAnnouncementsAsync` | Read and validate independent announcement feed |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `ReadJsonAsync` | Download bounded JSON and deserialize |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `PrepareAsync` | Download and validate a newer package in local staging |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `DownloadAsync` | Enforce HTTPS hosts, redirects, timeout and byte limits |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `VerifyPackage` | Check SHA-256 and exact byte size |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `ExtractPackage` | Allowlist payload paths and check executable identity/version |
| `Development/src/ReleaseChannel.cs / ReleaseChannel` | `Dispose` | Release HTTP resources |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `LaunchWorker` | Copy worker and persist explicit installation job |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `RunWorker` | Wait for parent, apply update, record result and optionally restart |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `Apply` | Revalidate, back up, replace, rollback and update matching Windows version metadata |
| `Development/tools/setup/Setup.cs / Setup` | `Target getter` | Read the remembered machine installation folder or Program Files default |
| `Development/tools/setup/Setup.cs / Setup` | `Shortcut getter` | Resolve the legacy per-user Start menu shortcut |
| `Development/tools/setup/Setup.cs / Setup` | `Main` | Detect direct Uninstall.exe launch and route Thai setup UI, silent install and uninstall helper |
| `Development/tools/setup/Setup.cs / Setup` | `AcquireInstance` | Coordinate with the dashboard's per-user mutex |
| `Development/tools/setup/Setup.cs / Setup` | `Install` | Request machine installation, then migrate the original user legacy copy |
| `Development/tools/setup/Setup.cs / Setup` | `InstallAt` | Validate and install payload, uninstaller, shortcut and registry metadata at an explicit target |
| `Development/tools/setup/Setup.cs / Setup` | `Remove` | Remove only known installed files and product registration |

### Branding and guided installer functions

| File / class | Function | Responsibility |
| --- | --- | --- |
| `Development/src/Branding.cs / Branding` | `CreateIcon` | Load and clone the embedded multi-size ICO without retaining a resource stream |
| `Development/src/Branding.cs / Branding` | `ApplyTo` | Apply the product icon to a form and dispose it with the form |
| `Development/src/Branding.cs / Branding` | `CreateLogoBox` | Create a zoomed embedded PNG control and dispose its image with the control |
| `Development/src/MainForm.cs / MainForm` | `MainForm(ConfigurationStore, bool)` | Apply the app icon before building the unchanged dashboard workflow |
| `Development/src/MainForm.Notifications.cs / MainForm` | `InitializeNotifications` | Assign the product tray icon and dispose it when monitoring closes |
| `Development/tools/setup/SetupForm.cs / SetupForm` | constructor | Apply product styling and bind installation, close and keyboard actions |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `BuildLayout` | Compose branded welcome, destination, progress, completion and action areas |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `RunPrimaryAction` | Handle installation, visible failure/retry, completion and optional app launch |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `RunOperationAsync` | Run install or removal on one STA worker and propagate its result |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `PreventCloseDuringOperation` | Prevent user closure while installing or removing files |
| `Development/tools/setup/Setup.cs / Setup` | `Main` | Route interactive setup through the separated SetupForm while preserving silent/uninstall routes |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `DefaultDirectory getter` | Resolve the standard Program Files product folder |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `Normalize` | Validate absolute paths, reject roots/invalid segments and remove trailing separators |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `ReadRemembered` | Read the registered folder with a safe default for absent/invalid/inaccessible metadata |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `ForDestination` | Resolve a typed drive/share root to a product subfolder while preserving full custom paths |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `ForBrowseSelection` | Add the product folder to a selected parent or keep an existing product destination |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `Matches` | Compare normalized folders for registration ownership |
| `Development/tools/setup/InstallLocation.cs / InstallLocation` | `ValidateProductFiles` | Reject unrelated files with product-owned names before writes |
| `Development/tools/setup/DesktopShortcut.cs / DesktopShortcut` | `Create` | Save a native Unicode Windows shortcut and always release its COM object |
| `Development/tools/setup/DesktopShortcut.cs / IShellLinkW` | COM interface methods | Declare the complete native vtable for Unicode shortcut creation and verification |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `GoBack` | Return to editable destination or welcome while preserving the path |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `BrowseDirectory` | Open the native chooser and display the automatically resolved program folder |
| `Development/tools/setup/SetupForm.cs / SetupForm` | four-argument constructor | Receive an optional completion-notice provider without coupling UI to the removal engine |
| `Development/tools/setup/SetupForm.cs / SetupForm` | `ShowPage` | Render state-specific text, navigation, progress, confirmation and completion |
| `Development/tools/setup/SetupForm.cs / WizardArtwork` | constructor / `OnPaint` | Draw the classic blue geometric artwork panel behind the supplied robot |
| `Development/tools/setup/Setup.cs / Setup` | `StartUninstallWizard` | Copy and launch the temporary UI helper with launcher identity and original installation folder |
| `Development/tools/setup/Setup.cs / Setup` | `WaitForLauncher` | Bound the wait for the exact original uninstaller process to exit |
| `Development/tools/setup/Setup.cs / Setup` | `RemoveInstallation` | Guard the explicit target/mutex, run reversible removal and retain any completion notice |
| `Development/tools/setup/UninstallTransaction.cs / UninstallTransaction` | `Remove` | Preflight, stage known files, remove matching registration, roll back failure and report cleanup notices |
| `Development/tools/setup/UninstallTransaction.cs / UninstallTransaction` | `CleanStage` | Delete only staging files/empty staging directory and return nonfatal cleanup information |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `MatchesInstallLocation` | Match equivalent full install paths for optional Control Panel version metadata |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `RequiresElevation` | Detect protected Program Files and registered machine targets |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `ApplyElevated` | Validate an administrator job, registered target and live official manifest before replacement |
| `Development/src/UpdateInstaller.cs / UpdateInstaller` | `VerifyOfficialRelease` | Compare prepared package identity with the official release |
| `Development/src/ElevationBroker.cs / ElevationBroker` | `IsAdministrator getter` | Check the current process token |
| `Development/src/ElevationBroker.cs / ElevationBroker` | `Run` | Request runas, wait for the helper and interpret its result without elevating the user window |
| `Development/src/ElevationBroker.cs / ElevationBroker` | `Complete` | Preflight result IPC, require the administrator token and record the actual operation outcome |
| `Development/tools/setup/InstallationScope.cs / InstallationScope` | `MachineShortcut / UserShortcut getters` | Resolve common and legacy per-user Start menu entries |
| `Development/tools/setup/InstallationScope.cs / InstallationScope` | `InitialDirectory / ReadMachineDirectory` | Read the remembered machine folder or standard Program Files default |
| `Development/tools/setup/InstallationScope.cs / InstallationScope` | `IsMachineTarget / RequiresMachinePrivileges` | Select machine/legacy scope for removal |
| `Development/tools/setup/InstallationScope.cs / InstallationScope` | `CleanLegacyUserRegistration / CleanLegacyRegistration` | Migrate the original user's old registration/files after successful machine installation |
| `Development/tools/setup/Setup.cs / Setup` | `InstallMachine / InstallAtScope` | Require administrator rights and install against explicit registry/shortcut scope with rollback |
| `Development/tools/setup/Setup.cs / Setup` | `RemoveMachine / RemoveAtScope` | Require administrator rights for machine removal and invoke the scoped transaction |
| `Development/tools/setup/SetupFiles.cs / SetupFiles` | `CheckAvailable / SameContents` | Share file preflight and avoid rewriting an unchanged file during rollback |
| `Development/tools/setup/RegistryBackup.cs / RegistryBackup` | `Capture / MatchesTarget / Restore` | Share original registry value capture, ownership matching and restoration |

## Build and verification boundaries

`Development/tools/Build.ps1` stages the complete C# source, branding and manifest in a unique local development directory, compiles with warnings as errors, copies the two runtime files to `release`, prints the hash, and removes only its verified staging directory. `Package.ps1` embeds the same assets and shared Branding/Theme source into Setup. Neither command performs network package restore.

`Development/tools/Verify.ps1` compiles a separate verification runner outside the project. `Development/tools/checks/Verification.cs` groups maintained checks in `VerifyPersistence`, `VerifyValidation`, `VerifyLegacyConfiguration`, `VerifyDeadline`, `VerifyNetworkAndGui`, `VerifyNotificationDialogs`, `VerifyServerDialogs`, and `VerifyLaunch`. `UpdatesVerification.cs` adds isolated feed/HTTP, notification, archive, rollback and real worker-process checks. Helper functions provide assertions, control discovery and bounded message pumping. The runner uses isolated fixture settings; it does not change the user's server list. It retains evidence outside the project and compares tested/released binary hashes.

The source is reviewed before building and the resulting GUI/executable is checked after building. The revised checks verify legacy-list compatibility and the removal of unnecessary dashboard/editor controls. Verification details and practical limits are recorded in `VERIFICATION.md`.

`Development/tools/checks/SetupVerification.cs` verifies original embedded assets, Windows file icons, and welcome/destination/confirmation/progress/completion/close-blocking/retry under real message loops. Its UI callback records the selected path without installing. `VerifyCustomInstallation` separately invokes the real install engine in isolated Thai paths with spaces, checks real COM shortcut/HKCU metadata, applies an actual update, then removes old/new fixture installations and checks ownership/data preservation. `VerifyUninstall` covers failure/retry with a locked fixture. `Run`, `VerifyBranding`, `Hash`, `SameImage`, `RenderIcon`, `CreateForm`, `VerifyInstallFlow`, `VerifyRetry`, `WaitUntilEnabled` and `Capture` keep checks separate from product code. The verification executable declares .NET Framework 4.8 and uses the application's runtime config. Both tested EXEs are checked against their release hashes.

`InstallationScopeVerification.Run` checks the Program Files/common shortcut defaults, captures `VerifyPreview`, tests `VerifyElevationJob`, verifies official-manifest mismatch rejection and exercises `VerifyLegacyMigration` only on fixtures. `VerifyMachineEngine` rejects unprivileged writes locally and performs real Program Files/HKLM fixture install/removal when administrative rights are already present. `VerifyMachineProcesses` runs only in the owner's GitHub Actions environment: from an empty product registration it exercises the real runas setup helper, downloads an official public package, exercises the actual administrator updater against an older disposable target, and invokes the runas uninstaller. It preserves unrelated fixture data and removes its own registration/shortcut. The existing published release is sufficient for this pre-publication check. If another workflow publishes during download, only an official-manifest mismatch is retried with the new manifest. This does not automate accepting a human UAC consent prompt.

## API references

- [Microsoft: Ping](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping?view=netframework-4.8)
- [Microsoft: Unicode IShellLinkW](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishelllinkw)
