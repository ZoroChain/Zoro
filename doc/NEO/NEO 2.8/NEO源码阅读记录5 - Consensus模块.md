## NEO源码阅读记录 - Consensus模块
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