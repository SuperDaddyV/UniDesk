# Third-party notices

## LibreHardwareMonitorLib 0.9.6

- Project: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- Release source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
- License: Mozilla Public License 2.0
- The upstream third-party notices are packaged as `LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt`.

## PawnIO 2.1.0.0

- Project: https://github.com/namazso/PawnIO
- Binary source: `LibreHardwareMonitor/Resources/PawnIO_setup.exe` from the official LibreHardwareMonitor `v0.9.6` source archive.
- SHA-256: `a3a46226c5e2824f4cdd42be0eecbabfc672c86f7889710f5ab1e6ad385b47a0`
- Authenticode signer: `namazso.eu`
- License: GNU GPL v2 with the exception stated in the bundled `PawnIO-COPYING.txt`.
- Corresponding source: https://github.com/namazso/PawnIO/tree/5cdf470831fdfff3f7f1d06363ca6b230f3bf35a

PawnIO is installed as a shared system component only when the user keeps the
installer task selected. UniDesk removes its own Windows service on uninstall,
but preserves PawnIO by default because other hardware tools may also use it.
