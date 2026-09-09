# Azeroth Questing Companion versioning

Companion releases follow semantic versioning and the project Beta-first policy. The authoritative release history is `CHANGELOG.md`.

- Development builds use prerelease versions such as `0.1.9-beta.5`.
- Player-facing text may display `0.1.9 Beta 5` while GitHub/updater comparison uses canonical SemVer.
- Every consumed Beta is frozen; the next release-worthy change increments the Beta number.
- Stable promotion is explicit and product-specific.
- Stable-channel users never receive prereleases; Beta-channel users may receive the newest eligible Beta or Stable release.
- Beta updater builds must be GitHub Pre-releases with `AzerothQuestingCompanion-Setup.exe`.
- Beta prereleases may be unsigned while trusted signing is unavailable and clearly labeled for testing. Stable Companion releases must be signed.
- Windows FileVersion remains numeric and advances independently as required by Windows tooling.
