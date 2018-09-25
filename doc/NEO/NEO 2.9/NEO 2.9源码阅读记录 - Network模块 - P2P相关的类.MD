# NEO 2.9源码阅读记录 - Network模块 - RPC相关的类
### Message
* NEO网络节点之间通信时发送的消息体
* 继承自ISerializable
* 主要接口函数
  * `int Size()` 获得对象数据的字节数
  * `Serialize(BinaryWriter writer)` 序列化
  * `Deserialize(BinaryReader reader)` 反序列化
  * `Create(string command, byte[] payload)` 静态函数，创建一个消息体
* 主要成员
  * `string Command:`类型字符串
  * `uint Checksum:`校验和
  * `byte[] Payload:`数据区

### 各种Payload类
  * Payload意为有效负载，指网络通信中传输的数据包里，承载实际数据的区域。在这里可以理解为Message消息里的实际数据区
  * 可以将Message里的Payload转换成对应类型的Payload类，例如：
     ```
    Block block = message.Payload.AsSerializable<Block>();
    AddrPayload payload = message.Payload.AsSerializable<AddrPayload>();
    ``` 
    
### Connection

### Peer

### LocalNode

### RemoteNode

### ProtocolHandler

### TaskSession

### TaskManager

