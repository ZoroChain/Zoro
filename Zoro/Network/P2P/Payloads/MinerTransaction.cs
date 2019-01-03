using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Wallets;
using Zoro.Persistence;
using Zoro.SmartContract;
using System;
using System.IO;
using Neo.VM;

namespace Zoro.Network.P2P.Payloads
{
    public class MinerTransaction : Transaction
    {
        public uint Nonce;

        public UInt160 Address = (new[] { (byte)OpCode.PUSHF }).ToScriptHash();

        public override int Size => base.Size + sizeof(uint) + Address.Size;

        public MinerTransaction()
            : base(TransactionType.MinerTransaction)
        {
            Version = 1;
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

        public override UInt160 GetAccountScriptHash(Snapshot snapshot)
        {
            return Address;
        }
    }
}
