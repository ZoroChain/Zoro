# Ethereum-GO代码阅读笔记

### 交易同步流程
* 分为两种情况：
  * 每当建立一个新的P2P连接时，会把本地节点处于的Pending状态的所有交易同步给新连接的节点
    * 为了控制网络传输的流量，会分多次进行转发，每次转发不超过100KB
  * 之后收到的交易会通过broadcast发送给所有已连接上的节点

### 交易处理流程
* peer.go 
 * `queuedTxs`: 交易队列的channel
 * `broadcast`: 从交易队列的channel里读取数据并发送，在建立P2P链接时开始运行
 * `AsyncSendTransactions`: 向交易队列的channel里添加要发送的交易数据

* handle.go
 * `handleMsg`: 处理远程节点发来的消息
   * `TxMsg`：交易消息
     * 调用`TxPool.AddRemotes`把交易添加到交易池里
 * `txBroadcastLoop`：接受本地程序发来的交易数据，调用`peer.AsyncSendTransactions`把交易数据广播出去

* tx_pool.go
 * `AddRemotes`: 把一批交易数据添加到池里，调用`addTxs`
 * `addTxs`: 先加锁，再调用`addTxsLocked`
 * `addTxsLocked`: 先调用`add`，再调用`promoteExecutables`
 * `add`: 把交易添加到不可执行队列，期间先调用`validateTx`来验证交易数据，再调用`enqueueTx`添加通过验证的交易
 * `validateTx`: 检查交易数据大小是否超过规定、是否有签名签名、Gas价格是否过低，发起人账户余额是否够支付转账金额和手续费等
 * `enqueueTx`: 把交易数据添加到不可执行队列
 * `promoteExecutables`: 从不可执行队列中找出所有可执行的交易，调用`promoteTx`添加到`pending`队列，并发消息通知其他子系统
 * `promoteTx`:把交易加入到`pending`队列

* sync.go
 * `syncTransactions`: 从`pending`队列中取出交易数据，发送到`txsyncCh`频道
 * `txsyncLoop`：从`txsyncCh`频道接收交易数据，并分组发送给新连接上的远程节点
 
### 交易的执行流程
* 分两种情况
  * 由矿工节点运行的交易
  * 其他节点同步块时执行的交易

* worker.go
  * `commitNewWork`: 执行创建新区块的流程
    * `commitTransactions`: 执行一组交易
      * `commitTransaction`：执行一笔交易，调用`ApplyTransaction`
    * `commit`：把新创建的块发送到`taskCh`消息频道
  * `taskLoop`：接收来自`taskCh`的新区块，加入到pending队列，并调用`consensus.Engine.Seal`运行挖矿流程，如果挖矿成功会发送消息到`resultCh`频道
  * `resultLoop`：接收`resultCh`的消息，将新创建的区块数据保存到数据库，并广播该区块

* state_processor.go
  * `ApplyTransaction`: 执行交易，返回交易收据、消耗的Gas和错误信息

* consensus\ethash\sealer.go
  * `Seal`: 执行POW共识流程，尝试通过运算找出符合Block的复杂度要求的Nonce
  * `mine`: 循环调用计算Nonce的算法函数，直到计算出符合要求的Nonce或者被中断运行

* blockchain.go
  * `WriteBlockWithState`: 将区块里的相关数据保存到DB

* handler.go
  * `minedBroadcastLoop`: 收到`NewMinedBlockEvent`消息时，把新产生的区块广播给其他节点
  * `BroadcastBlock`：根据传入的参数，向远程节点发送区块数据，或者是只发送区块的Hash