using Zoro.Persistence;

namespace Zoro.Ledger
{
    public sealed class GlobalAsset
    {
        private Blockchain blockchain;

        public UInt256 AssetId { get; private set; }

        public GlobalAsset(Blockchain blockchain, UInt256 assetId)
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

        public void AddBalance(Snapshot snapshot, UInt160 address, Fixed8 value)
        {
            if (value <= Fixed8.Zero)
                return;

            AccountState account = snapshot.Accounts.GetAndChange(address, () => new AccountState(address));

            if (account.Balances.ContainsKey(AssetId))
                account.Balances[AssetId] += value;
            else
                account.Balances[AssetId] = value;
        }

        public bool SubBalance(Snapshot snapshot, UInt160 address, Fixed8 value)
        {
            AccountState account = snapshot.Accounts.GetAndChange(address, () => new AccountState(address));
            if (account == null)
                return false;

            if (!account.Balances.TryGetValue(AssetId, out Fixed8 amount) || amount < value)
                return false;

            account.Balances[AssetId] = amount - value;

            return true;
        }

        public bool Transfer(Snapshot snapshot, UInt160 from, UInt160 to, Fixed8 value)
        {
            if (value <= Fixed8.Zero)
                return false;

            if (from.Equals(to))
                return false;

            if (!SubBalance(snapshot, from, value))
                return false;

            AddBalance(snapshot, to, value);

            return true;
        }
    }
}
