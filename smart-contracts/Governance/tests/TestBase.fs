module TestBase

open System
open System.Numerics
open System.IO
open System.Text
open System.Threading.Tasks
open Nethereum.Web3
open Nethereum.Web3.Accounts
open Nethereum.Util
open Nethereum.Contracts
open Nethereum.Hex.HexConvertors.Extensions
open Nethereum.RPC.Eth.DTOs
open Nethereum.RPC.Infrastructure
open Nethereum.Hex.HexTypes
open Nethereum.JsonRpc.Client
open Nethereum.Contracts.ContractHandlers
open FsUnit.Xunit
open Microsoft.FSharp.Control
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Constants
open Microsoft.Extensions.Configuration
open SolidityTypes

module Array =
    let ensureSize size array =
        let paddingArray = Array.init size (fun _ -> byte 0)
        Array.concat [|array;paddingArray|] |> Array.take size

type GanacheEvmSnapshot(client) = 
    inherit GenericRpcRequestResponseHandlerNoParam<string>(client, "evm_snapshot")

type HardhatForkInput() =
    [<JsonProperty(PropertyName = "jsonRpcUrl")>]
    member val JsonRpcUrl = "" with get, set
    [<JsonProperty(PropertyName = "blockNumber")>]
    member val BlockNumber = 0UL with get, set

type HardhatResetInput() =
    [<JsonProperty(PropertyName = "forking")>]
    member val Forking = HardhatForkInput() with get, set

type HardhatReset(client) = 
    inherit RpcRequestResponseHandler<bool>(client, "hardhat_reset")

    member __.SendRequestAsync (input:HardhatResetInput) (id:obj) = base.SendRequestAsync(id, input);

let rnd = Random()

let bigint (x:int) = bigint(x)

let rec rndRange min max  = 
    seq { 
        yield rnd.Next(min,max) |> BigInteger
        yield! rndRange min max
        }

let bigInt (value: uint64) = BigInteger(value)
let hexBigInt (value: uint64) = HexBigInteger(bigInt value)

let inline runNow task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

let inline runNowWithoutResult (task:Task) =
    task |> Async.AwaitTask |> Async.RunSynchronously

