using UniDesk.HardwareService;

if (args.Contains("--health-check", StringComparer.OrdinalIgnoreCase))
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    Environment.ExitCode = await HardwareServiceHealthCheck.RunAsync(cancellation.Token);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = HardwareServiceNames.ServiceName;
});
builder.Services.AddSingleton<HardwareSnapshotState>();
builder.Services.AddSingleton<IHardwareSnapshotSource>(provider =>
    provider.GetRequiredService<HardwareSnapshotState>());
builder.Services.AddSingleton<LibreHardwareSnapshotCollector>();
builder.Services.AddSingleton<HardwareServiceRequestHandler>();
builder.Services.AddSingleton<HardwarePipeServer>();
builder.Services.AddHostedService<HardwareSensorWorker>();
builder.Services.AddHostedService<HardwarePipeWorker>();

await builder.Build().RunAsync();
