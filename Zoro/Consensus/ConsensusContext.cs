using Zoro.Cryptography;
using Zoro.Cryptography.ECC;
using Zoro.IO;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Zoro.Plugins;
using Zoro.SmartContract;
using Zoro.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Consensus
{
    internal class ConsensusContext : IConsensusContext
    {
        public const uint Version = 0;
        public ConsensusState State { get; set; }
        public UInt256 PrevHash { get; set; }
        public uint BlockIndex { get; set; }
        public ushort ViewNumber { get; set; }
        public ECPoint[] Validators { get; set; }
        public int MyIndex { get; set; }
        public uint PrimaryIndex { get; set; }
        public uint Timestamp { get; set; }
        public ulong Nonce { get; set; }
        public UInt160 NextConsensus { get; set; }
        public UInt256[] TransactionHashes { get; set; }
        public Dictionary<UInt256, Transaction> Transactions { get; set; }
        public byte[][] Signatures { get; set; }
        public ushort[] ExpectedView { get; set; }
        private Snapshot snapshot;
        private KeyPair keyPair;
        private readonly Wallet wallet;
        private readonly Blockchain blockchain;

        public int M => Validators.Length - (Validators.Length - 1) / 3;
        public Header PrevHeader => snapshot.GetHeader(PrevHash);
        public bool TransactionExists(UInt256 hash) => snapshot.ContainsTransaction(hash);
        public bool VerifyTransaction(Transaction tx) => tx.Verify(snapshot);

        public ConsensusContext(Blockchain blockchain, Wallet wallet)
        {
            this.blockchain = blockchain;
            this.wallet = wallet;
        }

        public void ChangeView(ushort view_number)
        {
            State &= ConsensusState.SignatureSent;
            ViewNumber = view_number;
            PrimaryIndex = GetPrimaryIndex(view_number);
            if (State == ConsensusState.Initial)
            {
                TransactionHashes = null;
                Signatures = new byte[Validators.Length][];
            }
            if (MyIndex >= 0)
                ExpectedView[MyIndex] = view_number;
            _header = null;
        }

        public Block CreateBlock()
        {
            Block block = MakeHeader();
            if (block == null) return null;
            Contract contract = Contract.CreateMultiSigContract(M, Validators);
            ContractParametersContext sc = new ContractParametersContext(block, blockchain);
            for (int i = 0, j = 0; i < Validators.Length && j < M; i++)
                if (Signatures[i] != null)
                {
                    sc.AddSignature(contract, Validators[i], Signatures[i]);
                    j++;
                }
            block.Witness = sc.GetWitnesses()[0];
            block.Transactions = TransactionHashes.Select(p => Transactions[p]).ToArray();
            return block;
        }

        public void Dispose()
        {
            snapshot?.Dispose();
        }

        public uint GetPrimaryIndex(ushort view_number)
        {
            int p = ((int)BlockIndex - view_number) % Validators.Length;
            return p >= 0 ? (uint)p : (uint)(p + Validators.Length);
        }

        public ConsensusPayload MakeChangeView()
        {
            return MakeSignedPayload(new ChangeView
            {
                NewViewNumber = ExpectedView[MyIndex]
            });
        }

        private Block _header = null;
        public Block MakeHeader()
        {
            if (TransactionHashes == null) return null;
            if (_header == null)
            {
                _header = new Block
                {
                    Version = Version,
                    PrevHash = PrevHash,
                    ChainHash = blockchain.ChainHash,
                    MerkleRoot = MerkleTree.ComputeRoot(TransactionHashes),
                    Timestamp = Timestamp,
                    Index = BlockIndex,
                    ConsensusData = Nonce,
                    NextConsensus = NextConsensus,
                    Transactions = new Transaction[0]
                };
            }
            return _header;
        }

        private ConsensusPayload MakeSignedPayload(ConsensusMessage message)
        {
            message.ViewNumber = ViewNumber;
            ConsensusPayload payload = new ConsensusPayload
            {
                Version = Version,
                PrevHash = PrevHash,
                ChainHash = blockchain.ChainHash,
                BlockIndex = BlockIndex,
                ValidatorIndex = (ushort)MyIndex,
                Timestamp = Timestamp,
                Data = message.ToArray()
            };
            SignPayload(payload);
            return payload;
        }

        public void SignHeader()
        {
            Signatures[MyIndex] = MakeHeader()?.Sign(keyPair);
        }

        private void SignPayload(ConsensusPayload payload)
        {
            ContractParametersContext sc;
            try
            {
                sc = new ContractParametersContext(payload, blockchain);
                wallet.Sign(sc);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            payload.Witness = sc.GetWitnesses()[0];
        }

        public ConsensusPayload MakePrepareRequest()
        {
            return MakeSignedPayload(new PrepareRequest
            {
                Nonce = Nonce,
                NextConsensus = NextConsensus,
                TransactionHashes = TransactionHashes,
                MinerTransaction = (MinerTransaction)Transactions[TransactionHashes[0]],
                Signature = Signatures[MyIndex]
            });
        }

        public ConsensusPayload MakePrepareResponse(byte[] signature)
        {
            return MakeSignedPayload(new PrepareResponse
            {
                Signature = signature
            });
        }

        public void Reset()
        {
            snapshot?.Dispose();
            snapshot = blockchain.GetSnapshot();
            State = ConsensusState.Initial;
            PrevHash = snapshot.CurrentBlockHash;
            BlockIndex = snapshot.Height + 1;
            ViewNumber = 0;
            Validators = snapshot.GetValidators();
            MyIndex = -1;
            PrimaryIndex = BlockIndex % (uint)Validators.Length;
            TransactionHashes = null;
            Signatures = new byte[Validators.Length][];
            ExpectedView = new ushort[Validators.Length];
            keyPair = null;
            for (int i = 0; i < Validators.Length; i++)
            {
                WalletAccount account = wallet.GetAccount(Validators[i]);
                if (account?.HasKey == true)
                {
                    MyIndex = i;
                    keyPair = account.GetKey();
                    break;
                }
            }
            _header = null;
        }

        public void Fill()
        {
            IEnumerable<Transaction> mem_pool = blockchain.GetVerifiedTransactions();
            foreach (IPolicyPlugin plugin in PluginManager.Singleton.Policies)
                mem_pool = plugin.FilterForBlock(mem_pool);
            List<Transaction> transactions = mem_pool.ToList();
            while (true)
            {
                ulong nonce = Transaction.GetNonce();
                MinerTransaction tx = new MinerTransaction
                {
                    ChainHash = blockchain.ChainHash,
                    Nonce = nonce,
                    Account = wallet.GetChangeAddress(),
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0]
                };
                if (!snapshot.ContainsTransaction(tx.Hash))
                {
                    Nonce = nonce;
                    transactions.Insert(0, tx);
                    break;
                }
            }
            TransactionHashes = transactions.Select(p => p.Hash).ToArray();
            Transactions = transactions.ToDictionary(p => p.Hash);
            NextConsensus = Blockchain.GetConsensusAddress(snapshot.GetValidators(transactions).ToArray());
            Timestamp = Math.Max(TimeProvider.Current.UtcNow.ToTimestamp(), PrevHeader.Timestamp + 1);
        }

        public bool VerifyRequest()
        {
            if (!State.HasFlag(ConsensusState.RequestReceived))
                return false;
            if (!Blockchain.GetConsensusAddress(snapshot.GetValidators(Transactions.Values).ToArray()).Equals(NextConsensus))
                return false;
            return true;
        }
    }
}
