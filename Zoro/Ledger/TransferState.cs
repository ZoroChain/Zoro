using Zoro.IO;
using System.IO;

namespace Zoro.Ledger
{
    public class TransferState : StateBase, ICloneable<TransferState>
    {
        public Fixed8 Value = Fixed8.Zero;
        public UInt160 From = new UInt160();
        public UInt160 To = new UInt160();
        
        public override int Size => base.Size + Value.Size + From.Size + To.Size;

        TransferState ICloneable<TransferState>.Clone()
        {
            return new TransferState
            {
                Value = Value,
                From = From,
                To = To
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Value = reader.ReadSerializable<Fixed8>();
            From = reader.ReadSerializable<UInt160>();
            To = reader.ReadSerializable<UInt160>();
        }

        void ICloneable<TransferState>.FromReplica(TransferState replica)
        {
            Value = replica.Value;
            From = replica.From;
            To = replica.To;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Value);
            writer.Write(From);
            writer.Write(To);
        }
    }
}
