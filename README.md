# Azeroth Questing Companion

Windows companion application for the **Azeroth Questing** World of Warcraft addon.

## Development version - v0.1.9-beta.9

The companion currently:

- Detects a World of Warcraft Retail installation on Windows.
- Detects the installed Azeroth Questing addon and its version.
- Checks both the companion and Azeroth Questing addon for updates from GitHub, with **Stable** and **Beta** release channels.
- Installs, updates, or repairs Azeroth Questing from the public `Frostcanvas/AzerothQuesting` GitHub repository.
- Can install, update, or repair Azeroth Questing while World of Warcraft is running; the running game keeps its already-loaded addon code until `/reload` or the next login.
- Backs up the existing Azeroth Questing addon before replacing it.
- Watches only Azeroth Questing SavedVariables for changes.
- Queues deduplicated **Pending Observations** under `%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox` and synchronizes them to the Azeroth Questing Server when synchronization is enabled.
- Uses per-installation bearer registration; no shared server secret is embedded in the public companion.
- Uploads v0.2.32+ structured quest observations with quest/map/evidence, faction, class, level, completion, source, and timestamps; older already-queued snapshots use the compatibility raw endpoint.
- Imports the addon's per-character completed-quest `AQC1` handoff into a local cache and, beginning with Beta 7, creates an identity-free completed-quest contribution for the public Quest Repository when server synchronization is enabled.
- Keeps character and realm only in the player's local completed-quest cache; those identifiers are not copied into the Quest Repository contribution.
- Runs in the Windows notification area so it can keep watching in the background.
- Uses a dark dashboard-style interface with addon, update, WoW path, local data, and recent activity status.
- Shows submitted research data in a privacy-preserving viewer with aggregate counts, recent anonymous observations, class evidence, and active addon/Companion version counts.
- Sends a lightweight authenticated heartbeat every five minutes while server synchronization is enabled so the viewer can report Companions Online using a 10-minute activity window.
- Exports every distinct quest ID collected by Azeroth Questing research to CSV from the Submitted Research Data viewer.
- Installs as a normal Windows desktop application with Start Menu integration, Windows Installed Apps/uninstall registration, and a desktop shortcut selected by default on first install.

The companion does not detect, migrate, delete, watch, or otherwise manage the retired ZoneQuestGuide addon or its SavedVariables.

Azeroth Questing Server synchronization uses the dedicated HTTPS endpoint configured by the application. The player-facing dashboard refers to this service as **Azeroth Questing Server** rather than exposing private FrostLabs infrastructure. The dashboard includes a **Sync Quest Data to Azeroth Questing Server** checkbox, **Sync Now**, and a **Submitted Research Data** viewer. If the server is unavailable, Pending Observations remain in the local Outbox and are retried later. Companion Beta 7 requires Azeroth Questing Server API `0.2.5` or newer for the website-backed Quest Repository view.

## Completed quest repository handoff

With Azeroth Questing Addon `0.3.0-beta.5` or newer, the Companion can import a toon-specific completed-quest snapshot from WoW's per-character SavedVariables. The local cache is written to `completed-quests.json` and a Google-Sheets-friendly `completed-quests.tsv` in the Companion data folder.

Beginning with Companion `0.1.9-beta.7`, the same handoff also feeds the Azeroth Questing website's aggregate Quest Repository automatically when server synchronization is enabled. The Companion extracts only the identity-free `AQC1` payload, normalizes its changing capture timestamp for deduplication, and queues it through the existing retryable Outbox. Character and realm are derived only for the local cache and are never copied into the repository contribution.

The website repository is community-learned data. It is not a complete Blizzard master list of every quest ever shipped.

## Windows installation and updates

The public installer is named:

`AzerothQuestingCompanion-Setup.exe`

Beginning with v0.1.4, the installer targets `Program Files\Azeroth Questing Companion`, requests normal Windows administrator approval, creates Start Menu integration, registers the app for uninstall through Windows Installed Apps, and selects the desktop shortcut task by default on the first install. The app remains self-contained and does not require a separate .NET installation.

The **Check for Updates** action checks both repositories:

- Companion: `Frostcanvas/AzerothQuesting-Companion`
- Addon: `Frostcanvas/AzerothQuesting`

For companion updates, the app downloads the published setup package into `%LOCALAPPDATA%\AzerothQuesting\Companion\Updates`, verifies the GitHub SHA-256 digest when GitHub supplies one, starts the Windows installer with elevation, closes the old client, updates the installed files, and restarts the client.

Addon updates are reported in the same check. Stable mode ignores GitHub prereleases; Beta mode considers both stable releases and GitHub prereleases and selects the newest compatible release. Use **Update Addon** to install the selected channel package. The Companion may update addon files while World of Warcraft is running. WoW continues using the addon code already loaded in memory until the player uses `/reload` or logs out and back in.

## Code signing policy

**Free code signing provided by SignPath.io, certificate by SignPath Foundation.**

See the full [Code signing policy](CODE_SIGNING.md) and [Privacy policy](PRIVACY.md).

Public release builds use GitHub-hosted Windows runners and SignPath's GitHub trusted-build-system integration when signing is configured. A release build signs in two stages: the Companion executable is signed and verified before installer creation, then the completed installer is signed and verified before publication.

While trusted signing is unavailable, Beta Pre-releases may be published as explicitly unsigned testing builds. Stable Companion releases remain blocked unless signing is available and valid. Windows SmartScreen or endpoint-security products may warn about unsigned/reputation-new Beta builds; do not disable security software or add broad exclusions just to run the companion.

## Privacy

The companion does **not** read World of Warcraft process memory, capture the screen, record gameplay, inspect other applications, or take screenshots. It only works with Azeroth Questing addon files and Azeroth Questing SavedVariables on disk.

The local completed-quest cache may contain character and realm because it is for the player's own use. The website repository contribution deliberately excludes both. Public Quest Repository data is aggregate-only and does not expose character names, realms, installation IDs, bearer tokens, peer sender identities, or raw SavedVariables. See `PRIVACY.md` for the exact behavior.

## Build

GitHub Actions publishes a self-contained Windows x64 application directory and packages it with Inno Setup as `AzerothQuestingCompanion-Setup.exe`.

Local development requires the .NET 10 SDK on Windows:

```powershell
dotnet run --project src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj
```

## Repositories

- Addon: `Frostcanvas/AzerothQuesting`
- Companion: `Frostcanvas/AzerothQuesting-Companion`
- Website: `Frostcanvas/AzerothQuestingwebsite`

## AQM2 map identity evidence

Companion 0.1.9-beta.12 can prefer the add-on AQM2 record over its same-key AQM1 fallback and upload privacy-safe Blizzard map evidence such as parent map, map-art ID, scenario/step, difficulty, coordinates, and previous UiMapID. Player identity and local filesystem paths are not added to the structured map payload. AQM2 upload requires Azeroth Questing Server 0.2.12 / schema 5 or newer.
