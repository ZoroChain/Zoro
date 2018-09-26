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

### TaskManager

