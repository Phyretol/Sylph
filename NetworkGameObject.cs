using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Sylph {
    public class NetworkGameObject {
        private int networkId;
        private string typeName;
        private int replicationCount;
        private NetworkComponent[] components;
        private object gameObject;

        public NetworkGameObject(string typeName, NetworkComponent[] components, object gameObject = null) {
            this.typeName = typeName;
            this.components = components;
            this.gameObject = gameObject;
            foreach (var component in components)
                component.NetworkGameObject = this;
        }

        public int NetworkId { get => networkId; set => networkId = value; }
        public string TypeName { get => typeName; }
        public NetworkComponent[] Components { get => components; }
        public int ReplicationCount { get => replicationCount; set => replicationCount = value; }
        public object GameObject { get => gameObject; }

        public void Spawn() {
            if (NetworkManager.Instance.isServer)
                NetworkManager.Instance.AddGameObjectSendCreate(this);
            else
                NetworkManager.Instance.AddGameObject(this, networkId);
        }

        public void UpdateStateFlags() {
            foreach (var component in components)
                component.UpdateStateFlags();
        }

        public void Kill() {
            if (NetworkManager.Instance.isServer)
                NetworkManager.Instance.RemoveGameObjectSendDestroy(this);
            else
                NetworkManager.Instance.RemoveGameObject(this);
        }
    }
}
