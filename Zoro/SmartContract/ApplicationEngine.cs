using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Neo.VM;
using Neo.VM.Types;

namespace Zoro.SmartContract
{
    public class ApplicationEngine : ExecutionEngine
    {
        private const long ratio = 100000000;
        private readonly long gas_amount;
        private long gas_consumed = 0;
        private readonly bool testMode;
        private readonly Snapshot snapshot;

        public Fixed8 GasConsumed => new Fixed8(gas_consumed);
        public new ZoroService Service => (ZoroService)base.Service;

        public ApplicationEngine(TriggerType trigger, IScriptContainer container, Snapshot snapshot, Fixed8 gas, bool testMode = false)
            : base(container, Cryptography.Crypto.Default, snapshot, new ZoroService(trigger, snapshot))
        {
            this.gas_amount = gas.GetData();
            this.testMode = testMode;
            this.snapshot = snapshot;
        }

        private bool CheckDynamicInvoke()
        {
            Instruction instruction = CurrentContext.CurrentInstruction;
            switch (instruction.OpCode)
            {
                case OpCode.APPCALL:
                case OpCode.TAILCALL:
                    if (instruction.Operand.NotZero()) return true;
                    // if we get this far it is a dynamic call
                    // now look at the current executing script
                    // to determine if it can do dynamic calls
                    return snapshot.Contracts[new UInt160(CurrentContext.ScriptHash)].HasDynamicInvoke;
                case OpCode.CALL_ED:
                case OpCode.CALL_EDT:
                    return snapshot.Contracts[new UInt160(CurrentContext.ScriptHash)].HasDynamicInvoke;
                default:
                    return true;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            Service.Dispose();
        }

        protected virtual long GetPrice()
        {
            Instruction instruction = CurrentContext.CurrentInstruction;
            if (instruction.OpCode <= OpCode.NOP) return 0;
            switch (instruction.OpCode)
            {
                case OpCode.APPCALL:
                case OpCode.TAILCALL:
                    return 10;
                case OpCode.SYSCALL:
                    return GetPriceForSysCall();
                case OpCode.SHA1:
                case OpCode.SHA256:
                    return 10;
                case OpCode.HASH160:
                case OpCode.HASH256:
                    return 20;
                case OpCode.CHECKSIG:
                case OpCode.VERIFY:
                    return 100;
                case OpCode.CHECKMULTISIG:
                    {
                        if (CurrentContext.EvaluationStack.Count == 0) return 1;

                        var item = CurrentContext.EvaluationStack.Peek();

                        int n;
                        if (item is Array array) n = array.Count;
                        else n = (int)item.GetBigInteger();

                        if (n < 1) return 1;
                        return 100 * n;
                    }
                default: return 1;
            }
        }

        protected virtual long GetPriceForSysCall()
        {
            Instruction instruction = CurrentContext.CurrentInstruction;
            uint api_hash = instruction.Operand.Length == 4
                ? instruction.TokenU32
                : instruction.TokenString.ToInteropMethodHash();
            long price = Service.GetPrice(api_hash, this);
            if (price > 0) return price;

            return 1;
        }

        protected override bool PreExecuteInstruction()
        {
            if (CurrentContext.InstructionPointer >= CurrentContext.Script.Length)
                return true;
            gas_consumed = checked(gas_consumed + GetPrice() * ratio);
            if (!testMode && gas_consumed > gas_amount) return false;
            if (!CheckDynamicInvoke()) return false;
            return true;
        }

        public static ApplicationEngine Run(byte[] script, Snapshot snapshot,
            IScriptContainer container = null, Block persistingBlock = null, bool testMode = false)
        {
            snapshot.PersistingBlock = persistingBlock ?? snapshot.PersistingBlock ?? new Block
            {
                Version = 0,
                PrevHash = snapshot.CurrentBlockHash,
                MerkleRoot = new UInt256(),
                Timestamp = snapshot.Blocks[snapshot.CurrentBlockHash].TrimmedBlock.Timestamp + Blockchain.SecondsPerBlock,
                Index = snapshot.Height + 1,
                ConsensusData = 0,
                NextConsensus = snapshot.Blocks[snapshot.CurrentBlockHash].TrimmedBlock.NextConsensus,
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new byte[0]
                },
                Transactions = new Transaction[0]
            };
            ApplicationEngine engine = new ApplicationEngine(TriggerType.Application, container, snapshot, Fixed8.Zero, testMode);
            engine.LoadScript(script);
            engine.Execute();
            return engine;
        }

        public static ApplicationEngine Run(byte[] script, Blockchain blockchain, IScriptContainer container = null, Block persistingBlock = null, bool testMode = false)
        {
            using (Snapshot snapshot = blockchain.GetSnapshot())
            {
                return Run(script, snapshot, container, persistingBlock, testMode);
            }
        }
    }
}
