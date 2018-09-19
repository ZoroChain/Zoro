## NEPs
NEPs: NEO Enhancement Proposals,即 NEO 加强/改进提议 ，描述的是 NEO 平台的标准，包括核心协议规范，客户端 API 和合约标准。
## Nep5
### 概述
nep5 提案描述了neo 区块链的 token 标准，它为 token 类的智能合约提供了系统的通用交互机制，定义了这种机制和每种特性，并提供了开发模板和示例。
## 提出
随着 neo 区块链生态的发展，智能合约的部署和调用变得越来越重要，如果没有标准的交互方法，系统就需要为每个智能合约维护一套单独的 api，无论合约间有没有相似性。
token 类合约的操作机制其实基本都是相同的，因此需要这样一套标准。这些与 token 交互的标准方案使整个生态系统免于维护每个使用 token 的智能合约的 api。
## 规范
在下面的方法中，我们提供了在合约中函数的定义方式及参数调用。

* totalSupply
```
public static BigInteger totalSupply（）
```
返回 token 总量
* name
```
public static string name()
```
返回 token 名称 <br>
每次调用时此方法必须返回相同的值
* symbol
```
public static string symbol()
```
返回此合约中管理的 token 的简称。3-8 字符、限制为大写英文字母<br>
每次调用时此方法必须返回相同的值
* decimals
```
public static byte decimals()
```
返回 token 使用的小数位数<br>
每次调用时此方法必须返回相同的值
* balanceOf
```
public static BigInteger balanceOf(byte[] account)
```
返回 token 余额<br>
参数 account 应该是一个 20 字节的地址。如果没有，方法应该 throw 一个异常。<br>
如果 account 是未使用的地址，则此方法必须返回 0。
* transfer
```
public static bool transfer(byte[] from, byte[] to, BigInteger amount)
```
将 amount 数量的 token 从 from 账户转到 to 账户<br>
参数 from 和 to 应该是 20 字节的地址。如果没有，方法应该 throw 一个异常。<br>
参数 amount 必须大于或等于 0。如果没有，方法应该 throw 一个异常。<br>
如果 from 帐户没有足够的 token 可用，则该函数必须返回 false。<br>
如果该方法成功，它必须触发 transfer 交易，并且必须返回 true，即使 amount 是 0，或 from 与 to 有相同的地址。<br>
函数应该检查 from 地址是否等于合约调用者 hash。如果是这样，应该处理转账; 如果没有，该函数应该使用 SYSCALL Neo.Runtime.CheckWitness 来验证交易。<br>
如果 to 地址是已部署的合约地址，则该函数应该检查该合约的 payable 标志以决定是否应该将 token 转移到该合约地址。<br>
如果未处理转账，则函数应该返回 false。<br>
## 事件
* transfer
```
public static event transfer(byte[] from, byte[] to, BigInteger amount)
```
转移 token 时必须触发，包括零值转移。<br>
创建新 token 的合约必须触发一个 transfer 交易，其 from 地址设置为 null 时创建 token。<br>
燃烧 token 的合约必须触发一个 transfer 事件，其 to 地址设置为 null 时燃烧 token。
## 示例
* Woolong：<https://github.com/lllwvlvwlll/Woolong>
* ICO模板：<https://github.com/neo-project/examples/tree/master/ICO_Template>

