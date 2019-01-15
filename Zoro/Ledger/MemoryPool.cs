using Zoro.Network.P2P.Payloads;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Zoro.Ledger
{
    public class MemoryPool
    {
        internal class AscendingSortedItem : IComparable<AscendingSortedItem>
        {
            public Transaction tx;

            public int CompareTo(AscendingSortedItem other)
            {
                int r = tx.FeePerByte.CompareTo(other.tx.FeePerByte); 
                if (r != 0) return r;

                r = tx.SystemFee.CompareTo(other.tx.SystemFee);
                if (r != 0) return r;
                
                return tx.Hash.CompareTo(other.tx.Hash);
            }
        };

        internal class DescendingSortedItem : IComparable<DescendingSortedItem>
        {
            public Transaction tx;

            public int CompareTo(DescendingSortedItem other)
            {
                int r = tx.FeePerByte.CompareTo(other.tx.FeePerByte);
                if (r != 0) return -r;

                r = tx.SystemFee.CompareTo(other.tx.SystemFee);
                if (r != 0) return -r;

                r = tx.Hash.CompareTo(other.tx.Hash);
                if (r != 0) return -r;

                return 0;
            }
        };

        private readonly ConcurrentDictionary<UInt256, Transaction> _verified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly ConcurrentDictionary<UInt256, Transaction> _unverified = new ConcurrentDictionary<UInt256, Transaction>();
        private readonly SortedSet<AscendingSortedItem> _ascending_order_items = new SortedSet<AscendingSortedItem>();
        private readonly SortedSet<DescendingSortedItem> _descending_order_items = new SortedSet<DescendingSortedItem>();

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
            _ascending_order_items.Clear();
            _descending_order_items.Clear();
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

        public Transaction[] TakeUnverifiedTransactions(int count)
        {
            IEnumerable<AscendingSortedItem> items = _ascending_order_items.Take(count);

            Transaction[] txns = items.Select(p => p.tx).ToArray();

            foreach (var item in items.ToArray())
            {
                _ascending_order_items.Remove(item);
            }

            return txns;
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

        public void SetVerifyState(UInt256 hash, bool verifyResult)
        {
            if (_unverified.TryRemove(hash, out Transaction tx))
            {
                if (verifyResult)
                {
                    _verified.TryAdd(hash, tx);
                    AddToSortedSet(tx, true);
                }
                else
                {
                    RemoveSortedItem(tx, false);
                }
            }
        }

        private void AddToSortedSet(Transaction tx)
        {
            _ascending_order_items.Add(new AscendingSortedItem { tx = tx });
            _descending_order_items.Add(new DescendingSortedItem { tx = tx });
        }

        private void AddToSortedSet(Transaction tx, bool ascending)
        {
            if (ascending)
                _ascending_order_items.Add(new AscendingSortedItem { tx = tx });
            else
                _descending_order_items.Add(new DescendingSortedItem { tx = tx });
        }

        private void RemoveSortedItem(Transaction tx)
        {
            _ascending_order_items.Remove(new AscendingSortedItem { tx = tx });
            _descending_order_items.Remove(new DescendingSortedItem { tx = tx });
        }

        private void RemoveSortedItem(Transaction tx, bool ascending)
        {
            if (ascending)
                _ascending_order_items.Remove(new AscendingSortedItem { tx = tx });
            else
                _descending_order_items.Remove(new DescendingSortedItem { tx = tx });
        }

        private bool RemoveLowestFee(Transaction tx)
        {
            DescendingSortedItem item = _descending_order_items.Min;

            if (item != null && item.tx.FeePerByte < tx.FeePerByte)
            {
                TryRemove(item.tx.Hash, out _);
                return true;
            }

            return false;
        }
    }
}
