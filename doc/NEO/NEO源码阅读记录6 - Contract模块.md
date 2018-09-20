## NEO源码阅读记录 - Contract模块
#### ApplicationEngine
* 继承自VM模块的`ExecutionEngine`，`ExecutionEngine`负责智能合约虚拟机的运行机制
* `ApplicationEngine`结合了LevelDB中的状态类数据的操作接口，用来构造一个NEO区块链虚拟机的运行环境

* 构造函数
  * 创建一个虚拟机运行所需的基本环境，参数中包括脚本触发类型，脚本容器（例如交易），代码交互接口，为运行脚本支付的Gas，是否不检查Gas消耗
  * 脚本触发类型
    * 分为Verification和Application两种，前者被称为鉴权触发，后者是交易调用触发
    * 由于鉴权触发的合约是UTXO模型的鉴证过程，是在交易被写入区块之前执行，如果合约返回false或者发生异常，则交易不会被写入区块
    * 而由交易调用触发的合约，它的调用时机是交易被写入区块以后，此时无论应用合约返回为何以及是否失败，交易都已经发生，无法影响交易中的 UTXO 资产状态
  * 代码交互接口
    * 为智能合约代码提供可调用的系统级函数，例如根据Hash获取一个交易数据
    * 后面会提到的`StateReader`和`StateMachine`都是这个接口的实例对象
  * 为运行脚本支付的Gas
    * 运行合约脚本需要支付一定的Gas作为系统消耗，这个传入的参数表示交易发起人为执行合约支付的Gas
    * 在鉴权触发时，在鉴权触发和`invokescript`触发时，传入的Gas为零
    * 在运行脚本的过程中，每执行一条指令前都会检查剩余的Gas是否够消耗，如果不够会中断执行并返回错误
  * 是否不检查Gas消耗
    * 如果设置为true，则不检查Gas的消耗
      
* `LoadScript`
  * 基类的函数，加载一段合约脚本的二进制字节码数据
* `Execute`
  * 运行已加载的合约脚本
* `Run`
  * 创建一个虚拟机执行环境来运行脚本，运行过程中对数据的修改不会被保存到区块链上
  * 使用`invokescript`的RPC指令时，会用这种方式来运行脚本
 
#### StateReader
* 继承自VM模块的`InteropService`，interop的概念在Visual Studio .NET中也有类似的存在
```
在Visual Studio .NET中，让受管代码对象和非受管对象协同工作的过程称为互用性(interoperability)，通常简称为 interop。
```
* `InteropService`提供了一种函数注册和调用机制，可以向智能合约的代码提供系统或者外部的函数调用，例如获取当前的区块高度
* `StateReader`顾名思义，是一个提供状态查询的类，内部注册了大量的读取各种状态数据的函数，这些函数可以在智能合约的代码里使用
* `StateReader`是在只读类的虚拟机脚本执行时使用的，例如验签，鉴权

#### StateMachine
* 继承自`StateReader`
* `StateMachine`内部注册了创建、删除、修改类的函数，例如合约的创建和销毁，创建账户，写入或删除一个键值存储等
* `StateMachine`是用在执行需要读写类的虚拟机脚本时使用的，也就是`InvocationTransaction`中的应用合约

#### Contract


#### NEP6Contract