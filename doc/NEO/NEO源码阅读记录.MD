# NEO源码阅读记录
---
## 一、	基础结构类型
* `UInt160:` 
用来标记Address的散列值
* `UInt256:`
用来标记区块数据、交易数据、资产类型的散列值
* `ECPoint:`
用来标记一个公钥
--- 	
## 二、	IO模块
#### 1.ISerializable
虚函数
* `int Size()` 获得对象数据的字节数
* `Serialize(BinaryWriter writer)` 序列化
* `Deserialize(BinaryReader reader)` 反序列化

实函数
* `byte[] ToArray()` 转换成byte数组，定义在Helper.cs中

#### 2.DB
LevelDB的接口封装类，Key-Value数据库，常用操作接口：
* `Get(key):` 获取数据
* `Put(key, value):` 写入数据
* `TryGet(key, out value):` 尝试获取数据
* `Write(write_batch):` 批量写入数据
 
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

* 	Dictionary缓存的数据标记了四种状态：
    * None: 没有差异
	* Add: 增加到缓存里的数据
	* Changed: 从数据库里获取后，在缓存中已修改过的数据
	* Delete: 数据库里有，但缓存中已删除的数据
    	
	这个标记是为了记录Dictionary中的数据对应LevelDB里的数据状态差异，方便把缓存中的数据同步写回数据库。
---
## 三、	Core模块
### 1.各种State的类
* 对应LevelDB中保存的各类状态类数据，可通过BlockChain的GetStates函数获取对应的状态数据
```
DataCache<UInt160, AccountState> counts = Blockchain.Default.GetStates<UInt160, AccountState>();
DataCache<ECPoint, ValidatorState> validators = Blockchain.Default.GetStates<ECPoint, ValidatorState>();
```
#### 1.1 StateBase
* 各种State的抽象基类
* 主要对外接口:
  * `Size:` 返回该对象数据的字节长度
  * `Deserialize:` 解析加载
  * `Serialize:` 序列化
  * `ToJson:` 转换成Json对象

#### 1.2 AccountState
* 定义账户通用的属性和方法
  * `ScriptHash:`*UInt160* 地址的散列值
  * `IsFrozen：`*bool* 标记账户是否冻结
  * `Votes:`*ECPoint[]* 
  * `Size：`重写StateBase的Size
  * `GetBalance:`获取账户资产余额

#### 1.3 AssetState
* 继承StateBase，资产定义类
  * `AssetId:`*UInt256* 资产ID
  * `AssetType:`*AssetType枚举* 资产类别 每种类别定义在AssetType枚举中
  * `Name:`*String* 资产名
  * `Amount:`*Fixed8* 总量
  * `Available:`*Fixed8* 可用数量
  * `Precision:`*byte* 精度, neo的精度为0，gas的精度为8
  * `Fee:`*Fixed8* 手续费
  * `FeeAddress:`*UInt160* 手续费地址
  * `Owner:`*ECPoint* 所有者
  * `Admin:`*UInt160* 管理员地址
  * `Issuer:`*UInt160* 发行方地址
  * `Expiration:`*uint* 期限
  * `IsFrozen:`*bool* 标记资产是否冻结
  * `GetName:` 获取资产名称

#### 1.4 SpentCoinState
* 继承StateBase，交易定义类
  * `TransactionHash:`*UInt256* 交易哈希
  * `TransactionHeight:`*uint* 交易高度
  * `Items:`*Dictionary<ushort, uint>*

#### 1.5 UnspentCoinState
* 继承StateBase，ICloneable，未花费货币定义类
  * `Items:`*CoinState[]* 
#### 1.6 ValidatorState
* 记账候选人状态
  * `PublicKey:`*ECPoint* 候选人的公钥
  * `Registered:`*bool* 标记该候选人是否已被注册
  * `Votes:`*Fixed8* 该候选人的总票数，
    * 即投票支持该候选人的所有账户的NEO货币总额
    * 按照候选人的总票数高低来排序，决定最后选出的记账人
    * 有关NEO中的记账人的选举与投票机制可参考<http://docs.neo.org/zh-cn/node/gui/vote.html>
