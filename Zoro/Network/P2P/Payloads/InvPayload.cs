using Zoro.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zoro.Network.P2P.Payloads
{
    public class InvPayload : ISerializable
    {
        public InventoryType Type;
        public UInt256 Hash;

        public int Size => sizeof(InventoryType) + Hash.Size;

        public static InvPayload Create(InventoryType type, UInt256 hash)
        {
            return new InvPayload
            {
                Type = type,
                Hash = hash
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Type = (InventoryType)reader.ReadByte();
            if (!Enum.IsDefined(typeof(InventoryType), Type))
                throw new FormatException();
            Hash = reader.ReadSerializable<UInt256>();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Hash);
        }
    }
}
