# Folder layout aligned with MPFM_Bot_Worker

The reference project was inspected read-only at `\\10.0.0.97\kuma$\Z\Projects\MPFM_Bot_Worker`. Its top-level Program, Development and Archive organization is used here. Only folders required by this C# application were created; MPFM-specific databases, Python runtimes and report folders were not copied.

```text
FKP Server Monitor/
  FKP Server Monitor.lnk          Launch the current app
  README.md                      Short Thai entry guide
  Program/
    FKPServerMonitor.exe
    FKPServerMonitor.exe.config
    FKPServerMonitor-Setup.exe
    USER_GUIDE_TH.md
  Development/
    README.md
    src/
    assets/
    docs/
    tools/
      checks/
      launcher/
      setup/
  Archive/
    releases/v1.7.0/              Installer, ZIP and update manifests
    announcements/               Prepared announcement feed
    verification/                Verification binaries, fixtures and screenshots
    builds/                      Temporary compilation, removed after build
    previous/                    Preserved pre-layout release
    legacy-releases/             Older generated builds
    organization-preservation.json
```

Git metadata, .github automation and editor configuration remain at the repository root. Program, Archive and the generated root shortcut are excluded from Git. The complete source and source assets remain under Development.

## Paths and data flow

Build reads `Development/src`, `Development/assets` and the source runtime configuration. It compiles in a unique `Archive/builds/build-*` directory, copies the executable/configuration/user guide into Program, creates the root shortcut, then removes only its own compilation directory after validating the absolute cleanup path.

Package reads Program and writes versioned installer/ZIP/manifests to `Archive/releases`. It also copies the installer into Program for normal use. The ZIP still contains exactly the application EXE and runtime configuration; development files and the user guide are not added to automatic updates.

Verify copies the real Program files into a unique local temporary verification directory, runs the maintained checks there, and compares the tested EXE/installer hashes with Program. It then preserves the complete results in `Archive/verification/verification-*` and removes its successful local temporary run after copying. Failed local runs remain available for diagnosis. Local execution is necessary because Windows' associated-icon API rejects UNC paths and file-lock/permission fixtures need local filesystem behavior. The root shortcut is loaded through Windows' native Shell Link interface to verify its target, working folder and icon.

The preparation script writes announcement drafts to `Archive/announcements`; preparing a file does not send an announcement. GitHub Actions uses the relocated tools and Archive release paths. Existing public repository URLs, update manifests, installation behavior, product version and user-data paths are retained.

## Changed files and functions

| File | Functions / entry points | Change |
| --- | --- | --- |
| `Development/tools/Build.ps1` | Build entry point and cleanup block | New source/output/staging paths, Thai guide copy and root shortcut generation. |
| `Development/tools/Package.ps1` | Package entry point | Read Program and write Archive/releases; installer copy goes to Program. |
| `Development/tools/Verify.ps1` | Verification entry point and evidence-preservation block | Read Program, run local fixtures, preserve Archive/verification evidence, validate temporary cleanup paths and pass the repository root to checks. |
| `Development/tools/Prepare-Announcement.ps1` | Preparation entry point | Write drafts under Archive/announcements. |
| `Development/tools/launcher/CreateShortcut.cs` | `Main` | Generate the root launcher using the existing native Unicode shortcut helper. |
| `Development/tools/checks/LayoutVerification.cs` | `Run` | Verify the real directory arrangement, runnable contents and root shortcut metadata. |
| `Development/tools/checks/Verification.cs` | `Main` | Run the additional layout checks. |
| `.github/workflows/release.yml` | Build/package/verify/publication and empty-feed steps | Use Development and Archive paths. |
| `.gitignore` | Ignore rules | Exclude Program, Archive and generated root shortcuts. |
| `README.md`, `Development/README.md`, current Thai usage/publishing guides and architecture document | Documentation | Updated entry points, commands, folders and links. |

No production C# function is changed for this reorganization. All production source and icon hashes are checked against the preserved originals. Historical verification records remain intact and describe their earlier locations.

## Validation

Before the layout change, all 131 existing checks passed. After the change, run these from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Development\tools\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Development\tools\Package.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Development\tools\Verify.ps1
```

Existing checks cover the actual GUI, ping, settings, installer/uninstaller and update compatibility. New checks verify the actual root shortcut and Program contents. User settings remain in `%LOCALAPPDATA%\Kuma\ServerBotTracker`; the comparison project is not modified.

Short commit message: `refactor: organize project into Program Development and Archive`
