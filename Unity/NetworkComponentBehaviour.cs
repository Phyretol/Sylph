using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Sylph.Unity {
    [RequireComponent(typeof(NetworkGameObjectBehaviour))]
    public abstract class NetworkComponentBehaviour : MonoBehaviour {
        private NetworkComponent networkComponent;

        public virtual SyncVarBase[] InitSyncVars() {
            return new SyncVarBase[0];
        }

        public NetworkComponent NetworkComponent { get => networkComponent; set => networkComponent = value; }
    }
}
