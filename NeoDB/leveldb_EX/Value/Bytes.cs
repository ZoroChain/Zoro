using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LevelDB;

namespace LevelDB.Ex
{
    public class Bytes : IValue, IValueCreator
    {
        public Value_DataType type => Value_DataType.Bytes;

        public byte[] Value
        {
            get;
            private set;
        }
        public Bytes()
        {

        }
        public Bytes(byte[] value)
        {
            this.Value = value;
        }

        public void PutToDB(LevelDB.DB db, byte[] key)
        {
            var _value = Helper.tagValue_Bytes.Concat(this.Value).ToArray();
            db.Put(key, _value);
        }
        public void Batch_PutToDB(LevelDB.WriteBatch batch, LevelDB.DB db, byte[] key)
        {
            var _value = Helper.tagValue_Bytes.Concat(this.Value).ToArray();
            batch.Put(key, _value);
        }

        public void Init(DB db, byte[] data)
        {
            var type = (Value_DataType)data[0];
            if (type != Value_DataType.Bytes)
                throw new Exception("error info");
            this.Value = data.Skip(1).ToArray();
        }
    }
}
