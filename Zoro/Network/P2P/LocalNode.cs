using Akka.Actor;
using Zoro.IO;
using Zoro.Ledger;
using Zoro.AppChain;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Zoro.Network.P2P
{
    public class LocalNode : Peer
    {
        public class Relay { public IInventory Inventory; }
        internal class RelayDirectly { public IInventory Inventory; }
        internal class SendDirectly { public IInventory Inventory; }

        public const uint ProtocolVersion = 0;

        private readonly ZoroSystem system;
        internal readonly ConcurrentDictionary<IActorRef, RemoteNode> RemoteNodes = new ConcurrentDictionary<IActorRef, RemoteNode>();

        public int ConnectedCount => RemoteNodes.Count;
        public int UnconnectedCount => UnconnectedPeers.Count;
        public static readonly uint Nonce;
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
            Nonce = (uint)rand.Next();
            UserAgent = $"/{Assembly.GetExecutingAssembly().GetName().Name}:{Assembly.GetExecutingAssembly().GetVersion()}/";
        }

        public LocalNode(ZoroSystem system, UInt160 chainHash)
        {
            //lock (GetType())
            {
                this.system = system;
                this.ChainHash = chainHash;

                if (chainHash.Equals(UInt160.Zero))
                {
                    if (root != null)
                        throw new InvalidOperationException();

                    root = this;

                    this.SeedList = Settings.Default.SeedList;
                }
                else
                {
                    AppChainState state = AppChainManager.Singleton.RegisterAppChainLocalNode(chainHash, this);

                    this.SeedList = state.SeedList;
                }

                this.Blockchain = AppChainManager.Singleton.AskBlockchain(chainHash);
            }
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
                BroadcastMessage("getaddr");
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

        public static Props Props(ZoroSystem system, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new LocalNode(system, chainHash));
        }

        protected override Props ProtocolProps(object connection, IPEndPoint remote, IPEndPoint local)
        {
            return RemoteNode.Props(system, connection, remote, local, this);
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
    }
}
