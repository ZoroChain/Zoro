using Zoro.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zoro.Network.P2P.Payloads
{
    public class InvGroupPayload : ISerializable
    {
        public const int MaxHashesCount = 500;

        public InventoryType Type;
        public UInt256[] Hashes;

        public int Size => sizeof(InventoryType) + Hashes.GetVarSize();

        public static InvGroupPayload Create(InventoryType type, params UInt256[] hashes)
        {
            return new InvGroupPayload
            {
                Type = type,
                Hashes = hashes
            };
        }

        public static IEnumerable<InvGroupPayload> CreateGroup(InventoryType type, UInt256[] hashes)
        {
            for (int i = 0; i < hashes.Length; i += MaxHashesCount)
                yield return new InvGroupPayload
                {
                    Type = type,
                    Hashes = hashes.Skip(i).Take(MaxHashesCount).ToArray()
                };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Type = (InventoryType)reader.ReadByte();
            if (!Enum.IsDefined(typeof(InventoryType), Type))
                throw new FormatException();
            Hashes = reader.ReadSerializableArray<UInt256>(MaxHashesCount);
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Hashes);
        }
    }
}
