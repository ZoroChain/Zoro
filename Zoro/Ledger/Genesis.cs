using System;
using System.Numerics;
using Neo.VM;
using Zoro.SmartContract;
using Zoro.Cryptography.ECC;
using Zoro.Network.P2P.Payloads;

namespace Zoro.Ledger
{
    internal class Genesis
    {
        public static UInt160 BCPHash = null;
        public static UInt160 BCTHash = null;

        public static Block BuildGenesisBlock(UInt160 ChainHash, ECPoint[] validators)
        {
            InvocationTransaction CreateBCP = CreateNativeNEP5Transaction("BlaCat Point", "BCP", 2000000000, 8, ECCurve.Secp256r1.Infinity, (new[] { (byte)OpCode.PUSHF }).ToScriptHash());
            InvocationTransaction DeployBCP = DeployNativeNEP5Transaction(CreateBCP.ScriptHash, Contract.CreateMultiSigRedeemScript(validators.Length / 2 + 1, validators).ToScriptHash());
            InvocationTransaction CreateBCT = CreateNativeNEP5Transaction("BlaCat Token", "BCT", 0, 8, ECCurve.Secp256r1.Infinity, (new[] { (byte)OpCode.PUSHF }).ToScriptHash());

            BCPHash = CreateBCP.ScriptHash;
            BCTHash = CreateBCT.ScriptHash;

            Block genesisBlock = new Block
            {
                PrevHash = UInt256.Zero,
                Timestamp = (new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc)).ToTimestamp(),
                Index = 0,
                ConsensusData = 2083236893, //向比特币致敬
                NextConsensus = Blockchain.GetConsensusAddress(validators),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Transactions = new Transaction[]
                {
                    new MinerTransaction
                    {
                        ChainHash = ChainHash,
                        Nonce = 2083236893,
                        Attributes = new TransactionAttribute[0],
                        Witnesses = new Witness[0]
                    },
                    CreateBCP,
                    DeployBCP,
                    CreateBCT,
                }
            };

            return genesisBlock;
        }
 
        private static InvocationTransaction CreateNativeNEP5Transaction(string name, string symbol, decimal amount, byte precision, ECPoint owner, UInt160 admin)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitPush(admin);
                sb.EmitPush(owner);
                sb.EmitPush(precision);
                sb.EmitPush(new BigInteger(amount));
                sb.EmitPush(symbol);
                sb.EmitPush(name);
                sb.EmitSysCall("Zoro.NativeNEP5.Create");

                InvocationTransaction tx = new InvocationTransaction
                {
                    Script = sb.ToArray(),
                    GasPrice = Fixed8.Zero,
                    GasLimit = Fixed8.Zero,
                    ScriptHash = owner.EncodePoint(true).ToScriptHash(),
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0]
                };

                return tx;
            }
        }

        private static InvocationTransaction DeployNativeNEP5Transaction(UInt160 assetId, UInt160 scriptHash)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Deploy", assetId);

                InvocationTransaction tx = new InvocationTransaction
                {
                    Script = sb.ToArray(),
                    GasPrice = Fixed8.Zero,
                    GasLimit = Fixed8.Zero,
                    ScriptHash = scriptHash,
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0]
                };

                return tx;
            }
        }
    }
}
