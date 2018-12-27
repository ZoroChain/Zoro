using Akka.Actor;
using System.Collections.Generic;
using Zoro.Network.P2P.Payloads;

namespace Zoro.Network.P2P
{
    class RawTransactionList : UntypedActor
    {
        private class Timer { }

        private ZoroSystem system;
        private List<Transaction> rawtxnList = new List<Transaction>();

        public RawTransactionList(ZoroSystem system)
        {
            this.system = system;
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Timer timer:
                    OnTimer();
                    break;
                case Transaction tx:
                    OnRawTransaction(tx);
                    break;

            }
        }

        private void OnTimer()
        {
            BroadcastRawTransactions();
        }

        private void OnRawTransaction(Transaction tx)
        {
            rawtxnList.Add(tx);

            if (CheckRawTransactions())
                BroadcastRawTransactions();
        }

        private bool CheckRawTransactions()
        {
            if (rawtxnList.Count > RawTransactionPayload.MaxCount)
                return true;

            int size = 0;
            foreach (var tx in rawtxnList)
            {
                size += tx.Size;
                if (size >= RawTransactionPayload.MaxPayloadSize)
                    return true;
            }

            return false;
        }

        private void BroadcastRawTransactions()
        {
            List<UInt256> hashes = new List<UInt256>();

            int size = 0;
            foreach (var tx in rawtxnList.ToArray())
            {
                hashes.Add(tx.Hash);

                rawtxnList.Remove(tx);

                size += tx.Size;                
                if (size >= RawTransactionPayload.MaxPayloadSize)
                    break;
            }

            Context.Parent.Tell(Message.Create("rawinv", InvPayload.Create(InventoryType.TX, hashes.ToArray())));
        }

        public static Props Props(ZoroSystem system)
        {
            return Akka.Actor.Props.Create(() => new RawTransactionList(system));
        }
    }
}
