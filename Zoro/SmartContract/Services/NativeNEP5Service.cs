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
            if (amount == Fixed8.Zero || amount < -Fixed8.Satoshi) return false;

            // 货币精度
            byte precision = (byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            if (precision > 8) return false;
            if (amount != -Fixed8.Satoshi && amount.GetData() % (long)Math.Pow(10, 8 - precision) != 0)
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

            // 用ScriptHash作为assetId
            UInt160 assetId = engine.CurrentContext.Script.ToScriptHash();

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

        public static long GetPrice(ExecutionEngine engine)
        {
            long price = 0;
            string method = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Peek().GetByteArray());
            switch (method)
            {
                case "Transfer":
                case "Transfer_App":
                    price = 1000;
                    break;
                case "BalanceOf":
                case "GetTransferLog":
                    price = 100;
                    break;
                case "Name":
                case "Symbol":
                case "TotalSupply":
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
                case "Transfer_App":
                    return API_Transfer_App(engine, state);
                case "GetTransferLog":
                    return API_GetTransferLog(engine, state);
                case "Deploy":
                    return API_Deploy(engine, state);
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

        private bool API_Transfer_App(ExecutionEngine engine, NativeNEP5State state)
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

            byte[] transferLog = NativeAPI.GetTransferLog(Snapshot, state.AssetId, hash);

            if (transferLog == null)
                return false;

            engine.CurrentContext.EvaluationStack.Push(transferLog);
            return true;
        }

        private bool API_Deploy(ExecutionEngine engine, NativeNEP5State state)
        {
            // 创世块不做检查
            if (Snapshot.PersistingBlock.Index != 0 && !Service.CheckWitness(engine, state.Admin))
                return false;

            byte[] total_supply = NativeAPI.StorageGet(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply"));
            if (total_supply.Length != 0)
                return false;

            var keyAdmin = new byte[] { 0x11 }.Concat(state.Admin.ToArray());

            NativeAPI.StoragePut(Snapshot, state.AssetId, keyAdmin.ToArray(), state.TotalSupply);
            NativeAPI.StoragePut(Snapshot, state.AssetId, Encoding.ASCII.GetBytes("totalSupply"), state.TotalSupply);

            Service.AddTransferNotification(engine, state.AssetId, new UInt160(), state.Admin, state.TotalSupply);
            return true;
        }
    }
}
