using System;

namespace Zoro.Ledger
{
    public class AppChainEventArgs : EventArgs
    {
        public string Method { get; }
        public AppChainState State { get; }

        public AppChainEventArgs(string method, AppChainState state)
        {
            this.Method = method;
            this.State = state;
        }
    }
}
