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
对应LevelDB中保存的各类状态类数据，可通过BlockChain的GetStates函数获取对应的状态数据
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
* 继承自StateBase，定义账户通用的属性和方法
  * `ScriptHash:`*UInt160* 地址的散列值
  * `IsFrozen：`*bool* 标记账户是否冻结
  * `Votes:`*ECPoint[]* 
  * `Size：`重写StateBase的Size
  * `GetBalance:`获取账户资产余额
  * `Deserialize`、`Serialize`、`ToJson:`重写StateBase的对应方法

#### 1.3 AssetState
* 继承StateBase，资产定义类
  * `AssetId:`*UInt256* 资产ID
  * `AssetType:`*AssetType枚举* 资产类别 每种类别定义在AssetType枚举中
  * `Name:`*String* 资产名
  * `Amount:`*Fixed8* 总量
  * `Available:`*Fixed8* 可用数量
  * `Precision:`*byte* 精度
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
  * `Size`、`Deserialize`、`Serialize`、`ToJson:`重写StateBase的对应方法
  * `Clone`、`FromReplica:`实现ICloneable的对应方法
#### 1.6 ValidatorState
* 继承StateBase，ICloneable，验证器定义类
  * `PublicKey:`*ECPoint* 公钥
  * `Registered:`*bool* 标记验证器是否注册
  * `Votes:`*Fixed8* 投票结果
  * `Size`、`Deserialize`、`Serialize`、`ToJson:`重写StateBase的对应方法
  * `Clone`、`FromReplica:`实现ICloneable的对应方法
#### 1.7 ValidatorsCountState
* 继承StateBase，ICloneable，验证器总数定义类
  * `Votes:`*Fixed8[1024]* 投票结果
  * `Size`、`Deserialize`、`Serialize`、`ToJson:`重写StateBase的对应方法
  * `Clone`、`FromReplica:`实现ICloneable的对应方法
  
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
* 继承BlockBase，IInventory,IEquatable, 区块定义类
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

---
### 3. Transaction相关的类
#### 3.1 TransactionAttribute

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
  * `Outputs：`输出项列表
  * `Scripts:`*用于验证该交易的脚本列表?*
  * `Hash：`交易数据的散列值

#### 3.5 MinerTransaction

#### 3.6 IssueTransaction

#### 3.7 ClaimTransaction

#### 3.8 EnrollmentTransaction

#### 3.9 RegisterTransaction

#### 3.10 ContractTransaction

#### 3.11 StateTransaction

#### 3.12 PublishTransaction

#### 3.13 InvocationTransaction

---
## 四、Implementations模块
#### 1.LevelDBBlockChain
* 数据分类:
  LevelDB中保存的数据分为四类: 
  * 区块和交易数据: Block和Transaction
  * 状态类数据: Account, Coin, SpentCoin, Validator, Asset, Contract, Storage
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

* 程序初始化流程:
  * 从数据库中加载所有的区块头数据，缓存在变量header_index中
  * 如果数据库中没有区块头数据，加载所有的区块数据，并以此重新创建所有的区块头数据
  * 创建并运行一个独立线程，通过AutoResetEvent来控制线程的运行和挂起

* 主要处理流程:
  * 由外部发起写入区块数据的调用(AddBlock)，将要写入的区块数据保存在header_cache里，并向后台线程发信号
  * 后台线程从block_cache里取出要写入的区块数据，并调用Persist函数进行写入数据库的操作

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
  * 以后再次运行，会优先连接记录在`peers.dat`里的节点

#### 5.RemoteNode
远程通信节点的存根，每次接受一个远程节点的连接请求时会创建并记录该对象

* 主要函数:
  * `StartSendLoop:`异步函数，向远程节点发送消息队列中的数据
  * `StartProtocol:`异步函数，接收并处理远程节点发来的数据
  * `OnMessageReceived:`处理远程节点发来的数据
  * `EnqueueMessage:`向消息队列件添加一个等待发送的数据

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
      * 收到"inv"后，先将消息体里的Hash列表中排除掉最近一分钟内已经请求还未收到反馈的，再将Hash列表用"getdata"消息发送给对方
      * 收到"getdata"后，用"block"消息依次将请求的区块数据发送给对方
      * 收到"block"后，用Relay函数将该区块广播给其他远程节点
    * 发起交易共识：

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
  * `Process：` 命令处理函数
    * 可参考NEO技术文档<http://docs.neo.org/zh-cn/node/cli/apigen.html>
 
