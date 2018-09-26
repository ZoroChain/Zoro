## NEO源码阅读记录 - IO模块

#### 1.ISerializable
* 可序列化对象的接口类
* 虚函数
  * `int Size()` 获得对象数据的字节数
  * `Serialize(BinaryWriter writer)` 序列化
  * `Deserialize(BinaryReader reader)` 反序列化

* 实函数
  * `byte[] ToArray()` 转换成byte数组，定义在Helper.cs中

#### 2.DB
* LevelDB的接口封装类，Key-Value数据库，常用操作接口：
  * `Get(key):` 获取数据
  * `Put(key, value):` 写入数据
  * `TryGet(key, out value):` 尝试获取数据
  * `Write(write_batch):` 批量写入数据
  * `Find(prefix):` 模糊查找，返回值是数组类型

#### 3.DataCache
* 用Dictionary记录key-value数据，常用接口:
  * `GetAndChange(key, factory) :`  
     用key查询内存中的Dictionary，如果没有缓存过，则查询LevelDB，并把结果缓存在Dictionary中，如果数据库中也没有，则用factory函数创建一个并缓存
  * `TryGet(key) : `  
     先在Dictionary中找，如果没有再查询LevelDB并缓存，还没有则返回null
  * `Delete(key) :`  
     从Dictionary中删除缓存
  * `Commit() :`  
     把Dictionary中标记成增、删、改的数据写回到LevelDB数据库里

* Dictionary缓存的数据标记了四种状态：
  * None: 没有差异
  * Add: 增加到缓存里的数据
  * Changed: 从数据库里获取后，在缓存中已修改过的数据
  * Delete: 数据库里有，但缓存中已删除的数据
  * 这个标记是为了记录Dictionary中的数据和LevelDB里的差异，方便把缓存中的数据同步写回数据库