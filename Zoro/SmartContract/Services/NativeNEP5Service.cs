using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Network.P2P.Payloads;
using System.Numerics;
using Neo.VM;
using Neo.VM.Types;
using VMArray = Neo.VM.Types.Array;

namespace Zoro.SmartContract.Services
{
    class NativeNEP5Service
    {
        protected readonly ZoroService Service;
        protected readonly TriggerType Trigger;
        protected readonly Snapshot Snapshot;

        public NativeNEP5Service(ZoroService service, TriggerType trigger, Snapshot snapshot)
        {
            Service = service;
            Trigger = trigger;
            Snapshot = snapshot;
        }

        public bool Retrieve(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5 nativeNEP5 = Snapshot.Blockchain.GetNativeNEP5(hash);
            if (nativeNEP5 == null) return false;
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(nativeNEP5));
            return true;
        }

        public bool Name(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push(asset.FullName);
            return true;
        }

        public bool Symbol(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push(asset.Name);
            return true;
        }

        public bool Decimals(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push((int)asset.Precision);
            return true;
        }

        public bool TotalSupply(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push(asset.Amount.GetData());
            return true;
        }

        public bool BalanceOf(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5 nativeNEP5 = Snapshot.Blockchain.GetNativeNEP5(hash);
            if (nativeNEP5 == null) return false;

            UInt160 address = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 balance = nativeNEP5.BalanceOf(address);
            engine.CurrentContext.EvaluationStack.Push(balance.GetData());
            return true;
        }

        public bool Transfer(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5 nativeNEP5 = Snapshot.Blockchain.GetNativeNEP5(assetId);
            if (nativeNEP5 == null) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (!Service.CheckWitness(engine, from))
                return false;

            //禁止跳板调用、入口脚本不是当前执行脚本说明是跳板调用
            if (engine.EntryContext.ScriptHash != engine.CurrentContext.ScriptHash)
                return false;

            bool result = nativeNEP5.Transfer(Snapshot, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                    SaveTransferState(Snapshot, tx.Hash, from, to, value);

                AddTransferNotification(engine, assetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        public bool Transfer_App(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 assetId = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5 nativeNEP5 = Snapshot.Blockchain.GetNativeNEP5(assetId);
            if (nativeNEP5 == null) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());
            
            if (from != new UInt160(engine.CurrentContext.ScriptHash))
                return false;

            bool result = nativeNEP5.Transfer(Snapshot, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                    SaveTransferState(Snapshot, tx.Hash, from, to, value);

                AddTransferNotification(engine, assetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        public void SaveTransferState(Snapshot snapshot, UInt256 TransactionHash, UInt160 from, UInt160 to, Fixed8 value)
        {
            snapshot.Transfers.GetAndChange(TransactionHash, () => new TransferState
            {
                Value = value,
                From = from,
                To = to
            });
        }

        void AddTransferNotification(ExecutionEngine engine, UInt256 assetId, UInt160 from, UInt160 to, Fixed8 value)
        {
            VMArray array = new VMArray();
            array.Add("transfer");
            array.Add(new ByteArray(from.ToArray()));
            array.Add(new ByteArray(to.ToArray()));
            array.Add(new ByteArray(new BigInteger(value.GetData()).ToByteArray()));

            Service.NativeNEP5_Invoke_Notification(engine, assetId, array);
        }

        public bool GetTransferState(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5 nativeNEP5 = Snapshot.Blockchain.GetNativeNEP5(hash);
            if (nativeNEP5 == null) return false;

            UInt256 transactionHash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            TransferState state = Snapshot.Transfers.TryGet(transactionHash);
            if (state == null) return false;

            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));
            return true;
        }

        public bool TransferState_GetFrom(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferState ts = _interface.GetInterface<TransferState>();
                if (ts == null) return false;
                engine.CurrentContext.EvaluationStack.Push(ts.From.ToArray());
                return true;
            }
            return false;
        }

        public bool TransferState_GetTo(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferState ts = _interface.GetInterface<TransferState>();
                if (ts == null) return false;
                engine.CurrentContext.EvaluationStack.Push(ts.To.ToArray());
                return true;
            }
            return false;
        }

        public bool TransferState_GetValue(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferState ts = _interface.GetInterface<TransferState>();
                if (ts == null) return false;
                engine.CurrentContext.EvaluationStack.Push(ts.Value.GetData());
                return true;
            }
            return false;
        }
    }
}
