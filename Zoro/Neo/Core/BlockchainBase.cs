using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Caching;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Neo.Core
{
    /// <summary>
    /// 实现区块链功能的基类
    /// </summary>
    public abstract class BlockchainBase : IDisposable, IScriptTable
    {
        //public static event EventHandler<Block> PersistCompleted;
        //public static event EventHandler<Block> PersistUnlocked;

        //public CancellationTokenSource VerificationCancellationToken { get; protected set; } = new CancellationTokenSource();
        //public object PersistLock { get; } = new object();

        /// <summary>
        /// 产生每个区块的时间间隔，以秒为单位
        /// </summary>
        public readonly uint SecondsPerBlock = Settings.Default.SecondsPerBlock;
        public const uint DecrementInterval = 2000000;
        public const uint MaxValidators = 1024;
        public readonly uint[] GenerationAmount = { 8, 7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        /// <summary>
        /// 产生每个区块的时间间隔
        /// </summary>
        public readonly TimeSpan TimePerBlock;
        ///// <summary>
        ///// 后备记账人列表
        ///// </summary>
        //public static readonly ECPoint[] StandbyValidators = Settings.Default.StandbyValidators.OfType<string>().Select(p => ECPoint.DecodePoint(p.HexToBytes(), ECCurve.Secp256r1)).ToArray();

        /// <summary>
        /// Return true if haven't got valid handle
        /// </summary>
        public abstract bool IsDisposed { get; }

        /// <summary>
        /// 创世区块
        /// </summary>
        public abstract BlockBase GenesisBlock { get; }

        /// <summary>
        /// 当前最新区块散列值
        /// </summary>
        public abstract UInt256 CurrentBlockHash { get; }
        /// <summary>
        /// 当前最新区块头的散列值
        /// </summary>
        public abstract UInt256 CurrentHeaderHash { get; }
        /// <summary>
        /// 区块头高度
        /// </summary>
        public abstract uint HeaderHeight { get; }
        /// <summary>
        /// 区块高度
        /// </summary>
        public abstract uint Height { get; }
        
        /// <summary>
        /// 判断区块链中是否包含指定的区块
        /// </summary>
        /// <param name="hash">区块编号</param>
        /// <returns>如果包含指定区块则返回true</returns>
        public abstract bool ContainsBlock(UInt256 hash);

        /// <summary>
        /// 判断区块链中是否包含指定的交易
        /// </summary>
        /// <param name="hash">交易编号</param>
        /// <returns>如果包含指定交易则返回true</returns>
        public abstract bool ContainsTransaction(UInt256 hash);

        //public bool ContainsUnspent(CoinReference input)
        //{
        //    return ContainsUnspent(input.PrevHash, input.PrevIndex);
        //}

        public abstract bool ContainsUnspent(UInt256 hash, ushort index);

        public abstract MetaDataCache<T> GetMetaData<T>() where T : class, ISerializable, new();

        public abstract DataCache<TKey, TValue> GetStates<TKey, TValue>()
            where TKey : IEquatable<TKey>, ISerializable, new()
            where TValue : StateBase, ICloneable<TValue>, new();

        public abstract void Dispose();

        public abstract AccountState GetAccountState(UInt160 script_hash);

        public abstract AssetState GetAssetState(UInt256 asset_id);

        ///// <summary>
        ///// 根据指定的高度，返回对应的区块信息
        ///// </summary>
        ///// <param name="height">区块高度</param>
        ///// <returns>返回对应的区块信息</returns>
        //public Block GetBlock(uint height)
        //{
        //    UInt256 hash = GetBlockHash(height);
        //    if (hash == null) return null;
        //    return GetBlock(hash);
        //}

        ///// <summary>
        ///// 根据指定的散列值，返回对应的区块信息
        ///// </summary>
        ///// <param name="hash">散列值</param>
        ///// <returns>返回对应的区块信息</returns>
        //public abstract Block GetBlock(UInt256 hash);

        /// <summary>
        /// 根据指定的高度，返回对应区块的散列值
        /// </summary>
        /// <param name="height">区块高度</param>
        /// <returns>返回对应区块的散列值</returns>
        public abstract UInt256 GetBlockHash(uint height);

        public abstract ContractState GetContract(UInt160 hash);

        //public abstract IEnumerable<ValidatorState> GetEnrollments();

        /// <summary>
        /// 根据指定的高度，返回对应的区块头信息
        /// </summary>
        /// <param name="height">区块高度</param>
        /// <returns>返回对应的区块头信息</returns>
        public abstract Header GetHeader(uint height);

        /// <summary>
        /// 根据指定的散列值，返回对应的区块头信息
        /// </summary>
        /// <param name="hash">散列值</param>
        /// <returns>返回对应的区块头信息</returns>
        public abstract Header GetHeader(UInt256 hash);

        /// <summary>
        /// 获取记账人的合约地址
        /// </summary>
        /// <param name="validators">记账人的公钥列表</param>
        /// <returns>返回记账人的合约地址</returns>
        public static UInt160 GetConsensusAddress(ECPoint[] validators)
        {
            return Contract.CreateMultiSigRedeemScript(validators.Length - (validators.Length - 1) / 3, validators).ToScriptHash();
        }

        ///// <summary>
        ///// 根据指定的散列值，返回下一个区块的信息
        ///// </summary>
        ///// <param name="hash">散列值</param>
        ///// <returns>返回下一个区块的信息>
        //public abstract Block GetNextBlock(UInt256 hash);

        /// <summary>
        /// 根据指定的散列值，返回下一个区块的散列值
        /// </summary>
        /// <param name="hash">散列值</param>
        /// <returns>返回下一个区块的散列值</returns>
        public abstract UInt256 GetNextBlockHash(UInt256 hash);

        byte[] IScriptTable.GetScript(byte[] script_hash)
        {
            return GetContract(new UInt160(script_hash)).Script;
        }

        public abstract StorageItem GetStorageItem(StorageKey key);

        /// <summary>
        /// 根据指定的区块高度，返回对应区块及之前所有区块中包含的系统费用的总量
        /// </summary>
        /// <param name="height">区块高度</param>
        /// <returns>返回对应的系统费用的总量</returns>
        public virtual long GetSysFeeAmount(uint height)
        {
            return GetSysFeeAmount(GetBlockHash(height));
        }

        /// <summary>
        /// 根据指定的区块散列值，返回对应区块及之前所有区块中包含的系统费用的总量
        /// </summary>
        /// <param name="hash">散列值</param>
        /// <returns>返回系统费用的总量</returns>
        public abstract long GetSysFeeAmount(UInt256 hash);

        ///// <summary>
        ///// 根据指定的散列值，返回对应的交易信息
        ///// </summary>
        ///// <param name="hash">散列值</param>
        ///// <returns>返回对应的交易信息</returns>
        //public Transaction GetTransaction(UInt256 hash)
        //{
        //    return GetTransaction(hash, out _);
        //}

        ///// <summary>
        ///// 根据指定的散列值，返回对应的交易信息与该交易所在区块的高度
        ///// </summary>
        ///// <param name="hash">交易散列值</param>
        ///// <param name="height">返回该交易所在区块的高度</param>
        ///// <returns>返回对应的交易信息</returns>
        //public abstract Transaction GetTransaction(UInt256 hash, out int height);

        //public abstract Dictionary<ushort, SpentCoin> GetUnclaimed(UInt256 hash);

        ///// <summary>
        ///// 根据指定的散列值和索引，获取对应的未花费的资产
        ///// </summary>
        ///// <param name="hash">交易散列值</param>
        ///// <param name="index">输出的索引</param>
        ///// <returns>返回一个交易输出，表示一个未花费的资产</returns>
        //public abstract TransactionOutput GetUnspent(UInt256 hash, ushort index);

        //public abstract IEnumerable<TransactionOutput> GetUnspent(UInt256 hash);

        ///// <summary>
        ///// 判断交易是否双花
        ///// </summary>
        ///// <param name="tx">交易</param>
        ///// <returns>返回交易是否双花</returns>
        //public abstract bool IsDoubleSpend(Transaction tx);


        private static Dictionary<UInt256, BlockchainBase> Blockchains = new Dictionary<UInt256, BlockchainBase>();

        /// <summary>
        /// 默认的区块链实例
        /// </summary>
        public static BlockchainBase GetBlockchain(UInt256 hash)
        {
            BlockchainBase blockchain = null;
            Blockchains.TryGetValue(hash, out blockchain);
            return blockchain;
        }

        /// <summary>
        /// 注册默认的区块链实例
        /// </summary>
        /// <param name="blockchain">区块链实例</param>
        /// <returns>返回注册后的区块链实例</returns>
        public static BlockchainBase RegisterBlockchain(UInt256 hash, BlockchainBase blockchain)
        {
            if(blockchain == null)
            {
                return blockchain;
            }
            if (Blockchains.ContainsKey(hash))
            {
                // error : already exist
                return Blockchains[hash];
            }
            Blockchains[hash] = blockchain;
            return blockchain;
        }
    }
}
