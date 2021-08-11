pragma solidity ^0.5.0;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "@openzeppelin/contracts/math/SafeMath.sol";
import "./gFry.sol";

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
        gFry.mint(msg.sender, uint96(_amount));
    }

    // Redeem gFry in exchange for the share of FRY available
    function degovernate(uint _amount)
        public
    {
        uint share = _amount.mul(10**18).div(gFry.totalSupply());
        uint fryToReturn = FRY.balanceOf(address(this))
            .mul(share)
            .div(10**18);
    
        // this will be universal approval for this contract
        gFry.transferFrom(msg.sender, address(this), uint96(_amount));
        gFry.burn(uint96(_amount));
        FRY.transfer(msg.sender, fryToReturn);
    }
}