using Zoro.Network.P2P.Payloads;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Ledger
{
    internal class MemoryPool : IReadOnlyCollection<Transaction>
    {
        private readonly ConcurrentDictionary<UInt256, Transaction> _mem_pool = new ConcurrentDictionary<UInt256, Transaction>();

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
                return false;
            }

            _mem_pool.TryAdd(hash, tx);

            return true;
        }

        public bool TryRemove(UInt256 hash, out Transaction tx)
        {
            if (_mem_pool.TryRemove(hash, out tx))
            {
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
    }
}
