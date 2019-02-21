using Zoro.IO;
using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Cryptography.ECC;
using Zoro.Network.P2P.Payloads;
using Zoro.SmartContract.NativeNEP5;
using System;
using System.Linq;
using System.Text;
using System.Numerics;
using Neo.VM;

namespace Zoro.SmartContract.Services
{
    class NativeNEP5Service
    {
        public static readonly uint SysCall_MethodHash = "Zoro.NativeNEP5.Call".ToInteropMethodHash();

        protected readonly ZoroService Service;
        protected readonly TriggerType Trigger;
        protected readonly Snapshot Snapshot;

        public NativeNEP5Service(ZoroService service, TriggerType trigger, Snapshot snapshot)
        {
            Service = service;
            Trigger = trigger;
            Snapshot = snapshot;
        }

        public bool Create(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            // 全名
            if (engine.CurrentContext.EvaluationStack.Peek().GetByteArray().Length > 1024) return false;
            string name = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            // 简写名
            if (engine.CurrentContext.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string symbol = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            // 货币总量
            Fixed8 amount = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());
            if (amount < Fixed8.Zero) return false;

            // 货币精度
            byte precision = (byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            if (precision > 8) return false;
            if (amount.GetData() % (long)Math.Pow(10, precision) != 0)
                return false;

            // 发行人
            ECPoint owner = ECPoint.DecodePoint(engine.CurrentContext.EvaluationStack.Pop().GetByteArray(), ECCurve.Secp256r1);

            // 创世块不做检查
            if (Snapshot.PersistingBlock.Index != 0)
            {
                // 检查发行人的签名
                if (owner.IsInfinity) return false;
                if (!Service.CheckWitness(engine, owner))
                    return false;
            }

            // 管理员
            UInt160 admin = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            // 创世块不做检查
            if (Snapshot.PersistingBlock.Index != 0)
            {
                // 检查管理员的Hash
                if (admin.Equals(UInt160.Zero))
                    return false;
            }

            UInt160 assetId;
            if (Snapshot.PersistingBlock.Index == 0)
            {
                // 只有在创世块里可以自定义assetId
                assetId = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            }
            else
            {
                // 用ScriptHash作为assetId
                assetId = engine.CurrentContext.Script.ToScriptHash();
            }            

            NativeNEP5State state = Snapshot.NativeNEP5s.TryGet(assetId);
            if (state == null)
            {
                state = new NativeNEP5State
                {
                    AssetId = assetId,
                    Name = name,
                    Symbol = symbol,
                    TotalSupply = amount,
                    Decimals = precision,
                    Owner = owner,
                    Admin = admin,
                    BlockIndex = Snapshot.Height + 1,
                    IsFrozen = false
                };

                // 保存到数据库
                Snapshot.NativeNEP5s.Add(assetId, state);
            }

            // 设置脚本的返回值
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));
            return true;
        }

        public bool GetTransferLog(ExecutionEngine engine)
        {
            UInt160 assetId = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5State state = Snapshot.NativeNEP5s.TryGet(assetId);
            if (state == null) return false;

            return API_GetTransferLog(engine, state);
        }

        public static long GetPrice(ExecutionEngine engine)
        {
            long price = 0;
            string method = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Peek().GetByteArray());
            switch (method)
            {
                case "Deploy":
                case "Transfer":
                case "TransferApp":
                case "TransferFrom":
                    price = 1000;
                    break;
                case "MintToken":
                case "BalanceOf":
                case "TotalSupply":
                case "GetTransferLog":
                case "Allowance":
                case "Approve":
                    price = 100;
                    break;
                case "Name":
                case "Symbol":
                case "Decimals":
                    price = 1;
                    break;
            }

