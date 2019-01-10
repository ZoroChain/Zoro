using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Network.P2P.Payloads;
using System;
using System.IO;

namespace Zoro.Ledger
{
    public class TransactionState : StateBase, ICloneable<TransactionState>
    {
        public enum ExecuteResult : byte
        {
            Succeed,
            InsufficentFee,
            Fault,
        }

        public uint BlockIndex;
        public Transaction Transaction;
        public ExecuteResult Result;

        public override int Size => base.Size + sizeof(uint) + Transaction.Size + sizeof(byte);

        public TransactionState()
        {
            StateVersion = 1;
        }
        
        TransactionState ICloneable<TransactionState>.Clone()
        {
            return new TransactionState
            {
                StateVersion = StateVersion,
                BlockIndex = BlockIndex,
                Transaction = Transaction,
                Result = Result
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            byte version = reader.ReadByte();
            if (version > StateVersion)
                throw new FormatException();

            BlockIndex = reader.ReadUInt32();
            Transaction = Transaction.DeserializeFrom(reader);
            if (version >= 1)
                Result = (ExecuteResult)reader.ReadByte();
        }

        void ICloneable<TransactionState>.FromReplica(TransactionState replica)
        {
            BlockIndex = replica.BlockIndex;
            Transaction = replica.Transaction;
            Result = replica.Result;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(BlockIndex);
            writer.Write(Transaction);
            if (StateVersion >= 1)
                writer.Write((byte)Result);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["height"] = BlockIndex;
            json["tx"] = Transaction.ToJson();
            json["result"] = Result;
            return json;
        }
    }
}
