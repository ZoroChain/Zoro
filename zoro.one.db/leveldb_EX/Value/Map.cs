using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LevelDB.Ex
{
    public class MapIterator
    {
        LevelDB.DB db;
        LevelDB.ReadOptions snapshot;
        byte[] head;
        LevelDB.Iterator it;

        public MapIterator(LevelDB.DB db, LevelDB.ReadOptions snapshot, byte[] head)
        {
            this.db = db;
            this.snapshot = snapshot;
            this.head = head;

            this.it = new LevelDB.Iterator(db, snapshot);
            it.Seek(head);
        }
        public byte[] Key
        {
            get
            {
                if (!IsVaild)
                    return null;

                return it.Key.Skip(head.Length).ToArray();
            }
        }
        public IValue Value
        {
            get
            {
                if (!IsVaild)
                    return null;
                return Helper.CreateValue(db, it.Value);
            }
        }
        public bool IsVaild
        {
            get
            {
                if (!it.IsValid) return false;

                byte[] key = it.Key;
                if (key.Length < head.Length)
                    return false;

                for (var i = 0; i < head.Length; i++)
                {
                    if (key[i] != head[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
        public void Next()
        {
            it.Next();
        }
    }

    /// <summary>
    /// 可以遍历keys的数据结构，未实现完全
    /// </summary>
    public class Map : IMap, IValueCreator
    {
        public Value_DataType type => Value_DataType.Map;

        /// <summary>
        /// Map的value 是一个64位的instanceID
        /// </summary>
        public byte[] Value
        {
            get;
            private set;
        }
        LevelDB.DB db;
        public Map()
        {
        }
        public void Init(LevelDB.DB db, byte[] data)
        {
            this.db = db;
            if (data == null || data.Length == 0)
                throw new Exception("error map in Init");

            var type = (Value_DataType)data[0];
            if (type != Value_DataType.Map)
                throw new Exception("error map in init.");

            this.Value = data.Skip(1).ToArray();
        }

        public void PutToDB(LevelDB.DB db, byte[] key)
        {
            var snapshot = Helper.CreateSnapshot(db);

            if (this.Value == null)
            {//申请新的实例ID，然后初始化存储Map
                var key_instMax = Helper.tagKey_InstanceMax;
                var instid = db.Get(snapshot, key_instMax);
                if (instid == null || instid.Length == 0)
                {
                    instid = BitConverter.GetBytes((UInt64)1);
                }

                this.Value = instid;
                this.db = db;
                //刷新max
                {
                    UInt64 v = BitConverter.ToUInt64(instid, 0);
                    v++;
                    instid = BitConverter.GetBytes((UInt64)v);
                    db.Put(key_instMax, instid);
                }

                //初始化字典数量
                byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
                db.Put(key_count, BitConverter.GetBytes((UInt64)0));
            }
            else
            {//检查count是否存在，
                byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
                var count = db.Get(snapshot, key_count);
                if (count == null || count.Length == 0)
                    throw new Exception("error map instance.");
            }

            db.Put(key, Helper.tagValue_Map.Concat(this.Value).ToArray());
        }
        public void Batch_PutToDB(WriteBatch batch, LevelDB.DB db, byte[] key)
        {
            //var snapshot = Helper.CreateSnapshot(db);

            if (this.Value == null)
            {//申请新的实例ID，然后初始化存储Map
                var key_instMax = Helper.tagKey_InstanceMax;
                var instid = batch.Get( key_instMax);
                if (instid == null || instid.Length == 0)
                {
                    instid = BitConverter.GetBytes((UInt64)1);
                }

                this.Value = instid;
                this.db = db;
                //刷新max
                {
                    UInt64 v = BitConverter.ToUInt64(instid, 0);
                    v++;
                    instid = BitConverter.GetBytes((UInt64)v);
                    batch.Put(key_instMax, instid);
                }

                //初始化字典数量
                byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
                batch.Put(key_count, BitConverter.GetBytes((UInt64)0));
            }
            else
            {//检查count是否存在，
                byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
                var count = batch.Get(key_count);
                if (count == null || count.Length == 0)
                    throw new Exception("error map instance.");
            }

            batch.Put(key, Helper.tagValue_Map.Concat(this.Value).ToArray());
        }

        public UInt64 Count(LevelDB.ReadOptions ro)
        {
            byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
            byte[] data = db.Get(ro, key_count);
            if (data == null || data.Length == 0)
                throw new Exception("error map in Count");

            return BitConverter.ToUInt64(data, 0);
        }
        public byte[] GetBeginSeek()
        {
            return Helper.tagKey_MapValues.Concat(this.Value).ToArray();
        }
        public void SetItem(byte[] key, IValue item)
        {
            var snapshot = Helper.CreateSnapshot(db);

            byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
            byte[] data = db.Get(snapshot, key_count);
            if (data == null || data.Length == 0)
                throw new Exception("error map in Count");
            var count = BitConverter.ToUInt64(data, 0);

            //写入字典项
            var _key = Helper.tagKey_MapValues.Concat(this.Value).Concat(key).ToArray();


            var value = db.Get(snapshot, _key);
            bool bAdd = value == null;

            (item as IValueCreator).PutToDB(db, _key);


            if (bAdd)
            {//更新count
                count++;
                data = BitConverter.GetBytes(count);
                db.Put(key_count, data);
            }

        }
        public void Batch_SetItem(WriteBatch wb,byte[] key,IValue item)
        {
            //var snapshot = Helper.CreateSnapshot(db);

            byte[] key_count = Helper.tagKey_MapCount.Concat(this.Value).ToArray();
            byte[] data = wb.Get(key_count);
            if (data == null || data.Length == 0)
                throw new Exception("error map in Count");
            var count = BitConverter.ToUInt64(data, 0);

            //写入字典项
            var _key = Helper.tagKey_MapValues.Concat(this.Value).Concat(key).ToArray();


            var value = wb.Get(_key);
            bool bAdd = value == null;

            (item as IValueCreator).Batch_PutToDB(wb,db, _key);


            if (bAdd)
            {//更新count
                count++;
                data = BitConverter.GetBytes(count);
                wb.Put(key_count, data);
            }

        }
        public IValue GetItem(LevelDB.ReadOptions snapshot, byte[] key)
        {
            var _key = Helper.tagKey_MapValues.Concat(this.Value).Concat(key).ToArray();
            var data = db.Get(snapshot, _key);
            return Helper.CreateValue(db, data);
        }
        public MapIterator GetIterator(LevelDB.ReadOptions snapshot)
        {
            var head = Helper.tagKey_MapValues.Concat(this.Value).ToArray();
            MapIterator it = new MapIterator(db, snapshot,head);
            return it;
        }
    }
}
