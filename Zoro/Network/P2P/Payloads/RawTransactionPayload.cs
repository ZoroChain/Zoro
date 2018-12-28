using Zoro.IO;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Zoro.Network.P2P.Payloads
{
    public class RawTransactionPayload : ISerializable
    {
        public const int MaxCount = 500;
        public const int MaxPayloadSize = 1024 * 1024;

        public Transaction[] Array;

        public int Size => Array.GetVarSize();

        public static RawTransactionPayload Create(IEnumerable<Transaction> transactions)
        {
            return new RawTransactionPayload
            {
                Array = transactions.ToArray()
            };
        }

        public static IEnumerable<RawTransactionPayload> CreateGroup(Transaction[] transactions)
        {
            int skip = 0;
            int size = 0;
            int count = 0;
            
            for (int i = 0; i < transactions.Length; i ++, count ++)
            {
                size += transactions[i].Size;
                if (size >= MaxPayloadSize || count >= MaxCount)
                {
                    yield return new RawTransactionPayload
                    {
                        Array = transactions.Skip(skip).Take(count).ToArray()
                    };

                    skip = i;
                    size = 0;
                    count = 0;
                }
            }

            yield return new RawTransactionPayload
            {
                Array = transactions.Skip(skip).Take(count).ToArray()
            };
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Array = new Transaction[reader.ReadVarInt((ulong)MaxCount)];
            for (int i = 0; i < Array.Length; i++)
            {
                Array[i] = Transaction.DeserializeFrom(reader);
            }
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(Array);
        }
    }
}
