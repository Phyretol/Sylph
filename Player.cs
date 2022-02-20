using Sylph.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sylph {
    public class Player : RpcEndPoint {
        private StreamConnection connection;
        private bool isHostPlayer;

        private Dictionary<int, ReplicationCommand> commands = new Dictionary<int, ReplicationCommand>();
        private Queue<ReplicationCommand> commandQueue = new Queue<ReplicationCommand>();
        private Queue<RpcObject> rpcQueue = new Queue<RpcObject>();

        public Player(StreamConnection connection, bool isHostPlayer = false) {
            this.connection = connection;
            this.isHostPlayer = isHostPlayer;
            foreach (var gameObject in NetworkManager.Instance.NetworkGameObjects)
                Show(gameObject);
        }

        internal StreamConnection Connection { get => connection; set => connection = value; }
        public bool IsHostPlayer { get => isHostPlayer; set => isHostPlayer = value; }

        public void Show(NetworkGameObject gameObject) {
            if (!isHostPlayer)
                ReplicateCreate(gameObject);
        }

        public void Hide(NetworkGameObject gameObject) {
            if (!isHostPlayer)
                ReplicateDestroy(gameObject);
        }

        public void ReceiveInput() {
            if (connection.Connected) {
                while (connection.HasData) {
                    byte[] buffer = connection.Receive();
                    MemoryStream stream = new MemoryStream(buffer);
                    ReadExecuteRPC(stream, this);
                }
            }
        }

        public void Update() {
            connection.Update();
        }

        public void SendStateUpdate() {
            UpdateCommands();
            if (connection.Connected) {
                while (commandQueue.Count > 0 || rpcQueue.Count > 0) {
                    Post(connection);
                }
            }
        }

        public void UpdateCommands() {
            commandQueue.Clear();
            foreach (ReplicationCommand command in commands.Values) {
                command.UpdateStateFlags();
                if (command.hasDirtyState)
                    commandQueue.Enqueue(command);
            }
        }

        public void ReplicateCreate(NetworkGameObject gameObject/*, BitVector32 stateFlags*/) {
            commands[gameObject.NetworkId] = new ReplicationCommand(CommandType.Create, gameObject);
            gameObject.ReplicationCount++;
        }

        public void ReplicateDestroy(NetworkGameObject gameObject) {
            ReplicateDestroy(gameObject.NetworkId);
        }

        public void ReplicateDestroy(int networkId) {
            if (commands.TryGetValue(networkId, out ReplicationCommand command)) {
                command.type = CommandType.Destroy;
                command.hasDirtyState = true;
            } else
                commands[networkId] = new ReplicationCommand(networkId);
        }

        public void Call(RpcObject rpc) {
            rpcQueue.Enqueue(rpc);
        }

        public void RepeatRPC(RpcObject rpc) {
            rpcQueue.Enqueue(rpc);
        }

        public void AcknowledgeCreate(NetworkGameObject gameObject) {
            if (commands.TryGetValue(gameObject.NetworkId, out ReplicationCommand command)
                && command.type != CommandType.Destroy)
                command.type = CommandType.Update;
        }

        public void AddStateFlags(NetworkGameObject gameObject, ReplicationCommand packetCommand) {
            if (commands.TryGetValue(gameObject.NetworkId, out ReplicationCommand tableCommand))
                tableCommand.AddStateFlags(packetCommand.stateFlags);
        }

        public void Post(StreamConnection connection) {
            List<ReplicationCommand> packetCommands = new List<ReplicationCommand>();
            List<RpcObject> packetRPCs = new List<RpcObject>();
            List<int> remove = new List<int>();

            MemoryStream createStream = new MemoryStream();
            MemoryStream updateStream = new MemoryStream();
            MemoryStream destroyStream = new MemoryStream();
            MemoryStream rpcStream = new MemoryStream();
            int createCount = 0, updateCount = 0, destroyCount = 0, rpcCount = 0;

            createStream.Position = 1;
            updateStream.Position = 1;
            destroyStream.Position = 1;
            rpcStream.Position = 1;
            int size = 4;

            MemoryStream tempStream = new MemoryStream();

            while (commandQueue.Count > 0) {
                var command = commandQueue.Peek();
                tempStream.Position = 0;

                switch (command.type) {
                    case CommandType.Create:
                        WriteCreate(tempStream, command);
                        break;
                    case CommandType.Update:
                        WriteUpdate(tempStream, command);
                        break;
                    case CommandType.Destroy:
                        WriteDestroy(tempStream, command);
                        break;
                }

                size += (int)tempStream.Position;
                if (size > NetworkManager.Instance.PacketSize
                    || createCount >= 255 || updateCount >= 255 || destroyCount >= 255)
                    break;

                switch (command.type) {
                    case CommandType.Create:
                        WriteCreate(createStream, command);
                        command.ClearStateFlags();
                        createCount++;
                        break;
                    case CommandType.Update:
                        WriteUpdate(updateStream, command);
                        command.ClearStateFlags();
                        updateCount++;
                        break;
                    case CommandType.Destroy:
                        WriteDestroy(destroyStream, command);
                        remove.Add(command.networkId);
                        destroyCount++;
                        break;
                }
                commandQueue.Dequeue();
                packetCommands.Add(command);
            }

            while (rpcQueue.Count > 0) {
                var rpc = rpcQueue.Peek();
                tempStream.Position = 0;
                WriteRPC(tempStream, rpc);

                size += (int)tempStream.Position;
                if (size > NetworkManager.Instance.PacketSize)
                    break;
                WriteRPC(rpcStream, rpc);
                rpcCount++;
                rpcQueue.Dequeue();
                packetRPCs.Add(rpc);
            }

            createStream.Position = 0;
            createStream.WriteByte((byte)createCount);
            updateStream.Position = 0;
            updateStream.WriteByte((byte)updateCount);
            destroyStream.Position = 0;
            destroyStream.WriteByte((byte)destroyCount);
            rpcStream.Position = 0;
            rpcStream.WriteByte((byte)rpcCount);

            byte[] buffer = createStream.ToArray()
                .Concat(updateStream.ToArray())
                .Concat(destroyStream.ToArray())
                .Concat(rpcStream.ToArray())
                .ToArray();

            foreach (int networkId in remove)
                commands.Remove(networkId);

            StatePacketHandler handler = new StatePacketHandler(this, packetCommands, packetRPCs);
            connection.Send(buffer, handler);
        }

        private static void WriteCreate(MemoryStream stream, ReplicationCommand command) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(command.gameObject.NetworkId);
            writer.Write(command.gameObject.TypeName);
            WriteComponents(stream, command);
        }

        private static void WriteUpdate(MemoryStream stream, ReplicationCommand command) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(command.gameObject.NetworkId);
            WriteComponents(stream, command);
        }

        private static void WriteComponents(MemoryStream stream, ReplicationCommand command) {
            BinaryWriter writer = new BinaryWriter(stream);
            int componentFlags = 0;
            long componentFlagsPosition = stream.Position;
            stream.Position += sizeof(int);
            for (int i = 0; i < command.gameObject.Components.Length; i++) {
                if (command.stateFlags[i] == 0)
                    continue;
                writer.Write(command.stateFlags[i]);
                command.gameObject.Components[i].Write(stream, command.stateFlags[i]);
                componentFlags = SylphUtils.SetBit(componentFlags, i);
            }
            long positionTemp = stream.Position;
            stream.Position = componentFlagsPosition;
            writer.Write(componentFlags);
            stream.Position = positionTemp;
        }

        private void WriteDestroy(MemoryStream stream, ReplicationCommand command) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(command.networkId);
        }

        public void ResetCommands() {
            List<int> remove = new List<int>();
            foreach (ReplicationCommand command in commands.Values) {
                switch (command.type) {
                    case CommandType.Update:
                        command.type = CommandType.Create;
                        break;
                    case CommandType.Destroy:
                        remove.Add(command.networkId);
                        continue;
                }
                command.ResetStateFlags();
            }
            foreach (int networkId in remove)
                commands.Remove(networkId);
        }
    }

}
