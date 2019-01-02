using Zoro.Cryptography.ECC;
using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Persistence;
using Zoro.SmartContract;
using Zoro.Wallets;
using Zoro.Ledger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zoro.Network.P2P.Payloads
{
    [Obsolete]
    public class RegisterTransaction : Transaction
    {
        public AssetType AssetType;
        public string Name;
        public string FullName;
        public Fixed8 Amount;
        public byte Precision;
        public ECPoint Owner;
        public UInt160 Admin;

        private UInt160 _script_hash = null;
        internal UInt160 OwnerScriptHash
        {
            get
            {
                if (_script_hash == null)
                {
                    _script_hash = Contract.CreateSignatureRedeemScript(Owner).ToScriptHash();
                }
                return _script_hash;
            }
        }

        public override int Size => base.Size + sizeof(AssetType) + Name.GetVarSize() + FullName.GetVarSize() + Amount.Size + sizeof(byte) + Owner.Size + Admin.Size;

        public override Fixed8 SystemFee
        {
            get
            {
                if (AssetType == AssetType.UtilityToken)
                    return Fixed8.Zero;
                return base.SystemFee;
            }
        }

        public RegisterTransaction()
            : base(TransactionType.RegisterTransaction)
        {
            Version = 1;
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 1) throw new FormatException();
            AssetType = (AssetType)reader.ReadByte();
            Name = reader.ReadVarString(1024);
            FullName = Version > 0 ? reader.ReadVarString(1024) : Name;
            Amount = reader.ReadSerializable<Fixed8>();
            Precision = reader.ReadByte();
            Owner = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
            if (Owner.IsInfinity && AssetType != AssetType.UtilityToken)
                throw new FormatException();
            Admin = reader.ReadSerializable<UInt160>();
        }

        public override UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            UInt160 owner = Contract.CreateSignatureRedeemScript(Owner).ToScriptHash();
            return base.GetScriptHashesForVerifying(snapshot).Union(new[] { owner }).OrderBy(p => p).ToArray();
        }

        protected override void OnDeserialized()
        {
            base.OnDeserialized();
            if (AssetType == AssetType.UtilityToken && !Hash.Equals(Blockchain.UtilityToken.Hash))
                throw new FormatException();
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            writer.Write((byte)AssetType);
            writer.WriteVarString(Name);
            if (Version > 0)
                writer.WriteVarString(FullName);
            writer.Write(Amount);
            writer.Write(Precision);
            writer.Write(Owner);
            writer.Write(Admin);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["asset"] = new JObject();
            json["asset"]["type"] = AssetType;
            try
            {
                json["asset"]["name"] = Name == "" ? null : JObject.Parse(Name);
            }
            catch (FormatException)
            {
                json["asset"]["name"] = Name;
            }
            try
            {
                json["asset"]["fullname"] = FullName == "" ? null : JObject.Parse(FullName);
            }
            catch (FormatException)
            {
                json["asset"]["fullname"] = FullName;
            }
            json["asset"]["amount"] = Amount.ToString();
            json["asset"]["precision"] = Precision;
            json["asset"]["owner"] = Owner.ToString();
            json["asset"]["admin"] = Admin.ToAddress();
            return json;
        }

        public override bool Verify(Snapshot snapshot)
        {
            return false;
        }

        public override UInt160 GetAccountScriptHash(Snapshot snapshot)
        {
            return null;
        }
    }
}
