# Changelog

## 0.1.9 Beta 4 - September 9, 2026 - Available on GitHub Pre-release

- Added an automatic GitHub update check when the Companion finishes starting. The same Stable/Beta channel rules used by the manual **Check for Updates** button are used for this startup check.
- Added a background update check every 30 minutes while the Companion remains open. Background checks update the dashboard/activity status without showing the normal no-update information dialog.
- Added an update check after a manual **Sync Now** completes and after automatic Azeroth Questing Server synchronization successfully clears one or more Pending Observation files. This lets active research sessions notice newly published addon or Companion betas without waiting for the 30-minute timer.
- Kept Companion self-update behavior unchanged when a newer eligible Companion release is found: the installer is staged and launched using the existing updater flow. Addon updates remain player-controlled through **Update Addon** / **Install / Update Addon**.
- Advanced the Companion test build to `0.1.9-beta.4` with Windows file version `0.1.8.9` because Beta 3 had already been handed off before these automatic update-check changes.
- The previous Beta 3 release attempt built the application successfully but stopped at the SignPath configuration validation step before signing, installer creation, or GitHub Pre-release publication. No signed Beta 3 installer was published.
- Changed the GitHub release pipeline so Companion Beta builds may be published as explicitly unsigned GitHub Pre-releases while SignPath approval/configuration is pending. The updater-visible asset remains `AzerothQuestingCompanion-Setup.exe`, so Beta-channel Companion installations can exercise normal self-update behavior without waiting for SignPath.
- Kept Stable publication protected: a Stable Companion release still fails closed when SignPath configuration is missing and cannot be published unsigned. When SignPath becomes available, Beta and Stable releases can use the signed path without changing the updater asset name.

Beta 4 still requires Windows testing. Verify the Companion checks for updates at startup without a no-update popup, repeats the check after roughly 30 minutes, checks again after a successful data sync, still allows the manual **Check for Updates** action, does not auto-install addon updates, and can discover, stage, install, restart into, and report the unsigned `0.1.9-beta.4` GitHub Pre-release through the Beta channel. Windows may show SmartScreen or publisher warnings because this temporary Beta installer is unsigned. No signing success is claimed. Stable Companion releases still require SignPath and must never be published unsigned.

## 0.1.9 Beta 3 - September 9, 2026

- Fixed the final server-endpoint fallback in `Service01Client.NormalizeBaseUrl`. If the configured endpoint is blank at runtime, the Companion now falls back to `https://aq.frostlabs.dev` through `SettingsService.DefaultServiceBaseUrl` instead of the retired private LAN address.
- Kept the existing one-time migration from the exact legacy LAN endpoint to the public HTTPS endpoint and preserved manually configured custom endpoints.
- Updated the Companion GitHub API user-agent version from `0.1.8` to the current `0.1.9` train.
- Advanced the next testable Companion build to `0.1.9-beta.3` because Beta 2 had already been handed off for testing before this endpoint fallback correction.
- Recorded the actual public-ingress test status: `https://aq.frostlabs.dev/api/v1/status` was successfully reached from a phone on cellular data and returned Azeroth Questing Server API `0.2.4`; the public `/research` page did not expose the internal dashboard. This confirms the public server route, not a full Companion synchronization test.

Beta 3 still requires Windows testing. Verify the repaired Azeroth Questing icon on installed Windows surfaces, select the Beta channel, confirm the Companion uses `https://aq.frostlabs.dev`, then test registration, heartbeat, Submitted Research Data, Pending Observations synchronization, Companion self-update, and addon update from an outside network. Do not treat the cellular API test as proof that the Windows Companion path has passed. The WoW addon version is managed independently.


## 0.1.9 Beta 2 - September 8, 2026

- Changed the default Azeroth Questing Server endpoint from the private FrostLabs LAN address to the dedicated public HTTPS endpoint `https://aq.frostlabs.dev`, enabling controlled outside-network beta testing once the public ingress is deployed.
- Added a one-time settings migration for installations still using the exact legacy LAN default. Existing manually configured custom endpoints are preserved.
- Kept the existing anonymous per-installation bearer registration/authentication model; no shared backend secret was added to the Companion.
- Kept the public ingress limited to the required Companion `/api/v1` endpoints. The full `/research` website remains LAN-only.
- Bumped the development Companion version to `0.1.9-beta.2` and the numeric Windows file version to `0.1.8.7`.

