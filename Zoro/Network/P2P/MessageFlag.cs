using System;

namespace Zoro.Network.P2P
{
    [Flags]
    internal enum MessageFlag : byte
    {
        None = 0x00,
        HighPriority = 0x01,
        ShallDrop = 0x02,
        IsSingle = 0x04,
    }
}
