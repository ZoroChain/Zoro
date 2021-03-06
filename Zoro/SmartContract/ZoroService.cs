﻿using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.SmartContract.Services;
using System.Numerics;
using Neo.VM;
using Neo.VM.Types;
using VMArray = Neo.VM.Types.Array;

namespace Zoro.SmartContract
{
    public class ZoroService : NeoService
    {
        private AppChainService appchainService;
        private NativeNEP5Service nativeNEP5Service;
        private TransferLogServices transferLogService;

        public ZoroService(TriggerType trigger, Snapshot snapshot)
            : base(trigger, snapshot)
        {
            appchainService = new AppChainService(this, trigger, snapshot);
            nativeNEP5Service = new NativeNEP5Service(this, trigger, snapshot);
            transferLogService = new TransferLogServices();

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
            Register("Zoro.Blockchain.GetValidators", Blockchain_GetValidators, 200);
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
            Register("Zoro.Transaction.GetWitnesses", Transaction_GetWitnesses, 200);
            Register("Zoro.InvocationTransaction.GetScript", InvocationTransaction_GetScript, 1);
            Register("Zoro.Witness.GetVerificationScript", Witness_GetVerificationScript, 100);
            Register("Zoro.Attribute.GetUsage", Attribute_GetUsage, 1);
            Register("Zoro.Attribute.GetData", Attribute_GetData, 1);
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
            Register("Zoro.Storage.Delete", Storage_Delete, 10);
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
            Register("Zoro.Iterator.Concat", Iterator_Concat, 1);

            #region Aliases
            Register("Zoro.Iterator.Next", Enumerator_Next, 1);
            Register("Zoro.Iterator.Value", Enumerator_Value, 1);
            #endregion

            Register("Zoro.AppChain.Create", appchainService.CreateAppChain, 1_000_000L);
            Register("Zoro.AppChain.ChangeSeedList", appchainService.ChangeSeedList, 10_000L);
            Register("Zoro.AppChain.ChangeValidators", appchainService.ChangeValidators, 10_000L);

            Register("Zoro.NativeNEP5.Create", nativeNEP5Service.Create, 100_000L);
            Register("Zoro.NativeNEP5.Call", nativeNEP5Service.Call);
            Register("Zoro.NativeNEP5.GetTransferLog", nativeNEP5Service.GetTransferLog, 100);

            Register("Zoro.NativeNEP5.TransferLog.GetFrom", transferLogService.GetFrom, 1);
            Register("Zoro.NativeNEP5.TransferLog.GetTo", transferLogService.GetTo, 1);
            Register("Zoro.NativeNEP5.TransferLog.GetValue", transferLogService.GetValue, 1);
        }

        public void AddTransferNotification(ExecutionEngine engine, UIntBase assetId, UInt160 from, UInt160 to, Fixed8 value)
        {
            VMArray array = new VMArray();
            array.Add("transfer");
            array.Add(new ByteArray(from.ToArray()));
            array.Add(new ByteArray(to.ToArray()));
            array.Add(new ByteArray(new BigInteger(value.GetData()).ToByteArray()));

            NotifyEventArgs notification = new NotifyEventArgs(engine.ScriptContainer, assetId, array);
            InvokeNotification(notification);
        }

        public void AddApproveNotification(ExecutionEngine engine, UIntBase assetId, UInt160 from, UInt160 to, Fixed8 value)
        {
            VMArray array = new VMArray();
            array.Add("approve");
            array.Add(new ByteArray(from.ToArray()));
            array.Add(new ByteArray(to.ToArray()));
            array.Add(new ByteArray(new BigInteger(value.GetData()).ToByteArray()));

            NotifyEventArgs notification = new NotifyEventArgs(engine.ScriptContainer, assetId, array);
            InvokeNotification(notification);
        }

        public long GetPrice(uint api_hash, ExecutionEngine engine)
        {
            long price = base.GetPrice(api_hash);
            if (price > 0)
                return price;

            if (api_hash == NativeNEP5Service.SysCall_MethodHash)
            {
                price = NativeNEP5Service.GetPrice(engine);
            }
            else
            {
                if (IsUnpricedMethod(api_hash, "Contract.Create") || IsUnpricedMethod(api_hash, "Contract.Migrate"))
                {
                    long fee = 10_000L;

                    ContractPropertyState contract_properties = (ContractPropertyState)(byte)engine.CurrentContext.EvaluationStack.Peek(3).GetBigInteger();

                    if (contract_properties.HasFlag(ContractPropertyState.HasStorage))
                    {
                        fee += 40_000L;
                    }
                    if (contract_properties.HasFlag(ContractPropertyState.HasDynamicInvoke))
                    {
                        fee += 50_000L;
                    }
                    return fee;
                }

                if (IsUnpricedMethod(api_hash, "Storage.Put") || IsUnpricedMethod(api_hash, "Storage.PutEx"))
                {
                    return ((engine.CurrentContext.EvaluationStack.Peek(1).GetByteArray().Length + engine.CurrentContext.EvaluationStack.Peek(2).GetByteArray().Length - 1) / 1024 + 1) * 1000;
                }
            }

            return price;
        }
    }
}
