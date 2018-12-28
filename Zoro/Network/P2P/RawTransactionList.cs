using Akka.Actor;
using System;
using System.Linq;
using System.Collections.Generic;
using Zoro.Network.P2P.Payloads;

namespace Zoro.Network.P2P
{
    class RawTransactionList : UntypedActor
    {
        private class Timer { }

        private ZoroSystem system;
        private List<Transaction> rawtxnList = new List<Transaction>();

        private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(500);
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

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
            if (rawtxnList.Count >= InvPayload.MaxHashesCount)
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
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, rawtxnList.Select(p => p.Hash).ToArray()))
                system.LocalNode.Tell(Message.Create("rawinv", payload));
        }

        public static Props Props(ZoroSystem system)
        {
            return Akka.Actor.Props.Create(() => new RawTransactionList(system));
        }
    }
}
