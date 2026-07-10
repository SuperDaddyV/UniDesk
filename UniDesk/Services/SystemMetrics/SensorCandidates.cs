namespace UniDesk.Services;

public readonly record struct CpuTemperatureSensorCandidate(string Name, double? Value);

public readonly record struct CpuTemperatureSensorSelection(string Name, double Value);

public readonly record struct CpuUsageSensorCandidate(string Name, double? Value);

public readonly record struct CpuUsageSensorSelection(string Name, double Value);

public readonly record struct CpuTemperatureProviderSelection(
    string Source,
    string Name,
    double Value,
    int Priority);

public readonly record struct GpuSensorCandidate(string Name, double? Value);

public readonly record struct GpuSensorSelection(string Name, double Value);
