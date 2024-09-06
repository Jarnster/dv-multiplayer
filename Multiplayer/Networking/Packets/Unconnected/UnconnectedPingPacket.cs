using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Networking.Packets.Unconnected
{
    public class UnconnectedPingPacket
    {
        public byte[] ServerID { get; set; }
    }
}
