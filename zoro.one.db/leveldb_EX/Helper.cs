using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LevelDB.Ex
{

    public enum Key_DataType : byte
    {
        ItemData = 0x01,//普通数据，byte[]
        //Map = 0x11,//字典，value=InstanceID  0x11+tabelname+0x00+mapname
        Map_Count = 0x10,//字典的值数量 0x12+InstanceID(固定长度)
        Map_Values,//字典的值,如果写入一个不存在的值，count+1,delete一个存在的值，count-1
        //0x13+InstanceID+Key

        InstanceMax = 0x20,//所有的引用对象池，随机分配，64bit,不会给重复的
                           //一个map是一个Instance，使用这个机制是为了方便DeleteMap或者RenameMap

    }

    public class WriteBatch
    {
        LevelDB.ReadOptions snapshot;
        LevelDB.DB db;
        LevelDB.WriteBatch wb;
        System.Collections.Concurrent.ConcurrentDictionary<System.Numerics.BigInteger, byte[]> cache;
        public WriteBatch(LevelDB.DB _db, LevelDB.ReadOptions _snapshot = null)
        {
            this.db = _db;
            this.snapshot = _snapshot;
            if (this.snapshot == null)
            {
                this.snapshot = Helper.CreateSnapshot(db);
            }
            this.wb = new LevelDB.WriteBatch();
            cache = new System.Collections.Concurrent.ConcurrentDictionary<System.Numerics.BigInteger, byte[]>();
        }
        public void Put(byte[] key, byte[] value)
        {
            System.Numerics.BigInteger nkey = new System.Numerics.BigInteger(key);
            cache[nkey] = value;
            this.wb.Put(key, value);
        }
        public void Delete(byte[] key)
        {
            System.Numerics.BigInteger nkey = new System.Numerics.BigInteger(key);
            this.wb.Delete(key);
            cache.Remove(nkey, out byte[] v);
        }
        public byte[] Get(byte[] key)
        {
            System.Numerics.BigInteger nkey = new System.Numerics.BigInteger(key);
            if (cache.ContainsKey(nkey))
                return cache[nkey];
            return db.Get(snapshot, key);
        }
        public void Apply()
        {
            db.Write(wb);
        }
    }

    /// <summary>
    /// LevelDB只提供了简单的KeyValue操作，和读快照与批次写入这些操作
    /// 缺乏一些数据结构的支持，使用起来还是有一些不方便
    /// </summary>
    public class Helper
    {
        public static readonly byte[] tagZero = new byte[] { 0x00 };
        public static readonly byte[] tagKey_Item = new byte[] { (byte)Key_DataType.ItemData };
        public static readonly byte[] tagKey_MapCount = new byte[] { (byte)Key_DataType.Map_Count };
        public static readonly byte[] tagKey_MapValues = new byte[] { (byte)Key_DataType.Map_Values };
        public static readonly byte[] tagKey_InstanceMax = new byte[] { (byte)Key_DataType.InstanceMax };

        public static readonly byte[] tagValue_Bytes = new byte[] { (byte)Value_DataType.Bytes };
        public static readonly byte[] tagValue_Map = new byte[] { (byte)Value_DataType.Map };

        public static IValue CreateValue(LevelDB.DB db, byte[] data)
        {

            if (data == null || data.Length == 0)
                return null;
            IValue value = null;
            if (data[0] == (byte)Value_DataType.Bytes)
            {
                value = new Bytes();
            }
            if (data[0] == (byte)Value_DataType.Map)
            {
                value = new Map();
            }
            if (value == null)
            {
                throw new Exception("unknown datatype.");
            }
            (value as IValueCreator).Init(db, data);
            return value;

        }
        public static LevelDB.DB OpenDB(string path)
        {
            var op = new LevelDB.Options() { CreateIfMissing = true, Compression = LevelDB.CompressionType.SnappyCompression };
            return LevelDB.DB.Open(op, path);
        }
        public static LevelDB.ReadOptions CreateSnapshot(LevelDB.DB db)
        {
            return new LevelDB.ReadOptions() { Snapshot = db.CreateSnapshot() };
        }
        //table不是数据结构，只是给存储的数据加上前缀
        public static Table GetTable(LevelDB.DB db, byte[] tablename)
        {
            if (tablename == null || tablename.Length == 0)
            {
                return new Table(db, new byte[0]);
            }
            if (tablename.Contains((byte)0x00))
                throw new Exception("not a vaild tablename");
            return new Table(db, tablename);
        }
        public static string Hex2Str(byte[] data)
        {
            var outstr = "";
            if (data != null)
            {
                foreach (var b in data)
                {
                    outstr += b.ToString("X02");
                }
            }
            return outstr;
        }
    }
}
