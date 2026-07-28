using System.Security.Cryptography;

namespace UniDesk.HardwareRepair;

internal enum HardwarePackageVerificationResult
{
    Valid,
    Missing,
    HashMismatch,
    SignatureInvalid
}

internal sealed class HardwarePackageVerifier(
    IProcessRunner processRunner,
    HardwareRepairLogger logger)
{
    internal const string ExpectedPawnIoSha256 =
        "a3a46226c5e2824f4cdd42be0eecbabfc672c86f7889710f5ab1e6ad385b47a0";

    public HardwarePackageVerificationResult VerifyPawnIo(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            return HardwarePackageVerificationResult.Missing;
        }

        using (var stream = File.OpenRead(installerPath))
        {
            var actualHash = Convert.ToHexString(SHA256.HashData(stream));
            if (!actualHash.Equals(ExpectedPawnIoSha256, StringComparison.OrdinalIgnoreCase))
            {
                logger.Log("PawnIO verification failed: SHA-256 mismatch.");
                return HardwarePackageVerificationResult.HashMismatch;
            }
        }

        var escapedPath = installerPath.Replace("'", "''", StringComparison.Ordinal);
        var command =
            "$signature = Get-AuthenticodeSignature -LiteralPath '" + escapedPath +
            "'; if ($signature.Status -eq 'Valid') { exit 0 } else { exit 1 }";
        var powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        var result = processRunner.Run(
            powershellPath,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command],
            TimeSpan.FromSeconds(30));
        if (result.ExitCode != 0)
        {
            logger.Log($"PawnIO verification failed: Authenticode exit code {result.ExitCode}.");
            return HardwarePackageVerificationResult.SignatureInvalid;
        }

        return HardwarePackageVerificationResult.Valid;
    }
}
