# Azeroth Questing Companion Privacy Policy

Last updated: September 11, 2026

Azeroth Questing Companion is designed to work with the Azeroth Questing World of Warcraft addon while minimizing the data it accesses and keeping player identity out of public research views.

## What the companion reads

The companion may read:

- The Azeroth Questing addon files under the selected World of Warcraft Retail installation.
- `AzerothQuesting.lua` SavedVariables files under World of Warcraft's `WTF/Account/.../SavedVariables` folders.
- The addon's per-character `AzerothQuesting.lua` SavedVariables files for the completed-quest handoff.
- The installed addon version and the configured World of Warcraft Retail path.

The companion does not read the retired `ZoneQuestGuide.lua` SavedVariables file.

## What the companion does not do

The companion does not:

- Read World of Warcraft process memory.
- Capture the screen or take screenshots.
- Record gameplay, audio, keyboard input, or mouse input.
- Read unrelated files or other applications.
- Read BattleTags, guild chat, party chat, or unrelated chat messages as part of its intended data collection.

## Pending Observations and Quest Repository contributions

When Azeroth Questing SavedVariables change, the companion may create a deduplicated local copy called a **Pending Observation**. These files are stored under:

`%LOCALAPPDATA%\AzerothQuesting\Companion\Outbox`

Pending Observation filenames use a timestamp and SHA-256 content hash rather than a character or account name.

Beginning with Companion Beta 7, an addon completed-quest `AQC1` handoff can also create an identity-free Quest Repository contribution in the same retryable Outbox. Before that contribution is queued, the Companion extracts only the `AQC1` data and does not copy the local SavedVariables path, character name, or realm into the queued payload. The changing capture timestamp is normalized before hashing so repeatedly saving an unchanged completed-quest set does not create a new contribution every time.

The local completed-quest cache is separate from the public contribution. `completed-quests.json` and `completed-quests.tsv` may contain character and realm because those files are for the player's own local use. That local identity is not included in the repository contribution.

## Network communication

The companion can synchronize Pending Observations and identity-free Quest Repository contributions to the **Azeroth Questing Server** over HTTPS when server synchronization is enabled. The dashboard provides a **Sync Quest Data to Azeroth Questing Server** checkbox and a manual **Sync Now** action. Turning synchronization off keeps newly queued data local.

For Azeroth Questing v0.2.32 and newer, structured research uploads can include only addon-produced research fields: observation key, local/anonymous-peer source, quest ID, cached quest title when available, map ID, evidence type (`seen`, `available`, `offered`, `accepted`, `active`, or `turnedIn`), faction, World of Warcraft class ID/token, character level, quest completion state, observation timestamp, addon version, and companion version. The intended upload does not include character name, realm, BattleTag, guild, chat, party-member identity, GUID, screenshots, or gameplay recordings.

Quest Repository completed-quest contributions can include the completed quest IDs and cached quest titles exposed by the addon together with faction, class, level, and addon version from the `AQC1` handoff. They do not include character name, realm, account name, BattleTag, guild, chat, GUID, peer sender identity, local file path, screenshots, or gameplay recordings. The public website receives only aggregate repository results; it does not receive the raw contribution or installation identity.

The Azeroth Questing Server assigns a random per-installation bearer token. The public companion does not contain a shared server secret. The server stores only the SHA-256 hash of the bearer token. If synchronization fails, the companion keeps the Pending Observation locally instead of deleting it.

Older Pending Observation files created before the structured v0.2.32 handoff can be sent through a compatibility raw SavedVariables endpoint so existing queued research is not silently discarded. Those files are stored as data and are not executed as Lua by the server. The identity-free completed-quest repository contribution uses this authenticated compatibility transport in Companion Beta 7.

The companion also accesses GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates.

While Azeroth Questing Server synchronization is enabled, the Companion sends a lightweight authenticated heartbeat every five minutes. The heartbeat updates the anonymous installation's last-seen time and may include Companion version, installed addon version, and Stable/Beta update channel so the research dashboard can report active-version counts and **Companions Online**. It does not include character name, account/BattleTag, Windows username, screenshots, chat, gameplay activity, or IP history. A Companion counts as online when the server has received a heartbeat within the last 10 minutes; this is an activity window rather than a permanent network connection.

## Public Quest Repository

The public Quest Repository is an aggregate view built from Azeroth Questing research observations and identity-free completed-quest contributions. It may show a quest ID/name, observed maps, factions, classes, aggregate observation counts, the number of anonymous installations contributing evidence, whether completion evidence exists, and the newest receive time.

It does **not** expose character names, realm names, account identifiers, BattleTags, installation IDs, bearer tokens, client observation keys, peer sender identities, local file paths, raw SavedVariables, screenshots, gameplay recordings, or IP history.

## Submitted Research Data viewer

The Companion can use its per-installation bearer token to read a privacy-preserving research summary from the Azeroth Questing Server. The viewer may show aggregate observation and quest counts, this installation's own contribution count, recent anonymous structured observations, class-evidence summaries, and active addon/Companion version counts. The viewer can also export every distinct quest ID currently collected by the research system as CSV.

The viewer does **not** receive installation IDs, client observation keys, bearer-token hashes, raw SavedVariables payloads, character names, account identifiers, BattleTags, realm identifiers, screenshots, gameplay recordings, or IP history.

## Data deletion

Users can remove locally queued Pending Observations by deleting files from the companion Outbox after closing the companion. The local completed-quest cache and repository hash history are stored under the Companion Local AppData folder. Uninstalling the application may not automatically remove user data stored under Local AppData.

## Source code

Azeroth Questing Companion is open source under the MIT License. The source code is available at:

`https://github.com/Frostcanvas/AzerothQuesting-Companion`
