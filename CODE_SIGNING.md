# Code signing policy

Azeroth Questing Companion is an open-source Windows application distributed from the public `Frostcanvas/AzerothQuesting-Companion` GitHub repository.

**Free code signing provided by SignPath.io, certificate by SignPath Foundation.**

## Source and release policy

- Source repository: `https://github.com/Frostcanvas/AzerothQuesting-Companion`
- License: MIT
- Official Windows releases are built by GitHub Actions from this repository on GitHub-hosted runners.
- Release binaries must be traceable to the Git commit and GitHub Actions run that produced them.
- Public companion releases must not be published unless the Windows executable and installer have valid Authenticode signatures from the approved SignPath signing policy.
- The release pipeline verifies signatures before uploading the installer to a GitHub Release.
- Each public release signing request is manually approved as required by the SignPath Foundation open-source policy.

## Team roles

The project is currently maintained by a single project owner.

- Authors / committers: [Frostcanvas](https://github.com/Frostcanvas)
- Reviewers for external contributions: [Frostcanvas](https://github.com/Frostcanvas)
- Release signing approver: [Frostcanvas](https://github.com/Frostcanvas)

All maintainers with repository or SignPath access are expected to use multi-factor authentication.

## Build and signing flow

The Windows release pipeline is designed to:

1. Build `AzerothQuestingCompanion.exe` from source on a GitHub-hosted Windows runner.
2. Upload the unsigned executable as a GitHub Actions artifact.
3. Submit that artifact to SignPath using SignPath's GitHub trusted-build-system integration and origin verification.
4. Replace the build output with the signed executable and verify its Authenticode signature.
5. Build `AzerothQuestingCompanion-Setup.exe` with the already-signed executable inside the installer.
6. Upload the unsigned installer as a GitHub Actions artifact.
7. Submit the installer to SignPath and verify its Authenticode signature.
8. Publish the signed installer to the GitHub Release only after all signing and verification steps succeed.

The SignPath API token is stored only as a GitHub Actions secret. SignPath organization, project, signing-policy, and artifact-configuration identifiers are stored as repository variables.

## Privacy policy

See [PRIVACY.md](PRIVACY.md).

The current companion does not upload Pending Observations to Service01. They remain on the user's computer until Service01 synchronization is implemented and explicitly documented.
