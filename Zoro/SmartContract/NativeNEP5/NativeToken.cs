using Zoro.IO;
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

            return NativeAPI.StorageGet(snapshot, assetId, key).AsBigInteger();
        }

        public void AddBalance(Snapshot snapshot, UInt160 address, Fixed8 amount)
        {
            NativeAPI.AddBalance(snapshot, assetId, address, amount);
        }

        public bool SubBalance(Snapshot snapshot, UInt160 address, Fixed8 amount)
        {
            return NativeAPI.SubBalance(snapshot, assetId, address, amount);
        }

        public bool Transfer(Snapshot snapshot, UInt160 from, UInt160 to, Fixed8 amount)
        {
            return NativeAPI.Transfer(snapshot, assetId, from, to, amount);
        }
    }
}
