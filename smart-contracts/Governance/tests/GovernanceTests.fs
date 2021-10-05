module GovernanceTests

open System
open Xunit
open FsUnit.Xunit
open FsUnit.CustomMatchers
open TestBase
open GovernanceTestBase
open Nethereum.Web3.Accounts
open Nethereum.Contracts
open SolidityTypes
open AbiTypeProvider.Common


type System.String with
   member s1.icompare(s2: string) =
     System.String.Equals(s1, s2, StringComparison.CurrentCultureIgnoreCase);

[<Specification("gFry", "constructor", 0)>]
[<Fact>]
let ``initializes with correct initial supply`` () =
    restore ()

    let gFryCon = getGFryContract()
    let zero = bigint 0;

    // STATE
    should equal hardhatAccount (gFryCon.governatorQuery())
    should equal zero (gFryCon.totalSupplyQuery())
    should equal zero (gFryCon.balanceOfQuery(hardhatAccount))

[<Specification("gFry", "mint", 0)>]
[<Fact>]
let ``Non gFry deployer account can not mint`` () =
    restore ()

    let gFryCon = getGFryContract()
    let account = Account(hardhatPrivKey2)
    let mintAmount = bigint 10
    let zero = bigint 0;

    let debug = Debug(EthereumConnection(hardhatURI, account.PrivateKey))
    let data = gFryCon.mintData(account.Address, mintAmount)

    let receipt = debug.Forward(gFryCon.Address,  data)
    let forwardEvent = debug.DecodeForwardedEvents receipt |> Seq.head

    // RETURNS
    forwardEvent |> shouldRevertWithMessage "Comp::_mint: That account cannot mint"
    
    // STATE
    should equal zero (gFryCon.totalSupplyQuery())
    should equal zero (gFryCon.balanceOfQuery(account.Address))

[<Specification("gFry", "mint", 1)>]
[<Fact>]
let ``Can't mint to the zero address`` () =
    restore ()

    let gFryCon = getGFryContract()
    let mintAmount = bigint 10
    let zero = bigint 0;

    try
        gFryCon.mint(zeroAddress, mintAmount) |> ignore
        failwith "Should not be able to mint to zero address"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("cannot mint to the zero address") 
        |> should equal true

    // STATE
    should equal zero (gFryCon.totalSupplyQuery())
    should equal zero (gFryCon.balanceOfQuery(zeroAddress))

[<Specification("gFry", "mint", 3)>]
[<Theory>]
[<InlineData(10)>]
[<InlineData(0)>]
let ``Deployer can mint positive amount`` amount =
    restore ()

    let gFryCon = Contracts.gFRYContract(ethConn.GetWeb3)
    let zero = bigint 0;
    let mintAmountBigInt = (amount |> toE18)// bigint 10;
    let mintTx = gFryCon.mint(hardhatAccount,  mintAmountBigInt)
    mintTx |> shouldSucceed

    // EVENTS
    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents mintTx) |> Seq.head
    event.from |> should equal zeroAddress
    event._to |> should equal hardhatAccount
    event.amount |> should equal mintAmountBigInt

    // STATE
    should equal mintAmountBigInt (gFryCon.balanceOfQuery(hardhatAccount))
    should equal mintAmountBigInt (gFryCon.totalSupplyQuery())
    
[<Specification("gFry", "mint", 4)>]
[<Fact>]
let ``Deployer can mint and voting power is updated accordingly`` () =
    restore ()

    let gFryCon = Contracts.gFRYContract(ethConn.GetWeb3)
    let toMint = bigint 100
    let zero = 0

    let delegateTx = gFryCon.delegateAsync(hardhatAccount) |> runNow
    delegateTx |> shouldSucceed
    let gFryCon2 = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryCon.Address)
    let getVotesOfFunction = gFryCon2.GetFunction("getCurrentVotes")
    let votesBeforeMint = getVotesOfFunction.CallAsync<int>(hardhatAccount) |> runNow
    let totalSupplyBeforeMint = gFryCon.totalSupplyQuery()
    
    let mintTx = gFryCon.mint(hardhatAccount,  toMint)
    mintTx |> shouldSucceed
    let mintTx2 = gFryCon.mint(hardhatAccount2,  toMint)
    mintTx2|> shouldSucceed
    
    let votesAfterMint = getVotesOfFunction.CallAsync<int>(hardhatAccount) |> runNow
    let totalSupplyAfterMint = gFryCon.totalSupplyQuery()

    // STATE
    totalSupplyBeforeMint |> should equal (zero |> bigint)
    totalSupplyAfterMint |> should equal (toMint*(2 |> bigint))
    votesBeforeMint |> should equal (zero)
    votesAfterMint |> should equal ((toMint) |> int)

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents mintTx) |> Seq.head
    // EVENTS
    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents mintTx) |> Seq.head
    event.from |> should equal zeroAddress
    event._to |> should equal hardhatAccount
    event.amount |> should equal toMint

    let event = (Contracts.gFRYContract.DelegateVotesChangedEventDTO.DecodeAllEvents mintTx) |> Seq.head
    event._delegate |> should equal hardhatAccount
    event._previousBalance |> should equal (votesBeforeMint |> bigint)
    event._newBalance |> should equal (votesAfterMint |> bigint)


