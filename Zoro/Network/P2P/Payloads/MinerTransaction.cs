using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Wallets;
using System;
using System.IO;
using System.Linq;

namespace Zoro.Network.P2P.Payloads
{
    public class MinerTransaction : Transaction
    {
        public uint Nonce;

        public UInt160 Address;

        public override Fixed8 NetworkFee => Fixed8.Zero;

        public override int Size => base.Size + sizeof(uint) + Address.Size;

        public MinerTransaction()
            : base(TransactionType.MinerTransaction)
        {
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 1) throw new FormatException();
            Nonce = reader.ReadUInt32();

            if (Version >= 1)
                Address = reader.ReadSerializable<UInt160>();
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            writer.Write(Nonce);
            if (Version >= 1)
                writer.Write(Address);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["nonce"] = Nonce;
            json["address"] = Address.ToAddress();
            return json;
        }
    }
}
