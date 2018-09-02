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
#### 1.DB
LevelDB的接口封装类，Key-Value数据库，常用操作接口：
* `Get(key):` 获取数据
* `Put(key, value):` 写入数据
* `TryGet(key, out value):` 尝试获取数据
* `Write(write_batch):` 批量写入数据
 
#### 2.DataCache
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
* 继承StateBase，交易定义类
  * `Items:`*CoinState[]* 

#### 1.6 ValidatorState
* 继承StateBase，验证器定义类
  * `PublicKey:`*ECPoint* 公钥
  * `Registered:`*bool* 是否注册
  * `Votes:`*Fixed8* 票数

#### 1.7 ValidatorsCountState
* 继承StateBase，验证计数器定义类
  * `Votes:`*Fixed8[]* 票数

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
* 主要成员:
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

#### 2.5 Block

#### 2.6 Header

### 2.7 Blockchain

---
### 3. Transaction相关的类
#### 3.1 TransactionAttribute

#### 3.2 CoinReference

#### 3.3 TransactionOutput

#### 3.4 Transaction

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
 
* 主要成员:		
  * `header_index : List<UInt256>`  
	记录链上所有区块头的Hash数据，初始化时会加载并记录LevelDB中所有区块头的Hash数据
  * `header_cache : Dictionary<UInt256, Header>`  
	区块头的缓存，只是短暂的保存，在数据写入LevelDB后就会清除
  * `block_cache : Dictionary<UInt256, Block>`  
    区块数据的缓存，只是短暂的保存，在数据写入LevelDB后就会清除

* 对外主要接口:
  * `AddBlock(block)`    
    在主线程中调用，先把要写入数据库的block保存在block_cache中，等线程函数完成数据写入后再从block_cache中删除。	
  * `GetHeader(height) / GetHeader(hash)`  
    获取区块头，先在header_cache中找，没有再去数据库中查询。因为header_cache只是做短暂的保存，所以大部分时候是要查数据库获取的。
  * `GetState()`  
    获取状态类数据，并缓存在内存对象里
  * `GetBlock()`  
    获取一个区块，目前内存里没有缓存过区块数据，所以都是从LevelDB中查询获取

* 程序初始化流程:  
  * 从数据库中加载所有的区块头数据，缓存在变量header_index中
  * 如果数据库中没有区块头数据，加载所有的区块数据，并以此重新创建所有的区块头数据
  * 创建并运行一个独立线程，通过AutoResetEvent来控制线程的运行和挂起

* 主要处理流程:
  * 由外部发起写入区块数据的调用(AddBlock)，将要写入的区块数据保存在header_cache里，并向后台线程发信号
  * 后台线程从block_cache里取出要写入的区块数据，并调用Persist函数进行写入数据库的操作

---
## 五、Network模块
#### Message

#### UPnP

#### LocalNode
本地通信节点
* 主要成员:
  * `mem_pool : Dictionary<UInt256, Transaction>`
  * `temp_pool : HashSet<Transaction>`  
  用来记录远程节点发起的，还未处理的所有交易请求
 
* 程序初始化流程:
  * 初始化时启动两个线程
  * connectThread用来处理远程节点的连接请求
  * poolThread用来处理远程节点发起的交易请求
    
#### RemoteNode
远程通信节点的存根，每次接受一个远程节点的连接请求时会创建并记录该对象

#### TcpRemoteNode

#### WebSocketRemoteNode

#### RpcServer
  
 