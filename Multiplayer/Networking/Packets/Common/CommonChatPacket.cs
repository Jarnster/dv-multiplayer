using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Networking.Packets.Common;

public class CommonChatPacket
{

    public string message { get; set; }
    public MessageType type { get; set; }

}

public enum MessageType
{
    ServerMessage,
    Chat,
    Whisper
}
