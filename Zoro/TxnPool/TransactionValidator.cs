﻿using Akka.Actor;
using Akka.Configuration;
using Zoro.IO.Actors;
using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Network.P2P.Payloads;

namespace Zoro.TxnPool
{
    // 重新验证交易
    internal class TransactionValidator : UntypedActor
    {
        private Snapshot snapshot;
        private ZoroSystem system;
        private Blockchain blockchain;

        public TransactionValidator(ZoroSystem system, UInt160 chainHash)
        {
            this.system = system;
            this.blockchain = ZoroChainSystem.Singleton.AskBlockchain(chainHash);
            this.snapshot = blockchain.GetSnapshot();
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Blockchain.UpdateSnapshot _:
                    OnUpdateSnapshot();
                    break;

                case Transaction[] txns:
                    ValidateTransactions(txns);
                    break;
            }
        }

        private void OnUpdateSnapshot()
        {
            snapshot?.Dispose();
            snapshot = blockchain.GetSnapshot();
        }

        private void ValidateTransactions(Transaction[] txns)
        {
            foreach (var tx in txns)
            {
                bool result = tx.Reverify(snapshot);

                Sender.Tell(new TransactionPool.VerifyResult { tx = tx, Result = result });
            }
        }

        protected override void PostStop()
        {
            snapshot?.Dispose();
            base.PostStop();
        }

        public static Props Props(ZoroSystem system, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new TransactionValidator(system, chainHash)).WithMailbox("transaction-validator-mailbox");
        }
    }

    internal class TransactionValidatorMailbox : PriorityMailbox
    {
        public TransactionValidatorMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case Transaction _:
                    return false;
                default:
                    return true;
            }
        }
    }
}