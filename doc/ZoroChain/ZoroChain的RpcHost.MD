#ZoroChain的RpcHost
### RpcHost的作用
* 在NEO-Cli里集成了HttpService的RpcServer，可以处理用Http请求发起的Rpc指令
* 但如果并发的Http请求数量增大，CLR线程池会被占满，并导致Akka的异步任务无法被执行，致使节点的消息通信被阻断
* 为了避免这样的问题，我们使用一个独立的HttpService进程来处理Rpc指令
* RpcHost就是这样提供HttpService的一个独立进程，将收到的Rpc指令发送给Cli节点处理，并将结果返回给Http请求者

### RpcHost的程序结构和工作流程
* RpcHost由两个部分组成，一个是HttpService，一个是基于TCP连接的Rpc指令中转器
* RpcHost启动时，会根据配置文件，连接到一个开启了RpcAgent服务的Zoro-Cli节点
* 当收到Http请求时，通过TCP连接，将Rpc指令发送给RpcAgent进行处理，并暂时挂起该Http请求
* 当收到RpcAgent发回的处理结果时，再将结果发送给Http请求者

### Zoro-Cli节点的RpcAgent服务
* RpcAgent通过插件的形式在Cli进程中运行
* RpcAgent启动时，会根据配置文件，在特定端口开启TCP连接侦听，等待RpcHost的连接
* 当收到RpcHost发来的Rpc指令时，采用和原先RpcServer中相同的方式进行处理
* 处理完成时，将结果发回给对应的RpcHost，后者再将结果发回到Http请求者

### 对比RpcServer和RpcHost两种方式的测试结果
* 使用测试程序，向节点发送一定数量的并发交易进行测试
* 使用RpcServer，在收到一定数量的并发交易时，会很快占满CLR线程池
  * 此时可以观察到该进程的线程数量随时间不断增长
  * 同时已经向Blockchain上投递的消息始终没有被处理执行
  * 直到所有的并发交易要么超时失败，或者被接收后，有空闲线程返回到线程池
  * Blockchain开始恢复运行，逐步处理堆积的消息
  * 此过程的时间和并发交易数量成正比，在此期间节点无法同步区块，所有交易积累在本地Mempool里
* 使用RpcHost，在收到一定数量的并发交易时，也会很快占满CLR线程池
  * RpcHost和RpcAgent之间的网络通信采用异步函数机制，不依赖CLR线程池，所以可以及时的将收到的消息转发给RpcAgent
  * RpcAgent和RpcHost独立在不同的进程里，不会受到影响，可以正常的处理RpcHost发来的指令，并将结果返回给RpcHost
  * 在此过程中，受到RpcHost的HttpService处理能力上限的影响，超出上限后的交易会因为无法被HttpService接收而失败
  * 但Cli节点可以保持正常的工作，不受影响