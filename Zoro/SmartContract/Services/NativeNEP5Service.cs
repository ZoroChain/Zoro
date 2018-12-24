using Zoro.IO;
using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Cryptography.ECC;
using Zoro.Network.P2P.Payloads;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using Neo.VM;

namespace Zoro.SmartContract.Services
{
    class NativeNEP5Service
    {
        public static readonly uint SysCall_MethodHash = "Zoro.NativeNEP5.Call".ToInteropMethodHash();

        internal class TransferLog : ISerializable
        {
            public UInt160 From;
            public UInt160 To;
            public Fixed8 Value;

            public virtual int Size => From.Size + To.Size + Value.Size;

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(From);
                writer.Write(To);
                writer.Write(Value);
            }

            public void Deserialize(BinaryReader reader)
            {
                From = reader.ReadSerializable<UInt160>();
                To = reader.ReadSerializable<UInt160>();
                Value = reader.ReadSerializable<Fixed8>();
            }
        }

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
            if (owner.IsInfinity) return false;
            if (!Service.CheckWitness(engine, owner))
                return false;

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
            string method = Encoding.ASCII.GetString(engine.CurrentContext.EvaluationStack.Peek(1).GetByteArray());
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
            }

            return false;
        }

        private bool API_Name(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            engine.CurrentContext.EvaluationStack.Push(state.Name);
            return true;
        }

        private bool API_Symbol(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            engine.CurrentContext.EvaluationStack.Push(state.Symbol);
            return true;
        }

        private bool API_TotalSupply(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            engine.CurrentContext.EvaluationStack.Push(state.TotalSupply.GetData());
            return true;
        }

        private bool API_Decimals(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            engine.CurrentContext.EvaluationStack.Push((int)state.Decimals);
            return true;
        }

        private bool API_BalanceOf(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 address = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            StorageItem item = Snapshot.Storages.TryGet(new StorageKey
            {
                ScriptHash = state.AssetId,
                Key = address.ToArray()
            });

            if (item != null)
            {
                engine.CurrentContext.EvaluationStack.Push(new BigInteger(item.Value));
            }

            return true;
        }

        private bool API_Transfer(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (!Service.CheckWitness(engine, from))
                return false;

            //禁止跳板调用、入口脚本不是当前执行脚本说明是跳板调用
            if (engine.EntryContext.ScriptHash != engine.CurrentContext.ScriptHash)
                return false;

            bool result = Transfer(state, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                {
                    SaveTransferLog(state, tx.Hash, from, to, value);
                }

                Service.AddTransferNotification(engine, state.AssetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private bool API_Transfer_App(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 from = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            UInt160 to = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Fixed8 value = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());

            if (from != new UInt160(engine.CurrentContext.ScriptHash))
                return false;

            bool result = Transfer(state, from, to, value);

            if (result)
            {
                if (engine.ScriptContainer is Transaction tx)
                {
                    SaveTransferLog(state, tx.Hash, from, to, value);
                }

                Service.AddTransferNotification(engine, state.AssetId, from, to, value);
            }

            engine.CurrentContext.EvaluationStack.Push(result);

            return result;
        }

        private bool API_GetTransferLog(ExecutionEngine engine, NativeNEP5State state)
        {
            if (Trigger != TriggerType.Application) return false;
            byte[] hash = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            StorageItem item = Snapshot.Storages.TryGet(new StorageKey
            {
                ScriptHash = state.AssetId,
                Key = new byte[] { 0x13 }.Concat(hash).ToArray()
            });

            if (item == null)
                return false;

            engine.CurrentContext.EvaluationStack.Push(item.Value);
            return true;
        }

        private void SaveTransferLog(NativeNEP5State state, UInt256 TransactionHash, UInt160 from, UInt160 to, Fixed8 value)
        {
            var transferLog = new TransferLog
            {
                From = from,
                To = to,
                Value = value
            };

            StorageKey skey = new StorageKey
            {
                ScriptHash = state.AssetId,
                Key = new byte[] { 0x13 }.Concat(TransactionHash.ToArray()).ToArray()
            };

            StorageItem item = Snapshot.Storages.GetAndChange(skey, () => new StorageItem());
            item.Value = transferLog.ToArray();
        }

        private void AddBalance(NativeNEP5State state, UInt160 address, Fixed8 value)
        {
            if (value <= Fixed8.Zero)
                return;

            StorageKey skey = new StorageKey
            {
                ScriptHash = state.AssetId,
                Key = address.ToArray()
            };

            StorageItem item = Snapshot.Storages.GetAndChange(skey, () => new StorageItem());

            BigInteger balance = new BigInteger(item.Value);

            if (balance != 0)
            {
                balance = balance + value.GetData();
            }
            else
            {
                balance = value.GetData();
            }

            item.Value = balance.ToByteArray();
        }

        private bool SubBalance(NativeNEP5State state, UInt160 address, Fixed8 value)
        {
            StorageKey skey = new StorageKey
            {
                ScriptHash = state.AssetId,
                Key = address.ToArray()
            };

            StorageItem item = Snapshot.Storages.TryGet(skey);

            if (item == null)
                return false;

            BigInteger balance = new BigInteger(item.Value);

            if (balance < value.GetData())
                return false;

            balance = balance - value.GetData();

            if (balance == 0)
            {
                Snapshot.Storages.Delete(skey);
            }
            else
            {
                Snapshot.Storages.GetAndChange(skey, () => new StorageItem()).Value = balance.ToByteArray();
            }

            return true;
        }

        private bool Transfer(NativeNEP5State state, UInt160 from, UInt160 to, Fixed8 value)
        {
            if (value <= Fixed8.Zero)
                return false;

            if (from.Equals(to))
                return false;

            if (!SubBalance(state, from, value))
                return false;

            AddBalance(state, to, value);

            return true;
        }
    }
}
