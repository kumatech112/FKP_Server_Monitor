# Verification: Server Bot Tracker 1.5

Date: 2026-10-15 (Asia/Bangkok).

The unchanged 1.4 build passed all 89 baseline checks before edits. The selected installation folder now flows through the wizard, file engine, shortcut, registry and temporary uninstaller handoff. Application ping behavior, settings, updater implementation and notification flows remain unchanged.

## Current local verification

**102 checks passed** on the final packaged application and installer. Compilation treats warnings as errors. The suite covers the existing persistence/migration, ping, GUI, cancellation, notifications, update validation/rollback and worker process checks, plus:

- Editable destination page with Browse; invalid typed relative paths remain on that page with an error and no installation writes.
- Back/Next retain the selected Thai path; confirmation is read-only, and the dedicated STA worker receives that exact normalized folder.
- The real installer extracts its embedded payload into an isolated Thai path containing spaces. The real COM shortcut points to that executable and working directory.
- Isolated HKCU registration records the selected location, icon and quoted uninstall command. The next Setup run reads that remembered path.
- Blank, relative, drive/share root and invalid paths are rejected; an absolute UNC subfolder is normalized without writing to any share.
- A colliding unrelated Uninstall.exe is retained and installation fails before program replacement or shortcut creation.
- A real update is applied at the selected fixture path and preserves its uninstaller and unrelated data.
- Removing one version 1.5 fixture after registering another preserves the newer registration/shortcut. Removing the current fixture clears its files and registration while retaining unrelated data in the old folder.
- Locked-file uninstall/retry, no launch after removal, settings preservation and empty-directory cleanup remain verified.

The destination and confirmation renders were inspected. Confirmation wraps long paths and provides vertical scrolling. The native folder chooser is wired to the Browse button; automated fixtures use typed paths rather than operating the native picker.

Final evidence: `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261015-140635-95e153ae`.

- Application EXE: `DDF33F96DA37DD886F3EC06AC50038B95BFCD6F4C912A2CAAB122D2B136D84B8`
- Setup EXE: `616B48E3586014C0F847A1D8DFEB2926EA802B26F241041C73090FD6F618342D`
- ZIP: `FF8809A909A1E056CC503E6659CF2629B0FF0C83954D89493708CBBCFC4F1DFE`

## Current delivery boundaries

Tests use purpose-created folders, shortcuts and HKCU keys outside the product registration. The user's real installation was not moved or removed during this change. Protected destinations require existing write permission; setup runs as the current user and does not automatically elevate. UNC parsing is verified, but installation/update over a production share was not exercised.

Choosing a new folder leaves the previous copy in place and redirects the single product registration. Registration ownership protection is in version 1.5 uninstallers; legacy uninstallers retain their previous behavior. Use the current Control Panel entry. The existing LocalAppData data path remains independent of the selected program folder.

The installer is unsigned. Run the new Setup once to receive the folder chooser and current uninstaller; the two-file automatic update ZIP changes only the application and runtime config. Optional launch is disabled in the wizard fixture; the real application entry point is checked separately.

Short commit message: `feat: support custom installation folders`

---

# Historical verification: Server Bot Tracker 1.4

Date: 2026-10-15 (Asia/Bangkok).

The unchanged 1.3 build passed all 78 baseline checks before this work. The requested screenshot defines the setup layout: blue artwork panel, white pages and standard wizard navigation. The Thai ping-only dashboard and saved-list schema are unchanged.

## Local verification

**89 checks passed.** The expanded suite verifies the built application and installer under real Windows Forms message loops, with byte-identical release copies and compiler warnings treated as errors.

- Existing persistence/migration, ping, GUI, cancellation, notifications, live entry point, package validation, rollback and update-worker checks remain covered.
- Supplied icon/logo resource bytes and Windows native file icons match in both EXEs.
- Next advances to confirmation without modifying files; Back returns to welcome without invoking the operation.
- Installation progress blocks duplicate actions and close; operation runs on STA; completion and retry display correctly.
- The uninstall wizard requires explicit confirmation. Its real removal engine is invoked against isolated fixture files and a dedicated HKCU test key, never the user's installed product.
- A locked fixture EXE blocks deletion, preserves registration and permits retry. Retry removes the three owned files, fixture shortcut and fixture registry entry.
- Saved fixture settings and unrelated files/directories survive removal. The product directory is removed only when empty, and a volume root is rejected before changes.
- Uninstall completion offers no app launch.
- Version metadata path matching accepts trailing separators and case differences, and rejects absent/different installations.

