# Changelog

## 0.1.1 - September 8, 2026

- Added **Check for Updates** support for both the companion and the Azeroth Questing addon.
- Added companion self-updating from public GitHub Releases. When a newer companion release is available, the client downloads it, stages it with a temporary updater copy, exits, replaces the executable, and restarts automatically.
- Added SHA-256 verification when GitHub supplies an asset digest for the companion update package.
- Added GitHub Actions packaging for `AzerothQuestingCompanion-win-x64.zip` and automatic versioned GitHub Release publishing for release commits.
- Redesigned the Windows client around the dark Azeroth Questing dashboard style with sidebar navigation, top update actions, status cards, addon controls, WoW path controls, and recent activity.
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
