using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Sylph.Networking {
    public class ConnectionGroup {
        public Socket socket;
        public Dictionary<EndPoint, StreamConnection> connections;
        public StreamConnection listenerConnection;

        public ConnectionGroup(EndPoint localEndPoint) {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);
            connections = new Dictionary<EndPoint, StreamConnection>();
        }

        public Socket Socket { get => socket; }

        public StreamConnection Connection(EndPoint remoteEndPoint) {
            StreamConnection connection = null;
            connections.TryGetValue(remoteEndPoint, out connection);
            return connection;
        }

        public void AddConnection(StreamConnection connection) {
            connections[connection.RemoteEndPoint] = connection;
        }

        public void SetListenerConnection(StreamConnection connection) {
            listenerConnection = connection;
        }

        public void HandleConnectionPacket(EndPoint endPoint, byte[] buffer) {
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer));

            int id = reader.ReadUInt16();
            var serviceType = (ServiceType)reader.ReadByte();
            if (!(id == 0 && serviceType == ServiceType.ConnectionRequest))
                return;

            var connection = new StreamConnection(socket.LocalEndPoint);
            connection.RemoteEndPoint = endPoint;
            connections[endPoint] = connection;
            listenerConnection.AddPendingConnection(connection);
        }
    }

    public class ConnectionManager {
        private static ConnectionManager instance = null;
        public static ConnectionManager Instance {
            get {
                if (instance == null)
                    instance = new ConnectionManager();
                return instance;
            }
        }

        private const int packetSize = 1500;

        private Dictionary<EndPoint, ConnectionGroup> connections;

        private ConnectionManager() {
            connections = new Dictionary<EndPoint, ConnectionGroup>();
        }

        private ConnectionGroup GetConnectionGroup(StreamConnection connection) {
            ConnectionGroup connectionGroup;
            EndPoint localEndPoint = connection.LocalEndPoint;
            if (!connections.TryGetValue(localEndPoint, out connectionGroup)) {
                connectionGroup = new ConnectionGroup(localEndPoint);
                connections[localEndPoint] = connectionGroup;
            }
            return connectionGroup;
        }

        public void AddConnection(StreamConnection connection) {
            ConnectionGroup connectionGroup = GetConnectionGroup(connection);
            connectionGroup.AddConnection(connection);
            connection.ConnectionGroup = connectionGroup;
            connection.Socket = connectionGroup.Socket;
        }

        public void AddListenerConnection(StreamConnection connection) {
            ConnectionGroup connectionGroup = GetConnectionGroup(connection);
            connectionGroup.SetListenerConnection(connection);
            connection.ConnectionGroup = connectionGroup;
            connection.Socket = connectionGroup.Socket;
        }

        public static IPEndPoint endPointDefault = new IPEndPoint(IPAddress.Loopback, IPEndPoint.MaxPort);

        public void Receive(StreamConnection connection) {
            ConnectionGroup connectionGroup = connection.ConnectionGroup;
            Socket socket = connection.Socket;
            byte[] packetData;
            EndPoint endPoint;
            StreamConnection receivingConnection;

            while (socket.Available > 0) {
                endPoint = endPointDefault;
                packetData = new byte[packetSize];
                try {
                    socket.ReceiveFrom(packetData, ref endPoint);
                } catch (SocketException e) {
                    if (!endPoint.Equals(endPointDefault)) {
                        receivingConnection = connectionGroup.Connection(endPoint);
                        if (receivingConnection != null)
                            receivingConnection.Disconnect();
                    }
                    continue;
                }

                receivingConnection = connectionGroup.Connection(endPoint);
                if (receivingConnection != null)
                    receivingConnection.AddToBuffer(packetData);
                else
                    connectionGroup.HandleConnectionPacket(endPoint, packetData);
            }
        }
    }
}
