using Akka.Actor;
using Zoro.IO;
using Zoro.Plugins;
using Zoro.Persistence;
using Zoro.Cryptography;
using Zoro.SmartContract;
using Zoro.Network.P2P.Payloads;
using Neo.VM;
using System.Linq;
using System.Collections.Generic;

namespace Zoro.Ledger
{
    class BlockPersistor : UntypedActor
    {
        private ZoroSystem system;
        private Blockchain blockchain;

        public BlockPersistor(ZoroSystem system, Blockchain blockchain)
        {
            this.system = system;
            this.blockchain = blockchain;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Block block:
                    Persist(block);
                    Sender.Tell(new Blockchain.PersistCompleted { Block = block });
                    break;
            }
        }

        private void Persist(Block block)
        {
            if (system.Consensus == null)
                blockchain.Log($"Persist Block:{block.Index}, tx:{block.Transactions.Length}");

            using (Snapshot snapshot = blockchain.GetSnapshot())
            {
                Fixed8 sysfeeAmount = Fixed8.Zero;
                snapshot.PersistingBlock = block;
                foreach (Transaction tx in block.Transactions)
                {
                    // 先预扣手续费，如果余额不够，不执行交易
                    if (PrepaySystemFee(snapshot, tx))
                    {
                        sysfeeAmount += PersistTransaction(block, snapshot, tx);
                    }
                    else
                    {
                        blockchain.Log($"Not enough money to pay transaction fee, block:{block.Index}, tx:{tx.Hash}, fee:{tx.SystemFee}", LogLevel.Warning);
                    }
                }

                snapshot.Blocks.Add(block.Hash, new BlockState
                {
                    SystemFeeAmount = snapshot.GetSysFeeAmount(block.PrevHash) + sysfeeAmount.GetData(),
                    TrimmedBlock = block.Trim()
                });

                if (block.Index > 0 && sysfeeAmount > Fixed8.Zero)
                {
                    blockchain.BCPNativeNEP5.AddBalance(snapshot, block.Transactions[0].GetAccountScriptHash(snapshot), sysfeeAmount);
                }

                snapshot.BlockHashIndex.GetAndChange().Hash = block.Hash;
                snapshot.BlockHashIndex.GetAndChange().Index = block.Index;
                if (block.Index == blockchain.HeaderHeight + 1)
                {
                    snapshot.HeaderHashIndex.GetAndChange().Hash = block.Hash;
                    snapshot.HeaderHashIndex.GetAndChange().Index = block.Index;
                }
                foreach (IPersistencePlugin plugin in PluginManager.PersistencePlugins)
                    plugin.OnPersist(snapshot);

                //if (system.Consensus == null)
                //    blockchain.Log($"Commit Snapshot:{block.Index}, tx:{block.Transactions.Length}");

                snapshot.Commit();
            }
        }

        private Fixed8 PersistTransaction(Block block, Snapshot snapshot, Transaction tx)
        {
            Fixed8 sysfee = tx.SystemFee;

            snapshot.Transactions.Add(tx.Hash, new TransactionState
            {
                BlockIndex = block.Index,
                Transaction = tx
            });
            List<ApplicationExecutionResult> execution_results = new List<ApplicationExecutionResult>();
            switch (tx)
            {
#pragma warning disable CS0612
                case RegisterTransaction tx_register:
                    snapshot.Assets.Add(tx.Hash, new AssetState
                    {
                        AssetId = tx_register.Hash,
                        AssetType = tx_register.AssetType,
                        Name = tx_register.Name,
                        FullName = tx_register.FullName,
                        Amount = tx_register.Amount,
                        Available = Fixed8.Zero,
                        Precision = tx_register.Precision,
                        Fee = Fixed8.Zero,
                        FeeAddress = new UInt160(),
                        Owner = tx_register.Owner,
                        Admin = tx_register.Admin,
                        Issuer = tx_register.Admin,
                        BlockIndex = block.Index,
                        IsFrozen = false
                    });
                    break;
#pragma warning restore CS0612
                case IssueTransaction tx_issue:
                    // 只能在根链上发行流通BCP
                    // 暂时不做限制，等实现跨链兑换BCP后再限制
                    //if (tx_issue.AssetId != UtilityToken.Hash || ChainHash.Equals(UInt160.Zero)) 
                    {
                        snapshot.Assets.GetAndChange(tx_issue.AssetId).Available += tx_issue.Value;
                        AccountState account = snapshot.Accounts.GetAndChange(tx_issue.Address, () => new AccountState(tx_issue.Address));
                        if (account.Balances.ContainsKey(tx_issue.AssetId))
                            account.Balances[tx_issue.AssetId] += tx_issue.Value;
                        else
                            account.Balances[tx_issue.AssetId] = tx_issue.Value;
                    }
                    break;
                case ContractTransaction tx_contract:
                    NativeNEP5 nativeNEP5 = blockchain.GetNativeNEP5(tx_contract.AssetId);
                    nativeNEP5.Transfer(snapshot, tx_contract.From, tx_contract.To, tx_contract.Value);
                    break;
                case InvocationTransaction tx_invocation:
                    using (ApplicationEngine engine = new ApplicationEngine(TriggerType.Application, tx_invocation, snapshot.Clone(), tx_invocation.GasLimit))
                    {
                        engine.LoadScript(tx_invocation.Script);
                        if (engine.Execute())
                        {
                            engine.Service.Commit();
                        }
                        execution_results.Add(new ApplicationExecutionResult
                        {
                            Trigger = TriggerType.Application,
                            ScriptHash = tx_invocation.Script.ToScriptHash(),
                            VMState = engine.State,
                            GasConsumed = engine.GasConsumed,
                            Stack = engine.ResultStack.ToArray(),
                            Notifications = engine.Service.Notifications.ToArray()
                        });

                        // 如果在GAS足够的情况下，脚本发生异常中断，需要退回手续费
                        if (engine.State.HasFlag(VMState.FAULT) && engine.GasConsumed <= tx_invocation.GasLimit)
                        {
                            sysfee = Fixed8.Zero;
                        }
                        else
                        {
                            //按实际消耗的GAS，计算需要的手续费
                            sysfee = tx_invocation.GasPrice * engine.GasConsumed;
                        }

                        // 退回多扣的手续费
                        blockchain.BCPNativeNEP5.AddBalance(snapshot, tx.GetAccountScriptHash(snapshot), tx.SystemFee - sysfee);
                    }
                    break;
            }

            if (execution_results.Count > 0)
            {
                blockchain.Distribute(new Blockchain.ApplicationExecuted
                {
                    Transaction = tx,
                    ExecutionResults = execution_results.ToArray()
                });
            }

            return sysfee;
        }

        private bool PrepaySystemFee(Snapshot snapshot, Transaction tx)
        {
            if (tx.Type == TransactionType.MinerTransaction) return true;
            if (tx.SystemFee <= Fixed8.Zero) return true;

            UInt160 scriptHash = tx.GetAccountScriptHash(snapshot);

            return blockchain.BCPNativeNEP5.SubBalance(snapshot, scriptHash, tx.SystemFee);
        }

        public static Props Props(ZoroSystem system, Blockchain blockchain)
        {
            return Akka.Actor.Props.Create(() => new BlockPersistor(system, blockchain));
        }
    }
}