type EthereumConnection(nodeURI: string, privKey: string) =
    
    // this is needed to reset nonce.
    let getWeb3Unsigned () = (Web3(nodeURI))
    let getWeb3 () = Web3(Account(privKey), nodeURI)
    
    let mutable web3Unsigned = getWeb3Unsigned ()
    let mutable web3 = getWeb3 ()
    
    member val public Gas = hexBigInt 9500000UL
    member val public GasPrice = hexBigInt 8000000000UL
    member this.Account with get() = web3.TransactionManager.Account
    member this.Web3 with get() = web3
    member this.Web3Unsigned with get() = web3Unsigned
    member this.GetWeb3() = web3
    member this.GetWeb3Unsigned() = web3Unsigned

    member this.TimeTravel seconds =
        this.Web3.Client.SendRequestAsync(method = "evm_increaseTime", paramList = [| seconds |]) 
        |> Async.AwaitTask
        |> Async.RunSynchronously
        this.Web3.Client.SendRequestAsync(method = "evm_mine", paramList = [||]) 
        |> Async.AwaitTask
        |> Async.RunSynchronously

    member this.GetEtherBalance address = 
        let hexBigIntResult = this.Web3.Eth.GetBalance.SendRequestAsync(address) |> runNow
        hexBigIntResult.Value

    member this.SendEtherAsync address (amount:BigInteger) =
        let transactionInput =
            TransactionInput
                ("", address, this.Account.Address, hexBigInt 9500000UL, hexBigInt 1000000000UL, HexBigInteger(amount))
        this.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput, null)

    member this.SendEther address amount =
        this.SendEtherAsync address amount |> runNow

    member this.ImpersonateAccount (address:string) =
        this.Web3.Client.SendRequestAsync(RpcRequest(0, "hardhat_impersonateAccount", address)) |> runNowWithoutResult

    member this.MakeImpersonatedCallAsync weiValue gasLimit gasPrice addressFrom addressTo (functionArgs:#FunctionMessage) =
        this.ImpersonateAccount addressFrom

        let txInput = functionArgs.CreateTransactionInput(addressTo)
        
        txInput.From <- addressFrom
        txInput.Gas <- gasLimit
        txInput.GasPrice <- gasPrice
        txInput.Value <- weiValue

        this.Web3Unsigned.TransactionManager.SendTransactionAndWaitForReceiptAsync(txInput, tokenSource = null)
       
    member this.MakeImpersonatedCallWithNoEtherAsync addressFrom addressTo (functionArgs:#FunctionMessage) = this.MakeImpersonatedCallAsync (hexBigInt 0UL) (hexBigInt 9500000UL) (hexBigInt 0UL) addressFrom addressTo functionArgs
    
    member this.MakeImpersonatedCallWithNoEther addressFrom addressTo (functionArgs:#FunctionMessage) = this.MakeImpersonatedCallWithNoEtherAsync addressFrom addressTo functionArgs |> runNow

    member this.MakeSnapshotAsync () = GanacheEvmSnapshot(this.Web3.Client).SendRequestAsync()
    
    member this.MakeSnapshot = this.MakeSnapshotAsync >> runNow

    member this.RestoreSnapshot snapshotID =
        this.Web3.Client.SendRequestAsync(RpcRequest(1, "evm_revert", [|snapshotID|])) |> runNowWithoutResult
        web3 <- getWeb3()
        web3Unsigned <- getWeb3Unsigned()

    member this.HardhatResetAsync blockNumber url =
        let input = HardhatResetInput(Forking=HardhatForkInput(BlockNumber=blockNumber,JsonRpcUrl=url))
        HardhatReset(this.Web3.Client).SendRequestAsync input None


type Profile = { FunctionName: string; Duration: string }

let profileMe f =
    let start = DateTime.Now
    let result = f()
    let duration = DateTime.Now - start
    (f.GetType(), duration) |> printf "(Function, Duration) = %A\n"
    result

[<AttributeUsage(AttributeTargets.Method, AllowMultiple = true)>]
type SpecificationAttribute(contractName, functionName, specCode) =
    inherit Attribute()
    member _.ContractName: string = contractName
    member _.FunctionName: string = functionName
    member _.SpecCode: int = specCode

let useRinkeby = false
let hardhatURI = "http://localhost:8545"
let rinkebyURI = "https://rinkeby.infura.io/v3/c48bc466281c4fefb3decad63c4fc815"
let ganacheMnemonic = "join topple vapor pepper sell enter isolate pact syrup shoulder route token"
let hardhatPrivKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
let hardhatPrivKey2 = "59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d"
let hardhatAccount = "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266"
let hardhatAccount2 = "0x70997970c51812dc3a010c7d01b50e0d17dc79c8"
let hardhatAccount3 = "0x90f79bf6eb2c4f870365e785982e1f101e93b906"
let rinkebyPrivKey = "5ca35a65adbd49af639a3686d7d438dba1bcef97cf1593cd5dd8fd79ca89fa3c"
let blockNumber = 12330245UL
let zeroAddress = "0x0000000000000000000000000000000000000000"

let isRinkeby rinkeby notRinkeby =
    match useRinkeby with
    | true -> rinkeby
    | false -> notRinkeby

let ethConn =
    isRinkeby (EthereumConnection(rinkebyURI, rinkebyPrivKey)) (EthereumConnection(hardhatURI, hardhatPrivKey))

let ethConnWithPrivKey2 =
    isRinkeby (EthereumConnection(rinkebyURI, rinkebyPrivKey)) (EthereumConnection(hardhatURI, hardhatPrivKey2))

let shouldEqualIgnoringCase (a: string) (b: string) =
    let aString = a |> string
    let bString = b |> string
    should equal (aString.ToLower()) (bString.ToLower())

let shouldEqualInt (a: bigint) (b: bigint) =
    let aInt = a 
    let bInt = b 
    should equal aInt bInt

let shouldSucceed (txr: TransactionReceipt) = txr.Status |> should equal (hexBigInt 1UL)
let shouldFail (txr: TransactionReceipt) = txr.Status |> should equal (hexBigInt 0UL)

let decodeEvents<'a when 'a: (new: unit -> 'a)> (receipt: TransactionReceipt) =
    receipt.DecodeAllEvents<'a>() |> Seq.map (fun e -> e.Event)

let decodeFirstEvent<'a when 'a: (new: unit -> 'a)> (receipt: TransactionReceipt) =
    decodeEvents<'a> receipt |> Seq.head

let makeAccount() =
    let ecKey = Nethereum.Signer.EthECKey.GenerateKey();
    let privateKey = ecKey.GetPrivateKeyAsBytes().ToHex();
    Account(privateKey);

let makeAccountWithBalance () =
    let account = makeAccount()
    
    ethConn.GasPrice.Value * ethConn.Gas.Value * bigint 2
    |> ethConn.SendEther account.Address
    |> shouldSucceed

    account

let padAddress (address:string) = 
    let addressWithout0x = address.Remove(0, 2)
    let bytesToPad = (32 - addressWithout0x.Length / 2)
    
    (Array.replicate (bytesToPad * 2) '0' |> String) + addressWithout0x

let strToByte32 (str:string) = System.Text.Encoding.UTF8.GetBytes(str) |> Array.ensureSize 32

let bigintToByte size (a:BigInteger) = 
    let bytes = a.ToByteArray()
    bytes |> Array.ensureSize size |> Array.rev

let doTimes x action = 
    for _ in 1..x do
        action ()

let inline toBigDecimal (x:BigInteger) : BigDecimal = BigDecimal(x, 0);
let inline toBigInt (x:BigDecimal) = x.Mantissa / BigInteger.Pow(bigint 10, -x.Exponent)

let bigintDifference a b (precision:int) =
    Math.Round(decimal <| toBigDecimal a / toBigDecimal b, precision)

let toE18 (v:float) = 
    BigDecimal(decimal v) * (toBigDecimal E18) |> toBigInt

// reset the state to a particular block every time we start the tests to avoid having different state on different runs
let alchemyKey = ConfigurationBuilder().AddUserSecrets<HardhatForkInput>().Build().["AlchemyKey"]
ethConn.HardhatResetAsync blockNumber (sprintf "https://eth-mainnet.alchemyapi.io/v2/%s" alchemyKey)
|> runNow
|> should equal true

type Debug(ethConn: EthereumConnection) =
    member val public EthConn = ethConn
    member val public  DebugContract = Contracts.DebugContract(ethConn.GetWeb3)

    member this.Forward(toAddress, data:string) =
        this.DebugContract.forward(toAddress, data.HexToByteArray())

    member this.DecodeForwardedEvents(receipt: TransactionReceipt) =
        Contracts.DebugContract.ForwardedEventDTO.DecodeAllEvents receipt
        

    member this.BlockTimestamp:BigInteger = 
        this.DebugContract.blockTimestampQuery()
        
type Contracts.DebugContract.ForwardedEventDTO with
    member this.ResultAsRevertMessage =
        match this._success with
        | true -> None
        | _ -> Some(Encoding.ASCII.GetString(this._resultData))
      
let shouldRevertWithMessage expectedMessage (forwardedEvent: Contracts.DebugContract.ForwardedEventDTO) =
    //printf "EVENT \n"
    //printfn "%O" forwardedEvent.ResultAsRevertMessage
    match forwardedEvent.ResultAsRevertMessage with
    | None -> failwith "not a revert message"
    | Some actualMessage -> actualMessage |> should haveSubstring expectedMessage

let shouldRevertWithUnknownMessage (forwardedEvent: Contracts.DebugContract.ForwardedEventDTO) =
    shouldRevertWithMessage "" forwardedEvent

// type IAsyncTxSender =
//     abstract member SendTxAsync : string -> BigInteger -> string -> Task<TransactionReceipt>
    
// type Abi(filename) =
//     member val JsonString = File.OpenText(filename).ReadToEnd()
//     member this.AbiString = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("abi").ToString()
//     member this.Bytecode = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("bytecode").ToString()

// type ContractPlug(ethConn: EthereumConnection, abi: Abi, address) =
//     member val public Address = address

//     member val public Contract = 
//         ethConn.Web3.Eth.GetContract(abi.AbiString, address)

//     member this.Function functionName = 
//         this.Contract.GetFunction(functionName)

//     member this.QueryObjAsync<'a when 'a: (new: unit -> 'a)> functionName arguments = 
//         (this.Function functionName).CallDeserializingToObjectAsync<'a> (arguments)

//     member this.QueryObj<'a when 'a: (new: unit -> 'a)> functionName arguments = 
//         this.QueryObjAsync<'a> functionName arguments |> runNow

//     member this.QueryAsync<'a> functionName arguments = 
//         (this.Function functionName).CallAsync<'a> (arguments)

//     member this.Query<'a> functionName arguments = 
//         this.QueryAsync<'a> functionName arguments |> runNow

//     member this.FunctionData functionName arguments = 
//         (this.Function functionName).GetData(arguments)

//     member this.ExecuteFunctionFromAsyncWithValue value functionName arguments (connection:IAsyncTxSender) = 
//         this.FunctionData functionName arguments |> connection.SendTxAsync this.Address value

//     member this.ExecuteFunctionFromAsync = this.ExecuteFunctionFromAsyncWithValue (BigInteger(0))

//     member this.ExecuteFunctionFrom functionName arguments connection = 
//         this.ExecuteFunctionFromAsync functionName arguments connection |> runNow

//     member this.ExecuteFunctionAsync functionName arguments = 
//         this.ExecuteFunctionFromAsync functionName arguments (upcast ethConn)

//     member this.ExecuteFunction functionName arguments = 
//         this.ExecuteFunctionAsync functionName arguments |> runNow