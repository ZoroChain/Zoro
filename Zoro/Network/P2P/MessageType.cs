namespace Zoro.Network.P2P
{
    public class MessageType
    {
        public static string Version = "version";
        public static string VerAck = "verack";
        public static string GetAddr = "getaddr";
        public static string Addr = "addr";
        public static string GetHeaders = "getheaders";
        public static string Headers = "headers";
        public static string GetBlocks = "getblocks";
        public static string GetData = "getdata";
        public static string GetTxn = "gettxn";
        public static string GetBlk = "getblk";
        public static string Inv = "inv";
        public static string Tx = "tx";
        public static string Block = "block";
        public static string Consensus = "consensus";
        public static string MerkleBlock = "merkleblock";
        public static string RawTxn = "rawtxn";
        public static string MemPool = "mempool";
        public static string FilterAdd = "filteradd";
        public static string FilterClear = "filterclear";
        public static string FilterLoad = "filterload";
        public static string Alert = "alert";
        public static string NotFound = "notfound";
        public static string Ping = "ping";
        public static string Pong = "pong";
        public static string Reject = "reject";
    }
}
