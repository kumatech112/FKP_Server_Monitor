# FKP Server Monitor 1.7.0 rename

The project folder is now `FKP Server Monitor`. The dashboard title, tray tooltip, About dialog, installer/uninstaller pages, Start menu shortcut, Control Panel display name and executable metadata use FKP Server Monitor. Publisher metadata is FKP; this does not constitute a verified Windows signature.

Distribution files are `FKPServerMonitor.exe`, `FKPServerMonitor.exe.config`, `FKPServerMonitor-Setup.exe` and `FKPServerMonitor-portable.zip`. Fresh installs default to `%ProgramFiles%\FKP Server Monitor`. Existing custom installation paths remain remembered. Run Setup once in the existing installation folder to migrate older files and shortcuts.

## Compatibility

- The GitHub repository was subsequently renamed to `kumatech112/FKP_Server_Monitor` at the owner's request. Older published links were checked through GitHub's redirects. See [repository rename](REPOSITORY_RENAME.md).
- Retain `%LOCALAPPDATA%\Kuma\ServerBotTracker`, the registry key, single-instance mutex and internal namespace/resource identifiers. These preserve old server lists, notification history and installation detection.
- The new feed is `update-fkp.json`. Older clients require exact old filenames and the former ProductName; their `update.json` retains the public v1.6.0 package through `tools/legacy-update.json`. Keep the older tagged package available. Users of the former name run the new Setup once before receiving future updates through the renamed channel.
- Setup recognizes both product names, checks old files before writing, stages obsolete files and shortcuts, and restores them if installation fails. Unrelated colliding files are not overwritten.
- Ping checks, Thai controls, theme, icons, normal-user dashboard permissions and user data are preserved. No announcement is broadcast by this change.

## Changed files and functions

| File | Functions or entry points | Change |
| --- | --- | --- |
| `src/AssemblyInfo.cs` | Assembly metadata | New product/publisher and version 1.7.0.0. |
| `src/ProductIdentity.cs` | `IsKnownProduct` | Product names and compatibility constants. |
| `src/MainForm.cs` | `MainForm` | Window title. |
| `src/MainForm.Notifications.cs` | `InitializeNotifications`, `ShowUpdateDetails` | Tray and About text. |
| `src/ReleaseChannel.cs` | Constants, constructor, `GetLatestAsync`, `ExtractPackage` | New filenames, user agent, manifest path and product validation. |
| `src/UpdateInstaller.cs` | `RunWorker`, `Apply` | Validate the renamed product. |
| `tools/setup/InstallLocation.cs` | `DefaultDirectory`, `Normalize`, `ForDestination`, `ForBrowseSelection`, `ValidateProductFiles` | New folder and recognition of old files. |
| `tools/setup/InstallationScope.cs` | Shortcut properties, `ReadMachineDirectory`, `CleanLegacyUserRegistration`, `CleanLegacyRegistration` | New shortcut names, recognition of both product names and stable registry identity. |
| `tools/setup/Setup.cs` | `StartUninstallWizard`, `AcquireInstance`, `InstallAtScope`, `LegacyFiles` | Renamed helper/messages/registration, transactional migration and rollback. |
| `tools/setup/SetupForm.cs` | `SetupForm`, field initializers, `ShowPage`, `RunPrimaryAction`, `BrowseDirectory` | Thai wizard branding and new executable path. |
| `tools/setup/UninstallTransaction.cs` | `Remove` | Remove identified old files during older per-user cleanup. |
| `tools/Build.ps1` | Build entry point | New EXE/config names. |
| `tools/Package.ps1` | Package entry point | New installer/ZIP/feed and compatible old feed. |
| `tools/FKPServerMonitor.exe.config` | Runtime configuration | Filename changed; content unchanged. |
| `tools/Verify.ps1` | Verification entry point | New filenames and genuine old-product fixture. |
| `tools/checks/RenameVerification.cs` | `Run`, `CreateLegacy`, `VerifyMigration`, `VerifyRollback` | Metadata, retained storage, legacy feed, lock preflight, migration, uninstall and rollback. |
| `tools/checks/UpdatesVerification.cs` | Release-channel verification | Assert the actual new manifest request path. |
| `tools/checks/SetupVerification.cs` | `Run` and existing setup checks | New filenames/folder and migration verification. |
| `tools/checks/InstallationScopeVerification.cs` | Scope checks and `VerifyMachineProcesses` | New assets; explicitly report first-release public-feed unavailability and test packaged Program Files update instead. |
| `tools/checks/Verification.cs` | `VerifyLaunch` | Launch the renamed distributable. |
| `.github/workflows/release.yml` | Publication step | Publish both manifests and renamed assets when a release is authorized. |

README, Thai usage/publishing guides and release instructions reflect the new name. Historical v1.6 verification remains in `VERIFICATION.md`.

## Validation boundaries

Original baseline: 123 checks passed. Rebuild and verify from the renamed folder using Build, Package and Verify. Checks cover the real GUI, loopback ping, update validation/replacement, custom installation, migration rollback and both uninstall entry points. Generated evidence stays outside the project in the existing development directory.

The first renamed public feed cannot be verified live before publication. The CI exception applies only to version 1.7.0.0 and HTTP 404; other failures still fail verification. Production updater validation remains unchanged in strength. A local non-administrator run does not claim elevated Program Files verification.

Short commit message: `feat: rename application to FKP Server Monitor`
