using Zoro.IO;
using System;
using System.IO;

namespace Zoro.Network.P2P.Payloads
{
    public class PingPayload : ISerializable
    {
        public uint Timestamp;

        public int Size => sizeof(uint) + sizeof(uint);

        public static PingPayload Create()
        {
            return new PingPayload
            {
                Timestamp = DateTime.Now.ToTimestamp(),
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Timestamp = reader.ReadUInt32();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(Timestamp);
        }
    }
}
