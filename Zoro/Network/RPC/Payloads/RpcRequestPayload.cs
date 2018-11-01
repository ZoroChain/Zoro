using Zoro.IO;
using System;
using System.IO;

namespace Zoro.Network.RPC
{
    public class RpcRequestPayload : ISerializable
    {
        public Guid Guid;
        public string Command;
        public UInt160 ChainHash;
        public byte[] Data;

        public int Size => 16 + Command.GetVarSize() + ChainHash.Size + Data.GetVarSize();

        public static RpcRequestPayload Create(string command, UInt160 chainHash, byte[] data)
        {
            return new RpcRequestPayload
            {
                Guid = Guid.NewGuid(),
                Command = command,
                ChainHash = chainHash,
                Data = data
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Guid = new Guid(reader.ReadVarBytes());
            Command = reader.ReadVarString();
            ChainHash = reader.ReadSerializable<UInt160>();
            Data = reader.ReadVarBytes();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Guid.ToByteArray());
            writer.WriteVarString(Command);
            writer.Write(ChainHash);
            writer.WriteVarBytes(Data);
        }
    }
}
