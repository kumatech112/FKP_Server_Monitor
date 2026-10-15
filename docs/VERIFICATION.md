# Verification: Server Bot Tracker 1.6.0

Date: 2026-10-15 (Asia/Bangkok).

The owner requested a standard Program Files installation for everyone, automatic product-folder creation when choosing a drive, and removal that does not delete earlier files before reporting a later error. The unchanged 1.5.2 baseline passed 106 checks before edits.

## Local result

**123 checks passed** on byte-identical copies of the final local app and installer. The current Windows process is not elevated. Compilation treats warnings as errors.

- Program Files and the common Start menu are the standard defaults. The real destination page shows the full Program Files path before mutation.
- Drive roots resolve to a ServerBotTracker subfolder. Browse appends the product folder once; full custom paths and Thai names/spaces remain supported.
- Unprivileged administrator jobs and direct machine-engine calls refuse mutation. Invalid IPC job directories and existing result files are rejected.
- The elevated-update trust check rejects a prepared digest differing from the official manifest.
- Legacy user-registration/files migration uses isolated HKCU fixtures and preserves unrelated data.
- Locked second runtime file, uninstaller and shortcut are detected before any owned item disappears. Retry succeeds after releasing each obstruction.
- A Windows deny-delete permission on a later file fails after an earlier EXE move; the earlier move is rolled back, registration/shortcut remain, and retry succeeds after explicitly restoring fixture ACLs.
- Another isolated ACL permits staging but prevents cleanup after removal commits. The engine returns completed removal with a cleanup notice, rather than a false uninstall error.
- Both real Uninstall.exe launch routes open the temporary removal wizard for the original Thai fixture directory; cancelling exits cleanly without installing/removing files.
- Existing ping, configuration migration/preservation, GUI, cancellation, notifications, ZIP verification, replacement rollback and worker-process checks remain covered.

Local evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261015-170307-20638cde`. The standard-folder preview is `setup-program-files.png`.

- App EXE: `BC82F91DA5C975D08265DA18C82E73C5DCBCF81E45C38F6CFE05FADFC71C2229`
- Setup EXE: `7AB089E1D5A7524E4AC10E52B64D204B4437A5AF1A0EBDF376797A08BC612A5E`
- ZIP: `A1EB8C127013AB4455F0C8CBF2755D58B0F3D9C5372364315AB173A37025CC27`

## Administrator-runner verification

**127 administrator-runner checks passed** in the [main workflow](https://github.com/kumatech112/Server_Bot_Tracker/actions/runs/38717703684) for source commit `e4098e6`. Logs explicitly confirm real runas setup, real protected administrator update with live official-package validation/HKLM version synchronization, and real runas uninstall retaining unrelated data. The four additional checks cover privileged machine operations unavailable to the non-elevated local run.

The owner's GitHub Actions environment adds real Program Files/common-shortcut/HKLM fixture installation/removal. It also exercises the actual runas setup process against an empty product registration, downloads a public official release, applies it through the actual administrator update process to an older disposable protected copy, checks machine display-version metadata, then exercises the runas uninstaller. Unrelated fixture data is retained. These checks run only on the owner's disposable CI machine, with guards against overwriting an existing product registration/shortcut.

The existing published release is used for the administrator update test, so the new release is not published before this verification. If another workflow publishes during a download, only an official-manifest mismatch is retried with the new official package. The normal local process does not bypass or automate Windows consent.

## Public release receipt

The [1.6.0 release](https://github.com/kumatech112/Server_Bot_Tracker/releases/tag/v1.6.0) is published from `915d618`. Its [tag workflow](https://github.com/kumatech112/Server_Bot_Tracker/actions/runs/38718144009) passed all **127 checks** before publication, including the real administrator install, protected-file update and uninstall processes. The corresponding main workflow passed as well.

The independently downloaded public Setup hash matches the tag runner's verified installer hash. The original 1.2 client also read the live latest manifest and empty announcement feed, downloaded/validated the public 1.6.0 ZIP through its unchanged PrepareAsync, and applied it through its original UpdateInstaller.Apply to an isolated Thai path while preserving data. Its resulting app hash matches the tag runner's app hash. This legacy test verifies the two-file package path; it does not migrate that fixture into Program Files.

- Public app EXE: `CB356E112828B292D01B17AB76DEBEDE276E398CAED86E20A7B9223784ED878F`
- Public Setup EXE: `E39F2FD812FCBA373512CD88630A37E973AE960A6D6AFB67D77B9538957DA724`
- Public ZIP: `D2B54C6EBEC89C0D74317BA55DB8636573FDE763EB95C5F3A867EBB66C75DA54` (363,177 bytes)
- Download evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\public-1.6.0-20261015-171014`
- Original-client evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\live-1.2-to-1.6.0-20261015-171124`

Local and CI compiler outputs are checked against their own verified hashes. Running the new Setup once is required for the new machine scope and uninstaller.

## Boundaries and delivery behavior

- The local user's production installation, registry and server list were not changed. Purpose-created files, registry keys and ACLs were used for local checks; fixture ACLs were restored explicitly. One early fixture restoration attempt did not mark its DACL as modified; the fixture-only test was corrected and re-run successfully.
- The Windows consent click is not automated. The administrator runner tests the runas/helper process path with an already elevated token. The dashboard manifest remains asInvoker, and the original user process performs optional launch/restart.
- A machine installation registers in HKLM/CommonPrograms. A legacy per-user installation remains supported until Setup is run to migrate it. Saved lists/preferences remain in each original user's LocalAppData directory.
- Machine installation, uninstall and protected-folder updates need Windows administrator approval. No permanent elevated service or always-elevated dashboard was added.
- Two runtime files remain in the automatic update ZIP. Running Setup once is required to receive the new installation scope and uninstaller; updating only the app does not replace an older Uninstall.exe.
- Rollback handles ordinary operation failures while the process runs. It is not a persistent filesystem/registry transaction covering forced termination or power loss. Failed rollback retains backups and names their recovery directory. Completed operations with temporary cleanup trouble are reported as completed, with details.
- The installer remains unsigned. No announcement message was published as part of this change.

Earlier release evidence remains in [the previous verification document](https://github.com/kumatech112/Server_Bot_Tracker/blob/28a1f5834581c5889004812b884e67ec392f0372/docs/VERIFICATION.md), including the older Control Panel display-cache observation and historical blocked installed-deletion command. Those old commands were not retried; current deletion checks are disposable fixtures.

Changed files/functions are indexed in [ARCHITECTURE.md](ARCHITECTURE.md). Runtime behavior is concentrated in Setup.Main/Install/InstallMachine/InstallAtScope/Remove/RemoveMachine/RemoveAtScope, InstallationScope, shared RegistryBackup/SetupFiles, UninstallTransaction, ElevationBroker and UpdateInstaller.RunWorker/RequiresElevation/ApplyElevated/VerifyOfficialRelease/Apply. The regular ping dashboard remains unchanged.

Short commit message: `feat: add standard machine installation and reversible uninstall`
