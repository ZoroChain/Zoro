# NEO 2.9源码阅读记录 - Network模块 - RPC相关的类
### Message
* NEO网络节点之间通信时发送的消息体
* 继承自ISerializable
* 主要接口函数
  * `int Size()` 获得对象数据的字节数
  * `Serialize(BinaryWriter writer)` 序列化
  * `Deserialize(BinaryReader reader)` 反序列化
  * `Create(string command, byte[] payload)` 静态函数，创建一个消息体
* 主要成员
  * `string Command:`类型字符串
  * `uint Checksum:`校验和
  * `byte[] Payload:`数据区

### 各种Payload类
  * Payload意为有效负载，指网络通信中传输的数据包里，承载实际数据的区域。在这里可以理解为Message消息里的实际数据区
  * 可以将Message里的Payload转换成对应类型的Payload类，例如：
     ```
    Block block = message.Payload.AsSerializable<Block>();
    AddrPayload payload = message.Payload.AsSerializable<AddrPayload>();
    ``` 
### Peer
* 继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
* `Peer`表示一个P2P网络的通信节点，负责建立P2P网络的连接

* 消息对象
  * `Start:` 启动节点
  * `Peers:` 远程节点地址列表
  * `Connect:` 建立了TCP连接
  * `WsConnected:` 建立了WebSocket连接
  * `Timer:` 本地计时器

* 主要成员变量
  * `ConnectedAddresses:` *Dictionary<IPAddress, int>* 记录每个IP地址的Socket连接数，限制每个地址的重复连接数不能超过3个
  * `ConnectedPeers:` *ConcurrentDictionary<IActorRef, IPEndPoint>* 记录已连接的远程节点的Actor对象和地址
  * `UnconnectedPeers:` *ImmutableHashSet<IPEndPoint>* 还未连接的远程节点的地址列表
  * `Connections:` *ActorSelection* Actor筛选器，这里返回所有已连接的远程节点的Actor对象，可以用`Connections.Tell`向所有远程节点广播消息

* 主要函数
  * `ConnectToPeer(endPoint)`
    * 用TCP协议连接一个远程节点
  * `OnReceive(message)`
    * 消息处理函数，根据收到的消息类型，调用对应的处理函数
  * `OnStart(port, ws_port)`
    * 启动本地节点，开始接收远程节点的连接请求，并定时主动连接远程节点
  * `OnTcpConnected(remote, local)`
    * 建立了一个TCP连接
    * 检查本次连接的IP地址的重复连接数是否超过上限（3个），超过则断开连接
    * 创建一个RemoteNode，并记录到`ConnectedPeers`里
  * `OnTerminated(actorRef)`
    * 网络连接断开，更新`ConnectedPeers`和`ConnectedAddresses`
  * `OnTimer()`
    * 启动时会创建一个定时器，每5秒触发一次
    * 在该定时器函数里会向已连接的节点请求其他节点的地址，并主动和远程节点建立连接
  * `OnWsConnected(ws, remote, local)`
    * 建立了一个WebSocket连接
    * 检查本次连接的IP地址的重复连接数是否超过上限（3个），超过则断开连接
    * 创建一个RemoteNode，并记录到`ConnectedPeers`里
  * `ProcessWebSocketAsync(context)`
    * 接受WebSocket连接请求的异步函数
    * 当有连接请求时，会向`Peer`发送`WsConnected`消息

* 主动连接远程节点的程序逻辑:
  * 第一次运行的节点，会先连接上protocol.json里记录的seed节点
  * 连接上以后，会向对方请求其他可以连接的节点地址
  * 在收到可连接的节点地址后，会记录在`UnconnectedPeers`里，并在此后尝试连接
  * 已连接节点数达到或超过10个以后，不再主动连接其他节点

### LocalNode
* 本地通信节点，继承自`Peer`，只有一个实例存在，负责处理NEO节点的各种网络消息

* 消息对象
  * `Relay` 先在本地处理，然后再广播到远程节点
  * `RelayDirectly` 向远程节点广播数据的清单
  * `SendDirectly` 向远程节点广播数据，目前只有`ConsensusService`在广播共识消息的时候用到

* 主要成员变量
  * `RemoteNodes:` *ConcurrentDictionary<IActorRef, RemoteNode>* 记录已连接的远程节点，用来响应请求远程节点地址的消息

