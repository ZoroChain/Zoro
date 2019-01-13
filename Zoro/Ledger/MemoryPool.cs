using Zoro.Network.P2P.Payloads;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Zoro.Ledger
{
    internal class MemoryPool
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

        private readonly ConcurrentDictionary<UInt256, Transaction> _verified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly ConcurrentDictionary<UInt256, Transaction> _unverified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly SortedSet<Index> _index_set = new SortedSet<Index>();

        public int Capacity { get; }
        public int Count => _verified.Count + _unverified.Count;
        public int VerifiedCount => _verified.Count;
        public int UnverifiedCount => _unverified.Count;

        public MemoryPool(int capacity)
        {
            Capacity = capacity;
        }

        public bool ContainsKey(UInt256 hash)
        {
            if (_verified.ContainsKey(hash))
                return true;
            else if (_unverified.ContainsKey(hash))
                return true;

            return false;
        }

        public bool TryAddVerified(Transaction tx)
        {
            if (Count >= Capacity)
            {
                if (!RemoveLowestFee(tx))
                    return false;
            }

            if (_verified.TryAdd(tx.Hash, tx))
            {
                AddIndex(tx);
                return true;
            }

            return false;
        }

        public bool TryAddUnverified(Transaction tx)
        {
            if (Count >= Capacity)
            {
                if (!RemoveLowestFee(tx))
                    return false;
            }

            if (_unverified.TryAdd(tx.Hash, tx))
            {
                AddIndex(tx);
                return true;
            }

            return false;
        }

        public bool TryRemoveVerified(UInt256 hash, out Transaction tx)
        {
            if (_verified.TryRemove(hash, out tx))
            {
                RemoveIndex(tx);
                return true;
            }
            else
            {
                tx = null;
                return false;
            }
        }

        public bool TryRemoveUnverified(UInt256 hash, out Transaction tx)
        {
            if (_unverified.TryRemove(hash, out tx))
            {
                RemoveIndex(tx);
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
            if (_verified.TryGetValue(hash, out tx))
            {
                return true;
            }
            else if (_unverified.TryGetValue(hash, out tx))
            {
                return true;
            }
            else
            {
                tx = null;
                return false;
            }
        }

        public bool TryGetVerified(UInt256 hash, out Transaction tx)
        {
            if (_verified.TryGetValue(hash, out tx))
            {
                return true;
            }
            else
            {
                tx = null;
                return false;
            }
        }

        public bool TryGetUnverified(UInt256 hash, out Transaction tx)
        {
            if (_unverified.TryGetValue(hash, out tx))
            {
                return true;
            }
            else
            {
                tx = null;
                return false;
            }
        }

        public void ClearVerified()
        {
            foreach (var tx in _verified.Values)
                RemoveIndex(tx);

            _verified.Clear();
        }

        public void ClearUnverified()
        {
            foreach (var tx in _unverified.Values)
                RemoveIndex(tx);

            _unverified.Clear();
        }

        public void ResetToUnverified()
        {
            _unverified.Union(_verified);
            _verified.Clear();
        }

        public IEnumerable<Transaction> GetVerified()
        {
            return _verified.Values;
        }

        public IEnumerable<Transaction> GetUnverified()
        {
            return _unverified.Values;
        }

        public IEnumerable<Transaction> GetAll()
        {
            return _verified.Values.Union(_unverified.Values);
        }

        private Fixed8 GetFeeRatio(Transaction tx)
        {
            return tx.SystemFee / tx.Size;
        }

        private void AddIndex(Transaction tx)
        {
            Index index = new Index { Hash = tx.Hash, Fee = tx.SystemFee, FeeRatio = GetFeeRatio(tx) };
            _index_set.Add(index);
        }

        private void RemoveIndex(Transaction tx)
        {
            Index index = new Index { Hash = tx.Hash, Fee = tx.SystemFee, FeeRatio = GetFeeRatio(tx) };
            _index_set.Remove(index);
        }

        private bool RemoveLowestFee(Transaction tx)
        {
            Index index = _index_set.First();

            if (index.FeeRatio < GetFeeRatio(tx))
            {
                _verified.TryRemove(index.Hash, out Transaction _);
                _unverified.TryRemove(index.Hash, out Transaction _);
                _index_set.Remove(index);
                return true;
            }

            return false;
        }
    }
}
