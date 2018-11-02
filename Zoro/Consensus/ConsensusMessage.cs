using Zoro.IO;
using Zoro.IO.Caching;
using System;
using System.IO;

namespace Zoro.Consensus
{
    internal abstract class ConsensusMessage : ISerializable
    {
        /// <summary>
        /// Reflection cache for ConsensusMessageType
        /// </summary>
        private static ReflectionCache<byte> ReflectionCache = ReflectionCache<byte>.CreateFromEnum<ConsensusMessageType>();

        public readonly ConsensusMessageType Type;
        public ushort ViewNumber;

        public int Size => sizeof(ConsensusMessageType) + sizeof(byte);

        protected ConsensusMessage(ConsensusMessageType type)
        {
            this.Type = type;
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            if (Type != (ConsensusMessageType)reader.ReadByte())
                throw new FormatException();
            ViewNumber = reader.ReadUInt16();
        }

        public static ConsensusMessage DeserializeFrom(byte[] data)
        {
            ConsensusMessage message = ReflectionCache.CreateInstance<ConsensusMessage>(data[0]);
            if (message == null) throw new FormatException();

            using (MemoryStream ms = new MemoryStream(data, false))
            using (BinaryReader r = new BinaryReader(ms))
            {
                message.Deserialize(r);
            }
            return message;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(ViewNumber);
        }
    }
}
