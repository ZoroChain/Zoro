using Zoro.Persistence;

namespace Zoro.Network.P2P.Payloads
{
    public interface IInventory : IVerifiable
    {
        UInt256 Hash { get; }

        UInt160 ChainHash { get; set; }

        InventoryType InventoryType { get; }

        bool Verify(Snapshot snapshot);
    }
}
