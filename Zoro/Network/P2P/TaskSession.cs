using Akka.Actor;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;

namespace Zoro.Network.P2P
{
    internal class TaskSession
    {
        internal class Task
        {
            public InventoryType Type;
            public DateTime BeginTime;
        }

        public readonly IActorRef RemoteNode;
        public readonly VersionPayload Version;
        public readonly Dictionary<UInt256, Task> Tasks = new Dictionary<UInt256, Task>();
        public readonly HashSet<UInt256> AvailableTasks = new HashSet<UInt256>();

        public bool HasTask => Tasks.Count > 0;

        public uint Height = 0;
        public uint Latency = 0;
        public uint Timeout = 0;
        public uint SyncBlockTasks = 0;
        public bool HasHeaderTask = false;

        public TaskSession(IActorRef node, VersionPayload version)
        {
            this.RemoteNode = node;
            this.Version = version;
            this.Height = version.StartHeight;
        }
    }
}
