using Zoro.IO;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Zoro.Network.P2P.Payloads
{
    public class CompressedTransactionPayload : ISerializable
    {
        public const int MaxCount = 500;

        public int TransactionCount;
        public byte[] CompressedData;

        public int Size => CompressedData.Length;

        public static IEnumerable<CompressedTransactionPayload> CreateGroup(Transaction[] transactions)
        {
            for (int i = 0; i < transactions.Length; i += MaxCount)
            {
                Transaction[] txn = transactions.Skip(i).Take(MaxCount).ToArray();
                yield return new CompressedTransactionPayload
                {
                    TransactionCount = txn.Length,
                    CompressedData = CompressTransactions(txn)
                };
            }
        }

        private static byte[] CompressTransactions(Transaction[] transactions)
        {
            byte[] data = transactions.ToByteArray();
            using (var ms = new MemoryStream(data) { Position = 0 })
            {
                using (var outputms = new MemoryStream())
                {
                    using (var deflateStream = new DeflateStream(outputms, CompressionMode.Compress, true))
                    {
                        var buf = new byte[1024];
                        int len;
                        while ((len = ms.Read(buf, 0, buf.Length)) > 0)
                            deflateStream.Write(buf, 0, len);
                    }
                    return outputms.ToArray();
                }
            }
        }

        public static Transaction[] DecompressTransactions(byte[] data)
        {
            using (var ms = new MemoryStream(data) { Position = 0 })
            {
                using (var outputms = new MemoryStream())
                {
                    using (var deflateStream = new DeflateStream(ms, CompressionMode.Decompress, true))
                    {
                        var buf = new byte[1024];
                        int len;
                        while ((len = deflateStream.Read(buf, 0, buf.Length)) > 0)
                            outputms.Write(buf, 0, len);
                    }

                    outputms.Seek(0, SeekOrigin.Begin);

                    using (BinaryReader reader = new BinaryReader(outputms, Encoding.UTF8))
                    {
                        ulong count = reader.ReadVarInt();
                        Transaction[] txn = new Transaction[count];
                        for (ulong i = 0;i < count; i++)
                        {
                            txn[i] = Transaction.DeserializeFrom(reader);
                        }
                        return txn;
                    }
                }
            }
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            TransactionCount = reader.ReadInt32();
            CompressedData = reader.ReadBytes(reader.ReadInt32());
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(TransactionCount);
            writer.Write(CompressedData.Length);
            writer.Write(CompressedData);
        }
    }
}
