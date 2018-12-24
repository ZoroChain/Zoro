using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Wallets;
using Zoro.Cryptography.ECC;
using System;
using System.IO;

namespace Zoro.Ledger
{
    public class NativeNEP5State : StateBase, ICloneable<NativeNEP5State>
    {
        public UInt160 AssetId;
        public string Name;
        public string Symbol;
        public Fixed8 TotalSupply;
        public byte Decimals;
        public ECPoint Owner;
        public UInt160 Admin;
        public uint BlockIndex;
        public bool IsFrozen;

        public override int Size => base.Size + AssetId.Size + Name.GetVarSize() + Symbol.GetVarSize() + TotalSupply.Size + sizeof(byte) + Owner.Size + Admin.Size + sizeof(uint) + sizeof(bool);

        public NativeNEP5State()
        {
        }

        NativeNEP5State ICloneable<NativeNEP5State>.Clone()
        {
            return new NativeNEP5State
            {
                StateVersion = StateVersion,
                AssetId = AssetId,
                Name = Name,
                Symbol = Symbol,
                TotalSupply = TotalSupply,
                Decimals = Decimals,
                Owner = Owner,
                Admin = Admin,
                BlockIndex = BlockIndex,
                IsFrozen = IsFrozen,
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            byte version = reader.ReadByte();
            if (version > StateVersion)
                throw new FormatException();
            AssetId = reader.ReadSerializable<UInt160>();
            Name = reader.ReadVarString();
            Symbol = reader.ReadVarString();
            TotalSupply = reader.ReadSerializable<Fixed8>();
            Decimals = reader.ReadByte();
            Owner = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
            Admin = reader.ReadSerializable<UInt160>();
            BlockIndex = reader.ReadUInt32();
            IsFrozen = reader.ReadBoolean();
        }

        void ICloneable<NativeNEP5State>.FromReplica(NativeNEP5State replica)
        {
            AssetId = replica.AssetId;
            Name = replica.Name;
            Symbol = replica.Symbol;
            TotalSupply = replica.TotalSupply;
            Decimals = replica.Decimals;
            Owner = replica.Owner;
            Admin = replica.Admin;
            BlockIndex = replica.BlockIndex;
            IsFrozen = replica.IsFrozen;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(AssetId);
            writer.WriteVarString(Name);
            writer.WriteVarString(Symbol);
            writer.Write(TotalSupply);
            writer.Write(Decimals);
            writer.Write(Owner);
            writer.Write(Admin);
            writer.Write(BlockIndex);
            writer.Write(IsFrozen);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["id"] = AssetId.ToString();
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
                json["symbol"] = Symbol == "" ? null : JObject.Parse(Symbol);
            }
            catch (FormatException)
            {
                json["symbol"] = Symbol;
            }
            json["totalsupply"] = TotalSupply.ToString();
            json["decimals"] = Decimals;
            json["owner"] = Owner.ToString();
            json["admin"] = Admin.ToAddress();
            json["block_index"] = BlockIndex;
            json["frozen"] = IsFrozen;
            return json;
        }
    }
}
