using System;

namespace rocksdb_sharp_test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("meebey LevelDB test.");
            //var op = new LevelDB.Options() { CreateIfMissing = true, Compression = LevelDB.CompressionType.SnappyCompression };

            var op = new RocksDbSharp.DbOptions();
            op.SetCreateIfMissing(true);
            op.SetCompression(RocksDbSharp.CompressionTypeEnum.rocksdb_snappy_compression);
            op.SetMaxLogFileSize ( 4 * 1024 * 1024);
            // Neo.IO.Data.LevelDB.Options() { CreateIfMissing = true, Compression = Neo.IO.Data.LevelDB.CompressionType.kSnappyCompression };
            var db = RocksDbSharp.RocksDb.Open(op, "c:/newdb_neo_r");
            //var db2 = Neo.IO.Data.LevelDB.DB.Open("c:/newdb_neo2", op);
            int count = 0;
            Random r = new Random();
            DateTime timeBegin = DateTime.Now;
            //
            var wop = new RocksDbSharp.WriteOptions();
            //wop.SetSync(false);
            for (var i = 0; i < 1000000; i++)
            {
                byte[] testkey = System.Text.Encoding.UTF8.GetBytes("testkey" + count);
                byte[] testv = new byte[1024];
                r.NextBytes(testv);
                db.Put(testkey, testv);//,null,wop);
                if (i % 10000 == 0)
                {
                    Console.WriteLine("had put " + (i + 1));
                }
            }
            DateTime timeEnd = DateTime.Now;
            var totalseconds = (timeEnd - timeBegin).TotalSeconds;
            var speed = 1000000.0 / totalseconds;
            Console.WriteLine("put speed=" + speed + " per second");

            Console.ReadLine();
        }
    }
}
