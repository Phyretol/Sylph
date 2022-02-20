using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Sylph.Networking {
    public interface DeliveryHandler {
        void HandleDeliveryFailure();
        void HandleDeliverySuccess();
    }

    public enum ConnectionState {
        Default,
        Connecting,
        AwaitingConnection,
        Connected
    }

    public enum ServiceType {
        ConnectionRequest,
        ConnectionAccept,
        Acknowledge,
        Heartbeat,
        Disconnect
    }

    public class StreamConnection {
        private class PacketDeliveryData {
            public int packetId;
            public DeliveryHandler handler;
            public float time;

            public PacketDeliveryData(int packetId, DeliveryHandler handler, float time) {
                this.packetId = packetId;
                this.handler = handler;
                this.time = time;
            }
        }

        private class PacketCollectionData : IComparable {
            public int packetId;
            public int effectivePacketId;
            public MemoryStream stream;

            public PacketCollectionData(int packetId, int effectivePacketId, MemoryStream stream) {
                this.packetId = packetId;
                this.stream = stream;
                this.effectivePacketId = effectivePacketId;
            }

            public int CompareTo(object obj) {
                PacketCollectionData collectionData = (PacketCollectionData)obj;
                return effectivePacketId.CompareTo(collectionData.effectivePacketId);
            }
        }

        private const float connectionRequestInterval = 0.5f;

        private const int wrapAroundWindow = 1000;
        private const int bufferSizeIn = 32;

        private const float initialTimeout = 0.5f;
        private float estimatedRTT;
        private float devRTT;
        private const float beta = 0.25f;
        private const float alpha = 0.125f;

        private float connectionRequestTime;
        private float timeoutDuration;

        private float packetTimeIn;
        private float packetTimeOut;
        private const float heartbeatInterval = 1f;
        private const float disconnectionTimeout = 20f;

        private ConnectionState state;
        private ConnectionGroup connectionGroup;
        private Socket socket;

        private EndPoint remoteEndPoint;
        private ushort sequenceNumberOut;
        private Dictionary<int, PacketDeliveryData> deliveryDatas;
        private List<int> pendingAcknowledges;

        private EndPoint localEndPoint;
        private ushort sequenceNumberIn;
        private List<byte[]> buffers;
        private SortedSet<PacketCollectionData> collectionDatas;

        private Stopwatch stopwatch;
        private float time;

        private Queue<StreamConnection> pendingConnections;

        private List<int> removeTemp = new List<int>();
        private List<PacketDeliveryData> deliveryDatasTemp = new List<PacketDeliveryData>();

        public StreamConnection(EndPoint localEndPoint) {
            collectionDatas = new SortedSet<PacketCollectionData>();
            deliveryDatas = new Dictionary<int, PacketDeliveryData>();

            buffers = new List<byte[]>();
            pendingAcknowledges = new List<int>();

            this.localEndPoint = localEndPoint;
            sequenceNumberIn = sequenceNumberOut = 1;
            estimatedRTT = devRTT = timeoutDuration = initialTimeout;
            connectionRequestTime = float.NegativeInfinity;
            state = ConnectionState.Default;

            stopwatch = new Stopwatch();
        }

        public Socket Socket { get => socket; set => socket = value; }
        public ConnectionGroup ConnectionGroup { get => connectionGroup; set => connectionGroup = value; }
        public EndPoint LocalEndPoint { get => localEndPoint; }
        public EndPoint RemoteEndPoint { get => remoteEndPoint; set => remoteEndPoint = value; }
        public bool Connected { get => state == ConnectionState.Connected; }
        public ConnectionState State { get => state; }

        public void Connect(EndPoint endPoint) {
            try {
                state = ConnectionState.Connecting;
                remoteEndPoint = endPoint;
                ConnectionManager.Instance.AddConnection(this);
                stopwatch.Start();
            } catch (SocketException e) {
                state = ConnectionState.Default;
            }
        }

        public void Listen() {
            try {
                state = ConnectionState.AwaitingConnection;
                pendingConnections = new Queue<StreamConnection>();
                ConnectionManager.Instance.AddListenerConnection(this);
                stopwatch.Start();
            } catch (SocketException e) {
                state = ConnectionState.Default;
            }
        }

        public void Start() {
            try {
                ConnectionManager.Instance.AddConnection(this);
                stopwatch.Start();
                StartConnection();
            } catch (SocketException e) {
                state = ConnectionState.Default;
            }
        }

        public void AddPendingConnection(StreamConnection connection) {
            pendingConnections.Enqueue(connection);
        }

        public bool HasPendingConnections() {
            return pendingConnections.Count > 0;
        }

        public StreamConnection AcceptConnection() {
            var connection = pendingConnections.Dequeue();
            connection.Start();
            return connection;
        }

        public void StartConnection() {
            packetTimeIn = packetTimeIn = 0f;
            SendConnectionAccept();
            state = ConnectionState.Connected;
        }

        public void Update() {
            
            ConnectionManager.Instance.Receive(this);
            time = (float)stopwatch.Elapsed.TotalSeconds;
            ProcessBuffer();

            switch (state) {
                case ConnectionState.Connecting:
                    if (time - connectionRequestTime >= connectionRequestInterval) {
                        SendConnectionRequest();
                        connectionRequestTime = time;
                    }
                    break;
                case ConnectionState.Connected:
                    bool timeout = false;
                    foreach (PacketDeliveryData deliveryData in deliveryDatas.Values) {
                        if (time - deliveryData.time >= timeoutDuration) {
                            deliveryData.handler.HandleDeliveryFailure();
                            removeTemp.Add(deliveryData.packetId);
                            timeout = true;
                        }
                    }
                    if (timeout) {
                        foreach (int id in removeTemp) {
                            deliveryDatas.Remove(id);
                            timeoutDuration *= 1.5f;
                        }
                        removeTemp.Clear();
                    }
                    foreach (var deliveryData in deliveryDatasTemp)
                        deliveryDatas[deliveryData.packetId] = deliveryData;
                    deliveryDatasTemp.Clear();
                    
                    if (time - packetTimeIn >= disconnectionTimeout)
                        Disconnect();
                    if (pendingAcknowledges.Count > 0)
                        SendAcknowledge();
                    if (time - packetTimeOut >= heartbeatInterval)
                        SendHeartbeat();
                    break;
            }
        }

        public byte[] Receive() {
            PacketCollectionData minCollectionData = collectionDatas.Min;
            int old = sequenceNumberIn;
            sequenceNumberIn = (ushort)(minCollectionData.packetId + 1);
            if (sequenceNumberIn < old) {
                foreach (PacketCollectionData collectionData in collectionDatas) {
                    collectionData.effectivePacketId = collectionData.packetId;
                }
            }
            collectionDatas.Remove(minCollectionData);
            pendingAcknowledges.Add(minCollectionData.packetId);

            return minCollectionData.stream.ToArray().Skip(2).ToArray();
        }

        internal void AddToBuffer(byte[] packetData) {
            if (buffers.Count >= bufferSizeIn)
                return;
            buffers.Add(packetData);
        }

        public bool HasData { get => collectionDatas.Count > 0; }

        public void Send(byte[] data, DeliveryHandler handler) {
            ushort packetId = sequenceNumberOut++;
            if (sequenceNumberOut == 0)
                sequenceNumberOut++;
            PacketDeliveryData deliveryData = new PacketDeliveryData(packetId, handler, time);
            deliveryDatasTemp.Add(deliveryData);

            data = AppendPacketId(data, packetId);
            packetTimeOut = time;
            Send(data);
        }

        private static byte[] AppendPacketId(byte[] data, ushort packetId) {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(packetId);
            return stream.ToArray().Concat(data).ToArray();
        }

        public void SendSequence(byte[] data) {
            ushort packetId = sequenceNumberOut++;
            if (sequenceNumberOut == 0)
                sequenceNumberOut++;
            data = AppendPacketId(data, packetId);
            packetTimeOut = time;
            Send(data);
        }

        private void Send(byte[] data) {
            try {
                socket.SendTo(data, RemoteEndPoint);
            } catch (SocketException e) {
                Disconnect();
            }
        }

        private void ProcessBuffer() {
            foreach (byte[] buffer in buffers) {
                MemoryStream stream = new MemoryStream(buffer);
                BinaryReader reader = new BinaryReader(stream);
                ushort packetId = reader.ReadUInt16();

                if (packetId == 0) {
                    ProcessServicePacket(stream);
                } else {
                    if (state == ConnectionState.Connected)
                        ProcessSequencePacket(packetId, stream);
                }
            }

            buffers.Clear();
        }

        private void ProcessServicePacket(MemoryStream stream) {
            BinaryReader reader = new BinaryReader(stream);
            ServiceType serviceType = (ServiceType)reader.ReadByte();
            switch (serviceType) {
                case ServiceType.ConnectionRequest:
                    SendConnectionAccept();
                    break;
                case ServiceType.ConnectionAccept:
                    ReadConnectionAccept(stream);
                    break;
                case ServiceType.Acknowledge:
                    if (state == ConnectionState.Connected)
                        ReadAcknowledge(reader);
                    break;
                case ServiceType.Heartbeat:
                    packetTimeIn = time;
                    break;
            }
        }

        private void ProcessSequencePacket(ushort packetId, MemoryStream stream) {
            int effectivePacketId = packetId;
            if (packetId < sequenceNumberIn) {
                if (packetId < wrapAroundWindow / 2 && sequenceNumberIn > ushort.MaxValue - wrapAroundWindow)
                    effectivePacketId += ushort.MaxValue;
                else
                    return;
            }

            collectionDatas.Add(new PacketCollectionData(packetId, effectivePacketId, stream));
            packetTimeIn = time;
        }

        private void ReadAcknowledge(BinaryReader reader) {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                ushort acknowledgeId = reader.ReadUInt16();
                if (deliveryDatas.TryGetValue(acknowledgeId, out PacketDeliveryData deliveryData)) {
                    deliveryData.handler.HandleDeliverySuccess();
                    deliveryDatas.Remove(acknowledgeId);

                    float sampleRTT = time - deliveryData.time;
                    estimatedRTT = (1 - alpha) * estimatedRTT + alpha * sampleRTT;
                    devRTT = (1 - beta) * devRTT + beta * Math.Abs(sampleRTT - estimatedRTT);
                    timeoutDuration = estimatedRTT + 4 * devRTT;
                }
            }
        }

        private void ReadConnectionAccept(MemoryStream stream) {
            if (state == ConnectionState.Connecting)
                StartConnection();
        }

        public void Disconnect() {
            if (state != ConnectionState.Connected)
                return;
            state = ConnectionState.Default;
            SendDisconnect();
        }

        private void SendDisconnect() {
            MemoryStream stream = new MemoryStream();
            WriteServiceHeader(stream, ServiceType.Disconnect);
            Send(stream.ToArray());
        }

        private void SendAcknowledge() {
            MemoryStream stream = new MemoryStream();
            WriteServiceHeader(stream, ServiceType.Acknowledge);
            int count = (int)pendingAcknowledges.Count;
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(count);
            foreach (ushort id in pendingAcknowledges)
                writer.Write(id);
            pendingAcknowledges.Clear();
            Send(stream.ToArray());
        }

        private void SendConnectionRequest() {
            MemoryStream stream = new MemoryStream();
            WriteServiceHeader(stream, ServiceType.ConnectionRequest);
            Send(stream.ToArray());
        }

        private void SendConnectionAccept() {
            MemoryStream stream = new MemoryStream();
            WriteServiceHeader(stream, ServiceType.ConnectionAccept);
            Send(stream.ToArray());
        }

        private void WriteServiceHeader(MemoryStream stream, ServiceType serviceType) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((ushort)0);
            writer.Write((byte)serviceType);
        }

        private void SendHeartbeat() {
            MemoryStream stream = new MemoryStream();
            WriteServiceHeader(stream, ServiceType.Heartbeat);
            Send(stream.ToArray());
        }
    }
}