#### 1.7 ValidatorsCountState
* 被投票的候选人人数计数
  * `Votes:`*Fixed8[1024]* 投票结果
  * 每个账号最多可以投票支持1024个候选人
  * 这里的Votes是这样理解的：
    * Votes[0]表示总共投了一个候选人的票数总和
    * Votes[3]表示总共投了三个候选人的票数总和
    * Votes[i]表示总共投了i个候选人的票数总和
* `"NEO 网络将根据每个账户所投候选人数进行实时计算，选出共识节点。计算方法为：
对每个账户所投候选人数按大小排序，得到数组 C1, C2, ..., Cn
去掉数组中前 25% 和后 25% 的数值
对剩余的 50% 数值进行加权平均，得出 NEO 共识节点数 N
选出得票数最高的前 N 名候选人成为共识节点"`

  
#### 1.8 ContractState
* 继承StateBase，合约定义类
  * `Script:`*byte[]* 合约脚本
  * `ParameterList:`*ContractParameterType[]* 合约参数列表
  * `ReturnType:`*ContractParameterType枚举* 返回类型
  * `ContractProperties:`*ContractParameterType枚举* 合约属性
  * `Name:`*string* 合约名
  * `CodeVersion:`*string* 版本
  * `Author:`*string* 作者
  * `Email:`*string* 邮箱
  * `Description:`*string* 描述
  * `HasStorage:`*bool* 是否支持存储
  * `HasDynamicInvoke:`*bool* 是否同步调用
  * `Payable:`*bool* 是否支持转账
  * `ScriptHash:`*UInt160* 合约脚本哈希

#### 1.9 StorageItem
* 继承StateBase，存储定义类
  * `Value:`*byte[]* 存储的值
---
### 2. BlockChain相关的类
#### 2.1 IVerifiable
* 继承自ISerializable和IScriptContainer
* BlockBase的基类
* 主要对外接口:
  * `Scripts:` 用于验证该对象的脚本列表
  * `DeserializeUnsigned：` 反序列化未签名的数据
  * `SerializeUnsigned：` 序列化未签名的数据
  * `GetScriptHashesForVerifying：` 获得需要校验的脚本Hash值

#### 2.2 IInventory
* 继承自IVerifiable
* Block和Transaction的基类
* 主要对外接口:
  * `Hash:` 获取自身数据的Hash值
  * `InventoryType：` 获取类型，有三种：区块、交易、共识
  * `Verify：` 校验有效性

#### 2.3 BlockBase
* 主要成员:
  * MerkleRoot : 该区块中所有交易的Merkle树的根	
    ```
	该变量的生成代码:
	MerkeRoot = MerkleTree.ComputeRoot(Transactions.Select(p => p.Hash).ToArray());
    ```
	将Block中所有Transaction作为最底层的叶子节点，按照Merkle树（类似二叉树）的生成算法，从叶子节点开始，每两个节点生成一个父节点，并将这两个节点的Hash合并后生成这个父节点的Hash。如此逐层向上，直至构建完整个Merkle树。
Merkle树，可以理解为二叉树，其中每个节点有一个对应的Hash值，这里的MerkleRoot就是根节点的Hash。
  * Timestamp : 生成该区块时的时间戳
  * ConsensusData : Nonce
	```
	Nonce是Number once的缩写，在密码学中Nonce是一个只被使用一次的任意或非重复的随机数值。
	创世块的ConsensusData使用比特币创世块的Nonce值2083236893。
	```
	***在Neo里，ConsensusData没有实际作用？***
	
  * NextConsensus : 下一个区块的记账合约的散列值
	```
	该变量的生成代码：
	NextConsensus = GetConsensusAddress (GetValidators(transactions).ToArray());
	```
	***作用：验证本区块的合法性？***
  * Script : 用于验证该区块的脚本  

