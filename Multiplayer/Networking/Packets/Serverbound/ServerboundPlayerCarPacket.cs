using UnityEngine;

namespace Multiplayer.Networking.Packets.Serverbound;

public class ServerboundPlayerCarPacket
{
    public ushort CarId { get; set; }
    public Vector3 Position { get; set; }
    public Vector2 MoveDir { get; set; }
    public float RotationY { get; set; }
}
