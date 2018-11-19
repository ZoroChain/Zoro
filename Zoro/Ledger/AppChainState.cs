using Zoro.Cryptography.ECC;
using Zoro.IO;
using Zoro.IO.Json;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Zoro.Ledger
{
    public class AppChainState : StateBase, ICloneable<AppChainState>
    {
        public UInt160 Hash;
        public string Name;
        public ECPoint Owner;
        public uint Timestamp;
        public string[] SeedList;
        public ECPoint[] StandbyValidators;

        public override int Size => base.Size + Hash.Size + Name.GetVarSize() + Owner.Size + sizeof(uint) + SeedList.GetVarSize() + StandbyValidators.GetVarSize();

        public AppChainState() { }

        public AppChainState(UInt160 hash)
        {
            this.Hash = hash;
            this.Name = "";
            this.Owner = ECCurve.Secp256r1.Infinity;
            this.Timestamp = 0;
            this.SeedList = new string[0];
            this.StandbyValidators = new ECPoint[0];
        }

        AppChainState ICloneable<AppChainState>.Clone()
        {
            return new AppChainState
            {
                Hash = Hash,
                Name = Name,
                Owner = Owner,
                Timestamp = Timestamp,
                SeedList = SeedList,
                StandbyValidators = StandbyValidators,
                _names = _names
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Hash = reader.ReadSerializable<UInt160>();
            Name = reader.ReadVarString();
            Owner = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
            Timestamp = reader.ReadUInt32();
            SeedList = new string[reader.ReadVarInt()];
            for (int i = 0; i < SeedList.Length; i++)
                SeedList[i] = reader.ReadVarString();
            StandbyValidators = new ECPoint[reader.ReadVarInt()];
            for (int i = 0; i < StandbyValidators.Length; i++)
                StandbyValidators[i] = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
        }

        void ICloneable<AppChainState>.FromReplica(AppChainState replica)
        {
            Hash = replica.Hash;
            Name = replica.Name;
            Owner = replica.Owner;
            Timestamp = replica.Timestamp;
            SeedList = replica.SeedList;
            StandbyValidators = replica.StandbyValidators;
            _names = replica._names;
        }

        public void CopyFrom(AppChainState state)
        {
            Hash = state.Hash;
            Name = state.Name;
            Owner = state.Owner;
            Timestamp = state.Timestamp;
            SeedList = state.SeedList;
            StandbyValidators = state.StandbyValidators;
            _names = state._names;
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

        private static readonly CultureInfo en = new CultureInfo("en");

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Hash);
            writer.WriteVarString(Name);
            writer.Write(Owner);
            writer.Write(Timestamp);
            writer.WriteVarStringArray(SeedList);
            writer.Write(StandbyValidators);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["hash"] = Hash.ToString();
            try
            {
                json["name"] = Name == "" ? null : JObject.Parse(Name);
            }
            catch (FormatException)
            {
                json["name"] = Name;
            }
            json["owner"] = Owner.ToString();
            json["timestamp"] = Timestamp;
            json["seedlist"] = new JArray(SeedList.Select(p => (JObject)p));
            json["validators"] = new JArray(StandbyValidators.Select(p => (JObject)p.ToString()));
            return json;
        }

        public override string ToString()
        {
            return GetName();
        }

        public bool CompareStandbyValidators(ECPoint[] validators)
        {
            if (StandbyValidators.Length != validators.Length)
                return false;

            for (int i = 0; i < StandbyValidators.Length; i++)
            {
                if (!StandbyValidators[i].Equals(validators[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