#### 2.4 Witness
* 继承ISerializable，见证人定义类,每次交易需要添加见证人
  * `InvocationScript:`*byte[]* 需要验证的脚本数据
  * `VerificationScript:`*byte[]* 指定验证哪些脚本的数据
  * `ScriptHash:`*UInt160* VerificationScript的hash
  * `Size`、`Deserialize`、`Serialize`、`ToJson:`实现ISerializable的对应方法

#### 2.5 Block
* 继承BlockBase，IInventory,IEquatable, 区块定义类，交易对象的集合，链上的主体
  * `Transactions:`*Transaction[]* 交易列表
  * `Header:`*Header* 该区块的区块头
  * `IInventory.InventoryType:`*InventoryType* 资产清单的类型
  * `Equals(Block):`*bool* 比较当前区块与指定区块是否相等
  * `Verify(bool):`*bool* 验证该区块是否合法，传参是否同时验证区块每一笔交易，返回区块的合法性
  * `Size`、`Deserialize`、`Serialize`、`ToJson:`重写BlockBase的对应方法
  * `Trim():`*byte[]* 把区块对象变为只包含区块头和交易Hash的字节数组，去除交易数据,返回只包含区块头和交易Hash的字节数组
  * `RebuildMerkleRoot():` 根据区块中所有交易的Hash生成MerkleRoot
  * `CalculateNetFee(IEnumerable<Transaction>):` 计算网络费用
  * `FromTrimmedData(byte[],int,Func<UInt256, Transaction>):`*Block* 将数据库中保存的字节数组解析还原成Block的内部数据

#### 2.6 Header
* 继承BlockBase，IEquatable, 
  * 区块头定义类，Block = Header + Transactions

### 2.7 Blockchain
* 区块链的基类，定义了区块链的各类功能接口
* `GoverningToken:`对应Neo币
* `UtilityToken:`对应Gas

* 主要接口函数
  * `AddBlock(block)`
  * `GetBlock`
  * `ContainsBlock(hash)`
  * `GetNextBlock(hash)`
  * `GetNextBlockHash(hash)`
  * `AddHeaders(headers)`
  * `GetHeader`
  * `ContainsTransaction(hash)`
  * `GetTransaction(hash)`

---
### 3. Transaction相关的类
#### 3.1 TransactionAttribute
* 用来记录本次交易的说明，给交易取名字
#### 3.2 CoinReference
* UTXO模型中的Input，用作一笔交易的某一个Input项
* 通过Hash和索引可以定位到一笔交易中的某个Output项
 
#### 3.3 TransactionOutput
* UTXO模型中的Output
* 包含资产类型，金额，收账人地址

#### 3.4 Transaction
* 使用UTXO模型的交易记账数据，是所有交易类型的基类
* 主要成员
  * `Type：`交易类型
  * `Version：`版本号，用来在代码升级后兼容老版本的数据
  * `Inputs：`输入项列表
    * 由交易的Hash和Output索引号组成
    * 通过Hash和索引号可以唯一定位到一个交易的某个Output数据
  * `Outputs：`输出项列表
    * 由收款人地址、金额、货币类型组成
  * `Scripts:`*用于验证该交易的脚本列表?*
  * `Hash：`交易数据的散列值
  * `SystemFee：`根据交易类型，返回需要的系统费用（系统费用在protocal.json中配置）
* 主要函数
  * `Verify:`验证交易是否有效
  * `References:`返回用Input作为key，该Input所属交易中的Output作为value的映射表
    * 可以用来快速计算交易中所有Input项的金额总和
    * 可以用来快速收集交易中所有Input项的货币来源账户的地址
  * `GetTransactionResults:`返回交易后，各类资产的变化量
    * 一般的转账交易，这里不会有返回的资产变化量，因为交易的Input和Output的金额总和应该相等
    * 如果返回的资产变化量大于零，表示有资产被回收消耗了，例如因为系统费用消耗了gas
    * 如果返回的资产变化量小于零，表示有新的资产产生了，例如矿工交易生成了新的gas，有账户认领了新的gas