This beta still needs Windows and outside-network testing. Cloudflare DNS and the Services01 Caddy route must be deployed before the public endpoint can work. After that, verify an outside-network Companion can register, heartbeat, view Submitted Research Data, and sync Pending Observations. Do not consider the external route tested until that real test is performed. The Azeroth Questing addon was not changed.

## 0.1.9 Beta 1 - September 8, 2026

- Started the `0.1.9` beta train using canonical GitHub/updater version `0.1.9-beta.1` while presenting it to players as **0.1.9 Beta 1** inside the Companion.
- Added player-facing version cleanup so beta and release-candidate strings shown in dashboard controls, status text, activity history, and Companion dialogs use readable labels such as `0.1.9 Beta 1` and `0.1.9 RC 1` instead of raw prerelease syntax.
- Applied the same readable beta formatting to addon and Companion versions shown in the Submitted Research Data viewer while leaving the underlying version values suitable for update comparison and research/version tracking.
- Changed the Windows numeric file-version train to `0.1.8.6` for this beta. This keeps the hidden numeric build moving forward from the previous `0.1.8.5` beta build while leaving room for the golden `0.1.9` release to use the newer numeric file version `0.1.9.0`.
- Kept GitHub tags/releases and updater comparison on canonical prerelease versions such as `0.1.9-beta.1`; the friendly **Beta 1** wording is a player-facing presentation layer only.
- Fixed the Companion application icon so installed desktop and Start Menu shortcuts, the main window, Submitted Research Data window, notification-area icon, installer, and Windows uninstall entry use the Azeroth Questing icon instead of the generic Windows application icon. Recovered the existing Azeroth Questing artwork from the damaged icon resource and rebuilt it as a validated multi-resolution Windows `.ico`; existing desktop shortcuts are refreshed during an upgrade when they are already present without creating a shortcut for players who previously chose not to have one.

This beta still needs Windows testing. Verify that the Companion displays **0.1.9 Beta 1** in player-facing surfaces, still compares GitHub prereleases correctly, and continues reporting/updating normally. Also install it over an older Companion build that already has a desktop shortcut and verify the Azeroth Questing icon appears on the desktop shortcut, Start Menu shortcut, main/title-bar window, Submitted Research Data window, notification area, installer, and Windows Installed Apps/uninstall entry. The Azeroth Questing addon itself was not changed by this Companion icon fix.

## 0.1.8 Beta 5 - September 8, 2026

- Extended the Apple-style Beta train behavior to the Azeroth Questing addon as well as the Companion.
- When the Beta channel is selected and a prerelease addon is already installed, an older Stable addon release is no longer treated as the current channel version and is not installed as a downgrade.
- Beta-channel addon users now wait for a newer prerelease or the same/newer base version's Stable (golden) release before moving forward.
- Added an install-time safeguard so an unpublished/manual addon beta cannot be accidentally replaced by an older Stable package through the Companion.
- Kept Stable mode restricted to normal GitHub Releases. Beta mode continues to consider both GitHub Pre-releases and later Stable releases.
- Bumped the development Companion version to `0.1.8-beta.5`.

This beta still needs Windows testing. Automatic beta-to-beta Companion updating also still requires the beta installer to exist as a signed GitHub Pre-release; GitHub Actions development artifacts are not part of the in-app update feed. No Azeroth Questing addon code was changed, so the current addon remains `0.2.32` until an addon beta is actually published.

## 0.1.8 Beta 4 - September 8, 2026

- Changed Beta-channel Companion update status to stay on the installed beta train instead of presenting an older Stable release as the current Beta-channel version.
- Once a Companion is running a prerelease, the next eligible automatic update is a newer GitHub Pre-release or a Stable release with the same/newer base version (the golden release). Older Stable releases are ignored for Beta-channel status.
- Kept Stable mode restricted to normal GitHub Releases and Beta mode able to consume both GitHub Pre-releases and later Stable releases.
- Clarified the distribution requirement: an Actions development artifact is not visible to the in-app updater. A beta installer must be published as a GitHub Pre-release with the expected `AzerothQuestingCompanion-Setup.exe` asset before Beta-channel clients can discover it.
- Bumped the development Companion version to `0.1.8-beta.4`.

