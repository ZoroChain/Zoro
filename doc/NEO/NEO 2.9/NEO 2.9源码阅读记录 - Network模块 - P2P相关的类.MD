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
* 抽象类，继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
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
    * Actor的消息处理函数，根据收到的消息类型，调用对应的处理函数
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
* 本地通信节点，继承自`Peer`，只有一个实例存在，负责向已连接的远程节点发送广播消息

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
    * Actor的消息处理函数
  
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
    * 调用以下代码把消息投递给所有的RemoteNode
    ```
    Connections.Tell(new RemoteNode.Relay { Inventory = inventory });
    ```
    * `RemoteNode`收到后会调用`OnRelay(inventory)`，把该数据的清单发送到远程节点
  
  * `OnSendDirectly(inventory)` 
    * 收到了LocalNode.SendDirectly消息时触发
    * 调用`Connections.Tell(inventory);`把消息投递给所有的RemoteNode
    * `RemoteNode`收到后会调用`OnSend(inventory)`，把数据发送到远程节点

* 消息投递的范例代码
  ```
  system.LocalNode.Tell(new LocalNode.Relay { Inventory = block });
  ```
  * 用Relay消息向LocalNode投递一个block
  * LocalNode收到消息后会执行`OnRelay(block);`

### Connection
* 抽象类，继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
* `Connection`表示一个远程节点的网络连接，负责和该节点之间的网络通信

* 消息对象
  * `Timer` 定时器消息
  * `Ack` TCP确认消息，用于流量控制，避免不断的发消息造成远程节点的端口拥堵

* 主要成员变量
  * `timer` 
    * 计时器，超过时间没有收到消息，会主动断开连接，
    * 刚建立连接时设为10秒，收到第一个消息之后设为1分钟
  * `tcp`
    * TCP连接的Actor对象
    * 可调用`tcp.Tell`来向远程节点发消息
  * `ws`
    * WebSocket对象
    * 可调用ws对象提供的函数和远程节点进行消息收发

* 主要函数
  * `OnReceive(message)`
    * Actor的消息处理函数
  * `OnAck`
    * 远程节点每次收完一个消息后会发回Ack消息，表示接收成功
    * 收到了Ack消息，表示可以接着发送下一个消息了
  * `OnReceived(data)`
    * 收到了消息主体，调用RemoteNode.OnData做进一步处理
  * `OnData`
    * 虚函数，在RemoteNode里实现
  * `Disconnect`
    * 主动和远程节点中断连接
    * 超过时间没有收到消息时会触发
    * RemoteNode在处理消息的过程中发生异常时会触发
  * `SendData`
    * 向远程节点发送消息
  * `WsReceive`
    * 接收WebSocket的消息

* Ack方式的流量控制
  * 发送消息的一方，每次发送完一个消息后，会等待接收方发回Ack确认消息
  * 接收方在完整接收完一个消息后，会向发送方发送Ack消息
  * 发送方在收到Ack消息后，才会继续向接收方发送后续的消息

### RemoteNode
* 远程通信节点，继承自`Connection`，每连接上一个远程节点，会为该节点创建一个RemoteNode
* `RemoteNode`负责和远程节点之间的消息收发

* 消息对象
  * Relay 向远程节点发送数据的清单

* 主要成员变量
  * `protocol` 处理NEO消息的Actor对象
  * `message_queue_high` 高优先级的消息队列
  * `message_queue_low` 低优先级的消息队列
  * `msg_buffer` 消息接收缓存区
  * `ack` 标记是否收到了Ack确认消息，只有为true时，才会继续发送消息队列里的消息
  * `verack` 标记是否收到了版本确认消息，为true时，已完成网络连接的初始化流程

* 主要函数
  * `RemoteNode`构造函数
    * 每次建立一个网络连接时会创建RemoteNode对象，执行构造函数
    * 构造函数内部会创建`ProtocolHandler`的Actor对象，用来处理NEO消息
    * 把自己记录到LocalNode的远程节点列表里，以便于响应其他节点发来的请求可连接的节点地址的消息
    * 向远程节点发送version消息
  * `CheckMessageQueue`
    * 按照优先级高低，发送消息队列中的消息
    * 每次发送一个消息，必须先收到Ack确认后才会发送
  * `EnqueueMessage`
    * 把要发送的消息加入到消息队列
  * `OnReceive`
    * Actor的消息处理函数
  * `OnAck`
    * 收到Ack确认消息，检查消息队列，发送下一个消息
  * `OnData(data)`
    * 收到了消息主体，先加入到接收缓存区，再依次解析处理缓存区里的消息
  * `OnRelay(inventory)`
    * 收到了RemoteNode.Relay消息时触发
    * 向远程节点发送数据的清单
  * `OnSend(inventory)`
    * 收到了一个inventory对象时触发
    * 向远程节点发送数据的实体
  * `OnSetVerack`
    * 收到了verack消息时触发
    * 表示和该远程节点的网络连接初始化已经完成，可以正常发送消息了
  * `OnSetVersion`
    * 收到了version消息是触发
    * 会检查是否是重复的连接
    * 通过检查后向对方发送verack消息作为确认
  * `SendMessage`
    * 调用`Connnect.SendData`向远程节点发送消息
    * 把`ack`标记为`false`，等待接收对方的Ack消息
  * `TryParseMessage`
    * 尝试从接收缓存区里提取一个消息对象
    * 只有在接收了一个消息包的完整数据后，才能从缓存区里提取出消息对象
  * `Props`
    * 创建Actor的配置对象，返回值作为参数调用`ActorSystem.ActorOf`得到Actor对象

