using Neo.VM;
using System;

namespace Zoro.SmartContract
{
    internal class ContainerPlaceholder : StackItem
    {
        public StackItemType Type;
        public int ElementCount;

        public override bool Equals(StackItem other) => throw new NotSupportedException();

        public override bool GetBoolean() => throw new NotImplementedException();

        public override byte[] GetByteArray() => throw new NotSupportedException();
    }
}
