using Sylph.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public class ClientRpcPacketHandler : DeliveryHandler {
        private RpcObject rpc;

        public ClientRpcPacketHandler(RpcObject rpc) {
            this.rpc = rpc;
        }

        public void HandleDeliveryFailure() {
            NetworkManager.Instance.CallOnServer(rpc);
        }

        public void HandleDeliverySuccess() { }
    }
}
