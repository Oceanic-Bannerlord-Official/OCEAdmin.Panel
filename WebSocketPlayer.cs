using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace OCEAdmin.Panel
{
    public class WebSocketPlayer
    {
        public WebSocket socket;

        public NetworkCommunicator networkPeer;

        public WebSocketPlayer() { }

        public WebSocketPlayer(WebSocket socket, NetworkCommunicator networkPeer) 
        {
            this.socket = socket;
            this.networkPeer = networkPeer;
        }
    }
}
