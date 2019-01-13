using Akka.Actor;
using System;
using System.Linq;
using System.Collections.Generic;
using Zoro.Network.P2P.Payloads;

namespace Zoro.Network.P2P
{
    // 缓存新收到的交易，按策略批量转发
    class RawTransactionList : UntypedActor
    {
        private class Timer { }

        private ZoroSystem system;
        private List<Transaction> rawtxnList = new List<Transaction>();

        private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);
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

        // 定时器触发时，立刻广播缓存的所有的交易
        private void OnTimer()
        {
            BroadcastRawTransactions();
        }

        private void OnRawTransaction(Transaction tx)
        {
            // 缓存交易数据
            rawtxnList.Add(tx);

            // 如果缓存的交易数量或大小超过设定的上限，则立刻广播缓存的所有交易
            if (CheckRawTransactions())
                BroadcastRawTransactions();
        }

        // 判断缓存队列中的交易数据是否需要被广播
        private bool CheckRawTransactions()
        {
            // 数量超过上限
            if (rawtxnList.Count >= InvPayload.MaxHashesCount)
                return true;
            
            int size = 0;
            foreach (var tx in rawtxnList)
            {
                size += tx.Size;

                // 大小超过上限
                if (size >= RawTransactionPayload.MaxPayloadSize)
                    return true;
            }

            return false;
        }

        // 广播并清空缓存队列中的交易数据
        private void BroadcastRawTransactions()
        {
            if (rawtxnList.Count == 0)
                return;

            // 控制每组消息里的交易数量，向远程节点发送交易的清单
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, rawtxnList.Select(p => p.Hash).ToArray()))
                system.LocalNode.Tell(Message.Create(MessageType.Inv, payload));

            // 清空队列
            rawtxnList.Clear();
        }

        protected override void PostStop()
        {
            timer.CancelIfNotNull();
            base.PostStop();
        }

        public static Props Props(ZoroSystem system)
        {
            return Akka.Actor.Props.Create(() => new RawTransactionList(system));
        }
    }
}