##### 3.4.1 RegisterTransaction
* (已弃用) 用于资产登记的交易
* 注册区块链货币，目前只有两种
* BlockChain.GoverningToken和BlockChain.UtilityToken，分别对应neo和gas
 
##### 3.4.2 MinerTransaction
* 将每次出块所需要的系统费用转账给议长的交易
* 每次对一批交易发起共识处理时，会由议长生成一个MinerTransaction
* MinerTransaction没有Input项，只可能有Output项
* Output项的收款人是生成该交易的议长，货币类型是gas，金额是块内所有交易的系统费用的总和

##### 3.4.3 IssueTransaction
* 用于分发资产的交易
* RegisterTransaction登记的资产初始时没有所有者
* 可以用IssueTransaction来将资产分发到某个账户
* 创始块中有将NEO股转给默认候选人的交易

##### 3.4.4 ClaimTransaction
* 用于分配 NeoGas 的交易

##### 3.4.5 EnrollmentTransaction
* (已弃用)用于报名成为记账候选人的特殊交易

##### 3.4.6 PublishTransaction
* (已弃用)智能合约发布的特殊交易

##### 3.4.7 ContractTransaction
* 合约交易，这是最常用的一种交易

##### 3.4.8 InvocationTransaction
* 调用智能合约的特殊交易
##### 3.4.9 StateTransaction
---
## 四、Implementations模块
#### 1.LevelDBBlockChain
* 数据分类:
  LevelDB中保存的数据分为四类: 
  * 区块和交易数据: Block和Transaction
  * 状态类数据: Account, Coin, SpentCoin, Validator, Asset, Contract, Storage
    * 这些状态数据为了提供更快捷的方式获取到一些数据而存在
    * 这些状态数据是在处理Block和Transaction的过程中被保存到数据库中的
  * 索引类数据: 略
  * 系统数据: 略
 
* 主要成员:		
  * `header_index : List<UInt256>`
	* 记录链上所有区块头的Hash数据
    * 初始化时会加载并记录LevelDB中所有区块头的Hash数据
  * `header_cache : Dictionary<UInt256, Header>`  
	* 区块头的缓存
    * 只是短暂的保存，在数据写入LevelDB后就会清除
  * `block_cache : Dictionary<UInt256, Block>`  
    * 区块数据的缓存
    * 只是短暂的保存，在数据写入LevelDB后就会清除

* 对外主要接口:
  * `AddBlock(block)`
    * 在主线程中调用，先把要写入数据库的block保存在block_cache中，
    * 等线程函数完成数据写入后再从block_cache中删除
  * `AddBlockDirectly(block)`
    * 把一个block直接写入到LevelDB里
    * neo-cli和neo-gui里同步已出块的block到本地数据库时会使用
  * `GetHeader(height) / GetHeader(hash)`
    * 获取区块头，先在header_cache中找，没有再去数据库中查询
    * 因为header_cache只是做短暂的保存，所以大部分时候是要查数据库获取的
  * `GetStates()`  
    * 获取状态类数据，并缓存在内存对象里
  * `Block GetBlock(UInt256 hash)`  
    * 获取一个区块，
    * 目前内存里没有缓存过区块数据，所以都是从LevelDB中查询获取
  * `UInt256 GetBlockHash(uint height)`
    * 获取一个区块的Hash
    * 不查询LevelDB，直接返回header_index里记录的数据
  * `GetTransaction(hash)`
    * 获取一个交易
    * 内存里没有缓存，需要查询LevelDB获取数据

