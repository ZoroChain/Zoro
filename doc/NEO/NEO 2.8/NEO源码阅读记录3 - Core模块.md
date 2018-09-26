## NEO源码阅读记录 - Core模块
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
* 账户信息的状态数据
  * `ScriptHash:`*UInt160* 地址的Hash，即UTXO模型中Output里指定的收款人地址
  * `IsFrozen：`*bool* 标记账户是否冻结
  * `Votes:`*ECPoint[]* 该账户支持的记账备选人，用公钥表示，每个账户可以支持多个
  * `Balances:Dictionary<UInt256, Fixed8>`全局资产余额，目前就只有NEO和GAS两种
* 在使用钱包新建账户后，并不会马上生成一个AccountState
* 只有该账户有相关的交易上链时，才会生成对应的AccountState
* 当该账户所有全局资产余额都为零时，会销毁对应的AccountState

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
* `GoverningToken`的花费记录，也就是只记录消耗NEO货币的相关信息
* `SpentCoinState`可以用来追溯还未行权的GAS总额，并未ClaimTransaction提供来源依据
  * `TransactionHash:`*UInt256* 本次花费的余额来源，即UTOX模型中前一次交易输入项的Hash
  * `TransactionHeight:`*uint* 前一次交易的区块高度
  * `Items:`*Dictionary<ushort, uint>* 前一次交易输出项的索引编号，当前区块高度
* 在`LevelDBBlockchain:GetUnclaimed(UINT hash)`里会使用这个状态

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
  ```
  "NEO 网络将根据每个账户所投候选人数进行实时计算，选出共识节点。计算方法为：
  对每个账户所投候选人数按大小排序，得到数组 C1, C2, ..., Cn
  去掉数组中前 25% 和后 25% 的数值
  对剩余的 50% 数值进行加权平均，得出 NEO 共识节点数 N
  选出得票数最高的前 N 名候选人成为共识节点"
  ```
#### 1.8 ContractState
* 继承StateBase，合约定义类
  * `Script:`*byte[]* 合约脚本的二进制字节码，可以被虚拟机加载运行
  * `ParameterList:`*ContractParameterType[]* 合约参数列表
  * `ReturnType:`*ContractParameterType枚举* 返回类型
  * `ContractProperties:`*ContractParameterType枚举* 合约属性
  * `Name:`*string* 合约名
  * `CodeVersion:`*string* 版本
  * `Author:`*string* 作者
  * `Email:`*string* 邮箱
  * `Description:`*string* 描述
  * `HasStorage:`*bool* 是否需要使用存储
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
  * `InvocationScript:`*byte[]* 需要验证的脚本数据，通常是用私钥加密后的数据内容，也就是NEO中所说的签名数据
  * `VerificationScript:`*byte[]* 指定验证哪些脚本的数据，通常是一段VM字节码，由公钥长度+公钥+CheckSig指令组成，表示要执行一段检查签名的VM程序
  * `ScriptHash:`*UInt160* VerificationScript的hash
* Witness的作用是检验所见证的数据是否被篡改过
  * 在Help.cs的`VerifyScripts`函数里，我们可以找到使用Witness的代码
  * 用公钥解密被私钥加密过的数据，并和所见证的原始数据做比较，以此判断是否被篡改过

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
  * `CalculateNetFee(IEnumerable<Transaction>):` 计算一组交易的矿工小费
    * `netFee = amount_in - amount_out - amount_sysfee`
    * `amount_in`表示所有Input项的总额
    * `amount_in`表示所有Output项的总额
    * `amount_sysfee`表示所有系统费用的总额
    * 默认情况下`netfee`应该是零，当`netfee`大于零时，表示支付给矿工的小费
  * `FromTrimmedData(byte[],int,Func<UInt256, Transaction>):`*Block* 将数据库中保存的字节数组解析还原成Block的内部数据

#### 2.6 Header
* 继承BlockBase，IEquatable, 
  * 区块头定义类，Block = Header + Transactions

### 2.7 Blockchain
* 区块链的基类，定义了区块链的各类功能接口
* `GoverningToken:`对应Neo币
* `UtilityToken:`对应GAS

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
  * `CalculateBonus(inputs)`
    * 根据未行权的NEO货币的UTXO输入项，计算行权后可获得的GAS总额
  * `CalculateBonusInternal(unclaimed)`
    * 根据未行权NEO货币的花费记录，计算行权后可以获得的GAS总额
    * 每一条NEO花费记录对应的行权后可获得的GAS总额 = （生成的GAS总量 + 系统回收的GAS总量）* （NEO金额 / NEO货币总量）
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
    * 如果返回的资产变化量小于零，表示有新的资产产生了，例如有账户认领了新的gas
##### 3.4.1 RegisterTransaction
* (已弃用) 用于资产登记的交易
* 注册区块链货币，目前只有两种
* BlockChain.GoverningToken和BlockChain.UtilityToken，分别对应neo和gas
 
##### 3.4.2 MinerTransaction
* 向矿工（NEO中指共识节点）支付小费的交易，每次出块所有交易里的剩余未分配金额分配给议长
* 每次对一批交易发起共识处理时，会由议长生成一个MinerTransaction
* MinerTransaction没有Input项，只可能有Output项
* Output项的收款人是生成该交易的议长，货币类型是gas，金额是块内所有交易的剩余未分配金额的总和

##### 3.4.3 IssueTransaction
* 用于分发资产的交易
* RegisterTransaction登记的资产初始时没有所有者
* 可以用IssueTransaction来将资产分发到某个账户
* 创始块中有将NEO股转给默认候选人的交易

##### 3.4.4 ClaimTransaction
* 用于分配 NeoGAS 的交易
* 先通过GetUnclaimedCoins获得还未行权的GAS总额，再发起ClaimTransaction来完成行权

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
  * `GetUnclaimed(hash)`
    * 查询某个交易消耗了NEO货币，但这些NEO货币还有未行权的GAS

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
      * 对于分配GAS的交易，会更新已花费状态
      * 对于竞选记账人的交易，会更新候选人状态
      * 对于发布智能合约的交易，会更新合约状态
      * 对于执行智能合约的交易，会使用`StateMachine和ApplicationEngine`来执行交易里的合约脚本
        * 合约执行的结果会通过回调函数写入到ApplicationLog里
        * ApplicationLog里包含的notifications可以用来和外部系统做交互，例如判定合约执行的结果的依据