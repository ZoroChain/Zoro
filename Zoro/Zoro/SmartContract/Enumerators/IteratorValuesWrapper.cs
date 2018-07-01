using Zoro.SmartContract.Iterators;
using Neo.VM;

namespace Zoro.SmartContract.Enumerators
{
    internal class IteratorValuesWrapper : IEnumerator
    {
        private readonly IIterator iterator;

        public IteratorValuesWrapper(IIterator iterator)
        {
            this.iterator = iterator;
        }

        public void Dispose()
        {
            iterator.Dispose();
        }

        public bool Next()
        {
            return iterator.Next();
        }

        public StackItem Value()
        {
            return iterator.Value();
        }
    }
}
