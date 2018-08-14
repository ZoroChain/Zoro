using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LevelDB.Ex
{

    public class Table
    {
        byte[] prefix;
        LevelDB.DB db;


        public Table(LevelDB.DB db, byte[] tablename)
        {
            this.db = db;
            this.prefix = tablename;
        }
        /// <summary>
        /// delete
        /// </summary>
        /// <param name="key"></param>
        public byte[] CalcKey(byte[] tag, byte[] key)
        {
            return tag.Concat(prefix).Concat(Helper.tagZero).Concat(key).ToArray();
        }
        public IValue GetItem(LevelDB.ReadOptions snapshot, byte[] key)
        {
            var _key = Helper.tagKey_Item.Concat(prefix).Concat(Helper.tagZero).Concat(key).ToArray();
            var data = db.Get(snapshot, _key);
            return Helper.CreateValue(this.db, data);
        }
        public void DeleteItem(byte[] key)
        {
            var _key = CalcKey(Helper.tagKey_Item, key);
            this.db.Delete(_key);
        }
        public void Batch_DeleteItem(LevelDB.WriteBatch batch, byte[] key)
        {
            var _key = CalcKey(Helper.tagKey_Item, key);
            batch.Delete(_key);
        }
        public void PutItem(byte[] key, IValue value)
        {
            var _key = CalcKey(Helper.tagKey_Item, key);
            (value as IValueCreator).PutToDB(this.db, _key);
        }
        public void PutItem_Batch(LevelDB.WriteBatch batch, byte[] key, IValue value)
        {
            var _key = CalcKey(Helper.tagKey_Item, key);
            (value as IValueCreator).Batch_PutToDB(batch, this.db, _key);
        }

    }
}
