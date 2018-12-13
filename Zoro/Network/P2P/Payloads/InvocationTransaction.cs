using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Ledger;
using Zoro.Persistence;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Zoro.Network.P2P.Payloads
{
    public class InvocationTransaction : Transaction
    {
        public byte[] Script;
        public Fixed8 GasPrice = Fixed8.One;
        public Fixed8 GasLimit = Fixed8.Zero;
        public UInt160 ScriptHash = new UInt160();

        public override int Size => base.Size + Script.GetVarSize() + GasPrice.Size + GasLimit.Size + ScriptHash.Size;

        public override Fixed8 SystemFee => GasPrice * GasLimit;

        public InvocationTransaction()
            : base(TransactionType.InvocationTransaction)
        {
            Version = 2;
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 2) throw new FormatException();
            Script = reader.ReadVarBytes(65536);
            if (Script.Length == 0) throw new FormatException();
            if (Version >= 1)
            {
                GasLimit = reader.ReadSerializable<Fixed8>();
                if (GasLimit < Fixed8.Zero) throw new FormatException();
            }
            if (Version >= 2)
            {
                GasPrice = reader.ReadSerializable<Fixed8>();
                if (GasPrice <= Fixed8.Zero) throw new FormatException();
                ScriptHash = reader.ReadSerializable<UInt160>();
            }
        }

        public static Fixed8 GetGasLimit(Fixed8 consumed)
        {
            Fixed8 gas = consumed;
            if (gas <= Fixed8.Zero) return Fixed8.Zero;
            return gas.Ceiling();
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            writer.WriteVarBytes(Script);
            if (Version >= 1)
                writer.Write(GasLimit);
            if (Version >= 2)
            {
                writer.Write(GasPrice);
                writer.Write(ScriptHash);
            }
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["script"] = Script.ToHexString();
            json["gas_limit"] = GasLimit.ToString();
            json["gas_price"] = GasPrice.ToString();
            json["script_hash"] = ScriptHash.ToString();
            return json;
        }

        public override UInt160 GetAccountScriptHash(Snapshot snapshot)
        {
            return ScriptHash;
        }

        public override UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            return base.GetScriptHashesForVerifying(snapshot).Union(new[] { ScriptHash }).OrderBy(p => p).ToArray();
        }

        public override bool Verify(Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            if (ScriptHash.Equals(UInt160.Zero)) return false;
            if (GasLimit.GetData() % 100000000 != 0) return false;
            if (GasPrice <= Fixed8.Zero) return false;
            return base.Verify(snapshot, mempool);
        }
    }
}
