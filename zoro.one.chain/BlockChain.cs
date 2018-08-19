using System;
using System.Security.Cryptography;

namespace zoro.one.chain
{
    public class BlockChain
    {
        LevelDB.DB db = null;
        LevelDB.Ex.Table dbTable = null;
        byte[] blockChainMagic = null;
        public void Start(string dbPath, byte[] magic)
        {
            var curlibVersion = this.GetType().Assembly.GetName().Version;
            Console.WriteLine("zoro.one V" + curlibVersion);

            db = LevelDB.Ex.Helper.OpenDB(dbPath);

            this.blockChainMagic = magic;

            dbTable = new LevelDB.Ex.Table(db, magic);

            InitBlock();
        }
        SHA256 sha256 = SHA256.Create();
        void InitBlock()
        {
            var snapshot = LevelDB.Ex.Helper.CreateSnapshot(db);
            byte[] key = System.Text.Encoding.ASCII.GetBytes("blockcount");
            var blocks = dbTable.GetItem(snapshot, key) as LevelDB.Ex.Map;
            if (blocks == null)
            {
                blocks = new LevelDB.Ex.Map();
                dbTable.PutItem(key, blocks);
            }
            snapshot = LevelDB.Ex.Helper.CreateSnapshot(db);
            if (blocks.Count(snapshot) == 0)
            {
                var batch = new LevelDB.WriteBatch();
                //写入创世块
                var blockzero = new LevelDB.Ex.Map();
                byte[] blockkey = BitConverter.GetBytes((UInt64)0);
                blocks.Batch_SetItem(batch, blockkey, blockzero);

                byte[] keydata = System.Text.Encoding.ASCII.GetBytes("data");
                blockzero.Batch_SetItem(batch, keydata, new LevelDB.Ex.Bytes(new byte[0]));

                byte[] keyhash = System.Text.Encoding.ASCII.GetBytes("hash");

                byte[] hash = sha256.ComputeHash(new byte[0]);
                blockzero.Batch_SetItem(batch, keyhash, new LevelDB.Ex.Bytes(hash));

                db.Write(batch);
            }
        }
    }
}
