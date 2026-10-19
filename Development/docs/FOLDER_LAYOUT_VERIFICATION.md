# Folder-layout verification

## Results

- Baseline before moving folders: **131 checks passed**.
- Rebuilt from the new layout: **134 checks passed**.
- All 16 production source/manifest files match their preserved pre-move SHA-256 values. Both branding assets match their original Git blobs.
- Windows' native Shell Link reader confirmed that the top-level shortcut points to `Program/FKPServerMonitor.exe`, uses Program as its working directory and uses the application robot icon.
- Program contains the executable, required runtime configuration, installer and Thai guide.
- Existing GUI, real ping, update validation/replacement/rollback, legacy-product migration, installation/uninstall and retained-settings checks passed.
- An isolated announcement-preparation fixture generated its draft under Archive/announcements without sending a message or modifying the project's prepared feed.
- Archived verification application and installer hashes match Program. Successful local staging was removed after copying results into Archive.
- All developer PowerShell scripts passed syntax parsing. Program, Archive and the root shortcut are excluded from Git.
- MPFM_Bot_Worker was used only as a read-only layout reference.

## Evidence and preserved files

Final evidence: `Archive/verification/verification-20261019-091908-b81f2225`.

Original move map and source hashes: `Archive/organization-preservation.json`.

Pre-layout release: `Archive/previous/release-before-layout`. The remaining former release folder is preserved at `Archive/previous/release-folder`.

Older generated builds: `Archive/legacy-releases/before-fkp-rename`.

SHA-256:

| File | Hash |
| --- | --- |
| Program/FKPServerMonitor.exe | `AAB50AF8916E93AC429AF3077498DB0FCEEDC7AF2F1A561A16FDBA82453CFB9E` |
| Program/FKPServerMonitor-Setup.exe | `6A0D739734639EBA570990CD0101273FBA00964AEE57080AB56064A646DE8C18` |
| Archive/releases/v1.7.0/FKPServerMonitor-portable.zip | `0B3D60C8609FD0AF1FD7F92C6AE2C65D52EE5A1B93A4FD31F065EAB86ADD89F8` |

## Limits

The local account is not an administrator. Local checks use disposable fixtures and do not modify the real installed app. The retained GitHub Windows workflow covers administrator-only operations separately. The first renamed public update feed remains unpublished; its existing first-release CI limitation is unchanged.

An early verification attempt on the UNC archive path exposed Windows' `Icon.ExtractAssociatedIcon` restriction. Verification now runs locally and copies results to Archive, retaining the previous local filesystem behavior for icon and file-permission checks. Production application code was not changed to work around this test restriction.

No app version, update contract, settings location, release tag or announcement was changed by this reorganization.
