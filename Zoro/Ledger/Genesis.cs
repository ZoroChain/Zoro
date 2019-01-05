using System;
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
            UInt160 adminScriptHash = Contract.CreateMultiSigRedeemScript(validators.Length / 2 + 1, validators).ToScriptHash();

            InvocationTransaction CreateBCP = CreateNativeNEP5Transaction("BlaCat Point", "BCP", Fixed8.FromDecimal(2000000000), 8, ECCurve.Secp256r1.Infinity, adminScriptHash);
            InvocationTransaction DeployBCP = DeployNativeNEP5Transaction(CreateBCP.Script.ToScriptHash());
            InvocationTransaction CreateBCT = CreateNativeNEP5Transaction("BlaCat Token", "BCT", -Fixed8.Satoshi, 8, ECCurve.Secp256r1.Infinity, adminScriptHash);

            BCPHash = CreateBCP.Script.ToScriptHash();
            BCTHash = CreateBCT.Script.ToScriptHash();

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
 
        private static InvocationTransaction CreateNativeNEP5Transaction(string name, string symbol, Fixed8 amount, byte precision, ECPoint owner, UInt160 admin)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitPush(admin);
                sb.EmitPush(owner);
                sb.EmitPush(precision);
                sb.EmitPush(amount);
                sb.EmitPush(symbol);
                sb.EmitPush(name);
                sb.EmitSysCall("Zoro.NativeNEP5.Create");

                InvocationTransaction tx = new InvocationTransaction
                {
                    Nonce = 2083236893,
                    Script = sb.ToArray(),
                    GasPrice = Fixed8.Zero,
                    GasLimit = Fixed8.Zero,
                    Account = owner.EncodePoint(true).ToScriptHash(),
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0]
                };

                return tx;
            }
        }

        private static InvocationTransaction DeployNativeNEP5Transaction(UInt160 assetId)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Deploy", assetId);

                InvocationTransaction tx = new InvocationTransaction
                {
                    Nonce = 2083236893,
                    Script = sb.ToArray(),
                    GasPrice = Fixed8.Zero,
                    GasLimit = Fixed8.Zero,
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0]
                };

                return tx;
            }
        }
    }
}
