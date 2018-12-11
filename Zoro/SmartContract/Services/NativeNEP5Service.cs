using Zoro.Ledger;
using Zoro.Persistence;
using Neo.VM;

namespace Zoro.SmartContract.Services
{
    class NativeNEP5Service
    {
        protected readonly StandardService Service;
        protected readonly TriggerType Trigger;
        protected readonly Snapshot Snapshot;

        public NativeNEP5Service(TriggerType trigger, Snapshot snapshot)
        {
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

        public bool Register(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            return Snapshot.Blockchain.RegisterNativeNEP5(hash);
        }

        public bool Name(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push(asset.Name);
            return true;
        }

        public bool Symbol(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push(asset.FullName);
            return true;
        }

        public bool Decimals(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Snapshot.Assets.TryGet(hash);
            if (asset == null) return false;
            engine.CurrentContext.EvaluationStack.Push((uint)asset.Precision);
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

            UInt256 hash = new UInt256(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5 nativeNEP5 = Snapshot.Blockchain.GetNativeNEP5(hash);
            if (nativeNEP5 == null) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (!Service.CheckWitness(engine, from))
                return false;

            bool result = nativeNEP5.Transfer(Snapshot, from, to, value);
            return result;
        }
    }
}
