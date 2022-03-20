using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public class NetworkComponent {
        private NetworkGameObject gameObject;
        private int index;
        private int stateFlags;
        private SyncVarBase[] syncVars;

        public NetworkComponent() {
            stateFlags = 0;
        }

        public NetworkComponent(SyncVarBase[] syncVars) {
            this.syncVars = syncVars;
        }

        public void InitSyncVars(SyncVarBase[] syncVars) {
            this.syncVars = syncVars;
        }

        public int NetworkId { get => gameObject.NetworkId; }
        public NetworkGameObject NetworkGameObject { get => gameObject; set => gameObject = value; }
        public int Index { get => index; set => index = value; }
        public int StateFlags { get => stateFlags; }

        public void UpdateStateFlags() {
            stateFlags = 0;
            for (int i = 0 ; i < syncVars.Length; i++) {
                if (syncVars[i].IsDirty) {
                    stateFlags = SylphUtils.SetBit(stateFlags, i);
                    syncVars[i].IsDirty = false;
                }
            }
        }

        public void Write(MemoryStream stream, int playerStateFlags) {
            for (int i = 0; i < syncVars.Length; i++) {
                if (SylphUtils.GetBit(playerStateFlags, i)) 
                    syncVars[i].Write(stream);
            }
        }

        public void Read(MemoryStream stream, int stateFlags) {
            for (int i = 0; i < syncVars.Length; i++) {
                if (SylphUtils.GetBit(stateFlags, i))
                    syncVars[i].Read(stream);
            }
        }
    }
}