The real setup EXE also completed /silent installation with exit code 0. Installed application and Uninstall.exe hashes match their tested distributables; the existing real servers.json was unchanged. Registry metadata reports version 1.4.0.0, publisher Kuma, the quoted --uninstall command, NoModify/NoRepair, estimated size 1096 KB and installation date 20261015.

## Control Panel observation

Windows Programs and Features lists Server Bot Tracker. Its Uninstall/Change action successfully launched the new temporary-helper wizard titled `Server Bot Tracker 1.4.0` in uninstall mode. Welcome and confirmation states were observed; the real installed app was not removed by agent UI actions. The user interacted with the preview during inspection, so further input was stopped to avoid interference.

The existing Control Panel view continued to show version 1.2.0.0 despite registry reads in both 32/64-bit views showing 1.4.0.0 and the launched wizard showing 1.4.0. A refresh did not change the label. There is one matching uninstall key and no matching HKLM entry. A stale Shell display is suspected; its cause is not confirmed. This does not prevent the registered action from opening the current uninstaller. No unrelated Shell caches or registry entries were altered to conceal the discrepancy.

## Local hashes and evidence

- Application EXE: `6B891EC777CD454279E1DA11B23FF9A9D8F6E2562220B2DFFC540E6382CA401F`
- Setup EXE: `BD2ACF57D1EC5302ACFC9D591713AC749E6A1B2D77448D6DB3CCB383C52A9EEF`
- ZIP: `794C315447FF0905EFC8FB1359BE2BE69BB48364706C26226105E26890B55972`

The final 89-check evidence is at `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261015-134839-0b0046cb`, including install/uninstall welcome, confirmation and completion renders. Generated evidence remains outside the project.

## Release verification

The [stable 1.4.0 release](https://github.com/kumatech112/Server_Bot_Tracker/releases/tag/v1.4.0) is published from source commit `3d7196f`. Its [GitHub tag workflow](https://github.com/kumatech112/Server_Bot_Tracker/actions/runs/38694806548) passed all 89 checks before publishing setup, portable ZIP and update manifest assets.

The original 1.2 client read the live latest manifest and empty announcement feed, used its actual PrepareAsync to download/validate 1.4, and used its original UpdateInstaller.Apply to upgrade an isolated 1.2 copy while retaining fixture settings. An initial attempt just after publication still saw the preceding latest manifest; the same unmodified client passed after the new manifest became available. This does not replace an older Uninstall.exe: users must run the new Setup for the new uninstall wizard.

- Public application EXE: `EF3D32188DA8E931467F28B58A744050CA330753AC08471D6094CD652C3B3959`
- Public setup EXE: `6A65A74908AE7EE16DCE907259D2910FFBDB21F068D2D68BAAB353A8B160A5D9`
- Public ZIP: `F1A9E14DC7140DEFBA54FCF1DD7A505C74B82BE8F1FCEFBF119D4B9A8BD14E3A` (360,922 bytes)

Independently downloaded EXEs match the hashes verified in the tag workflow. Live integration evidence is at `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\live-1.2-to-1.4-20261015-135026`. Public and local builds are separate compiler outputs, so hashes are checked independently.

## Boundaries

- The installer is unsigned. HTTPS/SHA-256 do not replace Windows publisher signing.
- The complete uninstall/removal path was verified on disposable fixtures. The actual Control Panel action was verified to launch its current wizard; the agent did not confirm deletion of the user's real installation.
- The historical 1.2 combined installed-launch/uninstall test was blocked by automatic approval review. That old command was not retried; current removal tests use purpose-created fixtures and the new UI was inspected without deleting the installed product.
- Optional post-install launch was disabled in UI fixtures. The actual distributable entry point is tested separately.
- Other Windows/DPI environments and production server connectivity remain outside local verification.
- Existing installations must run Setup once to receive the new Uninstall.exe. The backward-compatible two-file automatic update package does not carry installer files.

Short commit message: `feat: add classic setup and Control Panel uninstall wizard`