### ProtocolHandler
* 继承自`Akka.Actor.UntypedActor`，作为一个Actor实体，可以收发和处理消息
* `ProtocolHandler` 负责解析和处理NEO的各种网络消息

* 消息对象
  * `SetVersion` 通知`RemoteNode`收到了version消息
  * `SetVerack` 通知`RemoteNode`收到了verack消息
  * `SetFilter`

* 主要成员变量
  * `knownHashes:`*HashSet<UInt256>* 
    * 在收到`inv`消息时，记录清单的Hash
    * 下次再收到`inv`消息时，过滤掉已记录过的Hash
    * 避免重复请求相同的数据
  * `sentHashes:`*HashSet<UInt256>* 
    * 在收到`getdata`消息时，记录清单的Hash
    * 下次再收到`getdata`消息时，过滤掉已记录过的Hash
    * 避免重复发送相同的数据
  * `version:`*VersionPayload* 远程节点发来的版本消息主体
  * `verack` 标记是否收到了`verack`消息，为true时，已完成网络连接的初始化流程
  * `bloom_filter`

* 主要成员函数
  * `OnReceive`
    * Actor的消息处理函数
 
  * `OnVersionMessageReceived`    
    * 收到了`version`消息时触发
    * 必须是建立连接后收到的第一个消息
    * 记录收到的版本消息主体，并通知RemoteNode
  
  * `OnVerackMessageReceived`
    * 收到了`verack`消息时触发
    * 把`verack`标记为true，表示已完成网络连接的初始化流程
  
  * `OnGetAddrMessageReceived`
    * 收到了`getaddr`消息时触发
    * 用`addr`消息向对方发送已连接的远程节点地址
  
  * `OnAddrMessageReceived`
    * 收到了`addr`消息时触发
    * 通知`LocalNode`记录远程节点的地址
    
  * `OnGetHeadersMessageReceived`
    * 收到了`getheaders`消息时触发
    * 用`headers`消息向对方发送区块头数据
    * 每次最多发送2000个区块头
  
  * `OnHeadersMessageReceived`
    * 收到了`headers`消息时触发
    * 通知`Blockchain`记录收到的区块头数据
    
  * `OnGetBlocksMessageReceived`
    * 收到了`getblocks`消息时触发
    * 用`inv`消息向远程节点发送本地的区块清单列表，一次最多500个

  * `OnInvMessageReceived`
    * 收到了`inv`消息时触发
    * 先过滤掉已发送过获取数据请求的或者是数据在本地已经存在的清单
    * 再向`TaskManager`投递`NewTasks`消息，让后者做进一步处理
    * `TaskManager`会向远程节点发送`getdata`消息
  
  * `OnGetDataMessageReceived`
    * 收到了`getdata`消息时触发
    * 按照所请求的数据类型，向远程节点发送`tx`，`block`或者`consensus`数据实体
  
  * `OnInventoryReceived`
    * 收到了`tx`，`block`或者`consensus`的数据实体时触发
    * 向`LocalNode`发送`Relay`消息，让`Blockchain`来处理收到的数据
  
  * `OnMemPoolMessageReceived`
    * 收到了`mempool`消息时触发
    * 向远程节点发送本地`mem_pool`里的交易清单

  * `OnFilterAddMessageReceived`    
  * `OnFilterClearMessageReceived`
  * `OnFilterLoadMessageReceived`

  * `Props`
    * 创建Actor的配置对象，返回值作为参数调用`ActorSystem.ActorOf`得到Actor对象

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
  * `knownHashes` 记录已完成同步的数据Hash，在`OnNewTasks`里用来过滤请求同步的Hash，避免重复请求本地已有的数据
  * `globalTasks` 记录正在请求同步的数据Hash，在`OnNewTasks`和`RequestTasks`里被用来过滤本地已有的区块数据，避免重复请求已有的区块数据
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