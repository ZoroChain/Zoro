using Zoro.Persistence;

namespace Zoro.Plugins
{
    public interface IPersistencePlugin
    {
        void OnPersist(Snapshot snapshot);
    }
}
