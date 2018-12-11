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
        private byte[] randomBytes;

        public UInt256 AssetId;
        public UInt160 From;
        public UInt160 To;
        public Fixed8 Value;

        public override int Size => base.Size + randomBytes.GetVarSize() + AssetId.Size + From.Size + To.Size + Value.Size;

        public ContractTransaction()
            : base(TransactionType.ContractTransaction)
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version != 0) throw new FormatException();

            randomBytes = reader.ReadBytes(8);
            AssetId = reader.ReadSerializable<UInt256>();
            From = reader.ReadSerializable<UInt160>();
            To = reader.ReadSerializable<UInt160>();
            Value = reader.ReadSerializable<Fixed8>();
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            writer.Write(randomBytes);
            writer.Write(AssetId);
            writer.Write(From);
            writer.Write(To);
            writer.Write(Value);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["randomBytes"] = randomBytes.ToString();
            json["asset"] = AssetId.ToString();
            json["value"] = Value.ToString();
            json["from"] = From.ToAddress();
            json["to"] = To.ToAddress();
            return json;
        }

        public override UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            HashSet<UInt160> hashes = new HashSet<UInt160>(Attributes.Where(p => p.Usage == TransactionAttributeUsage.Script).Select(p => new UInt160(p.Data)));
            hashes.Add(From);
            return hashes.OrderBy(p => p).ToArray();
        }

        public override bool Verify(Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            if (Value.Equals(Fixed8.Zero))
                return false;

            if (From.Equals(To))
                return false;

            if (From.Equals(UInt160.Zero) || To.Equals(UInt160.Zero))
                return false;

            AssetState asset = snapshot.Assets.TryGet(AssetId);
            if (asset == null) return false;
            if (asset.Expiration <= snapshot.Height + 1 && asset.AssetType != AssetType.GoverningToken && asset.AssetType != AssetType.UtilityToken)
                return false;

            return base.Verify(snapshot, mempool);
        }
    }
}
