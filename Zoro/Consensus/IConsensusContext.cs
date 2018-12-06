using Zoro.Cryptography.ECC;
using Zoro.Network.P2P.Payloads;
using System;
using System.Collections.Generic;

namespace Zoro.Consensus
{
    public interface IConsensusContext : IDisposable
    {
        //public const uint Version = 0;
        ConsensusState State { get; set; }
        UInt256 PrevHash { get; }
        uint BlockIndex { get; }
        ushort ViewNumber { get; }
        ECPoint[] Validators { get; }
        int MyIndex { get; }
        uint PrimaryIndex { get; }
        uint Timestamp { get; set; }
        ulong Nonce { get; set; }
        UInt160 NextConsensus { get; set; }
        UInt256[] TransactionHashes { get; set; }
        Dictionary<UInt256, Transaction> Transactions { get; set; }
        byte[][] Signatures { get; set; }
        ushort[] ExpectedView { get; set; }

        int M { get; }

        Header PrevHeader { get; }

        bool ContainsTransaction(UInt256 hash);
        bool VerifyTransaction(Transaction tx);

        void ChangeView(ushort view_number);

        Block CreateBlock();

        //void Dispose();

        uint GetPrimaryIndex(ushort view_number);

        ConsensusPayload MakeChangeView();

        Block MakeHeader();

        void SignHeader();

        ConsensusPayload MakePrepareRequest();

        ConsensusPayload MakePrepareResponse(byte[] signature);

        void Reset();

        void Fill();

        bool VerifyRequest();
    }
}
