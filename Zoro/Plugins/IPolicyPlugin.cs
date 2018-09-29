using Zoro.Network.P2P.Payloads;
using System.Collections.Generic;

namespace Zoro.Plugins
{
    public interface IPolicyPlugin
    {
        bool FilterForMemoryPool(Transaction tx);
        IEnumerable<Transaction> FilterForBlock(IEnumerable<Transaction> transactions);
    }
}
