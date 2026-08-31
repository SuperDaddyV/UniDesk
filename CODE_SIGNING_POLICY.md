# Code signing policy

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## Project and source

- Project: [UniDesk](https://github.com/SuperDaddyV/UniDesk)
- License: [MIT](LICENSE)
- Privacy policy: [PRIVACY.md](PRIVACY.md)
- Official release source: [GitHub Releases](https://github.com/SuperDaddyV/UniDesk/releases)

## Team roles

UniDesk is currently maintained by one person. The public account link below is the authoritative identity shown for each required project role.

- Authors: [SuperDaddyV](https://github.com/SuperDaddyV)
- Reviewers: [SuperDaddyV](https://github.com/SuperDaddyV)
- Approvers: [SuperDaddyV](https://github.com/SuperDaddyV)

Contributions from other people are reviewed through the public GitHub history before they can enter a signed release. A signing request is always a separate manual action and is never triggered automatically by a pull request or an unreviewed commit.

## Signing scope

The signing policy covers only binaries built from this repository and owned by UniDesk:

- `UniDesk.exe`, `UniDesk.dll`, and other first-party managed assemblies;
- `UniDesk.HardwareService.exe` and its first-party managed assembly;
- `UniDesk.HardwareRepair.exe` and its first-party managed assembly;
- the final `UniDesk_Setup_<version>.exe` installer.

Third-party runtimes, libraries, drivers, and installers are not re-signed as UniDesk binaries. In particular, the bundled PawnIO installer must retain its upstream publisher signature and pinned hash.

## Build and approval process

1. The candidate source revision is committed to the public repository and passes the normal GitHub Actions CI workflow.
2. An approver manually starts the `Build and sign release candidate` workflow for the exact `main` revision and version.
3. GitHub Actions creates a fresh application payload from that revision and sends only the declared first-party PE files to SignPath.
4. After SignPath signs the first-party payload, the workflow builds the installer from that signed payload and submits the installer for signing.
5. Release-readiness checks verify the source revision, version, expected and consistent first-party signer identity, third-party PawnIO hash and upstream signer, and final SHA-256 manifest.
6. Creating a Git tag or GitHub Release remains a separate manual decision after installation and release-matrix testing.

The SignPath API token is stored only as a GitHub Actions secret. The repository does not contain a certificate private key, signing token, or other signing identity material.

## v2.1.0 unsigned release exception

The project owner may explicitly approve one unsigned stable release for `v2.1.0`, matching the unsigned trust baseline of the existing stable release. This exception does not claim SignPath approval or a valid Authenticode signature, and it does not apply automatically to later versions.

An unsigned `v2.1.0` release must still be rebuilt from the exact clean public `main` revision, pass locked restore, zero-warning Release build, the full automated test suite, dependency-vulnerability and version checks, payload inventory and SHA-256 verification, and the complete applicable manual release matrix. The installer and every first-party PE file must be verified as `NotSigned`. The README and GitHub Release must disclose `Authenticode: NotSigned` and the resulting Windows SmartScreen or enterprise-policy risk. A Git tag or GitHub Release remains a separate manual action requiring the project owner's final confirmation.

## v2.2.0 unsigned release exception

On 2026-08-31, the project owner separately approved one unsigned stable release for `v2.2.0` because the SignPath application remains pending. This is a version-specific exception only: it does not claim SignPath approval or a valid Authenticode signature, does not alter the historical `v2.1.0` exception, and does not apply automatically to any later version.

An unsigned `v2.2.0` release must be rebuilt from the exact clean public `main` revision and satisfy the same locked restore, zero-warning Release build, full automated test suite, dependency-vulnerability and version checks, payload inventory, SHA-256 verification, and applicable manual release-matrix requirements as `v2.1.0`. The installer and every first-party PE file must be verified as `NotSigned`. The README and GitHub Release must disclose `Authenticode: NotSigned` and the resulting Windows SmartScreen or enterprise-policy risk. The owner's instruction to publish `v2.2.0` is the final release authorization for this exact version only.

## Reporting concerns

Report suspected misuse of the signing certificate, an unexpected signed file, or a release-integrity issue through [GitHub private vulnerability reporting](https://github.com/SuperDaddyV/UniDesk/security/advisories/new). Include the affected file name, release URL, SHA-256 value, and signature details when possible. Do not publish unpatched exploit details in a public issue.