This beta still needs Windows testing. The current public release feed has no Companion prerelease yet, so automatic beta-to-beta updating cannot be validated until a signed GitHub Pre-release is published under the existing code-signing policy. The addon itself was not changed for this fix.

## 0.1.8 Beta 3 - September 8, 2026

- Added a lightweight authenticated Companion heartbeat every five minutes while Azeroth Questing Server synchronization is enabled, allowing the research dashboard to show **Companions Online** as installations seen within the last 10 minutes.
- Heartbeats report only the existing anonymous installation identity plus Companion version, installed addon version, Stable/Beta update channel, and last-seen time; they do not add character, account, Windows-user, or gameplay identity data.
- Added **Export Collected Quests** to the Submitted Research Data viewer. The export saves every distinct quest ID currently collected by Azeroth Questing research as CSV with observed maps, factions, evidence types, classes, observation counts, installation counts, completion evidence, and first/last receive times.
- Clarified that the collected-quest CSV is not a complete catalog of every quest shipped in World of Warcraft; it contains the complete set observed by the Azeroth Questing research system.
- Bumped the development Companion version to `0.1.8-beta.3` and requires Azeroth Questing Server API `0.2.2` for heartbeat/online counts and collected-quest export.

This beta still needs testing on the player's Windows installation and against the live Azeroth Questing Server API `0.2.2`. A successful GitHub Actions build confirms compilation/packaging only and does not count as live Windows, server, or in-game testing. The WoW addon itself was not changed for this feature.

## 0.1.8 Beta 2 - September 8, 2026

- Added a **Submitted Research Data** viewer in the Companion with total observations, unique quests, active installation counts, this installation's contribution count, recent anonymous observations, class-evidence summaries, and active addon/Companion version counts.
- Added authenticated Companion support for the Azeroth Questing Server research-dashboard API. The viewer does not expose installation IDs, account/character identifiers, bearer tokens, client observation keys, or raw SavedVariables payloads.
- Restored the visible **Stable / Beta** update-channel control to the current dashboard so the existing channel-aware GitHub release logic is accessible to players.
- Removed remaining player-facing `Service01` and private endpoint wording from the dashboard, sync status, dialogs, and privacy copy.
- Fixed addon update status so an installed version that is newer than the selected channel is not incorrectly presented as an available update/downgrade.
- Bumped the development Companion version to `0.1.8-beta.2`.
- Requires Azeroth Questing Server API `0.2.1` for the Submitted Research Data viewer.

This beta still needs testing on the player's Windows installation and against the live Azeroth Questing Server API `0.2.1`. A successful GitHub Actions build only confirms that the Windows application and installer compile/package; it does not count as live Windows, server, or in-game testing. The addon itself was not changed for this feature.

## 0.1.8 Beta 1 - September 8, 2026

- Fixed Companion update comparison so semantic prerelease versions such as `0.1.8-beta.1` are recognized as newer than older stable or beta builds instead of being rejected by `System.Version` parsing.
- Changed Beta-channel release discovery for both **Azeroth Questing Companion** and the **Azeroth Questing addon** to scan GitHub Releases, include both normal public releases and GitHub prereleases, and select the highest semantic version with a usable release asset.
- Kept Stable mode restricted to normal public GitHub releases; addon source from `main` is still used only as a fallback when the addon repository has no usable release package.
- Added prerelease ordering so, for the same base version, a stable release sorts newer than its beta build and numbered beta builds sort correctly (`beta.2` newer than `beta.1`).
- Changed the Companion version display to use its informational version so beta builds can show the full prerelease label instead of only the numeric assembly version.
- Updated the Windows installer build to keep a numeric Windows file version while allowing a player-facing prerelease product version.
- Updated the signed-release workflow so versions containing a prerelease suffix are published as GitHub **Pre-release** releases instead of accidentally becoming Stable releases.
- Bumped the development Companion version to `0.1.8-beta.1`.

This beta update still needs testing on the player's Windows installation. In particular, publish a signed GitHub prerelease after SignPath approval, then verify a Companion on the Beta channel detects and installs it while Stable ignores it. Also verify Beta detects a prerelease `AzerothQuesting.zip` addon package when one is published. The unsigned development installer is for local testing only while SignPath Foundation approval is pending.

