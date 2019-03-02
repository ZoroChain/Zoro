using Zoro.IO;
using Zoro.Ledger;
using Zoro.Persistence;
using System.Linq;
using System.Numerics;

namespace Zoro.SmartContract.NativeNEP5
{
    public class NativeAPI
    {
        public static BigInteger BalanceOf(Snapshot snapshot, UInt160 assetId, UInt160 address)
        {
            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();

            return StorageGet(snapshot, assetId, key).AsBigInteger();
        }

        public static void AddBalance(Snapshot snapshot, UInt160 assetId, UInt160 address, Fixed8 amount)
        {
            if (amount <= Fixed8.Zero)
                return;

            BigInteger value = new BigInteger(amount.GetData());

            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();

            BigInteger balance = StorageGet(snapshot, assetId, key).AsBigInteger();

            StoragePut(snapshot, assetId, key, balance + value);
        }

        public static bool SubBalance(Snapshot snapshot, UInt160 assetId, UInt160 address, Fixed8 amount)
        {
            BigInteger value = new BigInteger(amount.GetData());

            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();

            BigInteger balance = StorageGet(snapshot, assetId, key).AsBigInteger();

            if (balance < value)
                return false;

            if (balance == value)
            {
                StorageDelete(snapshot, assetId, key);
            }
            else
            {
                StoragePut(snapshot, assetId, key, balance - value);
            }

            return true;
        }

        public static bool Transfer(Snapshot snapshot, UInt160 assetId, UInt160 from, UInt160 to, Fixed8 amount)
        {
            BigInteger value = new BigInteger(amount.GetData());

            if (value <= 0)
                return false;

            if (from.Equals(to))
                return false;

            if (from.ToArray().Length != 20 || to.ToArray().Length != 20)
                return false;

            var keyFrom = new byte[] { 0x11 }.Concat(from.ToArray()).ToArray();
            BigInteger from_value = StorageGet(snapshot, assetId, keyFrom).AsBigInteger();
            if (from_value < value)
                return false;
            if (from_value == value)
            {
                StorageDelete(snapshot, assetId, keyFrom);
            }
            else
            {
                StoragePut(snapshot, assetId, keyFrom, from_value - value);
            }

            var keyTo = new byte[] { 0x11 }.Concat(to.ToArray()).ToArray();
            BigInteger to_value = StorageGet(snapshot, assetId, keyTo).AsBigInteger();
            StoragePut(snapshot, assetId, keyTo, to_value + value);

            return true;
        }

        public static bool Approve(Snapshot snapshot, UInt160 assetId, UInt160 from, UInt160 to, Fixed8 amount)
        {
            BigInteger value = new BigInteger(amount.GetData());

            if (value < 0)
                return false;

            if (from.Equals(to))
                return false;

            if (from.ToArray().Length != 20 || to.ToArray().Length != 20)
                return false;

            var keyFrom = new byte[] { 0x11 }.Concat(from.ToArray()).ToArray();
            BigInteger from_value = StorageGet(snapshot, assetId, keyFrom).AsBigInteger();
            if (from_value < value)
                return false;

            var keyApprove = from.ToArray().Concat(to.ToArray()).ToArray();
            if (value == 0)
                StorageDelete(snapshot, assetId, keyApprove);
            else
                StoragePut(snapshot, assetId, keyApprove, value);
            return true;
        }

        public static bool TransferFrom(Snapshot snapshot, UInt160 assetId, UInt160 from, UInt160 to, Fixed8 amount)
        {
            BigInteger value = new BigInteger(amount.GetData());

            if (value <= 0)
                return false;

            if (from.Equals(to))
                return false;

            if (from.ToArray().Length != 20 || to.ToArray().Length != 20)
                return false;

            var keyFrom = new byte[] { 0x11 }.Concat(from.ToArray()).ToArray();
            BigInteger from_value = StorageGet(snapshot, assetId, keyFrom).AsBigInteger();

            var keyApprove = from.ToArray().Concat(to.ToArray()).ToArray();
            BigInteger approve_value = StorageGet(snapshot, assetId, keyApprove).AsBigInteger();

            if (from_value < value || approve_value < value)
                return false;

            //update Allowance
            if (approve_value == value)
            {
                StorageDelete(snapshot, assetId, keyApprove);
            }
            else
            {
                StoragePut(snapshot, assetId, keyApprove, approve_value - value);
            }

            //update from balance
            if (from_value == value)
            {
                StorageDelete(snapshot, assetId, keyFrom);
            }
            else
            {
                StoragePut(snapshot, assetId, keyFrom, from_value - value);
            }

            //update to balance
            var keyTo = new byte[] { 0x11 }.Concat(to.ToArray()).ToArray();
            BigInteger to_value = StorageGet(snapshot, assetId, keyTo).AsBigInteger();
            StoragePut(snapshot, assetId, keyTo, to_value + value);

            return true;
        }

        public static byte[] StorageGet(Snapshot snapshot, UInt160 assetId, byte[] key)
        {
            StorageItem item = snapshot.Storages.TryGet(new StorageKey
            {
                ScriptHash = assetId,
                Key = key
            });

            return item?.Value ?? new byte[0];
        }

        public static void StoragePut(Snapshot snapshot, UInt160 assetId, byte[] key, Fixed8 value)
        {
            StoragePut(snapshot, assetId, key, value.ToArray());
        }

        public static void StoragePut(Snapshot snapshot, UInt160 assetId, byte[] key, BigInteger value)
        {
            StoragePut(snapshot, assetId, key, value.ToByteArray());
        }

        public static void StoragePut(Snapshot snapshot, UInt160 assetId, byte[] key, byte[] value)
        {
            snapshot.Storages.GetAndChange(new StorageKey
            {
                ScriptHash = assetId,
                Key = key
            }, () => new StorageItem()).Value = value;
        }

        public static void StorageDelete(Snapshot snapshot, UInt160 assetId, byte[] key)
        {
            snapshot.Storages.Delete(new StorageKey
            {
                ScriptHash = assetId,
                Key = key
            });
        }

        public static void SaveTransferLog(Snapshot snapshot, UInt160 assetId, UInt256 TransactionHash, UInt160 from, UInt160 to, Fixed8 value)
        {
            var transferLog = new TransferLog
            {
                From = from,
                To = to,
                Value = value
            };

            StoragePut(snapshot, assetId, new byte[] { 0x13 }.Concat(TransactionHash.ToArray()).ToArray(), transferLog.ToArray());
        }

        public static TransferLog GetTransferLog(Snapshot snapshot, UInt160 assetId, byte[] key)
        {
            StorageItem item = snapshot.Storages.TryGet(new StorageKey
            {
                ScriptHash = assetId,
                Key = new byte[] { 0x13 }.Concat(key).ToArray()
            });

            return (TransferLog) item?.Value.AsSerializable(typeof(TransferLog));
        }

    }
}
