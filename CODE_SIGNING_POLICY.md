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
5. Release-readiness checks verify the source revision, version, Authenticode status, third-party PawnIO hash, and final SHA-256 manifest.
6. Creating a Git tag or GitHub Release remains a separate manual decision after installation and release-matrix testing.

The SignPath API token is stored only as a GitHub Actions secret. The repository does not contain a certificate private key, signing token, or other signing identity material.

## Reporting concerns

Report suspected misuse of the signing certificate, an unexpected signed file, or a release-integrity issue through [GitHub Issues](https://github.com/SuperDaddyV/UniDesk/issues). Include the affected file name, release URL, SHA-256 value, and signature details when possible.
