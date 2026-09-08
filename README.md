# Azeroth Questing Companion

Windows companion application for the **Azeroth Questing** World of Warcraft addon.

## Current MVP - v0.1.0

The companion currently:

- Detects a World of Warcraft Retail installation on Windows.
- Detects the installed Azeroth Questing addon and its version.
- Installs or updates Azeroth Questing from the public `Frostcanvas/AzerothQuesting` GitHub repository.
- Refuses to change addon files while World of Warcraft is running.
- Backs up an existing Azeroth Questing or legacy ZoneQuestGuide addon before replacing it.
- Migrates `ZoneQuestGuide.lua` SavedVariables to `AzerothQuesting.lua` when the new file does not already exist.
- Watches Azeroth Questing SavedVariables for changes.
- Queues deduplicated local snapshots under `%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox`.
- Runs in the Windows notification area so it can keep watching in the background.

The Service01 upload API is the next backend step. v0.1.0 intentionally keeps observations local until that API exists and can be validated.

## Privacy

The companion does **not** read World of Warcraft process memory. It only works with addon files and Azeroth Questing SavedVariables on disk. The local outbox uses hashes rather than character/account names in snapshot filenames.

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
