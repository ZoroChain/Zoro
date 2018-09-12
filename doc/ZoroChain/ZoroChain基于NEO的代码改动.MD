# ZoroChain基于NEO的代码改造
## 多链结构的解释
### 根链和应用链
* ZoroChain采用多链结构，分为根链和应用链
* 根链作为管理链，记录应用链的变更信息，以及其他的跨链交易信息
* 每一个应用会有对应的一条应用链，和该应用有关的交易信息都保存在各自的应用链上
### 链的识别方式
* ZoroChain里每个链都有一个唯一的字符串名字，该名字不能和其他的链重名
* 根据该字符串名字，会有一个对应的Hash值，用来快速索引该链
## 相关类的代码改动
#### IInventory
* 数据的清单（目录）
  * Block, Transaction, ConsensusPayload继承自IInventory
* 新增属性 `ChainHash`
  * 用来表示该清单的数据归属于哪一条链
  * 提供get方法，子类各自给出实现

#### BlockChain
* 新增静态函数 `GetBlockChain(hash)`
* 新增静态函数 `CreateBlockChain(name)`
* 新增静态函数 `InitRootChain()`
  * 为根链准备的函数，遍历所有区块的交易，从中获知目前所有的应用链信息
  * 在根链初始化时调用，用来创建当前关注的应用链的对象实例
* 新增静态变量 ``
* 先不考虑删除BlockChain的操作
* 修改静态变量 `Default`:
  * 目前`Default`代表NEO里唯一的区块链对象
  * 把`Default`改为`Root`，代表Zoro里的根链（管理链）
  * 需要修改现有代码里所有使用`BlockChain.Default`的地方

#### LevelDBBlockChain：
* LevelDBBlockChain还是继承自BlockChain
* 每一条链对应一个LevelDB数据存储
* 每一个LevelDBBlockChain对象会创建一个独立线程用来做数据存储
  
#### LocalNode
* 初始化，处理流程和线程结构维持不变


#### RemoteNode
* 初始化函数`StartProtocol()`的改动:
  * 版本信息里要加上关注的应用链的数量
    * 在VersionPayload类里加
  * 在互发版本信息后，要再互发各自关注的应用链列表，并记录在RemoteNode里
    * 需要增加一个应用链的版本信息类
    * 其中记录应用链的Hash，区块高度
    