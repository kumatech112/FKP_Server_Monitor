# Code review, data flow and function index

## Initial inspection and scope

The requested workspace was empty on 2026-10-09 and was not inside a Git repository. There was no existing application code, database, configuration, or packaging to refactor. This delivery creates an isolated Windows Forms application. It does not change other projects, install services, edit firewalls, or invoke command-line ping. Its network activity consists of DNS lookup, ICMP echo, and a TCP connection handshake to user-configured endpoints.

The app uses C# and the Windows .NET Framework 4.8 libraries. No package manager or third-party runtime dependencies are introduced. The source tree contains complete readable source; no generated/minified implementation is tracked.

## Data flow

```mermaid
flowchart TD
    Entry[Program.Main: user/session instance guard] --> Load[ConfigurationStore.Load]
    Load --> Window[MainForm: Thai GUI]
    Window --> Edit[ServerDialog / BulkDialog]
    Edit --> Validate[ServerEntry.Validate and configuration validation]
    Validate --> Save[Atomic JSON save and previous-version backup]
    Save --> Model[In-memory configuration updated only after successful save]
    Model --> Window
    Window --> Trigger[All / selected rows / automatic timer]
    Trigger --> Queue[Snapshot entries and timeout; at most 8 active entries]
    Queue --> DNS[Resolve one IP with bounded DNS wait]
    DNS --> ICMP[Asynchronous ICMP echo]
    DNS --> TCP[Asynchronous TCP handshake]
    ICMP --> Result[ServerResult with independent results and completion timestamp]
    TCP --> Result
    Result --> UI[UI-thread result dictionary, rows and summary counts]
    UI --> Timer[Wait configured interval after cycle completes]
    Timer --> Trigger
    UI --> CSV[ReportWriter: UTF-8 BOM CSV snapshot]
    Window --> Cancel[Stop / close: cancellation token]
    Cancel --> Queue
    Cancel --> DNS
    Cancel --> ICMP
    Cancel --> TCP
```

### Startup and persistence

`Program.Main` initializes Windows Forms and takes a session-local, per-user mutex. A second instance in the same Windows session displays a Thai message. `MainForm` loads the per-user JSON file through an injected `ConfigurationStore`; a first launch returns default settings without creating a data file.

Both loading and saving validate schema version, field values, IDs, modes, and settings ranges. A failed load stops startup and reports the existing file location. A save writes a unique temporary file in the same data directory, flushes it, then renames/replaces it atomically. Replacement retains the previous file as `.bak`. `MainForm.CommitConfiguration` updates its in-memory configuration only after persistence succeeds. Server edits invalidate their old check result; deletion removes matching result records.

The mutex is session-local: do not edit one user's settings concurrently from multiple Windows sessions or external tools. The application has no cross-machine synchronization or shared central database.

### Check scheduling and threading

`MainForm.RunChecksAsync` snapshots the chosen entries and timeout, stops the timer, and disables mutation controls. A semaphore permits at most eight active server entries. DNS gets its own timeout; a successful resolution selects the first IPv4 address or first available address. ICMP/TCP run concurrently for the same selected IP. Each method uses its own timeout and reports independent success/failure.

`NetworkProbe` uses `ConfigureAwait(false)` internally. `MainForm` awaits the probe on the Windows Forms synchronization context, so grid, counter, and dictionary updates occur on the UI thread. Search can safely rebuild visible rows while a check is running: completed results are stored by stable entry ID and applied only to currently visible matching rows.

`WithinAsync` races the operation against a linked cancellable delay. Socket resources are disposed after TCP timeout/cancellation. Late DNS/socket faults are explicitly observed; Framework DNS itself cannot be forcibly cancelled, so a timed-out DNS operation may finish in the background. Its result is discarded and never applied after cancellation.

Cancellation preserves completed results and previous results for unfinished entries. Closing marks the window as closing before cancelling; subsequent continuations skip UI work. The next automatic cycle starts only after all tasks in the current cycle settle and the configured delay elapses. Turning off automatic mode stops future cycles; the Stop button also cancels the current cycle.