* 程序初始化流程:
  * 从数据库中加载所有的区块头数据，缓存在变量header_index中
  * 如果数据库中没有区块头数据，加载所有的区块数据，并以此重新创建所有的区块头数据
  * 创建并运行一个独立线程，通过AutoResetEvent来控制线程的运行和挂起

* 主要处理流程:
  * 由外部发起写入区块数据的调用(AddBlock)，将要写入的区块数据保存在header_cache里，并向后台线程发信号
  * 后台线程从block_cache里取出要写入的区块数据，并调用Persist函数进行写入数据库的操作

* `Persist(Block block)`
  * 将一个Block的数据保存到LevelDB里
  * 在保存Block时，会把已产生的系统费用的总额插入到Block数据的前面一起保存
    * 这个系统费用的总额是包括当前区块以及之前所有区块的系统费用的总量
  * 在保存Transaction时的额外处理逻辑
    * 根据交易里的Input和Output，更新一批状态类数据，包括账号，候选人，已花费和未花费
    * 再根据具体的交易类型，执行对应的处理逻辑，例如
      * 对于资产登记的交易，会更新资产状态
      * 对于分配Gas的交易，会更新已花费状态
      * 对于竞选记账人的交易，会更新候选人状态
      * 对于发布智能合约的交易，会更新合约状态
      * 对于执行智能合约的交易，会使用`StateMachine和ApplicationEngine`来执行交易里的合约脚本
        * 合约执行的结果会通过回调函数写入到ApplicationLog里
        * ApplicationLog里包含的notifications可以用来和外部系统做交互，例如判定合约执行的结果的依据
---
## 五、Network模块
#### 1.Message
* NEO网络节点之间通信时发送的消息体
* 继承自ISerializable
* 主要接口函数
  * `int Size()` 获得对象数据的字节数
  * `Serialize(BinaryWriter writer)` 序列化
  * `Deserialize(BinaryReader reader)` 反序列化
* 主要成员
  * `string Command:`类型字符串
  * `uint Checksum:`校验和
  * `byte[] Payload:`数据区

#### 2.各种Payload类
  * Payload意为有效负载，指网络通信中传输的数据包里，承载实际数据的区域。在这里可以理解为Message消息里的实际数据区
  * 可以将Message里的Payload转换成对应类型的Payload类，例如：
     ```
    Block block = message.Payload.AsSerializable<Block>();
    AddrPayload payload = message.Payload.AsSerializable<AddrPayload>();
    ``` 
#### 3.UPnP

#### 4.LocalNode
本地通信节点，只有一个实例存在的对象，处理P2P网络的连接和通信流程，记录已连接上的远程节点列表
* 主要成员:
  * `mem_pool : Dictionary<UInt256, Transaction>`
    * 用来记录已通过校验的，所有还未保存到块的交易
    * 每次把块写入DB后，会从mem_pool里删除记录在块中的交易
    * 每次出块后，会重新校验所有记录在mem_pool里的交易（___对性能有影响，后期需要优化？___）
  * `temp_pool : HashSet<Transaction>`  
    * 用来还未通过校验的所有交易请求
    * 在线程函数中对这些交易进行校验，并把通过校验的交易保存到mem_pool里
  * HashSet\<IPEndPoint> unconnectedPeers
    * 还未连接的远程节点的地址列表
    * 数据来源于其他远程节点发来的已连接地址列表
    * 在本地节点启动时，会从本地文件中加载（在cli或gui中调用）
    * 在本地节点关闭时，会保存到本地文件（在cli或gui中调用）
    * 本地节点启动后，会优先尝试和这些节点建立连接
  * List\<RemoteNode> connectedPeers
    * 已经连接上的远程节点的列表，包括主动和被动两种连接方式
    * 最多连接10个节点，达到或超过后不再主动和其他节点建立连接，但仍可以接受其他节点的连接请求
 
