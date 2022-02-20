using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public static class RpcTable {
        private static Dictionary<string, RpcObject> rpcs = new Dictionary<string, RpcObject>();

        public static void Register(RpcObject rpc) {
            rpcs[rpc.Name] = rpc;
        }

        public static bool TryGetRPC(string name, out RpcObject rpc) {
            return rpcs.TryGetValue(name, out rpc);
        }

        public static bool TryReadRPC(MemoryStream stream, out RpcObject rpc) {
            BinaryReader reader = new BinaryReader(stream);
            string rpcName = reader.ReadString();
            if (!TryGetRPC(rpcName, out rpc))
                return false;
            if (rpc.IsReliable)
                rpc.CallCount = reader.ReadInt32();
            rpc.Read(stream);
            return true;
        }

        public static void WriteRPC(MemoryStream stream, RpcObject rpc) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(rpc.Name);
            if (rpc.IsReliable)
                writer.Write(rpc.CallCount);
            rpc.Write(stream);
        }
    }
}
