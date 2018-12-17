using Neo.VM;
using System;

namespace Zoro.SmartContract
{
    public class NotifyEventArgs : EventArgs
    {
        public IScriptContainer ScriptContainer { get; }
        public UIntBase ScriptHash { get; }
        public StackItem State { get; }

        public NotifyEventArgs(IScriptContainer container, UIntBase script_hash, StackItem state)
        {
            this.ScriptContainer = container;
            this.ScriptHash = script_hash;
            this.State = state;
        }
    }
}
