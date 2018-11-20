using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Dispatch.MessageQueues;
using System.Collections;
using System.Linq;

namespace Zoro.IO.Actors
{
    internal abstract class PriorityMailbox : MailboxType, IProducesMessageQueue<PriorityMessageQueue>
    {
        protected enum PriorityState : byte
        {
            Block = 1,
            Consensus = 2,
            Transaction = 4,
        }

        protected PriorityState state = 0;

        public PriorityMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
            InitializeHighPriorityState();
        }

        // 通过JSON配置文件，设定消息的优先级
        private void InitializeHighPriorityState()
        {
            string[] str = Zoro.Settings.Default.HighPriorityMessages;

            if (str.Contains("Block"))
            {
                state |= PriorityState.Block;
            }
            if (str.Contains("Consensus"))
            {
                state |= PriorityState.Consensus;
            }
            if (str.Contains("Transaction"))
            {
                state |= PriorityState.Transaction;
            }
        }

        public override IMessageQueue Create(IActorRef owner, ActorSystem system)
        {
            return new PriorityMessageQueue(ShallDrop, IsHighPriority);
        }

        protected virtual bool IsHighPriority(object message) => false;
        protected virtual bool ShallDrop(object message, IEnumerable queue) => false;
    }
}
