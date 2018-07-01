using System;

namespace neo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Neo LevelDB test.");
            var db = Neo.IO.Data.LevelDB.DB.Open("c:/newdb_neo", new Neo.IO.Data.LevelDB.Options() { CreateIfMissing = true });
            int count = 0;
            Random r = new Random();
            DateTime timeBegin = DateTime.Now;
            for (var i = 0; i < 1000000; i++)
            {
                byte[] testkey = System.Text.Encoding.UTF8.GetBytes("testkey" + count);
                byte[] testv = new byte[1024];
                r.NextBytes(testv);
                db.Put(Neo.IO.Data.LevelDB.WriteOptions.Default, testkey, testv);
                if (i % 10000 == 0)
                {
                    Console.WriteLine("had put " + (i + 1));
                }
            }
            DateTime timeEnd = DateTime.Now;
            var totalseconds = (timeEnd - timeBegin).TotalSeconds;
            var speed = 1000000.0 / totalseconds;
            Console.WriteLine("put speed=" + speed + " per second");
        }
    }
}
