using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {
    public abstract class RpcObject {
        private string name;
        private bool isClientCallable;
        private bool isReliable;
        private int callCount;
        private List<SyncVarBase> args;

        public RpcObject(string name, bool isClientCallable, bool isReliable) {
            this.name = name;
            this.isClientCallable = isClientCallable;
            this.isReliable = isReliable;
            args = new List<SyncVarBase>();
            callCount = -1;
        }

        public string Name { get => name; }
        public List<SyncVarBase> Args { get => args; }
        public bool IsClientCallable { get => isClientCallable; }
        public bool IsReliable { get => isReliable; }
        public int CallCount { get => callCount; set => callCount = value; }

        public void Read(MemoryStream stream) {
            foreach (var arg in args) 
                arg.Read(stream);
        }
        public void Write(MemoryStream stream) {
            foreach (var arg in args)
                arg.Write(stream);
        }
        public abstract void Execute(Player caller = null);
    }
}