## 0.1.7 Beta - September 8, 2026

- Removed the private FrostLabs server hostname/IP details and `Service01` wording from player-facing Companion controls, status messages, activity entries, and sync dialogs.
- Changed player-facing synchronization wording to **Azeroth Questing Server** while retaining the existing private test endpoint internally so current LAN synchronization continues to work until the public HTTPS endpoint is deployed.
- Bumped the development Companion version to 0.1.7.

The v0.1.7 development installer compiled and packaged successfully in GitHub Actions. The internal endpoint remained unchanged, so external testers still require the future public HTTPS endpoint before server synchronization can work outside the FrostLabs LAN. The build remained unsigned while SignPath Foundation approval was pending.

## 0.1.6 Beta - September 8, 2026

- Added a **Stable / Beta** update-channel selector directly to the Companion top bar.
- Changed the update channel to control both Azeroth Questing Companion updates and Azeroth Questing addon updates together.
- Added beta-channel GitHub release discovery. Stable mode continues to use normal public releases only; Beta mode also considers GitHub prereleases and falls back to the newest stable release when no prerelease is available.
- Kept the same addon folder and SavedVariables path when switching channels so beta testing does not create a second AzerothQuesting installation.
- Restored the working Azeroth Questing executable icon resource and adjusted installer packaging so the application/desktop shortcut can keep the custom icon without Inno Setup rejecting the installer icon resource.
- Bumped the development Companion version to 0.1.6.

The v0.1.6 beta-channel build compiled and packaged successfully in GitHub Actions. It has not yet been tested on the player's Windows installation. Test switching between Stable and Beta, run **Check for Updates**, verify Stable ignores prereleases, verify Beta can select prereleases when they exist, and verify addon install/update still refuses to replace files while WoW is running. The development installer is still unsigned while SignPath Foundation approval is pending, so Windows SmartScreen or endpoint security may warn about it. No public v0.1.6 GitHub Release has been published.

## 0.1.5 - September 8, 2026

- Added Service01 synchronization for Pending Observations using the private Azeroth Questing API on the FrostLabs LAN.
- Added anonymous per-installation registration so each client receives its own bearer token; no shared backend secret is embedded in the public companion.
- Added structured v0.2.32+ observation uploads for quest/map/evidence, faction, class ID/token, level, completion, source, timestamp, and addon/client versions.
- Kept compatibility upload support for already-queued pre-v0.2.32 SavedVariables snapshots so existing research is not silently discarded.
- Added automatic retry behavior: files are deleted from the local Outbox only after Service01 accepts them; unavailable/failed uploads remain local.
- Added **Sync Now** controls to the dashboard, sidebar, and tray menu plus a **Sync Pending Observations to Service01** opt-out checkbox.
- Changed the Sync Status card to report the Service01 API state rather than the previous placeholder.
- Updated the privacy policy and README before enabling network transfer behavior.

The v0.1.5 Service01 client code has not yet been tested against the live Services01 host or on the player's Windows installation. The GitHub development build must compile successfully, then the Service01 API must be deployed and health-checked before end-to-end synchronization can be considered working. Public signed publication still waits for SignPath Foundation approval/configuration; the latest public release remains v0.1.3.

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

The v0.1.4 source still needs testing on the player's Windows installation. SignPath Foundation approval and the resulting SignPath project/API configuration are still required before v0.1.4 can be published as a signed public release. Until trusted signing is active, development artifacts remain unsigned and Windows SmartScreen or endpoint protection can still warn about it. Service01 uploading remains disabled; Pending Observations stay local.

## 0.1.3 - September 8, 2026

- Added a normal Windows installer, `AzerothQuestingCompanion-Setup.exe`, for a proper installed desktop application.
- Added Start Menu integration and a desktop-shortcut option.
- Changed the companion publish format from a compressed single-file executable to a conventional self-contained application directory packaged by Inno Setup.
- Changed companion self-updates to download and run the published installer from the companion's local update directory instead of copying the running executable into a randomized Windows Temp folder.
- Kept SHA-256 verification when GitHub supplies a digest for the companion update package.
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
- Added SHA-256 verification when GitHub supplies a digest for the companion update package.
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
