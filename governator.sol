pragma solidity ^0.5.0;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";

// TODO : modify COMP to have a minter rights which are set to this contract
// TODO : add special approval rights to this contract so that gFRY doesn't need to be approved here

contract Govanator
{
    using SafeMath for uint;

    IERC20 FRY;
    COMP gFRY;

    // Mint gFRY in exchange for FRY
    function governate(uint _amount) 
        public 
    {
        FRY.transferFrom(msg.sender, _amount);
        gFRY.mint(msg.sender, _amount);
    }

    // Redeem gFRY in exchange for the share of FRY available
    function degovernate(uint _amount)
    {
        uint share = _amount.mul(10**18).div(gFRY.totalSupply());
        uint fryToReturn = FRY.balanceOf(address(this))
            .mul(share)
            .div(10**18);
    
        // this will be universal approval for this contract
        gFRY.transferFrom(msg.sender, address(this), _amount);
        gFRY.burn(_amount);
        FRY.transfer(msg.sender, fryToReturn);
    }
}