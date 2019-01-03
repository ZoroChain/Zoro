using Zoro.Persistence;
using System.Collections.Generic;
using static Zoro.Ledger.Blockchain;

namespace Zoro.Plugins
{
    public interface IPersistencePlugin
    {
        void OnPersist(Snapshot snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList);
    }
}