* 程序初始化流程:
  * 初始化时创建两个线程，
    * `connectThread:` 用来和其他的远程节点建立连接
    * `poolThread:` 用来处理远程节点发起的交易请求
  * 用Start函数启动本地节点，运行以上两个线程，以及两个用来处理远程节点的连接请求的异步函数
    * `AcceptPeers:` 接受TcpSocket发起的连接请求，创建并记录TcpRemoteNode
    * `ProcessWebSocketAsync:` 接受WebSocket发起的连接请求，创建并记录WebSocketRemoteNode
    
* 主要成员函数:
  * `ConnectToPeersLoop:` 
    * 线程`connectThread`的执行函数
    * 主动向远程节点发请求建立连接
  * `AddTransactionLoop:` 
    * 线程`poolThread`的执行函数，
    * 验证temp_pool中记录的交易，把通过验证的保存到mem_pool中
    * 向其他节点转发通过验证的交易
  * `Relay(IInventory inventory):` 
    * 向已连接的其他节点转发一个请求
    * 可以是Block,Transaction或Consensus
  * `RelayDirectly:`
    * 通过RemoteNode向远程节点广播消息
 
* 主动连接远程节点的程序逻辑:
  * 第一次运行的节点，会先连接上protocol.json里记录的5个seed节点
  * 连接上以后，会向对方请求其他可以连接的节点地址
  * 在收到可连接的节点地址后，会记录在`unconnectedPeers`里，并在此后尝试连接
  * 已连接节点数达到或超过10个以后，不再主动连接其他节点
  * 关闭时，会将可连接的节点地址保存到本地的`peers.dat`文件里
  * 以后再次运行，会优先连接记录在`peers.dat`里的节点和种子节点

#### 5.RemoteNode
远程通信节点的存根，每次接受一个远程节点的连接请求时会创建并记录该对象

* 主要函数:
  * `StartSendLoop:`异步函数，向远程节点发送消息队列中的数据
  * `StartProtocol:`异步函数，接收并处理远程节点发来的数据
  * `OnMessageReceived:`处理远程节点发来的数据
  * `EnqueueMessage:`向消息队列件添加一个等待发送的`Message`数据

* 节点间的通信流程
  * 初始化流程
    * 请求远程节点的NEO版本信息
    * 所有节点在与远程节点建立连接后都会立刻发送该消息，并等待对方也发送该消息
    * 收到远程节点发来的"version"消息后，记录在RemoteNode的Version成员里
    * 向对方发"verack"消息作为确认，同时也等待对方的确认消息
    * 根据本地记录的区块高度，向对方发"getheaders"消息，请求本地还未同步过的区块数据
    * 之后进入消息接收和处理循环，并在空闲时间发送"getblocks"消息，请求本地还未同步过的区块数据
  * 各类消息的处理流程
    * 获取可连接的远程节点列表："getaddr"->"addr"
      * 在远程节点的连接数量未达到一定值时，会向已连接的所有节点发送"getaddr"消息，请求该节点已连接的节点地址列表
      * 收到"getaddr"后，会将本地已连接的所有节点地址随机填入列表，最多200个，用"addr"消息发送给对方
    * 同步区块头："getheaders"->"headers"
      * 请求区块头数据，使用GetBlocksPayload结构，其中记录所请求区块列表中开始和结束的区块Hash
      * 收到"getheaders"后，用"headers"消息向对方发送区块头列表，一次最多发送2000个区块头
      * 收到`"headers"`后，将数据记录到BlockChain中，并继续请求还未同步过的区块头数据
    * 同步区块数据："getblocks"->"inv"->"getdata"->"block":
      * 请求区块数据，使用GetBlocksPayload结构，其中记录所请求区块列表中开始和结束的区块Hash
      * 收到"getblocks"后，用"inv"消息向对方发送区块的Hash列表，一次最多发送500个
      * （"inv"可以承载Block、Transaction和Consensus三种数据）
      * 收到"inv"后，先从消息体里的Hash列表中排除掉最近一分钟内已经请求还未收到反馈的，再将Hash列表用"getdata"消息发送给对方
      * 收到"getdata"后，用"block"消息依次将请求的区块数据发送给对方
      * 收到"block"后，用Relay函数将该区块广播给其他远程节点
    * 共识流程：用"inv"来承载共识消息
      * 收到"sendrawtransaction"后，先把交易数据缓存在本地节点的mem_pool里
      * 到达出块时间时，由议长发出PrepareRequest
      * 议员反馈PrepareResponse，当收到的PrepareResponse达到决议通过数时，广播新出的区块数据
      * 共识超时，发出ChangeView，更换议长，等待新一轮共识

