using Zoro.IO;
using System.IO;

namespace Zoro.Ledger
{
    public class TransferState : StateBase, ICloneable<TransferState>
    {
        public UInt256 AssetId = new UInt256();
        public Fixed8 Value = Fixed8.Zero;
        public UInt160 From = new UInt160();
        public UInt160 To = new UInt160();
        
        public override int Size => base.Size + AssetId.Size + Value.Size + From.Size + To.Size;

        TransferState ICloneable<TransferState>.Clone()
        {
            return new TransferState
            {
                AssetId = AssetId,
                Value = Value,
                From = From,
                To = To
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            AssetId = reader.ReadSerializable<UInt256>();
            Value = reader.ReadSerializable<Fixed8>();
            From = reader.ReadSerializable<UInt160>();
            To = reader.ReadSerializable<UInt160>();
        }

        void ICloneable<TransferState>.FromReplica(TransferState replica)
        {
            AssetId = replica.AssetId;
            Value = replica.Value;
            From = replica.From;
            To = replica.To;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(AssetId);
            writer.Write(Value);
            writer.Write(From);
            writer.Write(To);
        }
    }
}
