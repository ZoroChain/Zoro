using Zoro.Cryptography.ECC;
using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Network.P2P.Payloads;
using Neo.VM;
using Zoro.Wallets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Zoro.Ledger
{
    public class AssetState : StateBase, ICloneable<AssetState>
    {
        public UInt256 AssetId;
        public AssetType AssetType;
        public string Name;
        public string FullName;
        public Fixed8 Amount;
        public Fixed8 Available;
        public byte Precision;
        public const byte FeeMode = 0;
        public Fixed8 Fee;
        public UInt160 FeeAddress;
        public ECPoint Owner;
        public UInt160 Admin;
        public UInt160 Issuer;
        public uint BlockIndex;
        public bool IsFrozen;

        public override int Size => base.Size + AssetId.Size + sizeof(AssetType) + Name.GetVarSize() + FullName.GetVarSize() + Amount.Size + Available.Size + sizeof(byte) + sizeof(byte) + Fee.Size + FeeAddress.Size + Owner.Size + Admin.Size + Issuer.Size + sizeof(uint) + sizeof(bool);

        public AssetState()
        {
            StateVersion = 1;
        }

        AssetState ICloneable<AssetState>.Clone()
        {
            return new AssetState
            {
                StateVersion = StateVersion,
                AssetId = AssetId,
                AssetType = AssetType,
                Name = Name,
                FullName = FullName,
                Amount = Amount,
                Available = Available,
                Precision = Precision,
                //FeeMode = FeeMode,
                Fee = Fee,
                FeeAddress = FeeAddress,
                Owner = Owner,
                Admin = Admin,
                Issuer = Issuer,
                BlockIndex = BlockIndex,
                IsFrozen = IsFrozen,
                _names = _names
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            byte version = reader.ReadByte();
            if (version > StateVersion)
                throw new FormatException();
            AssetId = reader.ReadSerializable<UInt256>();
            AssetType = (AssetType)reader.ReadByte();
            Name = reader.ReadVarString();
            FullName = version >= 1 ? reader.ReadVarString() : Name;
            Amount = reader.ReadSerializable<Fixed8>();
            Available = reader.ReadSerializable<Fixed8>();
            Precision = reader.ReadByte();
            reader.ReadByte(); //FeeMode
            Fee = reader.ReadSerializable<Fixed8>(); //Fee
            FeeAddress = reader.ReadSerializable<UInt160>();
            Owner = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
            Admin = reader.ReadSerializable<UInt160>();
            Issuer = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            IsFrozen = reader.ReadBoolean();
        }

        void ICloneable<AssetState>.FromReplica(AssetState replica)
        {
            AssetId = replica.AssetId;
            AssetType = replica.AssetType;
            Name = replica.Name;
            FullName = replica.FullName;
            Amount = replica.Amount;
            Available = replica.Available;
            Precision = replica.Precision;
            //FeeMode = replica.FeeMode;
            Fee = replica.Fee;
            FeeAddress = replica.FeeAddress;
            Owner = replica.Owner;
            Admin = replica.Admin;
            Issuer = replica.Issuer;
            BlockIndex = replica.BlockIndex;
            IsFrozen = replica.IsFrozen;
            _names = replica._names;
        }

        private Dictionary<CultureInfo, string> _names;
        public string GetName(CultureInfo culture = null)
        {
            if (_names == null)
            {
                JObject name_obj;
                try
                {
                    name_obj = JObject.Parse(Name);
                }
                catch (FormatException)
                {
                    name_obj = Name;
                }
                if (name_obj is JString)
                    _names = new Dictionary<CultureInfo, string> { { new CultureInfo("en"), name_obj.AsString() } };
                else
                    _names = ((JArray)name_obj).Where(p => p.ContainsProperty("lang") && p.ContainsProperty("name")).ToDictionary(p => new CultureInfo(p["lang"].AsString()), p => p["name"].AsString());
            }
            if (culture == null) culture = CultureInfo.CurrentCulture;
            if (_names.TryGetValue(culture, out string name))
            {
                return name;
            }
            else if (_names.TryGetValue(en, out name))
            {
                return name;
            }
            else
            {
                return _names.Values.First();
            }
        }

        private Dictionary<CultureInfo, string> _fullnames;
        public string GetFullName(CultureInfo culture = null)
        {
            if (_fullnames == null)
            {
                JObject name_obj;
                try
                {
                    name_obj = JObject.Parse(FullName);
                }
                catch (FormatException)
                {
                    name_obj = FullName;
                }
                if (name_obj is JString)
                    _fullnames = new Dictionary<CultureInfo, string> { { new CultureInfo("en"), name_obj.AsString() } };
                else
                    _fullnames = ((JArray)name_obj).Where(p => p.ContainsProperty("lang") && p.ContainsProperty("name")).ToDictionary(p => new CultureInfo(p["lang"].AsString()), p => p["name"].AsString());
            }
            if (culture == null) culture = CultureInfo.CurrentCulture;
            if (_fullnames.TryGetValue(culture, out string fullname))
            {
                return fullname;
            }
            else if (_fullnames.TryGetValue(en, out fullname))
            {
                return fullname;
            }
            else
            {
                return _fullnames.Values.First();
            }
        }

        private static readonly CultureInfo en = new CultureInfo("en");

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(AssetId);
            writer.Write((byte)AssetType);
            writer.WriteVarString(Name);
            writer.WriteVarString(FullName);
            writer.Write(Amount);
            writer.Write(Available);
            writer.Write(Precision);
            writer.Write(FeeMode);
            writer.Write(Fee);
            writer.Write(FeeAddress);
            writer.Write(Owner);
            writer.Write(Admin);
            writer.Write(Issuer);
            writer.Write(BlockIndex);
            writer.Write(IsFrozen);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["id"] = AssetId.ToString();
            json["type"] = AssetType;
            try
            {
                json["name"] = Name == "" ? null : JObject.Parse(Name);
            }
            catch (FormatException)
            {
                json["name"] = Name;
            }
            try
            {
                json["fullname"] = FullName == "" ? null : JObject.Parse(FullName);
            }
            catch (FormatException)
            {
                json["fullname"] = FullName;
            }
            json["amount"] = Amount.ToString();
            json["available"] = Available.ToString();
            json["precision"] = Precision;
            json["owner"] = Owner.ToString();
            json["admin"] = Admin.ToAddress();
            json["issuer"] = Issuer.ToAddress();
            json["block_index"] = BlockIndex;
            json["frozen"] = IsFrozen;
            return json;
        }

        public override string ToString()
        {
            return GetName();
        }
    }
}
