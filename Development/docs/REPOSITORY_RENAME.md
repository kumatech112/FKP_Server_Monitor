# Project folder and repository name

Project folder: `\\10.0.0.97\kuma$\Z\Projects\FKP_Server_Monitor`.

GitHub repository: [kumatech112/FKP_Server_Monitor](https://github.com/kumatech112/FKP_Server_Monitor).

Git origin: `https://github.com/kumatech112/FKP_Server_Monitor.git`.

The application display name, installer display name, default Program Files folder, executable names, version and per-user settings location remain unchanged. The top-level shortcut is refreshed to point to the executable in the renamed project's Program folder.

## Changed files and functions

| File | Function / setting | Change |
| --- | --- | --- |
| `Development/src/ReleaseChannel.cs` | `ReleaseChannel.OfficialRepository` | Use the renamed repository for update and announcement downloads. |
| `Development/tools/setup/Setup.cs` | `InstallAtScope` | Register the new official repository link in Control Panel. |
| `Development/README.md` | Downloads, repository description and folder tree | New URLs and underscore-separated project folder name. |
| `Development/docs/ARCHITECTURE.md`, `RELEASES.th.md`, `RENAME.md`, `FOLDER_LAYOUT.md` | Repository and layout documentation | Record the new canonical repository and compatibility behavior. |
| `README.md` | Thai entry guide | Distinguish the filesystem/repository name from the app display name. |
| `.git/config` | `remote.origin.url` | Point the existing checkout to the renamed repository. |

## Validation and older installations

The GitHub repository ID, public visibility, branch history and release assets are preserved by renaming the existing repository. No replacement repository was created.

All 134 application checks passed after rebuilding with the new repository setting. The original `Server_Bot_Tracker` latest-update URL returned the v1.6.0 compatibility manifest. The original v1.6.0 package URL still downloaded the ZIP matching the pinned SHA-256 and size.

The old repository name must remain available for GitHub's redirect because older installed clients still use it. Do not create a new repository at that old name. GitHub documents repository redirects and their limitations in [Renaming a repository](https://docs.github.com/en/repositories/creating-and-managing-repositories/renaming-a-repository).

This change does not publish an application release or send an announcement. The previous first-release limitation for the new update-fkp.json feed remains until the renamed app's release is published.

Short commit message: `refactor: rename project folder and GitHub repository to FKP_Server_Monitor`
