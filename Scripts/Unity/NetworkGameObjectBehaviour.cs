using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sylph.Unity {
    public class NetworkGameObjectBehaviour : MonoBehaviour {
        public static NetworkGameObjectBehaviour InstantiateNetworkPrefab(NetworkGameObjectBehaviour prefab) {
            var gameObject = Instantiate(prefab);
            gameObject.InitNetworkComponents();
            gameObject.Spawn();
            return gameObject;
        }

        public static NetworkGameObjectBehaviour InstantiateNetworkPrefab(NetworkGameObjectBehaviour prefab, Vector3 position, Quaternion rotation) {
            var gameObject = Instantiate(prefab, position, rotation);
            gameObject.InitNetworkComponents();
            gameObject.Spawn();
            return gameObject;
        }

        [SerializeField]
        private string typeName;
        private NetworkGameObject networkGameObject;

        public void InitNetworkComponents() {
            var networkComponentBehaviours = GetComponents<NetworkComponentBehaviour>();
            var networkComponents = new NetworkComponent[networkComponentBehaviours.Length];
            for (int i = 0; i < networkComponentBehaviours.Length; i++) {
                networkComponentBehaviours[i].NetworkComponent = new NetworkComponent(
                    networkComponentBehaviours[i].InitSyncVars()
                );
                networkComponents[i] = networkComponentBehaviours[i].NetworkComponent;
            }
            networkGameObject = new NetworkGameObject(typeName, networkComponents, gameObject);
        }

        public void Spawn() {
            networkGameObject.Spawn();
        }

        public string TypeName { get => typeName; set => typeName = value; }
        public NetworkGameObject NetworkGameObject { get => networkGameObject; set => networkGameObject = value; }

        private void OnDestroy() {
            if (NetworkManager.Instance.isServer && networkGameObject != null)
                networkGameObject.Kill();
        }
    }
}
