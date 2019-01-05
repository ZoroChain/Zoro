using Zoro.IO.Caching;
using Zoro.IO.Wrappers;
using Zoro.Ledger;

namespace Zoro.Persistence
{
    internal class CloneSnapshot : Snapshot
    {
        public override DataCache<UInt256, BlockState> Blocks { get; }
        public override DataCache<UInt256, TransactionState> Transactions { get; }
        public override DataCache<UInt160, AppChainState> AppChains { get; }
        public override DataCache<UInt160, NativeNEP5State> NativeNEP5s { get; }
        public override DataCache<UInt160, ContractState> Contracts { get; }
        public override DataCache<StorageKey, StorageItem> Storages { get; }
        public override DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList { get; }
        public override MetaDataCache<HashIndexState> BlockHashIndex { get; }
        public override MetaDataCache<HashIndexState> HeaderHashIndex { get; }
        public override MetaDataCache<AppChainState> AppChainState { get; }

        public CloneSnapshot(Snapshot snapshot, Blockchain blockchain)
            : base(blockchain)
        {
            this.PersistingBlock = snapshot.PersistingBlock;
            this.Blocks = snapshot.Blocks.CreateSnapshot();
            this.Transactions = snapshot.Transactions.CreateSnapshot();
            this.AppChains = snapshot.AppChains.CreateSnapshot();
            this.NativeNEP5s = snapshot.NativeNEP5s.CreateSnapshot();
            this.Contracts = snapshot.Contracts.CreateSnapshot();
            this.Storages = snapshot.Storages.CreateSnapshot();
            this.HeaderHashList = snapshot.HeaderHashList.CreateSnapshot();
            this.BlockHashIndex = snapshot.BlockHashIndex.CreateSnapshot();
            this.HeaderHashIndex = snapshot.HeaderHashIndex.CreateSnapshot();
            this.AppChainState = snapshot.AppChainState.CreateSnapshot();
        }
    }
}
