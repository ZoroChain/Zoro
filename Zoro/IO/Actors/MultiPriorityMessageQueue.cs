using Akka.Actor;
using Akka.Dispatch;
using Akka.Dispatch.MessageQueues;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Zoro.IO.Actors
{
    internal class MultiPriorityMessageQueue : IMessageQueue, IUnboundedMessageQueueSemantics
    {
        private readonly ConcurrentQueue<Envelope>[] queues;
        private readonly Func<object, IEnumerable, bool> dropper;
        private readonly Func<object, int> priority_generator;
        private int queue_num = 0;
        private int idle = 1;

        public bool HasMessages => queues.Any(p => !p.IsEmpty);
        public int Count => queues.Sum(p => p.Count());

        public MultiPriorityMessageQueue(Func<object, IEnumerable, bool> dropper, Func<object, int> priority_generator, int queue_num)
        {
            this.dropper = dropper;
            this.priority_generator = priority_generator;
            this.queues = new ConcurrentQueue<Envelope>[queue_num];
            this.queue_num = queue_num;

            for (int i = 0;i < queue_num;i ++)
                this.queues[i] = new ConcurrentQueue<Envelope>();
        }

        public void CleanUp(IActorRef owner, IMessageQueue deadletters)
        {
        }

        public void Enqueue(IActorRef receiver, Envelope envelope)
        {
            Interlocked.Increment(ref idle);
            if (envelope.Message is Idle) return;

            int priority = priority_generator(envelope.Message);
            ConcurrentQueue<Envelope> queue = queues[priority];

            if (dropper(envelope.Message, queue.Select(p => p.Message)))
                return;
            queue.Enqueue(envelope);
        }

        public bool TryDequeue(out Envelope envelope)
        {
            for (int i = queue_num - 1; i >= 0; i--)
            {
                if (queues[i].TryDequeue(out envelope)) return true;
            }

            if (Interlocked.Exchange(ref idle, 0) > 0)
            {
                envelope = new Envelope(Idle.Instance, ActorRefs.NoSender);
                return true;
            }
            return false;
        }
    }
}
