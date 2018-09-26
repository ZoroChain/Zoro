## NEO源码阅读记录 - Nep5.5合约
#### Nep5
* Nep5是NEO中一种货币合约的约定标准，该标准定义了货币合约必须提供的一系列方法

* totalSupply 发币总量
* name 货币名称
* symbol 货币单位
* decimals 货币精度
* balanceOf 查询地址下的货币数量
  * who 需要传入地址
* transfer NEP5货币交易
  * from 从一个地址
  * to 到一个地址
  * value 转多少钱

#### Nep5.5
* 在Nep5基础上的扩展，增加了一些新的方法

* transferAPP
* getTxInfo 获取交易信息
* mintTokens 根据合约地址收到的gas转成cgas货币
* refund 把合约资产兑换成公共资产cgas兑换成gas
* getRefundTarget 获取兑换cgas兑换成gas的交易信息

#### 额外方法
* deploy 发币方法，cgas没有
