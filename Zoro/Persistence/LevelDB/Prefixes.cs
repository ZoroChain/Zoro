﻿namespace Zoro.Persistence.LevelDB
{
    internal static class Prefixes
    {
        public const byte DATA_Block = 0x01;
        public const byte DATA_Transaction = 0x02;

        public const byte ST_Account = 0x40;
        public const byte ST_Appchain = 0x41;
        public const byte ST_Transfer = 0x42;
        public const byte ST_Coin = 0x44;
        public const byte ST_SpentCoin = 0x45;
        public const byte ST_Validator = 0x48;
        public const byte ST_Asset = 0x4c;
        public const byte ST_Contract = 0x50;
        public const byte ST_Storage = 0x70;

        public const byte IX_HeaderHashList = 0x80;
        public const byte IX_ValidatorsCount = 0x90;
        public const byte IX_AppChainState = 0xa0;
        public const byte IX_CurrentBlock = 0xc0;
        public const byte IX_CurrentHeader = 0xc1;

        public const byte SYS_Version = 0xf0;
    }
}
