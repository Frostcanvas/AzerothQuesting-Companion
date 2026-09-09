# Azeroth Questing Companion code-signing policy

Trusted code signing is preferred for Companion releases.

## Beta

While trusted signing is unavailable, Companion Beta builds may be published as clearly labeled unsigned GitHub Pre-releases for testing. Windows SmartScreen or publisher warnings may occur. Never describe an unsigned Beta as signed.

If trusted signing is configured, Beta builds should use the signed path when practical.

## Stable

Stable Companion releases must always be signed. The release workflow must fail closed if trusted signing is unavailable; there is no unsigned Stable fallback.

## Secrets

Signing credentials, private keys, API tokens, and other authentication material must never be committed to the repository. Use approved secret storage such as GitHub Secrets and the selected signing provider's secure identity/credential system.
