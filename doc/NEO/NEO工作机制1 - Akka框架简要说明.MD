# NEO中使用的Akka框架简要说明
### Akka框架的简要说明
* Akka是一套基于Actor模型的，适用于高并发的分布式系统的消息通信框架程序
* Akka是用Scala语言开发，同时提供Java和Scala两种接口
* NEO 2.9中使用了Akka.Net来替代2.8版本里的异步函数进行网络消息的收发处理

### Akka的Actor模型的简要说明
* Actor可以理解为一个能够收发和处理网络消息的实体，消息收发和处理都是异步方式的
* Actor收发的网络消息，可以用一个任意的C#对象来定义
* Actor的主要函数有
  * `Tell(message)` 投递一个消息
  * `Ask(message)` 投递一个消息，并等待该消息的处理结果
  * `OnReceive(message)` 消息处理函数
* Actor的主要成员有
  * `Sender` 消息的发送方
  * `Self` 自己
* Actor的创建方法
  * 先用`Akka.Actor.Props.Create`创建一个Actor的Props
  * 再使用`ActorSystem.ActorOf(Props)`来创建一个Actor
  * 或者使用`Context.ActorOf(Props)`来创建一个Actor，这里的`Context`是Akka.Actor.UntypedActor类的静态方法
* ActorSystem
  * Actor的管理类
  * 内部会创建线程池，一般每个程序只用一个ActorSystem实例

### NEO 2.9中的Actor模型使用方式
* NEO 2.9中把以下几个对象作为Actor来使用
  * `Connection`
  * `Peer`
  * `ProtocolHandler`
  * `TaskManager`
  * `Blockchain`
  * `ConsensusService`

* NEO 2.9中的ActorSystem
  * `NeoSystem` 类里创建了ActorSystem
  * `NeoSystem` 构造函数里会创建Blockchain，LocalNode，TaskManager的Actor对象
  * `StartConsensus` 创建ConsensusService对象，发Start消息，启动共识服务
  * `StartNode` 发Start消息，启动LocalNode
  * `StartRpc` 创建RpcServer对象，启动RPC服务

* `Blockchain`的消息对象
  ```
  public sealed class Blockchain : UntypedActor
  {
    public class Register { }
    public class ApplicationExecuted { public Transaction Transaction; public ApplicationExecutionResult[] ExecutionResults; }
    public class PersistCompleted { public Block Block; }
    public class Import { public IEnumerable<Block> Blocks; }
    public class ImportCompleted { }
    ...
  }
  ```

* 向`Blockchain`投递消息的范例代码
  ```
  Program.NeoSystem.Blockchain.Tell(new Blockchain.Register(), actor);
  system.Blockchain.Tell(inventory);
  system.Blockchain.Tell(payload.Headers, Context.Parent);
  RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(tx).Result;
  RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(block).Result;
  ```
  
* `Blockchain`的消息处理函数
  ```
  protected override void OnReceive(object message)
  {
    switch (message)
    {
      case Register _:
          OnRegister();
          break;
      case Import import:
          OnImport(import.Blocks);
          break;
      case Header[] headers:
          OnNewHeaders(headers);
          break;
      case Block block:
          Sender.Tell(OnNewBlock(block));
          break;
      case Transaction transaction:
          Sender.Tell(OnNewTransaction(transaction));
          break;
      case ConsensusPayload payload:
          Sender.Tell(OnNewConsensus(payload));
          break;
      case Terminated terminated:
          subscribers.Remove(terminated.ActorRef);
          break;
    }
  }
  ```

* 消息投递的范例代码
  * `NeoSystem`启动本地节点
  ```
  LocalNode.Tell(new Peer.Start
            {
                Port = port,
                WsPort = ws_port
            });
  ```
  * `RpcServer`收到"sendrawtransaction"指令后的处理
  ```
  case "sendrawtransaction":
  {
    Transaction tx = Transaction.DeserializeFrom(_params[0].AsString().HexToBytes());
    RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(tx).Result;
    return GetRelayResult(reason);
  }
  ```
  * `LocalNode`向远程节点广播数据清单
  ```
  Connections.Tell(new RemoteNode.Relay { Inventory = inventory });
  ```

### NEO 2.9中Actor的消息队列
* Actor的消息队列
  * `NeoSystem`在创建`Akka`的`ActorSystem`时，使用了自定义的`Mailbox`类型
  * 这些自定义的`Mailbox`继承自`PriorityMailbox`类，并通过重载`IsHighPriority`和`ShallDrop`函数来实现划分消息优先级和保持消息在队列中的唯一性
  * `IsHighPriority`函数根据传入的消息类型，返回`true`或者`false`来表示是否是高优先级的消息
  * `ShallDrop`函数根据传入的消息类型，检查消息队列里是否已经有该类消息，并返回`true`或者`false`来表示是否要丢弃这个消息
    * 需要回应大量数据的消息，例如`getblocks`，`getheaders`，`getdata`，`getaddr`都被设置为消息对列里只能有一份
  * 在创建`Actor`时，通过`Props.WithMailbox`函数来绑定对应的消息队列