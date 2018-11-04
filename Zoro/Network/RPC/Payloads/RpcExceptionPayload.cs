using Zoro.IO;
using System;
using System.IO;

namespace Zoro.Network.RPC
{
    public class RpcExceptionPayload : ISerializable
    {
        public Guid Guid;
        public int HResult;
        public string Message;
#if DEBUG
        public string StackTrace;

        public int Size => 16 + sizeof(int) + Message.GetVarSize() + StackTrace.GetVarSize();
#else
        public int Size => 16 + sizeof(int) + Message.GetVarSize();
#endif

        public static RpcExceptionPayload Create(Guid guid, Exception e)
        {
            return new RpcExceptionPayload
            {
                Guid = guid,
                HResult = e.HResult,
                Message = e.Message,
#if DEBUG
                StackTrace = e.StackTrace,
#endif
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Guid = new Guid(reader.ReadVarBytes());
            HResult = reader.ReadInt32();
            Message = reader.ReadVarString();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(Guid.ToByteArray());
            writer.Write(HResult);
            writer.WriteVarString(Message);
#if DEBUG
            writer.WriteVarString(StackTrace);
#endif
        }
    }
}
