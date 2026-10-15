# Verification: updates and notifications 1.2

Date: 2026-10-15 (Asia/Bangkok).

The existing ping-only app and clean Git state were reviewed before implementation. Its existing 36 checks passed before changes. Server lists remain schema 2; port/service checks and CSV export were not reintroduced.

## Verified release

**62 checks passed** against a byte-identical copy of the final release. The build treats warnings as errors.

- Existing settings migration, corruption preservation, ping, DNS failure, selection, dialogs, search, cancellation, automatic ping refresh and real EXE startup.
- Announcement persistence, unread state, duplicate suppression, active/expired UTC windows and duplicate-ID rejection.
- Two-failure down transitions, no repeated warnings, independent server states, one recovery notice and baseline reset.
- Corrupt inbox fallback preserves its original data and does not stop monitoring.
- Real publisher-generated JSON/ZIP are accepted, including PowerShell's UTF-8 BOM.
- HTTPS channel parsing, rejection of unsafe redirects, unavailable-channel errors, normalized version comparisons and version/tag mismatch rejection.
- A deliberately stalled HTTP body that ignores read cancellation is closed by cancellation without hanging the update check.
- Current-version rejection before downloading; altered SHA-256 rejection; ZIP traversal rejection.
- Applying the actual new executable to an old-version fixture preserves unrelated files.
- A deliberate runtime-config lock fails after EXE replacement; the EXE rolls back to the old version and sibling staging files are removed.
- A real updater subprocess waits for its parent and installs without opening another dashboard.
- Real failed ping cycles populate the actual inbox; its read-all button persists read state; the update dialog shows the configured public repository.

The dashboard, inbox and update dialog were rendered under a real Windows Forms message loop with cross-thread access checking enabled. The main dashboard and original two-field server editor remain intact.

## Hashes

- Application EXE: `578F8E5C1D62B485694F6E10FC26CFDD4281E74ED19CFA3EEEECD22835876486`
- Setup EXE: `C2152F1306887C53288263549B5EBEDE3CBE1BDB237B87DD892D6E22ED2AE77F`
- Update ZIP: `019E783E4A25F90D135EDBF2E4E1A4454D5F0EC4D3F556E159B0F63718797C50`

Evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261015-114733-576435bb`.

The native per-user installer completed with exit code 0. Its installed EXE matches the final release hash. Start menu and Windows uninstall metadata were checked. The installed copy remains at `%LOCALAPPDATA%\Programs\ServerBotTracker`.

## Boundaries

- Automatic approval review rejected the combined installed-app launch/uninstall verification command with “blocked by policy.” That command did not execute. It was not retried through another method, and the installed copy was left in place. Release entry-point startup and the update worker were tested separately.
- The owner made the existing source repository public. Its public API confirms `private: false`, and Git push authentication was verified without reading credentials. Online publication is checked separately after the version tag is pushed.
- The same-repository release workflow uses GitHub's built-in workflow token. A matching version tag publishes after checks; a manual run creates a draft. The announcement channel starts empty, with no announcement broadcast.
- The installer is currently `NotSigned`; SHA-256 and HTTPS validation do not constitute Windows publisher code signing. A certificate-based signing script is prepared, but no certificate was generated or purchased.
- Production server connectivity, other Windows/DPI environments, and a public-Internet end-to-end upgrade were not verified. Controlled networking and local update fixtures were used.

All generated files stay in the ignored release folder or the Windows user's application-data directories. No server lists, settings, project source files or tokens are included in the update ZIP.

Short commit message: `feat: add GitHub updates and in-app notifications`
