using Neo.VM;

namespace Zoro.SmartContract
{
    internal class StorageContext : IInteropInterface
    {
        public UInt160 ScriptHash;
        public bool IsReadOnly;

        public byte[] ToArray()
        {
            return ScriptHash.ToArray();
        }
    }
}
