using Zoro.SmartContract.Enumerators;
using Neo.VM;

namespace Zoro.SmartContract.Iterators
{
    internal interface IIterator : IEnumerator
    {
        StackItem Key();
    }
}
