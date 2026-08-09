# Security policy

## Supported versions

Security fixes are provided for the latest published UniDesk release. Older releases may be used to reproduce an issue, but users should upgrade after a fix is available.

| Version | Supported |
| --- | --- |
| Latest release | Yes |
| Older releases | No |

## Reporting a vulnerability

Use [GitHub private vulnerability reporting](https://github.com/SuperDaddyV/UniDesk/security/advisories/new) for vulnerabilities, suspected code-signing misuse, local privilege escalation, unsafe installer behavior, or exposure of private user data.

Do not include exploit details, tokens, private user data, or an unpatched proof of concept in a public issue. Include the affected version, Windows version, reproduction steps, impact, and relevant hashes or signature details. Allow the maintainer time to confirm and remediate the report before public disclosure.

Ordinary bugs without security or privacy impact can continue to use [GitHub Issues](https://github.com/SuperDaddyV/UniDesk/issues).

## Release integrity

Official installers are published only through [GitHub Releases](https://github.com/SuperDaddyV/UniDesk/releases). Release candidates must pass the repository CI, controlled SignPath workflow, expected-signer checks, source-revision verification, and release test matrix before publication.
