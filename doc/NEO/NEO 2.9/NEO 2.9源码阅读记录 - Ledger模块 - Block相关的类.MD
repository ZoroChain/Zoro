# NEO 2.9源码阅读记录 - Ledger模块 - Block相关的类
### BlockBase
* 继承自`IVerifiable`，可通过`GetScriptHashesForVerifying`获取用于验证签名的脚本Hash
* 主要成员:
  * MerkleRoot : 该区块中所有交易的Merkle树的根节点Hash	
  	* Merkle树，可以理解为二叉树，其中每个节点有一个对应的Hash值，这里的MerkleRoot就是根节点的Hash，可以用来校验交易数据是否发生损坏或被篡改。    	
    * 将Block中所有Transaction作为最底层的叶子节点，按照Merkle树（类似二叉树）的生成算法，从叶子节点开始，每两个节点生成一个父节点，并将这两个节点的Hash合并后生成这个父节点的Hash。如此逐层向上，直至构建完整个Merkle树。
    * 该变量的生成代码:
    ```
    MerkeRoot = MerkleTree.ComputeRoot(Transactions.Select(p => p.Hash).ToArray());
    ```
  * Timestamp : 生成该区块时的时间戳
  * ConsensusData : Nonce
	```
	Nonce是Number once的缩写，在密码学中Nonce是一个只被使用一次的任意或非重复的随机数值。
	创世块的ConsensusData使用比特币创世块的Nonce值2083236893。
	```
  * `NextConsensus:`*UInt160* 下一个区块的记账合约的散列值，用来验证区块头签名的合法性
	```
	该变量的生成代码：
	NextConsensus = GetConsensusAddress (GetValidators(transactions).ToArray());
	```
  * `Script:` *Witness* 用于验证该区块签名的见证人  

### Witness
* 继承ISerializable，见证人定义类,每次交易需要添加见证人
  * `InvocationScript:`*byte[]* 需要验证的脚本数据，通常是用私钥加密后的数据内容，也就是NEO中所说的签名数据
  * `VerificationScript:`*byte[]* 指定验证哪些脚本的数据，通常是一段VM字节码，由公钥长度+公钥+CheckSig指令组成，表示要执行一段检查签名的VM程序
  * `ScriptHash:`*UInt160* VerificationScript的hash
* Witness的作用是检验所见证的数据是否被篡改过
  * 在Help.cs的`VerifyScripts`函数里，我们可以找到使用Witness的代码
  * 用公钥解密被私钥加密过的数据，并和所见证的原始数据做比较，以此判断是否被篡改过

### Block
* 继承自`BlockBase`和`IInventory`，区块定义类，交易对象的集合，链上的主体
  * `Transactions:`*Transaction[]* 交易列表
  * `Header:`*Header* 该区块的区块头
  * `IInventory.InventoryType:`*InventoryType* 资产清单的类型
  * `Equals(Block):`*bool* 比较当前区块与指定区块是否相等
  * `Verify(bool):`*bool* 验证该区块是否合法，传参是否同时验证区块每一笔交易，返回区块的合法性
  * `Size`、`Deserialize`、`Serialize`、`ToJson:`重写BlockBase的对应方法
  * `Trim():`*byte[]* 把区块对象变为只包含区块头和交易Hash的字节数组，去除交易数据,返回只包含区块头和交易Hash的字节数组
  * `RebuildMerkleRoot():` 根据区块中所有交易的Hash生成MerkleRoot
  * `CalculateNetFee(IEnumerable<Transaction>):` 计算一组交易的矿工小费
    * `netFee = amount_in - amount_out - amount_sysfee`
    * `amount_in`表示所有Input项的总额
    * `amount_in`表示所有Output项的总额
    * `amount_sysfee`表示所有系统费用的总额
    * 默认情况下`netfee`应该是零，当`netfee`大于零时，表示支付给矿工的小费
  * `Trim():`*byte[]* 把区块对象变为只包含区块头和交易Hash的字节数组，去除交易数据

### BlockState
* 继承自`StateBase`，2.9版本中增加的类
* 表示LevelDB中保存的区块数据
* `SystemFeeAmount` 从起始高度到当前高度累积的GAS系统开销，在GAS的再分配时用到，计算某个高度区间内的累积回收的GAS
* `TrimmedBlock` 区块头加上交易的Hash，不包括交易数据

### Blockchain
* 继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
* 区块链对象，负责区块的上链保存，接收并缓存新发起的交易

