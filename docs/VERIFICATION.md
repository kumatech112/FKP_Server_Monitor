# Verification: Server Bot Tracker 1.4

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

Publication and public 1.4 download verification are recorded after the version tag is pushed. Public and local builds are separate compiler outputs, so hashes are checked independently.

## Boundaries

- The installer is unsigned. HTTPS/SHA-256 do not replace Windows publisher signing.
- The complete uninstall/removal path was verified on disposable fixtures. The actual Control Panel action was verified to launch its current wizard; the agent did not confirm deletion of the user's real installation.
- The historical 1.2 combined installed-launch/uninstall test was blocked by automatic approval review. That old command was not retried; current removal tests use purpose-created fixtures and the new UI was inspected without deleting the installed product.
- Optional post-install launch was disabled in UI fixtures. The actual distributable entry point is tested separately.
- Other Windows/DPI environments and production server connectivity remain outside local verification.
- Existing installations must run Setup once to receive the new Uninstall.exe. The backward-compatible two-file automatic update package does not carry installer files.

Short commit message: `feat: add classic setup and Control Panel uninstall wizard`
