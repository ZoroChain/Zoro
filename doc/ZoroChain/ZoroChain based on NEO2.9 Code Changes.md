# ZoroChain基于NEO的代码改造 // Change based on Neo source code
## 多链结构的解释            // multi chain structure change
### 根链和应用链             // root and application chain
* ZoroChain采用多链结构，分为根链和应用链 // Zorochain to employ multi chain structure , for root and application chain
* 根链作为管理链，记录应用链的创建和变更交易，全局资产交易，以及其他的跨链交易信息 // root chain to manage , Record the creation and change transactions of the application chain, Global asset transactions, as well as other cross-chain trading information
* 每一个应用会有一条专属的应用链，和该应用有关的交易信息都保存在各自的应用链上// Each application will have a dedicated application chain, The transaction information associated with the application is stored on the respective application chain.

### 链的识别方式     // how to distinguish the chains
* ZoroChain里每个链都有一个名字字符串，该名字可以和其他的链重名 // Every chain in Zorochain has a string of names, The name can be the same as the other chain names
* 此外，每个链会有一个唯一的Hash值表示该链的地址，用来快速索引该链 // In addition, each chain will have a unique hash value representing the address of the chain used to quickly index the chain 
* 根链的Hash暂定为空 // The hash of the root chain is tentatively empty

## zoro库的代码改动 // zoro library code change
### Ledger模块的修改 // Ledger  module changes
#### IInventory // 
* 数据的清单（目录）// data , list of items
  * `Block, Transaction, ConsensusPayload`继承自`IInventory` // Inherits IInventory
* 新增属性 `ChainHash` // new properties ChainHash
  * 用来表示该清单的数据归属于哪一条链  // The chain used to indicate which data the manifest belongs to
  * 提供`get`方法，子类各自给出实现 // provide a `get` method , subclasses each give the implementation

#### Transaction
* 新增变量 `UInt160 _chainHash` // New Variables
  * 属性`ChainHash`的`get`方法返回`_chainHash` // Property `ChainHash` get method returns _chainHash
* 修改构造函数 `Transaction(TransactionType type)`// modify constructors
  * 增加参数`chainhash`，赋值给`_chainHash` // Add parameter ' Chainhash ', assign value to ' _chainhash
* 修改所有派生类的构造函数，也要增加参数`chainhash` // Modify the constructors of all derived classes, and also add the parameter ' Chainhash '
* 修改`Verify`函数 // * Modify the ' Verify ' function

  * 删掉对`MinerTransaction`，`ClaimTransaction`，`IssueTransaction`三种交易类型的验证处理 //   * Delete the verification process for ' minertransaction ', ' claimtransaction ', ' issuetransaction ' three types of trading

#### 现有的各类Transaction // Existing types of transaction
* RegisterTransaction
  * 用于资产登记的交易  //   * Transactions for asset registration
  * 删除，ZoroChain上不发布全局资产 //Delete, do not publish global assets on Zorochain 
* IssueTransaction    
  * 用于分发资产的交易 //   * Transactions for distribution of assets
  * 删除，ZoroChain上不发布全局资产 //   * Delete, do not publish global assets on Zorochain
* MinerTransaction
  * 向共识节点支付小费的交易 // A transaction that pays a tip to a consensus node 
  * 需要修改成ZoroChain的矿工机制 // The miners ' mechanism needed to be modified into Zorochain
* ClaimTransaction
  * 用于分配 NeoGas 的交易 //   * Transactions for assigning Neogas
  * 删除，ZoroChain上不存在全局资产的分红机制 //   * Delete, no dividend mechanism for global assets on Zorochain
* EnrollmentTransaction
  * 用于报名成为记账候选人的特殊交易，已经被NEO弃用 //   * Special transaction for registration as a candidate for accounting, has been deprecated by neo
  * 删除，ZoroChain上不提供类似功能 //   * Delete, no similar function on Zorochain
* PublishTransaction
  * 发布智能合约的特殊交易，已经被NEO弃用 //   * Special deals for the release of smart contracts have been deprecated by neo
  * 删除，该功可调用虚拟机脚本来实现  //   * Delete, this function can invoke the virtual machine script to implement
* ContractTransaction
  * UTXO模型的交易，这是最常用的一种交易 //   * Utxo model Trading, which is the most commonly used kind of trading
  * 暂时保留 // Temporarily reserved
* InvocationTransaction
  * 调用智能合约的特殊交易 //   * Special transactions for calling smart contracts
  * 保留 // reserved
