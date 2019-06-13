using System;
using System.Linq;
using System.Collections.Generic;
using Zoro.Ledger;
using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Zoro.SmartContract;
using Zoro.Plugins;
using Zoro.Wallets;
using Neo.VM;
using Akka.Actor;

namespace Zoro.Network.RPC
{
    public class RpcHandler
    {
        public RpcHandler()
        {
        }

        public JObject Process(string method, JArray _params)
        {
            try
            {
                switch (method)
                {
                    case "getbestblockhash":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            return GetBestBlockHash(blockchain);
                        }
                    case "getblock":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            JObject key = _params[1];
                            bool verbose = _params.Count >= 3 && _params[2].AsBoolean();
                            return GetBlock(blockchain, key, verbose);
                        }
                    case "getblockcount":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            return GetBlockCount(blockchain);
                        }
                    case "getblockhash":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            uint height = uint.Parse(_params[1].AsString());
                            return GetBlockHash(blockchain, height);
                        }
                    case "getblockheader":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            JObject key = _params[1];
                            bool verbose = _params.Count >= 3 && _params[2].AsBoolean();
                            return GetBlockHeader(blockchain, key, verbose);
                        }
                    case "getblocksysfee":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            uint height = uint.Parse(_params[1].AsString());
                            return GetBlockSysFee(blockchain, height);
                        }
                    case "getconnectioncount":
                        {
                            LocalNode localNode = GetTargetNode(_params[0]);
                            return localNode != null ? localNode.ConnectedCount : 0;
                        }
                    case "getcontractstate":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            return GetContractState(blockchain, script_hash);
                        }
                    case "getpeers":
                        {
                            return GetPeers(_params[0]);
                        }
                    case "getrawmempool":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            bool shouldGetUnverified = _params.Count >= 2 && _params[1].AsBoolean();
                            return GetRawMemPool(blockchain, shouldGetUnverified);
                        }
                    case "getrawmempoolcount":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            bool shouldGetUnverified = _params.Count >= 2 && _params[1].AsBoolean();
                            return GetRawMemPoolCount(blockchain, shouldGetUnverified);
                        }
                    case "getrawtransaction":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            UInt256 hash = UInt256.Parse(_params[1].AsString());
                            bool verbose = _params.Count >= 3 && _params[2].AsBoolean();
                            return GetRawTransaction(blockchain, hash, verbose);
                        }
                    case "getstorage":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            byte[] key = _params[2].AsString().HexToBytes();
                            return GetStorage(blockchain, script_hash, key);
                        }
                    case "gettransactionheight":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            UInt256 hash = UInt256.Parse(_params[1].AsString());
                            return GetTransactionHeight(blockchain, hash);
                        }
                    case "getvalidators":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            return GetValidators(blockchain);
                        }
                    case "getversion":
                        {
                            return GetVersion();
                        }
                    case "invokefunction":
                        {
                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            string operation = _params[2].AsString();
                            ContractParameter[] args = _params.Count >= 4 ? ((JArray)_params[3]).Select(p => ContractParameter.FromJson(p)).ToArray() : new ContractParameter[0];
                            return InvokeFunction(_params[0], script_hash, operation, args);
                        }
                    case "invokescript":
                        {
                            byte[] script = _params[1].AsString().HexToBytes();
                            return InvokeScript(_params[0], script);
                        }
                    case "listplugins":
                        {
                            return ListPlugins();
                        }
                    case "sendrawtransaction":
                        {
                            ZoroSystem targetSystem = GetTargetSystem(_params[0]);
                            if (targetSystem != null)
                            {
                                Transaction tx = Transaction.DeserializeFrom(_params[1].AsString().HexToBytes());
                                return SendRawTransaction(targetSystem, tx);
                            }
                            return RelayResultReason.Invalid;
                        }
                    case "submitblock":
                        {
                            ZoroSystem targetSystem = GetTargetSystem(_params[0]);
                            if (targetSystem != null)
                            {
                                Block block = _params[0].AsString().HexToBytes().AsSerializable<Block>();
                                return SubmitBlock(targetSystem, block);
                            }
                            return RelayResultReason.Invalid;
                        }
                    case "validateaddress":
                        {
                            string address = _params[0].AsString();
                            return ValidateAddress(address);
                        }
                    case "getappchainhashlist":
                        {
                            return GetAppChainHashList();
                        }
                    case "getappchainstate":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            UInt160 script_hash = UInt160.Parse(_params[0].AsString());                            
                            return GetAppChainState(blockchain, script_hash);
                        }
                    case "getappchainlist":
                        {
                            return GetAppChainList();
                        }
                    case "getappchainlistenerports":
                        {
                            return GetAppChainListenerPorts();
                        }
                    case "estimategas":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            Transaction tx = Transaction.DeserializeFrom(_params[1].AsString().HexToBytes());
                            return GetEstimateGas(blockchain, tx);
                        }
                    default:
                        throw new RpcException(-32601, "Method not found");
                }
            }
            catch (Exception ex)
            {
                JObject json = new JObject();
                json["message"] = ex.Message;
                json["method"] = method;
                json["source"] = ex.Source;
                return json;
            }
        }

        private JObject GetInvokeResult(JObject param, byte[] script)
        {
            Blockchain blockchain = GetTargetChain(param);

            ApplicationEngine engine = ApplicationEngine.Run(script, blockchain, testMode: true);

            JObject json = new JObject();
            json["script"] = script.ToHexString();
            json["state"] = engine.State;
            json["gas_consumed"] = engine.GasConsumed.ToString();
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            return json;
        }

        private static JObject GetRelayResult(RelayResultReason reason)
        {
            switch (reason)
            {
                case RelayResultReason.Succeed:
                    return true;
                case RelayResultReason.AlreadyExists:
                    throw new RpcException(-501, "Block or transaction already exists and cannot be sent repeatedly.");
                case RelayResultReason.OutOfMemory:
                    throw new RpcException(-502, "The memory pool is full and no more transactions can be sent.");
                case RelayResultReason.UnableToVerify:
                    throw new RpcException(-503, "The block cannot be validated.");
                case RelayResultReason.Invalid:
                    throw new RpcException(-504, "Block or transaction validation failed.");
                case RelayResultReason.PolicyFail:
                    throw new RpcException(-505, "One of the Policy filters failed.");
                default:
                    throw new RpcException(-500, "Unknown error.");
            }
        }

        private JObject GetBestBlockHash(Blockchain blockchain)
        {
            return blockchain.CurrentBlockHash.ToString();
        }

        private JObject GetBlock(Blockchain blockchain, JObject key, bool verbose)
        {
            Block block;
            if (key is JNumber)
            {
                uint index = uint.Parse(key.AsString());
                block = blockchain.Store.GetBlock(index);
            }
            else
            {
                UInt256 hash = UInt256.Parse(key.AsString());
                block = blockchain.Store.GetBlock(hash);
            }
            if (block == null)
                throw new RpcException(-100, "Unknown block");
            if (verbose)
            {
                JObject json = block.ToJson();
                json["confirmations"] = blockchain.Height - block.Index + 1;
                UInt256 hash = blockchain.Store.GetNextBlockHash(block.Hash);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }
            return block.ToArray().ToHexString();
        }

        private JObject GetBlockCount(Blockchain blockchain)
        {
            return blockchain.Height + 1;
        }

        private JObject GetBlockHash(Blockchain blockchain, uint height)
        {
            if (height <= blockchain.Height)
            {
                return blockchain.GetBlockHash(height).ToString();
            }
            throw new RpcException(-100, "Invalid Height");
        }

        private JObject GetBlockHeader(Blockchain blockchain, JObject key, bool verbose)
        {
            Header header;
            if (key is JNumber)
            {
                uint height = uint.Parse(key.AsString());
                header = blockchain.Store.GetHeader(height);
            }
            else
            {
                UInt256 hash = UInt256.Parse(key.AsString());
                header = blockchain.Store.GetHeader(hash);
            }
            if (header == null)
                throw new RpcException(-100, "Unknown block");

            if (verbose)
            {
                JObject json = header.ToJson();
                json["confirmations"] = blockchain.Height - header.Index + 1;
                UInt256 hash = blockchain.Store.GetNextBlockHash(header.Hash);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }

            return header.ToArray().ToHexString();
        }

        private JObject GetBlockSysFee(Blockchain blockchain, uint height)
        {
            if (height <= blockchain.Height)
            {
                return blockchain.Store.GetSysFeeAmount(height).ToString();
            }
            throw new RpcException(-100, "Invalid Height");
        }

        private JObject GetConnectionCount(LocalNode localNode)
        {
            return localNode.ConnectedCount;
        }

        private JObject GetContractState(Blockchain blockchain, UInt160 script_hash)
        {
            ContractState contract = blockchain.Store.GetContracts().TryGet(script_hash);
            return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
        }

        private JObject GetPeers(JObject param)
        {
            LocalNode localNode = GetTargetNode(param);

            JObject json = new JObject();
            if (localNode != null)
            {
                json["unconnected"] = new JArray(localNode.GetUnconnectedPeers().Select(p =>
                {
                    JObject peerJson = new JObject();
                    peerJson["address"] = p.Address.ToString();
                    peerJson["port"] = p.Port;
                    return peerJson;
                }));
                json["bad"] = new JArray(); //badpeers has been removed
                json["connected"] = new JArray(localNode.GetRemoteNodes().Select(p =>
                {
                    JObject peerJson = new JObject();
                    peerJson["address"] = p.Remote.Address.ToString();
                    peerJson["port"] = p.ListenerPort;
                    return peerJson;
                }));
            }

            return json;
        }

        private JObject GetRawMemPool(Blockchain blockchain, bool shouldGetUnverified)
        {
            if (!shouldGetUnverified)
                return new JArray(blockchain.GetVerifiedTransactions().Select(p => (JObject)p.Hash.ToString()));

            JObject json = new JObject();
            json["height"] = blockchain.Height;
            json["verified"] = new JArray(blockchain.GetVerifiedTransactions().Select(p => (JObject)p.Hash.ToString()));
            json["unverified"] = new JArray(blockchain.GetUnverifiedTransactions().Select(p => (JObject)p.Hash.ToString()));
            return json;
        }

        private int GetRawMemPoolCount(Blockchain blockchain, bool shouldGetUnverified)
        {
            if (!shouldGetUnverified)
                return blockchain.GetVerifiedTransactionCount();

            return blockchain.GetMemoryPoolCount();
        }

        private JObject GetRawTransaction(Blockchain blockchain, UInt256 hash, bool verbose)
        {
            Transaction tx = blockchain.GetTransaction(hash);
            if (tx == null)
                throw new RpcException(-100, "Unknown transaction");
            if (verbose)
            {
                JObject json = tx.ToJson();
                uint? height = blockchain.Store.GetTransactions().TryGet(hash)?.BlockIndex;
                if (height != null)
                {
                    Header header = blockchain.Store.GetHeader((uint)height);
                    json["blockhash"] = header.Hash.ToString();
                    json["confirmations"] = blockchain.Height - header.Index + 1;
                    json["blocktime"] = header.Timestamp;
                }
                return json;
            }
            return tx.ToArray().ToHexString();
        }

        private JObject GetStorage(Blockchain blockchain, UInt160 script_hash, byte[] key)
        {
            StorageItem item = blockchain.Store.GetStorages().TryGet(new StorageKey
            {
                ScriptHash = script_hash,
                Key = key
            }) ?? new StorageItem();
            return item.Value?.ToHexString();
        }

        private JObject GetTransactionHeight(Blockchain blockchain, UInt256 hash)
        {
            uint? height = blockchain.Store.GetTransactions().TryGet(hash)?.BlockIndex;
            if (height.HasValue) return height.Value;
            throw new RpcException(-100, "Unknown transaction");
        }

        private JObject GetValidators(Blockchain blockchain)
        {
            using (Snapshot snapshot = blockchain.GetSnapshot())
            {
                var validators = snapshot.GetValidators();
                return validators.Select(p =>
                {
                    JObject validator = new JObject();
                    validator["publickey"] = p.ToString();
                    return validator;
                }).ToArray();
            }
        }

        private JObject GetVersion()
        {
            JObject json = new JObject();
            json["port"] = LocalNode.Root.ListenerPort;
            json["nonce"] = LocalNode.NodeId;
            json["useragent"] = LocalNode.UserAgent;
            return json;
        }

        private JObject InvokeFunction(JObject param, UInt160 script_hash, string operation, ContractParameter[] args)
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                script = sb.EmitAppCall(script_hash, operation, args).ToArray();
            }
            return GetInvokeResult(param, script);
        }

        private JObject InvokeScript(JObject param, byte[] script)
        {
            return GetInvokeResult(param, script);
        }

        private JObject ListPlugins()
        {
            return new JArray(PluginManager.Singleton.Plugins
                .OrderBy(u => u.Name)
                .Select(u => new JObject
                {
                    ["name"] = u.Name,
                    ["version"] = u.Version.ToString(),
                    ["interfaces"] = new JArray(u.GetType().GetInterfaces()
                        .Select(p => p.Name)
                        .Where(p => p.EndsWith("Plugin"))
                        .Select(p => (JObject)p))
                }));
        }

        private JObject SendRawTransaction(ZoroSystem system, Transaction tx)
        {
            RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(tx).Result;
            return GetRelayResult(reason);
        }

        private JObject SubmitBlock(ZoroSystem system, Block block)
        {
            RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(block).Result;
            return GetRelayResult(reason);
        }

        private JObject ValidateAddress(string address)
        {
            JObject json = new JObject();
            UInt160 scriptHash;
            try
            {
                scriptHash = address.ToScriptHash();
            }
            catch
            {
                scriptHash = null;
            }
            json["address"] = address;
            json["isvalid"] = scriptHash != null;
            return json;
        }

        private JObject GetAppChainHashList()
        {
            JObject json = new JObject();
            IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);
            json["hashlist"] = new JArray(appchains.Select(p => (JObject)p.Hash.ToString()));

            return json;
        }

        private JObject GetAppChainState(Blockchain blockchain, UInt160 script_hash)
        {
            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(script_hash);
            JObject json = state?.ToJson() ?? throw new RpcException(-100, "Unknown appchain");            
            json["blockcount"] = blockchain != null ? blockchain.Height : 0;
            return json;
        }

        private JObject GetAppChainList()
        {
            IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);
            JObject json = new JArray(appchains.Select(p =>
            {
                JObject obj = new JObject();
                obj["name"] = p.Name;
                obj["hash"] = p.Hash.ToString();
                obj["owner"] = p.Owner.ToString();
                obj["type"] = p.Type.ToString();
                obj["createtime"] = $"{ p.Timestamp.ToDateTime():yyyy-MM-dd:hh\\:mm\\:ss}";
                obj["lastmodified"] = $"{ p.LastModified.ToDateTime():yyyy-MM-dd:hh\\:mm\\:ss}";
                obj["validators"] = new JArray(p.StandbyValidators.Select(q => (JObject)q.ToString()));
                obj["seedlist"] = new JArray(p.SeedList.Select(q => (JObject)q));
                return obj;
            }));
            return json;
        }

        private JObject GetAppChainListenerPorts()
        {
            LocalNode[] appchainNodes = ZoroChainSystem.Singleton.GetAppChainLocalNodes();
            JObject json = new JArray(appchainNodes.OrderBy(p => p.ListenerPort).Select(p =>
            {
                JObject obj = new JObject();
                obj["name"] = p.Blockchain.Name;
                obj["port"] = p.ListenerPort;
                return obj;
            }));
            return json;
        }

        private JObject GetEstimateGas(Blockchain blockchain, Transaction tx)
        {
            if (!(tx is InvocationTransaction tx_invocation))
                throw new RpcException(-100, "Invalid transaction type");

            using (ApplicationEngine engine = ApplicationEngine.Run(tx_invocation.Script, blockchain, tx, testMode: true))
            {
                JObject json = new JObject();
                json["state"] = engine.State;
                json["gas_consumed"] = engine.GasConsumed.ToString();
                return json;
            }
        }

        private Blockchain GetTargetChain(JObject param)
        {
            Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(param.AsString());

            if (blockchain == null)
                throw new RpcException(-100, "Invalid chain hash");

            return blockchain;
        }

        private LocalNode GetTargetNode(JObject param)
        {
            return ZoroChainSystem.Singleton.GetLocalNode(param.AsString());
        }

        private ZoroSystem GetTargetSystem(JObject param)
        {
            return ZoroChainSystem.Singleton.GetZoroSystem(param.AsString());
        }
    }
}
