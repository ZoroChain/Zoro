## ZoroChain的NativeNEP5资产和全局资产
### ZoroChain的NativeNEP5资产
* ZoroChain上的应用，除了可以发布NEP5合约资产外，还可以发布NativeNEP5资产
* 相比NEP5合约资产，NativeNEP5的处理速度会更快，需要的手续费也更低
* NativeNEP5资产是用C#代码实现的链上资产，模仿了NEP5合约资产的相关接口，但只提供最基本的资产转账功能，无法实现复杂的自定义功能和流程
* NativeNEP5资产如果需要跨链使用，需要在相关的链上都发布一次才行

### ZoroChain的NativeNEP5资产实现方案
* 通过"Zoro.NativeNEP5.Create"的SysCall来创建NativeNEP5资产，创建时需要填入名称、货币总量、货币精度、管理员账户等相关的信息
* 和NEO的NEP5合约一样，用20字节的UInt160来表示NativeNEP5的资产ID，这个UInt160实际是Script的Hash
* 通过"Zoro.NativeNEP5.Call"的SysCall来调用NativeNEP5，并在参数里填入NativeNEP5的资产ID和方法名以及参数
* 增加NativeNEP5State用来存储NativeNEP5的信息

### ZoroChain的全局资产
* 全局资产是指Zoro链上的系统资产，在根链和所有应用链上都可使用的资产
* 采用类似NEO和GAS的机制，在创世块中定义全局资产
* 目前BCP和BCT是ZoroChain上的两种全局资产

### ZoroChain的全局资产实现方案
* 通过NativeNEP5来实现全局资产，不再使用NEO原有的Asset和Account
* 在创世块中用交易来创建和分配全局资产，节点启动后，全局资产即已自动创建好了

### ZoroChain的BCP资产
* 用来支付交易的手续费和矿工的奖励
* 在根链和应用链的创世块中创建BCP，并分配到根链上最初的一组共识节点的多签账户
* 应用链的创世块只创建BCP，不做分配处理，默认没有BCP余额
* BCP可以通过跨链兑换机制在根链和应用链之间兑换

### NativeNEP5和NEP5合约的差异
* 通过不同的SysCall来创建和调用
  * NEP5合约通过“Zoro.Contract.Create”来创建，通过AppCall来调用
  * NativeNEP5通过"Zoro.NativeNEP5.Create"来创建，通过"Zoro.NativeNEP5.Call"来调用
* 转账手续费不同
  * NEP5合约转账手续费需要大约4.2个GAS
  * NativeNEP5的转账手续费只需要1个GAS  
