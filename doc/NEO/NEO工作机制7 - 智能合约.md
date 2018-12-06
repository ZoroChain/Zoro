# NEO 智能合约

NEO 中智能合约分为应用合约和鉴权合约
* 应用合约是用户通过发送交易来触发合约执行，只有交易完成后合约才被调用，所以不论合约执行结果如何，交易都会发生。
* 鉴权合约是一个合约账户，用户使用账户中的一笔资产时会触发合约。鉴权合约的触发是UTXO模型的鉴证过程，是在交易被写入区块之前执行。如果合约返回false或者发生异常，则交易不会被写入区块。
* 合约调用：就是使用ScriptBuiler拼接合约调用，并记录在InvocationTransaction交易的script里面

## NEO 智能合约开发流程
### 必须工具：
`安装配置过程：`<http://docs.neo.org/zh-cn/sc/quickstart/getting-started-csharp.html>
* NeoContractPlugin 插件
* 智能合约编译器 用来将c#代码 和 java代码编译为智能合约指令，neo-compiler <https://github.com/neo-project/neo-compiler>
### 编写合约
#### 智能合约的Hello World
* VS中创建NEOContract项目进行开发，NeoVM的数据类型说明 <http://docs.neo.org/zh-cn/sc/quickstart/limitation.html>
#### Nep5合约案例
* <https://github.com/NewEconoLab/neo-ns.git>目录下dapp_nnc4.0
### 部署合约
* C#或Java合约在编译时会产生中间语言 MSIL， neo-compiler 编译器通过 Mono.Ceill 将中间语言编译成 NeoVM 的字节码，生成.avm文件；
* 发布合约的时候，去读取.avm文件(合约脚本)并记录合约脚本hash，然后构造一笔InvocationTransaction类型的交易，合约脚本附加在该交易的数据中，不同的合约所需费用也不同，具体见 <http://docs.neo.org/zh-cn/sc/systemfees.html>，该交易其实就是给一个空地址发送合约费用，交易完成后，该合约会随着交易的打包被记录在区块链上；
* 合约脚本hash是后续测试和调用合约的依据。
* 部署合约拼接代码(下面是部署合约代码)：
```
    using (ThinNeo.ScriptBuilder sb = new ThinNeo.ScriptBuilder()) {
        var ss = need_storage | need_nep4 | need_canCharge;
        sb.EmitPushString(description);
        sb.EmitPushString(email);
        sb.EmitPushString(auther);
        sb.EmitPushString(version);
        sb.EmitPushString(name);
        sb.EmitPushNumber(ss);
        sb.EmitPushBytes(return_type);
        sb.EmitPushBytes(parameter__list);
        sb.EmitPushBytes(script);
        sb.EmitSysCall("Neo.Contract.Create");

        string scriptPublish = ThinNeo.Helper.Bytes2HexString(sb.ToArray());

        byte[] postdata;
        var url = Helper.MakeRpcUrlPost(api, "invokescript", out postdata, new MyJson.JsonNode_ValueString(scriptPublish));
        var result = await Helper.HttpPost(url, postdata);
    }
```
* 下面是把合约插入交易里的代码：
```
    ThinNeo.InvokeTransData extdata = new ThinNeo.InvokeTransData();
    extdata.script = sb.ToArray();

    extdata.gas = Math.Ceiling(gas_consumed - 10);

    ThinNeo.Transaction tran = makeTran(dir, null, new ThinNeo.Hash256(id_GAS), extdata.gas);
    tran.version = 1;
    tran.extdata = extdata;
```
* 参考案例<https://github.com/wowoyinwei/examples-transaction.git>
### 调用合约
* 调用合约时需要：合约脚本hash、合约中的方法名称、方法需要传入的参数；
* 调用合约也是构造一笔InvocationTransaction类型的交易，方法名和参数附加在交易数据中，交易发出之后NeoVM会运行合约，返回运行结果。
* 调用合约拼接代码（下面是nep5发币deploy方法调用）：
```
    using (ThinNeo.ScriptBuilder sb = new ThinNeo.ScriptBuilder()) {
        MyJson.JsonNode_Array array = new MyJson.JsonNode_Array();
        array.AddArrayValue("(int)1");
        sb.EmitParamJson(array);
        sb.EmitPushString("deploy");
        sb.EmitAppCall(new Hash160("0xccd651a5e7d9f4dc698353970df7b7180139cbbe"));

        string scriptPublish = ThinNeo.Helper.Bytes2HexString(sb.ToArray());

        byte[] postdata;
        var url = Helper.MakeRpcUrlPost(api, "invokescript", out postdata, new MyJson.JsonNode_ValueString(scriptPublish));
        var result = await Helper.HttpPost(url, postdata);
    }
```
### 调试合约
`智能合约调试需要使用nel提供的cli和gui,官方neo-compiler提供的neon替换为nel的debugneon`
* neo-cli-nel <https://github.com/NewEconoLab/neo-cli-nel.git>
* neo-gui-nel <https://github.com/NewEconoLab/neo-gui-nel.git>
* neondebug <https://github.com/NewEconoLab/neondebug.git>
  * 
* 上面neo-cli-nel和neo-gui-nel默认是在testnet上操作，配置文件config解读:
  * Chain:同步区块数据的存储地址
  * ApplicationLogs：notify文件存放地址
  * Fulllogs：智能合约调试需要的文件地址
  * fullLogOnlyLocal：默认false，只存储在本地操作的智能合约交易的调试文件
* 调试工具使用neondebuggui打开存储的调试文件

### 合约更新问题
* 合约更新更新的只是发布人的基本信息，对合约内部没有任何修改，所以合约更新不会对合约的hash有影响，合约发布基础费用100gas
### 智能合约部署问题
* 当合约需要被appcall调用时，当智能合约要作为一个类库使用时，才需要被部署。
  * 当一个智能合约有可变的传入参数，此时它必须作为一个类库，由调用者（Invocation 交易）或者其它的智能合约提供参数。
  * 当一个智能合约使用存储区（Storage）与动态调用（NEP4）时，必须作为一个类库。
  * 当一个智能合约实现了 NEP-5（合约资产）时，需要将该合约部署到区块链上。
* 仅由合约账户鉴权触发的合约，如锁仓合约、多方签名合约，不会被其它合约调用，所以无需部署。 “Return 1+1” 这样的合约，因为没有任何需要输入的参数，也无需部署。
### 合约发布时的参数
* parameter__list 合约入参byteArray（对合约发布没有任何影响的参数）
```ThinNeo.Helper.HexString2Bytes("0710")```
* return_type 合约出参byteArray（对合约发布没有任何影响的参数）
```ThinNeo.Helper.HexString2Bytes("05")```
* need int类型数据，参数赋值方式need_storage|need_nep4|need_canCharge
  * need_storage 是否需要storage存储，0不需要，1需要，发布时多支付400gas
  * need_nep4 是否需要nep4动态调用, 0不需要，2需要，发布时多支付500gas
  * need_canCharge 是否收钱，0不需要，4需要
* name 合约发布人的姓名
* version 发布合约的版本号
* auther 被发布合约的作者
* email 发布合约的邮箱
* description 发布合约的描述
### 智能合约的调用
* 调用合约的方式
```
[Appcall("0x1a328cdd53c7f1710b4006304e8c75236a9b18523f037cdf069a96f0d7f01379")]//合约hash
public static extern int AnotherContract(string arg);

public static void Main()
{
    AnotherContract("Hello");    
}
```
### 合约发布失败条件
* 发布失败的条件通常是gas费用不足
* 使用代码发布的时候没有按照invokescript计算出来的gas发布合约
