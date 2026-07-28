using System.Text;
using System.Text.Json;

namespace UniDesk.Hardware.Contracts;

public enum HardwareServiceCommand
{
    GetSnapshot = 1,
    GetStatus = 2
}

public enum HardwareServiceAvailability
{
    Available,
    ServiceNotInstalled,
    ServiceStopped,
    ServiceUnavailable,
    DriverUnavailable,
    ProtocolMismatch,
    TimedOut,
    Error
}

public enum HardwareDeviceType
{
    Cpu,
    Motherboard,
    GpuNvidia,
    GpuAmd,
    GpuIntel,
    Other
}

public sealed record HardwareServiceRequest(
    int ProtocolVersion,
    HardwareServiceCommand Command);

public sealed record PawnIoStatus(bool IsInstalled, string? Version);

public sealed record HardwareProviderStatus(
    bool IsInitialized,
    bool IsElevated,
    string? LastError,
    DateTimeOffset? LastRefreshUtc,
    IReadOnlyList<string> HardwareNames);

public sealed record HardwareSensorDto(
    string DeviceId,
    string DeviceName,
    HardwareDeviceType DeviceType,
    string SensorName,
    string SensorType,
    double? Value);

public sealed record HardwareServiceSnapshotResponse(
    int ProtocolVersion,
    HardwareServiceAvailability Availability,
    string? Error,
    DateTimeOffset CapturedAtUtc,
    PawnIoStatus PawnIo,
    HardwareProviderStatus Provider,
    IReadOnlyList<HardwareSensorDto> Sensors,
    string? ServiceVersion = null);

public static class HardwareIpcProtocol
{
    public const int CurrentVersion = 1;
    public const string PipeName = "UniDesk.HardwareMetrics.v1";
    public const int MaxRequestBytes = 1024;
    public const int MaxResponseBytes = 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeRequest(HardwareServiceRequest request) =>
        JsonSerializer.Serialize(request, JsonOptions);

    public static HardwareServiceRequest DeserializeRequest(string json)
    {
        ValidateLength(json, MaxRequestBytes, "request");
        HardwareServiceRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<HardwareServiceRequest>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The hardware service request is invalid.", ex);
        }

        if (request == null || !Enum.IsDefined(request.Command))
        {
            throw new InvalidDataException("The hardware service command is not supported.");
        }

        return request;
    }

    public static string SerializeResponse(HardwareServiceSnapshotResponse response)
    {
        var json = JsonSerializer.Serialize(response, JsonOptions);
        ValidateLength(json, MaxResponseBytes, "response");
        return json;
    }

    public static HardwareServiceSnapshotResponse DeserializeResponse(string json)
    {
        ValidateLength(json, MaxResponseBytes, "response");
        try
        {
            return JsonSerializer.Deserialize<HardwareServiceSnapshotResponse>(json, JsonOptions)
                ?? throw new InvalidDataException("The hardware service response is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The hardware service response is invalid.", ex);
        }
    }

    public static async Task<string?> ReadUtf8LineAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        var bytes = new byte[maximumBytes];
        var count = 0;
        var oneByte = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(oneByte.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return count == 0 ? null : DecodeUtf8(bytes.AsSpan(0, count));
            }

            if (oneByte[0] == (byte)'\n')
            {
                if (count > 0 && bytes[count - 1] == (byte)'\r')
                {
                    count--;
                }

                return DecodeUtf8(bytes.AsSpan(0, count));
            }

            if (count == bytes.Length)
            {
                throw new InvalidDataException("The hardware service frame exceeds the size limit.");
            }

            bytes[count++] = oneByte[0];
        }
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("The hardware service frame is not valid UTF-8.", ex);
        }
    }

    private static void ValidateLength(string value, int maximumBytes, string kind)
    {
        if (Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            throw new InvalidDataException($"The hardware service {kind} exceeds the size limit.");
        }
    }
}
