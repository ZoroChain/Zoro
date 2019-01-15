using Neo.VM;
using Neo.VM.Types;
using Zoro.SmartContract.NativeNEP5;

namespace Zoro.SmartContract.Services
{
    class TransferLogServices
    {
        public bool GetFrom(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferLog transferLog = _interface.GetInterface<TransferLog>();
                if (transferLog == null) return false;
                engine.CurrentContext.EvaluationStack.Push(transferLog.From.ToArray());
                return true;
            }
            return false;
        }

        public bool GetTo(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferLog transferLog = _interface.GetInterface<TransferLog>();
                if (transferLog == null) return false;
                engine.CurrentContext.EvaluationStack.Push(transferLog.To.ToArray());
                return true;
            }
            return false;
        }

        public bool GetValue(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransferLog transferLog = _interface.GetInterface<TransferLog>();
                if (transferLog == null) return false;
                engine.CurrentContext.EvaluationStack.Push((long)transferLog.Value);
                return true;
            }
            return false;
        }
    }
}
