using Zoro.IO;
using Zoro.IO.Json;
using System;
using System.IO;

namespace Zoro.Network.RPC
{
    public class RpcResponsePayload : ISerializable
    {
        public Guid Guid;
        public JObject Result;

        public int Size => 16 + Result.ToString().Length;

        public static RpcResponsePayload Create(Guid guid, JObject result)
        {
            return new RpcResponsePayload
            {
                Guid = guid,
                Result = result
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Guid = new Guid(reader.ReadVarBytes());
            Result = JObject.Parse(reader.ReadVarString());
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Guid.ToByteArray());
            writer.WriteVarString(Result.ToString());
        }
    }
}
