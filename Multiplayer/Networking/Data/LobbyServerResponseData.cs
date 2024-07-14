using Multiplayer.Components.MainMenu;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Networking.Data
{
    public class LobbyServerResponseData
    {
 
        public string game_server_id { get; set; }
        public string private_key { get; set; }
        [JsonIgnore]
        public bool? ipv4_request{ get; set; }

        public LobbyServerResponseData(string game_server_id, string private_key, bool? ipv4_request = null)
        {
            this.game_server_id = game_server_id;
            this.private_key = private_key;
            this.ipv4_request = ipv4_request;
        }
    }
}
