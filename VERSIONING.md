# Azeroth Questing Companion Versioning

Azeroth Questing Companion uses separate machine-facing and player-facing version representations.

## Release identity

GitHub tags, GitHub Releases, updater comparisons, and release automation use canonical prerelease versions:

- `0.1.9-beta.1`
- `0.1.9-beta.2`
- `0.1.9-beta.3`
- `0.1.9`

Beta builds are GitHub Pre-releases. The final/golden build is a normal GitHub Release only after explicit Stable-release approval.

Semantic ordering is authoritative for the updater:

`0.1.8` < `0.1.9-beta.1` < `0.1.9-beta.2` < `0.1.9`

## Player-facing display

The Companion converts canonical prerelease labels into a friendlier display form:

- `0.1.9-beta.1` is shown as **0.1.9 Beta 1**.
- `0.1.9-beta.8` is shown as **0.1.9 Beta 8**.
- `0.1.9-rc.1` is shown as **0.1.9 RC 1**.
- `0.1.9` is shown as **0.1.9**.

The friendly wording is presentation only. It must not change GitHub tag semantics or updater ordering.

## Windows numeric file version

Windows file metadata remains numeric. During a beta train, the internal numeric file version can continue on the preceding numeric line so the final/golden build is numerically newer.

The transition to this model starts with:

- Companion `0.1.9-beta.1` -> Windows FileVersion `0.1.8.6`
- Later `0.1.9` golden release -> Windows FileVersion `0.1.9.0`

The beta seed and Windows build component do not have to be the same number. GitHub/updater semantic versioning remains the source of truth for release ordering.

## Channels

- Stable channel ignores GitHub Pre-releases.
- Beta channel can receive newer GitHub Pre-releases and later Stable/golden releases.
- A Beta installation is never downgraded to an older Stable release.
- Every release-worthy change enters Beta first. Stable promotion requires explicit approval.

## Continuous changelog workflow

`CHANGELOG.md` is maintained continuously during development rather than reconstructed at release time.

- Every release-worthy Companion change must update `CHANGELOG.md` in the same development change that bumps the Companion version.
- Each Beta section describes the delta from the immediately previous Beta or Stable build. For example, `0.1.9 Beta 2` records what changed after `0.1.9 Beta 1`; it does not silently rewrite the Beta 1 history.
- Additional fixes before Stable increment the Beta seed and create a new changelog section (`beta.1` -> `beta.2` -> `beta.3`).
- Keep all Beta sections after the golden release so the complete test history remains available.
- When Stable/golden release is explicitly approved, add a new Stable section that consolidates the player-visible changes since the previous Stable release, removes duplicate wording, and is suitable for GitHub/distribution release notes.
- The Stable summary does not erase or replace the individual Beta sections.
- Record compatibility requirements when a Companion build depends on a particular Azeroth Questing addon or server/API version.
- Record only tests that actually occurred. Windows, live-server, and in-game testing remain explicitly pending until they are performed.
- When preparing a GitHub Release or another distribution listing, use the matching `CHANGELOG.md` section as the release-note source instead of trying to reconstruct the changes from commit history.

Documentation-only maintenance does not force a new Companion version unless it changes release behavior or player-facing application behavior.