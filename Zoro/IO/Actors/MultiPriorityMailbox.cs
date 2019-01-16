using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Dispatch.MessageQueues;
using System.Collections;

namespace Zoro.IO.Actors
{
    internal abstract class MultiPriorityMailbox : MailboxType, IProducesMessageQueue<PriorityMessageQueue>
    {
        protected int numQueues = 1;

        public MultiPriorityMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        public override IMessageQueue Create(IActorRef owner, ActorSystem system)
        {
            return new MultiPriorityMessageQueue(ShallDrop, IsHighPriority, numQueues);
        }

        protected virtual int IsHighPriority(object message) => 0;
        protected virtual bool ShallDrop(object message, IEnumerable queue) => false;
    }
}
