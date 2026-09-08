# Changelog

## 0.1.4 - September 8, 2026

- Changed the Windows installer to install Azeroth Questing Companion under **Program Files** as a normal machine-installed application instead of under the current user's Local AppData Programs folder.
- Changed installation to request administrator approval and register the application for normal Windows Installed Apps/uninstall behavior.
- Kept Start Menu integration and made the desktop shortcut selected by default on the first install.
- Changed installer-based self-updates to request elevation explicitly and use the normal Windows installer path instead of the retired temporary executable-replacement updater.
- Added cleanup for legacy v0.1.1/v0.1.2 temporary updater files when they can be removed safely.
- Removed the remaining legacy `ZoneQuestGuide.lua` SavedVariables watcher path so the companion now watches only `AzerothQuesting.lua`.
- Replaced the planned Azure Artifact Signing integration with SignPath Foundation's free open-source signing path.
- Added a public code signing policy and privacy policy for the SignPath Foundation application requirements.
- Prepared GitHub Actions to use SignPath's GitHub trusted-build-system integration with origin verification.
- Prepared two-stage signing so the companion executable is signed before the installer is built, then the completed installer is signed before publication.
- Added Authenticode signature verification for both the installed executable and the installer.
- Changed development build artifacts to be clearly labeled as unsigned and made public release publishing depend on completed SignPath signing.
- Added SignPath code-signing policy information to future GitHub Release notes.

The v0.1.4 source still needs testing on the player's Windows installation. SignPath Foundation approval and the resulting SignPath project/API configuration are still required before v0.1.4 can be published as a signed public release. Until trusted signing is active, development artifacts remain unsigned and Windows SmartScreen or endpoint protection can still warn about them. Service01 uploading remains disabled; Pending Observations stay local.

## 0.1.3 - September 8, 2026

- Added a normal Windows installer, `AzerothQuestingCompanion-Setup.exe`, for a proper installed desktop application.
- Added Start Menu integration and a desktop-shortcut option.
- Changed the companion publish format from a compressed single-file executable to a conventional self-contained application directory packaged by Inno Setup.
- Changed companion self-updates to download and run the published installer from the companion's local update directory instead of copying the running executable into a randomized Windows Temp folder.
- Kept SHA-256 verification for GitHub Release assets when GitHub supplies a digest.
- Improved player-facing terminology so the dashboard uses **Pending Observations** and **Scan for Observations** instead of snapshot wording.
- Added explicit privacy wording that the companion does not capture the screen, record gameplay, or take screenshots.
- Documented that the Windows binaries are not code-signed yet. Endpoint-security products can still warn about unsigned/reputation-new binaries until code signing is added.

The v0.1.3 source and installer still need testing on the player's Windows installation. In particular, verify installation, desktop/Start Menu shortcuts, the new installer-based self-update flow on the next release, WoW path detection, addon update/repair, and Pending Observations behavior. Service01 uploading remains disabled; pending observations stay local.

## 0.1.2 - September 8, 2026

- Changed the player-facing queue terminology from **Snapshots** to **Pending Observations** so the client more clearly describes research data waiting to upload to Service01.
- Changed the manual queue action to **Scan for Observations**.
- Updated activity/status wording so newly collected data is described as an observation rather than a snapshot.
- Kept the underlying deduplication/outbox behavior unchanged.
- Updated the companion version and GitHub API user agent to 0.1.2.

The v0.1.2 source still needs testing on the player's Windows installation. This release was also intended to test the v0.1.1 self-update flow by clicking **Check for Updates** in v0.1.1 and allowing it to upgrade itself to v0.1.2. Service01 uploading remains disabled; pending observations stay local.

## 0.1.1 - September 8, 2026

- Added **Check for Updates** support for both the companion and the Azeroth Questing addon.
- Added companion self-updating from public GitHub Releases. When a newer companion release is available, the client downloads it, stages it with a temporary updater copy, exits, replaces the executable, and restarts automatically.
- Added SHA-256 verification when GitHub supplies an asset digest for the companion update package.
- Added GitHub Actions packaging for `AzerothQuestingCompanion-win-x64.zip` and automatic versioned GitHub Release publishing for release commits.
- Redesigned the Windows client around the dark Azeroth Questing dashboard style with sidebar navigation, top update actions, status cards, addon controls, WoW path controls, local data, and recent activity.
- Changed addon update checks so the same update action reports both installed/latest addon status and companion status.
- Removed all legacy ZoneQuestGuide addon detection, deletion, backup, and SavedVariables migration behavior. The companion now manages only Azeroth Questing.
- Kept addon installation/update protection that refuses to replace addon files while World of Warcraft is running.
- Kept SavedVariables monitoring and the deduplicated local observation outbox.

The v0.1.1 source has been built by GitHub Actions as part of development, but the redesigned Windows UI, self-update restart flow, and addon install/update flow still need testing on the player's actual Windows WoW installation. Service01 uploading is still disabled; queued data remains local.

## 0.1.0 - September 8, 2026

- Added the first Windows companion MVP.
- Added World of Warcraft Retail path detection.
- Added Azeroth Questing addon install/update support with backups and legacy ZoneQuestGuide migration.
- Added SavedVariables monitoring and a deduplicated local observation outbox.
- Added a Windows tray interface with addon/data status and manual scan/install controls.
- Added a self-contained Windows x64 GitHub Actions build.

This first client build still needs testing on the player's Windows WoW installation. Service01 uploading is not enabled yet; queued data remains local.
