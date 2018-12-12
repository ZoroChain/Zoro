using Zoro.Cryptography.ECC;
using Zoro.IO.Caching;
using Zoro.IO.Wrappers;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zoro.Persistence
{
    public abstract class Snapshot : IDisposable, IPersistence, IScriptTable
    {
        public Block PersistingBlock { get; internal set; }
        public abstract DataCache<UInt256, BlockState> Blocks { get; }
        public abstract DataCache<UInt256, TransactionState> Transactions { get; }
        public abstract DataCache<UInt160, AccountState> Accounts { get; }
        public abstract DataCache<UInt160, AppChainState> AppChains { get; }
        public abstract DataCache<UInt256, UnspentCoinState> UnspentCoins { get; }
        public abstract DataCache<UInt256, SpentCoinState> SpentCoins { get; }
        public abstract DataCache<UInt256, AssetState> Assets { get; }
        public abstract DataCache<UInt160, ContractState> Contracts { get; }
        public abstract DataCache<StorageKey, StorageItem> Storages { get; }
        public abstract DataCache<UInt32Wrapper, HeaderHashList> HeaderHashList { get; }
        public abstract MetaDataCache<HashIndexState> BlockHashIndex { get; }
        public abstract MetaDataCache<HashIndexState> HeaderHashIndex { get; }
        public abstract MetaDataCache<AppChainState> AppChainState { get; }

        public uint Height => BlockHashIndex.Get().Index;
        public uint HeaderHeight => HeaderHashIndex.Get().Index;
        public UInt256 CurrentBlockHash => BlockHashIndex.Get().Hash;
        public UInt256 CurrentHeaderHash => HeaderHashIndex.Get().Hash;

        public Blockchain Blockchain { get; }

        public Snapshot(Blockchain blockchain)
        {
            Blockchain = blockchain;
        }        

        public Snapshot Clone()
        {
            return new CloneSnapshot(this, Blockchain);
        }

        public virtual void Commit()
        {
            Accounts.DeleteWhere((k, v) => !v.IsFrozen && v.Votes.Length == 0 && v.Balances.All(p => p.Value <= Fixed8.Zero));
            UnspentCoins.DeleteWhere((k, v) => v.Items.All(p => p.HasFlag(CoinState.Spent)));
            SpentCoins.DeleteWhere((k, v) => v.Items.Count == 0);
            Blocks.Commit();
            Transactions.Commit();
            Accounts.Commit();
            UnspentCoins.Commit();
            SpentCoins.Commit();
            Assets.Commit();
            Contracts.Commit();
            Storages.Commit();
            HeaderHashList.Commit();
            BlockHashIndex.Commit();
            HeaderHashIndex.Commit();
            AppChains.Commit();
            AppChainState.Commit();
        }

        public virtual void Dispose()
        {
        }

        byte[] IScriptTable.GetScript(byte[] script_hash)
        {
            return Contracts[new UInt160(script_hash)].Script;
        }

        private ECPoint[] _validators = null;
        public ECPoint[] GetValidators()
        {
            if (_validators == null)
            {
                _validators = GetValidators(Enumerable.Empty<Transaction>()).ToArray();
            }
            return _validators;
        }

        public IEnumerable<ECPoint> GetValidators(IEnumerable<Transaction> others)
        {
            ECPoint[] standbyValidators = Blockchain.StandbyValidators;
            Snapshot snapshot = Clone();
            foreach (Transaction tx in others)
            {
                switch (tx)
                {
                    case InvocationTransaction tx_invocation:
                        // 这里只能改变应用链的共识节点
                        if (!Blockchain.ChainHash.Equals(UInt160.Zero))
                        {
                            // 判断脚本里是否调用了更改应用链共识节点的SysCall
                            if (IsSysCallScript(tx_invocation.Script, "Zoro.AppChain.ChangeValidators"))
                            {
                                // 运行脚本，但结果不保存到DB
                                using (ApplicationEngine engine = new ApplicationEngine(TriggerType.Application, tx_invocation, snapshot, tx_invocation.GasLimit, true))
                                {
                                    engine.LoadScript(tx_invocation.Script);
                                    if (engine.Execute())
                                    {
                                        // 获取脚本的返回值
                                        if (engine.ResultStack.Peek() is InteropInterface _interface)
                                        {
                                            AppChainState state = _interface.GetInterface<AppChainState>();

                                            // 判断应用链的共识节点是否发生了变化
                                            if (state != null && !state.CompareStandbyValidators(standbyValidators))
                                            {
                                                standbyValidators = state.StandbyValidators;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        break;
                }
            }
            int count = standbyValidators.Length;
            IEnumerable<ECPoint> result;
            HashSet<ECPoint> hashSet = new HashSet<ECPoint>();
            for (int i = 0; i < standbyValidators.Length; i++)
                hashSet.Add(standbyValidators[i]);
            result = hashSet;
            return result.OrderBy(p => p);
        }

        // 判断虚拟机指令里是否调用了特定的SysCall
        private bool IsSysCallScript(byte[] script, string method)
        {
            byte opcode = (byte)OpCode.SYSCALL;
            int len = script.Length;
            for (int i = 0;i < len;i ++)
            {
                if (script[i] == opcode)
                {
                    // 获取字符串的长度
                    int strLen = script[i + 1];

                    // 防止越界
                    if (i + strLen < len - 1) 
                    {
                        string str = Encoding.ASCII.GetString(script, i + 2, strLen);

                        if (str == method)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
