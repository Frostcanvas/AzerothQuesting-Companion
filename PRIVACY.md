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

The current version does not upload Pending Observations to Service01. Pending Observations remain on the local computer.

The companion does access GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates.

If Service01 synchronization is added in a future version, this policy and the installer/application controls will be updated before that feature is enabled. Any automatic transfer of observation data will be documented and provided with an appropriate user control or opt-out consistent with the SignPath Foundation open-source signing requirements.

## Data deletion

Users can remove locally queued Pending Observations by deleting files from the companion Outbox after closing the companion. Uninstalling the application may not automatically remove user data stored under Local AppData.

## Source code

Azeroth Questing Companion is open source under the MIT License. The source code is available at:

`https://github.com/Frostcanvas/AzerothQuesting-Companion`
