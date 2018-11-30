using System;
using System.Threading;
using Akka.Actor;
using Zoro.Plugins;
using Zoro.Wallets;
using Zoro.AppChain;
using Zoro.Consensus;
using Zoro.Network.P2P;
using Zoro.Persistence;

namespace Zoro
{
    public sealed class ZoroSystem : UntypedActor
    {
        public class ChainStarted { public UInt160 ChainHash; public int Port; public int WsPort; }
        public class ChainStopped { public UInt160 ChainHash; }

        public class Start { public int Port = 0; public int WsPort = 0; public int MinDesiredConnections = Peer.DefaultMinDesiredConnections; public int MaxConnections = Peer.DefaultMaxConnections; }
        public class StartConsensus { public Wallet Wallet; };
        public class StopConsensus { };

        public UInt160 ChainHash { get; private set; }
        public ZoroActorSystem ActorSystem { get; private set; }

        public IActorRef Blockchain { get; }
        public IActorRef LocalNode { get; }
        internal IActorRef TaskManager { get; }
        public IActorRef Consensus { get; private set; }

        public bool HasConsensusService => Consensus != null;

        private static ZoroSystem root;
        public static ZoroSystem Root
        {
            get
            {
                while (root == null) Thread.Sleep(10);
                return root;
            }
        }

        public ZoroSystem(ZoroActorSystem actorSystem, Store store, UInt160 chainHash)
        {
            ChainHash = chainHash;
            ActorSystem = actorSystem;

            if (chainHash.Equals(UInt160.Zero))
            {
                if (root != null)
                    throw new InvalidOperationException();

                root = this;
            }
            else
            {
                AppChainManager.Singleton.RegisterAppSystem(chainHash, this);
            }

            Blockchain = Context.ActorOf(Ledger.Blockchain.Props(this, store, chainHash), $"Blockchain");
            LocalNode = Context.ActorOf(Network.P2P.LocalNode.Props(this, chainHash), $"LocalNode");
            TaskManager = Context.ActorOf(Network.P2P.TaskManager.Props(this, chainHash), $"TaskManager");
        }

        public IActorRef ActorOf(Props props, string name = null)
        {
            return Context.ActorOf(props, name);
        }

        public void Stop(IActorRef actor)
        {
            Context.Stop(actor);
        }

        private void _StartConsensus(Wallet wallet)
        {
            if (Consensus == null)
            {
                Consensus = Context.ActorOf(ConsensusService.Props(this, wallet, ChainHash), $"ConsensusService");
                Consensus.Tell(new ConsensusService.Start());
            }
        }

        private void _StopConsensus()
        {
            if (Consensus != null)
            {
                Context.Stop(Consensus);
                Consensus = null;
            }
        }

        private void StartNode(int port, int wsPort, int minDesiredConnections, int maxConnections)
        {
            LocalNode.Tell(new Peer.Start
            {
                Port = port,
                WsPort = wsPort,
                MinDesiredConnections = minDesiredConnections,
                MaxConnections = maxConnections
            });

            // 向插件发送消息通知
            PluginManager.Singleton.SendMessage(new ChainStarted
            {
                ChainHash = ChainHash,
                Port = port,
                WsPort = wsPort,
            });

            // 向应用链管理器发送事件通知
            AppChainManager.Singleton.OnBlockChainStarted(ChainHash, port, wsPort);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Start start:
                    StartNode(start.Port, start.WsPort, start.MinDesiredConnections, start.MaxConnections);
                    break;
                case StartConsensus startConsensus:
                    _StartConsensus(startConsensus.Wallet);
                    break;
                case StopConsensus _:
                    _StopConsensus();
                    break;
            }
        }

        protected override void PostStop()
        {
            base.PostStop();

            // 向插件发送消息通知
            PluginManager.Singleton.SendMessage(new ChainStopped
            {
                ChainHash = ChainHash
            });
        }

        public static Props Props(ZoroActorSystem actorSystem, Store store, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new ZoroSystem(actorSystem, store, chainHash));
        }
    }
}
