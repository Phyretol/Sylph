using Sylph.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public class StatePacketHandler : DeliveryHandler {
        private List<ReplicationCommand> commands;
        private List<RpcObject> rpcs;
        private Player player;

        public StatePacketHandler(Player player, List<ReplicationCommand> commands, List<RpcObject> rpcs) {
            this.player = player;
            this.commands = commands;
            this.rpcs = rpcs;
        }

        public void HandleDeliveryFailure() {
            foreach (var command in commands) {
                switch (command.type) {
                    case CommandType.Create:
                    case CommandType.Update:
                        player.AddStateFlags(command.gameObject, command);
                        break;
                    case CommandType.Destroy:
                        player.ReplicateDestroy(command.networkId);
                        break;
                }
            }
            foreach (var rpc in rpcs) {
                if (rpc.IsReliable)
                    player.RepeatRPC(rpc);
            }
        }

        public void HandleDeliverySuccess() {
            foreach (ReplicationCommand command in commands) {
                switch (command.type) {
                    case CommandType.Create:
                        player.AcknowledgeCreate(command.gameObject);
                        break;
                }
            }
        }
    }
}
