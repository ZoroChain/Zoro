using System;

namespace Zoro.Wallets
{
    public class Coin : IEquatable<Coin>
    {
        public UInt256 AssetId;
        public Fixed8 Balance;

        public bool Equals(Coin other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            return AssetId.Equals(other.AssetId) && Balance.Equals(other.Balance);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Coin);
        }

        public override int GetHashCode()
        {
            return AssetId.GetHashCode() + Balance.GetHashCode();
        }
    }
}
