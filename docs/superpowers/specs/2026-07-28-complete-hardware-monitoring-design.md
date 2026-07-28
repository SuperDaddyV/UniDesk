# UniDesk Complete Hardware Monitoring Design

## Goal

Provide CPU temperature and other low-level LibreHardwareMonitor values without elevating the UniDesk WPF process. The installer offers a clearly disclosed, default-selected complete hardware-monitoring component consisting of PawnIO and a narrowly scoped Windows service.

## Superseded decision

The 2026-07-12 hardware-resilience design intentionally deferred service and driver installation. This design supersedes only that deferral. Its existing sensor selection, provider ordering, sanitized diagnostics, immutable snapshots, serial sampling, and fallback rules remain in force.

## Process and privilege boundary

- `UniDesk.exe` remains a normal-user `asInvoker` process.
- `UniDesk.HardwareService.exe` is the only UniDesk executable installed as a Windows service.
- The service owns LibreHardwareMonitor and reads CPU, motherboard, and GPU sensors through PawnIO.
- The service does not access user databases, settings, clipboard content, shortcuts, notes, weather, network APIs, or update services.
- The service never starts another process and never writes hardware settings.
- The service accepts only fixed read-only IPC commands and returns detached sensor/status DTOs.

The service initially runs as `LocalSystem` because PawnIO and low-level register access must work without an interactive elevated user. This privilege is accepted only with the narrow code and IPC boundaries above. No WPF or user-content assembly is loaded into the service.

## Projects

### UniDesk.Hardware.Contracts

A dependency-free .NET class library containing:

- protocol version and pipe-name constants;
- request and response DTOs;
- hardware-device, sensor, provider, PawnIO, and service status DTOs;
- bounded JSON serialization helpers shared by the service and client.

### UniDesk.HardwareService

A .NET 10 LTS Windows Service containing:

- one LibreHardwareMonitor `Computer` instance;
- a serial sensor refresh loop;
- the latest immutable sensor/status snapshot;
- a local named-pipe server;
- Windows Event Log or service-local diagnostic logging only;
- no database, WPF, clipboard, shell, update, HTTP, or arbitrary command dependency.

### UniDesk

The WPF process contains a named-pipe client that obtains privileged LibreHardwareMonitor snapshots. Existing CPU/GPU selection code consumes the detached snapshots. Existing non-privileged providers continue to run locally and remain available when the service or PawnIO is missing.

## IPC protocol

- Pipe: `UniDesk.HardwareMetrics.v1`.
- Transport: local Windows named pipe only.
- Framing: one bounded UTF-8 JSON line per request and response.
- Maximum request size: 1 KiB.
- Maximum response size: 256 KiB.
- Commands: `GetSnapshot` and `GetStatus` only.
- Requests contain protocol version and command; no free-form arguments.
- Unknown protocol versions or commands return a bounded structured error.
- Client connection/read/write operations use short timeouts and never run on the UI thread.
- The server pipe ACL permits local authenticated users to read hardware metrics; remote pipe access is rejected.
- Four bounded accept loops isolate stalled clients; every request still has a two-second timeout and bounded message size.

## Runtime behavior

1. `SystemMetricsMonitor` starts its existing background serial sample.
2. `SystemMetricsService` asks the pipe client for the latest service snapshot.
3. If a compatible snapshot is returned, CPU/GPU LibreHardwareMonitor readers use it.
4. If the pipe is unavailable, timed out, or incompatible, the service snapshot is empty and existing non-privileged readers continue.
5. Availability metadata distinguishes `ServiceNotInstalled`, `ServiceStopped`, `DriverUnavailable`, `ProtocolMismatch`, `TimedOut`, and `Available` in diagnostics and maps them to the existing user-facing metric availability states.
6. A service failure never terminates UniDesk and never turns a persisted failure into a false success.
7. A failed LibreHardwareMonitor initialization is retried with bounded exponential backoff instead of remaining permanently unavailable for the lifetime of the service.

## Installer behavior

The Inno Setup tasks `desktopicon` and `completehardware` are selected by default. Setup does not reuse prior task selections during an overwrite install, so both remain visibly selected until the user clears them. The hardware task explicitly states that it installs the PawnIO hardware-access driver and the UniDesk hardware-monitoring system service with administrator privileges.

When selected, setup invokes the bundled `UniDesk.HardwareRepair` helper, which has a `requireAdministrator` manifest and is the single implementation of hardware-component maintenance. The helper:

