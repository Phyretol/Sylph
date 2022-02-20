using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Sylph.Networking;

namespace Sylph {
    public class RPCTemp {
        public RpcObject rpc;
        public MemoryStream data;
    }

    public class GameClient : RpcEndPoint {
        private EndPoint localEndPoint;
        private EndPoint serverEndPoint;
        private StreamConnection connection;
        private bool connected = false;
        private bool running = false;

        public GameClient(EndPoint localEndPoint, EndPoint serverEndPoint) {
            this.localEndPoint = localEndPoint;
            this.serverEndPoint = serverEndPoint;
            connection = new StreamConnection(localEndPoint);
        }

        public StreamConnection Connection { get => connection; }

        public void Start() {
            connection.Connect(serverEndPoint);
            running = true;
        }

        public void Update() {
            if (!running)
                return;

            connection.Update();
            if (connection.Connected) {
                if (!connected) {
                    connected = true;
                    NetworkManager.Instance.ClientConnected?.Invoke();
                }
                while (connection.HasData) {
                    byte[] buffer = connection.Receive();
                    ProcessStateUpdate(buffer);
                }
            } else {
                if (connection.State == ConnectionState.Default) {
                    NetworkManager.Instance.ClientDisconnected?.Invoke();
                    running = false;
                }
            }
        }

        public void Send(RpcObject rpc) {
            MemoryStream stream = new MemoryStream();
            WriteRPC(stream, rpc);
            Send(stream.ToArray(), new ClientRpcPacketHandler(rpc));
        }

        private void Send(byte[] buffer) {
            connection.SendSequence(buffer);
        }

        private void Send(byte[] buffer, DeliveryHandler deliveryHandler) {
            connection.Send(buffer, deliveryHandler);
        }

        private void ProcessStateUpdate(byte[] buffer) {
            MemoryStream stream = new MemoryStream(buffer);
            BinaryReader reader = new BinaryReader(stream);

            int createCount = reader.ReadByte();
            for (int i = 0; i < createCount; i++) {
                int networkId = reader.ReadInt32();
                if (!NetworkManager.Instance.TryGetGameObject(networkId, out NetworkGameObject gameObject)) {
                    string typeName = reader.ReadString();
                    gameObject = NetworkManager.Instance.CreateGameObject(typeName);
                    gameObject.NetworkId = networkId;
                    gameObject.Spawn();
                } else
                    reader.ReadString();

                ReadGameObject(stream, gameObject);
            }

            int updateCount = reader.ReadByte();
            for (int i = 0; i < updateCount; i++) {
                int networkId = reader.ReadInt32();
                if (NetworkManager.Instance.TryGetGameObject(networkId, out NetworkGameObject gameObject)) {
                    ReadGameObject(stream, gameObject);
                } else
                    return;
            }

            int destroyCount = reader.ReadByte();
            for (int i = 0; i < destroyCount; i++) {
                int networkId = reader.ReadInt32();
                if (NetworkManager.Instance.TryGetGameObject(networkId, out NetworkGameObject gameObject)) {
                    gameObject.Kill();
                    NetworkManager.Instance.DestroyGameObject(gameObject);
                }
            }

            int rpcCount = reader.ReadByte();
            for (int i = 0; i < rpcCount; i++) {
                if (!ReadExecuteRPC(stream))
                    return;
            }
        }

        private static void ReadGameObject(MemoryStream stream, NetworkGameObject gameObject) {
            BinaryReader reader = new BinaryReader(stream);
            int componentFlags = reader.ReadInt32();
            for (int i = 0; i < gameObject.Components.Length; i++) {
                if (!SylphUtils.GetBit(componentFlags, i))
                    continue;
                int stateFlags = reader.ReadInt32();
                gameObject.Components[i].Read(stream, stateFlags);
            }
        }
    }
}
