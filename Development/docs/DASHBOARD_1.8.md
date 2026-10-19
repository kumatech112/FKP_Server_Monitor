# Dashboard, SQL Server and alert delivery — 1.8

This extends the owner's previous ping-only contract with optional SQL authentication checks, a card dashboard and independent Outlook/ntfy delivery. The program remains **FKP Server Monitor**, in the **FKP_Server_Monitor** project/repository. UI is Thai. Classic setup/removal, installation paths, elevation boundaries, updates and announcements retain their existing behavior.

## Current data flow

1. `Program.Main` acquires the existing per-user/session instance guard. `ConfigurationStore.Load` reads `servers.json`; schemas 1/2 become schema 3 in memory, retaining IDs/names/hosts and leaving the original file untouched until a normal save. Absent SQL settings mean disabled.
2. `ServerDialog` edits a copied entry. The basic tab needs only name/host. The SQL tab optionally adds endpoint, database, SQL username and a DPAPI-protected password. Validation precedes the existing atomic configuration save; the previous file becomes `.bak`. Bulk input remains ping-only.
3. `MainForm.RunChecksAsync` snapshots entries, caps concurrency at eight, and prevents overlapping cycles. Per entry, `NetworkProbe.CheckAsync` runs DNS/ICMP and the optional `DatabaseProbe.CheckAsync` independently and concurrently. SQL opens the specified database with encryption, no connection pool and no automatic connection retries, then executes `SELECT 1`. No table reads/writes or schema changes occur.
4. Each completed `ServerResult` retains separate Ping and Database values. A disagreement is a warning, both passing is healthy, both failing is down, and a missing required result is pending. Ping failure never prevents an independent SQL endpoint from being checked.
5. The UI thread stores the latest result by stable server ID, updates the hidden/list row and visible card, appends one actual ping sample to a bounded 40-sample history, updates summary counts and feeds separate ping/database alert trackers. Search changes visible controls, not the monitoring list. Selection is shared between cards and the existing list.
6. Two consecutive failures create one outage event; one later success creates one recovery event. Continued failures do not repeat within the process session. Edit/delete clears both trackers and that entry's results/history. The existing master server-alert preference controls local and external health notifications.
7. The local notification is recorded before attempting external delivery. A bounded in-memory queue serializes events; Outlook and ntfy for the same event run independently in parallel. Failures produce local inbox entries. Sending does not block the monitoring cycle and ambiguous failures are not automatically retried. Closing cancels pending delivery and monitoring. There is no background Windows service.
8. Release checks/announcements continue on the existing independent timer and fixed GitHub channel. Server settings and health events are not uploaded to GitHub. Outlook/ntfy carry health events only; developer announcements remain in the app inbox.

## Outlook setup and identity

Microsoft Graph delegated `Mail.Send` uses a Microsoft public-client application registration supplied later by the owner. Device authorization requests `offline_access`; Microsoft hosts the sign-in page. The app stores a DPAPI-protected refresh token, requests short-lived access tokens in memory and posts text messages to `https://graph.microsoft.com/v1.0/me/sendMail`. HTTP 202 means accepted, not guaranteed mailbox delivery. No client secret, Outlook password or Outlook COM automation is involved. Personal accounts require a registration supporting personal accounts; organizations may require administrator consent or disallow device authorization.

Changing client ID/tenant clears the saved authorization in the dialog. A refreshed token is persisted only if the sending account still matches the current settings, avoiding overwriting an account changed while delivery was running. An unreadable alert settings file disables sending and is preserved rather than silently replaced. Channels start disabled and can be configured later.

## Credential and connection boundaries

- `ProtectedSecrets` uses Windows DPAPI CurrentUser, including configuration backups. Secrets must be re-entered after moving to another machine/profile. Names, hosts, database names, recipients and topics remain ordinary configuration data.
- SQL uses `SqlConnectionStringBuilder` to escape values and reports fixed failure categories instead of raw driver messages. Encryption/certificate validation defaults on; trusting a managed internal certificate is an explicit per-server option. `SELECT 1` confirms login/database access and basic execution, not complete application health.
- OAuth addresses are fixed Microsoft endpoints, tenant input is constrained, and production HTTP handlers disable redirects. ntfy uses an explicitly configured server root and JSON topic, UTF-8 text and optional bearer authorization. A token requires HTTPS for a non-loopback server. Public topics need appropriate access controls.
- Local notifications contain operational details. External channels send the server name, host, database name, state and time; choose recipients/topics accordingly.
- The portable package still contains exactly EXE + runtime config, so the existing update allowlist and rollback continue to apply. There are no new third-party DLLs or runtime installations.

## Changed files and functions

