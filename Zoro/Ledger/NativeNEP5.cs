using Zoro.Persistence;

namespace Zoro.Ledger
{
    public sealed class NativeNEP5
    {
        private Blockchain blockchain;

        public UInt256 AssetId { get; private set; }

        public NativeNEP5(Blockchain blockchain, UInt256 assetId)
        {
            this.blockchain = blockchain;

            AssetId = assetId;
        }

        public Fixed8 BalanceOf(UInt160 address)
        {
            AccountState account = blockchain.Store.GetAccounts().TryGet(address);

            if (account != null && account.Balances.TryGetValue(AssetId, out Fixed8 balance))
                return balance;

            return Fixed8.Zero;
        }

        public bool Transfer(Snapshot snapshot, UInt160 from, UInt160 to, Fixed8 value)
        {
            if (value.Equals(Fixed8.Zero))
                return false;

            if (from.Equals(to))
                return false;

            AccountState accountFrom = snapshot.Accounts.GetAndChange(from);
            if (accountFrom == null)
                return false;

            if (!accountFrom.Balances.TryGetValue(AssetId, out Fixed8 amount) || amount < value)
                return false;

            accountFrom.Balances[AssetId] = amount - value;

            AccountState accountTo = snapshot.Accounts.GetAndChange(to, () => new AccountState(to));
            if (accountTo.Balances.ContainsKey(AssetId))
                accountTo.Balances[AssetId] += value;
            else
                accountTo.Balances[AssetId] = value;

            return true;
        }
    }
}
