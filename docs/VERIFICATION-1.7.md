# Verification: FKP Server Monitor 1.7.0

## Local results

- The unmodified v1.6.0 baseline passed 123 checks before changes.
- The final v1.7.0 build passed 131 checks after changes.
- Build and Package compiled with warnings treated as errors from the final renamed network folder.
- The tested application and installer hashes match the delivered files.
- The real packaged dashboard launches and exits normally, performs loopback ping, and displays pass/fail states with Thai controls.
- The installer destination preview displays `C:\Program Files\FKP Server Monitor`; screenshots of the dashboard and installer were inspected.
- Migration checks cover old product metadata/filenames, blocked files before mutation, successful replacement of the old EXE/config/shortcut, preserved data, uninstall after migration, and rollback after a failure following staging.
- The older per-user registration cleanup uses a fixture with the actual former product name and filenames.
- The new client requests `update-fkp.json`. The old `update.json` preserves the published v1.6.0 manifest version, tag, hash and size.
- Existing custom paths, native Unicode shortcuts, installation rollback, updater replacement/rollback, direct Uninstall.exe and Control Panel's uninstall route remain covered by the existing suite.

Evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261019-090924-a5318702`.

Visually inspected screenshots: the preceding equivalent build's `verification-20261019-090815-661b4f7e\setup-program-files.png` and `gui.png`. The final verification changed only the legacy-cleanup test fixture; production binaries are identical.

SHA-256:

| File | Hash |
| --- | --- |
| `release/FKPServerMonitor.exe` | `1C9BDFE0B301F2E58753627B374D62F5FB8C2E1123502BCCF846A7AD348B8CC8` |
| `release/FKPServerMonitor-Setup.exe` | `A6E2CC9A84560E8020B74B1FE3BE68F76BBC081FD4DD6847661F91D658B48886` |
| `release/publish/v1.7.0/FKPServerMonitor-portable.zip` | `C8ED8533711E2641FF4A9AB4DFB89E64AA5A530B76D8080AAED9343222F4D16E` |

## Delivery and limits

Project: `\\10.0.0.97\kuma$\Z\Projects\FKP Server Monitor`.

The old root-level generated executables and eight older version output folders were moved to `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\rename-archive-20261019-98c8acfa`. Source and user data were not archived or deleted.

This local run used a normal Windows account. It did not modify the computer's actual installed application or claim administrator-only Program Files tests. The CI suite retains isolated machine installation/uninstall checks; for the first v1.7 release only, an absent public `update-fkp.json` is explicitly reported and the packaged update engine is tested in Program Files instead of claiming live public-feed elevation coverage.

No public release/tag or announcement was created by the local build. The renamed public update feed becomes available only after the release is published. The application and installer remain unsigned.
