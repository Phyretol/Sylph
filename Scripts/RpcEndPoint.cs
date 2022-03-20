using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public class RpcEndPoint {
        public class RPCTemp {
            public RpcObject rpc;
            public MemoryStream data;
        }

        private int rpcCallCountIn = 0;
        private int rpcCallCountOut = 0;

        private SortedDictionary<int, RPCTemp> rpcBuffer = new SortedDictionary<int, RPCTemp>();

        public void WriteRPC(MemoryStream stream, RpcObject rpc) {
            if (rpc.IsReliable && rpc.CallCount < 0)
                rpc.CallCount = rpcCallCountOut++;
            RpcTable.WriteRPC(stream, rpc);
        }

        public bool ReadExecuteRPC(MemoryStream stream, Player caller = null) {
            if (!RpcTable.TryReadRPC(stream, out RpcObject rpc))
                return false;
            if (rpc.IsReliable) {
                if (rpc.CallCount == rpcCallCountIn) {
                    rpc.Execute(caller);
                    rpcCallCountIn++;
                    ExecuteRPCQueue(caller);
                } else if (rpc.CallCount > rpcCallCountIn) {
                    var data = new MemoryStream();
                    rpc.Write(data);
                    data.Position = 0;
                    rpcBuffer[rpc.CallCount] = new RPCTemp() {
                        rpc = rpc,
                        data = data
                    };
                }
            } else
                rpc.Execute(caller);
            return true;
        }

        private void ExecuteRPCQueue(Player caller) {
            while (rpcBuffer.Count > 0) {
                int minCallCount = rpcBuffer.Keys.First();
                if (minCallCount != rpcCallCountIn)
                    return;
                var rpcTemp = rpcBuffer.Values.First();
                rpcTemp.rpc.Read(rpcTemp.data);
                rpcTemp.rpc.Execute(caller);
                rpcBuffer.Remove(minCallCount);
                rpcCallCountIn++;
            }
        }
    }
}
