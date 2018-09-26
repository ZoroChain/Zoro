# NEO 2.9源码阅读记录 - Persistence模块
### DataCache
* 抽象类，表示一个LevelDB中的键值数据在内存中的缓存
    * 在NEO中，一个LevelDB的键值数据，由prefix、key、value三个部分组成
    * prefix表示数据类型，key表示索引，value表示值
    * 其中prefix是Prefixes.cs里定义好的二进制值
* 用Dictionary记录key-value数据，常用接口:
  * `GetAndChange(key, factory):`  
     用key查询内存中的Dictionary，如果没有缓存过，则查询LevelDB，并把结果缓存在Dictionary中，如果数据库中也没有，则用factory函数创建一个并缓存
  * `TryGet(key): `  
     先在Dictionary中找，如果没有再查询LevelDB并缓存，还没有则返回null
  * `Delete(key):`  
     从Dictionary中删除缓存
  * `Find(key_prefix):`
     使用key的前缀进行模糊查询，结果以key-value的数组返回
  * `Commit():`  
     把Dictionary中标记成增、删、改的数据写回到LevelDB数据库里

* Dictionary缓存的数据标记了四种状态：
  * None: 没有差异
  * Add: 增加到缓存里的数据
  * Changed: 从数据库里获取后，在缓存中已修改过的数据
  * Delete: 数据库里有，但缓存中已删除的数据
  * 这个标记是为了记录Dictionary中的数据和LevelDB里的差异，方便把缓存中的数据同步写回数据库

### DbCache
* 继承`DataCache`，实现和LevelDB相关的数据存取函数
* `AddInternal(key, value)`
* `DeleteInternal(key)`
* `FindInternal(key_prefix)` 模糊查询
* `GetInternal(key)`
* `TryGetInternal(key)`
* `UpdateInternal(key, value)`

### MetaDataCache
* 抽象类，LevelDB中通用数据的缓存
* 这类数据在系统中只有一份，例如当前最新区块的Hash和高度

### DbMetaDataCache
* 继承`MetaDataCache`，实现和LevelDB相关的数据存取函数
* `AddInternal(item)`
* `TryGetInternal()`
* `UpdateInternal(item)`

### IPersistence
* 数据存储器的接口类，定义了获取各种数据的接口函数
* 这些接口函数的返回值是`DataCache`和`MetaDataCache`

* `Blocks:` 区块数据
* `Transactions:` 交易数据
* `Accounts:` 账户数据
* `UnspentCoins:`
* `SpentCoins:` NEO货币的花费记录
* `Validators:` 备选记账人的数据
* `Assets:` 全局资产数据
* `Contracts:` 智能合约的状态数据
* `Storages:` 键值数据存储
* `HeaderHashList:` 区块头的Hash列表
* `ValidatorsCount:` 候选人被投票人数统计
* `BlockHashIndex:` 最后一个区块的Hash和高度
* `HeaderHashIndex:` 最后一个区块头的Hash和高度

### Snapshot
* 数据库快照的抽象类，继承`IPersistence`

* `Height`
  * 当前最新区块的高度
* `HeaderHeight`
  * 当前最新的区块头的高度，数据库中保存的区块头和区块数据并不是同步的
* `CurrentBlockHash`
  * 当前最新区块的Hash
* `CurrentHeaderHash`
  * 当前最新的区块头的Hash
* `CalculateBonus(inputs)`
  * 根据未行权的NEO货币的UTXO输入项，计算行权后可获得的GAS总额
* `CalculateBonusInternal(unclaimed)`
  * 根据未行权NEO货币的花费记录，计算行权后可以获得的GAS总额
  * 每一条NEO花费记录对应的行权后可获得的GAS总额 = （生成的GAS总量 + 系统回收的GAS总量）* （NEO金额 / NEO货币总量）
* `Commit`
  * 将快照中修改过的数据保存到LevelDB里
* `GetUnclaimed(hash)`
  * 查询某个交易是否消耗了NEO货币，但这些NEO货币还有未行权的GAS
* `GetValidators`
  * 获得备选记账人的公钥列表
* `GetValidators(IEnumerable<Transaction> others)`
  * 获得备选记账人的公钥列表
  * 如果参数为空，返回的是记录在配置文件里的默认记账人
  * 如果传入参数里，如果有更改记账人的交易操作，会影响最终的返回结果

### DBSnapShot
* LevelDb的数据库快照类，继承自`Snapshot`
* 在构造函数里使用`DbCache`和`DBMetaDataCache`创建各种Cache对象，是之和LevelDB中的数据建立关联

### Store
* NEO中数据存储的抽象类，继承自IPersistence, 
* 用抽象函数定义了IPersistence接口类里的接口函数
* `GetSnapshot:`抽象函数，创建一份数据库快照

### LevelDBStore
* LevelDB数据存储实现类，继承自Store
* 实现了父类的抽象函数，用`DbCache`和`DBMetaDataCache`来返回各类Cache对象
* `GetSnapshot:`实现函数，使用`DbSnapshot`来创建一份数据库快照