* StateTransaction
  * 修改账户投票和记账人登记状态的交易 //   * Modify the transaction of the account voting and the register status of the bookkeeper.
  * 删除，ZoroChain上暂不提供类似功能 // Delete, no similar feature is available on Zorochain

#### Block
* 新增变量 `UInt160 _chainHash` New variable
  * 属性`ChainHash`的`get`方法返回`_chainHash` // New Property 

#### Blockchain
* NEO中只有一个`Blockchain`的对象实例，Zoro中每一条链对应一个`Blockchain`的对象实例 // Neo has a Blockchain object instance

* 新增静态变量 `Dictionary<UInt160, Blockchain> appchains;` // new static variable dictionary for the appchains
  * 记录所有的应用链对象实例，不包括根链 //   * Record all application chain object instances, not including the root chain

* 新增静态函数 `GetAppChain(hash)` // new method to get appchain using the hash
  * 从`blockchains`里查找对应的`blockchain` // Find the corresponding ' blockchain ' from ' blockchains '

* 新增静态函数 `RegisterAppChain(hash)` // new method to register appchain using hash
  * 把`blockchain`加入到`blockchains`字典里 //   * add ' blockchain ' to the ' blockchains ' dictionary

* 修改变量 `StandbyValidators` // modifying variables
  * 改为非静态的属性，提供`set和get`方法 //  Instead of static properties, provide ' set and get ' methods 
  * 根链的`StandbyValidators`改为在根链的构造函数里进行赋值，还是维持从Setting对象里取值的方法 // Change the root change StandbyValidators , Whether to assign values in the constructor of the root chain, or to maintain the value from the setting object
  * 应用链的`StandbyValidators`在创建应用链的`Blockchain`对象时，从应用链的State里取值，再用`set`方法赋值 //    * Application chain ' standbyvalidators ' when creating the ' Blockchain ' object of the application chain, from the state of the application chain, and then using the ' Set ' method to assign the value

* 修改 `Singleton`: Change 
  * 目前`Singleton`代表NEO里唯一的区块链对象 // Currently ' Singleton ' represents the only blockchain object in Neo
  * 把`Singleton`改为`Root`，代表Zoro里的根链 //Change ' Singleton ' to ' root ', representing the root chain in Zoro
  * 把`singleton`改为`root` // Change ' singleton ' to ' root '
  * 在构造函数里如果`root`为空时对其赋值, In the constructor, if ' root ' is empty, assign a value to it.
  * 需要修改现有代码里所有使用`Blockchain.Singleton`的地方 // Need to modify all the places in the existing code that use ' Blockchain.singleton '

* `GoverningToken和UtilityToken`
  * 保留，但实际不使用，Zoro中不用全局资产，只使用NEP5资产 //   * reserved, but not actually used, Zoro doesnt global assets, using only NEP5 assets


* `GenesisBlock`
  * 去掉注册和分发全局资产的交易，只保留一个空的`MinerTransaction` //   * Remove transactions that register and distribute global assets, leaving only an empty ' minertransaction '
 
* 修改构造函数 // modifying constructors
  * 当`root`为空时，用`this`对其赋值 //   * when ' root ' is empty, use ' this ' to assign a value to it
  * 当`root`为空时，用`Setting`对象对`StandbyValidators`进行赋值 //   * when ' root ' is empty, ' standbyvalidators ' is assigned with ' Setting ' object

* `Persist(block)`
  * 去掉已经不用的Transaction子类的处理  // Eliminate the processing of transaction subclasses that have not been used
* 删除`ProcessAccountStateDescriptor` // delete
* 删除`ProcessValidatorStateDescriptor`  // delete 

#### 新增的AppChainState // new
* 继承自`StateBase`，用来记录应用链的各种状态数据 // * Inherited from ' statebase ', used to record various state data of the application chain
  * `string Name` 应用链名称 // Application chain name
  * `UInt160 Hash` 应用链的Hash // Hash of the application chain
  * `UInt160 Owner` 创建者 // Creator
  * `uint Timestamp` 创建时间 // Creation time
  * `int Port` TCP端口号 // TCP port number
  * `int WsPort` WebSocket端口号 // websocket port number
  * `string[] SeedList` 种子节点地址 // Seed node address
  * `string[] StandbyValidators` 共识节点地址 //  Consensus nodes addresses
* 在`Zoro.App.Create`系统调用中会创建这个状态数据  // * This state data is created in the ' Zoro.App.Create ' system call

---
### Persistence模块的修改 // Modification of  Persistence Module
#### Prefixes.cs
* 增加枚举类型 `public const byte ST_Appchain = 0x41;` //  Increase enumeration type 


#### IPersistence
* 新增接口 `DataCache<UInt160, AppChainState> AppChains { get; }` // New interface

