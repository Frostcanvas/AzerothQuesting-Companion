# Code signing policy

Azeroth Questing Companion is an open-source Windows application distributed from the public `Frostcanvas/AzerothQuesting-Companion` GitHub repository.

**Free code signing provided by SignPath.io, certificate by SignPath Foundation.**

## Source and release policy

- Source repository: `https://github.com/Frostcanvas/AzerothQuesting-Companion`
- License: MIT
- Official Windows builds are produced by GitHub Actions from this repository on GitHub-hosted runners.
- Release binaries must be traceable to the Git commit and GitHub Actions run that produced them.
- Stable Companion releases must not be published unless the Windows executable and installer have valid Authenticode signatures from the approved SignPath signing policy.
- While SignPath approval/configuration is still pending, Companion **Beta** builds may be published only as clearly marked unsigned GitHub **Pre-releases** for testing. Windows SmartScreen or endpoint-security warnings are expected for these temporary unsigned Beta installers.
- Once SignPath configuration is available, Beta Pre-releases should use the same signing path as Stable releases whenever possible.
- The release pipeline must never fall back to an unsigned Stable release. Missing SignPath configuration is a hard failure for Stable publication.
- Signed releases verify the executable and installer signatures before uploading the installer to GitHub Releases.

## Team roles

The project is currently maintained by a single project owner.

- Authors / committers: [Frostcanvas](https://github.com/Frostcanvas)
- Reviewers for external contributions: [Frostcanvas](https://github.com/Frostcanvas)
- Release signing approver: [Frostcanvas](https://github.com/Frostcanvas)

All maintainers with repository or SignPath access are expected to use multi-factor authentication.

## Build and signing flow

The Windows release pipeline is designed to:

1. Build `AzerothQuestingCompanion.exe` from source on a GitHub-hosted Windows runner.
2. Read the Companion version and determine whether the requested release is a prerelease or Stable release.
3. Check whether all required SignPath repository configuration is available.
4. If SignPath is configured, upload the unsigned executable to SignPath, replace the build output with the signed executable, and verify its Authenticode signature.
5. Build `AzerothQuestingCompanion-Setup.exe` with the resulting executable.
6. If SignPath is configured, submit the installer to SignPath and verify the returned installer signature.
7. If the version is a Beta prerelease and SignPath is not yet configured, publish the installer only as an explicitly **unsigned GitHub Pre-release** for testing.
8. If the version is Stable and SignPath is not configured, stop the workflow and do not publish the release.
9. Upload the final installer as `AzerothQuestingCompanion-Setup.exe`, which is the asset consumed by the Companion self-updater.

The SignPath API token is stored only as a GitHub Actions secret. SignPath organization, project, signing-policy, and artifact-configuration identifiers are stored as repository variables.

## Privacy policy

See [PRIVACY.md](PRIVACY.md) for the current Companion data-collection and synchronization policy.
