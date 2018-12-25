using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Network.P2P.Payloads;
using Neo.VM;

namespace Zoro.SmartContract.Services
{
    class GlobalAssetService
    {
        protected readonly ZoroService Service;
        protected readonly TriggerType Trigger;
        protected readonly Snapshot Snapshot;

        public GlobalAssetService(ZoroService service, TriggerType trigger, Snapshot snapshot)
        {
            Service = service;
            Trigger = trigger;
            Snapshot = snapshot;
        }

        public bool GetName(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(assetId);
            if (asset == null) return false;

            engine.CurrentContext.EvaluationStack.Push(asset.GetName());
            return true;
        }

        public bool GetFullName(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(assetId);
            if (asset == null) return false;

            engine.CurrentContext.EvaluationStack.Push(asset.GetFullName());
            return true;
        }

        public bool GetAmount(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(assetId);
            if (asset == null) return false;

            engine.CurrentContext.EvaluationStack.Push(asset.Amount.GetData());
            return true;
        }

        public bool GetPrecision(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(assetId);
            if (asset == null) return false;

            engine.CurrentContext.EvaluationStack.Push((int)asset.Precision);
            return true;
        }

        public bool BalanceOf(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            GlobalAsset asset = Snapshot.Blockchain.GetGlobalAsset(assetId);
            if (asset == null) return false;

            UInt160 address = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 balance = asset.BalanceOf(address);

            engine.CurrentContext.EvaluationStack.Push(balance.GetData());
            return true;
        }

        public bool Transfer(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            GlobalAsset asset = Snapshot.Blockchain.GetGlobalAsset(assetId);
            if (asset == null) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (!Service.CheckWitness(engine, from))
                return false;

            //禁止跳板调用、入口脚本不是当前执行脚本说明是跳板调用
            if (engine.EntryContext.ScriptHash != engine.CurrentContext.ScriptHash)
                return false;

            bool result = asset.Transfer(Snapshot, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                    SaveTransferState(Snapshot, assetId, tx.Hash, from, to, value);

                Service.AddTransferNotification(engine, assetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        public bool Transfer_App(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            GlobalAsset asset = Snapshot.Blockchain.GetGlobalAsset(assetId);
            if (asset == null) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (from != new UInt160(engine.CurrentContext.ScriptHash))
                return false;

            bool result = asset.Transfer(Snapshot, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                    SaveTransferState(Snapshot, assetId, tx.Hash, from, to, value);

                Service.AddTransferNotification(engine, assetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private void SaveTransferState(Snapshot snapshot, UInt256 TransactionHash, UInt256 assetId, UInt160 from, UInt160 to, Fixed8 value)
        {
            snapshot.Transfers.GetAndChange(TransactionHash, () => new TransferState
            {
                AssetId = assetId,
                Value = value,
                From = from,
                To = to
            });
        }

        public bool GetTransferState(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 transactionHash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            TransferState state = Snapshot.Transfers.TryGet(transactionHash);
            if (state == null) return false;

            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));
            return true;
        }
    }
}
