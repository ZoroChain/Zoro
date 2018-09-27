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
* `Execute`
  * 运行已加载的合约脚本
  * 在这里逐条运行字节码，并在运行前检查Gas的消耗
* `Run`
  * 创建一个虚拟机执行环境来运行脚本，运行过程中对数据的修改不会被保存到区块链上
  * 使用`invokescript`的RPC指令时，会用这种方式来运行脚本
 
#### StateReader
* 继承自VM模块的`InteropService`
* `StateReader`顾名思义，是一个提供状态查询的类，内部注册了大量的读取各种状态数据的函数，这些函数可以在智能合约的代码里调用
* `StateReader`是在只读类的虚拟机脚本执行时使用的，运行过程中不会向DB中保存数据，例如验签，鉴权

#### StateMachine
* 继承自`StateReader`
* `StateMachine`内部注册了创建、删除、修改类的函数，例如合约的创建和销毁，创建资产，写入或删除一个键值存储等
* `StateMachine`是用在执行需要读写类的虚拟机脚本时使用的，也就是`InvocationTransaction`中的应用合约
* 在智能合约的脚本中，所有涉及到修改区块链上数据的操作，都是通过调用`StateMachine`里注册的函数来完成的
* `StateMachine`内部使用Core模块里的多种`State`对象了，通过这些对象将修改后的数据保存到LevelDB里

#### Contract
* 智能合约对象
* 主要成员变量
  * `Script:byte[]`：合约的字节码
  * `ParameterList:ContractParameterType[]`：参数列表
* 主要函数
  * `CreateSignatureContract(ECPoint publicKey)`
    * 创建一个签名合约 
  * `CreateMultiSigContract(int m, params ECPoint[] publicKeys)`
    * 创建一个多签合约