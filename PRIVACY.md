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

Beginning with development version 0.1.5, the companion can synchronize Pending Observations to the private FrostLabs Service01 Azeroth Questing API. The default FrostLabs LAN endpoint is `http://10.0.10.246:8766`. The dashboard provides a **Sync Pending Observations to Service01** checkbox and a manual **Sync Now** action. Turning the checkbox off keeps new Pending Observations local.

For Azeroth Questing v0.2.32 and newer, structured uploads can include only addon-produced research fields: observation key, local/anonymous-peer source, quest ID, map ID, evidence type (`seen`, `available`, `offered`, `accepted`, `active`, or `turnedIn`), faction, World of Warcraft class ID/token, character level, quest completion state, observation timestamp, addon version, and companion version. The intended upload does not include character name, realm, BattleTag, guild, chat, party-member identity, GUID, screenshots, or gameplay recordings.

The Service01 API assigns a random per-installation bearer token. The public companion does not contain a shared server secret. Service01 stores only the SHA-256 hash of the bearer token. If synchronization fails, the companion keeps the Pending Observation locally instead of deleting it.

Older Pending Observation files created before the structured v0.2.32 handoff can be sent through a compatibility raw SavedVariables endpoint so existing queued research is not silently discarded. Those files are stored as data and are not executed as Lua by the server.

The companion also accesses GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates.

## Data deletion

Users can remove locally queued Pending Observations by deleting files from the companion Outbox after closing the companion. Uninstalling the application may not automatically remove user data stored under Local AppData.

## Source code

Azeroth Questing Companion is open source under the MIT License. The source code is available at:

`https://github.com/Frostcanvas/AzerothQuesting-Companion`
