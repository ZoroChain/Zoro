using Neo.VM;
using System;

namespace Zoro.SmartContract.Enumerators
{
    internal interface IEnumerator : IDisposable
    {
        bool Next();
        StackItem Value();
    }
}
