# Verification — FKP Server Monitor 1.8.0

Date: 2026-10-19 (Asia/Bangkok). The unmodified baseline passed 134 checks before feature changes.

## Final local build

**173 checks passed** on byte-identical copies of the final app and installer. Native Framework compilation treats warnings as errors. The final source was rebuilt, packaged and verified after code/layout changes. Dashboard, SQL editor, Outlook settings and ntfy settings screenshots were rendered and visually reviewed.

Evidence: `Archive/verification/verification-20261019-104409-5207ee1c/verification.txt`.

| Artifact | SHA-256 |
| --- | --- |
| `Program/FKPServerMonitor.exe` | `F22ACEF81BFDE735947B0E6358D8E0C52AC8D970689140AEE6C1328AB5825209` |
| `Program/FKPServerMonitor-Setup.exe` | `82691984565407E828F8C8FA9093ABFA88CD2F2B9CB603804A7F9CF96902777D` |
| `Archive/releases/v1.8.0/FKPServerMonitor-portable.zip` | `5FA085D4C34F900E88367674747996CF61EDB676EC0DA93170D7BD74BB0BC135` |

The portable archive still has exactly the executable and required runtime config. Program also contains the rebuilt installer and current Thai guide. Generated deliverables, fixture binaries and screenshots stay outside Git in Program/Archive.

## New coverage

- Schemas 1/2 migrate to 3 without rewriting files during load or changing IDs/names/hosts. Existing backups remain part of atomic saving.
- SQL credentials containing separators are escaped correctly; encrypted connections, certificate validation and fresh non-pooled SQL authentication are checked. Passwords and OAuth/ntfy tokens remain DPAPI-protected on disk and in backups.
- Real loopback ICMP with injected SQL failure produces a database warning. Failed DNS with injected SQL success still runs the independent database check. Disabled SQL makes no SQL attempt.
- A real local TCP listener accepts the SqlClient connection but stalls the SQL handshake: the check returns unavailable within the bounded deadline, sanitizes the error and honors caller cancellation.
- Controlled HTTP fixtures exercise Microsoft device authorization including pending/cancel, token refresh/rotation and Graph 202 delivery, Thai mail content, recipients and credential boundaries.
- Controlled ntfy requests verify server-root JSON publishing, topic/priority, Thai UTF-8 and bearer authorization. Redirects are rejected. Either channel can fail while the other succeeds; disabled channels make zero requests.
- Corrupt alert settings are preserved and sending is disabled; tenant path injection and unencrypted remote token sending are rejected.
- Real settings dialogs retain hidden SQL passwords and support preparing disabled alert channels. Screenshots: `sql-settings.png`, `outlook-settings.png`, `ntfy-settings.png`.
- Independent database outage/recovery alerts suppress repetition. Card selection maps to the existing server-selection actions, top navigation changes views, and history retains the latest 40 supplied samples.

## Retained coverage and limits

Existing tests continue to exercise real ping/DNS, GUI CRUD/search/scheduling/stop/close, robot branding, actual EXE entry point, package integrity, update replacement/rollback, setup destination selection, uninstall launch routes, locked-file handling and removal rollback using isolated fixtures.

This local process is not elevated. Local tests do not claim new live Program Files/HKLM administrator operations; the Windows CI runner checks those in disposable fixtures. The renamed official update feed is still unpublished, so its administrator live-download path is explicitly skipped when it returns the expected 404 for the first renamed release; packaged protected-update checks remain active. Production update validation is unchanged.

No production SQL Server login, actual Microsoft account authorization, real email delivery or real ntfy publication was performed. Those end-to-end checks require the owner's later configuration. HTTP 202/2xx is service acceptance, not a guarantee of mailbox/phone delivery. Continuous monitoring requires the app to remain open with automatic checking enabled.

Source changes and full data flow: [DASHBOARD_1.8.md](DASHBOARD_1.8.md). User instructions: [USAGE.th.md](USAGE.th.md).
