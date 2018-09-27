## NEO源码阅读记录 - Nep5.5合约
#### Nep5
* Nep5是NEO中一种货币合约的约定标准，该标准定义了货币合约必须提供的一系列方法

* totalSupply 发币总量
```
if (method == "totalSupply") return TotalSupply();
```
```
[DisplayName("totalSupply")]
public static BigInteger TotalSupply()
{
    StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
    return contract.Get("totalSupply").AsBigInteger(); //0.1
}
```
* name 货币名称
```
if (method == "name") return Name();
```
```
[DisplayName("name")]
public static string Name() => "NEP5 GAS";
```
* symbol 货币单位
```
if (method == "symbol") return Symbol();
```
```
[DisplayName("symbol")]
public static string Symbol() => "CGAS";
```
* decimals 货币精度
```
if (method == "decimals") return Decimals();
```
精度数8是指小数点后8位
```
[DisplayName("decimals")]
public static byte Decimals() => 8;
```
* balanceOf 查询地址下的货币数量
  * who 需要传入地址
```
if (method == "balanceOf") return BalanceOf((byte[])args[0]);
```
```
[DisplayName("balanceOf")]
public static BigInteger BalanceOf(byte[] account)
{
    if (account.Length != 20)
        throw new InvalidOperationException("The parameter account SHOULD be 20-byte addresses.");
    StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
    return asset.Get(account).AsBigInteger(); //0.1
}
```
* transfer NEP5货币交易
  * from 从一个地址
  * to 到一个地址
  * value 转多少钱
```
if (method == "transfer") return Transfer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], callscript);
```
```
private static bool Transfer(byte[] from, byte[] to, BigInteger amount, byte[] callscript)
{
    //Check parameters
    if (from.Length != 20 || to.Length != 20)
        throw new InvalidOperationException("The parameters from and to SHOULD be 20-byte addresses.");
    if (amount <= 0)
        throw new InvalidOperationException("The parameter amount MUST be greater than 0.");
    if (!IsPayable(to))
        return false;
    if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger()) /*0.2*/
        return false;
    StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
    var fromAmount = asset.Get(from).AsBigInteger(); //0.1
    if (fromAmount < amount)
        return false;
    if (from == to)
        return true;

    //Reduce payer balances
    if (fromAmount == amount)
        asset.Delete(from); //0.1
    else
        asset.Put(from, fromAmount - amount); //1

    //Increase the payee balance
    var toAmount = asset.Get(to).AsBigInteger(); //0.1
    asset.Put(to, toAmount + amount); //1
    
    SetTxInfo(from, to, amount);
    Transferred(from, to, amount);
    return true;
}
```

#### Nep5.5
* 在Nep5基础上的扩展，增加了一些新的方法

* transferAPP
* getTxInfo 获取交易信息
```
if (method == "getTxInfo") return GetTxInfo((byte[])args[0]);
```
```
[DisplayName("getTxInfo")]
public static TransferInfo GetTxInfo(byte[] txId)
{
    if (txId.Length != 32)
        throw new InvalidOperationException("The parameter txId SHOULD be 32-byte transaction hash.");
    StorageMap txInfo = Storage.CurrentContext.CreateMap(nameof(txInfo));
    var result = txInfo.Get(txId); //0.1
    if (result.Length == 0) return null;
    return Helper.Deserialize(result) as TransferInfo;
}
```
* mintTokens 根据合约地址收到的gas转成cgas货币
```
if (method == "mintTokens") return MintTokens();
```
```
[DisplayName("mintTokens")]
public static bool MintTokens()
{
    var tx = ExecutionEngine.ScriptContainer as Transaction;

    //Person who sends a global asset, receives a NEP5 asset
    byte[] sender = null;
    var inputs = tx.GetReferences();
    foreach (var input in inputs)
    {
        if (input.AssetId.AsBigInteger() == AssetId.AsBigInteger())
            sender = sender ?? input.ScriptHash;
        //CGAS address as inputs is not allowed
        if (input.ScriptHash.AsBigInteger() == ExecutionEngine.ExecutingScriptHash.AsBigInteger())
            return false;
    }
    if (GetTxInfo(tx.Hash) != null)
        return false;

    //Amount of exchange
    var outputs = tx.GetOutputs();
    ulong value = 0;
    foreach (var output in outputs)
    {
        if (output.ScriptHash == ExecutionEngine.ExecutingScriptHash &&
            output.AssetId.AsBigInteger() == AssetId.AsBigInteger())
        {
            value += (ulong)output.Value;
        }
    }

    //Increase the total amount of contract assets
    StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
    var totalSupply = contract.Get("totalSupply").AsBigInteger(); //0.1
    totalSupply += value;
    contract.Put("totalSupply", totalSupply); //1

    //Issue NEP-5 asset
    StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
    var amount = asset.Get(sender).AsBigInteger(); //0.1
    asset.Put(sender, amount + value); //1

    SetTxInfo(null, sender, value);
    Transferred(null, sender, value);
    return true;
}
```
* refund 把合约资产兑换成公共资产cgas兑换成gas
  * from 从合约中哪个地址兑换
