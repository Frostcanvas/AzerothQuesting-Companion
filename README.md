# Azeroth Questing Companion

Windows companion application for the **Azeroth Questing** World of Warcraft addon.

## Development version - v0.1.4

The latest public release remains v0.1.3 until trusted Windows code signing is configured for the GitHub release pipeline.

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
- Installs as a normal Windows desktop application with Start Menu integration, Windows Installed Apps/uninstall registration, and a desktop shortcut selected by default on first install.

The companion no longer detects, migrates, deletes, or otherwise manages the retired ZoneQuestGuide addon or its SavedVariables.

The Service01 upload API is still a backend step. Pending observations remain local until that API exists and can be validated.

## Windows installation and updates

The public installer is named:

`AzerothQuestingCompanion-Setup.exe`

Beginning with v0.1.4, the installer targets `Program Files\Azeroth Questing Companion`, requests normal Windows administrator approval, creates Start Menu integration, registers the app for uninstall through Windows Installed Apps, and selects the desktop shortcut task by default on the first install. The app remains self-contained and does not require a separate .NET installation.

The **Check for Updates** action checks both repositories:

- Companion: `Frostcanvas/AzerothQuesting-Companion`
- Addon: `Frostcanvas/AzerothQuesting`

For companion updates, the app downloads the published setup package into `%LOCALAPPDATA%\AzerothQuesting\Companion\Updates`, verifies the GitHub SHA-256 digest when GitHub supplies one, starts the Windows installer with elevation, closes the old client, updates the installed files, and restarts the client.

This replaces the v0.1.1-v0.1.2 temporary self-copy updater, which executed an unsigned helper from a randomized temporary directory and could be blocked or mistaken for malware by endpoint-security products. v0.1.4 also attempts to clean up those legacy temporary updater files when they are no longer locked.

Addon updates are reported in the same check. Use **Update Addon** to install the newest addon package. Addon updates are not applied while World of Warcraft is running.

## Windows code signing

The GitHub Actions workflow is prepared for Microsoft Azure Artifact Signing. When configured, the pipeline Authenticode-signs both `AzerothQuestingCompanion.exe` and `AzerothQuestingCompanion-Setup.exe`, verifies the signatures, and refuses to publish a public release if signing is unavailable.

Required GitHub configuration:

- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- Repository variables: `AZURE_ARTIFACT_SIGNING_ENDPOINT`, `AZURE_ARTIFACT_SIGNING_ACCOUNT`, `AZURE_ARTIFACT_SIGNING_PROFILE`

The Azure identity used by GitHub Actions needs access to the selected Artifact Signing certificate profile.

Until that trusted signing configuration exists, development artifacts are unsigned and Windows SmartScreen or endpoint-security products can still warn about them. Do not disable security software or add broad exclusions just to run the companion.

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