[<Specification("gFry", "burn", 0)>]
[<Fact>]
let ``Account with zero balance can't burn`` () =
    restore ()

    let gFryConnection = Contracts.gFRYContract(ethConn.GetWeb3)
    let zero = bigint 0;
    let amountToBurn = bigint 10;

    try
        gFryConnection.burn(amountToBurn) |> ignore
        failwith "Should not be able to burn with zero balance"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("burn underflows") 
        |> should equal true
        
        // STATE
        let balanceAfterBurn = gFryConnection.balanceOfQuery(hardhatAccount)
        balanceAfterBurn |> should equal zero
        let totalSupplyAfterBurn = gFryConnection.totalSupplyQuery()
        totalSupplyAfterBurn |> should equal zero

[<Specification("gFry", "burn", 1)>]
[<Fact>]
let ``Account with non zero balance can't burn more tokens than they have`` () =
    restore ()

    let gFryConnection = Contracts.gFRYContract(ethConn.GetWeb3)
    let amountToBurn = bigint 150
    let toMint = bigint 100

    let mintTx = gFryConnection.mint(hardhatAccount,  toMint)
    mintTx |> shouldSucceed
    let mintTx2 = gFryConnection.mint(hardhatAccount2,  toMint)
    mintTx2|> shouldSucceed

    try
        gFryConnection.burn(amountToBurn) |> ignore
        failwith "Should not be able to burn more tokens than they have"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("burn underflows")
        |> should equal true
        
        // STATE
        let balanceAfterBurn = gFryConnection.balanceOfQuery(hardhatAccount)
        balanceAfterBurn |> should equal toMint
        let totalSupplyAfterBurn = gFryConnection.totalSupplyQuery()
        totalSupplyAfterBurn |> should equal (toMint*(2 |> bigint))

[<Specification("gFry", "burn", 2)>]
[<Theory>]
[<InlineData(1000000, 1000)>]
[<InlineData(0, 0)>]
let ``Account with positive balance can burn`` (amountToMint, amountToBurn) =
    restore ()

    let gFryConnection = Contracts.gFRYContract(ethConn.GetWeb3)
    let toMint = amountToMint |> bigint
    let burnAmount = amountToBurn |> bigint

    let mintTx = gFryConnection.mint(hardhatAccount,  toMint)
    mintTx |> shouldSucceed
    let mintTx2 = gFryConnection.mint(hardhatAccount2,  toMint)
    mintTx2|> shouldSucceed

    let totalSupplyBeforeBurn = gFryConnection.totalSupplyQuery()

    let burnTx = gFryConnection.burn(burnAmount)
    burnTx |> shouldSucceed
    let balanceAfterBurn = gFryConnection.balanceOfQuery(hardhatAccount)
    let totalSupplyAfterBurn = gFryConnection.totalSupplyQuery()

    // STATE
    balanceAfterBurn |> should equal (toMint - burnAmount)
    totalSupplyAfterBurn |> should equal (totalSupplyBeforeBurn - burnAmount)

    // EVENTS
    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents burnTx) |> Seq.head
    event.from |> should equal hardhatAccount
    event._to |> should equal zeroAddress
    event.amount |> should equal burnAmount


[<Specification("gFry", "burn", 3)>]
[<Theory>]
[<InlineData(1000000, 1000)>]
[<InlineData(10, 1)>]
let ``Account with positive balance can burn and voting power is updated accordingly`` (amountToMint, amountToBurn) =
    restore ()

    let gFryConnection = Contracts.gFRYContract(ethConn.GetWeb3)
    let toMint = amountToMint |> bigint
    let burnAmount = amountToBurn |> bigint

    let mintTx = gFryConnection.mint(hardhatAccount,  toMint)
    mintTx |> shouldSucceed
    let mintTx2 = gFryConnection.mint(hardhatAccount2,  toMint)
    mintTx2|> shouldSucceed
    let delegateTx = gFryConnection.delegateAsync(hardhatAccount) |> runNow
    delegateTx |> shouldSucceed
    let gFryCon2 = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryConnection.Address)
    let getVotesOfFunction = gFryCon2.GetFunction("getCurrentVotes")
    let votesBeforeBurn = getVotesOfFunction.CallAsync<int>(hardhatAccount) |> runNow

    let totalSupplyBeforeBurn = gFryConnection.totalSupplyQuery()

    let burnTx = gFryConnection.burn(burnAmount)
    burnTx |> shouldSucceed
    let balanceAfterBurn = gFryConnection.balanceOfQuery(hardhatAccount)
    let votesAfterBurn = getVotesOfFunction.CallAsync<int>(hardhatAccount) |> runNow
    let totalSupplyAfterBurn = gFryConnection.totalSupplyQuery()

    // STATE
    balanceAfterBurn |> should equal (toMint - burnAmount)
    totalSupplyAfterBurn |> should equal (totalSupplyBeforeBurn - burnAmount)
    votesBeforeBurn |> should equal (toMint |> int)
    votesAfterBurn |> should equal ((toMint - burnAmount) |> int)

    // EVENTS
    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents burnTx) |> Seq.head
    event.from |> should equal hardhatAccount
    event._to |> should equal zeroAddress
    event.amount |> should equal burnAmount

    let event = (Contracts.gFRYContract.DelegateVotesChangedEventDTO.DecodeAllEvents burnTx) |> Seq.head
    event._delegate |> should equal hardhatAccount
    event._previousBalance |> should equal (votesBeforeBurn |> bigint)
    event._newBalance |> should equal (votesAfterBurn |> bigint)
    
