﻿using Zoro.SmartContract;
using Neo.VM;

namespace Zoro.Ledger
{
    public class ApplicationExecutionResult
    {
        public TriggerType Trigger { get; internal set; }
        public UInt160 ScriptHash { get; internal set; }
        public VMState VMState { get; internal set; }
        public Fixed8 GasConsumed { get; internal set; }
        public StackItem[] Stack { get; internal set; }
        public NotifyEventArgs[] Notifications { get; internal set; }
    }
}
