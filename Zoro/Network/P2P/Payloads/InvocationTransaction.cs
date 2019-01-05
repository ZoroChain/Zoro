using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Persistence;
using System;
using System.IO;
using System.Linq;

namespace Zoro.Network.P2P.Payloads
{
    public class InvocationTransaction : Transaction
    {
        public byte[] Script;
        public Fixed8 GasPrice = Fixed8.One;
        public Fixed8 GasLimit = Fixed8.Zero;

        public override int Size => base.Size + Script.GetVarSize() + GasPrice.Size + GasLimit.Size;

        public override Fixed8 SystemFee => GasPrice * GasLimit;

        public InvocationTransaction()
            : base(TransactionType.InvocationTransaction)
        {
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 0) throw new FormatException();
            Script = reader.ReadVarBytes(65536);
            if (Script.Length == 0) throw new FormatException();
            GasLimit = reader.ReadSerializable<Fixed8>();
            if (GasLimit < Fixed8.Zero) throw new FormatException();
            GasPrice = reader.ReadSerializable<Fixed8>();
            if (GasPrice <= Fixed8.Zero) throw new FormatException();
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
            writer.Write(GasLimit);
            writer.Write(GasPrice);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["script"] = Script.ToHexString();
            json["gas_limit"] = GasLimit.ToString();
            json["gas_price"] = GasPrice.ToString();
            
            return json;
        }

        public override UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            return base.GetScriptHashesForVerifying(snapshot).Union(new[] { Account }).OrderBy(p => p).ToArray();
        }

        public override bool Verify(Snapshot snapshot)
        {
            if (Account.Equals(UInt160.Zero)) return false;
            if (GasLimit.GetData() % 100000000 != 0) return false;
            if (GasPrice <= Fixed8.Zero) return false;
            try
            {
                if (SystemFee <= Fixed8.Zero) return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            return base.Verify(snapshot);
        }
    }
}
