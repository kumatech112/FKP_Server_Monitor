# Verification: Server Bot Tracker 1.3

Date: 2026-10-15 (Asia/Bangkok).

The clean 1.2 source and installer were inspected before editing. All 62 baseline checks passed before changes. The owner requested product icons and a branded installer, then explicitly chose to keep the existing colors. Ping behavior, settings schema 2, update validation and notification rules were preserved.

## Current local release

**78 checks passed** against byte-identical copies of the application and installer. Compilation treats warnings as errors. The final hash-comparison addition to Verify.ps1 was also checked directly against both tested files.

- The original 62 persistence, migration, ping, GUI, notifications, release-channel, archive, rollback, real worker-process and EXE entry-point checks pass.
- Both EXEs embed the original supplied ICO and PNG byte for byte. Native Windows file icons render the robot correctly.
- The dashboard, dialogs and tray receive the product icon. The dashboard header displays the supplied artwork and retains the existing light/blue palette.
- Setup and application versions match at 1.3.0.0.
- The actual compiled SetupForm renders welcome, progress and completion screens under a real Windows Forms message loop.
- Installation disables duplicate actions and blocks user closure until its worker returns. The worker runs in STA, preserving the existing COM shortcut/mutex assumptions.
- Completion offers optional app launch. Unchecking it and clicking Finish closes without launching or writing installation files in the isolated UI fixture.
- An injected installation failure displays its error, permits retry, then completes on the successful retry.
- The verification runner targets .NET Framework 4.8 with the app's runtime config. Native icons are compared through Graphics.DrawIcon, rather than the legacy PNG-backed Icon.ToBitmap conversion.

UI state checks use injected callbacks. The actual installation engine was separately exercised through the real setup EXE with /silent: exit code 0, installed version 1.3.0.0, installed EXE hash equal to the tested release, correct Start menu icon location, and existing servers.json preserved byte for byte. No installed-app launch or uninstall was performed.

## Local build hashes

- Application EXE: `998E860913AB24E29F70CA91020BD0D7FCDDE2B318C07B9E957E31933A84B0EC`
- Setup EXE: `F45C0EDF038369DEE1F8889D840D3B323612A4E9C1A21EE5D24A4E994223E356`
- Update ZIP: `6FA0A630EE81E0B61A3C83FE17536C19EEE6956C919E02048CAD4AE262941993`
- Original ICO: `1045110612329C78DDF59C52F5DD54078EE3AA3BBC8D15E3D8C037C0210E60B9`
- Original PNG: `FDC2A81D16B763291C78D15079891414155FE2FA8228DFED27E91DBCAEBE7B44`

Evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261015-125037-dac517ce`. Includes `gui.png`, `setup-welcome.png`, `setup-installing.png`, `setup-complete.png`, notification/update/server dialogs and native icon renders. Generated evidence stays outside the project.

## Public release verification

The [stable 1.3.0 release](https://github.com/kumatech112/Server_Bot_Tracker/releases/tag/v1.3.0) is published from source commit `d68c627`, with setup, portable ZIP and update manifest assets. Both main and tag workflows succeeded; the [tag run](https://github.com/kumatech112/Server_Bot_Tracker/actions/runs/38689301068) passed all 78 checks before publishing. Its verified EXE hashes match the independently downloaded public assets.

The original 1.2 application's actual `ReleaseChannel.GetLatestAsync`, `GetAnnouncementsAsync` and `PrepareAsync` read the live public channel and downloaded/validated the 1.3 package. The original 1.2 `UpdateInstaller.Apply` then upgraded an isolated 1.2 install to the branded 1.3 executable, preserving a fixture data file. This checks backward compatibility of the updater already distributed to users, without replacing an active user installation. The announcement feed still contains zero items.

- Public application EXE SHA-256: `1069EFFAC2FEE6B2A1284A8F80FBC5CE6BB9F1AEA9DD390D9F98EF84805D126A`.
- Public setup EXE SHA-256: `59D5D9D22BDDFDCD59B18C747A56B54861C82135C52894F4096F1B0ABA7EBE77`.
- Public ZIP SHA-256: `6695341C1666F8225B7CAFB03BBBC44812A71C84A22CCEBCF5867EAC721FDFFD` (360,685 bytes).

Live integration evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\live-1.2-to-1.3-20261015-125437`. Local and GitHub builds are separate compiler outputs and their hashes are checked separately.

## Boundaries

- During 1.2 verification, automatic approval review rejected a combined installed-app launch/uninstall command with "blocked by policy." That command did not run and was not retried. Uninstall was not retested in 1.3; its implementation is unchanged.
- The installer remains unsigned. HTTPS and SHA-256 validation do not constitute Windows publisher signing.
- The installer UI's optional launch branch is present but was disabled during fixture checks. The actual application entry point was tested separately from the distributable.
- Production server connectivity and other Windows/DPI environments were not verified. Controlled networking and isolated settings/install fixtures were used.
- The same public repository hosts source and releases at the owner's request. No user server list, credentials or runtime notification history is included in either downloadable package.

Short commit message: `feat: add robot branding and guided Thai setup`
