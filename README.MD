#What is ZoroChain  #
&ensp;&ensp;&ensp;&ensp;ZoroChain是基于NEO技术开发的高速应用链（High Speed Application Chain），一应用一链，200-300毫秒确认交易，采用NEO智能合约虚拟机，可与NEO公链进行交互。  
&ensp;&ensp;&ensp;&ensp;ZoroChain is ‘High Speed Application Chain’ Based on NEO technology, one application one chain, transaction confirm by 200-300ms, use NEO smart contract virtual machine, and can interactive with NEO Chain.  
##What is Application Chain  ##
&ensp;&ensp;&ensp;&ensp;BitCoin开创了区块链的先河，Ethereum的智能合约给我们带来了DApp的可能，但是仅仅是可能，受制于公链性能，目前DApp的实际体验非常差；  
&ensp;&ensp;&ensp;&ensp;我们认为应该有这么一种链，性能上能满足普遍应用需求，必要的时候可以适当的牺牲安全性，其介于应用与公链之间，承载主要应用逻辑，由公链提供高安全性交易服务，连接应用与公链，我们称之为应用链；  
&ensp;&ensp;&ensp;&ensp;ZoroChain正是这样的一条链，在应用链上采用了特有的共识算法，简化了共识步骤，让共识能够快速完成，同时采用应用链与节点多对多的拓扑结构，任意节点可以随时加入网络，为任意应用链提供服务，是真正意义上的去中心化应用链，并提供与NEO公链交互接口。  
&ensp;&ensp;&ensp;&ensp;BitCoin bring us block chain, Ethereum give us smart contract and possibility of DApp, but only possibility, restrict by bottle neck of public chain performance, DApp’s user experience is quite dreadful.  
&ensp;&ensp;&ensp;&ensp;We suppose there is a chain like this, in performance it satisfies most app’s requirement, safety can be sacrificed properly in need, it’s between application and public chain, runs most chain logic of application，connect public chain and application and let public chain provide high safety transaction, we call it Application Chain.  
&ensp;&ensp;&ensp;&ensp;ZoroChain is a chain just like this, it use special consensus algorithm, simplified the consensus step, make consensus in very high speed, and we have a ‘n to n’ topological structure between application chain and chain node, any node can connect to chain network any time, and server any application chain, it’s a fully decentralized chain, and it provides NEO Chain operate interface.  
##RS matters than TPS  ##
&ensp;&ensp;&ensp;&ensp;目前主流公链竞争的一个重要指标是TPS（Transactions Per Second即每秒交易数），而在我们看来，应用链的一个重要指标是RS（Response Speed即响应速度），TPS再高，一个链上操作要等十几秒，应用的体验依旧很差，在大量链上操作的情况下，要达到优秀的应用体验，必须要有毫秒级别的操作响应速度，所以我们定义了RS这样一个指标，作为应用链的一个重要考核参数。  
&ensp;&ensp;&ensp;&ensp;ZoroChain通过一应用一链来解决拥堵问题，单应用链指标目前为1000+TPS，200-300毫秒RS，预计在2018年底达成，2019年目标单链5000+TPS，100毫秒以内RS。    
&ensp;&ensp;&ensp;&ensp;For now most public chain compete on TPS (Transactions Per Second), but in our opinion, one of most important indicator is RS (Response Speed), one chain has very high TPS but response transaction in very long time(let’s say ten second), user experience of the app build on it is also awful, great user experience with magnificent on chain operates need response very quickly like in millisecond, so we define RS as a first indicator of an application chain.  
&ensp;&ensp;&ensp;&ensp;ZoroChain solve traffic problem by use one application one chain strategy, single application performance is 1000+TPS, 200-300ms RS in 2018, grow to 5000+TPS, less than 100ms RS in 2019.  
##Application Engine  ##
&ensp;&ensp;&ensp;&ensp;从以太坊颠覆性的提出智能合约概念以来，智能合约得到了不断的发展，但是从主流公链的智能合约设计来看，其虚拟机还是服务于功能需求并不复杂的金融计算，对于计时、随机源、数据源、算法库、接口扩展等需求都并未做出很好的支持，受限于目前主流公链智能合约实现方式以及公链TPS性能限制，其也无法很好的支持复杂应用的实现。   
&ensp;&ensp;&ensp;&ensp;对于应用链来说，链上操作不应该局限于简单的智能合约，我们选择用应用合约（Application Contract）概念来代替智能合约（Smart Contract），应用合约运行于应用引擎（Application Engine），是应用链的操作入口。应用引擎对比智能合约虚拟机，需要具备更丰富的接口，提供满足应用的扩展性，其在支持复杂计算的同时，需要支持同步计算以满足单链TPS性能要求，以及需要支持快速达成链上共识以满足RS性能要求。  
&ensp;&ensp;&ensp;&ensp;ZoroChain的应用链将基于.NETCore对Neo VM进行扩展，以支持链上复杂应用逻辑需求，ZoroChain的Application Engine采用计算分层结构，.NETCore的复杂计算扩展部分将产生链上操作输出，作为输入提交给Neo VM进行节点间的链上共识以满足TPS以及RS要求，同时，.NETCore的输入亦将在链上同步，其他节点可以基于输入对.NETCore的链上操作输出进行验证，以识别并惩罚节点的.NETCore输出作弊行为。  
&ensp;&ensp;&ensp;&ensp;Since smart contract invented by Ethereum, it developing continuously, but by design of smart contract from most public chains, it’s target to solve finance problem that do not contain very complex computing, support to functions such as timing, random seed, data source, algorithm library, api extend, etc. is not very well, limited by implementation of smart contract and TPS, it can’t support complex applications.  
&ensp;&ensp;&ensp;&ensp;For application chain, on chain operate should not limit by typical smart contract, we prefer ‘Application Contract’ to ‘Smart Contract’, application contract runs by ‘Application Engine’, which operates the application chain. Application engine compare to smart contract vm, it needs more functions like mention above, more flexibility to extend, it needs support complex computing, meantime, as well as support concurrent computing to get high TPS, and support fast consensus to get fast RS.  
&ensp;&ensp;&ensp;&ensp;ZoroChain will extend NEO VM base on .NETCore, to support on chain complex computing, Application engine of ZoroChain compute in 2 level, .NETCore in charge with complex computing and give on chain operation output, and it will be send to Neo VM as input to make consensus on chain between nodes, thus to get high TPS and RS, and input of .NETCore also broadcast between nodes, therefor, these nodes can verify the output of .NETCore, check and punish nodes that cheat on the output.  
##ZoroChain Features  ##
&ensp;&ensp;&ensp;&ensp;Abstract consensus layer, various consensus algorithm support  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;dBFT consensus  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;VRF-BFT consensus  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;VRF-POWL consensus  
&ensp;&ensp;&ensp;&ensp;High speed P2P network, sub network support  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;Heart beat with time sync  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;DHT  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;UPnP  
&ensp;&ensp;&ensp;&ensp;Extendable Application Engine  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;NEO VM  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;Zoro VM (subset of .NETCore with sandbox)  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;On chain timing  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;On chain random  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;Data source API  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;Various algorithm API  
&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;&ensp;Plugin support  
&ensp;&ensp;&ensp;&ensp;Distribute crypto database  
