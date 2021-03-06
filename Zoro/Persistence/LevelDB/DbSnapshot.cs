﻿using Zoro.IO.Caching;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Wrappers;
using Zoro.Ledger;
using LSnapshot = Zoro.IO.Data.LevelDB.Snapshot;

namespace Zoro.Persistence.LevelDB
{
    internal class DbSnapshot : Snapshot
    {
        private readonly DB db;
        private readonly LSnapshot snapshot;
        private readonly WriteBatch batch;

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

        public DbSnapshot(DB db, Blockchain blockchain)
            : base(blockchain)
        {
            this.db = db;
            this.snapshot = db.GetSnapshot();
            this.batch = new WriteBatch();
            ReadOptions options = new ReadOptions { FillCache = false, Snapshot = snapshot };
            Blocks = new DbCache<UInt256, BlockState>(db, options, batch, Prefixes.DATA_Block);
            Transactions = new DbCache<UInt256, TransactionState>(db, options, batch, Prefixes.DATA_Transaction);
            AppChains = new DbCache<UInt160, AppChainState>(db, options, batch, Prefixes.ST_Appchain);
            NativeNEP5s = new DbCache<UInt160, NativeNEP5State>(db, options, batch, Prefixes.ST_NativeNEP5);
            Contracts = new DbCache<UInt160, ContractState>(db, options, batch, Prefixes.ST_Contract);
            Storages = new DbCache<StorageKey, StorageItem>(db, options, batch, Prefixes.ST_Storage);
            HeaderHashList = new DbCache<UInt32Wrapper, HeaderHashList>(db, options, batch, Prefixes.IX_HeaderHashList);
            BlockHashIndex = new DbMetaDataCache<HashIndexState>(db, options, batch, Prefixes.IX_CurrentBlock);
            HeaderHashIndex = new DbMetaDataCache<HashIndexState>(db, options, batch, Prefixes.IX_CurrentHeader);
            AppChainState = new DbMetaDataCache<AppChainState>(db, options, batch, Prefixes.IX_AppChainState);
        }

        public override void Commit()
        {
            base.Commit();
            db.Write(WriteOptions.Default, batch);
        }

        public override void Dispose()
        {
            snapshot.Dispose();
        }
    }
}
