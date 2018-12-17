using Zoro.Cryptography.ECC;
using Zoro.IO.Caching;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Wrappers;
using Zoro.Ledger;
using System;
using System.Reflection;

namespace Zoro.Persistence.LevelDB
{
    public class LevelDBStore : Store, IDisposable
    {
        private readonly DB db;

        public LevelDBStore(string path)
        {
            this.db = DB.Open(path, new Options { CreateIfMissing = true });
            if (db.TryGet(ReadOptions.Default, SliceBuilder.Begin(Prefixes.SYS_Version), out Slice value) && Version.TryParse(value.ToString(), out Version version) && version >= Version.Parse("0.1.0"))
                return;
            WriteBatch batch = new WriteBatch();
            ReadOptions options = new ReadOptions { FillCache = false };
            using (Iterator it = db.NewIterator(options))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    batch.Delete(it.Key());
                }
            }
            db.Put(WriteOptions.Default, SliceBuilder.Begin(Prefixes.SYS_Version), Assembly.GetExecutingAssembly().GetName().Version.ToString());
            db.Write(WriteOptions.Default, batch);
        }

        public override void Dispose()
        {
            db.Dispose();
        }

        public override DataCache<UInt160, AccountState> GetAccounts()
        {
            return new DbCache<UInt160, AccountState>(db, null, null, Prefixes.ST_Account);
        }

        public override DataCache<UInt160, AppChainState> GetAppChains()
        {
            return new DbCache<UInt160, AppChainState>(db, null, null, Prefixes.ST_Appchain);
        }

        public override DataCache<UInt256, AssetState> GetAssets()
        {
            return new DbCache<UInt256, AssetState>(db, null, null, Prefixes.ST_Asset);
        }

        public override DataCache<UInt256, BlockState> GetBlocks()
        {
            return new DbCache<UInt256, BlockState>(db, null, null, Prefixes.DATA_Block);
        }

        public override DataCache<UInt160, ContractState> GetContracts()
        {
            return new DbCache<UInt160, ContractState>(db, null, null, Prefixes.ST_Contract);
        }

        public override Snapshot GetSnapshot()
        {
            return new DbSnapshot(db, Blockchain);
        }

        public override DataCache<StorageKey, StorageItem> GetStorages()
        {
            return new DbCache<StorageKey, StorageItem>(db, null, null, Prefixes.ST_Storage);
        }

        public override DataCache<UInt256, TransactionState> GetTransactions()
        {
            return new DbCache<UInt256, TransactionState>(db, null, null, Prefixes.DATA_Transaction);
        }

        public override DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList()
        {
            return new DbCache<UInt32Wrapper, HeaderHashList>(db, null, null, Prefixes.IX_HeaderHashList);
        }

        public override MetaDataCache<HashIndexState> GetBlockHashIndex()
        {
            return new DbMetaDataCache<HashIndexState>(db, null, null, Prefixes.IX_CurrentBlock);
        }

        public override MetaDataCache<HashIndexState> GetHeaderHashIndex()
        {
            return new DbMetaDataCache<HashIndexState>(db, null, null, Prefixes.IX_CurrentHeader);
        }

        public override MetaDataCache<AppChainState> GetAppChainState()
        {
            return new DbMetaDataCache<AppChainState>(db, null, null, Prefixes.IX_AppChainState);
        }

        public override DataCache<UInt256, TransferState> GetTransfers()
        {
            return new DbCache<UInt256, TransferState>(db, null, null, Prefixes.ST_Transfer);
        }
    }
}
