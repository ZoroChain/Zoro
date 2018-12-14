namespace Zoro.Network.P2P.Payloads
{
    public enum AssetType : byte
    {
        NativeNEP5Token = 0x01,

        UtilityToken = NativeNEP5Token | 0x80,
    }
}