#### Snapshot
* 新增抽象函数 `public abstract DataCache<UInt160, AppChainState> AppChains { get; }` // New abstract function
* 删除函数 // delete
  * `CalculateBonus`
  * `CalculateBonusInternal`

#### DBSnapshot
* 新增函数 `public override DataCache<UInt160, AppChainState> AppChains { get; }` // new function
* 在构造函数里创建该成员 // Create the member in the constructor
  ```
  AppChains = new DbCache<UInt160, AppChainState>(db, options, batch, Prefixes.ST_Appchain);
  ```
#### Store
* 新增抽象函数 `public abstract DataCache<UInt160, AppChainState> GetAppChains();` // New abstract function
* 定义接口实现 `DataCache<UInt160, AppChainState> IPersistence.AppChains => GetAppChains();` // Defining interface Implementations
* 新增象函数`public abstract Blockchain Blockchain();` // New function

#### LevelDBStore
* 实现`GetAppChains`函数 // Implement GerAppChains function
  ```
  public override DataCache<UInt160, AppChainState> GetAppChains()
  {
    return new DbCache<UInt160, AppChainState>(db, null, null, Prefixes.ST_Appchain);
  }
  ``` 
* 新增属性访问方法`public override Blockchain Blockchain { get; set; }` // New Property access Method
  * 在`Blockchain`的构造函数里对上面这个属性赋值 //   Assign a value to the above attribute in the constructor of ' Blockchain '

---
### Network模块的修改 // Modification of the network module
#### Peer
* 修改变量`tcp_manager` // Modify Variable '`
  * 改为非静态 //    Change to non-static


* 修改变量`localAddresses` //   * Modify Variables
  * 改为非静态，在构造函数里用传入的参数进行赋值 //   * Instead of static, assign values in the constructor with the parameters passed in

* 修改构造函数
  * 增加参数`localAddresses`，使用该参数对`this.localAddresses`赋值 //   * Add parameter ' localaddresses ', use this parameter to assign value to ' this.localaddresses '

#### LocalNode
* 负责建立和维护P2P网络连接，收发广播消息 // Responsible for setting up and maintaining peer-to network connections, sending and receiving broadcast messages
* 每一条链（包括根链和应用链）单独搭建一组P2P网络，彼此之间不造成干扰 // * Each chain (including the root chain and the application chain) is set up in a separate group of peers without interfering with each other.
* 需要为每一个链创建一个LocalNode对象实例，不同应用链的通信端口可以重复 // A Localnode object instance needs to be created for each chain, and communication ports of different application chains can be duplicated

* 新增变量 `blockchain` // new variable
  * 表示该LocalNode所对应的链 //   * indicates the corresponding chain of the Localnode
  * 把目前LocalNode.cs里的`system.Blockchain`替换成新的变量`blockchain` //  * Put the current LocalNode.cs in the ' System.Blockchain ' replaced by a new variable ' Blockchain '


* 新增变量 `consensusService` // new variable
  * 表示该LocalNode所对应链的共识服务对象 //   * Represents the consensus service object for the Localnode's corresponding chain


* 修改 `Singleton`:
  * 把`Singleton`改为`Root`，代表根链对应的LocalNode //   * change ' Singleton ' to ' Root ', representing the root chain corresponding to the Localnode
  * 把`singleton`改为`root` // Change singleton to root
  * 构造函数里，如果`root`为空，则对其赋值 //   * constructor, if ' root ' is empty, assign a value to it 
  * 需要修改现有代码里所有使用`LocalNode.Singleton`的地方 //   * Need to modify all the places in the existing code that use ' Localnode.Singleton '

* 新增变量 `string[] SeedList` new variable    
  * 记录种子节点的地址列表 //  Record the address list of the seed node 

* 修改函数 `GetIPEndPointsFromSeedList` // modifying functions
  * 用`SeedList`变量替代`Settings.Default.SeedList` //   * replace ' Settings.Default.SeedList ' with ' seedlist ' variable

* 修改构造函数 // modifying constructors
  * 增加参数`localAddresses`，传给父类的构造函数 //   * Add parameter ' localaddresses ' to the constructor of the parent class
  * 增加参数`blockchain`，对`this.blockchain`赋值 //   * Add parameter ' blockchain ', assign value to ' this.blockchain '
  * 当`root`为空时，用`this`对其赋值 //   * when ' root ' is empty, use ' this ' to assign a value to it
  * 当`root`为空时，用`Setting`对象对`SeedList`进行赋值 //   * when ' root ' is empty, ' seedlist ' is assigned with ' Setting ' object

