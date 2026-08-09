namespace UniDesk.HardwareRepair;

public enum HardwareRepairExitCode
{
    Success = 0,
    InvalidArguments = 2,
    NotElevated = 3,
    PawnIoInstallerMissing = 10,
    PawnIoHashMismatch = 11,
    PawnIoSignatureInvalid = 12,
    PawnIoInstallFailed = 13,
    PawnIoStatusCheckFailed = 14,
    PawnIoStartFailed = 15,
    ServiceBinaryMissing = 20,
    ServiceCreateFailed = 21,
    ServiceConfigureFailed = 22,
    ServiceDescriptionFailed = 23,
    ServiceRecoveryFailed = 24,
    ServiceStartFailed = 25,
    ServicePayloadSecurityInvalid = 26,
    ServiceOwnershipInvalid = 27,
    HealthCheckFailed = 30,
    ServiceRemoveFailed = 40,
    StartupCleanupFailed = 50,
    UnexpectedError = 100
}
