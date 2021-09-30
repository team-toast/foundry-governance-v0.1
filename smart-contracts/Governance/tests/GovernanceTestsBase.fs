module GovernanceTestBase

open TestBase
open Nethereum.Util
open System.Numerics
open SolidityTypes
open FSharp.Data
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Text
open System.IO

module Array = 
    let removeFromEnd elem = Array.rev >> Array.skipWhile (fun i -> i = elem) >> Array.rev

let baseDirectory = __SOURCE_DIRECTORY__
let baseDirectory' = Directory.GetParent(baseDirectory)
let gFryFilePath = @"../Governance/build/contracts/gFRY.json"
let fullgFryPath = Path.Combine(baseDirectory'.FullName, gFryFilePath)
let jsonGFryString = File.OpenText(fullgFryPath).ReadToEnd()
let gFryAbiString = JsonConvert.DeserializeObject<JObject>(jsonGFryString).GetValue("abi").ToString()

let getGFryContract() =
    let contract = Contracts.gFRYContract(ethConn.GetWeb3)
    contract

let getGovernatorContract(gFryAddress) =
    let contract = Contracts.GovernatorContract(ethConn.GetWeb3, gFryAddress)
    contract

// note: this is used to be able to specify owner and contract addresses in inlinedata (we cannot use DUs in attributes)
let mapInlineDataArgumentToAddress inlineDataArgument calledContractAddress =
    match inlineDataArgument with
      | "owner" -> ethConn.Account.Address // we assume that the called contract is "owned" by our connection
      | "contract" -> calledContractAddress
      | _ -> inlineDataArgument

// this is a mechanism of being able to revert to the same snapshot over and over again.
// when we call restore, the snapshot we restore to gets deleted. So we need to create a new one immediatelly after that.
// this is put in this module because we need to get snapshot at the point when every static state in this module is initialized
let mutable snapshotId = ethConn.MakeSnapshot()
let restore () =
    ethConn.RestoreSnapshot snapshotId
    snapshotId <- ethConn.MakeSnapshot ()