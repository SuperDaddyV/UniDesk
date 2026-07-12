namespace UniDesk.Services;

public enum HardwareSensorDeviceType
{
    Cpu,
    Motherboard,
    GpuNvidia,
    GpuAmd,
    GpuIntel,
    Other
}

public sealed record HardwareSensorSnapshot(
    string DeviceId,
    string DeviceName,
    HardwareSensorDeviceType DeviceType,
    string SensorName,
    string SensorType,
    double? Value);
