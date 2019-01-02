#pragma warning disable CS0612

using Zoro.IO.Caching;

namespace Zoro.Network.P2P.Payloads
{
    public enum TransactionType : byte
    {
        [ReflectionCache(typeof(MinerTransaction))]
        MinerTransaction = 0x00,
        [ReflectionCache(typeof(IssueTransaction))]
        IssueTransaction = 0x01,
        [ReflectionCache(typeof(RegisterTransaction))]
        RegisterTransaction = 0x40,
        [ReflectionCache(typeof(InvocationTransaction))]
        InvocationTransaction = 0xd1
    }
}
