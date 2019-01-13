using Zoro.Network.P2P.Payloads;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Zoro.Ledger
{
    internal class MemoryPool : IReadOnlyCollection<Transaction>
    {
        internal class Index : IComparable<Index>
        {
            public UInt256 Hash;
            public Fixed8 FeeRatio;
            public Fixed8 Fee;

            public int CompareTo(Index other)
            {
                int r = FeeRatio.CompareTo(other.FeeRatio); 
                if (r != 0) return r;

                r = Fee.CompareTo(other.Fee);
                if (r != 0) return r;
                
                return Hash.CompareTo(other.Hash);
            }
        };

        private readonly ConcurrentDictionary<UInt256, Transaction> _mem_pool = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly SortedSet<Index> _index_set = new SortedSet<Index>();

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
            if (Count >= Capacity)
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
                RemoveIndex(hash, tx);
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

        private Fixed8 GetFeeRatio(Transaction tx)
        {
            return tx.SystemFee / tx.Size;
        }

        private void AddIndex(UInt256 hash, Transaction tx)
        {
            Index index = new Index { Hash = hash, Fee = tx.SystemFee, FeeRatio = GetFeeRatio(tx) };
            _index_set.Add(index);
        }

        private void RemoveIndex(UInt256 hash, Transaction tx)
        {
            Index index = new Index { Hash = hash, Fee = tx.SystemFee, FeeRatio = GetFeeRatio(tx) };
            _index_set.Remove(index);
        }

        private bool RemoveLowestFee(Transaction tx)
        {
            Index index = _index_set.First();

            if (index.FeeRatio < GetFeeRatio(tx))
            {
                _mem_pool.TryRemove(index.Hash, out Transaction _);
                _index_set.Remove(index);
                return true;
            }

            return false;
        }
    }
}
