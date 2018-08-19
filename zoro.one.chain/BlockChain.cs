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
            System.Threading.Thread t = new System.Threading.Thread(TimerThread);
            t.IsBackground = true;//设置为后台线程，主程序退出这个线程就会玩完儿了，不用特别管他
            t.Start();
        }
        void TimerThread()
        {
            while(true)
            {
                System.Threading.Thread.Sleep(500);
                Tick();
            }
        }
        void Tick()
        {
            _Test_Add();
        }

        SHA256 sha256 = SHA256.Create();
        void InitBlock()
        {
            var snapshot = LevelDB.Ex.Helper.CreateSnapshot(db);
            byte[] key = System.Text.Encoding.ASCII.GetBytes("allblocks");
            var blocks = dbTable.GetItem(snapshot, key) as LevelDB.Ex.Map;
            if (blocks == null)
            {
                blocks = new LevelDB.Ex.Map();
                dbTable.PutItem(key, blocks);
            }
            snapshot = LevelDB.Ex.Helper.CreateSnapshot(db);
            if (blocks.Count(snapshot) == 0)
            {
                var batch = new LevelDB.Ex.WriteBatch(db);
                //写入创世块
                var blockzero = new LevelDB.Ex.Map();
                byte[] blockkey = BitConverter.GetBytes((UInt64)0);
                blocks.Batch_SetItem(batch, blockkey, blockzero);

                byte[] keydata = System.Text.Encoding.ASCII.GetBytes("data");
                blockzero.Batch_SetItem(batch, keydata, new LevelDB.Ex.Bytes(new byte[0]));

                byte[] keyhash = System.Text.Encoding.ASCII.GetBytes("hash");

                byte[] hash = sha256.ComputeHash(new byte[0]);
                blockzero.Batch_SetItem(batch, keyhash, new LevelDB.Ex.Bytes(hash));

                batch.Apply();
            }
        }
        public ulong GetBlockCount()
        {
            var snapshot = LevelDB.Ex.Helper.CreateSnapshot(db);
            byte[] key = System.Text.Encoding.ASCII.GetBytes("allblocks");
            var blocks = dbTable.GetItem(snapshot, key) as LevelDB.Ex.Map;
            if (blocks == null)
            {
                return 0;
            }
            return blocks.Count(snapshot);
        }
        public void _Test_Add()
        {
            var snapshot = LevelDB.Ex.Helper.CreateSnapshot(db);
            byte[] key = System.Text.Encoding.ASCII.GetBytes("allblocks");
            var blocks = dbTable.GetItem(snapshot, key) as LevelDB.Ex.Map;
            var blockcount = blocks.Count(snapshot);
            {
                var batch = new LevelDB.Ex.WriteBatch(db);
                //写入创世块
                var blockadd = new LevelDB.Ex.Map();
                byte[] blockkey = BitConverter.GetBytes((UInt64)blockcount);
                blocks.Batch_SetItem(batch, blockkey, blockadd);

                byte[] keydata = System.Text.Encoding.ASCII.GetBytes("data");
                blockadd.Batch_SetItem(batch, keydata, new LevelDB.Ex.Bytes(new byte[0]));

                byte[] keyhash = System.Text.Encoding.ASCII.GetBytes("hash");

                byte[] hash = sha256.ComputeHash(new byte[0]);
                blockadd.Batch_SetItem(batch, keyhash, new LevelDB.Ex.Bytes(hash));

                batch.Apply();
            }
        }
    }
}
