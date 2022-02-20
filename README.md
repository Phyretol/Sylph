# Sylph Game Server

Sylph is a networking framework for real time multiplayer games based on the UDP protocol written in C# for Unity. It was developed following the instructions from the book "Multiplayer Game Programming: Architecting Networked Games" with a few improvements.

## FAQ

*Does Sylph (only) work with Unity?*
Sylph was designed to be used with Unity projects. However, the framework is modular and can be adapted to work with other engines and frameworks.

*Are there any games made with Sylph?*
I'm currently using Sylph to develop a MOBA (similar to League of Legends) game prototype. [YouTube video](https://www.youtube.com/watch?v=5z_l2fDyxSk)

## Main Classes and Code Samples

### NetworkGameObjectBehaviour

All networked game objects must be defined through prefabs having a NetworkGameObjectBehaviour script attached. Each prefab needs a unique Type Name. These prefabs must be instantiated using the InstantiateNetworkPrefab static method, which initializes the components and spawns the game object across the network.

### NetworkComponentBehaviour

Networked game objects can use scripts that extend the NetworkComponentBehaviour class to synchronize their values over the network. In order to extend the NetworkComponentBehaviour class, the InitSyncVars method must be implemented returning all the SyncVar values to be synchronized across the network.

```
public class PlayerCharacter : NetworkComponentBehaviour {
    private StringSyncVar playerName;
    private IntSyncVar score;

    public override SyncVarBase[] InitSyncVars {
        playerName = new StringSyncVar("");
        score = new IntSyncVar(0);

        return new SyncVarBase[] {
            playerName,
            score
        };
    }

    public void Init(string playerName, int score) {
        this.playerName.Value = playerName;
        this.score.Value = score;
    }
}
```

### NetworkManager

Singleton class used to setup the network configuration. UnityNetworkManager extends NetworkManager and must be inizialized with the network prefabs to be used in your game.

```
public class MyGameManager : MonoBehaviour {
    private static MyGameManager instance = null;

    [SerializeField]
    private List<NetworkGameObjectBehaviour> networkPrefabs;
    [SerializeField]
    private NetworkGameObject playerPrefab;
    [SerializeField]
    private Transform playerSpawnTransform;

    private void Awake() {
        NetworkManager.SetInstance(new UnityNetworkManager(networkPrefabs));
        NetworkManager.Instance.Init();
        InitRpc();

        instance = this;
    }
    
    private void InitRpc() {
    	RpcTable.Register(new SetPlayerNameRpc());
    }

    public static MyGameManager Instance { get => instance; }

    public void SpawnPlayerCharacter(string playerName) {
        var playerCharacter = NetworkGameObjectBehaviour.InstantiateNetworkPrefab(playerPrefab);
        playerCharacter.transform.position = playerSpawnTransform.position;
        playerCharacter.transform.rotation = playerSpawnTransform.rotation;
        playerCharacter.GetComponent<PlayerCharacter>().Init(playerName, 0);
    }
}
```

### RpcObject

A Remote Procedure Call (RPC) is a method that can be executed across the network. It can be called from the client to be executed on the server or it can be called on the server to be executed on the clients.
RPCs can be defined extending the RpcObject class and can be called from the NetworkManager.

```
public class SetPlayerNameRpc : RpcObject {
    private StringSyncVar playerName;
    
    public SetPlayerNameRpc() : base(
        name: "SetPlayerName",
        isReliable: true,
        isClientCallable: true
    ) {
        Args.Add(playerName);
    }
    
    public string PlayerName { get => playerName.Value; set => playerName.Value = Value; }
    
    public override void Execute(Player caller) {
        MyGameManager.Instance.SpawnPlayerCharacter(PlayerName);
    }
}
```