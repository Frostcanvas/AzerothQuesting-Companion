# Azeroth Questing Companion

Windows companion application for the **Azeroth Questing** World of Warcraft addon.

## Development version - v0.1.8-beta.2

The latest public release remains v0.1.3 until trusted Windows code signing is approved and configured through SignPath Foundation.

The companion currently:

- Detects a World of Warcraft Retail installation on Windows.
- Detects the installed Azeroth Questing addon and its version.
- Checks both the companion and Azeroth Questing addon for updates from GitHub, with **Stable** and **Beta** release channels.
- Installs, updates, or repairs Azeroth Questing from the public `Frostcanvas/AzerothQuesting` GitHub repository.
- Refuses to change addon files while World of Warcraft is running.
- Backs up the existing Azeroth Questing addon before replacing it.
- Watches only the current `AzerothQuesting.lua` SavedVariables data for changes.
- Queues deduplicated **Pending Observations** under `%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox` and synchronizes them to the Azeroth Questing Server when synchronization is enabled.
- Uses per-installation bearer registration; no shared server secret is embedded in the public companion.
- Uploads new v0.2.32+ structured quest observations with quest/map/evidence, faction, class, level, completion, source, and timestamps; older already-queued snapshots use the compatibility raw endpoint.
- Runs in the Windows notification area so it can keep watching in the background.
- Uses a dark dashboard-style interface with addon, update, WoW path, local data, and recent activity status.
- Shows submitted research data in a privacy-preserving viewer with aggregate counts, recent anonymous observations, class evidence, and active addon/Companion version counts.
- Installs as a normal Windows desktop application with Start Menu integration, Windows Installed Apps/uninstall registration, and a desktop shortcut selected by default on first install.

The companion does not detect, migrate, delete, watch, or otherwise manage the retired ZoneQuestGuide addon or its SavedVariables.

Azeroth Questing Server synchronization is configurable internally. The player-facing dashboard does not display the private FrostLabs host name or LAN address. It includes a **Sync Pending Observations to Azeroth Questing Server** checkbox, **Sync Now**, and a **Submitted Research Data** viewer. If the server is unavailable, Pending Observations remain in the local Outbox and are retried later. The Submitted Research Data viewer requires Azeroth Questing Server API `0.2.1` or newer. External testers still require the future HTTPS endpoint before server synchronization can work outside the FrostLabs LAN.

## Windows installation and updates

The public installer is named:

`AzerothQuestingCompanion-Setup.exe`

Beginning with v0.1.4, the installer targets `Program Files\Azeroth Questing Companion`, requests normal Windows administrator approval, creates Start Menu integration, registers the app for uninstall through Windows Installed Apps, and selects the desktop shortcut task by default on the first install. The app remains self-contained and does not require a separate .NET installation.

The **Check for Updates** action checks both repositories:

- Companion: `Frostcanvas/AzerothQuesting-Companion`
- Addon: `Frostcanvas/AzerothQuesting`

For companion updates, the app downloads the published setup package into `%LOCALAPPDATA%\AzerothQuesting\Companion\Updates`, verifies the GitHub SHA-256 digest when GitHub supplies one, starts the Windows installer with elevation, closes the old client, updates the installed files, and restarts the client.

This replaces the v0.1.1-v0.1.2 temporary self-copy updater, which executed an unsigned helper from a randomized temporary directory and could be blocked or mistaken for malware by endpoint-security products. v0.1.4 also attempts to clean up those legacy temporary updater files when they are no longer locked.

Addon updates are reported in the same check. Stable mode ignores GitHub prereleases; Beta mode considers both stable releases and GitHub prereleases and selects the newest compatible release. Use **Update Addon** to install the selected channel package. Addon updates are not applied while World of Warcraft is running.

## Code signing policy

**Free code signing provided by SignPath.io, certificate by SignPath Foundation.**

See the full [Code signing policy](CODE_SIGNING.md) and [Privacy policy](PRIVACY.md).

The repository is being prepared for SignPath Foundation's free open-source code-signing program. Public release builds use GitHub-hosted Windows runners and SignPath's GitHub trusted-build-system integration so signed binaries can be tied back to the repository, workflow run, and commit that built them.

A release build signs in two stages:

1. Build and submit `AzerothQuestingCompanion.exe` to SignPath.
2. Verify the signed executable and include it in the Windows installer.
3. Build and submit `AzerothQuestingCompanion-Setup.exe` to SignPath.
4. Verify the signed installer before publishing the GitHub Release.

This two-stage process ensures that both the installed executable and the installer itself are signed.

After SignPath Foundation approves the project, the GitHub repository will need:

- Secret: `SIGNPATH_API_TOKEN`
- Repository variable: `SIGNPATH_ORGANIZATION_ID`
- Repository variable: `SIGNPATH_PROJECT_SLUG`
- Repository variable: `SIGNPATH_SIGNING_POLICY_SLUG`
- Repository variable: `SIGNPATH_EXECUTABLE_ARTIFACT_CONFIGURATION_SLUG`
- Repository variable: `SIGNPATH_INSTALLER_ARTIFACT_CONFIGURATION_SLUG`

The SignPath GitHub App must also be allowed to access this repository for trusted-build origin verification.

Until SignPath approval and configuration are complete, development artifacts remain unsigned and Windows SmartScreen or endpoint-security products can still warn about them. Do not disable security software or add broad exclusions just to run the companion.

## Privacy

The companion does **not** read World of Warcraft process memory, capture the screen, record gameplay, inspect other applications, or take screenshots. It only works with Azeroth Questing addon files and `AzerothQuesting.lua` SavedVariables on disk. Pending observations are stored in the local Outbox and use hashes rather than character/account names in their filenames.

Azeroth Questing Server synchronization can upload Pending Observations to the private FrostLabs API. Synchronization can be disabled at any time from the dashboard; failed uploads remain local for retry. The Submitted Research Data viewer uses the same per-installation authentication to read privacy-preserving aggregates and anonymous observation rows. See `PRIVACY.md` for the exact observation fields.

## Build

GitHub Actions publishes a self-contained Windows x64 application directory and packages it with Inno Setup as `AzerothQuestingCompanion-Setup.exe`.

Local development requires the .NET 10 SDK on Windows:

```powershell
dotnet run --project src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj
```

## Repositories

- Addon: `Frostcanvas/AzerothQuesting`
- Companion: `Frostcanvas/AzerothQuesting-Companion`
