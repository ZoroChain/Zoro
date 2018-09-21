# NEO节点的多线程结构
---
## 一、主线程
* NEO是基于网络消息驱动的程序流程，`LocalNode`的`Start`函数可以视为主线程的启动点
* 在与远程节点建立网络连接后，本地节点不断侦听来自远程节点的消息包，收到后执行对应的处理流程
* 为了不造成阻塞，所有网络消息的收发和处理采用异步函数的机制运行

## 二、网络通信线程
* `LocalNode`和`RemoteNode`类负责NEO的网络通信相关的程序逻辑
  * `LocalNode`表示本地节点，只有一个实例存在
  * `RemoteNode`表示远程节点，每次建立一个新的网络连接后，都会创建一个`RemoteNode`对象，用来表示连接上的远程节点
  * `RemoteNode`使用异步函数机制，使得多个远程节点可以并行的收发网络消息
* 线程1：`LocalNode`的`ConnectToPeersLoop`
  * 作用：负责主动与其他远程节点建立网络连接的线程
  * NEO节点启动后，会加载一份上次运行时保存的远程节点列表(`peers.dat`)，并尝试与它们建立连接
  * 如果没有保存过的远程节点列表，会尝试与种子节点建立连接（在`protocol.json`的`SeedList`里配置）
  * 连接上任意的远程节点后，会定时请求该节点已连接的节点地址列表，记录到本地，并尝试与它们建立连接
  * 如果当前已经连接了10个远程节点，则不会再主动连接其他的远程节点
* 线程2：`LocalNode`的`AddTransactionLoop`
  * 作用：负责检查收到的交易数据是否合法，
  * 本地节点会因为各种原因收到其他节点发来的交易数据
  * 收到后会先临时记录在temp_pool里，等待后台线程进行校验
  * 校验通过的交易会被记录到本地节点的mem_pool里
* 异步函数1：`LocalNode`的`AcceptPeers`
  * 等待并接受用Socket发起的网络连接请求    
* 异步函数2：`LocalNode`的`ProcessWebSocketAsync`
  * 等待并接受用WebSocket发起的网络连接请求 
* 异步函数3：`RemoteNode`的`StartSendLoop`
  * 向远程节点发送消息队列里的消息包
* 异步函数4：`RemoteNode`的`ReceiveMessageAsync`
  * 等待并接收远程节点发送的消息包

## 三、区块链的数据存储线程
* `LevelDBBlockchain`类负责NEO区块链的数据存储
* 线程函数：`PersistBlocks`
  * 负责向LevelDB中保存Block数据，有两种可能的情况：
    * 1.收到远程节点发来的Block，可能是自己主动请求同步区块时收到的，也可能是有新的块生成了
    * 2.cli或gui为了加速区块同步过程，加载了本地的区块缓存文件
  * 本地节点在广播一个Block的同时，会把该Block加入到`LevelDBBlockChain`的临时缓存区`block_cache`里
  * 在线程函数里会根据记录的当前同步高度，从临时缓存区里依次取出Block，并写入LevelDB

## 四、钱包账户的数据存储线程
* `WalletIndexer`类负责存储钱包关联的账户和交易
* 线程函数：`ProcessBlocks`

## 五、RpcServer的指令处理
* `RpcServer`类负责提供基于HTTP接口的指令处理功能
* 异步函数：`ProcessAsync`
  * 处理各种json格式的指令
  * 相关的指令集可参考NEO技术文档<http://docs.neo.org/zh-cn/node/cli/apigen.html>
