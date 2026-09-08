# Azeroth Questing Companion

Windows companion application for the **Azeroth Questing** World of Warcraft addon.

## Current client - v0.1.3

The companion currently:

- Detects a World of Warcraft Retail installation on Windows.
- Detects the installed Azeroth Questing addon and its version.
- Checks both the companion and Azeroth Questing addon for updates from GitHub.
- Installs, updates, or repairs Azeroth Questing from the public `Frostcanvas/AzerothQuesting` GitHub repository.
- Refuses to change addon files while World of Warcraft is running.
- Backs up the existing Azeroth Questing addon before replacing it.
- Watches Azeroth Questing SavedVariables for changes.
- Queues deduplicated **Pending Observations** under `%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox` until Service01 accepts uploads.
- Runs in the Windows notification area so it can keep watching in the background.
- Uses a dark dashboard-style interface with addon, update, WoW path, local data, and recent activity status.
- Installs as a normal per-user Windows application with Start Menu and optional desktop shortcuts.

The companion no longer detects, migrates, deletes, or otherwise manages the retired ZoneQuestGuide addon or its SavedVariables.

The Service01 upload API is still the next backend step. v0.1.3 keeps pending observations local until that API exists and can be validated.

## Windows installation and updates

The recommended download is the GitHub Release asset:

`AzerothQuestingCompanion-Setup.exe`

The installer places the application under `%LOCALAPPDATA%\Programs\Azeroth Questing Companion`, adds a Start Menu shortcut, and offers a desktop shortcut. The app remains self-contained and does not require a separate .NET installation.

The **Check for Updates** action checks both repositories:

- Companion: `Frostcanvas/AzerothQuesting-Companion`
- Addon: `Frostcanvas/AzerothQuesting`

For companion updates, v0.1.3 and later download the published setup package into `%LOCALAPPDATA%\AzerothQuesting\Companion\Updates`, verify the GitHub SHA-256 digest when GitHub supplies one, launch the normal installer silently, close the old client, update the installed files, and restart the client.

This replaces the v0.1.1-v0.1.2 temporary self-copy updater, which executed an unsigned helper from a randomized temporary directory and could look suspicious to endpoint-security products.

Addon updates are reported in the same check. Use **Update Addon** to install the newest addon package. Addon updates are not applied while World of Warcraft is running.

## Security and antivirus notes

The companion is open-source and release packages are built by GitHub Actions. The updater verifies the SHA-256 digest supplied for GitHub Release assets when available.

The Windows binaries are **not code-signed yet**. Endpoint-security or reputation products can still warn about an unsigned new application or installer. Do not disable security software or add broad exclusions just to run the companion. Code signing is the remaining step for establishing a normal Windows publisher identity and reducing reputation-based warnings.

## Privacy

The companion does **not** read World of Warcraft process memory, capture the screen, record gameplay, inspect other applications, or take screenshots. It only works with Azeroth Questing addon files and SavedVariables on disk. Pending observations are stored in the local Outbox and use hashes rather than character/account names in their filenames.

When Service01 uploading is added, the API payload will be limited to the quest/map/phase/instance research data needed by Azeroth Questing. Character names, BattleTags, guild chat, party chat, and unrelated files are not part of the design.

## Build

GitHub Actions publishes a self-contained Windows x64 application directory and packages it with Inno Setup as `AzerothQuestingCompanion-Setup.exe`.

Local development requires the .NET 10 SDK on Windows:

```powershell
dotnet run --project src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj
```

## Repositories

- Addon: `Frostcanvas/AzerothQuesting`
- Companion: `Frostcanvas/AzerothQuesting-Companion`
