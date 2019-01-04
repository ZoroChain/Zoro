using Zoro.IO;
using Zoro.Ledger;
using Zoro.Persistence;
using System.Linq;
using System.Numerics;

namespace Zoro.SmartContract.NativeNEP5
{
    public class NativeToken
    {
        private UInt160 assetId;

        public NativeToken(UInt160 assetId)
        {
            this.assetId = assetId;
        }

        public BigInteger BalanceOf(Snapshot snapshot, UInt160 address)
        {
            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();

            return StorageGet(snapshot, key).AsBigInteger();
        }

        public void AddBalance(Snapshot snapshot, UInt160 address, Fixed8 amount)
        {
            if (amount <= Fixed8.Zero)
                return;

            BigInteger value = new BigInteger(amount.GetData());

            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();

            BigInteger balance = StorageGet(snapshot, key).AsBigInteger();

            StoragePut(snapshot, key, balance + value);
        }

        public bool SubBalance(Snapshot snapshot, UInt160 address, Fixed8 amount)
        {
            BigInteger value = new BigInteger(amount.GetData());

            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();

            BigInteger balance = StorageGet(snapshot, key).AsBigInteger();

            if (balance < value)
                return false;

            if (balance == value)
            {
                StorageDelete(snapshot, key);
            }
            else
            {
                StoragePut(snapshot, key, balance - value);
            }

            return true;
        }

        public bool Transfer(Snapshot snapshot, UInt160 from, UInt160 to, Fixed8 amount)
        {
            BigInteger value = new BigInteger(amount.GetData());

            if (value <= 0)
                return false;

            if (from.Equals(to))
                return false;

            if (from.ToArray().Length <= 0 || to.ToArray().Length <= 0)
                return false;

            if (from.ToArray().Length > 0)
            {
                var keyFrom = new byte[] { 0x11 }.Concat(from.ToArray()).ToArray();
                BigInteger from_value = StorageGet(snapshot, keyFrom).AsBigInteger();
                if (from_value < value)
                    return false;
                if (from_value == value)
                {
                    StorageDelete(snapshot, keyFrom);
                }
                else
                {
                    StoragePut(snapshot, keyFrom, from_value - value);
                }
            }

            if (to.ToArray().Length > 0)
            {
                var keyTo = new byte[] { 0x11 }.Concat(to.ToArray()).ToArray();
                BigInteger to_value = StorageGet(snapshot, keyTo).AsBigInteger();
                StoragePut(snapshot, keyTo, to_value + value);
            }

            return true;
        }

        private byte[] StorageGet(Snapshot snapshot, byte[] key)
        {
            StorageItem item = snapshot.Storages.TryGet(new StorageKey
            {
                ScriptHash = assetId,
                Key = key
            });

            return item?.Value ?? new byte[0];
        }

        private void StoragePut(Snapshot snapshot, byte[] key, Fixed8 value)
        {
            StoragePut(snapshot, key, value.ToArray());
        }

        private void StoragePut(Snapshot snapshot, byte[] key, BigInteger value)
        {
            StoragePut(snapshot, key, value.ToByteArray());
        }

        private void StoragePut(Snapshot snapshot, byte[] key, byte[] value)
        {
            snapshot.Storages.GetAndChange(new StorageKey
            {
                ScriptHash = assetId,
                Key = key
            }, () => new StorageItem()).Value = value;
        }

        private void StorageDelete(Snapshot snapshot, byte[] key)
        {
            snapshot.Storages.Delete(new StorageKey
            {
                ScriptHash = assetId,
                Key = key
            });
        }

        private void SaveTransferLog(Snapshot snapshot, UInt256 TransactionHash, UInt160 from, UInt160 to, Fixed8 value)
        {
            var transferLog = new TransferLog
            {
                From = from,
                To = to,
                Value = value
            };

            StoragePut(snapshot, new byte[] { 0x13 }.Concat(TransactionHash.ToArray()).ToArray(), transferLog.ToArray());
        }
    }
}
