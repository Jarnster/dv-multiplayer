using System;
using System.Net;
using System.Text;
using System.Collections.Generic;
using LiteNetLib;
using Multiplayer.Networking.Packets.Unconnected;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;


namespace Multiplayer.Networking.Listeners;

public class ServerBrowserClient : NetworkManager, IDisposable
{
    protected override string LogPrefix => "[SBClient]";
    private class PingInfo
    {
        public Stopwatch Stopwatch { get; } = new Stopwatch();
        public DateTime StartTime { get; private set; }
        public bool IPv4Received { get; set; }
        public bool IPv6Received { get; set; }
        public bool IPv4Sent { get; set; }
        public bool IPv6Sent { get; set; }

        public void Start()
        {
            StartTime = DateTime.Now;
            Stopwatch.Start();
        }
    }

    private Dictionary<string, PingInfo> pingInfos = new Dictionary<string, PingInfo>();
    public Action<string, int, bool, bool> OnPing; // serverId, pingTime, isIPv4, isIPv6

    private const int PingTimeoutMs = 5000; // 5 seconds timeout

    public ServerBrowserClient(Settings settings) : base(settings)
    {
    }

    public void Start()
    {
        Log($"ServerBrowserClient.Start()");
        netManager.Start();
    }
    public override void Stop()
    {
        base.Stop();
        Dispose();
    }

    public void Dispose()
    {
        foreach (var pingInfo in pingInfos.Values)
        {
            pingInfo.Stopwatch.Stop();
        }
        pingInfos.Clear();
    }
    private async Task CleanupTimedOutPings()
    {
        while (true)
        {
            await Task.Delay(PingTimeoutMs * 2);
            var now = DateTime.Now;
            var timedOutServers = pingInfos
                .Where(kvp => (now - kvp.Value.StartTime).TotalMilliseconds > PingTimeoutMs)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var serverId in timedOutServers)
            {
                pingInfos.Remove(serverId);
                Log($"Cleaned up timed out ping for {serverId}");
            }
        }
    }

    protected override void Subscribe()
    {
        netPacketProcessor.SubscribeReusable<UnconnectedPingPacket, IPEndPoint>(OnUnconnectedPingPacket);
    }

    #region Net Events

    public override void OnPeerConnected(NetPeer peer)
    {
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
    }

    public override void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
    }

    #endregion

    #region Listeners

    private void OnUnconnectedPingPacket(UnconnectedPingPacket packet, IPEndPoint endPoint)
    {
        string serverId = new Guid(packet.ServerID).ToString();
        Log($"OnUnconnectedPingPacket({serverId ?? ""}, {endPoint?.Address})");

        if (pingInfos.TryGetValue(serverId, out PingInfo pingInfo))
        {
            pingInfo.Stopwatch.Stop();
            int pingTime = (int)pingInfo.Stopwatch.ElapsedMilliseconds;

            bool isIPv4 = endPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
            if (isIPv4)
                pingInfo.IPv4Received = true;
            else
                pingInfo.IPv6Received = true;

            OnPing?.Invoke(serverId, pingTime, pingInfo.IPv4Received, pingInfo.IPv6Received);

            Log($"Ping received for {serverId}: {pingTime}ms, IPv4: {pingInfo.IPv4Received}, IPv6: {pingInfo.IPv6Received}");

            if (pingInfo.IPv4Received && pingInfo.IPv6Received)
            {
                pingInfos.Remove(serverId);
            }
        }
    }

    #endregion

    #region Senders
    public async Task SendUnconnectedPingPacket(string serverId, string ipv4, string ipv6, int port)
    {
        if (!Guid.TryParse(serverId, out Guid server))
        {
            LogError($"SendUnconnectedPingPacket({serverId}) failed to parse GUID");
            return;
        }

        PingInfo pingInfo = new PingInfo();
        pingInfos[serverId] = pingInfo;

        Log($"Sending ping to {serverId} at IPv4: {ipv4}, IPv6: {ipv6}, Port: {port}");
        pingInfo.Start();

        var packet = new UnconnectedPingPacket { ServerID = server.ToByteArray() };

        // Send to IPv4 if provided
        if (!string.IsNullOrEmpty(ipv4))
        {
            SendUnconnnectedPacket(packet, ipv4, port);
            pingInfo.IPv4Sent = true;
        }

        // Send to IPv6 if provided
        if (!string.IsNullOrEmpty(ipv6))
        {
            SendUnconnnectedPacket(packet, ipv6, port);
            pingInfo.IPv6Sent = true;
        }

        // Start a timeout task
        _ = StartTimeoutTask(serverId);
    }

    private async Task StartTimeoutTask(string serverId)
    {
        await Task.Delay(PingTimeoutMs);
        if (pingInfos.TryGetValue(serverId, out PingInfo pingInfo))
        {
            pingInfo.Stopwatch.Stop();
            OnPing?.Invoke(serverId, -1, pingInfo.IPv4Received, pingInfo.IPv6Received);
            pingInfos.Remove(serverId);
            Log($"Ping timeout for {serverId}");
        }
    }

    #endregion

    #region NAT Punch Events
    public override void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        //do some stuff here
    }
    public override void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        //do other stuff here
    }
    #endregion
}
