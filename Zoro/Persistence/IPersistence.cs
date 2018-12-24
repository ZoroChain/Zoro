using Zoro.Cryptography.ECC;
using Zoro.IO.Caching;
using Zoro.IO.Wrappers;
using Zoro.Ledger;

namespace Zoro.Persistence
{
    public interface IPersistence
    {
        DataCache<UInt256, BlockState> Blocks { get; }
        DataCache<UInt256, TransactionState> Transactions { get; }
        DataCache<UInt160, AccountState> Accounts { get; }
        DataCache<UInt160, AppChainState> AppChains { get; }
        DataCache<UInt160, NativeNEP5State> NativeNEP5s { get; }        
        DataCache<UInt256, TransferState> Transfers { get; }
        DataCache<UInt256, AssetState> Assets { get; }
        DataCache<UInt160, ContractState> Contracts { get; }
        DataCache<StorageKey, StorageItem> Storages { get; }
        DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList { get; }
        MetaDataCache<HashIndexState> BlockHashIndex { get; }
        MetaDataCache<HashIndexState> HeaderHashIndex { get; }
        MetaDataCache<AppChainState> AppChainState { get; }

        Blockchain Blockchain { get; }
    }
}
