using System;
using System.IO;

namespace Neo.Core
{
    /// <summary>
    /// 合约交易，这是最常用的一种交易
    /// </summary>
    public class ContractTransaction : Transaction
    {
        public ContractTransaction(UInt256 chainhash)
            : base(TransactionType.ContractTransaction, chainhash)
        {
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version != 0) throw new FormatException();
        }
    }
}
