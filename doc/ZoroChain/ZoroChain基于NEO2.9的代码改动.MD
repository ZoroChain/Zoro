# ZoroChain基于NEO的代码改造
## 多链结构的解释
### 根链和应用链
* ZoroChain采用多链结构，分为根链和应用链
* 根链作为管理链，记录应用链的创建和变更交易，全局资产交易，以及其他的跨链交易信息
* 每一个应用会有一条专属的应用链，和该应用有关的交易信息都保存在各自的应用链上

### 链的识别方式
* ZoroChain里每个链都有一个名字字符串，该名字可以和其他的链重名
* 此外，每个链会有一个唯一的Hash值表示该链的地址，用来快速索引该链
* 根链的Hash暂定为空

## zoro库的代码改动
### Ledger模块的修改
#### IInventory
* 数据的清单（目录）
  * `Block, Transaction, ConsensusPayload`继承自`IInventory`
* 新增属性 `ChainHash`
  * 用来表示该清单的数据归属于哪一条链
  * 提供`get`方法，子类各自给出实现

#### Transaction
* 新增变量 `UInt160 _chainHash`
  * 属性`ChainHash`的`get`方法返回`_chainHash`
* 修改构造函数 `Transaction(TransactionType type)`
  * 增加参数`chainhash`，赋值给`_chainHash`
* 修改所有派生类的构造函数，也要增加参数`chainhash`
* 修改`Verify`函数
  * 删掉对`MinerTransaction`，`ClaimTransaction`，`IssueTransaction`三种交易类型的验证处理

#### 现有的各类Transaction
* RegisterTransaction
  * 用于资产登记的交易
  * 删除，ZoroChain上不发布全局资产
* IssueTransaction
  * 用于分发资产的交易
  * 删除，ZoroChain上不发布全局资产
* MinerTransaction
  * 向共识节点支付小费的交易
  * 需要修改成ZoroChain的矿工机制
* ClaimTransaction
  * 用于分配 NeoGas 的交易
  * 删除，ZoroChain上不存在全局资产的分红机制
* EnrollmentTransaction
  * 用于报名成为记账候选人的特殊交易，已经被NEO弃用
  * 删除，ZoroChain上不提供类似功能
* PublishTransaction
  * 发布智能合约的特殊交易，已经被NEO弃用
  * 删除，该功可调用虚拟机脚本来实现
* ContractTransaction
  * UTXO模型的交易，这是最常用的一种交易
  * 暂时保留
* InvocationTransaction
  * 调用智能合约的特殊交易
  * 保留
* StateTransaction
  * 修改账户投票和记账人登记状态的交易
  * 删除，ZoroChain上暂不提供类似功能

#### Block
* 新增变量 `UInt160 _chainHash`
  * 属性`ChainHash`的`get`方法返回`_chainHash`

#### Blockchain
* NEO中只有一个`Blockchain`的对象实例，Zoro中每一条链对应一个`Blockchain`的对象实例

* 新增静态变量 `Dictionary<UInt160, Blockchain> blockchains;`
  * 记录所有的链对象实例，包括根链和应用链

* 新增静态函数 `GetBlockchain(hash)`
  * 从`blockchains`里查找对应的`blockchain`

* 新增静态函数 `RegisterBlockchain(hash)`
  * 把`blockchain`加入到`blockchains`字典里

* 修改变量 `StandbyValidators`
  * 改为非静态的属性，提供`set和get`方法
  * 根链的`StandbyValidators`改为在根链的构造函数里进行赋值，还是维持从Setting对象里取值的方法
  * 应用链的`StandbyValidators`在创建应用链的`Blockchain`对象时，从应用链的State里取值，再用`set`方法赋值

* 修改 `Singleton`:
  * 目前`Singleton`代表NEO里唯一的区块链对象
  * 把`Singleton`改为`Root`，代表Zoro里的根链
  * 把`singleton`改为`root`
  * 在构造函数里如果`root`为空时对其赋值
  * 需要修改现有代码里所有使用`Blockchain.Singleton`的地方

* `GoverningToken和UtilityToken`
  * 保留，但实际不使用，Zoro中不用全局资产，只使用NEP5资产

* `GenesisBlock`
  * 去掉注册和分发全局资产的交易，只保留一个空的`MinerTransaction`
 