[<Specification("gFry", "transferFrom", 0)>]
[<Fact>]
let ``Non deployer can't transfer without allowance`` () =
    restore () 
    
    let gFryCon = getGFryContract()
    let account = Account(hardhatPrivKey)
    let toMint = bigint 100
    let zero = bigint 0  

    let mintTx = gFryCon.mint(account.Address,  toMint)
    mintTx |> shouldSucceed

    let debug = Debug(EthereumConnection(hardhatURI, account.PrivateKey))
    let data = gFryCon.transferFromData(account.Address, hardhatAccount2, bigint 10)
    let receipt = debug.Forward(gFryCon.Address,  data)

    // RETURNS
    let forwardEvent = debug.DecodeForwardedEvents receipt |> Seq.head
    forwardEvent |> shouldRevertWithMessage "Comp::transferFrom: transfer amount exceeds spender allowance"

    // STATE
    should equal toMint (gFryCon.balanceOfQuery(account.Address))
    should equal zero (gFryCon.balanceOfQuery(hardhatAccount2))

[<Specification("gFry", "transferFrom", 1)>]
[<Fact>]
let ``Cannot transfer to zero address`` () =
    restore ()

    let gFryConnection = Contracts.gFRYContract(ethConn.GetWeb3)
    let toMint = bigint 100
    let toTransfer = bigint 10
    let zero = bigint 0

    let mintTx = gFryConnection.mint(hardhatAccount,  toMint)
    mintTx |> shouldSucceed

    try
        gFryConnection.transferFrom(hardhatAccount, zeroAddress, toTransfer) |> ignore
        failwith "Should not be able to transfer to with zero address"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("cannot transfer to the zero address")
        |> should equal true
        
    // STATE
    let balanceAfterTransfer = gFryConnection.balanceOfQuery(hardhatAccount)
    balanceAfterTransfer |> should equal toMint
    let zeroAddressBalanceAfterTransfer = gFryConnection.balanceOfQuery(zeroAddress)
    zeroAddressBalanceAfterTransfer |> should equal zero
    let totalSupplyAfter = gFryConnection.totalSupplyQuery()
    totalSupplyAfter |> should equal toMint

[<Specification("gFry", "transferFrom", 2)>]
[<Fact>]
let ``Cannot transfer more than uint96 max`` () =
    restore ()

    let gFryConnection = Contracts.gFRYContract(ethConn.GetWeb3)
    let hugeNumber = 10000000000000000000.0 |> toE18
    let toMint = bigint 100;

    let mintTx = gFryConnection.mint(hardhatAccount,  toMint)
    mintTx |> shouldSucceed

    try
        gFryConnection.transferFrom(hardhatAccount, hardhatAccount2, hugeNumber) |> ignore
        failwith "Should not be able to transfer more than uint96 max"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("amount exceeds 96 bits")
        |> should equal true
    
    // STATE
    let balanceAfterTransfer = gFryConnection.balanceOfQuery(hardhatAccount)
    balanceAfterTransfer |> should equal toMint
    let totalSupplyAfter = gFryConnection.totalSupplyQuery()
    totalSupplyAfter |> should equal toMint

[<Specification("gFry", "transferFrom", 3)>]
[<Fact>]
let ``Non deployer can transferFrom when approved`` () =
    restore ()

    let toMint = bigint 20000
    let transferAmount = bigint 5000;
    let zero = bigint 0

    let connection = ethConn.GetWeb3
    let gFryCon1 = Contracts.gFRYContract(connection)

    let approveInput = gFryCon1.approveTransactionInput(string hardhatAccount3, bigint (transferAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon1.Address
    approveInput.To <- gFryCon1.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let mintTx2 = gFryCon1.mint(hardhatAccount2,  toMint)
    mintTx2 |> shouldSucceed

    let balanceBeforeTransferAccount2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceBeforeTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)
    
    let transferInput = gFryCon1.transferFromTransactionInput(string hardhatAccount2, string hardhatAccount3, bigint (transferAmount |> int))
    transferInput.From <- mapInlineDataArgumentToAddress hardhatAccount3 gFryCon1.Address
    transferInput.To <- gFryCon1.Address
    let transferFromTxr = ethConn.MakeImpersonatedCallWithNoEther transferInput
        
    let balanceAfterTransferAccount2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceAfterTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)

    let allowanceAfter = gFryCon1.allowanceQuery(hardhatAccount2, hardhatAccount3)

    // STATE
    allowanceAfter
    |> should equal zero
    balanceAfterTransferAccount2
    |> should equal (toMint - transferAmount)
    balanceAfterTransferAccount3
    |> should equal transferAmount
    
    // EVENTS
    let event = (Contracts.gFRYContract.ApprovalEventDTO.DecodeAllEvents approveTxr) |> Seq.head
    event.owner |> should equal hardhatAccount2
    event.spender |> should equal hardhatAccount3
    event.amount |> should equal transferAmount

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents transferFromTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal hardhatAccount3
    event.amount |> should equal transferAmount
    