* 修改`Props`函数 // * Modify the ' Props ' function
  * 增加参数`localAddresses`和`blockchain` //   * Add parameter ' localaddresses ' and ' blockchain '

* 修改`ProtocolProps`函数 // * Modify the ' ProtocolProps ' function
  * 增加参数`localNode` // Add localnode

* 把静态函数改为非静态 // Change static to non static function
  * `GetIPEndPointsFromSeedList`
  * `GetIPEndpointFromHostPort`

#### TaskManager
* 新增变量 `blockchain` // new variable
  * 在构造函数里赋值，记录所关联的链 // Assign a value in the constructor to record the associated chain
  * 把目前TaskManager.cs里的`Blockchain.Singleton`替换成`blockchain` //  * Replace the current TaskManager.cs ' Blockchain.singleton ' with ' Blockchain '


#### ProtocolHandler
* 新增变量 `localNode` // new variable
  * 在构造函数里赋值，记录关联的本地节点 //   * Assign a value in the constructor to record the associated local node
  * 把目前ProtocolHandler.cs里的`LocalNode.Singleton`替换成`localNode` //  * Replace the current ProtocolHandler.cs ' Localnode.singleton ' with ' Localnode '
  * 把目前ProtocolHandler.cs里的`Blockchain.Singleton`替换成`localNode.Blockchain`  //  * Replace the current ProtocolHandler.cs ' Blockchain.singleton ' with ' Localnode.blockchain '
  * 修改构造函数，增加参数 `localNode`  //   * Modify constructor, add parameter ' Localnode '  
  * 修改函数 `Props`，增加参数 `localNode` //   * 修改函数 `Props`，增加参数 `localNode` 


#### RemoteNode
* 新增变量 `localNode` // new variable
  * 在构造函数里赋值，记录关联的本地节点 //    * Modify function ' Props ', add parameter ' Localnode ' 
  * 把目前RemoteNode.cs里的`LocalNode.Singleton`替换成`localNode` // Replace the current RemoteNode.cs ' Localnode.singleton ' with ' Localnode '
  * 把目前RemoteNode.cs里的`Blockchain.Singleton`替换成`localNode.Blockchain`  // Replace the current RemoteNode.cs ' Blockchain.singleton ' with ' Localnode.blockchain '
* 修改构造函数，增加参数 `localNode` // Modify constructor, add parameter ' Localnode
  * 调用`ProtocolHandler.Props`时，传入`localNode` //  * When calling ' ProtocolHandler.props ', pass in ' localnode '
* 修改函数 `Props`，增加参数 `localNode` // Modify function ' Props ', add parameter ' localnode

### Consensus模块
#### ConsensusService
* 新增变量 `localNode`  ,new variable
  * 记录该共识服务所属链的本地节点 //   * Record the local node of the chain to which the consensus service belongs
  * 把目前ConsensusService.cs里的`LocalNode.Singleton`替代成新的变量`localNode` ,  * Replace the current ConsensusService.cs ' Localnode.singleton ' with the new variable ' localnode '
  * 把目前ConsensusService.cs里的`Blockchain.Default`替换成新的变量`localNode.Blockchain` // Replace the current ConsensusService.cs ' Blockchain.default ' with a new variable ' Localnode.blockchain '


#### ConsensusContext
* 修改函数`Reset` // Modify function
  * 增加参数`blockchain`，把`Blockchain.Singleton`替换掉 //   * Add the parameter ' blockchain ' and replace ' Blockchain.Singleton '

### NeoSystem模块的修改
#### Settings
* 增加关注的应用链列表 // List of app chains that are related
  * `public IReadOnlyDictionary<string, int> AppChains { get; private set; }`
  * 用应用链地址的字符串做索引 //   * Index with string of application chain address

#### NeoSystem
* `NeoSytem`改名为`ZoroSystem` // change NeoSystem to ZoroSystem
* 把`ZoroSystem`改造为每个链有一个对象实例，所有实例共享一个`ActorSystem`对象 // * transform ' zorosystem ' to one object instance per chain, with all instances sharing a ' Actorsystem ' object
* 把`ActorSystem`的创建放到构造函数里 // * Put the creation of ' Actorsystem ' in the constructor 
* 修改构造函数，增加参数`actorSystem` // * Modify constructor, add parameter ' Actorsystem '
  * 如果`actorSystem`为空，则调用`ActorSystem.Create`创建`ActorSystem`对象 //  * if ' actorsystem ' is empty, call ' actorsystem.create ' to create ' Actorsystem ' object
  * 如果`actorSystem`不为空，则使用该参数对`ActorSystem`进行赋值  //   * if ' actorsystem ' is not empty, use this parameter to assign the ' Actorsystem ' value  


