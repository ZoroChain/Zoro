## NEO源码阅读记录 - Wallet模块
#### Wallet
* 钱包的抽象类
* 在Implementations/Wallets里有两个实现类
  * `UserWallet:`老版本的钱包，db3格式
  * `NEP6Wallet:`基于NEP6标准的钱包，json格式
* 提供和钱包相关的操作，例如创建和删除账户，查找和钱包账户关联的交易
* 主要函数
  * `CreateAccount:`创建账户，使用一个随机数作为账户的私钥，依次唯一标识一个账户
  * `GetAvailable(UIntBase asset_id)`查询账户资产余额，根据传入的资产ID类型，分为全局资产和NEP5资产两种不同的处理方式
  * `GetCoins`获得账号下的所有未花费且未行权的货币信息，包括这笔货币的来源，转出和状态

#### WalletAccount
* 账户的抽象类
* 主要成员
  * `ScriptHash:UInt160` 账户地址
  * `KeyPair GetKey()` 获取公私钥对

#### WalletIndexer
* 存储钱包账户相关的索引数据
* 使用独立的线程来记录相关的数据到LevelDB数据库

* 存储的数据
  * `ST_Coin:`
    * 用UTXO模型中的Input为键，Output为值
  * `ST_Transaction:`
    * 用账号地址+交易Hash为键，可以快速查找钱包中的账号相关的所有交易的Hash(Tx id)
  * `IX_Group:`
    * 用区块高度为键，32字节的随机数为值，表示某个区块高度对应的一个组号，用来关联账户地址用
  * `IX_Accounts:`
    * 用`IX_Group`的随机数组号为键，一组账户地址为值，表示某个高度的区块相关联的所有账户地址

* 成员变量
  * `indexes:`*Dictionary<uint, HashSet<UInt160>>*
    * key:区块高度，value:账户列表
    * 表示和某个高度的区块里记录的交易有关的所有账户地址

  * `accounts_tracked:`*Dictionary<UInt160, HashSet<CoinReference>>*
    * 记录某个账号下的所有未行权的货币来源

  * `coins_tracked:`*Dictionary<CoinReference, Coin>*
    * key:交易的Input项，value:Coin对象，包括Input, Output, CoinState
    * 以某个交易的货币来源为索引的货币信息，记录了关联的Input, Output和CoinState