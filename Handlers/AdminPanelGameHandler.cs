using OCEAdmin.Commands;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.MountAndBlade;
using OCEAdmin.Panel.Controllers;

namespace OCEAdmin.Panel.Handlers
{
    class AdminPanelGameHandler : GameHandler
    {
        public override void OnAfterSave() { }

        public override void OnBeforeSave() { }

        protected override void OnPlayerDisconnect(VirtualPlayer peer)
        {
           _ = WebSocketController.RemoveClient(peer.Id.ToString());
        }
    }
}
