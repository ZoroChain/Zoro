using LevelDB.Ex;
using System;
using System.Collections.Generic;
using System.Linq;
namespace leveldb_EX_test
{
    class Program
    {
        static Dictionary<string, Action> menuacts = new System.Collections.Generic.Dictionary<string, Action>();
        static Dictionary<string, string> menus = new Dictionary<string, string>();
        static void InitMenu()
        {
            menus.Add("1", "TestBytes");
            menuacts.Add("1", Test_Bytes);

            menus.Add("2", "Test_Map_New");
            menuacts.Add("2", Test_Map_New);

            menus.Add("3", "Test_Map_Get");
            menuacts.Add("3", Test_Map_Get);

            menus.Add("4", "Test_Map_Insert");
            menuacts.Add("4", Test_Map_Insert);
        }
        static void Main(string[] args)
        {
            Console.WriteLine("NeoDB Test 0.01");
            InitMenu();

            Show_Menu();
            while (true)
            {
                Console.Write("->");
                var info = Console.ReadLine();
                var line = info.Replace(" ", "").ToLower();
                if (menuacts.ContainsKey(line))
                {
                    menuacts[line]();
                }
                else if (line == "?" || line == "help")
                {
                    Show_Menu();
                }
            }

        }
        static void Show_Menu()
        {
            foreach (var m in menus)
            {
                Console.WriteLine(m.Key + ":" + m.Value);
            }
        }
        static void Test_Bytes()
        {
            var dbex = Helper.OpenDB("c:\\testdb");
            var read = Helper.CreateSnapshot(dbex);

            var data = new byte[16];
            Random r = new Random();
            r.NextBytes(data);

            var key = new byte[] { 0x01, 0x02 };

            var table = Helper.GetTable(dbex, new byte[] { 0x03 });
            var table2 = Helper.GetTable(dbex, new byte[] { 0x04 });

            var batch = new LevelDB.WriteBatch();
            //dbex.BeginBatch();
            var item = new Bytes(data);
            table.PutItem(key, item);
            r.NextBytes(data);
            table2.PutItem(key, item);

            //dbex.WriteBatch(batch);
            //dbex.EndBatch();

            var read2 = Helper.CreateSnapshot(dbex);

            var get1 = table.GetItem(read2, key) as Bytes;
            var get2 = table2.GetItem(read2, key) as Bytes;
            Console.WriteLine("get1=" + Helper.Hex2Str(get1.Value));
            Console.WriteLine("get2=" + Helper.Hex2Str(get2.Value));

            dbex.Dispose();
        }
        static void Test_Map_New()
        {
            using (var dbex = Helper.OpenDB("c:\\testdb"))
            {
                var read = Helper.CreateSnapshot(dbex);
                var map = new Map();
                var table = Helper.GetTable(dbex, new byte[] { 0x11, 0x22 });
                table.PutItem(new byte[] { 0x01, 0x02 }, map);
                Console.WriteLine("map.inst=" + Helper.Hex2Str(map.Value));
            }
        }
        static void Test_Map_Get()
        {
            using (var dbex = Helper.OpenDB("c:\\testdb"))
            {
                var table = Helper.GetTable(dbex, new byte[] { 0x11, 0x22 });
                var snapshot = Helper.CreateSnapshot(dbex);
                var map = table.GetItem(snapshot, new byte[] { 0x01, 0x02 }) as Map;
                var count = map.Count(snapshot);
                Console.WriteLine("map.count=" + count);
                Console.WriteLine("map.inst=" + Helper.Hex2Str(map.Value));

                var it = map.GetIterator(snapshot);
                byte[] head = map.GetBeginSeek();
                Console.WriteLine("head=" + Helper.Hex2Str(head));


                while (it.IsVaild)
                {
                    byte[] key = it.Key;
                    var value = it.Value;
                    it.Next();
                    Console.WriteLine(Helper.Hex2Str(key) + "-->(" + value.type + ")" + Helper.Hex2Str(value.Value));
                }
            }
        }
        static void Test_Map_Insert()
        {
            using (var dbex = Helper.OpenDB("c:\\testdb"))
            {
                var table = Helper.GetTable(dbex, new byte[] { 0x11, 0x22 });
                var read = Helper.CreateSnapshot(dbex);
                var map = table.GetItem(read, new byte[] { 0x01, 0x02 }) as Map;

                var data = new byte[8];
                Random r = new Random();
                r.NextBytes(data);
                var bytes = new Bytes(data);
                map.SetItem(new byte[] { 0x0a }, bytes);
                var count = map.Count(read);
                Console.WriteLine("map.count=" + count);
                Console.WriteLine("map.inst=" + Helper.Hex2Str(map.Value));
            }
        }
    }
}
