## NEO源码阅读记录 - Contract模块
#### ApplicationEngine
* 继承自VM模块的`ExecutionEngine`，`ExecutionEngine`负责智能合约虚拟机的运行机制
* `ApplicationEngine`结合了LevelDB中的状态类数据的操作接口，用来构造一个NEO区块链虚拟机的运行环境

* 构造函数
  * 创建一个虚拟机运行所需的基本环境，参数中包括脚本触发类型，脚本容器（例如交易），为运行脚本支付的GAS，是否不检查GAS消耗
  * 脚本触发类型
    * 分为Verification和Application两种，前者被称为鉴权触发，后者是交易调用触发
    * 由于鉴权触发的合约是UTXO模型的鉴证过程，是在交易被写入区块之前执行，如果合约返回false或者发生异常，则交易不会被写入区块
    * 而由交易调用触发的合约，它的调用时机是交易被写入区块以后，此时无论应用合约返回为何以及是否失败，交易都已经发生，无法影响交易中的 UTXO 资产状态
  * 为运行脚本支付的GAS
    * 运行合约脚本需要支付一定的GAS作为系统消耗，这个传入的参数表示交易发起人为执行合约支付的GAS
    * 在鉴权触发时，在鉴权触发和`invokescript`触发时，传入的GAS为零
    * 在运行脚本的过程中，每执行一条指令前都会检查剩余的GAS是否够消耗，如果不够会中断执行并返回错误
  * 是否不检查GAS消耗
    * 如果设置为true，则不检查GAS的消耗
* `Execute`
  * 运行已加载的合约脚本
  * 在这里逐条运行字节码，并在运行前检查GAS的消耗
* `Run`
  * 创建一个虚拟机执行环境来运行脚本，运行过程中对数据的修改不会被保存到区块链上
  * 使用`invokescript`的RPC指令时，会用这种方式来运行脚本
 
#### StandardService
* 继承自VM模块的`InteropService`
* `StandardService`内部注册了以`System`作为命名空间的系统函数，这些函数可以在智能合约的代码里调用
* `StandardService`是在只读类的虚拟机脚本执行时使用的，运行过程中不会向DB中保存数据，例如验签，鉴权
* 在创建`StandardService`对象时，会传入一份数据库的`Snapshot`，通过该`Snapshot`获取和修改数据

#### NeoService
* 继承自`StandardService`
* `NeoService`内部注册了以`NEO`作为命名空间的系统函数
* 相对于`StandardService`，`NeoService`提供了更丰富完整的系统函数

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
  * `CreateSignatureRedeemScript(ECPoint publicKey):`*byte[]*
    * 创建一段用于验证签名的虚拟机字节码
  * `CreateMultiSigRedeemScript(int m, params ECPoint[] publicKeys):`*byte[]*
    * 创建一段用于验证多个签名的虚拟机字节码