| File under `Development` | Functions / responsibility |
| --- | --- |
| `src/Models.cs` | `ServerEntry.Copy/Validate`, `AppConfiguration` schema 3, `ServerResult.Health/Summary`: optional SQL settings and separate status aggregation |
| `src/ConfigurationStore.cs` | `Load`: compatible in-memory schema migration; atomic saving retained |
| `src/DatabaseProbe.cs` | `DatabaseOptions.Copy/Validate`, `CreateConnectionString`, `CheckAsync`: fresh SQL-authenticated, encrypted read-only check with deadline/cancellation |
| `src/NetworkProbe.cs` | `CheckAsync`, `CheckHostAsync`, injected `IDatabaseProbe`: independent ICMP and database checks |
| `src/ProtectedSecrets.cs` | `Protect`, `Unprotect`: profile-bound credential storage and explicit re-entry failures |
| `src/FormFields.cs` | `Create`, `AddField`, `AddWide`: shared form layout without coupling the two settings dialogs |
| `src/ServerDialog.cs` | constructor, `SaveEntry`: copied entry editor and validated save |
| `src/ServerDialog.Database.cs` | `BuildServerEditor`, `ReadEntry`, `SetDatabaseControls`, `TestDatabase`: optional SQL tab and explicit test |
| `src/AlertSettings.cs` | options validation/copy, recipient parsing, endpoint validation, `AlertSettingsStore.Save`: disabled defaults and protected persistence |
| `src/AlertHttp.cs` | `Json`, `ReadAsync`: UTF-8 serialization and bounded response parsing |
| `src/MicrosoftMail.cs` | `BeginSignInAsync`, `CompleteSignInAsync`, `SendAsync`: cancellable device authorization, refresh and Graph delivery |
| `src/AlertDelivery.cs` | `NtfySender.SendAsync`, `AlertDeliveryService.SendAsync`: independent channel delivery with sanitized errors |
| `src/AlertSettingsDialog.cs` | `BuildOutlook`, `BuildNtfy`, `ReadSettings`, `SignIn`, `TestChannel`, `SaveSettings`: later account setup, masked tokens and explicit real test sends |
| `src/ServerCard.cs` | `UpdateResult`, input handlers, `OnPaint`, `DrawTrend`: dark rounded cards and real bounded ping history; dashboard palette isolated from installer palette |
| `src/MainForm.Dashboard.cs` | `BuildNavigation`, `BuildSummary`, `ShowDashboard`, `RenderCards`, `SelectCard`, `SyncCardSelections`, `ResizeCards`, `UpdateCard`: top menu, counters, cards/list and selection/history |
| `src/MainForm.cs` | constructor, `BuildLayout`, `BuildSettings`, `BuildGrid`, `RenderRows`, `UpdateRow`, `UpdateTotals`, `ShowSelectedDetails`, `RunChecksAsync`, edit/delete handlers, `OnClosing`: integrate dashboard/SQL while retaining scheduler and cancellation |
| `src/MainForm.AlertDelivery.cs` | `InitializeAlertDelivery`, `ShowAlertSettings`, `PublishHealthTransition`, `QueueExternalAlert`: separate trackers and bounded delivery pump |
| `src/MainForm.Notifications.cs` | `InitializeNotifications`, `AddNotification`, `PublishServerAlert`: integrate delivery, database warning popups and safe startup notifications |
| `src/AssemblyInfo.cs` | product description/version 1.8.0; compatibility namespace/identity retained |
| `tools/Build.ps1`, `tools/Package.ps1` | Framework data/security references, current feature release notes; same distributable structure |
| `tools/Verify.ps1`, `tools/checks/Verification.cs` | run focused checks alongside existing GUI/install/update/removal checks; updated expected schema/dashboard |
| `tools/checks/MonitoringVerification.cs` | credential/migration, real local stalled SQL handshake, independent probe fixtures, mocked Microsoft/ntfy requests, dialog rendering, alert transitions, selection and history checks |
| `tools/checks/InstallationScopeVerification.cs` | first unpublished renamed feed exception follows 1.8; other unexpected feed errors still fail and production update validation is unchanged |

## Verification limits

The previous baseline passed 134 checks before this feature work. New delivery tests use injected HTTP handlers; SQL success/failure composition uses an injected database probe alongside real local ICMP. The SqlClient timeout path uses a real local TCP listener that intentionally stalls the SQL handshake. Dialogs are rendered and reviewed, and the real packaged EXE is launched. Tests do not contact a production database, sign into a Microsoft account, send email or publish ntfy messages. Those end-to-end checks require the owner's later account/endpoint configuration. Installer checks retain isolated fixtures, rollback and uninstall safeguards.

See the Thai [user guide](USAGE.th.md) for setup and the current verification record for final build hashes/evidence.
