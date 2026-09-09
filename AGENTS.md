# Azeroth Questing Release Instructions

## Changelog source of truth

Authoritative release history lives in:

- `Frostcanvas/AzerothQuesting/CHANGELOG.md` — Addon
- `Frostcanvas/AzerothQuesting-Companion/CHANGELOG.md` — Companion
- `Frostcanvas/AzerothQuestingwebsite/CHANGELOG.md` — Website

The applicable changelog must be updated when a release-worthy change is implemented. Each Beta gets its own entry and becomes frozen once handed off, distributed, deployed for testing, or published. Stable promotion requires explicit user approval.

## Companion release rules

- Companion versions advance independently from Addon and Website versions.
- Release-worthy Companion changes go through Beta first.
- A consumed Beta must not be modified; increment to the next Beta.
- Update project Version/FileVersion and `CHANGELOG.md` together.
- Beta updater testing requires a GitHub Pre-release with `AzerothQuestingCompanion-Setup.exe`.
- Do not claim Windows runtime testing from CI/build/installer success.
- Beta prereleases may be unsigned while trusted signing is unavailable if clearly labeled as testing builds.
- Stable Companion releases must always be signed and must fail closed if signing is unavailable.

## Implementation

Treat requested changes as implementation requests unless the user explicitly asks only for discussion/planning. Update every affected component and keep protocol/API/data formats compatible. Never commit secrets.
