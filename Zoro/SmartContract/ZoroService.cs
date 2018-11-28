using Zoro.Cryptography.ECC;
using Zoro.Ledger;
using Zoro.Persistence;
using Neo.VM;
using System;
using System.Net;
using System.Text;

namespace Zoro.SmartContract
{
    public class ZoroService : NeoService
    {
        public ZoroService(TriggerType trigger, Snapshot snapshot)
            : base(trigger, snapshot)
        {
            Register("Zoro.Runtime.GetTrigger", Runtime_GetTrigger, 1);
            Register("Zoro.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            Register("Zoro.Runtime.Notify", Runtime_Notify, 1);
            Register("Zoro.Runtime.Log", Runtime_Log, 1);
            Register("Zoro.Runtime.GetTime", Runtime_GetTime, 1);
            Register("Zoro.Runtime.Serialize", Runtime_Serialize, 1);
            Register("Zoro.Runtime.Deserialize", Runtime_Deserialize, 1);
            Register("Zoro.Blockchain.GetHeight", Blockchain_GetHeight, 1);
            Register("Zoro.Blockchain.GetHeader", Blockchain_GetHeader, 100);
            Register("Zoro.Blockchain.GetBlock", Blockchain_GetBlock, 200);
            Register("Zoro.Blockchain.GetTransaction", Blockchain_GetTransaction, 100);
            Register("Zoro.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight, 100);
            Register("Zoro.Blockchain.GetAccount", Blockchain_GetAccount, 100);
            Register("Zoro.Blockchain.GetValidators", Blockchain_GetValidators, 200);
            Register("Zoro.Blockchain.GetAsset", Blockchain_GetAsset, 100);
            Register("Zoro.Blockchain.GetContract", Blockchain_GetContract, 100);
            Register("Zoro.Header.GetHash", Header_GetHash, 1);
            Register("Zoro.Header.GetVersion", Header_GetVersion, 1);
            Register("Zoro.Header.GetPrevHash", Header_GetPrevHash, 1);
            Register("Zoro.Header.GetMerkleRoot", Header_GetMerkleRoot, 1);
            Register("Zoro.Header.GetTimestamp", Header_GetTimestamp, 1);
            Register("Zoro.Header.GetIndex", Header_GetIndex, 1);
            Register("Zoro.Header.GetConsensusData", Header_GetConsensusData, 1);
            Register("Zoro.Header.GetNextConsensus", Header_GetNextConsensus, 1);
            Register("Zoro.Block.GetTransactionCount", Block_GetTransactionCount, 1);
            Register("Zoro.Block.GetTransactions", Block_GetTransactions, 1);
            Register("Zoro.Block.GetTransaction", Block_GetTransaction, 1);
            Register("Zoro.Transaction.GetHash", Transaction_GetHash, 1);
            Register("Zoro.Transaction.GetType", Transaction_GetType, 1);
            Register("Zoro.Transaction.GetAttributes", Transaction_GetAttributes, 1);
            Register("Zoro.Transaction.GetInputs", Transaction_GetInputs, 1);
            Register("Zoro.Transaction.GetOutputs", Transaction_GetOutputs, 1);
            Register("Zoro.Transaction.GetReferences", Transaction_GetReferences, 200);
            Register("Zoro.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins, 200);
            Register("Zoro.Transaction.GetWitnesses", Transaction_GetWitnesses, 200);
            Register("Zoro.InvocationTransaction.GetScript", InvocationTransaction_GetScript, 1);
            Register("Zoro.Witness.GetVerificationScript", Witness_GetVerificationScript, 100);
            Register("Zoro.Attribute.GetUsage", Attribute_GetUsage, 1);
            Register("Zoro.Attribute.GetData", Attribute_GetData, 1);
            Register("Zoro.Input.GetHash", Input_GetHash, 1);
            Register("Zoro.Input.GetIndex", Input_GetIndex, 1);
            Register("Zoro.Output.GetAssetId", Output_GetAssetId, 1);
            Register("Zoro.Output.GetValue", Output_GetValue, 1);
            Register("Zoro.Output.GetScriptHash", Output_GetScriptHash, 1);
            Register("Zoro.Account.GetScriptHash", Account_GetScriptHash, 1);
            Register("Zoro.Account.GetVotes", Account_GetVotes, 1);
            Register("Zoro.Account.GetBalance", Account_GetBalance, 1);
            Register("Zoro.Account.IsStandard", Account_IsStandard, 100);
            Register("Zoro.Asset.Create", Asset_Create);
            Register("Zoro.Asset.Renew", Asset_Renew);
            Register("Zoro.Asset.GetAssetId", Asset_GetAssetId, 1);
            Register("Zoro.Asset.GetAssetType", Asset_GetAssetType, 1);
            Register("Zoro.Asset.GetAmount", Asset_GetAmount, 1);
            Register("Zoro.Asset.GetAvailable", Asset_GetAvailable, 1);
            Register("Zoro.Asset.GetPrecision", Asset_GetPrecision, 1);
            Register("Zoro.Asset.GetOwner", Asset_GetOwner, 1);
            Register("Zoro.Asset.GetAdmin", Asset_GetAdmin, 1);
            Register("Zoro.Asset.GetIssuer", Asset_GetIssuer, 1);
            Register("Zoro.Contract.Create", Contract_Create);
            Register("Zoro.Contract.Migrate", Contract_Migrate);
            Register("Zoro.Contract.Destroy", Contract_Destroy, 1);
            Register("Zoro.Contract.GetScript", Contract_GetScript, 1);
            Register("Zoro.Contract.IsPayable", Contract_IsPayable, 1);
            Register("Zoro.Contract.GetStorageContext", Contract_GetStorageContext, 1);
            Register("Zoro.Storage.GetContext", Storage_GetContext, 1);
            Register("Zoro.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext, 1);
            Register("Zoro.Storage.Get", Storage_Get, 100);
            Register("Zoro.Storage.Put", Storage_Put);
            Register("Zoro.Storage.Delete", Storage_Delete, 100);
            Register("Zoro.Storage.Find", Storage_Find, 1);
            Register("Zoro.StorageContext.AsReadOnly", StorageContext_AsReadOnly, 1);
            Register("Zoro.Enumerator.Create", Enumerator_Create, 1);
            Register("Zoro.Enumerator.Next", Enumerator_Next, 1);
            Register("Zoro.Enumerator.Value", Enumerator_Value, 1);
            Register("Zoro.Enumerator.Concat", Enumerator_Concat, 1);
            Register("Zoro.Iterator.Create", Iterator_Create, 1);
            Register("Zoro.Iterator.Key", Iterator_Key, 1);
            Register("Zoro.Iterator.Keys", Iterator_Keys, 1);
            Register("Zoro.Iterator.Values", Iterator_Values, 1);

            #region Aliases
            Register("Zoro.Iterator.Next", Enumerator_Next, 1);
            Register("Zoro.Iterator.Value", Enumerator_Value, 1);
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

                int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                // 种子节点的数量不能为零
                if (seedCount <= 0)
                    return false;

                string[] seedList = new string[seedCount];
                for (int i = 0; i < seedCount; i++)
                {
                    seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
                }

                // 判断输入的种子节点地址是否有效
                if (!CheckSeedList(seedList, seedCount))
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

                // 判断输入的共识节点字符串格式是否有效
                if (!CheckValidators(validators, validatorCount))
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
            
            // 判断输入的共识节点字符串格式是否有效
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

            // 只有应用链的所有者有权限更改种子节点
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

            // 判断输入的种子节点地址是否有效
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

        // 检查输入的种子节点是否有效
        private bool CheckSeedList(string[] seedList, int count)
        {
            // 检查输入的种子节点是否重复
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

            // 检查输入的种子节点IP地址是否有效
            foreach (var ipaddress in seedList)
            {
                if (!CheckIPAddress(ipaddress))
                {
                    return false;
                }
            }

            return true;
        }

        // 检查IP地址是否有效
        private bool CheckIPAddress(string ipaddress)
        {            
            string[] p = ipaddress.Split(':');
            if (p.Length < 2)
                return false;

            IPEndPoint seed;
            try
            {
                seed = Zoro.Helper.GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
            }
            catch (AggregateException)
            {
                return false;
            }

            return true;
        }
    }
}
