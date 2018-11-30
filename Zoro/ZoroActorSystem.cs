using Akka.Actor;
using Zoro.Consensus;
using Zoro.Ledger;
using Zoro.Network.P2P;
using Zoro.Persistence;
using Zoro.Wallets;
using System;
using System.Threading;


namespace Zoro
{
    public class ZoroActorSystem : IDisposable
    {
        public UInt160 ChainHash { get; private set; }        
        public IActorRef System { get; private set; }
        public ActorSystem ActorSystem { get; }

        private Store store;

        private static ZoroActorSystem root;

        public static ZoroActorSystem Root
        {
            get
            {
                while (root == null) Thread.Sleep(10);
                return root;
            }
        }
        public ZoroActorSystem(UInt160 chainHash, Store store)
        {
            ChainHash = chainHash;

            // 只有在创建根链的ZoroSystem对象时，才创建ActorSystem
            if (chainHash.Equals(UInt160.Zero))
            {
                if (root != null)
                    throw new InvalidOperationException();

                root = this;

                this.ActorSystem = ActorSystem.Create(nameof(ZoroSystem),
                    $"akka {{ log-dead-letters = off }}" +
                    $"blockchain-mailbox {{ mailbox-type: \"{typeof(BlockchainMailbox).AssemblyQualifiedName}\" }}" +
                    $"task-manager-mailbox {{ mailbox-type: \"{typeof(TaskManagerMailbox).AssemblyQualifiedName}\" }}" +
                    $"remote-node-mailbox {{ mailbox-type: \"{typeof(RemoteNodeMailbox).AssemblyQualifiedName}\" }}" +
                    $"protocol-handler-mailbox {{ mailbox-type: \"{typeof(ProtocolHandlerMailbox).AssemblyQualifiedName}\" }}" +
                    $"consensus-service-mailbox {{ mailbox-type: \"{typeof(ConsensusServiceMailbox).AssemblyQualifiedName}\" }}");
            }
            else
            {
                // 创建应用链的ZoroSystem时，使用根链的ActorSystem
                this.ActorSystem = Root.ActorSystem;
            }

            string hashString = chainHash.ToString();

            this.System = this.ActorSystem.ActorOf(Zoro.ZoroSystem.Props(this, store, chainHash), $"{chainHash.ToString()}");

            this.store = store;
        }

        public void Dispose()
        {
            ActorSystem.Stop(System);

            // 只有在停止根链时才停止ActorSystem
            if (this == root)
            {
                Thread.Sleep(5000);
                ActorSystem.Dispose();
            }
        }

        public void StartConsensus(UInt160 chainHash, Wallet wallet)
        {
            System.Tell(new ZoroSystem.StartConsensus { Wallet = wallet });
        }

        public void StopConsensus()
        {
            System.Tell(new ZoroSystem.StopConsensus());
        }

        public void StartNode(int port = 0, int wsPort = 0, int minDesiredConnections = Peer.DefaultMinDesiredConnections,
            int maxConnections = Peer.DefaultMaxConnections)
        {
            System.Tell(new ZoroSystem.Start
            {
                Port = port,
                WsPort = wsPort,
                MinDesiredConnections = minDesiredConnections,
                MaxConnections = maxConnections
            });
        }
    }
}