            return price;
        }

        public bool Call(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            if (engine.CurrentContext.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string method = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            // 兼容用json数组来打包的参数
            StackItem stackItem = engine.CurrentContext.EvaluationStack.Peek();
            if (stackItem is Neo.VM.Types.Array array)
            {
                engine.CurrentContext.EvaluationStack.Pop();

                int count = array.Count();
                for (int i = count - 1; i >= 0; i--)
                {
                    engine.CurrentContext.EvaluationStack.Push(array[i]);
                }
            }

            UInt160 assetId = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            NativeNEP5State state = Snapshot.NativeNEP5s.TryGet(assetId);
            if (state == null) return false;

            switch (method)
            {
                case "Name":
                    return API_Name(engine, state);
                case "Symbol":
                    return API_Symbol(engine, state);
                case "TotalSupply":
                    return API_TotalSupply(engine, state);
                case "Decimals":
                    return API_Decimals(engine, state);
                case "BalanceOf":
                    return API_BalanceOf(engine, state);
                case "Transfer":
                    return API_Transfer(engine, state);
                case "TransferApp":
                    return API_TransferApp(engine, state);
                case "Approve":
                    return API_Approve(engine, state);
                case "TransferFrom":
                    return API_TransferFrom(engine, state);
                case "Allowance":
                    return API_Allowance(engine, state);
                case "GetTransferLog":
                    return API_GetTransferLog(engine, state);
                case "Deploy":
                    return API_Deploy(engine, state);
                case "MintToken":
                    return API_MintToken(engine, state);
            }

            return false;
        }

        private bool API_Name(ExecutionEngine engine, NativeNEP5State state)
        {
            engine.CurrentContext.EvaluationStack.Push(state.Name);
            return true;
        }

        private bool API_Symbol(ExecutionEngine engine, NativeNEP5State state)
        {
            engine.CurrentContext.EvaluationStack.Push(state.Symbol);
            return true;
        }

        private bool API_TotalSupply(ExecutionEngine engine, NativeNEP5State state)
        {
            engine.CurrentContext.EvaluationStack.Push(NativeAPI.StorageGet(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply")).AsBigInteger());
            return true;
        }

        private bool API_Decimals(ExecutionEngine engine, NativeNEP5State state)
        {
            engine.CurrentContext.EvaluationStack.Push((int)state.Decimals);
            return true;
        }

        private bool API_BalanceOf(ExecutionEngine engine, NativeNEP5State state)
        {
            UInt160 address = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            var key = new byte[] { 0x11 }.Concat(address.ToArray()).ToArray();
            engine.CurrentContext.EvaluationStack.Push(NativeAPI.StorageGet(Snapshot, state.AssetId, key).AsBigInteger());
            return true;
        }

        private bool API_Allowance(ExecutionEngine engine, NativeNEP5State state)
        {
            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            var keyApprove = from.ToArray().Concat(to.ToArray()).ToArray();
            engine.CurrentContext.EvaluationStack.Push(NativeAPI.StorageGet(Snapshot, state.AssetId, keyApprove).AsBigInteger());
            return true;
        }

        private bool API_Transfer(ExecutionEngine engine, NativeNEP5State state)
        {
            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (!Service.CheckWitness(engine, from))
                return false;

            //禁止跳板调用、入口脚本不是当前执行脚本说明是跳板调用
            if (engine.EntryContext.ScriptHash != engine.CurrentContext.ScriptHash)
                return false;

            bool result = NativeAPI.Transfer(Snapshot, state.AssetId, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                {
                    NativeAPI.SaveTransferLog(Snapshot, state.AssetId, tx.Hash, from, to, value);
                }

                Service.AddTransferNotification(engine, state.AssetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private bool API_TransferFrom(ExecutionEngine engine, NativeNEP5State state)
        {
            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());
                        
            bool result = NativeAPI.TransferFrom(Snapshot, state.AssetId, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                {
                    NativeAPI.SaveTransferLog(Snapshot, state.AssetId, tx.Hash, from, to, value);
                }

                Service.AddTransferNotification(engine, state.AssetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private bool API_Approve(ExecutionEngine engine, NativeNEP5State state)
        {
            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (!Service.CheckWitness(engine, from))
                return false;

            if (engine.EntryContext.ScriptHash != engine.CurrentContext.ScriptHash)
                return false;

            bool result = NativeAPI.Approve(Snapshot, state.AssetId, from, to, value);

            if (result)
            {
                Service.AddApproveNotification(engine, state.AssetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private bool API_TransferApp(ExecutionEngine engine, NativeNEP5State state)
        {
            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (from != new UInt160(engine.CurrentContext.ScriptHash))
                return false;

            bool result = NativeAPI.Transfer(Snapshot, state.AssetId, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                {
                    NativeAPI.SaveTransferLog(Snapshot, state.AssetId, tx.Hash, from, to, value);
                }

                Service.AddTransferNotification(engine, state.AssetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private bool API_GetTransferLog(ExecutionEngine engine, NativeNEP5State state)
        {
            byte[] hash = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            TransferLog transferLog = NativeAPI.GetTransferLog(Snapshot, state.AssetId, hash);

            if (transferLog == null)
                return false;

            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(transferLog));
            return true;
        }

        private bool API_Deploy(ExecutionEngine engine, NativeNEP5State state)
        {
            // 创世块里的全局资产不做检查
            if (Snapshot.PersistingBlock.Index != 0)
            {
                // 检查管理员的签名
                if (state.Admin.Equals(UInt160.Zero) || !Service.CheckWitness(engine, state.Admin))
                    return false;
            }

            // 全局资产只在根链上做初始分配，暂时注释掉，等跨链兑换完成后再打开检查
            //if (Snapshot.PersistingBlock.Index == 0 && !Snapshot.Blockchain.ChainHash.Equals(UInt160.Zero))
            //    return false;

            byte[] total_supply = NativeAPI.StorageGet(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply"));
            if (total_supply.Length != 0)
                return false;

            // 创世块里的全局资产，使用创世块的共识多签地址作为初始地址
            UInt160 scriptHash = Snapshot.PersistingBlock.Index == 0 ? Snapshot.PersistingBlock.NextConsensus : state.Admin;
            var keyAdmin = new byte[] { 0x11 }.Concat(scriptHash.ToArray());

            NativeAPI.StoragePut(Snapshot, state.AssetId, keyAdmin.ToArray(), state.TotalSupply);
            NativeAPI.StoragePut(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply"), state.TotalSupply);

            Service.AddTransferNotification(engine, state.AssetId, UInt160.Zero, state.Admin, state.TotalSupply);
            return true;
        }

        private bool API_MintToken(ExecutionEngine engine, NativeNEP5State state)
        {
            // 只有创建时总量设为0的货币才能调用发币接口
            if (state.TotalSupply.GetData() != 0)
                return false;

            // 检查管理员的签名
            if (state.Admin.Equals(UInt160.Zero) || !Service.CheckWitness(engine, state.Admin))
                return false;

            UInt160 address = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 amount = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            // 变更货币总量
            BigInteger totalSupply = NativeAPI.StorageGet(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply")).AsBigInteger();
            NativeAPI.StoragePut(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply"), totalSupply + amount.GetData());

            // 发放货币
            NativeAPI.AddBalance(Snapshot, state.AssetId, address, amount);

            if (engine.ScriptContainer is Transaction tx)
            {
                NativeAPI.SaveTransferLog(Snapshot, state.AssetId, tx.Hash, UInt160.Zero, address, amount);
            }

            Service.AddTransferNotification(engine, state.AssetId, UInt160.Zero, address, amount);
            return true;
        }
    }
}
