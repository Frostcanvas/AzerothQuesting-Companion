# Download Azeroth Questing Companion

Official Windows releases of **Azeroth Questing Companion** are published through the project's GitHub Releases page:

https://github.com/Frostcanvas/AzerothQuesting-Companion/releases

The current public release, v0.1.3, was published before the SignPath Foundation application and is unsigned. Beginning with the first approved SignPath-signed release, the release pipeline is intended to publish only installers that have passed the project's signing and Authenticode verification steps.

Beta-channel Companion builds may be published as explicitly unsigned GitHub Pre-releases while trusted signing is unavailable. Beginning with Companion `0.1.9-beta.9`, addon updates can be written while World of Warcraft is running; use `/reload` or relog after the update to load the new addon files in the active game client.

## Code signing policy

**Free code signing provided by SignPath.io, certificate by SignPath Foundation.**

See [CODE_SIGNING.md](CODE_SIGNING.md) for the full code signing policy and [PRIVACY.md](PRIVACY.md) for the privacy policy.

Official releases are built from this public repository by GitHub Actions. Signed release artifacts are required to be traceable to the repository commit and workflow run that produced them.
