﻿using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Neo.VM;
using System.Linq;

namespace Zoro.SmartContract
{
    internal class WitnessWrapper : IInteropInterface
    {
        public byte[] VerificationScript;

        public static WitnessWrapper[] Create(IVerifiable verifiable, Snapshot snapshot)
        {
            WitnessWrapper[] wrappers = verifiable.Witnesses.Select(p => new WitnessWrapper
            {
                VerificationScript = p.VerificationScript
            }).ToArray();
            if (wrappers.Any(p => p.VerificationScript.Length == 0))
            {
                UInt160[] hashes = verifiable.GetScriptHashesForVerifying(snapshot);
                for (int i = 0; i < wrappers.Length; i++)
                    if (wrappers[i].VerificationScript.Length == 0)
                        wrappers[i].VerificationScript = snapshot.Contracts[hashes[i]].Script;
            }
            return wrappers;
        }
    }
}
