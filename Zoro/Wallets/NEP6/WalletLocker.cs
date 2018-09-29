using System;

namespace Zoro.Wallets.NEP6
{
    internal class WalletLocker : IDisposable
    {
        private NEP6Wallet wallet;

        public WalletLocker(NEP6Wallet wallet)
        {
            this.wallet = wallet;
        }

        public void Dispose()
        {
            wallet.Lock();
        }
    }
}
