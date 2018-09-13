# What is ZoroChain  
&ensp;&ensp;&ensp;&ensp;ZoroChain是基于NEO技术开发的高速应用链（High Speed Application Chain），一应用一链，200-300毫秒确认交易，采用NEO智能合约虚拟机，可与NEO公链进行交互。  
&ensp;&ensp;&ensp;&ensp;ZoroChain is ‘High Speed Application Chain’ that runs as a side chain based on NEO blockchain, it focuses on providing each application with its own chain and transactions on ZoroChain are confirmed in less than 300ms. It leverages the NEO smart contract virtual machine and its highly interactive with NEO blockchain.  
## What is Application Chain  
&ensp;&ensp;&ensp;&ensp;BitCoin开创了区块链的先河，Ethereum的智能合约给我们带来了DApp的可能，但是仅仅是可能，受制于公链性能，目前DApp的实际体验非常差；  
&ensp;&ensp;&ensp;&ensp;我们认为应该有这么一种链，性能上能满足普遍应用需求，必要的时候可以适当的牺牲安全性，其介于应用与公链之间，承载主要应用逻辑，由公链提供高安全性交易服务，连接应用与公链，我们称之为应用链；  
&ensp;&ensp;&ensp;&ensp;ZoroChain正是这样的一条链，在应用链上采用了特有的共识算法，简化了共识步骤，让共识能够快速完成，同时采用应用链与节点多对多的拓扑结构，任意节点可以随时加入网络，为任意应用链提供服务，是真正意义上的去中心化应用链，并提供与NEO公链交互接口。  
&ensp;&ensp;&ensp;&ensp;BitCoin first introduced the concept of a blockchain, Ethereum provided us with smart contracts and the possibility of DApps, but its just a possibility.Currently public blockchain performance is restricted by a scalabity bottleneck and the user experience on DApps is quite dreadful.  
&ensp;&ensp;&ensp;&ensp;We belive that there should be a kind of blockchain whose performance effectively meets the user experience requirements, and when necessary security can be compromised between application and public chain to acheive this, it should be able to host the application logic，it should be able to support high speed transactions between the private and public blockchain , We call such an a blockchain an "Application Chain".
&ensp;&ensp;&ensp;&ensp;ZoroChain is a blockchain that meets the abovementioned requirements, it use special consensus algorithm which greatly simplifies the consensus phase making it possible for consensus to be reached on the chain in a swift manner. It's architecture supports a many to many topological structure between the application chain and the chain node, any node can connect to chain network at any time, and provide service for any application chain. It guarantees full decentralization and it provides an interface for mutual interaction with the NEO public blockchain. 
## RS matters than TPS  
&ensp;&ensp;&ensp;&ensp;目前主流公链竞争的一个重要指标是TPS（Transactions Per Second即每秒交易数），而在我们看来，应用链的一个重要指标是RS（Response Speed即响应速度），TPS再高，一个链上操作要等十几秒，应用的体验依旧很差，在大量链上操作的情况下，要达到优秀的应用体验，必须要有毫秒级别的操作响应速度，所以我们定义了RS这样一个指标，作为应用链的一个重要考核参数。  
&ensp;&ensp;&ensp;&ensp;ZoroChain通过一应用一链来解决拥堵问题，单应用链指标目前为1000+TPS，200-300毫秒RS，预计在2018年底达成，2019年目标单链5000+TPS，100毫秒以内RS。    
&ensp;&ensp;&ensp;&ensp;Currently the main public blockchain competition is centered on  TPS (Transactions Per Second), but in our opinion, one of the most important indicators is RS (Response Speed), even if a blockchain has a very high TPS but on chain actions take a long time(let’s say ten seconds), user experience of the app built on the blockchain will be awful in a scenario where there is a large amount of traffic.  To achieve an excellent user experience , it is imperative to have an onchain operation response speed in milliseconds, therefore we have defined RS (Response Speed) as one of the most important evaluation parameters of any blockchain that hosts user applications. 
&ensp;&ensp;&ensp;&ensp;ZoroChain solves the congestion and scalability problem by leveraging the One Application One Chain strategy, this ensures that single application performance can reach 1000+TPS(Transactions Per Second), 200-300ms RS(Response Speed) in 2018, and we expect this to grow to 5000+TPS (Transactions Per Second), less than 100ms RS (Response Speed) in 2019.  
## Application Engine  
&ensp;&ensp;&ensp;&ensp;从以太坊颠覆性的提出智能合约概念以来，智能合约得到了不断的发展，但是从主流公链的智能合约设计来看，其虚拟机还是服务于功能需求并不复杂的金融计算，对于计时、随机源、数据源、算法库、接口扩展等需求都并未做出很好的支持，受限于目前主流公链智能合约实现方式以及公链TPS性能限制，其也无法很好的支持复杂应用的实现。   
&ensp;&ensp;&ensp;&ensp;对于应用链来说，链上操作不应该局限于简单的智能合约，我们选择用应用合约（Application Contract）概念来代替智能合约（Smart Contract），应用合约运行于应用引擎（Application Engine），是应用链的操作入口。应用引擎对比智能合约虚拟机，需要具备更丰富的接口，提供满足应用的扩展性，其在支持复杂计算的同时，需要支持同步计算以满足单链TPS性能要求，以及需要支持快速达成链上共识以满足RS性能要求。  
&ensp;&ensp;&ensp;&ensp;ZoroChain的应用链将基于.NETCore对Neo VM进行扩展，以支持链上复杂应用逻辑需求，ZoroChain的Application Engine采用计算分层结构，.NETCore的复杂计算扩展部分将产生链上操作输出，作为输入提交给Neo VM进行节点间的链上共识以满足TPS以及RS要求，同时，.NETCore的输入亦将在链上同步，其他节点可以基于输入对.NETCore的链上操作输出进行验证，以识别并惩罚节点的.NETCore输出作弊行为。  
&ensp;&ensp;&ensp;&ensp;Since the inception of smart contracts from Ethereum,its development has been quite rapid but the inherent design of smart contracts on most public blockchains are such that they aim at solving finance problems that do not require very complex computing, support to functions such as timing, random seed, data source, algorithm library, api extend, etc. is not very well, limited by implementation of smart contract and TPS, it can’t support complex applications.  
&ensp;&ensp;&ensp;&ensp;For application chain, on chain operate should not limit by typical smart contract, we prefer ‘Application Contract’ to ‘Smart Contract’, application contract runs by ‘Application Engine’, which operates the application chain. Application engine compare to smart contract vm, it needs more functions like mention above, more flexibility to extend, it needs support complex computing, meantime, as well as support concurrent computing to get high TPS, and support fast consensus to get fast RS.  
&ensp;&ensp;&ensp;&ensp;ZoroChain will extend NEO VM base on .NETCore, to support on chain complex computing, Application engine of ZoroChain compute in 2 level, .NETCore in charge with complex computing and give on chain operation output, and it will be send to Neo VM as input to make consensus on chain between nodes, thus to get high TPS and RS, and input of .NETCore also broadcast between nodes, therefor, these nodes can verify the output of .NETCore, check and punish nodes that cheat on the output.  
## ZoroChain Features  
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
