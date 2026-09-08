# Azeroth Questing Companion

Windows companion application for the **Azeroth Questing** World of Warcraft addon.

## Current client - v0.1.2

The companion currently:

- Detects a World of Warcraft Retail installation on Windows.
- Detects the installed Azeroth Questing addon and its version.
- Checks both the companion and Azeroth Questing addon for updates from GitHub.
- Automatically downloads, applies, and restarts the companion when a newer public companion release is available.
- Installs, updates, or repairs Azeroth Questing from the public `Frostcanvas/AzerothQuesting` GitHub repository.
- Refuses to change addon files while World of Warcraft is running.
- Backs up the existing Azeroth Questing addon before replacing it.
- Watches Azeroth Questing SavedVariables for changes.
- Queues deduplicated pending observations under `%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox` until Service01 accepts uploads.
- Runs in the Windows notification area so it can keep watching in the background.
- Uses a dark dashboard-style interface with addon, update, WoW path, local data, and recent activity status.

The companion no longer detects, migrates, deletes, or otherwise manages the retired ZoneQuestGuide addon or its SavedVariables.

The Service01 upload API is still the next backend step. v0.1.2 keeps pending observations local until that API exists and can be validated.

## Updates

The **Check for Updates** action checks both repositories:

- Companion: `Frostcanvas/AzerothQuesting-Companion`
- Addon: `Frostcanvas/AzerothQuesting`

If a newer companion release is available, the client downloads `AzerothQuestingCompanion-win-x64.zip`, verifies the GitHub SHA-256 digest when GitHub supplies one, launches a temporary updater copy, exits, replaces the running executable, and restarts automatically.

Addon updates are reported in the same check. Use **Update Addon** to install the newest addon package. Addon updates are not applied while World of Warcraft is running.

GitHub Actions creates the self-contained Windows x64 package and can publish a versioned GitHub Release when a release commit is pushed.

## Privacy

The companion does **not** read World of Warcraft process memory. It only works with Azeroth Questing addon files and SavedVariables on disk. Pending observations are stored in the local Outbox and use hashes rather than character/account names in their filenames.

When Service01 uploading is added, the API payload will be limited to the quest/map/phase/instance research data needed by Azeroth Questing. Character names, BattleTags, guild chat, party chat, and unrelated files are not part of the design.

## Build

The GitHub Actions workflow publishes a self-contained Windows x64 build. The resulting `AzerothQuestingCompanion.exe` does not require a separate .NET installation.

Local development requires the .NET 10 SDK on Windows:

```powershell
dotnet run --project src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj
```

## Repositories

- Addon: `Frostcanvas/AzerothQuesting`
- Companion: `Frostcanvas/AzerothQuesting-Companion`