* 修改构造函数
  * 当`root`为空时，用`this`对其赋值
  * 当`root`为空时，用`Setting`对象对`StandbyValidators`进行赋值

* `Persist(block)`
  * 去掉已经不用的Transaction子类的处理
* 删除`ProcessAccountStateDescriptor`
* 删除`ProcessValidatorStateDescriptor`   

#### 新增的AppChainState
* 继承自`StateBase`，用来记录应用链的各种状态数据
  * `string Name` 应用链名称
  * `UInt160 Hash` 应用链的Hash
  * `UInt160 Owner` 创建者
  * `uint Timestamp` 创建时间
  * `int Port` TCP端口号
  * `int WsPort` WebSocket端口号
  * `string[] SeedList` 种子节点地址
  * `string[] StandbyValidators` 共识节点地址
* 在`Zoro.App.Create`系统调用中会创建这个状态数据  
---
### Persistence模块的修改
#### Prefixes.cs
* 增加枚举类型 `public const byte ST_Appchain = 0x41;`

#### IPersistence
* 新增接口 `DataCache<UInt160, AppChainState> AppChains { get; }`

#### Snapshot
* 新增抽象函数 `public abstract DataCache<UInt160, AppChainState> AppChains { get; }`
* 删除函数
  * `CalculateBonus`
  * `CalculateBonusInternal`

#### DBSnapshot
* 新增函数 `public override DataCache<UInt160, AppChainState> AppChains { get; }`
* 在构造函数里创建该成员
  ```
  AppChains = new DbCache<UInt160, AppChainState>(db, options, batch, Prefixes.ST_Appchain);
  ```
#### Store
* 新增抽象函数 `public abstract DataCache<UInt160, AppChainState> GetAppChains();`
* 定义接口实现 `DataCache<UInt160, AppChainState> IPersistence.AppChains => GetAppChains();`

#### LevelDBStore
* 实现`GetAppChains`函数
  ```
  public override DataCache<UInt160, AppChainState> GetAppChains()
  {
    return new DbCache<UInt160, AppChainState>(db, null, null, Prefixes.ST_Appchain);
  }
  ``` 
---
### Network模块的修改
#### Peer
* 修改变量`tcp_manager`
  * 改为非静态

* 修改变量`localAddresses`
  * 改为非静态，在构造函数里用传入的参数进行赋值

* 修改构造函数
  * 增加参数`localAddresses`，使用该参数对`this.localAddresses`赋值

#### LocalNode
* 负责建立和维护P2P网络连接，收发广播消息
* 每一条链（包括根链和应用链）单独搭建一组P2P网络，彼此之间不造成干扰
* 需要为每一个链创建一个LocalNode对象实例，不同应用链的通信端口可以重复

* 新增变量 `blockchain`
  * 表示该LocalNode所对应的链
  * 把目前LocalNode.cs里的`system.Blockchain`替换成新的变量`blockchain`

* 新增变量 `consensusService`
  * 表示该LocalNode所对应链的共识服务对象

* 修改 `Singleton`:
  * 把`Singleton`改为`Root`，代表根链对应的LocalNode
  * 把`singleton`改为`root`
  * 构造函数里，如果`root`为空，则对其赋值
  * 需要修改现有代码里所有使用`LocalNode.Singleton`的地方

* 新增变量 `string[] SeedList`
  * 记录种子节点的地址列表

* 修改函数 `GetIPEndPointsFromSeedList`
  * 用`SeedList`变量替代`Settings.Default.SeedList`

* 修改构造函数
  * 增加参数`localAddresses`，传给父类的构造函数
  * 增加参数`blockchain`，对`this.blockchain`赋值
  * 当`root`为空时，用`this`对其赋值
  * 当`root`为空时，用`Setting`对象对`SeedList`进行赋值

* 修改`Props`函数
  * 增加参数`localAddresses`和`blockchain`

* 修改`ProtocolProps`函数
  * 增加参数`localNode`

* 把静态函数改为非静态
  * `GetIPEndPointsFromSeedList`
  * `GetIPEndpointFromHostPort`

#### TaskManager
* 新增变量 `blockchain`
  * 在构造函数里赋值，记录所关联的链
  * 把目前TaskManager.cs里的`Blockchain.Singleton`替换成`blockchain`

