using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sylph.Unity {
    public class UnityNetworkManager : NetworkManager {
        private Dictionary<string, NetworkGameObjectBehaviour> networkPrefabsMap;

        public UnityNetworkManager(List<NetworkGameObjectBehaviour> networkPrefabs) {
            networkPrefabsMap = new Dictionary<string, NetworkGameObjectBehaviour>();
            foreach (var prefab in networkPrefabs) {
                string typeName = prefab.TypeName;
                if (typeName.Equals("")) {
                    string objectName = prefab.gameObject.name;
                    Debug.LogWarning($"Prefab type not set on {objectName} game object");
                    typeName = prefab.TypeName = objectName;
                }
                if (networkPrefabsMap.ContainsKey(typeName)) {
                    Debug.LogError($"Duplicate prefab type {typeName}");
                    continue;
                }
                networkPrefabsMap[typeName] = prefab;
            }
        }

        public void SpawnAllNetworkGameObjects() {
            var networkGameObjects = GameObject.FindObjectsOfType<NetworkGameObjectBehaviour>();
            foreach (var networkObject in networkGameObjects) {
                if (networkObject.NetworkGameObject == null) {
                    if (isClientOnly)
                        GameObject.Destroy(networkObject.gameObject);
                    else {
                        networkObject.InitNetworkComponents();
                        networkObject.Spawn();
                    }
                }
            }
        }

        public override NetworkGameObject CreateGameObject(string typeName) {
            if (!networkPrefabsMap.TryGetValue(typeName, out NetworkGameObjectBehaviour prefab)) {
                Debug.LogError($"Prefab type {typeName} not found");
                return null;
            }

            var networkGameObjectBehaviour = GameObject.Instantiate(prefab);
            networkGameObjectBehaviour.InitNetworkComponents();

            return networkGameObjectBehaviour.NetworkGameObject;
        }

        public override void DestroyGameObject(NetworkGameObject gameObject) {
            GameObject.Destroy((GameObject)gameObject.GameObject);
        }
    }
}
