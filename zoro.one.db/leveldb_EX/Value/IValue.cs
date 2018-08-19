using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LevelDB.Ex
{
    public interface IValue
    {
        Value_DataType type
        {
            get;
        }
        byte[] Value
        {
            get;
        }
    }
 
    public interface IMap : IValue
    {
        UInt64 Count(LevelDB.ReadOptions snapshot);
        IValue GetItem(LevelDB.ReadOptions snapshot, byte[] key);
        void SetItem(byte[] key, IValue item);
        void Batch_SetItem(WriteBatch batch, byte[] key, IValue item);
        MapIterator GetIterator(LevelDB.ReadOptions snapshot);
    }
    public interface IValueCreator
    {
        void Init(LevelDB.DB db, byte[] data);
        void PutToDB(LevelDB.DB db, byte[] key);
        void Batch_PutToDB(WriteBatch batch, LevelDB.DB db, byte[] key);
    }
}
