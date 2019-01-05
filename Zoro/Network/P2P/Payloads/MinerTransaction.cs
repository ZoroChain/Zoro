namespace Zoro.Network.P2P.Payloads
{
    public class MinerTransaction : Transaction
    {
        public MinerTransaction()
            : base(TransactionType.MinerTransaction)
        {
        }
    }
}