* 主要成员函数
  * `BroadcastMessage` 
    * 向远程节点广播消息
  * `GetIPEndpointFromHostPort` 
    * 从本地配置文件中获取种子节点的网络地址
  * `NeedMorePeers` 
    * 请求远程节点地址或者加载种子节点地址
  * `OnReceive` 
    * 消息处理函数
  
  * `OnRelay(inventory)` 
    * 收到了LocalNode.Relay消息时触发
    * 调用`system.Blockchain.Tell(inventory);`把消息转给`Blockchain`处理
    * NEO中有三种`inventory`，分别是`Block`，`Transaction`，`ConsensusPayload`
    * `Blockchain`根据`inventory`的类型分别调用不同的函数来处理
      * `OnNewBlock(block)` 根据不同的情况，先在记录未验证列表里记录该`block`，或者是把`block`保存到LevelDB，以及是否要向其他节点转发该数据的清单
      * `OnNewTransaction(block)` 先验证该交易数据是否合法，通过后保存到`mem_pool`里，并向其他节点转发该数据的清单
      * `OnNewConsensus(block)` 如果本地开启了共识，则通知共识模块，并向其他节点转发该数据的清单

  * `OnRelayDirectly(inventory)` 
    * 收到了LocalNode.RelayDirectly消息时触发
    * 调用`Connections.Tell(new RemoteNode.Relay { Inventory = inventory });`把消息投递给所有的RemoteNode
    * `RemoteNode`收到后会调用`OnRelay(inventory)`，把该数据的清单发送到远程节点
  
  * `OnSendDirectly(inventory)` 
    * 收到了LocalNode.SendDirectly消息时触发
    * 调用`Connections.Tell(inventory);`把消息投递给所有的RemoteNode
    * `RemoteNode`收到后会调用`OnSend(inventory)`，把数据发送到远程节点
    
### Connection

### RemoteNode

### ProtocolHandler

### TaskSession
* 任务会话，记录该会话里正在运行的数据同步任务
* 每个远程节点会有一个对应的任务会话
* 主要成员变量
  * `Tasks:`*Dictionary<UInt256, DateTime>* 当前正在运行的任务，记录该任务同步的数据Hash，以及任务的开始时间
  * `AvailableTasks:`*HashSet<UInt256>* 记录被重复请求的数据Hash

### TaskManager
* 继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
* 只有一个实例存在，负责管理NEO节点之间的数据同步流程
* 这里的数据同步是指向远程节点请求`tx`，`block`，`consensus`数据

* 消息对象
  * `Register` 注册一个`TaskSession`
  * `NewTasks` 开始一个数据同步任务，按照清单列表，分批次发送数据同步请求，每批次最多500个
  * `TaskCompleted` 一个数据同步任务完成了，该消息在收到数据实体时会被投递
  * `HeaderTaskCompleted` 同步区块头的任务完成了，该消息在收到区块头的数据时会被投递
  * `RestartTasks` 
  * `Timer` 内部使用的定时器消息，每30秒触发一次

* 主要成员变量
  * `knownHashes` 记录已完成同步的数据Hash，在`OnNewTasks`里用来过滤请求同步的Hash，避免同步相同的数据
  * `globalTasks` 记录已开始同步的数据Hash，在`RequestTasks`里被用来过滤本地已有的区块数据，避免重复请求已有的区块数据
  * `sessions` 会话列表，每个远程节点会有一个会话
  * `timer` 定时器

* 主要函数
  * `OnReceive` 
    * Actor的消息处理函数
  
  * `OnRegister`
    * 收到了`Register`消息时触发
    * 为消息发送者创建一个`TaskSession`，记录到会话列表
    * 开始运行该会话的数据同步任务
  
  * `OnNewTasks`  
    * 当`ProtocolHandler`收到`inv`消息时，会向`TaskManager`投递`NewTasks`消息
    * 按批次向`RemoteNode`投递`getdata`消息，后者会向远程节点请求同步数据
  
  * `OnRestartTasks`
    * 收到了`RestartTasks`消息时触发
  
  * `OnTaskCompleted`
    * 收到了`TaskCompleted`消息时触发，在收到数据实体时会被投递
    * 在`knownHashes`里记录已完成同步的数据Hash
    * 在`globalTasks`里清除掉已完成同步的数据Hash
    * 尝试开始一个新的任务
  
  * `OnHeaderTaskCompleted`  
    * 收到了`HeaderTaskCompleted`消息时触发，在收到区块头的数据时会被投递
    * 清除对应的任务标记
    * 尝试开始一个新的任务

  * `OnTerminated(actor)`
    * 当`TaskSession`的创建者（远程节点）被销毁时触发
    * 从会话列表里清理会话

  * `OnTimer`
    * 每30秒触发一次
    * 检查所有已开始的数据同步任务，超过1分钟的视为超时，会重新同步该数据
    * 尝试开始一个新的任务

  * `RequestTasks(session)`
    * 开始某个会话的一次数据同步任务
    * 只有在所有的任务都已完成的情况下才会开始下一个任务
    * 分三种优先级运行
      * 先取出会话里记录的，因重复而没有开始的任务，运行这些任务
        * 这些记录应该在上一次任务完成时被清除掉，没有清除掉的说明同步过程没有完成，需要重复执行
      * 如果没有需要重复执行的任务，则判断是否需要同步区块头
      * 最后判断是否要同步区块数据