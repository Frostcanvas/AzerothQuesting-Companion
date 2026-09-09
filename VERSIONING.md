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