* 主要变量
  * `missions_global：`
    * 静态变量
    * 避免在一分钟内反复请求同一个数据
  * `mission_start:`
    * 如果超过一分钟还没有收到请求的数据，则断开网络连接
  * `missions:`
    * 当前正在请求中的数据的数量
    * 用来判断当前网络是否空闲，空闲时会用来同步区块数据

#### 6.TcpRemoteNode
* 继承自RemoteNode
* 主要函数
    * `ConnectAsync：`异步函数，主动向一个远程节点请求建立连接
    * `ReceiveMessageAsync：`接受远程节点发来的数据，并转换成Message对象
    * `SendMessageAsync：`异步函数，向远程节点发送一个Message对象的数据

#### 7.WebSocketRemoteNode
* 继承自RemoteNode
* 主要函数
    * `ReceiveMessageAsync：`接受远程节点发来的数据，并转换成Message对象
    * `SendMessageAsync：`异步函数，向远程节点发送一个Message对象的数据

#### 8.RpcServer
* 提供基于HTTP协议的远程过程调用服务
* 主要函数
  * `Start:`启动函数
  * `Process：` 指令处理函数
    * 相关的指令集可参考NEO技术文档<http://docs.neo.org/zh-cn/node/cli/apigen.html>
 
* 主要RPC指令
  * "sendrawtransaction":发送一个原始的交易数据，请求进行共识流程
---
## 六、Consensus模块
NEO采用基于DBFT机制的共识算法，相关的技术文档可参考<http://docs.neo.org/zh-cn/basic/consensus/whitepaper.html>
#### 1.ConsensusState
* 用来标记共识节点的各种状态
* 共有以下这些状态:
  * `Initial：`启动或重置后的初始状态
  * `Primary:`本轮共识中，本地节点是议长
  * `Backup:`本轮共识中，本地节点是议员
  * `RequestSent:`议长已经把本轮要共识的交易数据广播出去了
  * `RequestReceived:`已收到议长广播的需要共识的交易数据
  * `SignatureSent:`本地节点已将新区块头的签名广播出去了
  * `BlockSent:`本轮共识完成，新的区块已生成并广播出去了
  * `ViewChanging:`正在变更视图

#### 2.ConsensusMessage
* Type共识处理流程中使用到的网络消息，有三种类型：
  * ChangeView，变更视图
  * PrepareRequest，议长发送待共识的交易数据和区块头签名
  * PrepareResponse，议员发送区块头签名

#### 3.ConsensusContext
* 记录共识过程中的各种状态数据
* 主要成员:
  * `State:`当前的共识状态
  * `PrevHash:`上个区块的Hash
  * `BlockIndex:`新块的高度
  * `ViewNumber:`视图编号
    * 每次`ChangeView`时`ViewNumber`会累加
    * 每次出块后，ViewNumber会被重置为0
  * `Validators:`共识节点的地址列表
  * `MyIndex:`本地节点在共识节点列表（Validators数组）中的索引
    * 默认值-1,表示本地节点不是共识节点
    * 如果本地节点不是共识节点，相关的程序流程不会被执行
  * `PrimaryIndex:`本次共识的议长在共识节点列表（Validators数组）中的索引
    * 每一轮新的共识开始时，会采用循环轮换的方式，在共识节点中选择一个议长
    * `PrimaryIndex = (BlockIndex - ViewNumber) % Validators.Length;`
  * `Signatures:`记录每个共识节点对新区块的签名数据
  * `ExpectedView:`记录每个共识节点期望的，更改视图后的新编号
  * `KeyPair:`本地节点的公私钥对，仅当本地节点是共识节点时有效
  * `M:`通过决议的最小节点数量
    * `Validators.Length - (Validators.Length - 1) / 3;`

