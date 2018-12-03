using Zoro.Cryptography.ECC;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Persistence
{
    public static class Helper
    {
        public static bool ContainsBlock(this IPersistence persistence, UInt256 hash)
        {
            BlockState state = persistence.Blocks.TryGet(hash);
            if (state == null) return false;
            return state.TrimmedBlock.IsBlock;
        }

        public static bool ContainsTransaction(this IPersistence persistence, UInt256 hash)
        {
            TransactionState state = persistence.Transactions.TryGet(hash);
            return state != null;
        }

        public static Block GetBlock(this IPersistence persistence, uint index)
        {
            UInt256 hash = persistence.Blockchain.GetBlockHash(index);
            if (hash == null) return null;
            return persistence.GetBlock(hash);
        }

        public static Block GetBlock(this IPersistence persistence, UInt256 hash)
        {
            BlockState state = persistence.Blocks.TryGet(hash);
            if (state == null) return null;
            if (!state.TrimmedBlock.IsBlock) return null;
            state.TrimmedBlock.ChainHash = persistence.Blockchain.ChainHash;
            Block block = state.TrimmedBlock.GetBlock(persistence.Transactions);
            block?.UpdateTransactionsChainHash();

            return block;
        }

        public static Header GetHeader(this IPersistence persistence, uint index)
        {
            UInt256 hash = persistence.Blockchain.GetBlockHash(index);
            if (hash == null) return null;
            return persistence.GetHeader(hash);
        }

        public static Header GetHeader(this IPersistence persistence, UInt256 hash)
        {
            return persistence.Blocks.TryGet(hash)?.TrimmedBlock.Header;
        }

        public static UInt256 GetNextBlockHash(this IPersistence persistence, UInt256 hash)
        {
            BlockState state = persistence.Blocks.TryGet(hash);
            if (state == null) return null;
            return persistence.Blockchain.GetBlockHash(state.TrimmedBlock.Index + 1);
        }

        public static long GetSysFeeAmount(this IPersistence persistence, uint height)
        {
            return persistence.GetSysFeeAmount(persistence.Blockchain.GetBlockHash(height));
        }

        public static long GetSysFeeAmount(this IPersistence persistence, UInt256 hash)
        {
            BlockState block_state = persistence.Blocks.TryGet(hash);
            if (block_state == null) return 0;
            return block_state.SystemFeeAmount;
        }

        public static Transaction GetTransaction(this IPersistence persistence, UInt256 hash)
        {
            Transaction tx = persistence.Transactions.TryGet(hash)?.Transaction;
            if (tx != null && persistence.Blockchain != null)
            {
                tx.ChainHash = persistence.Blockchain.ChainHash;
            }
            return tx;
        }

        public static TransactionOutput GetUnspent(this IPersistence persistence, UInt256 hash, ushort index)
        {
            UnspentCoinState state = persistence.UnspentCoins.TryGet(hash);
            if (state == null) return null;
            if (index >= state.Items.Length) return null;
            if (state.Items[index].HasFlag(CoinState.Spent)) return null;
            return persistence.GetTransaction(hash).Outputs[index];
        }

        public static IEnumerable<TransactionOutput> GetUnspent(this IPersistence persistence, UInt256 hash)
        {
            List<TransactionOutput> outputs = new List<TransactionOutput>();
            UnspentCoinState state = persistence.UnspentCoins.TryGet(hash);
            if (state != null)
            {
                Transaction tx = persistence.GetTransaction(hash);
                for (int i = 0; i < state.Items.Length; i++)
                    if (!state.Items[i].HasFlag(CoinState.Spent))
                        outputs.Add(tx.Outputs[i]);
            }
            return outputs;
        }
    }
}
