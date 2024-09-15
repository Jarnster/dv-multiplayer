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
    public Action<string, int, bool> OnPing; // serverId, pingTime, isIPv4

    private const int PingTimeoutMs = 5000; // 5 seconds timeout

    public ServerBrowserClient(Settings settings) : base(settings)
    {
    }

    public void Start()
    {
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
                LogDebug(() => $"Cleaned up timed out ping for {serverId}");
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
        //Log($"OnUnconnectedPingPacket({serverId ?? ""}, {endPoint?.Address})");

        if (pingInfos.TryGetValue(serverId, out PingInfo pingInfo))
        {
            int pingTime = (int)pingInfo.Stopwatch.ElapsedMilliseconds;

            bool isIPv4 = endPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

            if (isIPv4)
                pingInfo.IPv4Received = true;
            else
                pingInfo.IPv6Received = true;

            OnPing?.Invoke(serverId, pingTime, isIPv4);

            LogDebug(()=>$"OnUnconnectedPingPacket() serverId {serverId}, IPv4 ({pingInfo.IPv4Sent}, {pingInfo.IPv4Received}), IPv6 ({pingInfo.IPv6Sent}, {pingInfo.IPv6Received})");
            if ((!pingInfo.IPv4Sent || pingInfo.IPv4Received) && (!pingInfo.IPv6Sent || pingInfo.IPv6Received))
            {
                pingInfo.Stopwatch.Stop();
                pingInfos.Remove(serverId);
                LogDebug(()=>$"OnUnconnectedPingPacket() removed {serverId}");
            }
        }
    }

    #endregion

    #region Senders
    public void SendUnconnectedPingPacket(string serverId, string ipv4, string ipv6, int port)
    {
        if (!Guid.TryParse(serverId, out Guid server))
        {
            LogError($"SendUnconnectedPingPacket({serverId}) failed to parse GUID");
            return;
        }

        PingInfo pingInfo = new PingInfo();
        pingInfos[serverId] = pingInfo;

        LogDebug(()=>$"Sending ping to {serverId} at IPv4: {ipv4}, IPv6: {ipv6}, Port: {port}");
        var packet = new UnconnectedPingPacket { ServerID = server.ToByteArray() };

        pingInfo.Start();

        // Send to IPv4 if provided
        if (!string.IsNullOrEmpty(ipv4))
        {
            SendUnconnectedPacket(packet, ipv4, port);
            pingInfo.IPv4Sent = true;
        }

        // Send to IPv6 if provided
        if (!string.IsNullOrEmpty(ipv6))
        {
            SendUnconnectedPacket(packet, ipv6, port);
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
            LogDebug(() => $"Ping timeout for {serverId}, elapsed: {pingInfo.Stopwatch.ElapsedMilliseconds}, IPv4: ({pingInfo.IPv4Sent}, {pingInfo.IPv4Received}), IPv6: ({pingInfo.IPv6Sent}, {pingInfo.IPv6Received}) ");

            if(!pingInfo.IPv4Received && pingInfo.IPv4Sent)
                OnPing?.Invoke(serverId, -1, true);

            if (!pingInfo.IPv6Received && pingInfo.IPv6Sent)
                OnPing?.Invoke(serverId, -1, false);


            pingInfos.Remove(serverId);
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
