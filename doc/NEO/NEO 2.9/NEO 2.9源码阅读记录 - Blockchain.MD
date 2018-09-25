# NEO 2.9源码阅读记录 - Blockchain
* 继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
* 主要成员变量:
  * `Register` Akka消息对象类，收到该消息后，会把发送者加入到观察者队列，通过`Distribute()`可以给观察者们发送消息通知
  * `ApplicationExecuted` Akka消息对象类，在交易上链时，将合约脚本的执行结果（包括Notifications）发送给插件`ApplicationLogs`，后者会将结果保存到本地的AppliationLog里
  * `PersistCompleted` Akka消息对象，在每次成功出块后，通知共识模块开始下一次共识流程
  * `Import` Akka消息对象，在导入本地的离线包数据时，由插件`ImportBlocks`发送给`Blockchain`，收到该消息后`Blockchain`会将消息里的区块数据保存到LevelDB里
  * `ImportCompleted` Akka消息对象，在`Blockchain`保存完`Import`消息里所有的区块后，会发该消息通知`ImportBlocks`插件

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

  * `block_cache:Dictionary<UInt256, Block>` 记录未出块的区块数据
  * `block_cache_unverified:Dictionary<uint, Block>` 记录还未验证的区块数据
  * `mem_pool:ConcurrentDictionary<UInt256, Transaction>`
    * 记录已通过校验的，所有还未保存到块的交易
    * 每次把块写入DB后，会从mem_pool里删除记录在块中的交易
    * 每次出块后，会重新校验mem_pool里剩余的交易

  * `RelayCache` 广播数据缓存，用来避免重复请求本地已有的数据，目前只用来优化共识消息
  * `subscribers` 观察者，通过向`Blockchain`发送`Register`来注册观察者
  * `currentSnapshot` 当前数据库快照，每次DB数据发生变更时，需要调用`UpdateCurrentSnapshot`重新获取快照

* 主要成员函数:
  * `ContainsBlock(hash)`
  * `ContainsTransaction(hash)`
  * `Distribute(message)` 向观察者发送消息通知
  * `GetBlock(hash)`
  * `GetBlockHash(index)`
  * `GetConsensusAddress(validators)` 用记账人公钥生成检验多重签名的脚本字节码，并转换成Hash
  * `GetMemoryPool()`
  * `GetSnapshot()`
  * `GetTransaction(hash)`
  * `OnImport(blocks)`
  * `OnNewBlock(block)`
  * `OnNewConsensus(payload)`
  * `OnNewHeaders(headers)`
  * `OnNewTransaction(transaction)`
  * `OnPersistCompleted(block)`
  * `OnReceive(message)` 基于Actor模型的Akka框架中的消息接收函数
  * `OnRegister()`
  * `Persist(block)` 保存一个区块数据，根据区块内的交易以及合约，更改所有相关的状态数据
  * `ProcessAccountStateDescriptor(descriptor, snapshot)`
  * `ProcessValidatorStateDescriptor(descriptor, snapshot)`
  * `Props(system, store)` 创建一个`Blockchain`的Actor代理对象
  * `SaveHeaderHashList(snapshot)` 将区块的Hash保存到LevelDB，累积收到超过2000个新区块Hash时才会进行保存
  * `UpdateCurrentSnapshot()`