```
if (method == "refund") return Refund((byte[])args[0]);
```
```
[DisplayName("refund")]
public static bool Refund(byte[] from)
{
    if (from.Length != 20)
        throw new InvalidOperationException("The parameter from SHOULD be 20-byte addresses.");
    var tx = ExecutionEngine.ScriptContainer as Transaction;
    //output[0] Is the asset that the user want to refund
    var preRefund = tx.GetOutputs()[0];
    //refund assets wrong, failed
    if (preRefund.AssetId.AsBigInteger() != AssetId.AsBigInteger()) return false;

    //Not to itself, failed
    if (preRefund.ScriptHash.AsBigInteger() != ExecutionEngine.ExecutingScriptHash.AsBigInteger()) return false;

    //double refund
    StorageMap refund = Storage.CurrentContext.CreateMap(nameof(refund));
    if (refund.Get(tx.Hash).Length > 0) return false; //0.1

    if (!Runtime.CheckWitness(from)) return false; //0.2

    //Reduce the balance of the refund person
    StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
    var fromAmount = asset.Get(from).AsBigInteger(); //0.1
    var preRefundValue = preRefund.Value;
    if (fromAmount < preRefundValue)
        return false;
    else if (fromAmount == preRefundValue)
        asset.Delete(from); //0.1
    else
        asset.Put(from, fromAmount - preRefundValue); //1
    refund.Put(tx.Hash, from); //1

    //Change the totalSupply
    StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
    var totalSupply = contract.Get("totalSupply").AsBigInteger(); //0.1
    totalSupply -= preRefundValue;
    contract.Put("totalSupply", totalSupply); //1

    SetTxInfo(from, null, preRefundValue);
    Transferred(from, null, preRefundValue);
    Refunded(tx.Hash, from);
    return true;
}
```
* getRefundTarget 获取兑换cgas兑换成gas的交易信息
```
if (method == "getRefundTarget") return GetRefundTarget((byte[])args[0]);
```
```
[DisplayName("getRefundTarget")]
public static byte[] GetRefundTarget(byte[] txId)
{
    if (txId.Length != 32)
        throw new InvalidOperationException("The parameter txId SHOULD be 32-byte transaction hash.");
    StorageMap refund = Storage.CurrentContext.CreateMap(nameof(refund));
    return refund.Get(txId); //0.1
}
```

#### 额外方法
* deploy 发币方法，cgas没有
* setTxInfo 添加交易信息
```
private static void SetTxInfo(byte[] from, byte[] to, BigInteger value)
{
    var txid = (ExecutionEngine.ScriptContainer as Transaction).Hash;
    TransferInfo info = new TransferInfo
    {
        from = from,
        to = to,
        value = value
    };
    StorageMap txInfo = Storage.CurrentContext.CreateMap(nameof(txInfo));
    txInfo.Put(txid, Helper.Serialize(info)); //1
}
```
* Transferred 往notifactions添加log
```
[DisplayName("transfer")]
public static event deleTransfer Transferred;
public delegate void deleTransfer(byte[] from, byte[] to, BigInteger value);
```
* Transferred 往notifactions添加log
```
[DisplayName("refund")]
public static event deleRefundTarget Refunded;
public delegate void deleRefundTarget(byte[] txId, byte[] who);
```
* supportedStandards 支持标准
```
if (method == "supportedStandards") return SupportedStandards();
```
```
[DisplayName("supportedStandards")]
public static string SupportedStandards() => "{\"NEP-5\", \"NEP-7\", \"NEP-10\"}";
```
* CGAS在Verification状态下的代码，也就是从合约地址把钱取出来需要的代码，跟转入代码相关联
```
if (Runtime.Trigger == TriggerType.Verification)
{
    var tx = ExecutionEngine.ScriptContainer as Transaction;
    var inputs = tx.GetInputs();
    var outputs = tx.GetOutputs();
    //Check if the input has been marked
    foreach (var input in inputs)
    {
        if (input.PrevIndex == 0)//If UTXO n is 0, it is possible to be a marker UTXO
        {
            StorageMap refund = Storage.CurrentContext.CreateMap(nameof(refund));
            var refundMan = refund.Get(input.PrevHash); //0.1
            //If the input that is marked for refund
            if (refundMan.Length > 0)
            {
                //Only one input and one output is allowed in refund
                if (inputs.Length != 1 || outputs.Length != 1)
                    return false;
                return outputs[0].ScriptHash.AsBigInteger() == refundMan.AsBigInteger();
            }
        }
    }
    var currentHash = ExecutionEngine.ExecutingScriptHash;
    //If all the inputs are not marked for refund
    BigInteger inputAmount = 0;
    foreach (var refe in tx.GetReferences())
    {
        if (refe.AssetId.AsBigInteger() != AssetId.AsBigInteger())
            return false;//Not allowed to operate assets other than GAS

        if (refe.ScriptHash.AsBigInteger() == currentHash.AsBigInteger())
            inputAmount += refe.Value;
    }
    //Check that there is no money left this contract
    BigInteger outputAmount = 0;
    foreach (var output in outputs)
    {
        if (output.ScriptHash.AsBigInteger() == currentHash.AsBigInteger())
            outputAmount += output.Value;
    }
    return outputAmount == inputAmount;
}
```