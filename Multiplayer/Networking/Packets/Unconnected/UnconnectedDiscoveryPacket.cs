using LiteNetLib.Utils;
using Multiplayer.Networking.Data;

namespace Multiplayer.Networking.Packets.Unconnected;

public enum DiscoveryPacketType : byte
{
    Discovery = 1,
    Response = 2,
}
public class UnconnectedDiscoveryPacket
{
    public DiscoveryPacketType PacketType { get; set; } = DiscoveryPacketType.Discovery;
    public LobbyServerData data { get; set; }
}
