module GovernanceTestBase

open TestBase
open Nethereum.Util
open System.Numerics
open SolidityTypes

module Array = 
    let removeFromEnd elem = Array.rev >> Array.skipWhile (fun i -> i = elem) >> Array.rev

let ERC20_ABI = @"[{""constant"":false,""inputs"":[{""name"":""_spender"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":""success"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""totalSupply"",""outputs"":[{""name"":""supply"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_from"",""type"":""address""},{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""name"":""success"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""balance"",""type"":""uint256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":""success"",""type"":""bool""}],""type"":""function""},{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""},{""name"":""_spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":""remaining"",""type"":""uint256""}],""type"":""function""},{""inputs"":[{""name"":""_initialAmount"",""type"":""uint256""}],""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""_from"",""type"":""address""},{""indexed"":true,""name"":""_to"",""type"":""address""},{""indexed"":false,""name"":""_value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""_owner"",""type"":""address""},{""indexed"":true,""name"":""_spender"",""type"":""address""},{""indexed"":false,""name"":""_value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""}]";

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