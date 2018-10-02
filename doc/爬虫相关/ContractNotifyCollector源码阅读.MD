# ContractNotify解析合约类, analyze contract type
## 主要方法, main method
### initConfig 初始化读取的配置文件, initialize reading of the configuration file.
### startTask 开启线程, start thread
* 线程里面先是判断initSuccFlag，用来判断json文件有没有加载并处理完成。In thread first determine initSuccFlag, use to determine if the JSON file is loaded and processing is complete
* ping() while(true)循环的sleep方法，里面做了Sleep操作，时间由config配置而来, Loop sleep method, time configuration  came
* 获取远程高度remoteHeight与本地高度localHeight比较，本地高本次不做处理，远程高就继续向下, Get RemoteHeight and Compare with localHeight
* 开启for循环，取一个范围的数据，本地为开始，按配置表的配置取500长度，如果超出远程高度则取远程高度, Open a for loop , get a range of data , start at local, based on the configuation table's configuration get a 500 length, If remote height is exceeded, remote height is taken
* 先选出块高度值，然后同一高度的块进行解析，从rpc的getapplicationlog方法里获取txid，vmstate，notifications, first select block height value,  then parse the block with the same height, using the getapplicationlog method in rpc extract txid, vmstate, notifications
```
{
    "jsonrpc": "2.0",
    "id": 1,
    "result": {
        "txid": "0x0d03ad35eb8b0bb2e43e18896d22cd2a77fe54fc0b00794fb295bcf96257d0e3",
        "vmstate": "HALT, BREAK",
        "gas_consumed": "2.932",
        "stack": [],
        "notifications": [
            {
                "contract": "0xac116d4b8d4ca55e6b6d4ecce2192039b51cccc5",
                "state": {
                    "type": "Array",
                    "value": [
                        {
                            "type": "ByteArray",
                            "value": "7472616e73666572"
                        },
                        {
                            "type": "ByteArray",
                            "value": "45fc40a091bd0de5e5408e3dbf6b023919a6f7d9"
                        },
                        {
                            "type": "ByteArray",
                            "value": "96da23f79685e1611b99633f7a37bf07b542d42b"
                        },
                        {
                            "type": "ByteArray",
                            "value": "00345cd65804"
                        }
                    ]
                }
            }
        ]
    }
}
```
从中获取智能合约的hash contract和调用该合约使用的方法value的第一个值，经过转译, from the gained smart contract hash contract and smart contract to be transferred employs the first value of the method value , then convert
```
Encoding.Default.GetString(ThinNeo.Helper.HexString2Bytes("7472616e73666572"))
```
得到结果transfer，说明这次是个nep5合约转账。After getting the result transfer , describe this nep5 smart contract transfer
* 然后把索引信息存起来入库，结构, Then the index information is stored in the storage, structure
```
    JObject notifyInfo = new JObject();
    notifyInfo.Add("blockindex", blockindex);
    notifyInfo.Add("txid", txid);
    notifyInfo.Add("n", n);
    notifyInfo.Add("vmstate", vmstate);
    notifyInfo.Add("contractHash", contractHash);
    notifyInfo.Add(taskName, taskValue);
    notifyInfo.Add("state", notification["state"]);
```