[<Specification("gFry", "transferFrom", 4)>]
[<Fact>]
let ``Non deployer can transferFrom when approved and msg.sender is the src address`` () =
    restore ()

    let toMint = bigint 20000
    let transferAmount = bigint 5000;
    let zero = bigint 0

    let connection = ethConn.GetWeb3
    let gFryCon1 = Contracts.gFRYContract(connection)

    let mintTx2 = gFryCon1.mint(hardhatAccount2,  toMint)
    mintTx2 |> shouldSucceed

    let balanceBeforeTransferAccount2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceBeforeTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)
    
    let transferInput = gFryCon1.transferFromTransactionInput(string hardhatAccount2, string hardhatAccount3, bigint (transferAmount |> int))
    transferInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon1.Address
    transferInput.To <- gFryCon1.Address
    let transferFromTxr = ethConn.MakeImpersonatedCallWithNoEther transferInput
        
    let balanceAfterTransferAccount2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceAfterTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)

    let allowanceAfter = gFryCon1.allowanceQuery(hardhatAccount2, hardhatAccount3)

    // STATE
    allowanceAfter
    |> should equal zero
    balanceAfterTransferAccount2
    |> should equal (toMint - transferAmount)
    balanceAfterTransferAccount3
    |> should equal transferAmount
    
    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents transferFromTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal hardhatAccount3
    event.amount |> should equal transferAmount

[<Specification("gFry", "transferFrom", 5)>]
[<Fact>]
let ``Non deployer can transferFrom when approved and voting power is updated accordingly`` () =
    restore ()

    let toMint = bigint 20000
    let transferAmount = bigint 5000
    let zero = bigint 0
    
    let connection = ethConn.GetWeb3
    let gFryCon1 = Contracts.gFRYContract(connection)
    let gFryAddress = gFryCon1.Address
    let gFryCon2 = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let getVotesOfFunction = gFryCon2.GetFunction("getCurrentVotes")

    let approveInput = gFryCon1.approveTransactionInput(string hardhatAccount3, bigint (transferAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon1.Address
    approveInput.To <- gFryCon1.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let mintTx2 = gFryCon1.mint(hardhatAccount2,  toMint) // 20,000
    mintTx2 |> shouldSucceed

    let balanceBeforeTransferAccout2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceBeforeTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)

    let delegateInput = gFryCon1.delegateTransactionInput(string hardhatAccount3)
    delegateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon1.Address
    delegateInput.To <- gFryCon1.Address
    let delegateTxr = ethConn.MakeImpersonatedCallWithNoEther delegateInput

    let votesBeforeTransfer = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow

    let transferInput = gFryCon1.transferFromTransactionInput(string hardhatAccount2, string hardhatAccount3, bigint (transferAmount |> int))
    transferInput.From <- mapInlineDataArgumentToAddress hardhatAccount3 gFryCon1.Address
    transferInput.To <- gFryCon1.Address
    let transferFromTxr = ethConn.MakeImpersonatedCallWithNoEther transferInput

    let votesAfterTransfer = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow
        
    let balanceAfterTransferAccount2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceAfterTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)

    let allowanceAfter = gFryCon1.allowanceQuery(hardhatAccount2, hardhatAccount3)

    // STATE
    allowanceAfter
    |> should equal zero
    balanceAfterTransferAccount2 
    |> should equal (toMint - transferAmount)
    balanceAfterTransferAccount3
    |> should equal (balanceBeforeTransferAccount3 + transferAmount)
    votesBeforeTransfer
    |> should equal (toMint |> int)
    votesAfterTransfer
    |> should equal ((toMint - transferAmount) |> int)
    
    // EVENTS
    let event = (Contracts.gFRYContract.ApprovalEventDTO.DecodeAllEvents approveTxr) |> Seq.head
    event.owner |> should equal hardhatAccount2
    event.spender |> should equal hardhatAccount3
    event.amount |> should equal transferAmount

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents transferFromTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal hardhatAccount3
    event.amount |> should equal transferAmount

    let event = (Contracts.gFRYContract.DelegateVotesChangedEventDTO.DecodeAllEvents transferFromTxr) |> Seq.head
    event._delegate |> should equal hardhatAccount3
    event._previousBalance |> should equal balanceBeforeTransferAccout2
    event._newBalance |> should equal (toMint - transferAmount)
    