#### 4.ConsensusService
* 开启服务后，会处理本地节点接收的共识请求
* 开启共识处理服务的方法
  * 在cli的config.json中可以配置本地节点是否要开启共识处理 
    * `Paths里面Notifications参数代表共识节点，可以参与共识，共识节点配置在protocol.json中，非共识节点添加此参数将不能启动节点（搭建私链时测试发现，具体产生原因不明）`
    * `Paths里面ApplicationLogs参数代表可以检测智能合约的notify，获取智能合约状态`
    * `Paths里面Chain参数代表爬虫获取的块数据存储地址`
  * 在cli的shell中输入指令"start consensus"
* 共识处理流程
  * RPCServer接收到"sendrawtransaction"消息后，通过本地节点的`Relay`函数把该交易请求广播给其他远程节点
  * 收到广播消息的节点，会将交易数据验证后记录在本地节点的mem_pool里
  * 每一轮共识会有一个最长处理时间（初始为15秒），当超过这个时长后，会更换视图并开始新一轮共识
  * 每次开始新一轮共识时，先选出一个共识节点作为本轮的议长
  * 议长把本地节点记录的待出块交易记录在ConsensusContext里,并等待下一次的出块时间
  * 到达出块时间后，议长先将新区块头签名，
    * 记录待出块的交易数据时，会最前面插入一个新生成的MinerTransaction
  * 议长将这些交易的Hash和区块头的签名广播出去，并标记状态`RequestSent`
  * 议员收到广播后，根据消息体里的Hash，从本地mem_pool里查找交易数据，并把这些交易数据记录到ConsensusContext里
  * 议员在完成上一步操作后，也生成一个新区块头的签名，广播出去，并标记状态`SignatureSent`
  * 每个共识节点收到区块头的签名后，会判断是否达到了通过决议的最小节点数，如果通过了，会生成并广播新的区块，并标记状态为`BlockSent`
  * 本轮共识结束，在本地节点将区块保存到LevelDB后，开始新一轮的倒计时
* 主要函数
  * `InitializeConsensus：`开始新一轮共识
  * `FillContext:`把本轮共识的要处理的交易保存到Context里
  * `OnTimeout:`本轮共识过程的最长时间的倒计时函数
  * `SignAndRelay:`将共识消息包签名后广播出去
  * `OnPrepareRequestReceived:`收到并处理议长广播的需要共识的交易数据，只有议员会执行
  * `OnPrepareResponseReceived:`收到并处理议员广播的区块头签名数据
* 疑问
  * `BlockChain`的`GetConsensusAddress`函数没看懂
    * Contract.CreateMultiSigRedeemScript(validators.Length - (validators.Length - 1) / 3, validators).ToScriptHash();
  * `SignAndRelay`函数没看懂
  * `CheckSignatures`函数没看懂
  * 每次更换视图时，共识过程的最长处理时间增加一倍？
    * `timer.Change(TimeSpan.FromSeconds(Blockchain.SecondsPerBlock << (view_number + 1)), Timeout.InfiniteTimeSpan);`
## 七、Wallet模块
#### Wallet
* 钱包的基础类
* 在Implementations/Wallets里有两个实现类
  * `UserWallet:`老版本的钱包，db3格式
  * `NEP6Wallet:`基于NEP6标准的钱包，json格式
* 提供和钱包相关的操作，例如创建账户，获取账户地址，查询资产余额之类的接口
#### WalletIndexer
* 钱包相关的LevelDB数据库