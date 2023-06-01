using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OCEAdmin.Commands;
using OCEAdmin.Panel.Controllers;
using OCEAdmin.Shared.Network;
using OCEAdmin.Shared.Network.Attributes;
using OCEAdmin.Shared.Network.FromClient;
using OCEAdmin.Shared.Network.FromServer;
using OCEAdmin.Shared.Network.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace OCEAdmin.Panel.Handlers
{
    public class PacketHandler
    {
        [Handles(typeof(ClientRequestSync))]
        public async Task ClientRequestSync(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            ClientRequestSync packet = (ClientRequestSync)packetObj;

            MissionData missionData = AdminPanel.Instance.getMultiplayerOptionsState();

            ServerSyncResponse syncPacket = new ServerSyncResponse();
            syncPacket.missionData = missionData;

            Dictionary<string, List<string>> maps = new Dictionary<string, List<string>>();

            var gameModes = MultiplayerOptions.Instance.GetMultiplayerOptionsList(MultiplayerOptions.OptionType.GameType);
            gameModes.Sort();

            foreach(string gamemode in gameModes)
            {
                List<string> gamemodeMaps = AdminPanel.Instance.GetMapsForGameType(gamemode);

                maps.Add(gamemode, gamemodeMaps);
            }

            // There's custom maps we want to inject into
            // the OCEAdmin list.
            if(SubModule.customMaps.ContainsKey(missionData.gameType))
            {
                maps[missionData.gameType] = maps[missionData.gameType].Union(SubModule.customMaps[missionData.gameType]).ToList();
            }

            syncPacket.Maps = maps;

            await WebSocketController.Send(webSocketPlayer, syncPacket);
        }

        [Handles(typeof(ClientRequestMission))]
        public async Task ClientRequestMission(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            ClientRequestMission packet = (ClientRequestMission)packetObj;

            MissionData curData = AdminPanel.Instance.getMultiplayerOptionsState();

            // Update the timers
            if(curData.warmupTime != packet.missionData.warmupTime)
                MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.WarmupTimeLimit)
                    .UpdateValue(Math.Clamp(packet.missionData.warmupTime, 1, 45));

            if (curData.roundTime != packet.missionData.roundTime)
            {
                MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.RoundTimeLimit)
                    .UpdateValue(Math.Clamp(packet.missionData.roundTime, 60, 2700));

                // We're expecting the client to be giving us this value in seconds.
                MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.MapTimeLimit).
                    UpdateValue(Math.Clamp(packet.missionData.roundTime / 60, 1, 60));
            }

            if (curData.numRounds != packet.missionData.numRounds)
                MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.RoundTotal).
                    UpdateValue(Math.Clamp(packet.missionData.numRounds, 1, 19));

            AdminPanel.Instance.ChangeGameTypeMapFactions(packet.missionData.gameType,
                packet.missionData.mapId,
                packet.missionData.cultureTeam1,
                packet.missionData.cultureTeam2);
        }

        [Handles(typeof(ClientUpdateMapTime))]
        public async Task ClientUpdateMapTime(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            ClientUpdateMapTime packet = (ClientUpdateMapTime)packetObj;

            var options = MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions;
            MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.MapTimeLimit, options).UpdateValue(packet.mapTime);

            AdminPanel.Instance.SetMapTime(packet.mapTime);

            var timers = AdminPanel.Instance.GetTimers();

            await WebSocketController.Broadcast(new ServerSyncTimers(timers.warmupTime, timers.mapTime, timers.roundTime));
        }

        [Handles(typeof(ClientUpdateRoundTime))]
        public async Task ClientUpdateRoundTime(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            ClientUpdateRoundTime packet = (ClientUpdateRoundTime)packetObj;

            var options = MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions;
            MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.RoundTimeLimit, options).UpdateValue(packet.roundTime);

            AdminPanel.Instance.SetRoundTime(packet.roundTime);

            var timers = AdminPanel.Instance.GetTimers();

            await WebSocketController.Broadcast(new ServerSyncTimers(timers.warmupTime, timers.mapTime, timers.roundTime));
        }

        [Handles(typeof(ClientUpdateWarmupTime))]
        public async Task ClientUpdateWarmupTime(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            ClientUpdateWarmupTime packet = (ClientUpdateWarmupTime)packetObj;

            var options = MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions;
            MultiplayerOptions.Instance.GetOptionFromOptionType(MultiplayerOptions.OptionType.WarmupTimeLimit, options).UpdateValue(packet.warmupTime);

            AdminPanel.Instance.SetWarmupTime(packet.warmupTime);

            var timers = AdminPanel.Instance.GetTimers();

            await WebSocketController.Broadcast(new ServerSyncTimers(timers.warmupTime, timers.mapTime, timers.roundTime));
        }

        [Handles(typeof(ClientRequestEndWarmup))]
        public async Task ClientRequestEndWarmup(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            MPUtil.WriteToConsole("Ending warmup");
            AdminPanel.Instance.EndWarmup();
        }

        [Handles(typeof(ClientRequestCommand))]
        public async Task ClientRequestCommand(object packetObj, WebSocketPlayer webSocketPlayer)
        {
            ClientRequestCommand packet = (ClientRequestCommand)packetObj;

            CommandManager.Execute(webSocketPlayer.networkPeer, packet.command, packet.args).Log();
        }
    }
}