[<Specification("gFry", "transferFrom", 6)>]
[<Fact>]
let ``Deployer can transferFrom without approval and voting power is updated accordingly`` () =
    restore ()

    let toMint = bigint 20000
    let transferAmount = bigint 5000;
    let zero = bigint 0

    let connection = ethConn.GetWeb3
    let gFryCon1 = Contracts.gFRYContract(connection)
    let gFryAddress = gFryCon1.Address
    let gFryCon2 = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let getVotesOfFunction = gFryCon2.GetFunction("getCurrentVotes")

    let mintTx2 = gFryCon1.mint(hardhatAccount2,  toMint) // 20,000
    mintTx2 |> shouldSucceed

    let balanceBeforeTransferAccout2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceBeforeTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)

    let delegateInput = gFryCon1.delegateTransactionInput(string hardhatAccount3)
    delegateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon1.Address
    delegateInput.To <- gFryCon1.Address
    let delegateTxr = ethConn.MakeImpersonatedCallWithNoEther delegateInput

    let votesBeforeTransfer = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow

    let transferInput = gFryCon1.transferFromTransactionInput(string hardhatAccount2, string hardhatAccount3, bigint (transferAmount |> int))
    transferInput.From <- mapInlineDataArgumentToAddress hardhatAccount gFryCon1.Address
    transferInput.To <- gFryCon1.Address
    let transferFromTxr = ethConn.MakeImpersonatedCallWithNoEther transferInput

    let votesAfterTransfer = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow
        
    let balanceAfterTransferAccount2 = gFryCon1.balanceOfQuery(hardhatAccount2)
    let balanceAfterTransferAccount3 = gFryCon1.balanceOfQuery(hardhatAccount3)

    let allowanceAfter = gFryCon1.allowanceQuery(hardhatAccount2, hardhatAccount3)

    // STATE
    allowanceAfter
    |> should equal zero
    balanceAfterTransferAccount2 
    |> should equal (toMint - transferAmount)
    balanceAfterTransferAccount3
    |> should equal (balanceBeforeTransferAccount3 + transferAmount)
    votesBeforeTransfer
    |> should equal (toMint |> int)
    votesAfterTransfer
    |> should equal ((toMint - transferAmount) |> int)
    
    // EVENTS
    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents transferFromTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal hardhatAccount3
    event.amount |> should equal transferAmount

    let event = (Contracts.gFRYContract.DelegateVotesChangedEventDTO.DecodeAllEvents transferFromTxr) |> Seq.head
    event._delegate |> should equal hardhatAccount3
    event._previousBalance |> should equal balanceBeforeTransferAccout2
    event._newBalance |> should equal (toMint - transferAmount)


[<Specification("Governator", "constructor", 0)>]
[<Fact>]
let ``Constructor initiates with correct values`` () =
    restore ()

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)

    let gFryAddress = governatorCon.gFryQuery()
    let governatorFryAddress = governatorCon.FRYQuery()
    let fryAddress = fryCon.Address
    let addressLength = 42 

    // STATE
    governatorFryAddress 
    |> should equal fryAddress
    gFryAddress.Length
    |> should equal addressLength // Any address with correct length
    

[<Specification("Governator", "governate", 0)>]
[<Fact>]
let ``Can not mint gFry without giving governator Fry allowance`` () =
    restore ()

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    let gFryAddress = (governatorCon.gFryQuery())
    let gFryContract = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = gFryContract.GetFunction("balanceOf")
    let totalSupplyFunction = gFryContract.GetFunction("totalSupply")
    let amountOfFryToMint = bigint 1000
    let gFryBuyAmount = bigint 400
    let zero = 0

    fryCon.mint(hardhatAccount2, amountOfFryToMint)
    |> shouldSucceed
    fryCon.balanceOfQuery(hardhatAccount2)
    |> should equal amountOfFryToMint

    try
        let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
        governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
        governateInput.To <- governatorCon.Address
        let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

        failwith "Should not be able get gFry without giving allowance"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("transfer amount exceeds allowance")
        |> should equal true
   
    // STATE
    let balanceAfterTransfer = fryCon.balanceOfQuery(hardhatAccount2)
    balanceAfterTransfer |> should equal amountOfFryToMint
    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow
    gFryBalance |> should equal zero
    let gFryTotalSupply= totalSupplyFunction.CallAsync<int>() |> runNow
    gFryTotalSupply |> should equal zero


