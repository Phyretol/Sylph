using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public enum CommandType {
        Create,
        Update,
        Destroy
    }

    public class ReplicationCommand {
        public int[] stateFlags;
        public int networkId;
        public NetworkGameObject gameObject;
        public CommandType type;
        public bool hasDirtyState;

        public ReplicationCommand(CommandType type, NetworkGameObject gameObject) {
            this.type = type;
            this.gameObject = gameObject;
            networkId = gameObject.NetworkId;
            stateFlags = new int[gameObject.Components.Length];
            for (int i = 0; i < stateFlags.Length; i++)
                stateFlags[i] = -1;
        }

        public ReplicationCommand(int networkId) {
            this.networkId = networkId;
            type = CommandType.Destroy;
            hasDirtyState = true;
        }

        public void AddStateFlags(int[] otherStateFlags) {
            for (int i = 0; i < gameObject.Components.Length; i++)
                stateFlags[i] |= otherStateFlags[i];
        }

        public void UpdateStateFlags() {
            if (type == CommandType.Create || type == CommandType.Update) {
                hasDirtyState = false;
                for (int i = 0; i < gameObject.Components.Length; i++) {
                    stateFlags[i] |= gameObject.Components[i].StateFlags;
                    hasDirtyState |= stateFlags[i] != 0;
                }
                if (type == CommandType.Create)
                    hasDirtyState = true;
            }
        }

        public void ClearStateFlags() {
            for (int i = 0; i < gameObject.Components.Length; i++)
                stateFlags[i] = 0;
            hasDirtyState = false;
        }

        public void ResetStateFlags() {
            for (int i = 0; i < gameObject.Components.Length; i++)
                stateFlags[i] = -1;
            hasDirtyState = true;
        }
    }
}
