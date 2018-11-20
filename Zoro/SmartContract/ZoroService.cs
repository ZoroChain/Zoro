using Zoro.Cryptography.ECC;
using Zoro.Network.P2P.Payloads;
using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.IO.Caching;
using Neo.VM;
using System.Text;
using Zoro.Plugins;

namespace Zoro.SmartContract
{
    public class ZoroService : NeoService
    {
        public ZoroService(TriggerType trigger, Snapshot snapshot)
            : base(trigger, snapshot)
        {
            Register("Zoro.Runtime.GetTrigger", Runtime_GetTrigger);
            Register("Zoro.Runtime.CheckWitness", Runtime_CheckWitness);
            Register("Zoro.Runtime.Notify", Runtime_Notify);
            Register("Zoro.Runtime.Log", Runtime_Log);
            Register("Zoro.Runtime.GetTime", Runtime_GetTime);
            Register("Zoro.Runtime.Serialize", Runtime_Serialize);
            Register("Zoro.Runtime.Deserialize", Runtime_Deserialize);
            Register("Zoro.Blockchain.GetHeight", Blockchain_GetHeight);
            Register("Zoro.Blockchain.GetHeader", Blockchain_GetHeader);
            Register("Zoro.Blockchain.GetBlock", Blockchain_GetBlock);
            Register("Zoro.Blockchain.GetTransaction", Blockchain_GetTransaction);
            Register("Zoro.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight);
            Register("Zoro.Blockchain.GetAccount", Blockchain_GetAccount);
            Register("Zoro.Blockchain.GetValidators", Blockchain_GetValidators);
            Register("Zoro.Blockchain.GetAsset", Blockchain_GetAsset);
            Register("Zoro.Blockchain.GetContract", Blockchain_GetContract);
            Register("Zoro.Header.GetHash", Header_GetHash);
            Register("Zoro.Header.GetVersion", Header_GetVersion);
            Register("Zoro.Header.GetPrevHash", Header_GetPrevHash);
            Register("Zoro.Header.GetMerkleRoot", Header_GetMerkleRoot);
            Register("Zoro.Header.GetTimestamp", Header_GetTimestamp);
            Register("Zoro.Header.GetIndex", Header_GetIndex);
            Register("Zoro.Header.GetConsensusData", Header_GetConsensusData);
            Register("Zoro.Header.GetNextConsensus", Header_GetNextConsensus);
            Register("Zoro.Block.GetTransactionCount", Block_GetTransactionCount);
            Register("Zoro.Block.GetTransactions", Block_GetTransactions);
            Register("Zoro.Block.GetTransaction", Block_GetTransaction);
            Register("Zoro.Transaction.GetHash", Transaction_GetHash);
            Register("Zoro.Transaction.GetType", Transaction_GetType);
            Register("Zoro.Transaction.GetAttributes", Transaction_GetAttributes);
            Register("Zoro.Transaction.GetInputs", Transaction_GetInputs);
            Register("Zoro.Transaction.GetOutputs", Transaction_GetOutputs);
            Register("Zoro.Transaction.GetReferences", Transaction_GetReferences);
            Register("Zoro.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins);
            Register("Zoro.Transaction.GetWitnesses", Transaction_GetWitnesses);
            Register("Zoro.InvocationTransaction.GetScript", InvocationTransaction_GetScript);
            Register("Zoro.Witness.GetVerificationScript", Witness_GetVerificationScript);
            Register("Zoro.Attribute.GetUsage", Attribute_GetUsage);
            Register("Zoro.Attribute.GetData", Attribute_GetData);
            Register("Zoro.Input.GetHash", Input_GetHash);
            Register("Zoro.Input.GetIndex", Input_GetIndex);
            Register("Zoro.Output.GetAssetId", Output_GetAssetId);
            Register("Zoro.Output.GetValue", Output_GetValue);
            Register("Zoro.Output.GetScriptHash", Output_GetScriptHash);
            Register("Zoro.Account.GetScriptHash", Account_GetScriptHash);
            Register("Zoro.Account.GetVotes", Account_GetVotes);
            Register("Zoro.Account.GetBalance", Account_GetBalance);
            Register("Zoro.Account.IsStandard", Account_IsStandard);
            Register("Zoro.Asset.Create", Asset_Create);
            Register("Zoro.Asset.Renew", Asset_Renew);
            Register("Zoro.Asset.GetAssetId", Asset_GetAssetId);
            Register("Zoro.Asset.GetAssetType", Asset_GetAssetType);
            Register("Zoro.Asset.GetAmount", Asset_GetAmount);
            Register("Zoro.Asset.GetAvailable", Asset_GetAvailable);
            Register("Zoro.Asset.GetPrecision", Asset_GetPrecision);
            Register("Zoro.Asset.GetOwner", Asset_GetOwner);
            Register("Zoro.Asset.GetAdmin", Asset_GetAdmin);
            Register("Zoro.Asset.GetIssuer", Asset_GetIssuer);
            Register("Zoro.Contract.Create", Contract_Create);
            Register("Zoro.Contract.Migrate", Contract_Migrate);
            Register("Zoro.Contract.Destroy", Contract_Destroy);
            Register("Zoro.Contract.GetScript", Contract_GetScript);
            Register("Zoro.Contract.IsPayable", Contract_IsPayable);
            Register("Zoro.Contract.GetStorageContext", Contract_GetStorageContext);
            Register("Zoro.Storage.GetContext", Storage_GetContext);
            Register("Zoro.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext);
            Register("Zoro.Storage.Get", Storage_Get);
            Register("Zoro.Storage.Put", Storage_Put);
            Register("Zoro.Storage.Delete", Storage_Delete);
            Register("Zoro.Storage.Find", Storage_Find);
            Register("Zoro.StorageContext.AsReadOnly", StorageContext_AsReadOnly);
            Register("Zoro.Enumerator.Create", Enumerator_Create);
            Register("Zoro.Enumerator.Next", Enumerator_Next);
            Register("Zoro.Enumerator.Value", Enumerator_Value);
            Register("Zoro.Enumerator.Concat", Enumerator_Concat);
            Register("Zoro.Iterator.Create", Iterator_Create);
            Register("Zoro.Iterator.Key", Iterator_Key);
            Register("Zoro.Iterator.Keys", Iterator_Keys);
            Register("Zoro.Iterator.Values", Iterator_Values);

            #region Aliases
            Register("Zoro.Iterator.Next", Enumerator_Next);
            Register("Zoro.Iterator.Value", Enumerator_Value);
            #endregion

            Register("Zoro.AppChain.Create", AppChain_Create);
            Register("Zoro.AppChain.ChangeSeedList", AppChain_ChangeSeedList);
            Register("Zoro.AppChain.ChangeValidators", AppChain_ChangeValidators);
        }

        private bool AppChain_Create(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;
            try
            {
                // 只能在根链上执行创建应用链的指令
                if (!Snapshot.Blockchain.ChainHash.Equals(UInt160.Zero))
                    return false;

                // 应用链的Hash
                UInt160 hash = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

                // 应用链的名字
                if (engine.CurrentContext.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
                string name = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

                // 应用链的所有者
                ECPoint owner = ECPoint.DecodePoint(engine.CurrentContext.EvaluationStack.Pop().GetByteArray(), ECCurve.Secp256r1);
                if (owner.IsInfinity) return false;

                // 交易的见证人里必须有应用链的所有者
                if (!CheckWitness(engine, owner))
                    return false;

                // 创建时间
                uint timestamp = (uint)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                // 种子节点
                int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                string[] seedList = new string[seedCount];
                for (int i = 0; i < seedCount; i++)
                {
                    seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
                }

                // 共识节点
                int validatorCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                ECPoint[] validators = new ECPoint[validatorCount];
                for (int i = 0; i < validatorCount; i++)
                {
                    validators[i] = ECPoint.DecodePoint(Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray()).HexToBytes(), ECCurve.Secp256r1);
                }

                // 共识节点的数量不能小于四个
                if (validatorCount < 4)
                    return false;

                // 判断输入的共识节点字符串格式是否无效或者重复
                if (!CheckValidators(validators, validatorCount))
                    return false;

                // 种子节点的数量不能为零
                if (seedCount <= 0)
                    return false;

                AppChainState state = Snapshot.AppChains.TryGet(hash);
                if (state == null)
                {
                    state = new AppChainState
                    {
                        Hash = hash,
                        Name = name,
                        Owner = owner,
                        Timestamp = timestamp,
                        SeedList = seedList,
                        StandbyValidators = validators,
                    };

                    // 保存到数据库
                    Snapshot.AppChains.Add(hash, state);

                    // 添加通知事件，等待上链后处理
                    if (Snapshot.PersistingBlock != null)
                        Snapshot.Blockchain.AddAppChainNotification("Create", state);
                }

                // 设置脚本的返回值
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));
            }
            catch
            {
                return false;
            }
            return true;
        }

        private bool AppChain_ChangeValidators(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 chainHash = Snapshot.Blockchain.ChainHash;

            // 只能在应用链上执行更改应用链共识节点的指令
            if (chainHash.Equals(UInt160.Zero))
                return false;

            // 在应用链的数据库里查询应用链状态信息
            AppChainState state = Snapshot.AppChainState.GetAndChange();
            if (state.Hash == null)
                return false;

            // 只有应用链的所有者有权限更换共识节点
            if (!CheckWitness(engine, state.Owner))
                return false;

            int validatorCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

            // 共识节点的数量不能小于四个
            if (validatorCount < 4)
                return false;

            ECPoint[] validators = new ECPoint[validatorCount];
            for (int i = 0; i < validatorCount; i++)
            {
                validators[i] = ECPoint.DecodePoint(Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray()).HexToBytes(), ECCurve.Secp256r1);
            }
            
            // 判断输入的共识节点字符串格式是否无效或者重复
            if (!CheckValidators(validators, validatorCount))
                return false;

            // 将修改保存到应用链的数据库
            state.StandbyValidators = validators;

            // 添加通知事件，等待上链后处理
            if (Snapshot.PersistingBlock != null)
                Snapshot.Blockchain.AddAppChainNotification("ChangeValidators", state);

            // 设置脚本的返回值
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));

