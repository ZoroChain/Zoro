using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zoro.Network.P2P.Payloads
{
    public class IssueTransaction : Transaction
    {
        public UInt256 AssetId;
        public UInt160 Address;
        public Fixed8 Value;

        public override int Size => base.Size + AssetId.Size + Address.Size + Value.Size;

        public override Fixed8 SystemFee
        {
            get
            {
                if (Version > 1) return Fixed8.Zero;
                if (AssetId == Blockchain.UtilityToken.Hash)
                    return Fixed8.Zero;
                return base.SystemFee;
            }
        }

        public IssueTransaction()
            : base(TransactionType.IssueTransaction)
        {
            Version = 1;
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 1) throw new FormatException();

            if (Version > 0)
            {
                AssetId = reader.ReadSerializable<UInt256>();
                Value = reader.ReadSerializable<Fixed8>();
                Address = reader.ReadSerializable<UInt160>();
            }
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            if (Version > 0)
            {
                writer.Write(AssetId);
                writer.Write(Value);
                writer.Write(Address);
            }
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            if (Version > 0)
            {
                json["asset"] = AssetId.ToString();
                json["value"] = Value.ToString();
                json["address"] = Address.ToAddress();
            }
            return json;
        }

        public override UInt160 GetAccountScriptHash(Snapshot snapshot)
        {
            if (AssetId != null)
            {
                AssetState asset = snapshot.Assets.TryGet(AssetId);
                if (asset == null) throw new InvalidOperationException();
                return asset.Issuer;
            }

            return UInt160.Zero;
        }

        public override UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            HashSet<UInt160> hashes = new HashSet<UInt160>(base.GetScriptHashesForVerifying(snapshot));
            if (AssetId != null)
            {
                AssetState asset = snapshot.Assets.TryGet(AssetId);
                if (asset == null) throw new InvalidOperationException();
                hashes.Add(asset.Issuer);
            }

            return hashes.OrderBy(p => p).ToArray();
        }

        public override bool Verify(Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            if (Version < 1) return false;
            if (!base.Verify(snapshot, mempool)) return false;
            if (Value == null || Value <= Fixed8.Zero) return false;
            if (AssetId == null) return false;
            if (Address == null) return false;

            AssetState asset = snapshot.Assets.TryGet(AssetId);
            if (asset == null) return false;
            if (asset.Amount < Fixed8.Zero) return false;
            if (asset.Available < Value) return false;
            return true;
        }
    }
}
