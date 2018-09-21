## NEO源码阅读记录 - VM模块
#### ExecutionContext
* NEO虚拟机的上下文环境，内部主要包括要运行的字节码和一个计算堆栈
* 字节码是在智能合约脚本编写完成后，用官方提供的编译工具`neon`编译而成的
* 计算堆栈`EvaluationStack`，负责在脚本和虚拟机之间传递参数和返回值

#### ExecutionEngine
* NEO中的虚拟机，可以运行智能合约脚本编译后的字节码
* `LoadScript`
  * 加载一段智能合约脚本的字节码数据
* `Execute`
  * 运行已加载的合约脚本
* `ExecuteOp`
  * `ExecutionEngine`最主要的函数
  * 运行一条字节码指令，使用`EvaluationStack`来获取参数，记录结果

#### InteropService
* interop的概念在Visual Studio .NET中也有类似的存在
```
在Visual Studio .NET中，让受管代码对象和非受管对象协同工作的过程称为互用性(interoperability)，通常简称为 interop。
```
* 在NeoVM里，`InteropService`负责向智能合约的代码编写者提供NEO的系统函数调用，例如获取当前的区块高度
* `Register(string method, Func<ExecutionEngine, bool> handler)`
  * 注册一个系统函数
* `Invoke(string method, ExecutionEngine engine)`
  * 调用一个系统函数

#### ScriptBuilder
* 在调用智能合约时，需要手动拼装二进制指令，包括要调用的合约地址，方法，和相关参数
* 通过`ScriptBuilder`的各种`Emit`函数来完成这个步骤，并通过`ToArray`函数获得对应的字节码
* 通常情况有两种方式来使用这些字节码来调用合约：
  * 使用RPC指令`invokescript`，将字节码作为参数一起发送到某个节点
    * 这种方法适合用来执行查询类的合约，或者是预执行一次合约，得到该合约所需要花费的Gas，之后再正式执行
    * 运行过程中不会保存对数据的修改
  * 使用RPC指令`sendrawtransaction`，发起一个交易，将字节码作为交易附带的脚本，一起发送到某个节点
    * 这种方法是正式执行一个合约，需要预备要消耗的Gas，运行过程中修改的数据会保存到LevelDB