            return true;
        }

        private bool AppChain_ChangeSeedList(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 chainHash = Snapshot.Blockchain.ChainHash;

            // 只能在应用链上执行更改应用链共识节点的指令
            if (chainHash.Equals(UInt160.Zero))
                return false;

            // 在应用链的数据库里查询应用链状态信息
            AppChainState state = Snapshot.AppChainState.GetAndChange();
            if (state.Hash == null)
                return false;

            // 只有应用链的所有者有权限更换种子节点
            if (!CheckWitness(engine, state.Owner))
                return false;

            int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

            // 种子节点的数量不能为零
            if (seedCount <= 0)
                return false;

            string[] seedList = new string[seedCount];
            for (int i = 0; i < seedCount; i++)
            {
                seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            }

            // 判断输入的种子节点地址是否重复
            if (!CheckSeedList(seedList, seedCount))
                return false;

            // 把变更保存到应用链的数据库
            state.SeedList = seedList;

            // 添加通知事件，等待上链后处理
            if (Snapshot.PersistingBlock != null)
                Snapshot.Blockchain.AddAppChainNotification("ChangeSeedList", state);

            // 设置脚本的返回值
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));

            return true;
        }

        // 检查输入的共识节点是否无效或重复
        private bool CheckValidators(ECPoint[] validators, int count)
        {
            for (int i = 0;i < count; i ++)
            {
                // 判断有效性
                if (validators[i].IsInfinity)
                    return false;

                // 判断重复
                for (int j = i + 1;j < count; j ++)
                {
                    if (validators[i].Equals(validators[j]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // 检查输入的种子节点是否重复
        private bool CheckSeedList(string[] seedList, int count)
        {
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (seedList[i].Equals(seedList[j]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
