# NEO 2.9源码阅读记录 - Ledger模块 - 各种State相关的类

### State类的作用
* State对象对应于LevelDB中保存的key-value中的value
* 可以通过Store类获取LevelDB中的数据，例如
```
BlockState state = Blockchain.Singleton.Store.GetBlocks().TryGet(hash);
ContractState contract = Blockchain.Singleton.Store.GetContracts().TryGet(script_hash);
```
* 可以通过Snapshot类获取数据库快照中的数据，例如
```
AssetState asset = snapshot.Assets.TryGet(assetId);
AccountState account = snapshot.Accounts.TryGet(hash);
```
### StateBase
* 各种State的抽象基类
* 主要对外接口:
  * `Size:` 返回该对象数据的字节长度
  * `Deserialize:` 解析加载
  * `Serialize:` 序列化
  * `ToJson:` 转换成Json对象

### BlockState
  * 区块数据，使用区块的Hash作为索引
  * `SystemFeeAmount` 从起始高度到当前高度累积的GAS系统开销，在GAS的再分配时用到，计算某个高度区间内的累积回收的GAS
  * `TrimmedBlock` 区块头加上交易的Hash，不包括交易数据

### TransactionState
  * 交易数据，使用交易的Has作为索引
  * `BlockIndex` 交易所属区块的高度
  * `Transaction` 交易数据

### AccountState
  * 账户数据，使用账户的地址作为索引
  * `ScriptHash:`*UInt160* 地址的Hash，即UTXO模型中Output里指定的收款人地址
  * `IsFrozen：`*bool* 标记账户是否冻结
  * `Votes:`*ECPoint[]* 该账户支持的记账备选人，用公钥表示，每个账户可以支持多个
  * `Balances:`*Dictionary<UInt256, Fixed8>* 全局资产余额，目前就只有NEO和GAS两种 
  * 通过钱包类可以创建账户，新建账户后，并不会马上生成一个AccountState
  * 只有对该账户进行操作时，例如转账，查询状态等，才会生成对应的AccountState
  * 当账户里所有的全局资产余额都为零时，会销毁对应的AccountState

### UnspentCoinState
  * 用交易Hash为索引，记录该交易里的输出项里的资产状态
  * `Items:`*CoinState[]* 一笔交易可能有多个输出，这里对应每一个输出项的资产状态
  * `ConState`定义了以下几种可以并在的状态：
    * `Unconfirmed` 已发起交易，还未被确认
    * `Confirmed` 该交易是否已经上链
    * `Spent` 已被花费
    * `Claimed` 已经行权（认领GAS）
    * `Frozen` 被冻结 

### SpentCoinState
  * NEO货币的花费记录，使用获得NEO货币的交易Hash作为索引，只记录`GoverningToken`的消耗记录
  * 用来追溯还未行权的GAS总额，并为ClaimTransaction提供来源依据
  * `TransactionHash:`*UInt256* 本次花费的余额来源，即UTOX模型中前一次交易输入项的Hash
  * `TransactionHeight:`*uint* 前一次交易的区块高度
  * `Items:`*Dictionary<ushort, uint>* 前一次交易输出项的索引编号，当前区块高度
* 在`Snapshot:GetUnclaimed(UINT hash)`里会使用这个状态

### ValidatorState
  * 备选记账人的数据，使用记账人的公钥作为索引
  * `PublicKey:`*ECPoint* 候选人的公钥
  * `Registered:`*bool* 标记该候选人是否已被注册
  * `Votes:`*Fixed8* 该候选人的总票数，
    * 即投票支持该候选人的所有账户的NEO货币总额
    * 按照候选人的总票数高低来排序，决定最后选出的记账人
    * 有关NEO中的记账人的选举与投票机制可参考<http://docs.neo.org/zh-cn/node/gui/vote.html>

### AssetState
  * 全局资产数据，使用资产类型作为索引
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

### ContractState
  * 智能合约的状态数据，使用合约地址作为索引
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

### StorageItem
  * 键值存储，使用StorageKey作为索引
  * `Value:`*byte[]* 存储的值

### ValidatorsCountState
  * 候选人投票数统计
  * `Votes:`*Fixed8[]* 投票结果
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

### HeaderHashList
 * 区块头的Hash列表，使用起始区块高度作为索引
 * `Hashes:`*UInt256[]* 区块头的Hash列表

### HashIndexState
 * 区块的Hash和高度
 * `Hash:`*UInt256* 区块的Hash
 * `Index:`*uint 区块的高度
