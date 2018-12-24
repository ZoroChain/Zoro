namespace Zoro.Network.P2P.Payloads
{
    public enum AssetType : byte
    {
        GlobalToken = 0x01,

        UtilityToken = GlobalToken | 0x80,
    }
}
