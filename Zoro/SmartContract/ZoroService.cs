﻿using Zoro.Cryptography.ECC;
using Zoro.Ledger;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Zoro.SmartContract.Enumerators;
using Zoro.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.IO;
using System.Linq;
using System.Text;
using VMArray = Neo.VM.Types.Array;

namespace Zoro.SmartContract
{
    public class ZoroService : NeoService
    {
        public ZoroService(TriggerType trigger, Snapshot snapshot)
            : base(trigger, snapshot)
        {
            Register("Zoro.AppChain.Create", AppChain_Create);
            Register("Zoro.AppChain.ChangeSeedList", AppChain_ChangeSeedList);
            Register("Zoro.AppChain.ChangeValidators", AppChain_ChangeValidators);
        }

        private bool AppChain_Create(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;
            try
            {
                UInt160 hash = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

                if (engine.CurrentContext.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
                string name = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

                ECPoint owner = ECPoint.DecodePoint(engine.CurrentContext.EvaluationStack.Pop().GetByteArray(), ECCurve.Secp256r1);
                if (owner.IsInfinity) return false;
                if (!CheckWitness(engine, owner))
                    return false;

                uint timestamp = (uint)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                int tcpPort = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                int wsPort = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();

                int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                string[] seedList = new string[seedCount];
                for (int i = 0; i < seedCount; i++)
                {
                    seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
                }

                int validatorCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                ECPoint[] validators = new ECPoint[validatorCount];
                for (int i = 0; i < validatorCount; i++)
                {
                    validators[i] = ECPoint.DecodePoint(Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray()).HexToBytes(), ECCurve.Secp256r1);
                }

                AppChainState appchain = Snapshot.AppChains.TryGet(hash);
                if (appchain == null)
                {
                    appchain = new AppChainState
                    {
                        Hash = hash,
                        Name = name,
                        Owner = owner,
                        Timestamp = timestamp,
                        TcpPort = tcpPort,
                        WsPort = wsPort,
                        SeedList = seedList,
                        StandbyValidators = validators,
                    };
                    Snapshot.AppChains.Add(hash, appchain);
                }

                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(appchain));
            }
            catch
            {
                return false;
            }
            return true;
        }

        private bool AppChain_ChangeSeedList(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 hash = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            AppChainState state = Snapshot.AppChains.TryGet(hash);
            if (state == null)
                return false;

            int validatorCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            ECPoint[] validators = new ECPoint[validatorCount];
            for (int i = 0; i < validatorCount; i++)
            {
                validators[i] = ECPoint.DecodePoint(Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray()).HexToBytes(), ECCurve.Secp256r1);
            }

            state.StandbyValidators = validators;

            Blockchain appchain = Blockchain.GetBlockchain(hash);
            if (appchain != null)
            {
                appchain.StandbyValidators = (ECPoint[])validators.Clone();
            }

            return true;
        }

        private bool AppChain_ChangeValidators(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;

            UInt160 hash = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());

            AppChainState state = Snapshot.AppChains.TryGet(hash);
            if (state == null)
                return false;

            int seedCount = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            string[] seedList = new string[seedCount];
            for (int i = 0; i < seedCount; i++)
            {
                seedList[i] = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            }

            state.SeedList = seedList;

            LocalNode appnode = LocalNode.GetLocalNode(hash);
            if (appnode != null)
            {
                appnode.SeedList = seedList;
            }

            return true;
        }
    }
}