* 新增成员变量 `ZoroSystem[] AppChainSystems` // new member variable
  * 记录所有应用链的`ZoroSystem` // Record all the application chains

* 新增函数`GetAppChainSystem(hash)` // new function
  * 根据应用链的Hash，获取应用链的`ZoroSystem` //   * Obtain the ' Zorosystem ' of the application chain according to the hash of the application chain
  * 在处理RPC指令时，需要先根据目标链的Hash，获得到对应的`ZoroSystem`对象，才能向该对象里的`LocalNode`发消息 //   * When processing RPC instructions, it is necessary to obtain the corresponding ' Zorosystem ' object based on the hash of the target chain before sending a message to the ' Localnode ' in the object.

* 修改函数 `StartNode(port, ws_port)` // new function
  * 在函数尾部调用`StartAppChains`函数，根据配置文件里设定的关注列表，连接应用链的P2P网络 //   * Call the ' startappchains ' function at the end of the function to connect to the application chain based on the list of concerns set in the configuration file

* 成员函数 `StartAppChains()` // member functions
  * 根据配置文件里设定的关注列表，依次调用`FollowAppChain` //   * According to the list of concerns set in the configuration file, call ' Followappchain ' in turn

* 成员函数 `FollowAppChain(hash, startConsensusu)`// member functions
  * 关注某一条应用链                                            // * Focus on an application chain
  * 先判断要关注的应用链是否存在，通过应用链的`hash`查询数据库中的`AppChainState` // * First to determine whether the application chain to be concerned exists, through the application chain ' hash ' query database ' appchainstate '
  * 创建应用链的`ZoroSytem`，共享根链的`ActorSystem` //  * Create app chain ' Zorosytem ', share root chain ' Actorsystem '
  * 在ZoroSytem的构造函数里创建`Blockchain, LocalNode, TaskManager`对象和对应的Actor对象 //  * create ' Blockchain, Localnode, TaskManager ' object and corresponding actor in Zorosytem's constructor
  * 使用`AppChainState`里记录的端口，调用`StartNode`，启动应用链的`LocalNode` // * use ' appchainstate ' to log the port, call ' Startnode ', start the application chain ' Localnode '
  * 如果`startConsensusu`为`true`，调用`StartConsensus`，启动该应用链的共识服务 // * if ' startconsensusu ' is ' true ', call ' Startconsensus ', start the application chain consensus service

### SmartContract模块 // 
#### ApplicationEngine 
* 修改`Run`函数 // change Run function
  * 增加参数`Snapshot snapshot` // Add
  * 去掉用`Blockchain.Singleton`创建数据库快照的代码 //   * Remove the code that creates a db snapshot with ' Blockchain.Singleton '
  * 修改所有调用`Application.Run`的地方 //  * Modify all calls to ' Application.Run ' sections


#### NeoService
* 把系统调用函数的名字空间从`Neo`改为`Zoro` // * Change the system call function's namespace from ' Neo ' to ' Zoro '
* 新增系统调用函数`Zoro.AppChain.Create` // New system call function
  * 类似`Neo.Contract.Create`，创建并保存`AppChainState` //  * Similar to ' Neo.Contract.Create ', create and save ' appchainstate '


#### Zoro官方的应用链管理合约 // # # # Zoro official application chain management contract
* 编写并发布一个官方的应用链管理合约 // * Write and publish an official application chain management contract
* 该合约提供方法来修改应用链的各项数据，包括：* This contract provides methods to modify the data of the application chain, including:
  * 种子节点地址 // Seed Node Address
  * 备选记账人地址 // Candidate validator node
* 该合约内部，通过AppChainState来保存修改后的数据 // Within the contract, the modified data is saved through Appchainstate

### RPC模块
#### RpcServer
* RpcServer还是保持一个实例，收到指令后分发到对应的`LocalNode`去处理 // * Rpcserver still maintains an instance, receives the instruction to distribute to the corresponding ' Localnode ' to handle

* 指令参数的修改 // Parameter modifications
  * 所有的指令需要增加目标链的`hash`，根链的Hash暂定为空 //   * All instructions need to increase the target chain's ' hash ', the root chain hash is tentatively empty
  * 通过`ZoroSystem`的`GetAppChainSystem(hash)`来获取目标链的`ActorSystem` //   * Zorosystem ' GetAppchainSystem (hash) ' To get the ' Actorsystem ' of the target chain

### Wallets模块

## cli项目的代码改动 // cli project code changes
### MainServices
