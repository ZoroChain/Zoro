using Akka.Actor;
using Zoro.IO;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Text;

namespace Zoro.Network.P2P
{
    public class LocalNode : Peer
    {
        public class Relay { public IInventory Inventory; }
        internal class RelayDirectly { public IInventory Inventory; }
        internal class SendDirectly { public IInventory Inventory; }

        public const uint ProtocolVersion = 0;

        private static readonly object lockObj = new object();
        private readonly ZoroSystem system;
        internal readonly ConcurrentDictionary<IActorRef, RemoteNode> RemoteNodes = new ConcurrentDictionary<IActorRef, RemoteNode>();

        public int ConnectedCount => RemoteNodes.Count;
        public int UnconnectedCount => UnconnectedPeers.Count;
        public double TxRate => RemoteNodes.Sum(p => p.Value.TXRate);
        public static readonly uint NodeId;
        public static readonly string RecentPeersFile = "RecentPeers_{0}.dat";
        public static string UserAgent { get; set; }

        public string[] SeedList { get; private set; }

        public UInt160 ChainHash { get; }
        public Blockchain Blockchain { get; }

        private static LocalNode root;
        public static LocalNode Root
        {
            get
            {
                while (root == null) Thread.Sleep(10);
                return root;
            }
        }

        static LocalNode()
        {
            Random rand = new Random();
            NodeId = (uint)rand.Next();
            UserAgent = $"/{Assembly.GetExecutingAssembly().GetName().Name}:{Assembly.GetExecutingAssembly().GetVersion()}/";
        }

        public LocalNode(ZoroSystem system, UInt160 chainHash)
        {
            lock (lockObj)
            {
                this.system = system;
                this.ChainHash = chainHash;

                if (chainHash.Equals(UInt160.Zero))
                {
                    if (root != null)
                        throw new InvalidOperationException();

                    root = this;

                    SeedList = ProtocolSettings.Default.SeedList;
                }
                else
                {
                    AppChainState state = ZoroChainSystem.Singleton.RegisterAppChainLocalNode(chainHash, this);

                    SeedList = state.SeedList;
                }

                Blockchain = ZoroChainSystem.Singleton.AskBlockchain(chainHash);
            }

            LoadRecentPeers();
        }

        private void BroadcastMessage(string command, ISerializable payload = null)
        {
            BroadcastMessage(Message.Create(command, payload));
        }

        private void BroadcastMessage(Message message)
        {
            Connections.Tell(message);
        }

        private IEnumerable<IPEndPoint> GetIPEndPointsFromSeedList(int seedsToTake)
        {
            if (seedsToTake > 0)
            {
                Random rand = new Random();
                foreach (string hostAndPort in SeedList.OrderBy(p => rand.Next()))
                {
                    if (seedsToTake == 0) break;
                    string[] p = hostAndPort.Split(':');
                    IPEndPoint seed;
                    try
                    {
                        seed = Zoro.Helper.GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                    }
                    catch (AggregateException)
                    {
                        continue;
                    }
                    if (seed == null) continue;
                    seedsToTake--;
                    yield return seed;
                }
            }
        }

        public IEnumerable<RemoteNode> GetRemoteNodes()
        {
            return RemoteNodes.Values;
        }

        public IEnumerable<IPEndPoint> GetUnconnectedPeers()
        {
            return UnconnectedPeers;
        }

        protected override void NeedMorePeers(int count)
        {
            count = Math.Max(count, 5);
            if (ConnectedPeers.Count > 0)
            {
                BroadcastMessage(MessageType.GetAddr);
            }
            else
            {
                AddPeers(GetIPEndPointsFromSeedList(count));
            }
        }

        protected override void OnReceive(object message)
        {
            base.OnReceive(message);
            switch (message)
            {
                case Message msg:
                    BroadcastMessage(msg);
                    break;
                case Relay relay:
                    OnRelay(relay.Inventory);
                    break;
                case RelayDirectly relay:
                    OnRelayDirectly(relay.Inventory);
                    break;
                case SendDirectly send:
                    OnSendDirectly(send.Inventory);
                    break;
                case RelayResultReason _:
                    break;
            }
        }

        private void OnRelay(IInventory inventory)
        {
            inventory.ChainHash = ChainHash;
            if (inventory is Transaction transaction)
                system.Consensus?.Tell(transaction);
            system.Blockchain.Tell(inventory);
        }

        private void OnRelayDirectly(IInventory inventory)
        {
            Connections.Tell(new RemoteNode.Relay { Inventory = inventory });
        }

        private void OnSendDirectly(IInventory inventory)
        {
            Connections.Tell(inventory);
        }

        protected override void PostStop()
        {
            Blockchain.Log($"OnStop LocalNode {Blockchain.Name}");
            base.PostStop();
        }

        public static Props Props(ZoroSystem system, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new LocalNode(system, chainHash));
        }

        protected override Props ProtocolProps(object connection, IPEndPoint remote, IPEndPoint local)
        {
            return RemoteNode.Props(system, connection, remote, local, Blockchain, this);
        }

        public void ChangeSeedList(string[] seedList)
        {
            // 只能变更应用链的种子节点
            if (ChainHash.Equals(UInt160.Zero))
            {
                throw new InvalidOperationException();
            }

            if (seedList.Length < 1)
            {
                throw new ArgumentException();
            }

            SeedList = seedList;

            // 通知根链更新应用链的种子节点
            ZoroSystem.Root.Blockchain.Tell(new Blockchain.ChangeAppChainSeedList { ChainHash = ChainHash, SeedList = seedList });
        }

        public void SaveRecentPeers()
        {
            List<IPEndPoint> seedlist = new List<IPEndPoint>();
            foreach (string hostAndPort in SeedList)
            {
                try
                {
                    string[] p = hostAndPort.Split(':');
                    IPEndPoint seed = Zoro.Helper.GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                    seedlist.Add(seed);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            IPEndPoint[] peers = ConnectedPeers.Values.Where(p => !seedlist.Contains(p)).ToArray();
            if (peers.Length == 0)
                return;

            string path = string.Format(RecentPeersFile, ChainHash.ToString());
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (BinaryWriter writer = new BinaryWriter(fs, Encoding.ASCII, true))
                {
                    writer.Write(peers.Length);
                    foreach (IPEndPoint endpoint in peers)
                    {
                        writer.Write(endpoint.Address.MapToIPv4().GetAddressBytes());
                        writer.Write((ushort)endpoint.Port);
                    }
                }
            }
        }

        private void LoadRecentPeers()
        {
            string path = string.Format(RecentPeersFile, ChainHash.ToString());
            if (!File.Exists(path))
                return;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                UnconnectedPeers.Clear();
                using (BinaryReader reader = new BinaryReader(fs, Encoding.ASCII, true))
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        IPAddress address = new IPAddress(reader.ReadBytes(4));
                        int port = reader.ReadUInt16();
                        ImmutableInterlocked.Update(ref UnconnectedPeers, p => p.Add(new IPEndPoint(address.MapToIPv6(), port)));
                    }
                }
            }
        }
    }
}
