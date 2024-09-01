
namespace Multiplayer.Networking.Packets.Clientbound.Jobs;

public class ClientboundJobValidateResponsePacket
{
    public ushort JobNetId { get; set; }
    public bool Accepted { get; set; }
}
