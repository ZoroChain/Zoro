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
using Zoro.Wallets;
using Zoro.Wallets.NEP6;
using Neo.VM;
using Akka.Actor;

namespace Zoro.Network.RPC
{
    public class RpcHandler
    {
        protected Wallet wallet;

        public RpcHandler()
        {
        }

        public RpcHandler(Wallet wallet)
        {
            this.wallet = wallet;
        }

        public void SetWallet(Wallet wallet)
        {
            this.wallet = wallet;
        }

        public JObject Process(string method, JArray _params)
        {
            try
            {

                switch (method)
                {
                    case "getbalance":
                        if (wallet == null)
                            throw new RpcException(-400, "Access denied.");
                        else
                        {
                            JObject json = new JObject();
                            switch (UIntBase.Parse(_params[0].AsString()))
                            {
                                case UInt160 asset_id_160: //NEP-5 balance
                                    json["balance"] = wallet.GetAvailable(asset_id_160).ToString();
                                    break;
                            }
                            return json;
                        }
                    case "getbestblockhash":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");
                            return blockchain.CurrentBlockHash.ToString();
                        }
                    case "getblock":
                        {
                            Block block;
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");
                            if (_params[1] is JNumber)
                            {
                                uint index = (uint)_params[1].AsNumber();
                                block = blockchain.Store.GetBlock(index);
                            }
                            else
                            {
                                UInt256 hash = UInt256.Parse(_params[1].AsString());
                                block = blockchain.Store.GetBlock(hash);
                            }
                            if (block == null)
                                throw new RpcException(-100, "Unknown block");
                            bool verbose = _params.Count >= 3 && _params[2].AsBoolean();
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
                    case "getblockcount":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");
                            return blockchain.Height + 1;
                        }
                    case "getblockhash":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");
                            uint height = (uint)_params[1].AsNumber();
                            if (height <= blockchain.Height)
                            {
                                return blockchain.GetBlockHash(height).ToString();
                            }
                            throw new RpcException(-100, "Invalid Height");
                        }
                    case "getblockheader":
                        {
                            Header header;
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");
                            if (_params[1] is JNumber)
                            {
                                uint height = (uint)_params[1].AsNumber();
                                header = blockchain.Store.GetHeader(height);
                            }
                            else
                            {
                                UInt256 hash = UInt256.Parse(_params[1].AsString());
                                header = blockchain.Store.GetHeader(hash);
                            }
                            if (header == null)
                                throw new RpcException(-100, "Unknown block");

                            bool verbose = _params.Count >= 3 && _params[2].AsBoolean();
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
                    case "getblocksysfee":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            uint height = (uint)_params[1].AsNumber();
                            if (height <= blockchain.Height)
                            {
                                return blockchain.Store.GetSysFeeAmount(height).ToString();
                            }
                            throw new RpcException(-100, "Invalid Height");
                        }
                    case "getconnectioncount":
                        {
                            LocalNode localNode = GetTargetNode(_params[0]);
                            return localNode != null ? localNode.ConnectedCount : 0;
                        }
                    case "getcontractstate":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Unknown blockchain");

                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            ContractState contract = blockchain.Store.GetContracts().TryGet(script_hash);
                            return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
                        }
                    case "getnewaddress":
                        if (wallet == null)
                            throw new RpcException(-400, "Access denied");
                        else
                        {
                            WalletAccount account = wallet.CreateAccount();
                            if (wallet is NEP6Wallet nep6)
                                nep6.Save();
                            return account.Address;
                        }
                    case "getpeers":
                        {
                            JObject json = new JObject();
                            LocalNode localNode = GetTargetNode(_params[0]);
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
                    case "getrawmempool":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            return new JArray(blockchain.GetMemoryPool().Select(p => (JObject)p.Hash.ToString()));
                        }
                    case "getrawmempoolcount":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            return blockchain.GetMemoryPoolCount();
                        }
                    case "getrawtransaction":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            UInt256 hash = UInt256.Parse(_params[1].AsString());
                            bool verbose = _params.Count >= 3 && _params[2].AsBoolean();
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
                    case "getstorage":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            byte[] key = _params[2].AsString().HexToBytes();
                            StorageItem item = blockchain.Store.GetStorages().TryGet(new StorageKey
                            {
                                ScriptHash = script_hash,
                                Key = key
                            }) ?? new StorageItem();
                            return item.Value?.ToHexString();
                        }
                    case "gettransactionheight":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            UInt256 hash = UInt256.Parse(_params[1].AsString());
                            uint? height = blockchain.Store.GetTransactions().TryGet(hash)?.BlockIndex;
                            if (height.HasValue) return height.Value;
                            throw new RpcException(-100, "Unknown transaction");
                        }
                    case "getvalidators":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

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
                    case "getversion":
                        {
                            JObject json = new JObject();
                            json["port"] = LocalNode.Root.ListenerPort;
                            json["nonce"] = LocalNode.NodeId;
                            json["useragent"] = LocalNode.UserAgent;
                            return json;
                        }
                    case "invoke":
                        {
                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            ContractParameter[] parameters = ((JArray)_params[2]).Select(p => ContractParameter.FromJson(p)).ToArray();
                            byte[] script;
                            using (ScriptBuilder sb = new ScriptBuilder())
                            {
                                script = sb.EmitAppCall(script_hash, parameters).ToArray();
                            }
                            return GetInvokeResult(_params[0], script);
                        }
                    case "invokefunction":
                        {
                            UInt160 script_hash = UInt160.Parse(_params[1].AsString());
                            string operation = _params[2].AsString();
                            ContractParameter[] args = _params.Count >= 4 ? ((JArray)_params[3]).Select(p => ContractParameter.FromJson(p)).ToArray() : new ContractParameter[0];
                            byte[] script;
                            using (ScriptBuilder sb = new ScriptBuilder())
                            {
                                script = sb.EmitAppCall(script_hash, operation, args).ToArray();
                            }
                            return GetInvokeResult(_params[0], script);
                        }
                    case "invokescript":
                        {
                            byte[] script = _params[1].AsString().HexToBytes();
                            return GetInvokeResult(_params[0], script);
                        }
                    case "listaddress":
                        if (wallet == null)
                            throw new RpcException(-400, "Access denied.");
                        else
                            return wallet.GetAccounts().Select(p =>
                            {
                                JObject account = new JObject();
                                account["address"] = p.Address;
                                account["haskey"] = p.HasKey;
                                account["label"] = p.Label;
                                account["watchonly"] = p.WatchOnly;
                                return account;
                            }).ToArray();
                    case "sendrawtransaction":
                        {
                            ZoroSystem targetSystem = GetTargetSystem(_params[0]);
                            if (targetSystem != null)
                            {
                                Transaction tx = Transaction.DeserializeFrom(_params[1].AsString().HexToBytes());
                                RelayResultReason reason = targetSystem.Blockchain.Ask<RelayResultReason>(tx).Result;
                                return GetRelayResult(reason);
                            }
                            return RelayResultReason.Invalid;
                        }
                    case "submitblock":
                        {
                            ZoroSystem targetSystem = GetTargetSystem(_params[0]);
                            if (targetSystem != null)
                            {
                                Block block = _params[0].AsString().HexToBytes().AsSerializable<Block>();
                                RelayResultReason reason = targetSystem.Blockchain.Ask<RelayResultReason>(block).Result;
                                return GetRelayResult(reason);
                            }
                            return RelayResultReason.Invalid;
                        }
                    case "validateaddress":
                        {
                            JObject json = new JObject();
                            UInt160 scriptHash;
                            try
                            {
                                scriptHash = _params[0].AsString().ToScriptHash();
                            }
                            catch
                            {
                                scriptHash = null;
                            }
                            json["address"] = _params[0];
                            json["isvalid"] = scriptHash != null;
                            return json;
                        }
                    case "getappchainhashlist":
                        {
                            JObject json = new JObject();
                            IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);
                            json["hashlist"] = new JArray(appchains.Select(p => (JObject)p.Hash.ToString()));

                            return json;
                        }
                    case "getappchainstate":
                        {
                            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
                            AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(script_hash);
                            JObject json = state?.ToJson() ?? throw new RpcException(-100, "Unknown appchain");
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            json["blockcount"] = blockchain != null ? blockchain.Height : 0;
                            return json;
                        }
                    case "getappchainlist":
                        {
                            IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);
                            JObject json = new JArray(appchains.Select(p => {
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
                    case "getappchainlistenerports":
                        {
                            LocalNode[] appchainNodes = ZoroChainSystem.Singleton.GetAppChainLocalNodes();
                            JObject json = new JArray(appchainNodes.OrderBy(p => p.ListenerPort).Select(p => {
                                JObject obj = new JObject();
                                obj["name"] = p.Blockchain.Name;
                                obj["port"] = p.ListenerPort;
                                return obj;
                                }));
                            return json;
                        }
                    case "estimategas":
                        {
                            Blockchain blockchain = GetTargetChain(_params[0]);
                            if (blockchain == null)
                                throw new RpcException(-100, "Invalid chain hash");

                            Transaction tx = Transaction.DeserializeFrom(_params[1].AsString().HexToBytes());
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
            if (blockchain == null)
                throw new RpcException(-100, "Invalid chain hash");

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
            if (wallet != null)
            {
                InvocationTransaction tx = new InvocationTransaction
                {
                    ChainHash = blockchain.ChainHash,
                    Script = json["script"].AsString().HexToBytes(),
                    GasLimit = InvocationTransaction.GetGasLimit(Fixed8.Parse(json["gas_consumed"].AsString()))
                };
                tx = wallet.MakeTransaction(tx);
                if (tx != null)
                {
                    ContractParametersContext context = new ContractParametersContext(tx, blockchain);
                    wallet.Sign(context);
                    if (context.Completed)
                        tx.Witnesses = context.GetWitnesses();
                    else
                        tx = null;
                }
                json["tx"] = tx?.ToArray().ToHexString();
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

        private Blockchain GetTargetChain(JObject param)
        {
            return ZoroChainSystem.Singleton.GetBlockchain(param.AsString());
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
