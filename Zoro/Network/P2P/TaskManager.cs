using Akka.Actor;
using Akka.Configuration;
using Zoro.IO.Actors;
using Zoro.Ledger;
using Zoro.Plugins;
using Zoro.Network.P2P.Payloads;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Zoro.Network.P2P
{
    internal class TaskManager : UntypedActor
    {
        class SyncHeaderTask
        {
            public uint HeaderHeight;
            public DateTime ExpiryTime;
            public TaskSession Session;
        }

        class SyncBlockTask
        {
            public UInt256 Hash;
            public uint BlockHeight;
            public DateTime ExpiryTime;
            public TaskSession Session;
        }

        public class Register { public VersionPayload Version; }
        public class NewTasks { public InvPayload Payload; }
        public class TaskCompleted { public UInt256 Hash; public InventoryType Type; }
        public class HeaderTaskCompleted { }
        public class HeaderMessageReceived { }
        public class RestartTasks { public InvPayload Payload; }
        public class UpdateSession { public uint Height; public uint Latency; }
        private class Timer { }
        private class SyncTimer { }

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan TaskTimeout = TimeSpan.FromSeconds(15);
        private static readonly int MaxKnownHashCount = ProtocolSettings.Default.MaxTaskHashCount;

        private readonly ZoroSystem system;
        private readonly HashSet<UInt256> knownHashes = new HashSet<UInt256>();
        private readonly HashSet<UInt256> globalTasks = new HashSet<UInt256>();
        private readonly Dictionary<IActorRef, TaskSession> sessions = new Dictionary<IActorRef, TaskSession>();
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);
        private readonly ICancelable syncTimer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(SyncTimerInterval, SyncTimerInterval, Context.Self, new SyncTimer(), ActorRefs.NoSender);

        private static readonly int MaxHeaderForwardCount = 5000;
        private static readonly int MaxBlockForwardCount = 1000;
        private static readonly int MaxSyncHeaderTaskCount = 1;
        private static readonly int MaxSyncBlockTaskCount = 50;
        private static readonly TimeSpan SyncHeaderTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SyncBlockTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SyncTimerInterval = TimeSpan.FromSeconds(1);

        private readonly Dictionary<TaskSession, SyncHeaderTask> syncHeaderTasks = new Dictionary<TaskSession, SyncHeaderTask>();
        private readonly Dictionary<UInt256, List<SyncBlockTask>> syncBlockTasks = new Dictionary<UInt256, List<SyncBlockTask>>();
        private uint syncingBlockHeight = 0;

        private readonly UInt160 chainHash;
        private readonly Blockchain blockchain;

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
            CompleteSyncHeaderTask(session);
            RequestTasks(session);
        }

        private void OnHeaderMessageReceived()
        {
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                SetSyncHeaderTaskNeverExpire(session);
            }
        }

        private void OnNewTasks(InvPayload payload)
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            if (payload.Type == InventoryType.TX && blockchain.Height < (int)blockchain.HeaderHeight - 100)
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
            {
                session.Tasks[hash] = new TaskSession.Task { Type = payload.Type, BeginTime = DateTime.UtcNow };
            }
            RequestInventoryData(payload.Type, hashes.ToArray(), Sender);
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
                    OnTaskCompleted(completed.Hash, completed.Type);
                    break;
                case HeaderTaskCompleted completed:
                    OnHeaderTaskCompleted();
                    break;
                case HeaderMessageReceived _:
                    OnHeaderMessageReceived();
                    break;
                case RestartTasks restart:
                    OnRestartTasks(restart.Payload);
                    break;
                case Timer _:
                    OnTimer();
                    break;
                case SyncTimer _:
                    OnSyncTimer();
                    break;
                case Terminated terminated:
                    OnTerminated(terminated.ActorRef);
                    break;
                case UpdateSession msg:
                    OnUpdateSession(msg);
                    break;
            }
        }

        private void OnRegister(VersionPayload version)
        {
            Context.Watch(Sender);
            TaskSession session = new TaskSession(Sender, version);
            sessions.Add(Sender, session);
        }

        private void OnRestartTasks(InvPayload payload)
        {
            knownHashes.ExceptWith(payload.Hashes);
            globalTasks.ExceptWith(payload.Hashes);
            RequestInventoryData(payload.Type, payload.Hashes, system.LocalNode);
        }

        private void OnTaskCompleted(UInt256 hash, InventoryType type)
        {
            knownHashes.Add(hash);
            globalTasks.Remove(hash);
            foreach (TaskSession ms in sessions.Values)
                ms.AvailableTasks.Remove(hash);
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                if (type == InventoryType.Block)
                    CompleteSyncBlockTask(hash, session);
                Sender.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Received, type));
                session.Tasks.Remove(hash);
                RequestTasks(session);
            }
        }

        private void OnUpdateSession(UpdateSession msg)
        {
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                session.Height = msg.Height;
                session.Latency = msg.Latency;
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
            CheckSyncTimeout();

            foreach (TaskSession session in sessions.Values)
                foreach (var task in session.Tasks.ToArray())
                    if (DateTime.UtcNow - task.Value.BeginTime > TaskTimeout)
                    {
                        globalTasks.Remove(task.Key);
                        session.Tasks.Remove(task.Key);
                        session.RemoteNode.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Timeout, task.Value.Type));
                    }
            foreach (TaskSession session in sessions.Values)
                RequestTasks(session);

            ClearKnownHashes();
        }

        private void CheckSyncTimeout()
        {
            if (sessions.Count() <= 0)
                return;

            CheckSyncHeaderTimeout();
            CheckSyncBlockTimeout();
        }

        private void OnSyncTimer()
        {
            if (sessions.Count() <= 0)
                return;

            SyncHeader();
            SyncBlocks();
        }

        protected override void PostStop()
        {
            Log($"OnStop TaskManager {blockchain.Name}");
            timer.CancelIfNotNull();
            syncTimer.CancelIfNotNull();
            base.PostStop();
        }

        public static Props Props(ZoroSystem system, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new TaskManager(system, chainHash)).WithMailbox("task-manager-mailbox");
        }

        protected void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginManager.Singleton?.Log(nameof(TaskManager), level, message, blockchain.ChainHash);
        }

        private TaskSession GetSyncSession(uint blockHeight, bool headerTask)
        {
            if (sessions.Count() <= 0)
                return null;

            List<TaskSession> sns = sessions.Values.Where(p => (!headerTask || !p.HasHeaderTask) && p.Height >= blockHeight).ToList();
            if (sns.Count() <= 0)
                return null;

            sns.Sort(CompareSession);
            return sns.First();
        }

        private int CompareSession(TaskSession ts1, TaskSession ts2)
        {
            int r = ts1.Weight.CompareTo(ts2.Weight);
            if (r != 0) return r;

            r = ts1.Latency.CompareTo(ts2.Latency);
            if (r != 0) return r;

            return ts1.Version.NodeId.CompareTo(ts2.Version.NodeId);
        }

        private void SyncHeader()
        {
            int taskCount = syncHeaderTasks.Count();
            if (taskCount >= MaxSyncHeaderTaskCount)
                return;

            uint currentBlockHeight = blockchain.Height;
            uint currentHeaderHeight = blockchain.HeaderHeight;
            if (currentHeaderHeight >= currentBlockHeight + MaxHeaderForwardCount)
                return;

            uint nextHeaderHeight = currentHeaderHeight + 1;
            int count = MaxSyncHeaderTaskCount - taskCount;
            for (int i = 1; i <= count; i++)
            {
                TaskSession session = GetSyncSession(nextHeaderHeight, true);
                if (session == null)
                    return;

                AddSyncHeaderTask(nextHeaderHeight, session);
                session.RemoteNode.Tell(Message.Create(MessageType.GetHeaders, GetBlocksPayload.Create(blockchain.CurrentHeaderHash)));
                Log($"SyncHeader {nextHeaderHeight} {session.Version.NodeId}");
            }
        }

        private void AddSyncHeaderTask(uint headerHeight, TaskSession session)
        {
            if (syncHeaderTasks.TryGetValue(session, out SyncHeaderTask task))
            {
                task.ExpiryTime = DateTime.UtcNow + SyncHeaderTimeout;
                task.HeaderHeight = headerHeight;
            }
            else
            {
                syncHeaderTasks.Add(session, new SyncHeaderTask
                {
                    HeaderHeight = headerHeight,
                    ExpiryTime = DateTime.UtcNow + SyncHeaderTimeout,
                    Session = session
                });
            }

            session.HasHeaderTask = true;
        }

        private bool RemoveSyncHeaderTask(TaskSession session)
        {
            return syncHeaderTasks.Remove(session);
        }

        private void SetSyncHeaderTaskNeverExpire(TaskSession session)
        {
            if (syncHeaderTasks.TryGetValue(session, out SyncHeaderTask task))
            {
                task.ExpiryTime = DateTime.MaxValue;
                Log($"SyncHeader received");
            }
        }

        private void CompleteSyncHeaderTask(TaskSession session)
        {
            if (RemoveSyncHeaderTask(session))
            {
                session.HasHeaderTask = false;
                Log($"SyncHeader completed");
                SyncHeader();
            }
        }

        private void CheckSyncHeaderTimeout()
        {
            DateTime now = DateTime.UtcNow;

            SyncHeaderTask[] timeoutTasks = syncHeaderTasks.Values.Where(p => now >= p.ExpiryTime).ToArray();
            foreach (SyncHeaderTask task in timeoutTasks)
            {
                task.Session.Timeout++;
                Log($"SyncHeader timeout: {task.HeaderHeight} {task.Session.Version.NodeId}");
                RemoveSyncHeaderTask(task.Session);
            }

            if (timeoutTasks.Length > 0)
                SyncHeader();
        }

        private void SyncBlocks()
        {
            int taskCount = syncBlockTasks.Count();
            if (taskCount >= MaxSyncBlockTaskCount)
                return;

            uint currentBlockHeight = blockchain.Height;
            if (syncingBlockHeight >= currentBlockHeight + MaxBlockForwardCount)
                return;

            uint startBlockHeight = Math.Max(currentBlockHeight, syncingBlockHeight);
            if (startBlockHeight >= blockchain.HeaderHeight)
                return;

            int count = MaxSyncBlockTaskCount - taskCount;
            Log($"SyncBlocks: {startBlockHeight}, {count}");
            for (int i = 1;i <= count;i ++)
            {
                uint nextBlockHeight = startBlockHeight + (uint)i;
                if (!SyncBlock(nextBlockHeight))
                    break;
            }
        }

        public bool SyncBlock(uint blockHeight)
        {
            TaskSession session = GetSyncSession(blockHeight, false);
            if (session == null)
                return false;

            UInt256 hash = blockchain.GetBlockHash(blockHeight);
            if (hash == null)
                return false;

            if (!globalTasks.Add(hash))
                return true;

            AddSyncBlockTask(hash, blockHeight, session);
            session.RemoteNode.Tell(Message.Create(MessageType.GetData, InvPayload.Create(InventoryType.Block, hash)));
            session.RemoteNode.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Request, InventoryType.Block));

            syncingBlockHeight = blockHeight;
            Log($"SyncBlock: {blockHeight}, {session.Version.NodeId}", LogLevel.Debug);
            return true;
        }

        private void AddSyncBlockTask(UInt256 hash, uint blockHeight, TaskSession session)
        {
            if (!syncBlockTasks.TryGetValue(hash, out List<SyncBlockTask> list))
            {
                list = new List<SyncBlockTask>();

                syncBlockTasks.Add(hash, list);
            }

            SyncBlockTask task = list.Find(p => p.Session == session);
            if (task != null)
            {
                task.ExpiryTime = DateTime.UtcNow + SyncBlockTimeout;
            }
            else
            {
                list.Add(new SyncBlockTask
                {
                    Hash = hash,
                    BlockHeight = blockHeight,
                    ExpiryTime = DateTime.UtcNow + SyncBlockTimeout,
                    Session = session
                });
            }

            session.SyncBlockTasks++;
        }

        private void RemoveSyncBlockTask(UInt256 hash, TaskSession session, bool cleanup = true)
        {
            if (syncBlockTasks.TryGetValue(hash, out List<SyncBlockTask> list))
            {
                list.RemoveAll(p => p.Session == session);

                if (cleanup && list.Count() == 0)
                    syncBlockTasks.Remove(hash);
            }
        }

        private void RemoveSyncBlockTask(SyncBlockTask task, bool cleanup = true)
        {
            RemoveSyncBlockTask(task.Hash, task.Session, cleanup);
        }

        private void RestartSyncBlockTask(SyncBlockTask task, TaskSession session)
        {
            RemoveSyncBlockTask(task.Hash, task.Session, false);

            AddSyncBlockTask(task.Hash, task.BlockHeight, session);
        }

        private void CompleteSyncBlockTask(UInt256 hash, TaskSession session)
        {
            if (syncBlockTasks.TryGetValue(hash, out List<SyncBlockTask> list))
            {
                SyncBlockTask task = list.Find(p => p.Session == session);

                if (task != null)
                {
                    list.Remove(task);

                    if (list.Count() == 0)
                        syncBlockTasks.Remove(hash);
                }
            }

            if (syncBlockTasks.Count() == 0)
            {
                SyncBlocks();
            }
        }

        private void CheckSyncBlockTimeout()
        {
            DateTime now = DateTime.UtcNow;

            SyncBlockTask[] timeoutTasks = syncBlockTasks.Values.SelectMany(p => p.Where(q => now >= q.ExpiryTime)).ToArray();
            foreach (SyncBlockTask task in timeoutTasks)
            {
                task.Session.Timeout++;
                task.Session.RemoteNode.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Timeout, InventoryType.Block));
                Log($"SyncBlock timeout: {task.BlockHeight}, {task.Session.Version.NodeId}");

                if (task.BlockHeight <= blockchain.Height)
                {
                    RemoveSyncBlockTask(task);
                }
                else
                {
                    TaskSession session = GetSyncSession(task.BlockHeight, false);

                    if (session != null)
                    {
                        RestartSyncBlockTask(task, session);

                        session.RemoteNode.Tell(Message.Create(MessageType.GetData, InvPayload.Create(InventoryType.Block, task.Hash)));
                        session.RemoteNode.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Request, InventoryType.Block));
                    }
                    else
                    {
                        RemoveSyncBlockTask(task);
                    }
                }
            }            
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
                        session.Tasks[hash] = new TaskSession.Task { Type = InventoryType.Block, BeginTime = DateTime.UtcNow }; 
                    RequestInventoryData(InventoryType.Block, hashes.ToArray(), session.RemoteNode);
                    return;
                }
            }
        }

        private void RequestInventoryData(InventoryType type, UInt256[] hashes, IActorRef sender)
        {
            if (hashes.Length == 0)
                return;

            if (hashes.Length == 1)
            {
                sender.Tell(Message.Create(MessageType.GetData, InvPayload.Create(type, hashes[0])));
            }
            else
            {
                string cmd;
                if (type == InventoryType.Block)
                {
                    cmd = MessageType.GetBlk;
                }
                else if (type == InventoryType.TX)
                {
                    cmd = MessageType.GetTxn;
                }
                else
                {
                    return;
                }

                foreach (InvPayload group in InvPayload.CreateGroup(type, hashes))
                    sender.Tell(Message.Create(cmd, group));
            }

            Sender.Tell(RemoteNode.NewCounterMessage(RemoteNode.CounterType.Request, type, hashes.Length));
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
