﻿namespace Zoro.Network.P2P.Payloads
{
    public enum AssetType : byte
    {
        CreditFlag = 0x40,
        DutyFlag = 0x80,

        Currency = 0x08,
        Share = DutyFlag | 0x10,
        Invoice = DutyFlag | 0x18,
        Token = CreditFlag | 0x20,
    }
}
