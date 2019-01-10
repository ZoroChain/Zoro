using Zoro.Network.P2P.Payloads;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Ledger
{
    internal class MemoryPool : IReadOnlyCollection<Transaction>
    {
        internal class Index
        {
            public UInt256 Hash;
            public Fixed8 FeeRatio;
            public Fixed8 Fee;
        };

        private readonly ConcurrentDictionary<UInt256, Transaction> _mem_pool = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly List<Index> _index_list = new List<Index>();

        public int Capacity { get; }
        public int Count => _mem_pool.Count;

        public MemoryPool(int capacity)
        {
            Capacity = capacity;
        }

        public void Clear()
        {
            _mem_pool.Clear();
        }

        public bool ContainsKey(UInt256 hash) => _mem_pool.ContainsKey(hash);

        public IEnumerator<Transaction> GetEnumerator()
        {
            return _mem_pool.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryAdd(UInt256 hash, Transaction tx)
        {
            if (Count > Capacity)
            {
                if (!RemoveLowestFee(tx))
                    return false;
            }

            _mem_pool.TryAdd(hash, tx);

            AddIndex(hash, tx);

            return true;
        }

        public bool TryRemove(UInt256 hash, out Transaction tx)
        {
            if (_mem_pool.TryRemove(hash, out tx))
            {
                RemoveIndex(hash);
                return true;
            }
            else
            {
                tx = null;
                return false;
            }
        }

        public bool TryGetValue(UInt256 hash, out Transaction tx)
        {
            if (_mem_pool.TryGetValue(hash, out tx))
            {
                return true;
            }
            else
            {
                tx = null;
                return false;
            }
        }

        public Transaction[] GetTransactions(int count)
        {
            if (count > 0)
            {
                return _mem_pool.Values.Take(count).ToArray();
            }
            else
            {
                return _mem_pool.Values.ToArray();
            }
        }

        private void AddIndex(UInt256 hash, Transaction tx)
        {
            _index_list.Add(new Index { Hash = hash, Fee = tx.SystemFee, FeeRatio = GetFeeRatio(tx) });

            _index_list.OrderBy(p => p.FeeRatio).ThenBy(p => p.Fee);
        }

        private void RemoveIndex(UInt256 hash)
        {
            foreach (var index in _index_list)
            {
                if (index.Hash.Equals(hash))
                {
                    _index_list.Remove(index);
                    break;
                }
            }
        }

        private Fixed8 GetFeeRatio(Transaction tx)
        {
            return tx.SystemFee / tx.Size;
        }

        private bool RemoveLowestFee(Transaction tx)
        {
            Index index = _index_list.First();

            if (index.FeeRatio < GetFeeRatio(tx))
            {
                TryRemove(index.Hash, out Transaction _);
                RemoveIndex(index.Hash);
                return true;
            }

            return false;
        }
    }
}
