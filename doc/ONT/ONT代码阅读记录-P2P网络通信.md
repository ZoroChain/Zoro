# ONT代码阅读记录 - P2P网络通信
## 主要代码的对象结构
* 相关代码存放在p2pserver目录下
### `Peer`
* peer/peer.go
* P2P节点对象，在每次建立一个网络连接后会创建一个对应的Peer对象
* 每个`Peer`管理两条P2P网络连接，其中`SyncLink`是仅用来同步数据的，`ConsLink`是用来收发共识消息的
### `NbrPeers`
* peer/nbr_peers.go
* `Peer`列表的管理对象，负责管理所有的`Peer`
### `NetServer` - netserver.go
* net/netserver.go
* 提供主动连接远程节点的`Connect`函数
* 提供侦听和处理远程节点发来的连接请求的函数
* 提供广播和单点发送消息的函数
* 封装了`NbrPeers`的相关函数，对外提供上层函数接口
### `BlockSyncMgr`
* block_sync.go
* 负责从远程节点同步区块头和区块数据
### `P2PActor`
* actor/actor.go
* 负责处理由其他程序模块投递来的，和P2P网络通信相关的Actor消息
### `P2PServer`
* p2pserver.go
* P2P网络通信模块的管理类，封装了`NetServer`和`BlockSyncMgr`的相关函数接口
* 提供包括启动P2P网络，连接种子节点，发送广播消息，保持通信，超时无响应处理，节点之间的断线重连，记录和连接最近连接节点等相关功能

## 主程序的初始化流程
### main.go - `initP2PNode`
* 首先创建和启动`P2PServer`和`P2PActor`对象，并在`P2PServer`对象里记录`P2PActor`的`PID`
 ```
    p2p := p2pserver.NewServer()
    p2pActor := p2pactor.NewP2PActor(p2p)
    p2pPID, err := p2pActor.Start()
    ...
    p2p.SetPID(p2pPID)
    err = p2p.Start()
```
* 然后把相关的`Actor`的`PID`记录到需要使用该`Actor`的对象里，以便进行模块间通信
  * 在`P2PServer`里记录交易池对象的`TxActor`的`PID`
  * 在`TXPoolServer`里记录`P2PActor`的`PID`
  * 在http模块里记录`P2PActor`的`PID`
```
    netreqactor.SetTxnPoolPid(txpoolSvr.GetPID(tc.TxActor))
    txpoolSvr.RegisterActor(tc.NetActor, p2pPID)
    hserver.SetNetServerPID(p2pPID)
```
* 最后等待P2P网络连接数量达到设定值，初始化结束
```
    p2p.WaitForPeersStart()
    log.Infof("P2P init success")
    return p2p, p2pPID, nil
```    

## 网络消息处理流程
### msg_handler.go
* `version-verack`消息
  * 在`Connect`函数里发送`version`消息
  * 在处理`version`消息时发送`verack`消息
  * 在处理`verack`消息时调用`Connect`函数建立用于收发共识消息的P2P连接
* `getaddr-addr`消息
  * 在处理`verack`消息时会发送`getaddr`获取可连接的节点地址列表
  * 之后定时触发随机向一个已连接的节点发送`getaddr`
  * 在处理`getaddr`消息时收集已完全连接的节点地址列表，发送`addr`
* `ping-pong`消息
  * 定时触发发送`ping`消息，消息里附带当前的区块高度
  * 发送`pong`消息作为回应，同样附带当前的区块高度
* `getheaders-headers`消息
  * 收到`getheaders`消息时，一次最多发送500个区块头
  * 收到`headers`消息时，转调`BlockSyncMgr`的`OnHeaderReceive`，记录区块头数据
* `inv-getdata`消息
  * 收到`inv`消息时，先判断在本地db里是否已经有该数据，没有的话通过发送`getdata`消息来同步数据
  * 收到`getdata`消息时，如果是`block`，先从本地缓存中取数据，没有的话再从db里取数据，发送给请求方 
  * 如果是`tx`直接从db里取数据并发送给请求方
  * `getdata`消息没有处理`consensus`类别的共识消息，也就是说共识消息是不用先发inv消息，直接发送数据的
* `block`消息
  * 通过`AppendBlock`消息将block投递到`P2PServer`，用`BlockSyncMgr`的`OnBlockReceive`函数进行处理
* `tx`消息
  * 通过actor/req/txnpool.go里的`AddTransaction`函数，将收到的交易投递到`TxnPool`模块处理
* `consensus`消息
  * 先验证消息里的ConsensusPayload，通过后投递给共识模块处理