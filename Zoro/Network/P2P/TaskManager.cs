using Akka.Actor;
using Akka.Configuration;
using Zoro.IO.Actors;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Zoro.Network.P2P
{
    internal class TaskManager : UntypedActor
    {
        public class Register { public VersionPayload Version; }
        public class NewTasks { public InvPayload Payload; }
        public class RawTransactionTask { public InvPayload Payload; }
        public class TaskCompleted { public UInt256 Hash; }
        public class HeaderTaskCompleted { }
        public class RestartTasks { public InvPayload Payload; }
        private class Timer { }

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(1);
        private static readonly int MaxKnownHashCount = ProtocolSettings.Default.MaxTaskHashCount;

        private readonly ZoroSystem system;
        private const int MaxConncurrentTasks = 3;
        private readonly HashSet<UInt256> knownHashes = new HashSet<UInt256>();
        private readonly Dictionary<UInt256, int> globalTasks = new Dictionary<UInt256, int>();
        private readonly Dictionary<IActorRef, TaskSession> sessions = new Dictionary<IActorRef, TaskSession>();
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        private readonly UInt256 HeaderTaskHash = UInt256.Zero;
        private bool HasHeaderTask => globalTasks.ContainsKey(HeaderTaskHash);

        private readonly UInt160 chainHash;
        private readonly Blockchain blockchain;

        private bool HeaderTask => sessions.Values.Any(p => p.HeaderTask);

        public TaskManager(ZoroSystem system, UInt160 chainHash)
        {
            this.system = system;
            this.chainHash = chainHash;
            this.blockchain = ZoroChainSystem.Singleton.AskBlockchain(chainHash);
        }

        private void OnHeaderTaskCompleted()
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            session.Tasks.Remove(HeaderTaskHash);
            DecrementGlobalTask(HeaderTaskHash);
            RequestTasks(session);
        }

        private void OnNewTasks(InvPayload payload)
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
                session.AvailableTasks.UnionWith(hashes.Where(p => globalTasks.ContainsKey(p)));

            hashes.ExceptWith(globalTasks.Keys);
            if (hashes.Count == 0)
            {
                RequestTasks(session);
                return;
            }

            foreach (UInt256 hash in hashes)
            {
                IncrementGlobalTask(hash);
                session.Tasks[hash] = new RequestTask { Type = payload.Type, BeginTime = DateTime.UtcNow };
            }
            RequestInventoryData(payload.Type, hashes.ToArray(), Sender);
        }

        // 向远程节点发送获取未处理交易的消息
        private void OnRawTransactionTask(InvPayload payload)
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;

            HashSet<UInt256> hashes = new HashSet<UInt256>(payload.Hashes);
            hashes.ExceptWith(knownHashes);

            hashes.ExceptWith(globalTasks.Keys);
            if (hashes.Count == 0)
            {
                RequestTasks(session);
                return;
            }

            foreach (UInt256 hash in hashes)
            {
                IncrementGlobalTask(hash);
                session.Tasks[hash] = new RequestTask { Type = payload.Type, BeginTime = DateTime.UtcNow };
            }
            RequestRawTransactions(hashes.ToArray(), Sender);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Register register:
                    OnRegister(register.Version);
                    break;
                case NewTasks task:
                    OnNewTasks(task.Payload);
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
                case RawTransactionTask task:
                    OnRawTransactionTask(task.Payload);
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

        private void OnRestartTasks(InvPayload payload)
        {
            knownHashes.ExceptWith(payload.Hashes);
            foreach (UInt256 hash in payload.Hashes)
                globalTasks.Remove(hash);
            if (payload.Type == InventoryType.TX)
                RequestRawTransactions(payload.Hashes, system.LocalNode);
            else
                throw new ArgumentException();
        }

        private void OnTaskCompleted(UInt256 hash)
        {
            knownHashes.Add(hash);
            globalTasks.Remove(hash);
            foreach (TaskSession ms in sessions.Values)
                ms.AvailableTasks.Remove(hash);
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                if (session.Tasks.TryGetValue(hash, out RequestTask task))
                    Sender.Tell(new RemoteNode.TaskCompleted { Type = task.Type });
                session.Tasks.Remove(hash);
                RequestTasks(session);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecrementGlobalTask(UInt256 hash)
        {
            if (globalTasks.ContainsKey(hash))
            {
                if (globalTasks[hash] == 1)
                    globalTasks.Remove(hash);
                else
                    globalTasks[hash]--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IncrementGlobalTask(UInt256 hash)
        {
            if (!globalTasks.ContainsKey(hash))
            {
                globalTasks[hash] = 1;
                return true;
            }
            if (globalTasks[hash] >= MaxConncurrentTasks)
                return false;

            globalTasks[hash]++;

            return true;
        }

        private void OnTerminated(IActorRef actor)
        {
            if (!sessions.TryGetValue(actor, out TaskSession session))
                return;
            sessions.Remove(actor);
            foreach (UInt256 hash in session.Tasks.Keys)
                DecrementGlobalTask(hash);
        }

        private void OnTimer()
        {
            foreach (TaskSession session in sessions.Values)
                foreach (var task in session.Tasks.ToArray())
                    if (DateTime.UtcNow - task.Value.BeginTime > TaskTimeout)
                    {
                        if (session.Tasks.Remove(task.Key))
                            DecrementGlobalTask(task.Key);

                        session.RemoteNode.Tell(new RemoteNode.TaskTimeout { Type = task.Value.Type });
                    }
            foreach (TaskSession session in sessions.Values)
                RequestTasks(session);

            ClearKnownHashes();
        }

        protected override void PostStop()
        {
            blockchain.Log($"OnStop TaskManager {blockchain.Name}");
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
                if (hashes.Count > 0)
                {
                    foreach (UInt256 hash in hashes.ToArray())
                    {
                        if (!IncrementGlobalTask(hash))
                            hashes.Remove(hash);
                    }
                    session.AvailableTasks.ExceptWith(hashes);
                    foreach (UInt256 hash in hashes)
                        session.Tasks[hash] = new RequestTask { Type = InventoryType.Block, BeginTime = DateTime.UtcNow }; 
                    RequestInventoryData(InventoryType.Block, hashes.ToArray(), session.RemoteNode);
                    return;
                }
            }
            if ((!HasHeaderTask || globalTasks[HeaderTaskHash] < MaxConncurrentTasks) && blockchain.HeaderHeight < session.Version.StartHeight)
            {
                session.Tasks[HeaderTaskHash] = new RequestTask { Type = InventoryType.Block, BeginTime = DateTime.UtcNow };
                IncrementGlobalTask(HeaderTaskHash);
                session.RemoteNode.Tell(Message.Create("getheaders", GetBlocksPayload.Create(blockchain.CurrentHeaderHash)));
            }
            else if (blockchain.Height < session.Version.StartHeight && blockchain.Height < blockchain.HeaderHeight)
            {
                UInt256 hash = blockchain.CurrentBlockHash;
                for (uint i = blockchain.Height + 1; i <= blockchain.HeaderHeight; i++)
                {
                    hash = blockchain.GetBlockHash(i);
                    if (!globalTasks.ContainsKey(hash))
                    {
                        hash = blockchain.GetBlockHash(i - 1);
                        break;
                    }
                }
                if (hash != null)
                {
                    session.RemoteNode.Tell(Message.Create("getblocks", GetBlocksPayload.Create(hash)));
                }
            }
        }

        private void RequestInventoryData(InventoryType type, UInt256[] hashes, IActorRef sender)
        {
            if (hashes.Length == 0)
                return;

            if (hashes.Length == 1)
            {
                sender.Tell(Message.Create("getdata", InvPayload.Create(type, hashes[0])));
            }
            else
            {
                foreach (InvPayload group in InvPayload.CreateGroup(type, hashes))
                    sender.Tell(Message.Create("getdatagroup", group));
            }

            Sender.Tell(new RemoteNode.RequestInventory { Type = type, Count = hashes.Length });
        }

        // 向远程节点发送获取未处理交易的消息
        private void RequestRawTransactions(UInt256[] hashes, IActorRef sender)
        {
            if (hashes.Length == 0)
                return;

            foreach (InvPayload group in InvPayload.CreateGroup(InventoryType.TX, hashes))
                sender.Tell(Message.Create("getrawtxn", group));

            Sender.Tell(new RemoteNode.RequestInventory { Type = InventoryType.TX, Count = hashes.Length });
            blockchain.Log($"send getrawtxn, count:{hashes.Length}", Plugins.LogLevel.Debug);
        }

        private void ClearKnownHashes()
        {
            if (MaxKnownHashCount > 0 && knownHashes.Count > MaxKnownHashCount)
            {
                knownHashes.Clear();
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
                case TaskManager.NewTasks task:
                    if (task.Payload.Type == InventoryType.Block || task.Payload.Type == InventoryType.Consensus)
                        return true;
                    return false;
                default:
                    return false;
            }
        }
    }
}
