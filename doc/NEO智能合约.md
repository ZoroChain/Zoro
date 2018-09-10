# NEO 智能合约

NEO 中智能合约分为应用合约和鉴权合约
* 应用合约是用户通过发送交易来触发合约执行，只有交易之星完成后合约才被调用，所以不论合约执行结果如何，交易都会发生。
* 鉴权合约是一个合约账户，用户使用账户中的一笔资产时会触发合约。鉴权合约的触发是UTXO模型的鉴证过程，是在交易被写入区块之前执行。如果合约返回false或者发生异常，则交易不会被写入区块。


## NEO 智能合约开发流程
### 开发
#### 必须工具：
`安装配置过程：`<http://docs.neo.org/zh-cn/sc/quickstart/getting-started-csharp.html>
* NeoContractPlugin 插件
* 智能合约编译器 用来将c#代码 和 java代码编译为智能合约指令，neo-compiler <https://github.com/neo-project/neo-compiler>
#### 编写合约
VS中创建NEOContract项目进行开发，NeoVM的数据类型说明 <http://docs.neo.org/zh-cn/sc/quickstart/limitation.html>
#### 部署合约
* C#或Java合约在编译时会产生中间语言 MSIL， neo-compiler 编译器通过 Mono.Ceill 将中间语言编译成 NeoVM 的字节码，生成.avm文件；
* 发布合约的时候，去读取.avm文件(合约脚本)并记录合约脚本hash，然后构造一笔InvocationTransaction类型的交易，合约脚本附加在该交易的数据中，不同的合约所需费用也不同，具体见 <http://docs.neo.org/zh-cn/sc/systemfees.html>，该交易其实就是给一个空地址发送合约费用，交易完成后，该合约会随着交易的打包被记录在区块链上；
* 合约脚本hash是后续测试和调用合约的依据。
#### 调用合约
* 调用合约时需要：合约脚本hash、合约中的方法名称、方法需要传入的参数；
* 调用合约也是构造一笔InvocationTransaction类型的交易，方法名和参数附加在交易数据中，交易发出之后NeoVM会运行合约，返回运行结果。
