# NEO 2.9源码阅读记录 - Ledger模块 - Transaction相关的类
### TransactionAttribute
* 用来记录本次交易的说明，给交易取名字

### CoinReference
* UTXO模型中的Input，用作一笔交易的某一个Input项
* 通过Hash和索引可以定位到一笔交易中的某个Output项
 
### TransactionOutput
* UTXO模型中的Output
* 包含资产类型，金额，收账人地址

### Transaction
* 继承自`IInventory`
* 使用UTXO模型的交易记账数据，是所有交易类型的基类
* 主要成员
  * `Type：`交易类型
  * `Version：`版本号，用来在代码升级后兼容老版本的数据
  * `Attributes：`交易的属性数据，可以用来触发鉴权合约
  * `Inputs：`输入项列表
    * 由交易的Hash和Output索引号组成
    * 通过Hash和索引号可以唯一定位到一个交易的某个Output数据
  * `Outputs：`输出项列表
    * 由收款人地址、金额、货币类型组成
  * `Witnesses:`见证人，用来验证交易发起人的签名
  * `Hash：`交易数据的Hash值
  * `SystemFee：`根据交易类型，返回需要的系统费用（系统费用在protocal.json中配置）
* 主要函数
  * `Verify:`验证交易是否有效
  * `VerifyWitnesses:`验证见证人的签名
  * `GetScriptHashesForVerifying:`获取验证签名脚本的Hash，用来验证验签脚本的合法性
    * 在UTXO模型里，收款人地址就是验签脚本的Hash
  * `References:`返回用Input作为key，该Input所属交易中的Output作为value的映射表
    * 可以用来快速计算交易中所有Input项的金额总和
    * 可以用来快速收集交易中所有Input项的货币来源账户的地址
  * `GetTransactionResults:`返回交易后，各类资产的变化量
    * 一般的转账交易，这里不会有返回的资产变化量，因为交易的Input和Output的金额总和应该相等
    * 如果返回的资产变化量大于零，表示有资产被回收消耗了，例如因为系统费用消耗了GAS
    * 如果返回的资产变化量小于零，表示有新的资产产生了，例如有账户认领了新的GAS

### TransactionAttribute
* 记录交易的额外属性
* `Usage` 标记该属性数据的类型
  * `Script` 表示在验证交易签名时，额外执行`Data`里的脚本，可以用来触发合约的鉴权函数
* `Data`
  * 当`Usage=Script`的时候，`Data`里记录的是要调用的合约地址（脚本的Hash)
* 在进行NEP5资产的转账和赎回时，进行鉴权

### TransactionState
* 继承自`StateBase`，2.9版本中增加的类
* 表示LevelDB中保存的交易数据
* `BlockIndex` 交易所属区块的高度
* `Transaction` 交易数据

### Transaction的派生类
#### RegisterTransaction
* (已弃用) 用于资产登记的交易
* 注册区块链货币，目前只有两种
* BlockChain.GoverningToken和BlockChain.UtilityToken，分别对应NEO和GAS
 
#### MinerTransaction
* 向矿工（NEO中指共识节点）支付小费的交易，每次出块所有交易里的剩余未分配金额分配给议长
* 每次对一批交易发起共识处理时，会由议长生成一个MinerTransaction
* MinerTransaction没有Input项，只可能有Output项
* Output项的收款人是生成该交易的议长，货币类型是GAS，金额是块内所有交易的剩余未分配金额的总和

#### IssueTransaction
* 用于分发资产的交易
* RegisterTransaction登记的资产初始时没有所有者
* 可以用IssueTransaction来将资产分发到某个账户
* 创始块中有将NEO股转给默认候选人的交易

#### ClaimTransaction
* 用于分配GAS的交易
* 先通过`Wallet.GetUnclaimedCoins`获得还未行权的GAS总额，再发起ClaimTransaction来完成行权

#### EnrollmentTransaction
* (已弃用)用于报名成为记账候选人的特殊交易

#### PublishTransaction
* (已弃用)智能合约发布的特殊交易

#### ContractTransaction
* 合约交易，这是最常用的一种交易

#### InvocationTransaction
* 调用智能合约的特殊交易

#### StateTransaction
* 用来变更备选记账人的投票数和某个账户支持的记账人数量