#### ProtocolHandler
* 新增变量 `localNode`
  * 在构造函数里赋值，记录关联的本地节点
  * 把目前ProtocolHandler.cs里的`LocalNode.Singleton`替换成`localNode`
  * 把目前ProtocolHandler.cs里的`Blockchain.Singleton`替换成`localNode.Blockchain`  
* 修改构造函数，增加参数 `localNode`
* 修改函数 `Props`，增加参数 `localNode`

#### RemoteNode
* 新增变量 `localNode`
  * 在构造函数里赋值，记录关联的本地节点
  * 把目前RemoteNode.cs里的`LocalNode.Singleton`替换成`localNode`
  * 把目前RemoteNode.cs里的`Blockchain.Singleton`替换成`localNode.Blockchain`  
* 修改构造函数，增加参数 `localNode`
  * 调用`ProtocolHandler.Props`时，传入`localNode`
* 修改函数 `Props`，增加参数 `localNode`

### Consensus模块
#### ConsensusService
* 新增变量 `localNode`
  * 记录该共识服务所属链的本地节点
  * 把目前ConsensusService.cs里的`LocalNode.Singleton`替代成新的变量`localNode`
  * 把目前ConsensusService.cs里的`Blockchain.Default`替换成新的变量`localNode.Blockchain`

#### ConsensusContext
* 修改函数`Reset`
  * 增加参数`blockchain`，把`Blockchain.Singleton`

### NeoSystem模块的修改
#### Settings
* 增加关注的应用链列表
  * `public IReadOnlyDictionary<string, int> AppChains { get; private set; }`
  * 用应用链地址的字符串做索引

#### NeoSystem
* `NeoSytem`改名为`ZoroSystem`
* 把`ZoroSystem`改造为每个链有一个对象实例，所有实例共享一个`ActorSystem`对象
* 把`ActorSystem`的创建放到构造函数里
* 修改构造函数，增加参数`actorSystem`
  * 如果`actorSystem`为空，则调用`ActorSystem.Create`创建`ActorSystem`对象
  * 如果`actorSystem`不为空，则使用该参数对`ActorSystem`进行赋值

* 新增成员变量 `ZoroSystem[] AppChainSystems`
  * 记录所有应用链的`ZoroSystem`

* 新增函数`GetAppChainSystem(hash)`
  * 根据应用链的Hash，获取应用链的`ZoroSystem`
  * 在处理RPC指令时，需要先根据目标链的Hash，获得到对应的`ZoroSystem`对象，才能向该对象里的`LocalNode`发消息

* 修改函数 `StartNode(port, ws_port)`
  * 在函数尾部调用`StartAppChains`函数，根据配置文件里设定的关注列表，连接应用链的P2P网络

* 成员函数 `StartAppChains()`
  * 根据配置文件里设定的关注列表，依次调用`FollowAppChain`

* 成员函数 `FollowAppChain(hash, startConsensusu)`
  * 关注某一条应用链
  * 先判断要关注的应用链是否存在，通过应用链的`hash`查询数据库中的`AppChainState`
  * 创建应用链的`ZoroSytem`，共享根链的`ActorSystem`
  * 在ZoroSytem的构造函数里创建`Blockchain, LocalNode, TaskManager`对象和对应的Actor对象
  * 使用`AppChainState`里记录的端口，调用`StartNode`，启动应用链的`LocalNode`
  * 如果`startConsensusu`为`true`，调用`StartConsensus`，启动该应用链的共识服务

### SmartContract模块
#### NeoService
* 把系统调用函数的名字空间从`Neo`改为`Zoro`
* 新增系统调用函数`Zoro.AppChain.Create`
  * 类似`Neo.Contract.Create`，创建并保存`AppChainState`

#### Zoro官方的应用链管理合约
* 编写并发布一个官方的应用链管理合约
* 该合约提供方法来修改应用链的各项数据，包括：
  * 种子节点地址
  * 备选记账人地址
* 该合约内部，通过AppChainState来保存修改后的数据

### RPC模块
#### RpcServer
* RpcServer还是保持一个实例，收到指令后分发到对应的`LocalNode`去处理
* 指令参数的修改
  * 所有的指令需要增加目标链的`hash`，根链的Hash暂定为空
  * 通过`ZoroSystem`的`GetAppChainSystem(hash)`来获取目标链的`ActorSystem`

### Wallets模块

## cli项目的代码改动
### MainServices