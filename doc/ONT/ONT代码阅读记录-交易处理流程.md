# ONT代码阅读记录 - 交易处理流程
### 相关程序模块的简介 - txnpool
#### txnpool/common
* transaction_pool.go
  * `TXPool`交易池对象，记录所有已验证通过未上链交易
#### txnpool/proc
* txnpool_server.go
  * 交易池管理对象，由它来管理交易池的所有`Actor`、`Worker`和`Validator`对象
  * 内部维护Pending队列和TxPool，分别用来记录待验证交易和已验证交易
* txnpool_actor.go
  * 和交易池相关的Actor模型消息处理对象
  * 分为`TxActor`, `TxPoolActor`, `VerifyRspActor`三种Actor对象
    * `TxActor`用来处理来自P2P网络和RPC的交易，属于低优先级消息
    * `TxPoolActor`用来处理来自共识模块的高优先级消息
    * `VerifyRspActor`用来处理`Validator`的验证结果消息
* txnpool_worker.go
  * 由`TXPoolServer`来管理和调度，将需要验证的交易发送给`Validator`，并处理`Validator`的验证结果
* stateful_validator.go
  * 查询数据库，检查交易Hash是否重复
* stateless_validator.go
  * 检查交易的签名和交易的类型是否正确

### 处理来自HTTP接收到的交易
* 通过RPC或者REST的API接口函数`SendRawTransaction`处理一笔新收到的交易
  * 代码在http/base/rest和http/base/rpc的interfaces.go里
* 再先后调用`SendTxToPool`和`AppendTxToPool`函数把交易法送到交易池
  * 先调用http/common/common.go的`SendTxToPool`
  * 再调用http/base/actor/txnpool.go的`AppendTxToPool`
  * 在`AppendTxToPool`函数里向`TxActor`投递`TxReq`消息
* `TxActor`收到`TxReq`消息后，调用`handleTransaction`函数
  * `handleTransaction`里会对交易进行一系列的检查，包括交易体的大小是否超限，交易ID是否重复，Gas是否正确设置等等
  * 通过检查后，调用`TXPoolServer`对象的`assignTxToWorker`把交易分配给`txPoolWorker`
* `TXPoolServer`的`assignTxToWorker`函数
  * 先调用`setPendingTx`把交易记录到Pending队列里
  * 再通过频道`rcvTXCh`向`txPoolWorker`发送待处理的交易
* `txPoolWorker`在收到来自`rcvTx`频道的交易后，调用`verifyTx`函数
  * 先检查该交易是否是已经验证过，或者正在验证中
  * 再调用`sendReq2Validator`函数，向`Validator`投递`CheckTx`消息
* `Validator`在收到`CheckTx`消息后，对交易进行验证，并返回`CheckResponse`消息
  * 目前有stateful和stateless两种`Validator`，分别执行不同的验证流程
  * 验证通过后`Validator`向`VerifyRspActor`返回`CheckResponse`消息
  * `VerifyRspActor`收到`CheckResponse`消息后，调用`TXPoolServer`的`assignRspToWorker`函数
  * `assignRspToWorker`通过`rspCh`频道把验证结果返回给`txPoolWorker`
* `txPoolWorker`在收到来自`rspCh`频道的消息后调用`handleRsp`
  * 调用`TXPoolServer`的`addTxList`函数把交易添加到已验证队列
  * 调用`TXPoolServer`的`removePendingTx`函数把交易从Pending队列里移除
* `TXPoolServer`的`removePendingTx`函数
  * 如果交易验证通过，会向`NetActor`发送该交易数据，由后者将通过验证的交易广播到P2P网络
  * 调用`replyTxResult`函数，将交易的验证结果返回给Http的发起方

### 处理P2P网络收到的交易
* 在msg_router.go里，通过`RegisterMsgHandler`注册了对于交易数据的处理函数`TransactionHandle`
  * 在收到来自P2P网络的交易数据时，会调用msg_handler.go里的`TransactionHandle`函数
  * 在`TransactionHandle`里调用http/base/actor/txnpool.go的`AddTransaction`函数
  * 在`AddTransaction`函数里向`TxActor`投递`TxReq`消息
  * 之后的处理流程和上面的相同