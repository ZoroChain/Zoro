## NEO源码阅读记录 - Wallet模块
#### Wallet
* 钱包的基础类
* 在Implementations/Wallets里有两个实现类
  * `UserWallet:`老版本的钱包，db3格式
  * `NEP6Wallet:`基于NEP6标准的钱包，json格式
* 提供和钱包相关的操作，例如创建账户，获取账户地址，查询资产余额之类的接口
#### WalletIndexer
* 钱包相关的LevelDB数据库