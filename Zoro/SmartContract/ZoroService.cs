using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Cryptography.ECC;
using Zoro.SmartContract.Services;
using Zoro.Network.P2P.Payloads;
using System;
using System.Text;
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
        private GlobalAssetService globalAssetService;
        private TransferStateService transferStateService;

        public ZoroService(TriggerType trigger, Snapshot snapshot)
            : base(trigger, snapshot)
        {
            appchainService = new AppChainService(this, trigger, snapshot);
            nativeNEP5Service = new NativeNEP5Service(this, trigger, snapshot);
            globalAssetService = new GlobalAssetService(this, trigger, snapshot);
            transferStateService = new TransferStateService(this, trigger, snapshot);

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
            Register("Zoro.Transaction.GetWitnesses", Transaction_GetWitnesses, 200);
            Register("Zoro.InvocationTransaction.GetScript", InvocationTransaction_GetScript, 1);
            Register("Zoro.Witness.GetVerificationScript", Witness_GetVerificationScript, 100);
            Register("Zoro.Attribute.GetUsage", Attribute_GetUsage, 1);
            Register("Zoro.Attribute.GetData", Attribute_GetData, 1);
            Register("Zoro.Account.GetScriptHash", Account_GetScriptHash, 1);
            Register("Zoro.Account.GetVotes", Account_GetVotes, 1);
            Register("Zoro.Account.GetBalance", Account_GetBalance, 1);
            Register("Zoro.Account.IsStandard", Account_IsStandard, 100);
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

            Register("Zoro.AppChain.Create", appchainService.CreateAppChain, 5000 * 1000);
            Register("Zoro.AppChain.ChangeSeedList", appchainService.ChangeValidators, 1000);
            Register("Zoro.AppChain.ChangeValidators", appchainService.ChangeSeedList, 1000);

            Register("Zoro.GlobalAsset.GetPrecision", globalAssetService.GetPrecision, 1);
            Register("Zoro.GlobalAsset.Transfer", globalAssetService.Transfer, 1000);
            Register("Zoro.GlobalAsset.Transfer_App", globalAssetService.Transfer_App, 1000);
            Register("Zoro.GlobalAsset.GetTransferState", globalAssetService.GetTransferState, 100);

            Register("Zoro.TransferState.AssetId", transferStateService.AssetId, 1);
            Register("Zoro.TransferState.From", transferStateService.From, 1);
            Register("Zoro.TransferState.To", transferStateService.To, 1);
            Register("Zoro.TransferState.Value", transferStateService.Value, 1);

            Register("Zoro.NativeNEP5.Create", nativeNEP5Service.Create, 1000 * 1000);
            Register("Zoro.NativeNEP5.Call", nativeNEP5Service.Call);
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

        public long GetPrice(uint hash, ExecutionEngine engine)
        {
            long price = base.GetPrice(hash);
            if (price > 0)
                return price;

            if (hash == NativeNEP5Service.SysCall_MethodHash)
            {
                price = NativeNEP5Service.GetPrice(engine);
            }

            return price;
        }
    }
}
