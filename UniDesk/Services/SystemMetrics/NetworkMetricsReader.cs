using System.Net.NetworkInformation;

namespace UniDesk.Services;

public sealed class NetworkMetricsReader : INetworkMetricsReader
{
    private static readonly string[] VirtualAdapterKeywords =
    [
        "virtual", "loopback", "pseudo", "hyper-v", "vethernet", "vmware",
        "virtualbox", "docker", "wintun", "tap", "npcap", "vpn", "wireguard",
        "tailscale", "zerotier", "bluetooth"
    ];

    private readonly Func<NetworkSample> _readSample;
    private NetworkSample? _previous;

    public NetworkMetricsReader()
        : this(ReadSystemSample)
    {
    }

    public NetworkMetricsReader(Func<NetworkSample> readSample)
    {
        _readSample = readSample;
    }

    public NetworkMetrics Read()
    {
        try
        {
            var current = _readSample();
            var previous = _previous;
            _previous = current;
            if (previous == null) return NetworkMetrics.Zero;

            var seconds = (current.Timestamp - previous.Timestamp).TotalSeconds;
            var receivedDelta = current.ReceivedBytes - previous.ReceivedBytes;
            var sentDelta = current.SentBytes - previous.SentBytes;
            if (seconds <= 0 || receivedDelta < 0 || sentDelta < 0)
            {
                return NetworkMetrics.Zero;
            }

            return new NetworkMetrics(receivedDelta / seconds, sentDelta / seconds);
        }
        catch
        {
            return NetworkMetrics.Empty;
        }
    }

    public static bool IsUsableAdapter(
        OperationalStatus operationalStatus,
        NetworkInterfaceType type,
        string? name,
        string? description)
    {
        if (operationalStatus != OperationalStatus.Up ||
            type is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown ||
            type is not (NetworkInterfaceType.Ethernet or
                NetworkInterfaceType.FastEthernetFx or
                NetworkInterfaceType.FastEthernetT or
                NetworkInterfaceType.GigabitEthernet or
                NetworkInterfaceType.Wireless80211))
        {
            return false;
        }

        var text = $"{name} {description}".ToLowerInvariant();
        return !VirtualAdapterKeywords.Any(text.Contains);
    }

    private static NetworkSample ReadSystemSample()
    {
        double received = 0;
        double sent = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!IsUsableAdapter(
                    adapter.OperationalStatus,
                    adapter.NetworkInterfaceType,
                    adapter.Name,
                    adapter.Description))
            {
                continue;
            }

            try
            {
                var stats = adapter.GetIPv4Statistics();
                received += Math.Max(0, stats.BytesReceived);
                sent += Math.Max(0, stats.BytesSent);
            }
            catch
            {
            }
        }

        return new NetworkSample(DateTimeOffset.UtcNow, received, sent);
    }

    public void Dispose()
    {
    }
}
