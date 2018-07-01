using Neo.VM;
using System;

namespace Zoro.SmartContract.Enumerators
{
    internal interface IEnumerator : IDisposable, IInteropInterface
    {
        bool Next();
        StackItem Value();
    }
}
