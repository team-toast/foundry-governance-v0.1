pragma solidity ^0.5.0;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "@openzeppelin/contracts/math/SafeMath.sol";
import "./gFry.sol";
import "hardhat/console.sol";

// TODO : modify COMP to have a minter rights which are set to this contract
// TODO : add special approval rights to this contract so that gFRY doesn't need to be approved here

contract Governator
{
    using SafeMath for uint;

    IERC20 public FRY;
    gFRY public gFry;

    constructor(IERC20 _FRY) 
        public 
    {
        gFry = new gFRY();
        FRY = _FRY;
    }

    // Mint gFRY in exchange for FRY
    function governate(uint _amount) 
        public 
    {
        FRY.transferFrom(msg.sender, address(this), _amount);
        gFry.mint(msg.sender, safe96(_amount, "Governator: uint96 overflows"));
    }

    // Redeem gFry in exchange for the share of FRY available
    function degovernate(uint _amount)
        public
    {
        console.log("Debug Info 1: ");
        console.log("Amount to degovernate: ", _amount);
        console.log("Total gFry supply: ", gFry.totalSupply());

        uint share = _amount.mul(10**18).div(gFry.totalSupply());

        console.log("share: ", share);

        uint fryToReturn = FRY.balanceOf(address(this))
            .mul(share)
            .div(10**18);

        console.log("fryToReturn: ", fryToReturn);
        // this will be universal approval for this contract
        console.log("Test 1: ");

        gFry.transferFrom(msg.sender, address(this), _amount);

        console.log("Test 2: ");

        gFry.burn(safe96(_amount, "Governator: uint96 overflows"));

        console.log("Test 3: ");

        FRY.transfer(msg.sender, fryToReturn);
        
        console.log("Test 4: ");
    }

    function safe96(uint n, string memory errorMessage) internal pure returns (uint96) {
        require(n < 2**96, errorMessage);
        return uint96(n);
    }
}