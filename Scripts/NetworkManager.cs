using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using Sylph.Networking;

namespace Sylph {
    public enum NetworkMode {
        Default,
        Client,
        Server,
        Host
    }

    public abstract class NetworkManager {
        [ThreadStatic]
        private static NetworkManager instance;

        public static NetworkManager Instance {
            get {
                return instance;
            }
        }

        public static void SetInstance(NetworkManager _instance) {
            instance = _instance;
        }

        private Dictionary<int, NetworkGameObject> networkGameObjects;

        private int packetSize = 1450;
        private float updateInterval = 1f / 30f;
        private float updateTime = 0f;
        private Stopwatch stopwatch;
        private int networkId;

        private NetworkMode networkMode;
        private GameServer gameServer;
        private GameClient gameClient;

        public Action<Player> PlayerConnected;
        public Action<Player> PlayerDisconnected;
        public Action ClientConnected;
        public Action ClientDisconnected;

        public virtual void Init() {
            networkGameObjects = new Dictionary<int, NetworkGameObject>();
            stopwatch = new Stopwatch();
            stopwatch.Start();
            networkId = 1;
        }

        public List<Player> Players { get => gameServer.Players; }
        public int PacketSize { get => packetSize; }
        public NetworkMode NetworkMode { get => networkMode;}
        public bool isServer { get => networkMode == NetworkMode.Server 
                || networkMode == NetworkMode.Host; }
        public bool isClient { get => networkMode == NetworkMode.Client 
                || networkMode == NetworkMode.Host; }
        public bool isClientOnly { get => networkMode == NetworkMode.Client; }
        public ICollection<NetworkGameObject> NetworkGameObjects { get => networkGameObjects.Values; }

        public void StartClient(IPEndPoint localEndPoint, IPEndPoint serverEndPoint) {
            gameClient = new GameClient(localEndPoint, serverEndPoint);
            gameClient.Start();
            networkMode = NetworkMode.Client;
        }

        public void StartServer(IPEndPoint localEndPoint) {
            gameServer = new GameServer(localEndPoint);
            gameServer.Start();
            networkMode = NetworkMode.Server;
        }

        public void StartHost(IPEndPoint serverLocalEndPoint, IPEndPoint hostLocalEndPoint) {
            gameServer = new GameServer(serverLocalEndPoint);
            gameServer.AddHostPlayer(hostLocalEndPoint);
            gameServer.Start();
            
           
            gameClient = new GameClient(hostLocalEndPoint, serverLocalEndPoint);
            gameClient.Start();
            networkMode = NetworkMode.Host;
        }

        public void PreUpdate() {
            gameClient?.Update();
            gameServer?.ProcessInput();
            gameServer?.Update();
        }

        public void PostUpdate() {
            float time = (float)stopwatch.Elapsed.TotalSeconds;
            if (time - updateTime >= updateInterval) {
                gameServer?.SendStateUpdate();
                updateTime = time;
            }
        }

        public void StopClient() {
            gameClient = null;
            networkMode = NetworkMode.Default;
        }

        public void StopServer() {
            gameServer = null;
            networkMode = NetworkMode.Default;
        }

        public void StopHost() {
            StopClient();
            StopServer();
        }

        public abstract NetworkGameObject CreateGameObject(string typeName);

        public abstract void DestroyGameObject(NetworkGameObject gameObject);

        public bool TryGetGameObject(int networkId, out NetworkGameObject gameObject) {
            return networkGameObjects.TryGetValue(networkId, out gameObject);
        }

        public void AddGameObjectSendCreate(NetworkGameObject gameObject) {
            gameObject.NetworkId = networkId++;
            networkGameObjects[gameObject.NetworkId] = gameObject;
            foreach (var player in Players)
                player.Show(gameObject);
        }

        public void AddGameObject(NetworkGameObject gameObject, int networkId) {
            gameObject.NetworkId = networkId;
            networkGameObjects[networkId] = gameObject;
        }

        public void RemoveGameObjectSendDestroy(NetworkGameObject gameObject) {
            networkGameObjects.Remove(gameObject.NetworkId);
            foreach (var player in Players)
                player.Hide(gameObject);
        }

        public void RemoveGameObject(NetworkGameObject gameObject) {
            networkGameObjects.Remove(gameObject.NetworkId);
        }

        public void CallOnAllPlayers(RpcObject rpc) {
            foreach (var player in Players)
                player.Call(rpc);
        }

        public void CallOnServer(RpcObject rpc) {
            gameClient.Send(rpc);
        }
    }
}
