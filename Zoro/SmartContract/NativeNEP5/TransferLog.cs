using Zoro.IO;
using System.IO;

namespace Zoro.SmartContract.NativeNEP5
{
    internal class TransferLog : ISerializable
    {
        public UInt160 From;
        public UInt160 To;
        public Fixed8 Value;

        public virtual int Size => From.Size + To.Size + Value.Size;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(From);
            writer.Write(To);
            writer.Write(Value);
        }

        public void Deserialize(BinaryReader reader)
        {
            From = reader.ReadSerializable<UInt160>();
            To = reader.ReadSerializable<UInt160>();
            Value = reader.ReadSerializable<Fixed8>();
        }
    }
}
