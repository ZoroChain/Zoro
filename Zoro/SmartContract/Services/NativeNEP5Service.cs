using Zoro.Cryptography.ECC;
using Zoro.Ledger;
using Zoro.Persistence;
using Neo.VM;
using System;
using System.Net;
using System.Text;

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
            engine.CurrentContext.EvaluationStack.Push(asset.Name);
            return true;
        }

        public bool Decimals(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            return true;
        }

        public bool TotalSupply(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            return true;
        }

        public bool BalanceOf(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            return true;
        }

        public bool Transfer(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            return true;
        }
    }
}
