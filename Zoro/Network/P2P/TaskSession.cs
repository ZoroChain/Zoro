using Akka.Actor;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;

namespace Zoro.Network.P2P
{
    internal class RequestTask
    {
        public InventoryType Type;
        public DateTime BeginTime;
    }

    internal class TaskSession
    {
        public readonly IActorRef RemoteNode;
        public readonly VersionPayload Version;
        public readonly Dictionary<UInt256, RequestTask> Tasks = new Dictionary<UInt256, RequestTask>();
        public readonly HashSet<UInt256> AvailableTasks = new HashSet<UInt256>();

        public bool HasTask => Tasks.Count > 0;
        public bool HeaderTask => Tasks.ContainsKey(UInt256.Zero);

        public TaskSession(IActorRef node, VersionPayload version)
        {
            this.RemoteNode = node;
            this.Version = version;
        }
    }
}
