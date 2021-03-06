﻿using System;
using Neo.VM;
using Zoro.SmartContract;
using Zoro.Cryptography.ECC;
using Zoro.Network.P2P.Payloads;

namespace Zoro.Ledger
{
    public class Genesis
    {
        public static UInt160 BcpContractAddress = new UInt160(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        public static UInt160 BctContractAddress = new UInt160(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

        public static Block BuildGenesisBlock(UInt160 ChainHash, ECPoint[] validators)
        {
            ECPoint owner = ECCurve.Secp256r1.Infinity;
            UInt160 admin = UInt160.Zero;

            if (ChainHash.Equals(UInt160.Zero))
                admin = Contract.CreateMultiSigRedeemScript(validators.Length / 2 + 1, validators).ToScriptHash();

            InvocationTransaction CreateBCPTransaction = CreateNativeNEP5Transaction("ZORO", "ZORO", Fixed8.FromDecimal(20000000000), 8, owner, admin, BcpContractAddress);            
            InvocationTransaction CreateBCTTransaction = CreateNativeNEP5Transaction("BlaCat Token", "BCT", Fixed8.Zero, 8, owner, admin, BctContractAddress);

            Block genesisBlock = new Block
            {
                PrevHash = UInt256.Zero,
                Timestamp = (new DateTime(2019, 1, 11, 0, 0, 0, DateTimeKind.Utc)).ToTimestamp(),
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
                    CreateBCPTransaction,
                    CreateBCTTransaction
                }
            };

            return genesisBlock;
        }
 
        private static InvocationTransaction CreateNativeNEP5Transaction(string name, string symbol, Fixed8 amount, byte precision, ECPoint owner, UInt160 admin, UInt160 assetId)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitPush(assetId);
                sb.EmitPush(admin);
                sb.EmitPush(owner);
                sb.EmitPush(precision);
                sb.EmitPush(amount);
                sb.EmitPush(symbol);
                sb.EmitPush(name);
                sb.EmitSysCall("Zoro.NativeNEP5.Create");
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Deploy", assetId);

                InvocationTransaction tx = new InvocationTransaction
                {
                    Nonce = 2083236893,
                    Script = sb.ToArray(),
                    GasPrice = Fixed8.One,
                    GasLimit = Fixed8.Zero,
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0]
                };

                return tx;
            }
        }
    }
}
