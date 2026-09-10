# Azeroth Questing Companion Privacy Policy

Last updated: September 8, 2026

Azeroth Questing Companion is designed to work with the Azeroth Questing World of Warcraft addon while minimizing the data it accesses.

## What the companion reads

The companion may read:

- The Azeroth Questing addon files under the selected World of Warcraft Retail installation.
- `AzerothQuesting.lua` SavedVariables files under World of Warcraft's `WTF/Account/.../SavedVariables` folders.
- The installed addon version and the configured World of Warcraft Retail path.

The companion does not read the retired `ZoneQuestGuide.lua` SavedVariables file.

## What the companion does not do

The companion does not:

- Read World of Warcraft process memory.
- Capture the screen or take screenshots.
- Record gameplay, audio, keyboard input, or mouse input.
- Read unrelated files or other applications.
- Read BattleTags, guild chat, party chat, or unrelated chat messages as part of its intended data collection.

## Pending Observations

When Azeroth Questing SavedVariables change, the companion may create a deduplicated local copy called a **Pending Observation**. These files are stored under:

`%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox`

Pending Observation filenames use a timestamp and SHA-256 content hash rather than a character or account name.

## Network communication

The companion can synchronize Pending Observations to the Azeroth Questing Server over HTTPS at `https://aq.frostlabs.dev`. The player-facing application refers to this service as **Azeroth Questing Server** rather than displaying the private FrostLabs LAN address. The dashboard provides a **Sync Pending Observations to Azeroth Questing Server** checkbox and a manual **Sync Now** action. Turning the checkbox off keeps new Pending Observations local.

For Azeroth Questing v0.2.32 and newer, structured uploads can include only addon-produced research fields: observation key, local/anonymous-peer source, quest ID, map ID, evidence type (`seen`, `available`, `offered`, `accepted`, `active`, or `turnedIn`), faction, World of Warcraft class ID/token, character level, quest completion state, observation timestamp, addon version, and companion version. The intended upload does not include character name, realm, BattleTag, guild, chat, party-member identity, GUID, screenshots, or gameplay recordings.

The Azeroth Questing Server assigns a random per-installation bearer token. The public companion does not contain a shared server secret. The server stores only the SHA-256 hash of the bearer token. If synchronization fails, the companion keeps the Pending Observation locally instead of deleting it.

Older Pending Observation files created before the structured v0.2.32 handoff can be sent through a compatibility raw SavedVariables endpoint so existing queued research is not silently discarded. Those files are stored as data and are not executed as Lua by the server.

The companion also accesses GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates.

While Azeroth Questing Server synchronization is enabled, the Companion sends a lightweight authenticated heartbeat every five minutes. The heartbeat updates the anonymous installation's last-seen time and may include Companion version, installed addon version, and Stable/Beta update channel so the research dashboard can report active-version counts and **Companions Online**. It does not include character name, account/BattleTag, Windows username, screenshots, chat, gameplay activity, or IP history. A Companion counts as online when the server has received a heartbeat within the last 10 minutes; this is an activity window rather than a permanent network connection.

The public HTTPS ingress is limited to the Companion API routes required for registration, heartbeat, observation synchronization, Submitted Research Data, and collected-quest export. The full `/research` website and its unauthenticated website CSV export remain LAN-only and are not intended to be exposed through the public Companion hostname.

## Submitted Research Data viewer

The Companion can use its per-installation bearer token to read a privacy-preserving research summary from the Azeroth Questing Server. The viewer may show aggregate observation and quest counts, this installation's own contribution count, recent anonymous structured observations, class-evidence summaries, and active addon/Companion version counts. The viewer can also export every distinct quest ID currently collected by the research system as CSV.

The viewer does **not** receive installation IDs, client observation keys, bearer-token hashes, raw SavedVariables payloads, character names, account identifiers, BattleTags, realm identifiers, screenshots, gameplay recordings, or IP history.

## Data deletion

Users can remove locally queued Pending Observations by deleting files from the companion Outbox after closing the companion. Uninstalling the application may not automatically remove user data stored under Local AppData.

## Source code

Azeroth Questing Companion is open source under the MIT License. The source code is available at:

`https://github.com/Frostcanvas/AzerothQuesting-Companion`


## Per-character completed quest data (Beta 6)

Azeroth Questing Companion can read the addon's per-character completed-quest SavedVariables handoff. Character and realm are derived locally from the World of Warcraft folder path so the addon wire payload does not need to contain those identifiers. This completed-quest cache is stored only in the Companion local data folder (`completed-quests.json` and `completed-quests.tsv`) and is not added to Pending Observations or uploaded to Azeroth Questing Server by this feature.
