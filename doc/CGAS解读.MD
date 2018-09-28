# CGAS 解读
## Main方法
* Runtime.Trigger == TriggerType.Verification
  * 获取此次ContractTransaction的输入和输出 
  ```
    var tx = ExecutionEngine.ScriptContainer as Transaction;
    var inputs = tx.GetInputs();
    var outputs = tx.GetOutputs();
  ```
  * 遍历输入，对每个输入判断PrevIndex是否为0，如果为0就有可能是个记录中的UTXO，从refund记录中根据此输入的PrevHash查询是否在Storage中存储有值，如果长度大于0再判断输入和输出的长度是否为1，只要有一个不为1就返回false，都为1就判断唯一的输出地址是不是Storage查询出的地址（往合约转过账的地址）
  ```
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
  ```
  * 使用tx.GetReferences()方法从本次交易的输入中PrevHash和PrevIndex获取原有的输出，
  然后遍历判断每个输出资产是否是GAS，是的话就判断原有输出地址的scripthash与ExecutionEngine.ExecutingScriptHash（合约地址的ScriptHash）是否相同,如果相同累计输入货币数量
  ```
    BigInteger inputAmount = 0;
    foreach (var refe in tx.GetReferences())
    {
        if (refe.AssetId.AsBigInteger() != AssetId.AsBigInteger())
            return false;//Not allowed to operate assets other than GAS

        if (refe.ScriptHash.AsBigInteger() == currentHash.AsBigInteger())
            inputAmount += refe.Value;
    }
  ```
  * 遍历当前输出，统计当前输出地址与合约地址相同的情况下货币数量
  ```
    BigInteger outputAmount = 0;
    foreach (var output in outputs)
    {
        if (output.ScriptHash.AsBigInteger() == currentHash.AsBigInteger())
            outputAmount += output.Value;
    }
    return outputAmount == inputAmount;
  ```
* Runtime.Trigger == TriggerType.Application
  * 用balanceOf查询合约里指定地址有多少CGAS
  ```
    public static BigInteger BalanceOf(byte[] account)
    {
        if (account.Length != 20)
            throw new InvalidOperationException("The parameter account SHOULD be 20-byte addresses.");
        StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
        return asset.Get(account).AsBigInteger(); //0.1
    }
  ```
  * 获取nep5精度
  ```
    public static byte Decimals() => 8;
  ```
  * 从交易ID获取退回标记的地址
  ```
    public static byte[] GetRefundTarget(byte[] txId)
    {
        if (txId.Length != 32)
            throw new InvalidOperationException("The parameter txId SHOULD be 32-byte transaction hash.");
        StorageMap refund = Storage.CurrentContext.CreateMap(nameof(refund));
        return refund.Get(txId); //0.1
    }
  ```
  * 从交易ID获取交易信息
  ```
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
  * 铸造Tokens货币
    * 从本次交易中的输入获取原有的输出信息，遍历这些输出判断每个输出资产ID是不是Gas，如果是gas，把此地址保存。如果输入地址与合约地址相同返回false
    ```
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
    ```
    * 用此交易ID查询交易信息，如果存在返回false
    ```
       if (GetTxInfo(tx.Hash) != null) return false; 
    ```
    * 获取此交易的输出，遍历所有输出检测每个输出的地址是不是合约地址，并且资产是不是GAS，如果成立统计转账金额
    ```
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
    ```
    * 修改资产总量，为输入地址存入相应NEP5资产，保存交易信息，上传notification
    ```
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
    ```
