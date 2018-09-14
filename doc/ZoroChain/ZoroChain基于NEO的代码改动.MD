# ZoroChain基于NEO的代码改造
## 多链结构的解释
### 根链和应用链
* ZoroChain采用多链结构，分为根链和应用链
* 根链作为管理链，记录应用链的创建和变更交易，全局资产交易，以及其他的跨链交易信息
* 每一个应用会有对应的一条应用链，和该应用有关的交易信息都保存在各自的应用链上
### 链的识别方式
* ZoroChain里每个链都有一个唯一的字符串名字，该名字不能和其他的链重名
* 根据该字符串名字，会有一个对应的Hash值，用来快速索引该链
## 相关类的代码改动
#### IInventory
* 数据的清单（目录）
  * `Block, Transaction, ConsensusPayload`继承自`IInventory`
* 新增属性 `ChainHash`
  * 用来表示该清单的数据归属于哪一条链
  * 提供`get`方法，子类各自给出实现

#### Transaction
* 新增变量 `UINT _chainHash`
  * 属性`ChainHash`的`get`方法返回`_chainHash`
* 修改构造函数 `Transaction(TransactionType type)`
  * 增加参数`chainhash`，赋值给`_chainHash`
* 修改所有派生类的构造函数，也要增加参数`chainhash`
* 增加新的Transaction派生类
  * `AppBuildTransaction`
  * `AppChangeValidatorTransaction`
  * `AppChangePortTransaction`

#### Block
* 新增变量 `UINT _chainHash`
  * 属性`ChainHash`的`get`方法返回`_chainHash`

#### Blockchain
* 新增静态变量 `Dictionary<UInt256, Blockchain> blockchains;`
  * 记录所有的链对象实例，包括根链和应用链
* 新增静态函数 `GetBlockchain(hash)`
* 新增静态函数 `CreateBlockchain(name)`
* 新增静态函数 `InitRootChain()`
  * 为根链准备的函数，遍历所有区块的交易，从中获知目前所有的应用链信息
  * 在根链初始化时调用，用来创建当前关注的应用链的对象实例
* 修改静态函数 `RegisterBlockchain(blockchain)`
  * 把`blockchain`加入到`blockchains`字典里
* 修改静态变量 `Default`:
  * 目前`Default`代表NEO里唯一的区块链对象
  * 把`Default`改为`Root`，代表Zoro里的根链（管理链）
  * 需要修改现有代码里所有使用`Blockchain.Default`的地方
* 把以下两个委托事件改为非静态变量：
  * `public static event EventHandler<Block> PersistCompleted;`
  * `public static event EventHandler<Block> PersistUnlocked;`
* 还未确定的怎么修改的地方： 
  * `StandbyValidators`
  * `GoverningToken`
  * `UtilityToken`
  * `GenesisBlock`
  * `ContainsUnspent`
  * `GetMetaData`
  * `GetStates`
  * `GetAccountState`
  * `GetAssetState`
  * `GetContract`
  * `GetEnrollments`
  * `GetConsensusAddress`
  * `GetValidators`
  * `GetStorageItem`
  * `GetSysFeeAmount`
  * `GetUnclaimed`
  * `GetUnspent`
  * `IsDoubleSpend`
  * `ProcessAccountStateDescriptor`
  * `ProcessValidatorStateDescriptor`
#### LevelDBBlockchain：
* LevelDBBlockchain还是继承自Blockchain
* 每一条链对应一个LevelDB数据存储
* 每一个LevelDBBlockchain对象会创建一个独立线程用来做数据存储
  
#### LocalNode
* 负责建立和维护P2P网络连接，收发广播消息
* 每一条链（包括根链和应用链）单独搭建一组P2P网络，彼此之间不造成干扰
* 需要为每一个链创建一个LocalNode对象实例，每个应用链要提供专门的通信端口

* 新增变量 `blockchain`
  * 表示该LocalNode所对应的链
  * 把目前LocalNode.cs里的`Blockchain.Default`替换成新的变量`blockchain`

* 新增变量 `consensusService`
  * 表示该LocalNode所对应链的共识服务对象

* 新增变量 `LocalNode[] ChildLocalNodes`
  * 记录所有应用链的LocalNode，只有根链的LocalNode才会有数据

* 新增变量 `string[] SeedList`  
  * 记录种子节点的地址列表

* 修改函数 `GetIPEndPointsFromSeedList`
  * 用`SeedList`变量替代`Settings.Default.SeedList`

* 修改函数 `Relay(IInventory inventory)`
  * 当收到的`block`的时候，如果自身是根链的LocalNode，则调用`CheckAppBuildTransaction`

* 新增函数 `CheckAppBuildTransaction(block)`
  * 检查block里的所有Tx，如果有创建应用链的交易，则调用`CreateAppBlockchain`

* 新增函数 `CreateAppBlockchain(tx)`
  * 创建应用链的`Blockchain和LocalNode`对象
  * 将新创建的`LocalNode`对象记录在`ChildLocalNodes`里
  * 启动应用链的`LocalNode`
  * 判断是否要成为应用链的共识节点
    * 如果是的话，创建`ConsensusService`对象
    * 启动共识服务

* 把静态变量改为非静态
  * `InventoryReceiving和InventoryReceived`
  * `Dictionary<UInt256, Transaction> mem_pool`
  * `Dictionary<UInt256, DateTime> KnownHashes`
  * `HashSet<IPEndPoint> unconnectedPeers`
  * `HashSet<IPEndPoint> badPeers`
  * `<IPAddress> LocalAddresses`

* 把静态函数改为非静态
  * `LocalNode()`
  * `bool AddTransaction(Transaction tx)`
  * `void AllowHashes(IEnumerable<UInt256> hashes)`
  * `bool CheckKnownHashes(UInt256 hash)`
  * `void CheckMemPool()`
  * `bool ContainsTransaction(UInt256 hash)`
  * `Transaction[] GetMemoryPool()`
  * `Transaction GetTransaction(UInt256 hash)`
  * `void LoadState(Stream stream)`
  * `void SaveState(Stream stream)`

#### RemoteNode

    

#### ConsensusService
* 新增变量 `blockchain`
  * 记录该共识服务所归属的链
  * 把目前ConsensusService.cs里的`Blockchain.Default`替换成新的变量`blockchain`

* 新增变量 `localNode`
  * 记录该共识服务所属链的本地节点
  * 把目前ConsensusService.cs里的`LocalNode.`替代成新的变量`localNode.`
  
#### RpcServer


#### Wallet

#### 问题
* 创建应用链的交易，怎么来验证指定的记账人是否有效？
* NEO中的每个块的第一个交易都是矿工交易，即使是空块也是如此，我们也需要这样吗？