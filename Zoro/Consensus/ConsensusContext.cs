﻿using Zoro.Cryptography;
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
    internal class ConsensusContext : IDisposable
    {
        public const uint Version = 0;
        public ConsensusState State;
        public UInt256 PrevHash;
        public uint BlockIndex;
        public ushort ViewNumber;
        public Snapshot Snapshot;
        public ECPoint[] Validators;
        public int MyIndex;
        public uint PrimaryIndex;
        public uint Timestamp;
        public ulong Nonce;
        public UInt160 NextConsensus;
        public UInt256[] TransactionHashes;
        public Dictionary<UInt256, Transaction> Transactions;
        public byte[][] Signatures;
        public ushort[] ExpectedView;
        private KeyPair KeyPair;
        private readonly Wallet wallet;        
        private readonly Blockchain blockchain;

        public int M => Validators.Length - (Validators.Length - 1) / 3;

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
            sc.Verifiable.Witnesses = sc.GetWitnesses();
            block.Transactions = TransactionHashes.Select(p => Transactions[p]).ToArray();
            return block;
        }

        public void Dispose()
        {
            Snapshot?.Dispose();
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
            Signatures[MyIndex] = MakeHeader()?.Sign(KeyPair);
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
            sc.Verifiable.Witnesses = sc.GetWitnesses();
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
            Snapshot?.Dispose();
            Snapshot = blockchain.GetSnapshot();
            State = ConsensusState.Initial;
            PrevHash = Snapshot.CurrentBlockHash;
            BlockIndex = Snapshot.Height + 1;
            ViewNumber = 0;
            Validators = Snapshot.GetValidators();
            MyIndex = -1;
            PrimaryIndex = BlockIndex % (uint)Validators.Length;
            TransactionHashes = null;
            Signatures = new byte[Validators.Length][];
            ExpectedView = new ushort[Validators.Length];
            KeyPair = null;
            for (int i = 0; i < Validators.Length; i++)
            {
                WalletAccount account = wallet.GetAccount(Validators[i]);
                if (account?.HasKey == true)
                {
                    MyIndex = i;
                    KeyPair = account.GetKey();
                    break;
                }
            }
            _header = null;
        }

        public void Fill()
        {
            IEnumerable<Transaction> mem_pool = blockchain.GetMemoryPool();
            foreach (IPolicyPlugin plugin in PluginManager.Singleton.Policies)
                mem_pool = plugin.FilterForBlock(mem_pool);
            List<Transaction> transactions = mem_pool.ToList();
            Fixed8 amount_netfee = Block.CalculateNetFee(transactions);
            TransactionOutput[] outputs = amount_netfee == Fixed8.Zero ? new TransactionOutput[0] : new[] { new TransactionOutput
            {
                AssetId = Blockchain.UtilityToken.Hash,
                Value = amount_netfee,
                ScriptHash = wallet.GetChangeAddress()
            } };
            while (true)
            {
                ulong nonce = GetNonce();
                MinerTransaction tx = new MinerTransaction
                {
                    ChainHash = blockchain.ChainHash,
                    Nonce = (uint)(nonce % (uint.MaxValue + 1ul)),
                    Attributes = new TransactionAttribute[0],
                    Inputs = new CoinReference[0],
                    Outputs = outputs,
                    Witnesses = new Witness[0]
                };
                if (!Snapshot.ContainsTransaction(tx.Hash))
                {
                    Nonce = nonce;
                    transactions.Insert(0, tx);
                    break;
                }
            }
            TransactionHashes = transactions.Select(p => p.Hash).ToArray();
            Transactions = transactions.ToDictionary(p => p.Hash);
            NextConsensus = Blockchain.GetConsensusAddress(Snapshot.GetValidators(transactions).ToArray());
            Timestamp = Math.Max(DateTime.UtcNow.ToTimestamp(), Snapshot.GetHeader(PrevHash).Timestamp + 1);
        }

        private static ulong GetNonce()
        {
            byte[] nonce = new byte[sizeof(ulong)];
            Random rand = new Random();
            rand.NextBytes(nonce);
            return nonce.ToUInt64(0);
        }

        public bool VerifyRequest()
        {
            if (!State.HasFlag(ConsensusState.RequestReceived))
                return false;
            if (!Blockchain.GetConsensusAddress(Snapshot.GetValidators(Transactions.Values).ToArray()).Equals(NextConsensus))
                return false;
            Transaction tx_gen = Transactions.Values.FirstOrDefault(p => p.Type == TransactionType.MinerTransaction);
            Fixed8 amount_netfee = Block.CalculateNetFee(Transactions.Values);
            if (tx_gen?.Outputs.Sum(p => p.Value) != amount_netfee) return false;
            return true;
        }
    }
}
