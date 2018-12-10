using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Wallets;
using System;
using System.IO;

namespace Zoro.Network.P2P.Payloads
{
    public class TransactionDetail : ISerializable
    {
        public UInt256 AssetId;
        public UInt160 From;
        public UInt160 To;
        public Fixed8 Value;

        public int Size => AssetId.Size + To.Size + From.Size + Value.Size;

        void ISerializable.Deserialize(BinaryReader reader)
        {
            this.AssetId = reader.ReadSerializable<UInt256>();
            this.From = reader.ReadSerializable<UInt160>();
            this.To = reader.ReadSerializable<UInt160>();
            this.Value = reader.ReadSerializable<Fixed8>();
            if (Value <= Fixed8.Zero) throw new FormatException();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(AssetId);
            writer.Write(From);
            writer.Write(To);
            writer.Write(Value);
        }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["asset"] = AssetId.ToString();
            json["value"] = Value.ToString();
            json["from"] = From.ToAddress();
            json["to"] = To.ToAddress();
            return json;
        }
    }
}
