# NEO 2.9源码阅读记录 - Akka框架简要说明
### Akka框架的简要说明
* Akka是一套基于Actor模型的，适用于高并发的分布式系统的消息通信框架程序
* Akka是用Scala语言开发，同时提供Java和Scala两种接口
* NEO 2.9中使用了Akka C#来替代2.8版本里的异步函数进行网络消息的收发处理

### Akka的Actor模型的简要说明
* Actor可以理解为一个能够收发和处理网络消息的实体，消息收发和处理都是异步方式的
* Actor收发的网络消息，可以用一个任意的C#对象来定义
* Actor的主要函数有
  * `Tell(message)` 投递一个消息
  * `Ask(message)` 投递一个消息，并询问该消息的处理结果
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
  * `NeoSystem` 构造函数里会创建Blockchain，LocalNode，TaskManager
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