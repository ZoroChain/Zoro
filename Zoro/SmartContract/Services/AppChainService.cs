using Zoro.Cryptography.ECC;
using Zoro.Ledger;
using Zoro.AppChain;
using Zoro.Persistence;
using Neo.VM;
using System;
using System.Net;
using System.Text;

namespace Zoro.SmartContract.Services
{
    class AppChainService
    {
        protected readonly ZoroService Service;
        protected readonly TriggerType Trigger;
        protected readonly Snapshot Snapshot;

        public AppChainService(ZoroService service, TriggerType trigger, Snapshot snapshot)
        {
            Service = service;
            Trigger = trigger;
            Snapshot = snapshot;
        }

        public bool CreateAppChain(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;
            try
            {
                // 只能在根链上执行创建应用链的指令
                if (!Snapshot.Blockchain.ChainHash.Equals(UInt160.Zero))
                    return false;

                // 应用链的Hash
                UInt160 hash = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

                // 应用链的名字
                if (engine.CurrentContext.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
                string name = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

                // 应用链的所有者
                ECPoint owner = ECPoint.DecodePoint(engine.CurrentContext.EvaluationStack.Pop().GetByteArray(), ECCurve.Secp256r1);
                if (owner.IsInfinity) return false;

                // 交易的见证人里必须有应用链的所有者
                if (!Service.CheckWitness(engine, owner))
                    return false;

                AppChainType type = (AppChainType)(byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                // 创建时间
                uint timestamp = (uint)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                // 种子节点的数量不能为零
                if (seedCount <= 0)
                    return false;

                string[] seedList = new string[seedCount];
                for (int i = 0; i < seedCount; i++)
                {
                    seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
                }

                // 判断输入的种子节点地址是否有效
                if (!CheckSeedList(seedList, seedCount))
                    return false;

                int validatorCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                // 共识节点的数量不能小于四个
                if (validatorCount < 4)
                    return false;

                ECPoint[] validators = new ECPoint[validatorCount];
                for (int i = 0; i < validatorCount; i++)
                {
                    validators[i] = ECPoint.DecodePoint(Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray()).HexToBytes(), ECCurve.Secp256r1);
                }

                // 判断输入的共识节点字符串格式是否有效
                if (!CheckValidators(validators, validatorCount))
                    return false;

                AppChainState state = Snapshot.AppChains.TryGet(hash);
                if (state == null)
                {
                    state = new AppChainState
                    {
                        Hash = hash,
                        Name = name,
                        Owner = owner,
                        Type = type,
                        Timestamp = timestamp,
                        LastModified = timestamp,
                        SeedList = seedList,
                        StandbyValidators = validators,
                    };

                    // 保存到数据库
                    Snapshot.AppChains.Add(hash, state);

                    // 添加通知事件，等待上链后处理
                    if (engine.ScriptContainer != null)
                        Snapshot.Blockchain.AddAppChainNotification("Create", state);
                }

                // 设置脚本的返回值
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool ChangeValidators(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 chainHash = Snapshot.Blockchain.ChainHash;

            // 只能在应用链上执行更改应用链共识节点的指令
            if (chainHash.Equals(UInt160.Zero))
                return false;

            // 在应用链的数据库里查询应用链状态信息
            AppChainState state = Snapshot.AppChainState.GetAndChange();
            if (state.Hash == null)
                return false;

            // 只有应用链的所有者有权限更换共识节点
            if (!Service.CheckWitness(engine, state.Owner))
                return false;

            int validatorCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

            // 共识节点的数量不能小于四个
            if (validatorCount < 4)
                return false;

            ECPoint[] validators = new ECPoint[validatorCount];
            for (int i = 0; i < validatorCount; i++)
            {
                validators[i] = ECPoint.DecodePoint(Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray()).HexToBytes(), ECCurve.Secp256r1);
            }

            // 判断输入的共识节点字符串格式是否有效
            if (!CheckValidators(validators, validatorCount))
                return false;

            // 将修改保存到应用链的数据库
            state.StandbyValidators = validators;
            state.LastModified = DateTime.UtcNow.ToTimestamp();

            // 添加通知事件，等待上链后处理
            if (engine.ScriptContainer != null)
                Snapshot.Blockchain.AddAppChainNotification("ChangeValidators", state);

            // 设置脚本的返回值
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));

            return true;
        }

        public bool ChangeSeedList(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 chainHash = Snapshot.Blockchain.ChainHash;

            // 只能在应用链上执行更改应用链共识节点的指令
            if (chainHash.Equals(UInt160.Zero))
                return false;

            // 在应用链的数据库里查询应用链状态信息
            AppChainState state = Snapshot.AppChainState.GetAndChange();
            if (state.Hash == null)
                return false;

            // 只有应用链的所有者有权限更改种子节点
            if (!Service.CheckWitness(engine, state.Owner))
                return false;

            int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

            // 种子节点的数量不能为零
            if (seedCount <= 0)
                return false;

            string[] seedList = new string[seedCount];
            for (int i = 0; i < seedCount; i++)
            {
                seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            }

            // 判断输入的种子节点地址是否有效
            if (!CheckSeedList(seedList, seedCount))
                return false;

            // 把变更保存到应用链的数据库
            state.SeedList = seedList;
            state.LastModified = DateTime.UtcNow.ToTimestamp();

            // 添加通知事件，等待上链后处理
            if (engine.ScriptContainer != null)
                Snapshot.Blockchain.AddAppChainNotification("ChangeSeedList", state);

            // 设置脚本的返回值
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(state));

            return true;
        }

        // 检查输入的共识节点是否无效或重复
        private bool CheckValidators(ECPoint[] validators, int count)
        {
            for (int i = 0; i < count; i++)
            {
                // 判断有效性
                if (validators[i].IsInfinity)
                    return false;

                // 判断重复
                for (int j = i + 1; j < count; j++)
                {
                    if (validators[i].Equals(validators[j]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // 检查输入的种子节点是否有效
        private bool CheckSeedList(string[] seedList, int count)
        {
            // 检查输入的种子节点是否重复
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (seedList[i].Equals(seedList[j]))
                    {
                        return false;
                    }
                }
            }

            // 检查输入的种子节点IP地址是否有效
            foreach (var ipaddress in seedList)
            {
                if (!CheckIPAddress(ipaddress))
                {
                    return false;
                }
            }

            return true;
        }

        // 检查IP地址是否有效
        private bool CheckIPAddress(string ipaddress)
        {
            string[] p = ipaddress.Split(':');
            if (p.Length < 2)
                return false;

            IPEndPoint seed;
            try
            {
                seed = Zoro.Helper.GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
            }
            catch (AggregateException)
            {
                return false;
            }
            if (seed == null) return false;
            return true;
        }
    }
}
