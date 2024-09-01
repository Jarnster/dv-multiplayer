namespace Multiplayer.Networking.Packets.Clientbound.Jobs;

public enum ValidationType : byte
{
    JobOverview,
    JobBooklet
}
public class ServerboundJobValidateRequestPacket
{
    public ushort JobNetId { get; set; }
    public ushort StationNetId { get; set; }
    public ValidationType validationType { get; set; }
}
