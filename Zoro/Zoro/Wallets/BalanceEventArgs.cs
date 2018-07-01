using Zoro.Core;
using System;

namespace Zoro.Wallets
{
    public class BalanceEventArgs : EventArgs
    {
        public Transaction Transaction;
        public UInt160[] RelatedAccounts;
        public uint? Height;
        public uint Time;
    }
}
