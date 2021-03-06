﻿using Zoro.Cryptography.ECC;
using Zoro.IO.Caching;
using Zoro.IO.Wrappers;
using Zoro.Ledger;
using System;

namespace Zoro.Persistence
{
    public abstract class Store : IDisposable, IPersistence
    {
        DataCache<UInt256, BlockState> IPersistence.Blocks => GetBlocks();
        DataCache<UInt256, TransactionState> IPersistence.Transactions => GetTransactions();
        DataCache<UInt160, AppChainState> IPersistence.AppChains => GetAppChains();
        DataCache<UInt160, NativeNEP5State> IPersistence.NativeNEP5s => GetNativeNEP5s();
        DataCache<UInt160, ContractState> IPersistence.Contracts => GetContracts();
        DataCache<StorageKey, StorageItem> IPersistence.Storages => GetStorages();
        DataCache<UInt32Wrapper, HeaderHashList> IPersistence.HeaderHashList => GetHeaderHashList();
        MetaDataCache<HashIndexState> IPersistence.BlockHashIndex => GetBlockHashIndex();
        MetaDataCache<HashIndexState> IPersistence.HeaderHashIndex => GetHeaderHashIndex();
        MetaDataCache<AppChainState> IPersistence.AppChainState => GetAppChainState();

        public abstract DataCache<UInt256, BlockState> GetBlocks();
        public abstract DataCache<UInt256, TransactionState> GetTransactions();
        public abstract DataCache<UInt160, AppChainState> GetAppChains();
        public abstract DataCache<UInt160, NativeNEP5State> GetNativeNEP5s();
        public abstract DataCache<UInt160, ContractState> GetContracts();
        public abstract DataCache<StorageKey, StorageItem> GetStorages();
        public abstract DataCache<UInt32Wrapper, HeaderHashList> GetHeaderHashList();
        public abstract MetaDataCache<HashIndexState> GetBlockHashIndex();
        public abstract MetaDataCache<HashIndexState> GetHeaderHashIndex();
        public abstract MetaDataCache<AppChainState> GetAppChainState();

        public abstract Snapshot GetSnapshot();
        public abstract void Dispose();

        public Blockchain Blockchain { get; set; }
    }
}