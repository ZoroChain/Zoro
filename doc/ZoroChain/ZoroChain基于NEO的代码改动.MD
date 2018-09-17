# ZoroChain基于NEO的代码改造
## 多链结构的解释

### 根链和应用链
* ZoroChain采用多链结构，分为根链和应用链
* 根链作为管理链，记录应用链的创建和变更交易，全局资产交易，以及其他的跨链交易信息
* 每一个应用会有对应的一条应用链，和该应用有关的交易信息都保存在各自的应用链上

### 链的识别方式
* ZoroChain里每个链都有一个名字字符串，该名字可以和其他的链重名
* 此外，每个链会有一个唯一的Hash值，用来快速索引该链

## zoro库的代码改动
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
  * `AppChangeSeedTransaction`
  * `AppChangePortTransaction`

#### 现有的各类Transaction
* RegisterTransaction
  * 用于资产登记的交易
  * 该功可用智能合约来实现，不再保留
* IssueTransaction
  * 用于分发资产的交易
  * 该功可用智能合约来实现，不再保留
* MinerTransaction
  * 向共识节点支付小费的交易
  * 需要修改？
* ClaimTransaction
  * 用于分配 NeoGas 的交易
  * 需要修改？
* EnrollmentTransaction
  * 用于报名成为记账候选人的特殊交易
  * 已经被NEO弃用，不再保留
* PublishTransaction
  * 发布智能合约的特殊交易
  * 已经被NEO弃用，该功可用智能合约来实现，不再保留
* ContractTransaction
  * 合约交易，这是最常用的一种交易
  * 保留
* InvocationTransaction
  * 调用智能合约的特殊交易
  * 保留
* StateTransaction
  * 修改各类状态数据的交易
  * 不再保留，该功可用智能合约来实现

#### 新增的交易类
* AppBuildTransaction
  * 用于创建应用链
* AppChangeValidatorTransaction
  * 用于修改应用链的共识节点账户地址
* AppChangeSeedTransaction
  * 用于修改应用链的种子节点域名
* AppChangePortTransaction
  * 用于修改应用链的端口号

#### Block
* 新增变量 `UINT _chainHash`
  * 属性`ChainHash`的`get`方法返回`_chainHash`

#### Blockchain
* 新增静态变量 `Dictionary<UInt256, Blockchain> blockchains;`
  * 记录所有的链对象实例，包括根链和应用链
* 新增静态函数 `GetBlockchain(hash)`
* 新增静态函数 `CreateBlockchain(hash)`
  * 抽象函数，在`LevelDBBlockchain`中实现
  * 创建一个`LevelDBBlockchain`的实例并记录在`blockchains`里
* 修改静态函数 `RegisterBlockchain(blockchain)`
  * 把`blockchain`加入到`blockchains`字典里
* 修改静态变量 `Default`:
  * 目前`Default`代表NEO里唯一的区块链对象
  * 把`Default`改为`Root`，代表Zoro里的根链（管理链）
  * 需要修改现有代码里所有使用`Blockchain.Default`的地方
* 把以下两个委托事件改为非静态变量：
  * `public static event EventHandler<Block> PersistCompleted;`
  * `public static event EventHandler<Block> PersistUnlocked;`
* 修改变量`StandbyValidators`
  * 改为非静态的属性，提供`set和get`方法
  * 根链的`StandbyValidators`还是维持之前的赋值方法
  * 应用链的`StandbyValidators`从应用链的State里获值
* `GoverningToken和UtilityToken`
  * 只保留一个，改名为`ZoroToken`
* 还未确定的怎么修改的地方：   
  * `GenesisBlock`
  * `CalculateBonus`
  * `CalculateBonusInternal`
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
  * `ContainsUnspent`
  * `IsDoubleSpend`
  * `ProcessAccountStateDescriptor`
  * `ProcessValidatorStateDescriptor`

#### LevelDBBlockchain
* LevelDBBlockchain还是继承自Blockchain
* 每一条链对应一个LevelDB数据存储
* 每一个LevelDBBlockchain对象会创建一个独立线程用来做数据存储
* 新增静态函数 `CreateBlockchain(hash)`

#### 新增AppState
* 记录App链的各种信息，包括：
  * 名称、Hash
  * 创建时间
  * 网络端口号
  * 种子节点列表
  * 共识节点列表  
* 由LevelDBBlockchain在保存创建App链的交易时，同步更新和保存这个状态数据

#### LocalNode
* 负责建立和维护P2P网络连接，收发广播消息
* 每一条链（包括根链和应用链）单独搭建一组P2P网络，彼此之间不造成干扰
* 需要为每一个链创建一个LocalNode对象实例，不同应用链的通信端口可以重复

* 新增变量 `blockchain`
  * 表示该LocalNode所对应的链
  * 把目前LocalNode.cs里的`Blockchain.Default`替换成新的变量`blockchain`

* 新增变量 `consensusService`
  * 表示该LocalNode所对应链的共识服务对象

* 新增变量 `string[] SeedList`  
  * 记录种子节点的地址列表

* 修改函数 `GetIPEndPointsFromSeedList`
  * 用`SeedList`变量替代`Settings.Default.SeedList`

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

#### RootNode
* 继承自LocalNode，代表根链的本地节点，只有唯一的对象实例
* 把cli里创建LocalNode的代码，换成创建RootNode

* 成员变量 `LocalNode[] ChildNodes`
  * 记录所有应用链的LocalNode

* 成员函数 `FollowAppchain(hash)`
  * 关注某一条应用链
  * 先判断要关注的应用链是否存在，通过应用链的`hash`查询数据库中的`AppState`
  * 创建应用链的`Blockchain和LocalNode`对象
  * 将新创建的`LocalNode`对象记录在`ChildNodes`里
  * 使用`AppState`里记录的端口，启动应用链的`LocalNode`

* 成员函数 `StartAppConsensus(hash)`
  * 启动某一条应用链的共识服务
  * 先判断该应用链对象是否存在  
  * 创建`ConsensusService`对象，调用该对象的`Start`函数

* 成员函数 `StartAppNodes`
  * 创建关注的所有App链的本地节点
  * 根据配置文件里设定的关注列表，依次调用`FollowAppchain`
  * 如果配置了要运行共识服务，再调用`StartAppConsensus`

* 成员函数 `Start(port, ws_port)`
  * 先调用基类的`Start`函数，连接根链的P2P网络
  * 再调用`StartAppNodes`函数，根据配置文件里设定的关注列表，连接App链的P2P网络

#### RemoteNode
* 修改对`LocalNode`静态对象的访问
  * 把目前RemoteNode.cs里的`LocalNode.`替换成`localNode.`    

#### ConsensusService
* 新增变量 `blockchain`
  * 记录该共识服务所归属的链
  * 把目前ConsensusService.cs里的`Blockchain.Default`替换成新的变量`blockchain`

* 新增变量 `localNode`
  * 记录该共识服务所属链的本地节点
  * 把目前ConsensusService.cs里的`LocalNode.`替代成新的变量`localNode.`
  
#### RpcServer
* 指令参数的修改
  * 所有的指令需要增加对应链的`hash`

#### Wallet

## cli项目的代码改动
### MainServices
* 修改`OnStart`函数
  * 把`new LocalNode()`改为`new RootNode()`
* ...
* ...