using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Persistence;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Zoro.Network.P2P.Payloads
{
    public class ContractTransaction : Transaction
    {
        private byte[] randomBytes = new byte[8];

        public UInt256 AssetId = new UInt256();
        public UInt160 From = new UInt160();
        public UInt160 To = new UInt160();
        public Fixed8 Value = Fixed8.Zero;
        public Fixed8 GasPrice = Fixed8.One;

        public readonly static Fixed8 Gas = Fixed8.One;

        public override int Size => base.Size + randomBytes.GetVarSize() + AssetId.Size + From.Size + To.Size + Value.Size + GasPrice.Size;

        public override Fixed8 SystemFee => GasPrice * Gas;

        public ContractTransaction()
            : base(TransactionType.ContractTransaction)
        {
            Version = 1;

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 1) throw new FormatException();

            if (Version > 0)
            {
                randomBytes = reader.ReadBytes(8);
                AssetId = reader.ReadSerializable<UInt256>();
                From = reader.ReadSerializable<UInt160>();
                To = reader.ReadSerializable<UInt160>();
                Value = reader.ReadSerializable<Fixed8>();
                GasPrice = reader.ReadSerializable<Fixed8>();
            }
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            if (Version > 0)
            {
                writer.Write(randomBytes);
                writer.Write(AssetId);
                writer.Write(From);
                writer.Write(To);
                writer.Write(Value);
                writer.Write(GasPrice);
            }
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            if (Version > 0)
            {
                json["randomBytes"] = randomBytes.ToString();
                json["asset"] = AssetId.ToString();
                json["value"] = Value.ToString();
                json["from"] = From.ToAddress();
                json["to"] = To.ToAddress();
                json["gas_price"] = GasPrice.ToString();
            }
            return json;
        }

        public override UInt160 GetAccountScriptHash(Snapshot snapshot)
        {
            return From;
        }

        public override UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            return base.GetScriptHashesForVerifying(snapshot).Union(new[] { From }).OrderBy(p => p).ToArray();
        }

        public override bool Verify(Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            if (Value <= Fixed8.Zero)
                return false;

            if (From.Equals(To))
                return false;

            if (From.Equals(UInt160.Zero) || To.Equals(UInt160.Zero))
                return false;

            AssetState asset = snapshot.Assets.TryGet(AssetId);
            if (asset == null)
                return false;

            return base.Verify(snapshot, mempool);
        }

        protected override bool CheckBalance(Snapshot snapshot)
        {
            Fixed8 amount = SystemFee + Value;

            if (amount <= Fixed8.Zero)
                return true;

            AccountState account = snapshot.Accounts.TryGet(GetAccountScriptHash(snapshot));

            if (account == null || !account.Balances.TryGetValue(Blockchain.UtilityToken.Hash, out Fixed8 balance))
                return false;

            if (balance < amount)
                return false;

            return true;
        }
    }
}