[<Specification("Governator", "governate", 1)>]
[<Fact>]
let ``Can not get gFry without having Fry`` () =
    restore ()

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    let gFryAddress = (governatorCon.gFryQuery())
    let gFryContract = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = gFryContract.GetFunction("balanceOf")
    let totalSupplyFunction = gFryContract.GetFunction("totalSupply")
    let gFryBuyAmount = bigint 400
    let zero = 0
    let zeroBigInt = bigint 0

    let approveInput = fryCon.approveTransactionInput(string governatorCon.Address, bigint (gFryBuyAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 fryCon.Address
    approveInput.To <- fryCon.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    try
        let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
        governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
        governateInput.To <- governatorCon.Address
        let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput
        
        failwith "Should not be able to get gFry without having Fry"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("transfer amount exceeds balance")
        |> should equal true

    // STATE
    let balanceAfterTransfer = fryCon.balanceOfQuery(hardhatAccount2)
    balanceAfterTransfer |> should equal zeroBigInt
    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow
    gFryBalance |> should equal zero
    let gFryTotalSupply= totalSupplyFunction.CallAsync<int>() |> runNow
    gFryTotalSupply |> should equal zero
    
[<Specification("Governator", "governate", 2)>]
[<Theory>]
[<InlineData(1000000, 1000)>]
[<InlineData(10, 0)>]
let ``Governator can accept FRY in exchange for gFry`` (amountToMint, amountToBuy) =
    restore ()

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    
    let amountOfFryToMint = amountToMint |> bigint
    let gFryBuyAmount = amountToBuy |> bigint
    let zero = bigint 0
    
    let gFryAddress = (governatorCon.gFryQuery())
    let gFryCon = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = gFryCon.GetFunction("balanceOf")
    let totalSupplyFunction = gFryCon.GetFunction("totalSupply")

    fryCon.mint(hardhatAccount2, amountOfFryToMint)
    |> shouldSucceed

    let approveInput = fryCon.approveTransactionInput(string governatorCon.Address, bigint (gFryBuyAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 fryCon.Address
    approveInput.To <- fryCon.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow

    let allowanceAfter = fryCon.allowanceQuery(hardhatAccount2, governatorCon.Address)

    // STATE
    allowanceAfter
    |> should equal zero
    gFryBalance |> should equal (gFryBuyAmount |> int)
    fryCon.balanceOfQuery(hardhatAccount2)
    |> should equal (amountOfFryToMint - gFryBuyAmount)
    fryCon.balanceOfQuery(governatorCon.Address)
    |> should equal gFryBuyAmount
    let gFryTotalSupply= totalSupplyFunction.CallAsync<int>() |> runNow
    (gFryTotalSupply|> bigint) |> should equal gFryBuyAmount 

    // EVENTS
    let event = (Contracts.gFRYContract.ApprovalEventDTO.DecodeAllEvents approveTxr) |> Seq.head
    event.owner |> should equal hardhatAccount2
    event.spender |> should equal governatorCon.Address
    event.amount |> should equal gFryBuyAmount

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents governateTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal governatorCon.Address
    event.amount |> should equal gFryBuyAmount

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents governateTxr) |> Seq.item(1)
    event.from |> should equal zeroAddress
    event._to |> should equal hardhatAccount2
    event.amount |> should equal gFryBuyAmount


[<Specification("Governator", "governate", 3)>]
[<Fact>]
let ``Governator can accept FRY in exchange for gFry and delegatee voting power is updated accordingly`` () =
    restore ()

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    let gFryAddress = (governatorCon.gFryQuery())
    let gFryCon = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let gFryCon2 = getGFryContract()
    let contract = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = contract.GetFunction("balanceOf")
    let totalSupplyFunction = gFryCon.GetFunction("totalSupply")
    let getVotesOfFunction = gFryCon.GetFunction("getCurrentVotes")

    let amountOfFryToMint = bigint 1000
    let gFryBuyAmount = bigint 400
    let zero = bigint 0
    
    fryCon.mint(hardhatAccount2, amountOfFryToMint)
    |> shouldSucceed

    let gFryAddress = (governatorCon.gFryQuery())

    let approveInput = fryCon.approveTransactionInput(string governatorCon.Address, bigint (gFryBuyAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 fryCon.Address
    approveInput.To <- fryCon.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let votesBeforeGovernate = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow

    let delegateInput = gFryCon2.delegateTransactionInput(string hardhatAccount3)
    delegateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon.Address
    delegateInput.To <- gFryCon.Address
    let delegateTxr = ethConn.MakeImpersonatedCallWithNoEther delegateInput

    let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

    let votesAfterGovernate = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow

    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow
    let gFryTotalSupply= totalSupplyFunction.CallAsync<int>() |> runNow

    let allowanceAfter = fryCon.allowanceQuery(hardhatAccount2, governatorCon.Address)

    // STATE
    allowanceAfter
    |> should equal zero
    votesAfterGovernate |> should equal (gFryBuyAmount |> int)
    gFryBalance |> should equal (gFryBuyAmount |> int)
    fryCon.balanceOfQuery(hardhatAccount2)
    |> should equal (amountOfFryToMint - gFryBuyAmount)
    fryCon.balanceOfQuery(governatorCon.Address)
    |> should equal gFryBuyAmount
    gFryTotalSupply |> should equal (gFryBuyAmount |> int)

    // EVENTS
    let event = (Contracts.gFRYContract.ApprovalEventDTO.DecodeAllEvents approveTxr) |> Seq.head
    event.owner |> should equal hardhatAccount2
    event.spender |> should equal governatorCon.Address
    event.amount |> should equal gFryBuyAmount

    let event = (Contracts.FRYContract.TransferEventDTO.DecodeAllEvents governateTxr) |> Seq.item(0)
    event.from |> should equal hardhatAccount2
    event._to |> should equal governatorCon.Address
    event.value |> should equal gFryBuyAmount

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents governateTxr) |> Seq.item(1)
    event.from |> should equal zeroAddress
    event._to |> should equal hardhatAccount2
    event.amount |> should equal gFryBuyAmount

    let event = (Contracts.gFRYContract.DelegateVotesChangedEventDTO.DecodeAllEvents governateTxr) |> Seq.head
    event._delegate |> should equal hardhatAccount3
    event._previousBalance |> should equal (votesBeforeGovernate |> bigint)
    event._newBalance |> should equal (votesAfterGovernate |> bigint)


[<Specification("Governator", "degovernate", 0)>]
[<Fact>]
let ``Governator can not degovernate if user does not have sufficient gFry balance`` () =
    restore ()

    // First give the account some gFry

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    let amountOfFryToMint = bigint 1000
    let gFryBuyAmount = bigint 400
    let zero = bigint 0

    fryCon.mint(hardhatAccount2, amountOfFryToMint)
    |> shouldSucceed

    let gFryAddress = (governatorCon.gFryQuery())

    let approveInput = fryCon.approveTransactionInput(string governatorCon.Address, bigint (gFryBuyAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 fryCon.Address
    approveInput.To <- fryCon.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

    let gFry = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = gFry.GetFunction("balanceOf")
    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow

    gFryBalance |> should equal (gFryBuyAmount |> int)
    let accFryBalance = fryCon.balanceOfQuery(hardhatAccount2)
    accFryBalance |> should equal (amountOfFryToMint - gFryBuyAmount)
    let govFryBalance = fryCon.balanceOfQuery(governatorCon.Address)
    govFryBalance |> should equal gFryBuyAmount

    // Now degovernate

    let amountOfgFryToDegovernate = bigint 500 // This is more than the user has

    try
        let governateInput = governatorCon.degovernateTransactionInput(bigint (amountOfgFryToDegovernate |> int))
        governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
        governateInput.To <- governatorCon.Address
        let degovernateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput
        
        failwith "Should not be able to degovernate without suffienct gFry balance"
    with ex ->
        // RETURNS
        ex.Message.ToLowerInvariant().Contains("transfer amount exceeds balance")
        |> should equal true
    
    // STATE
    let gFryBalanceAfterDegovernate = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow
    gFryBalanceAfterDegovernate |> should equal ((gFryBalance) |> int)
    fryCon.balanceOfQuery(hardhatAccount2)
    |> should equal (accFryBalance)


[<Specification("Governator", "degovernate", 1)>]
[<Theory>]
[<InlineData(1000000, 1000)>]
[<InlineData(10, 0)>]
let ``Governator can accept gFry in exchange for Fry`` (gFryToMint, degovAmount) =
    restore ()

    // First give the account some gFry

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    let gFryAddress = (governatorCon.gFryQuery())
    let gFryCon = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let totalSupplyFunction = gFryCon.GetFunction("totalSupply")
    let allowanceFunction = gFryCon.GetFunction("allowance")
    let amountOfFryToMint = gFryToMint |> bigint
    let gFryBuyAmount = gFryToMint |> bigint
    let zero = bigint 0

    fryCon.mint(hardhatAccount2, amountOfFryToMint)
    |> shouldSucceed

    let approveInput = fryCon.approveTransactionInput(string governatorCon.Address, bigint (gFryBuyAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 fryCon.Address
    approveInput.To <- fryCon.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

    let gFry = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = gFry.GetFunction("balanceOf")
    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow

    gFryBalance |> should equal (gFryBuyAmount |> int)
    let accFryBalance = fryCon.balanceOfQuery(hardhatAccount2)
    accFryBalance |> should equal (amountOfFryToMint - gFryBuyAmount)
    let govFryBalance = fryCon.balanceOfQuery(governatorCon.Address)
    govFryBalance |> should equal gFryBuyAmount

    // Now degovernate

    let amountOfgFryToDegovernate = degovAmount |> bigint

    let governateInput = governatorCon.degovernateTransactionInput(bigint (amountOfgFryToDegovernate |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let degovernateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput
    
    let gFryBalanceAfterDegovernate = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow
    let gFryTotalSupplyAfter = totalSupplyFunction.CallAsync<int>() |> runNow

    // STATE
    gFryBalanceAfterDegovernate |> should equal ((gFryBuyAmount - amountOfgFryToDegovernate) |> int)
    fryCon.balanceOfQuery(hardhatAccount2)
    |> should equal (accFryBalance + amountOfgFryToDegovernate)
    gFryTotalSupplyAfter |> should equal ((gFryBuyAmount - amountOfgFryToDegovernate) |> int)

    // EVENTS
    let event = (Contracts.FRYContract.TransferEventDTO.DecodeAllEvents degovernateTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal governatorCon.Address
    event.value |> should equal amountOfgFryToDegovernate

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents degovernateTxr) |> Seq.item(1)
    event.from |> should equal governatorCon.Address
    event._to |> should equal zeroAddress
    event.amount |> should equal amountOfgFryToDegovernate


[<Specification("Governator", "degovernate", 2)>]
[<Fact>]
let ``Governator can accept gFry in exchange for Fry and delegatee voting power is updated accordingly`` () =
    restore ()

    // First give the account some gFry

    let connection = ethConn.GetWeb3
    let fryCon = Contracts.FRYContract(connection)
    
    let governatorCon = Contracts.GovernatorContract(connection, fryCon.Address)
    let gFryAddress = (governatorCon.gFryQuery())
    let gFryCon = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let gFryCon2 = getGFryContract()
    let totalSupplyFunction = gFryCon.GetFunction("totalSupply")
    let amountOfFryToMint = bigint 1000
    let gFryBuyAmount = bigint 400
    let zero = bigint 0

    fryCon.mint(hardhatAccount2, amountOfFryToMint)
    |> shouldSucceed

    let approveInput = fryCon.approveTransactionInput(string governatorCon.Address, bigint (gFryBuyAmount |> int))
    approveInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 fryCon.Address
    approveInput.To <- fryCon.Address
    let approveTxr = ethConn.MakeImpersonatedCallWithNoEther approveInput

    let governateInput = governatorCon.governateTransactionInput(bigint (gFryBuyAmount |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let governateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

    let gFry = ethConn.Web3.Eth.GetContract(gFryAbiString, gFryAddress)
    let balanceOfFunction = gFry.GetFunction("balanceOf")
    let gFryBalance = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow

    gFryBalance |> should equal (gFryBuyAmount |> int)
    let accFryBalance = fryCon.balanceOfQuery(hardhatAccount2)
    accFryBalance |> should equal (amountOfFryToMint - gFryBuyAmount)
    let govFryBalance = fryCon.balanceOfQuery(governatorCon.Address)
    govFryBalance |> should equal gFryBuyAmount

    // Delegate
    
    let getVotesOfFunction = gFryCon.GetFunction("getCurrentVotes")
    let currentVotesAccount3 = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow

    let delegateInput = gFryCon2.delegateTransactionInput(string hardhatAccount3)
    delegateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 gFryCon.Address
    delegateInput.To <- gFryCon.Address
    let delegateTxr = ethConn.MakeImpersonatedCallWithNoEther delegateInput

    let currentVotesAccount3BeforeDegov = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow

    // Now Degovernate

    let amountOfgFryToDegovernate = bigint 150
    
    let governateInput = governatorCon.degovernateTransactionInput(bigint (amountOfgFryToDegovernate |> int))
    governateInput.From <- mapInlineDataArgumentToAddress hardhatAccount2 governatorCon.Address
    governateInput.To <- governatorCon.Address
    let degovernateTxr = ethConn.MakeImpersonatedCallWithNoEther governateInput

    // STATE
    let gFryBalanceAfterDegovernate = balanceOfFunction.CallAsync<int>(hardhatAccount2) |> runNow
    gFryBalanceAfterDegovernate |> should equal ((gFryBuyAmount - amountOfgFryToDegovernate) |> int)
    let currentVotesAccount3 = getVotesOfFunction.CallAsync<int>(hardhatAccount3) |> runNow
    currentVotesAccount3 |> should equal (currentVotesAccount3BeforeDegov - (amountOfgFryToDegovernate |> int))
    let gFryTotalSupplyAfter = totalSupplyFunction.CallAsync<int>() |> runNow
    gFryTotalSupplyAfter |> should equal ((gFryBuyAmount - amountOfgFryToDegovernate) |> int)
    fryCon.balanceOfQuery(hardhatAccount2)
    |> should equal (accFryBalance + amountOfgFryToDegovernate)

    // EVENTS
    let event = (Contracts.FRYContract.TransferEventDTO.DecodeAllEvents degovernateTxr) |> Seq.head
    event.from |> should equal hardhatAccount2
    event._to |> should equal governatorCon.Address
    event.value |> should equal amountOfgFryToDegovernate

    let event = (Contracts.gFRYContract.TransferEventDTO.DecodeAllEvents degovernateTxr) |> Seq.item(1)
    event.from |> should equal governatorCon.Address
    event._to |> should equal zeroAddress
    event.amount |> should equal amountOfgFryToDegovernate

    let event = (Contracts.gFRYContract.DelegateVotesChangedEventDTO.DecodeAllEvents degovernateTxr) |> Seq.head
    event._delegate |> should equal hardhatAccount3
    event._previousBalance |> should equal (currentVotesAccount3BeforeDegov |> bigint)
    event._newBalance |> should equal ((currentVotesAccount3BeforeDegov - (amountOfgFryToDegovernate |> int))|> bigint)
