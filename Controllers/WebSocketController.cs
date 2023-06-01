using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using OCEAdmin.Panel.Handlers;
using OCEAdmin.Shared.Network.Attributes;
using OCEAdmin.Shared.Network;
using OCEAdmin.Panel.SteamAPI;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Net.Sockets;
using TaleWorlds.MountAndBlade;
using System.Linq;
using TaleWorlds.MountAndBlade.Diamond;
using OCEAdmin.Shared.Network.FromServer;

namespace OCEAdmin.Panel.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebSocketController : ControllerBase
    {
        public static List<WebSocketPlayer> _connectedClients = new List<WebSocketPlayer>();
        private readonly RequestDelegate next;

        public WebSocketController(RequestDelegate next)
        {
            this.next = next;
        }

        [HttpGet("/")]
        public async Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                string address = context.Connection.RemoteIpAddress.ToString();

                // Check the custom header for the game ID
                if (context.Request.Headers.TryGetValue("gameID", out var gameID) && context.Request.Headers.TryGetValue("steamID", out var steamID)
                    && context.Request.Headers.TryGetValue("steamTicket", out var steamTicket))
                {
                    bool authed = await this.Challenge(context, gameID.FirstOrDefault(),
                        steamID.FirstOrDefault(), steamTicket.FirstOrDefault());

                    if (authed)
                    {
                        WebSocketPlayer webSocketPlayer = new WebSocketPlayer();

                        // Proceed with the WebSocket connection.
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        // Encapsulate the webSocket with the TaleWorlds object.
                        webSocketPlayer.networkPeer = MPUtil.GetPeerFromIDNoSync(gameID);
                        webSocketPlayer.socket = webSocket;

                        // Add the player to the connected clients.
                        await AddClient(webSocketPlayer);

                        // Let the client know they've been authed.
                        await Send(webSocketPlayer, new ServerAuthResponse(true));

                        try
                        {
                            await HandleWebSocket(webSocketPlayer);
                        }
                        catch (Exception ex)
                        {
                            // Handle any exceptions that occur during WebSocket communication
                            MPUtil.WriteToConsole("WebSocket error: " + ex.Message);
                        }
                        finally
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "WebSocket connection terminated.", CancellationToken.None);
                        }
                    }
                }

                // Do nothing. Drop the request.
                // todo - poll rate?
            }
            else
            {
                await next.Invoke(context);
            }
        }

        public async Task<bool> Challenge(HttpContext context, string gameID, string steamID, string steamTicket)
        {
            NetworkCommunicator networkPeer = MPUtil.GetPeerFromIDNoSync(gameID);

            // todo - maybe generate a fingerprint to reduce api requests?
            // attach socket id + hwid + ip?

            bool auth = await SteamAuth.ValidateSteamTicket(steamID, steamTicket);

            if (networkPeer == null || !auth)
                return false;

            if (!networkPeer.HasAdmin())
                return false;

            return true;
        }

        private async Task HandleWebSocket(WebSocketPlayer webSocketPlayer)
        {
            while (webSocketPlayer.socket.State == WebSocketState.Open)
            {
                byte[] buffer = new byte[8096];
                WebSocketReceiveResult result = await webSocketPlayer.socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocketPlayer.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "WebSocket connection stopped.", CancellationToken.None);
                }
                else if(result.MessageType == WebSocketMessageType.Text)
                {
                    string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // MPUtil.WriteToConsole("Received JSON: " + jsonString, true);

                    // Deserialize JSON into appropriate packet object based on packet ID
                    JObject jsonObject = JObject.Parse(jsonString);
                    int packetId = jsonObject["packetId"].Value<int>();

                    Type packetType = GetPacketType(packetId);

                    if (packetType != null)
                    {
                        MPUtil.WriteToConsole(packetType.ToString());
                        object packetObject = jsonObject.ToObject(packetType);

                        // Process the packet object as needed
                        ProcessPacket(packetObject, webSocketPlayer);
                    }
                }
            }
        }

        public static async Task Send(WebSocketPlayer webSocketPlayer, object packetObject)
        {
            string responseJson = JsonConvert.SerializeObject(packetObject);

            // Encode response JSON and send it back to the client
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
            await webSocketPlayer.socket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private Type GetPacketType(int packetId)
        {
            foreach (KeyValuePair<int, Type> packetEntry in PacketRegistry.storage)
            {
                if (packetEntry.Key == packetId)
                {
                    return packetEntry.Value;
                }
            }

            MPUtil.WriteToConsole("Could not find matching packet");

            return null; // No matching packet type found
        }

        private void ProcessPacket(object packetObject, WebSocketPlayer webSocketPlayer)
        {
            Type packetType = packetObject.GetType();
            PacketHandler packetHandler = new PacketHandler();
            MethodInfo[] methods = typeof(PacketHandler).GetMethods();

            foreach (MethodInfo method in methods)
            {
                Handles handlesAttribute = method.GetCustomAttribute<Handles>();
                if (handlesAttribute != null && handlesAttribute.Packet == packetType)
                {
                    method.Invoke(packetHandler, new object[] { packetObject, webSocketPlayer });
                    return;
                }
            }

            // Handle case when no matching method is found
            MPUtil.WriteToConsole("No matching handler method found for packet type: " + packetType);
        }

        public static async Task AddClient(WebSocketPlayer player)
        {
            _connectedClients.Add(player);
        }

        public static async Task RemoveClient(string id)
        {
            foreach(NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if(peer.VirtualPlayer.Id.ToString() == id)
                {
                    await RemoveClient(peer);
                }
            }
        }

        public static async Task RemoveClient(NetworkCommunicator peer)
        {
            foreach (WebSocketPlayer player in _connectedClients)
            {
                if (player.networkPeer != null)
                {
                    if (player.networkPeer.VirtualPlayer.Id.ToString() == peer.VirtualPlayer.Id.ToString())
                    {
                        await player.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "WebSocket connection terminated.", CancellationToken.None);

                        MPUtil.WriteToConsole("Terminating websocket for " + player.networkPeer.GetUsername());
                        _connectedClients.Remove(player);
                    }
                }
            }
        }

        public static async Task Broadcast(object packetObject)
        {
            foreach (WebSocketPlayer player in _connectedClients)
            {
                if (player.socket.State == WebSocketState.Open)
                {
                    await Send(player, packetObject);
                }
            }
        }

    }
}