* 消息对象
  * `Register` 
    * 收到该消息后，会把发送者加入到观察者队列，通过`Distribute()`可以给观察者们发送消息通知
  * `ApplicationExecuted` 
    * 在交易上链时，将合约脚本的执行结果（包括Notifications）发送给插件`ApplicationLogs`
    * 后者会将结果保存到本地的AppliationLog里
  * `PersistCompleted` 
    * 在每次成功出块后，通知共识模块开始下一次共识流程
  * `Import` 
    * 在导入本地的离线包数据时，由插件`ImportBlocks`发送给`Blockchain`
    * 收到该消息后`Blockchain`会将消息里的区块数据保存到LevelDB里
  * `ImportCompleted` 
    * 在`Blockchain`保存完`Import`消息里所有的区块后，会发该消息通知`ImportBlocks`插件


* 主要成员变量:
  * `SecondsPerBlock` 间隔多少秒出一次块
  * `TimePerBlock` 每次出块的间隔时间

  * `DecrementInterval` GAS生成的衰减周期
  * `GenerationAmount` GAS衰减周期对应的生成数量

  * `MaxValidators` 备选记账人数量上限
  * `StandbyValidators` NEO官方指定的备选记账人

  * `GoverningToken` 注册NEO货币，确定NEO的货币总量
  * `UtilityToken` 注册GAS货币，确定GAS的货币总量
  * `GenesisBlock` 创世块，内部会注册NEO和GAS货币总量，并将NEO转给一个备选记账人

  * `header_index:List<UInt256>` 	
    * 按区块高度顺序，记录所有区块头的Hash数据
    * 初始化时会加载LevelDB中保存的所有区块头的Hash数据
    * 通过这个列表可以快速获得本地的区块高度，以及按高度查询某个区块的Hash

  * `stored_header_count` 记录`header_index`中已经存储的Hash数量

  * `block_cache:Dictionary<UInt256, Block>` 记录未出块的区块数据，在区块上链后会从`block_cache`里删除该区块的数据
  * `block_cache_unverified:Dictionary<uint, Block>` 记录还未验证的区块数据

  * `mem_pool:ConcurrentDictionary<UInt256, Transaction>`
    * 记录已通过校验的，所有还未保存到块的交易
    * 每次把块写入DB后，会从mem_pool里删除记录在块中的交易
    * 每次出块后，会重新校验mem_pool里剩余的交易

  * `RelayCache` 广播数据的缓存，降低从LevelDB中获取数据的次数，目前只有共识数据使用了这个缓存
  * `subscribers` 观察者，通过向`Blockchain`发送`Register`来注册观察者
  * `currentSnapshot` 当前数据库快照，每次DB数据发生变更时，需要调用`UpdateCurrentSnapshot`重新获取快照

* 主要成员函数:
  * `ContainsBlock(hash)` 根据Hash判断是否存在对应的区块，先查block_cache，再查DB
  * `ContainsTransaction(hash)`根据Hash判断是否存在对应的交易数据，先查mem_pool，再查DB
  * `Distribute(message)` 向观察者发送消息通知
  * `GetBlock(hash)` 根据Hash获取区块数据，先查block_cache，再查DB
  * `GetBlockHash(index)` 根据高度获取区块Hash，查header_index，不会查DB
  * `GetConsensusAddress(validators)` 用记账人公钥生成检验多重签名的脚本字节码，并转换成Hash
  * `GetMemoryPool()` 返回mem_pool
  * `GetSnapshot()` 创建一份新的数据库快照
  * `GetTransaction(hash)` 根据Hash获取交易数据，先查mem_pool，再查DB
  * `OnImport(blocks)` 导入离线区块数据，用于加速区块同步过程
  * `OnNewBlock(block)` 收到一个区块数据
  * `OnNewConsensus(payload)` 收到一个共识消息
  * `OnNewHeaders(headers)` 收到一组区块头数据
  * `OnNewTransaction(transaction)` 收到一个交易数据
  * `OnPersistCompleted(block)` 当区块数据保存到DB时被调用，用来通知共识模块开始新一轮共识
  * `OnReceive(message)` 基于Actor模型的Akka框架中的消息接收函数
  * `OnRegister()` 收到注册观察者的消息，将该详细的发送者注册为观察者
  * `Persist(block)` 保存一个区块数据，根据区块内的交易以及合约，更改所有相关的状态数据
  * `ProcessAccountStateDescriptor(descriptor, snapshot)` 变更备选记账人的投票数
  * `ProcessValidatorStateDescriptor(descriptor, snapshot)` 变更备选记账人的登记状态
  * `Props(system, store)` 创建一个`Blockchain`的Actor代理对象
  * `SaveHeaderHashList(snapshot)` 将区块的Hash保存到LevelDB，累积收到超过2000个新区块Hash时才会进行保存
  * `UpdateCurrentSnapshot()` 更新当前的数据库快照