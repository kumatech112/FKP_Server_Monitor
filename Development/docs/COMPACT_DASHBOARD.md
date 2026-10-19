# Compact CCTV-style dashboard — 1.8.1

The dashboard now shows small tiles containing only the server name and health state: **ปกติ**, **มีปัญหา**, **เชื่อมต่อไม่ได้**, or **ยังไม่ตรวจ** before a completed check. IP, SQL status, latency and timestamps remain in the separate **รายการเซิร์ฟเวอร์** (Listing) view. Colors remain green, amber, red and gray respectively.

## Presentation and data flow

The product identity and navigation share a compact top bar. Four counters use a thin strip. Existing actions/search/automatic-check controls remain above the monitoring area, following the owner's marked workspace. Tiles are 84 pixels tall at 100% display scaling, with adaptive columns and scrolling; widening the window adds columns instead of enlarging tiles. A 1260-pixel reference client area fits six columns; a 1900-pixel reference client area fits nine at 100% scaling. Actual columns adapt to available window space. The unused graph rendering and sample-history dictionary have been removed.

The existing results dictionary still feeds both views. `ServerResult.Health` keeps its previous semantics: disagreement between enabled Ping/SQL checks is a problem, both passing is normal, and both failing is disconnected. Missing results stay pending. Card labels simplify the presentation without changing network checks or notification thresholds. Click/Ctrl-select and double-click editing remain connected to the shared server selection. Listing retains detailed results and tooltips.

## Changed source functions

| File | Function | Change |
| --- | --- | --- |
| `src/ServerCard.cs` | constructor | Compact tile with no graph/history dependency |
| `src/ServerCard.cs` | `StatusText` | Thai normal/problem/disconnected/pending labels |
| `src/ServerCard.cs` | `UpdateResult` | Updates tile state and accessibility text |
| `src/ServerCard.cs` | `OnPaint` | Draws only name and status with a color accent |
| `src/ServerCard.cs` | `DrawText`, `Rounded` | Compact text bounds and corner radius |
| `src/MainForm.Dashboard.cs` | `BuildNavigation` | Shared compact product/menu bar |
| `src/MainForm.Dashboard.cs` | `BuildSummary` | Thin counters instead of large summary panels |
| `src/MainForm.Dashboard.cs` | `ShowDashboard` | Switches cards/Listing and removes stale Listing details from dashboard footer |
| `src/MainForm.Dashboard.cs` | `RenderCards` | Creates compact cards and a short empty-state prompt |
| `src/MainForm.Dashboard.cs` | `ResizeCards` | Adaptive dense columns, DPI-scaled height/gaps and bounded width |
| `src/MainForm.Dashboard.cs` | `UpdateCard` | Applies the current result without storing unused chart samples |
| `src/MainForm.cs` | `BuildLayout` | Single compact header and enlarged monitoring area |
| `src/MainForm.cs` | `UpdateTotals` | Footer includes all health categories, including problems |
| `src/MainForm.cs` | `ShowSelectedDetails` | Detailed selected-result text belongs to Listing |
| `src/MainForm.cs` | `EditServer`, `DeleteServers` | Remove obsolete history cleanup; existing result/tracker invalidation retained |

`AssemblyInfo.cs` increments the app/setup version to 1.8.1. `Package.ps1` describes the compact layout in release notes. User/developer guides reflect the new presentation. Existing monitoring, SQL credential storage, Outlook/ntfy delivery, installer/removal and update implementation are unchanged. The verification-only first-unpublished-feed exception follows the current patch version; production update checks remain intact.

## Double-check and packaged verification

The unchanged 1.8.0 baseline passed **173 checks** before edits. The rebuilt 1.8.1 app and installer passed **178 checks** afterward, including the retained monitoring, SQL, alert delivery, configuration, installation/removal, rollback and update checks. Existing GUI checks now also exercise compact pending/normal/problem/disconnected labels, shared selection, six/nine-column layouts, and the separate Listing view. Both layouts were rendered and visually reviewed with isolated UI fixture data. The actual packaged entry point was launched.

Evidence: `Archive/verification/verification-20261019-105635-5425a896/verification.txt`.
Screenshots: `compact-dashboard.png`, `compact-dashboard-wide.png`, `listing-wide.png`. These screenshots contain test entries, not the user's saved server list. No production accounts/endpoints were used or modified. The local process is not elevated; administrator-only operations remain the CI runner's responsibility.

| Final local artifact | SHA-256 |
| --- | --- |
| `Program/FKPServerMonitor.exe` | `89CDEE671C016AE72BCB3F51F064F840F8F884060C7C6D88F86EFB4A5A8C40FB` |
| `Program/FKPServerMonitor-Setup.exe` | `A1B314C6BDAADE1A47CEFC1E1EE0852646E54AB49FC13321123EE3117CD90476` |
| `Archive/releases/v1.8.1/FKPServerMonitor-portable.zip` | `59070F91FAED173C6AF218B1AF3EE9B4551738DAD8E7B0BB92776E3202F01673` |

Generated programs/evidence stay in Program/Archive and remain excluded from Git. The Thai guide in Program matches the maintained source guide. The portable archive still contains exactly EXE + runtime config. A source push alone does not publish an update to installed users.

Short commit message: `feat: add compact CCTV-style server dashboard`

The density fixture uses a child preview hosted inside an isolated test window, sets reference client sizes after creation, scales them with display DPI and records actual layout dimensions. This allows the reference layout to be measured independently of a smaller headless desktop. Production layout behavior is unchanged by this test correction.
