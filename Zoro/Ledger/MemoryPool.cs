using Zoro.Network.P2P.Payloads;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Zoro.Ledger
{
    public class MemoryPool
    {
        internal class SortedItem : IComparable<SortedItem>, IEquatable<SortedItem>
        {
            public Transaction tx;
            public bool verified;
            public bool reverse;

            public int CompareTo(SortedItem other)
            {
                int r = tx.FeePerByte.CompareTo(other.tx.FeePerByte); 
                if (r != 0) return reverse ? -r : r;

                r = tx.SystemFee.CompareTo(other.tx.SystemFee);
                if (r != 0) return reverse ? -r : r;
                
                r = tx.Hash.CompareTo(other.tx.Hash);
                if (r != 0) return reverse ? -r : r;

                return 0;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as SortedItem);
            }

            public bool Equals(SortedItem other)
            {
                if (ReferenceEquals(this, other)) return true;
                if (other is null) return false;
                return tx.Equals(other.tx);
            }

            public override int GetHashCode()
            {
                return tx.GetHashCode();
            }
        };

        private readonly ConcurrentDictionary<UInt256, Transaction> _verified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly ConcurrentDictionary<UInt256, Transaction> _unverified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly SortedSet<SortedItem> _item_sorted = new SortedSet<SortedItem>();
        private readonly SortedSet<SortedItem> _item_reverse_sorted = new SortedSet<SortedItem>();

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
            _item_sorted.Clear();
            _item_reverse_sorted.Clear();
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

        public IEnumerable<Transaction> TakeUnverifiedTransactions(int count)
        {
            return _item_reverse_sorted.Where(p => !p.verified).Select(p => p.tx).Take(count).ToArray();
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
                AddToSortedSet(tx, true);
                return true;
            }

            return false;
        }

        public bool TryRemove(UInt256 hash, out Transaction tx)
        {
            if (_verified.TryRemove(hash, out tx))
            {
                RemoveSortedItem(tx, true);
                return true;
            }
            else if(_unverified.TryRemove(hash, out tx))
            {
                RemoveSortedItem(tx, false);
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
                RemoveSortedItem(tx, false);
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

            foreach (var item in _item_sorted)
            {
                item.verified = false;
            }

            foreach (var item in _item_reverse_sorted)
            {
                item.verified = false;
            }
        }

        private void AddToSortedSet(Transaction tx, bool verified)
        {
            _item_sorted.Add(new SortedItem { tx = tx, verified = verified, reverse = false });
            _item_reverse_sorted.Add(new SortedItem { tx = tx, verified = verified, reverse = true });
        }

        private void RemoveSortedItem(Transaction tx, bool verified)
        {
            _item_sorted.Remove(new SortedItem { tx = tx, verified = verified, reverse = false });
            _item_reverse_sorted.Remove(new SortedItem { tx = tx, verified = verified, reverse = true });
        }

        private bool RemoveLowestFee(Transaction tx)
        {
            SortedItem item = _item_sorted.First();

            if (item != null && item.tx.FeePerByte < tx.FeePerByte)
            {
                _verified.TryRemove(item.tx.Hash, out Transaction _);
                _unverified.TryRemove(item.tx.Hash, out Transaction _);
                _item_sorted.Remove(item);
                return true;
            }

            return false;
        }
    }
}
