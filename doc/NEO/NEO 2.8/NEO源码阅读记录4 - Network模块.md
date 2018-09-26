## NEO源码阅读记录 - Network模块
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
 
 * `unconnectedPeers ： HashSet<IPEndPoint>`
    * 还未连接的远程节点的地址列表
    * 数据来源于其他远程节点发来的已连接地址列表
    * 在本地节点启动时，会从本地文件中加载（在cli或gui中调用）
    * 在本地节点关闭时，会保存到本地文件（在cli或gui中调用）
    * 本地节点启动后，会优先尝试和这些节点建立连接
 
 * `connectedPeers ： List<RemoteNode>`
    * 已经连接上的远程节点的列表，包括主动和被动两种连接方式
    * 最多连接10个节点，达到或超过后不再主动和其他节点建立连接，但仍可以接受其他节点的连接请求
 
 * `KnownHashes ： Dictionary<UInt256, DateTime>`
   * 记录上一次发送某个清单的时间，或是上一次收到某个数据的时间
   * 避免重复发送或者重复接收相同的数据

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
  * `missions_global：Dictionary<UInt256, DateTime> `
    * 静态变量，所有远程节点共享一个映射表
    * 在请求某一个数据时，会记录请求的发起时间
    * 避免在一分钟内重复请求同一个数据
  * `mission_start：DateTime`
    * 如果超过一分钟还没有收到请求的数据，则断开网络连接
  * `missions：HashSet<UInt256>`
    * 当前正在请求中的数据的数量
    * 用来判断和某个远程节点的网络通信是否空闲，空闲时会用来同步区块数据

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