### Reports and result meaning

The results dictionary is memory-only and keyed by entry ID. It is neither historical storage nor a server-health database. CSV exports all configured entries in saved order, independently of search or selection. It quotes fields, doubles embedded quotes, includes a UTF-8 BOM, explicitly labels untested/disabled checks, and prevents formula-like server names from being executed as spreadsheet formulas.

TCP measures connection establishment only; no HTTP request, TLS validation, database login, or application transaction is performed. ICMP cannot distinguish a powered-off host from a firewall that drops echo packets. Multiple hostname DNS records are not exhaustively checked. The actual chosen IP appears in row detail/tooltip and export.

## Function index

All application functions added in this initial delivery are listed below. Methods with the same name belong to the explicitly identified class. UI construction and behavior are grouped in `MainForm` so a small app does not require dozens of single-purpose files.

| File / class | Function | Responsibility |
| --- | --- | --- |
| `src/Program.cs` / Program | `Main` | Initialize GUI, guard same-session duplicate launch, load user store, report startup errors |
| `src/Models.cs` / ServerEntry | constructor | Initialize stable ID and entry defaults |
| `src/Models.cs` / ServerEntry | `Copy` | Create independent editable / check snapshot |
| `src/Models.cs` / ServerEntry | `Validate` | Validate name, host, mode and port |
| `src/Models.cs` / AppConfiguration | constructor | Initialize schema, empty list and settings defaults |
| `src/Models.cs` / AppConfiguration | `Copy` | Deep-copy entries before mutation |
| `src/Models.cs` / AppConfiguration | `Validate` | Validate schema/settings/entries and reject duplicate IDs |
| `src/Models.cs` / ServerResult | `Summary` getter | Compute pass, partial pass or fail from enabled checks |
| `src/ConfigurationStore.cs` / ConfigurationStore | constructor | Normalize an explicit storage file path |
| `src/ConfigurationStore.cs` / ConfigurationStore | `ForCurrentUser` | Resolve the per-user LocalAppData data path |
| `src/ConfigurationStore.cs` / ConfigurationStore | `Load` | Deserialize validated configuration without replacing unreadable files |
| `src/ConfigurationStore.cs` / ConfigurationStore | `Save` | Validate and atomically persist with backup and temporary-file cleanup |
| `src/NetworkProbe.cs` / NetworkProbe | `CheckAsync` | Resolve address, run selected checks, assemble completed result |
| `src/NetworkProbe.cs` / NetworkProbe | `CheckIcmpAsync` | ICMP echo, latency and timeout/error translation |
| `src/NetworkProbe.cs` / NetworkProbe | `CheckTcpAsync` | TCP handshake, connection duration and timeout/error translation |
| `src/NetworkProbe.cs` / NetworkProbe | `ConnectAsync` | Adapt asynchronous connect to the generic deadline helper |
| `src/NetworkProbe.cs` / NetworkProbe | `Failure` | Create a failed result with an explicit message |
| `src/NetworkProbe.cs` / NetworkProbe | `WithinAsync` | Bound waits, prioritize cancellation, and observe abandoned faults |
| `src/NetworkProbe.cs` / NetworkProbe | `ObserveFailure` | Consume a late task exception without UI side effects |
| `src/ReportWriter.cs` / ReportWriter | `Write` | Export all latest results to Thai CSV |
| `src/ReportWriter.cs` / ReportWriter | `EscapeCell` | Preserve CSV text and protect formula-like input |
| `src/Theme.cs` / Theme | `Button` | Consistent button colors, dimensions and padding |
| `src/Theme.cs` / Theme | `Label` | Consistent label styling |
| `src/Theme.cs` / Theme | `ApplyDialog` | Apply Thai font, DPI behavior and modal window styling |
| `src/ServerDialog.cs` / ServerDialog | constructor | Build/populate single-entry editor |
| `src/ServerDialog.cs` / ServerDialog | `SaveEntry` | Validate editor input and return an independent entry |
| `src/ServerDialog.cs` / BulkDialog | constructor | Build multiline entry dialog |
| `src/ServerDialog.cs` / BulkDialog | `AddEntries` | Parse one host per line, validate all and deduplicate lines |
| `src/MainForm.cs` / MainForm | constructor | Load data, initialize window and attach lifecycle/timer handlers |
| `src/MainForm.cs` / MainForm | `BuildLayout` | Arrange heading, totals, tools, grid and status |
| `src/MainForm.cs` / MainForm | `BuildToolbar` | Bind CRUD, checking, Stop and export actions |
| `src/MainForm.cs` / MainForm | `AddEditButton` | Register buttons disabled during checks |
| `src/MainForm.cs` / MainForm | `BuildSettings` | Wire filter, timeout, interval and automatic checking |
| `src/MainForm.cs` / MainForm | `BuildGrid` | Define result table styling and interactions |
| `src/MainForm.cs` / MainForm | `AddColumn` | Define a named sortable read-only result column |
| `src/MainForm.cs` / MainForm | `RenderRows` | Filter/rebuild visible rows, restore selection and apply saved results |
| `src/MainForm.cs` / MainForm | `SelectedIds` | Map row selection to stable entry IDs |
| `src/MainForm.cs` / MainForm | `UpdateRow` | Render independent probe values, status color, timestamp and tooltip |
| `src/MainForm.cs` / MainForm | `ProbeText` | Format probe message and optional duration |
| `src/MainForm.cs` / MainForm | `UpdateTotals` | Count latest completed status and visible/untested entries |
| `src/MainForm.cs` / MainForm | `ShowSelectedDetails` | Display resolved IP and complete probe messages |
| `src/MainForm.cs` / MainForm | `RunChecksAsync` | Snapshot, schedule, cancel, collect and complete one bounded check cycle |
| `src/MainForm.cs` / MainForm | `SetBusy` | Toggle checking and mutation controls |
| `src/MainForm.cs` / MainForm | `CommitConfiguration` | Persist before changing in-memory configuration |
| `src/MainForm.cs` / MainForm | `AddServer` | Add a validated entry through the editor |
| `src/MainForm.cs` / MainForm | `AddMany` | Add validated bulk entries and skip existing combinations |
| `src/MainForm.cs` / MainForm | `EditServer` | Replace one selected entry and invalidate its old result |
| `src/MainForm.cs` / MainForm | `DeleteServers` | Confirm and persist deletion of selected entries |
| `src/MainForm.cs` / MainForm | `SaveSettings` | Persist interval/timeout, revert controls on failure and reschedule timer |
| `src/MainForm.cs` / MainForm | `ExportReport` | Choose output location and write latest snapshot |
| `src/MainForm.cs` / MainForm | `OnClosing` | Stop scheduling, cancel current cycle and suppress later UI updates |

`src/AssemblyInfo.cs` supplies version and Framework metadata; `src/app.manifest` declares user-level execution and DPI awareness. `tools/ServerBotTracker.exe.config` is the tracked source for the release runtime config.

## Build and verification boundaries

`tools/Build.ps1` stages the complete C# source and manifest in a unique local development directory, compiles with warnings as errors, copies the two runtime files to `release`, prints the hash, and removes only its verified staging directory. It performs no network package restore.

`tools/Verify.ps1` compiles a separate verification runner outside the project. `tools/checks/Verification.cs` groups maintained checks in `VerifyPersistence`, `VerifyValidation`, `VerifyReports`, `VerifyDeadline`, `VerifyNetworkAndGui`, `VerifyServerDialogs`, and `VerifyLaunch`. Helper functions provide assertions, control discovery and bounded message pumping. The runner uses isolated fixture settings; it does not change the user's server list. It retains evidence outside the project and compares tested/released binary hashes.

The source is reviewed before building and the resulting GUI/executable is checked after building. Verification details and practical limits are recorded in `VERIFICATION.md`.

## API references

- [Microsoft: Ping](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping?view=netframework-4.8)
- [Microsoft: TcpClient.ConnectAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.connectasync?view=netframework-4.8)
