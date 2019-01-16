using Zoro.IO;
using System.IO;

namespace Zoro.Network.P2P.Payloads
{
    public class PongPayload : ISerializable
    {
        public uint Height;
        public uint Timestamp;

        public int Size => sizeof(uint) + sizeof(uint);

        public static PongPayload Create(uint height, uint timestamp)
        {
            return new PongPayload
            {
                Height = height,
                Timestamp = timestamp,
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Height = reader.ReadUInt32();
            Timestamp = reader.ReadUInt32();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(Height);
            writer.Write(Timestamp);
        }
    }
}
