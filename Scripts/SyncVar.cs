using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sylph {

    public abstract class SyncVarBase {
        private bool isDirty;

        public bool IsDirty { get => isDirty; set => isDirty = value; }

        public abstract void Write(MemoryStream stream);
        public abstract void Read(MemoryStream stream);
    }

    public abstract class SyncVar<T> : SyncVarBase {
        private T _value;
        public Action<T, T> ValueChanged;

        public SyncVar(T value) {
            _value = value;
        }

        public T Value {
            get => _value;
            set {
                ValueChanged?.Invoke(_value, value);
                if (!value.Equals(_value)) {
                    _value = value;
                    IsDirty = true;
                }
            }
        }
    }

    public class IntSyncVar : SyncVar<int> {
        public IntSyncVar(int value) : base(value) { }

        public override void Read(MemoryStream stream) {
            BinaryReader reader = new BinaryReader(stream);
            Value = reader.ReadInt32();
        }

        public override void Write(MemoryStream stream) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Value);
        }
    }

    public class FloatSyncVar : SyncVar<float> {
        public FloatSyncVar(float value) : base(value) { }

        public override void Read(MemoryStream stream) {
            BinaryReader reader = new BinaryReader(stream);
            Value = reader.ReadSingle();
        }

        public override void Write(MemoryStream stream) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Value);
        }
    }

    public class BoolSyncVar : SyncVar<bool> {
        public BoolSyncVar(bool value) : base(value) { }

        public override void Read(MemoryStream stream) {
            BinaryReader reader = new BinaryReader(stream);
            Value = reader.ReadBoolean();
        }

        public override void Write(MemoryStream stream) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Value);
        }
    }

    public class StringSyncVar : SyncVar<string> {
        public StringSyncVar(string value) : base(value) { }

        public override void Read(MemoryStream stream) {
            BinaryReader reader = new BinaryReader(stream);
            Value = reader.ReadString();
        }

        public override void Write(MemoryStream stream) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Value);
        }
    }
}
