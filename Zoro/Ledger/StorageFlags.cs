using System;

namespace Zoro.Ledger
{
    [Flags]
    public enum StorageFlags : byte
    {
        None = 0,
        Constant = 0x01
    }
}
