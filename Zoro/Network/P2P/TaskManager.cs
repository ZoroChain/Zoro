﻿using Akka.Actor;
using Akka.Configuration;
using Zoro.IO.Actors;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Network.P2P
{
    internal class TaskManager : UntypedActor
    {
        public class Register { public VersionPayload Version; }
        public class NewTask { public InvPayload Payload; }
        public class NewGroupTask { public InvGroupPayload Payload; }
        public class TaskCompleted { public UInt256 Hash; }
        public class HeaderTaskCompleted { }
        public class RestartTasks { public InvGroupPayload Payload; }
        private class Timer { }

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(1);

        private readonly ZoroSystem system;
        private readonly HashSet<UInt256> knownHashes = new HashSet<UInt256>();
        private readonly HashSet<UInt256> globalTasks = new HashSet<UInt256>();
        private readonly Dictionary<IActorRef, TaskSession> sessions = new Dictionary<IActorRef, TaskSession>();
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        private readonly UInt160 chainHash;
        private readonly Blockchain blockchain;

        private bool HeaderTask => sessions.Values.Any(p => p.HeaderTask);

        public TaskManager(ZoroSystem system, UInt160 chainHash)
        {
            this.system = system;
            this.chainHash = chainHash;
            this.blockchain = ZoroSystem.AskBlockchain(chainHash);
        }

        private void OnHeaderTaskCompleted()
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            session.Tasks.Remove(UInt256.Zero);
            RequestTasks(session);
        }

        private void OnNewTask(InvPayload payload)
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            if (payload.Type == InventoryType.TX && blockchain.Height < blockchain.HeaderHeight)
            {
                RequestTasks(session);
                return;
            }
            UInt256 hash = payload.Hash;
            if (knownHashes.Contains(hash))
                return;

            bool isRunningTask = globalTasks.Contains(hash);
            if (payload.Type == InventoryType.Block && isRunningTask)
                session.AvailableTasks.Add(hash);

            if (isRunningTask)
            {
                RequestTasks(session);
                return;
            }
            globalTasks.Add(hash);
            session.Tasks[hash] = DateTime.UtcNow;
            Sender.Tell(Message.Create("getdata", InvPayload.Create(payload.Type, hash)));
        }

        private void OnNewGroupTask(InvGroupPayload payload)
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            if (payload.Type == InventoryType.TX && blockchain.Height < blockchain.HeaderHeight)
            {
                RequestTasks(session);
                return;
            }
            HashSet<UInt256> hashes = new HashSet<UInt256>(payload.Hashes);
            hashes.ExceptWith(knownHashes);
            if (payload.Type == InventoryType.Block)
                session.AvailableTasks.UnionWith(hashes.Where(p => globalTasks.Contains(p)));
            hashes.ExceptWith(globalTasks);
            if (hashes.Count == 0)
            {
                RequestTasks(session);
                return;
            }
            globalTasks.UnionWith(hashes);
            foreach (UInt256 hash in hashes)
                session.Tasks[hash] = DateTime.UtcNow;

            if (hashes.Count == 1)
            {
                Sender.Tell(Message.Create("getdata", InvPayload.Create(payload.Type, hashes.First())));
            }
            else
            {
                foreach (InvGroupPayload group in InvGroupPayload.CreateGroup(payload.Type, hashes.ToArray()))
                    Sender.Tell(Message.Create("getdatagroup", group));
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Register register:
                    OnRegister(register.Version);
                    break;
                case NewTask task:
                    OnNewTask(task.Payload);
                    break;
                case NewGroupTask task:
                    OnNewGroupTask(task.Payload);
                    break;
                case TaskCompleted completed:
                    OnTaskCompleted(completed.Hash);
                    break;
                case HeaderTaskCompleted _:
                    OnHeaderTaskCompleted();
                    break;
                case RestartTasks restart:
                    OnRestartTasks(restart.Payload);
                    break;
                case Timer _:
                    OnTimer();
                    break;
                case Terminated terminated:
                    OnTerminated(terminated.ActorRef);
                    break;
            }
        }

        private void OnRegister(VersionPayload version)
        {
            Context.Watch(Sender);
            TaskSession session = new TaskSession(Sender, version);
            sessions.Add(Sender, session);
            RequestTasks(session);
        }

        private void OnRestartTasks(InvGroupPayload payload)
        {
            knownHashes.ExceptWith(payload.Hashes);
            globalTasks.ExceptWith(payload.Hashes);
            foreach (InvGroupPayload group in InvGroupPayload.CreateGroup(payload.Type, payload.Hashes))
                system.LocalNode.Tell(Message.Create("getdatagroup", group));
        }

        private void OnTaskCompleted(UInt256 hash)
        {
            knownHashes.Add(hash);
            globalTasks.Remove(hash);
            foreach (TaskSession ms in sessions.Values)
                ms.AvailableTasks.Remove(hash);
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                session.Tasks.Remove(hash);
                RequestTasks(session);
            }
        }

        private void OnTerminated(IActorRef actor)
        {
            if (!sessions.TryGetValue(actor, out TaskSession session))
                return;
            sessions.Remove(actor);
            globalTasks.ExceptWith(session.Tasks.Keys);
        }

        private void OnTimer()
        {
            foreach (TaskSession session in sessions.Values)
                foreach (var task in session.Tasks.ToArray())
                    if (DateTime.UtcNow - task.Value > TaskTimeout)
                    {
                        globalTasks.Remove(task.Key);
                        session.Tasks.Remove(task.Key);
                    }
            foreach (TaskSession session in sessions.Values)
                RequestTasks(session);
        }

        protected override void PostStop()
        {
            timer.CancelIfNotNull();
            base.PostStop();
        }

        public static Props Props(ZoroSystem system, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new TaskManager(system, chainHash)).WithMailbox("task-manager-mailbox");
        }

        private void RequestTasks(TaskSession session)
        {
            if (session.HasTask) return;
            if (session.AvailableTasks.Count > 0)
            {
                session.AvailableTasks.ExceptWith(knownHashes);
                session.AvailableTasks.RemoveWhere(p => blockchain.ContainsBlock(p));
                HashSet<UInt256> hashes = new HashSet<UInt256>(session.AvailableTasks);
                hashes.ExceptWith(globalTasks);
                if (hashes.Count > 0)
                {
                    session.AvailableTasks.ExceptWith(hashes);
                    globalTasks.UnionWith(hashes);
                    foreach (UInt256 hash in hashes)
                        session.Tasks[hash] = DateTime.UtcNow;
                    foreach (InvGroupPayload group in InvGroupPayload.CreateGroup(InventoryType.Block, hashes.ToArray()))
                        session.RemoteNode.Tell(Message.Create("getdatagroup", group));
                    return;
                }
            }
            if (!HeaderTask && blockchain.HeaderHeight < session.Version.StartHeight)
            {
                session.Tasks[UInt256.Zero] = DateTime.UtcNow;
                session.RemoteNode.Tell(Message.Create("getheaders", GetBlocksPayload.Create(blockchain.CurrentHeaderHash)));
            }
            else if (blockchain.Height < session.Version.StartHeight)
            {
                UInt256 hash = blockchain.CurrentBlockHash;
                for (uint i = blockchain.Height + 1; i <= blockchain.HeaderHeight; i++)
                {
                    hash = blockchain.GetBlockHash(i);
                    if (!knownHashes.Contains(hash) && !globalTasks.Contains(hash))
                    {
                        hash = blockchain.GetBlockHash(i - 1);
                        break;
                    }
                }
                session.RemoteNode.Tell(Message.Create("getblocks", GetBlocksPayload.Create(hash)));
            }
        }
    }

    internal class TaskManagerMailbox : PriorityMailbox
    {
        public TaskManagerMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case TaskManager.Register _:
                case TaskManager.RestartTasks _:
                    return true;
                case TaskManager.NewTask task:
                    if (task.Payload.Type == InventoryType.Block || task.Payload.Type == InventoryType.Consensus)
                        return true;
                    return false;
                case TaskManager.NewGroupTask tasks:
                    if (tasks.Payload.Type == InventoryType.Block || tasks.Payload.Type == InventoryType.Consensus)
                        return true;
                    return false;
                default:
                    return false;
            }
        }
    }
}
