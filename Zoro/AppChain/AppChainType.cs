using System;

namespace Zoro.AppChain
{
    [Flags]
    public enum AppChainType : byte
    {
        None = 0x00,
        Private = 0x01,
    }
}
