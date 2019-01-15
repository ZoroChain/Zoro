using Zoro.Network.P2P.Payloads;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Zoro.Ledger
{
    public class MemoryPool
    {
        internal class SortedItem : IComparable<SortedItem>
        {
            public Transaction tx;

            public int CompareTo(SortedItem other)
            {
                int r = tx.FeePerByte.CompareTo(other.tx.FeePerByte); 
                if (r != 0) return r;

                r = tx.SystemFee.CompareTo(other.tx.SystemFee);
                if (r != 0) return r;
                
                return tx.Hash.CompareTo(other.tx.Hash);
            }
        };

        private readonly ConcurrentDictionary<UInt256, Transaction> _verified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly ConcurrentDictionary<UInt256, Transaction> _unverified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly SortedSet<SortedItem> _sorted_items = new SortedSet<SortedItem>();
        private readonly HashSet<UInt256> _reverifying_items = new HashSet<UInt256>();

        public int Capacity { get; }
        public int Count => _verified.Count + _unverified.Count;
        public int VerifiedCount => _verified.Count;
        public int UnverifiedCount => _unverified.Count;
        public bool HasVerified => !_verified.IsEmpty;
        public bool HasUnverified => !_unverified.IsEmpty;

        public MemoryPool(int capacity)
        {
            Capacity = capacity;
        }
        
        public void Clear()
        {
            _verified.Clear();
            _unverified.Clear();
            _sorted_items.Clear();
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
                AddToSortedSet(tx);
                return true;
            }

            return false;
        }

        public bool TryRemove(UInt256 hash, out Transaction tx)
        {
            if (_verified.TryRemove(hash, out tx) || _unverified.TryRemove(hash, out tx))
            {
                RemoveSortedItem(tx);
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

        public void ResetToUnverified()
        {
            if (_verified.IsEmpty)
                return;

            foreach (var item in _verified)
            {
                _unverified.TryAdd(item.Key, item.Value);
            }
            
            _verified.Clear();
        }

        public Transaction[] TakeUnverifiedTransactions(int count)
        {
            return _sorted_items.Where(p => !_reverifying_items.Contains(p.tx.Hash) && _unverified.ContainsKey(p.tx.Hash))
                .Take(count)
                .Select(p => {
                    _reverifying_items.Add(p.tx.Hash);
                    return p.tx;
                }).ToArray();
        }

        public bool SetVerifyState(UInt256 hash, bool verifyResult)
        {
            _reverifying_items.Remove(hash);

            if (_unverified.TryRemove(hash, out Transaction tx))
            {
                if (verifyResult)
                {
                    _verified.TryAdd(hash, tx);
                }

                return true;
            }

            return false;            
        }

        private void AddToSortedSet(Transaction tx)
        {
            _sorted_items.Add(new SortedItem { tx = tx });
        }

        private void RemoveSortedItem(Transaction tx)
        {
            _sorted_items.Remove(new SortedItem { tx = tx });
        }

        private bool RemoveLowestFee(Transaction tx)
        {
            SortedItem min = _sorted_items.Min;

            if (min != null && min.tx.FeePerByte < tx.FeePerByte)
            {
                TryRemove(min.tx.Hash, out _);
                return true;
            }

            return false;
        }
    }
}