1. queries the PawnIO driver and, when already installed, starts or confirms it without running the installer again;
2. only when PawnIO is absent, verifies the pinned installer SHA-256 and valid Authenticode signature;
3. installs missing PawnIO silently using its documented installer switches and confirms that the driver can start;
4. creates or reconfigures `UniDesk.HardwareService` with automatic start, LocalSystem identity, description, and bounded failure restart policy by passing each `sc.exe` argument separately;
5. starts the service and runs a versioned IPC health check that verifies service availability, PawnIO state, and provider initialization;
6. returns a stable step-specific exit code and diagnostic log if any component step fails; setup records the code and shows a non-fatal warning while completing the base application installation.

Setup requires administrator privileges and is intended to be started by a normal double-click. A standard user supplies administrator credentials through the Windows UAC prompt; setup does not ask the user to right-click **Run as administrator**. The completion page launches UniDesk by default with Inno Setup's `runasoriginaluser` flag, so a normally started setup returns to the original standard-user token. `UniDesk.exe` remains `asInvoker`; an explicitly elevated setup process that lacks the original token is not compensated for by elevating the main application permanently. Settings repair launches the bundled helper normally and awaits its result; the helper manifest owns the UAC prompt. The installed application never copies, locates, downloads, or depends on a full UniDesk setup executable for repair.

The complete hardware component is an optional enhancement. A driver, service, security-policy, or unsupported-hardware failure must not raise a setup Runtime error or make the base application installation fail. The warning includes the stable helper exit code and diagnostic-log location, and Settings remains able to retry repair later.

The elevated setup phase does not write `HKCU` or administrator-profile `LocalAppData`. Per-user startup remains configured by the ordinary-privilege app from Settings for the actual signed-in user.

Upgrade stops the UniDesk service before replacing files and invokes the same helper to repair and restart it afterward. Uninstall invokes the helper's fixed `remove-service` operation before installed files are removed; if the helper fails, setup may only fall back to fixed `sc.exe stop/delete UniDeskHardwareService` commands and must warn if the owned service still cannot be removed. PawnIO is a shared dependency and remains installed by default; optional PawnIO removal requires a separate explicit user choice.

The installer accepts Windows x64 only. It must use Inno Setup's native-x64 architecture check rather than the x64-compatible check, because x64 application emulation on Windows ARM64 cannot make the bundled x64 kernel driver compatible. ARM64 requires a separately built and verified native driver/service package.

The PawnIO binary is pinned and unmodified. Public distribution is blocked until third-party license text, corresponding-source information, signature verification, and the exact bundled hash are documented.

## Diagnostics and UI

The sanitized diagnostic export adds:

- component requested/installed state;
- service installed/running state and service version;
- IPC protocol, last success, and last bounded error;
- PawnIO installed/version state;
- privileged sensor provider status.

It continues to exclude user names, user paths, network identifiers, serial numbers, clipboard data, database content, API keys, and process lists.

Settings shows a read-only complete-hardware-monitoring status and a repair action only when a component is unavailable. Hardware tooltips explain service, driver, protocol, and timeout failures without claiming that every motherboard exposes a usable sensor.

Public distribution requires valid Authenticode signatures for the main executable, hardware service, PawnIO installer, and final installer. The certificate and private key are external release inputs; an unsigned artifact is never described as release-ready.

## Verification

- Contract serialization and request-bound tests.
- Pipe server tests for supported commands, unknown commands, protocol mismatch, oversized input, timeout, and reconnect.
- Service snapshot tests with a fake LibreHardwareMonitor host.
- Main-process tests proving privileged snapshot use and ordinary-provider fallback.
- Diagnostic sanitization and status tests.
- Installer and helper checks for x64-only setup, default disclosure, helper packaging, argument-safe service install/start/stop/delete, non-fatal component failure, PawnIO hash/signature gates, `asInvoker`/`requireAdministrator` manifests, original-user post-install launch, and default PawnIO retention.
- Full `dotnet test UniDesk.sln -c Release --no-restore`.
- Manual supported-Windows x64 VM matrix for administrator and true standard-user installation, selected/unselected component, fresh and existing PawnIO, UAC cancellation, security-product blocking, standard-user app launch, upgrade, service stopped, protocol mismatch, unsupported sensors, repair, uninstall, and user-data preservation.

No service or driver is installed on the development machine as part of automated verification.
