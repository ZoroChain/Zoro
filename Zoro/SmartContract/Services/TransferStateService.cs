using Zoro.Ledger;
using Zoro.Persistence;
using Neo.VM;
using Neo.VM.Types;

namespace Zoro.SmartContract.Services
{
    class TransferStateService
    {
        protected readonly ZoroService Service;
        protected readonly TriggerType Trigger;
        protected readonly Snapshot Snapshot;

        public TransferStateService(ZoroService service, TriggerType trigger, Snapshot snapshot)
        {
            Service = service;
            Trigger = trigger;
            Snapshot = snapshot;
        }

        public bool AssetId(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferState ts = _interface.GetInterface<TransferState>();
                if (ts == null) return false;
                engine.CurrentContext.EvaluationStack.Push(ts.AssetId.ToArray());
                return true;
            }
            return false;
        }

        public bool From(ExecutionEngine engine)
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

        public bool To(ExecutionEngine engine)
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

        public bool Value(ExecutionEngine engine)
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
