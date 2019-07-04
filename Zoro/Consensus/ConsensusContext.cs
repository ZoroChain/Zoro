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
using System.IO;

namespace Zoro.Consensus
{
    internal class ConsensusContext : IDisposable, ISerializable
    {
        /// <summary>
        /// Prefix for saving consensus state.
        /// </summary>
        public const byte CN_Context = 0xf4;
        public Block Block;

        public int Size => throw new NotImplementedException();
        public ConsensusPayload[] PreparationPayloads;
        public ConsensusPayload[] CommitPayloads;
        public ConsensusPayload[] ChangeViewPayloads;
        public ConsensusPayload[] LastChangeViewPayloads;
        // LastSeenMessage array stores the height of the last seen message, for each validator.
        // if this node never heard from validator i, LastSeenMessage[i] will be -1.
        public int[] LastSeenMessage;

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
            Contract contract = Contract.CreateMultiSigContract(M, Validators);
            ContractParametersContext sc = new ContractParametersContext(Block, blockchain);
            for (int i = 0, j = 0; i < Validators.Length && j < M; i++)
            {
                if (CommitPayloads[i]?.ConsensusMessage.ViewNumber != ViewNumber) continue;
                sc.AddSignature(contract, Validators[i], CommitPayloads[i].GetDeserializedMessage<Commit>().Signature);
                j++;
               
            }
            Block.Witness = sc.GetWitnesses()[0];
            Block.Transactions = TransactionHashes.Select(p => Transactions[p]).ToArray();
            return Block;
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

        public bool Load()
        {
            byte[] data = blockchain.Store.Get(CN_Context, new byte[0]);
            if (data is null || data.Length == 0) return false;
            using (MemoryStream ms = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                try
                {
                    Deserialize(reader);
                }
                catch
                {
                    return false;
                }
                return true;
            }
        }

        public void Reset(byte viewNumber)
        {
            if (viewNumber == 0)
            {
                snapshot?.Dispose();
                snapshot = blockchain.GetSnapshot();
                Block = new Block
                {
                    PrevHash = snapshot.CurrentBlockHash,
                    Index = snapshot.Height + 1,
                    NextConsensus = Blockchain.GetConsensusAddress(snapshot.GetValidators().ToArray()),
                    ConsensusData = new ConsensusData()
                };
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

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Block.Version);
            writer.Write(Block.Index);
            writer.Write(Block.Timestamp);
            writer.Write(Block.NextConsensus ?? UInt160.Zero);
            writer.Write(Block.ConsensusData);
            writer.Write(ViewNumber);
            writer.Write(TransactionHashes ?? new UInt256[0]);
            writer.Write(Transactions?.Values.ToArray() ?? new Transaction[0]);
            writer.WriteVarInt(PreparationPayloads.Length);
            foreach (var payload in PreparationPayloads)
            {
                bool hasPayload = !(payload is null);
                writer.Write(hasPayload);
                if (!hasPayload) continue;
                writer.Write(payload);
            }
            writer.WriteVarInt(CommitPayloads.Length);
            foreach (var payload in CommitPayloads)
            {
                bool hasPayload = !(payload is null);
                writer.Write(hasPayload);
                if (!hasPayload) continue;
                writer.Write(payload);
            }
            writer.WriteVarInt(ChangeViewPayloads.Length);
            foreach (var payload in ChangeViewPayloads)
            {
                bool hasPayload = !(payload is null);
                writer.Write(hasPayload);
                if (!hasPayload) continue;
                writer.Write(payload);
            }
            writer.WriteVarInt(LastChangeViewPayloads.Length);
            foreach (var payload in LastChangeViewPayloads)
            {
                bool hasPayload = !(payload is null);
                writer.Write(hasPayload);
                if (!hasPayload) continue;
                writer.Write(payload);
            }
        }

        public void Deserialize(BinaryReader reader)
        {
            Reset(0);
            if (reader.ReadUInt32() != Block.Version) throw new FormatException();
            if (reader.ReadUInt32() != Block.Index) throw new InvalidOperationException();
            Block.Timestamp = reader.ReadUInt32();
            Block.NextConsensus = reader.ReadSerializable<UInt160>();
            if (Block.NextConsensus.Equals(UInt160.Zero))
                Block.NextConsensus = null;
            Block.ConsensusData = reader.ReadSerializable<ConsensusData>();
            ViewNumber = reader.ReadByte();
            TransactionHashes = reader.ReadSerializableArray<UInt256>();
            if (TransactionHashes.Length == 0)
                TransactionHashes = null;
            Transaction[] transactions = reader.ReadSerializableArray<Transaction>(Block.MaxTransactionsPerBlock);
            Transactions = transactions.Length == 0 ? null : transactions.ToDictionary(p => p.Hash);
            PreparationPayloads = new ConsensusPayload[reader.ReadVarInt(Blockchain.MaxValidators)];
            for (int i = 0; i < PreparationPayloads.Length; i++)
                PreparationPayloads[i] = reader.ReadBoolean() ? reader.ReadSerializable<ConsensusPayload>() : null;
            CommitPayloads = new ConsensusPayload[reader.ReadVarInt(Blockchain.MaxValidators)];
            for (int i = 0; i < CommitPayloads.Length; i++)
                CommitPayloads[i] = reader.ReadBoolean() ? reader.ReadSerializable<ConsensusPayload>() : null;
            ChangeViewPayloads = new ConsensusPayload[reader.ReadVarInt(Blockchain.MaxValidators)];
            for (int i = 0; i < ChangeViewPayloads.Length; i++)
                ChangeViewPayloads[i] = reader.ReadBoolean() ? reader.ReadSerializable<ConsensusPayload>() : null;
            LastChangeViewPayloads = new ConsensusPayload[reader.ReadVarInt(Blockchain.MaxValidators)];
            for (int i = 0; i < LastChangeViewPayloads.Length; i++)
                LastChangeViewPayloads[i] = reader.ReadBoolean() ? reader.ReadSerializable<ConsensusPayload>() : null;
        }
    }
}
