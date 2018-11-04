using Zoro.IO;
using System;
using System.IO;

namespace Zoro.Network.RPC
{
    public class RpcRequestPayload : ISerializable
    {
        public Guid Guid;
        public string Method;
        public string Params;

        public int Size => 16 + Method.GetVarSize() + Params.GetVarSize();

        public static RpcRequestPayload Create(string method, string parameters)
        {
            return new RpcRequestPayload
            {
                Guid = Guid.NewGuid(),
                Method = method,
                Params = parameters,
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Guid = new Guid(reader.ReadVarBytes());
            Method = reader.ReadVarString();
            Params = reader.ReadVarString();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Guid.ToByteArray());
            writer.WriteVarString(Method);
            writer.WriteVarString(Params);
        }
    }
}
