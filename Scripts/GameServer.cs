using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Sylph.Networking;

namespace Sylph {
    public class GameServer {
        private EndPoint endPoint;
        private List<Player> players;
        private Player hostPlayer;
        private StreamConnection listenerConnection;

        public GameServer(EndPoint endPoint) {
            this.endPoint = endPoint;
            listenerConnection = new StreamConnection(endPoint);
            players = new List<Player>();
        }

        public List<Player> Players { get => players; }
        public EndPoint EndPoint { get => endPoint; }

        public void Start() {
            listenerConnection.Listen();
        }

        public void AddHostPlayer(IPEndPoint hostEndPoint) {
            var connection = new StreamConnection(endPoint);
            connection.RemoteEndPoint = hostEndPoint;
            connection.Start();
            
            hostPlayer = new Player(connection);
            hostPlayer.IsHostPlayer = true;
            players.Add(hostPlayer);
        }

        public void Update() {
            listenerConnection.Update();
            while (listenerConnection.HasData)
                listenerConnection.Receive();
            while (listenerConnection.HasPendingConnections()) {
                var connection = listenerConnection.AcceptConnection();
                var player = new Player(connection);
                players.Add(player);
                NetworkManager.Instance.PlayerConnected?.Invoke(player);
            }
            var remove = new List<Player>();
            foreach (Player player in players) {
                StreamConnection connection = player.Connection;
                connection.Update();
                if (connection.State == ConnectionState.Default) {
                    NetworkManager.Instance.PlayerDisconnected?.Invoke(player);
                    remove.Add(player);
                }
            }
            foreach (var player in remove) {
                players.Remove(player);
            }
        }

        public void ProcessInput() {
            foreach (Player player in players) {
                player.ReceiveInput();
            }
        }

        public void SendStateUpdate() {
            foreach (var gameObject in NetworkManager.Instance.NetworkGameObjects)
                gameObject.UpdateStateFlags();
            foreach (Player player in players) {
                player.SendStateUpdate();
            }
        }
    }
}
