using Zoro.IO;
using Zoro.IO.Json;
using Neo.VM;
using System;
using System.IO;

namespace Zoro.Ledger
{
    public abstract class StateBase : ISerializable
    {
        public byte StateVersion = 0;

        public virtual int Size => sizeof(byte);

        public virtual void Deserialize(BinaryReader reader)
        {
            if (reader.ReadByte() != StateVersion) throw new FormatException();
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(StateVersion);
        }

        public virtual JObject ToJson()
        {
            JObject json = new JObject();
            json["version"] = StateVersion;
            return json;
        }
    }
}
