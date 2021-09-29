pragma solidity ^0.5.0;

import "./Comp.sol";
import "hardhat/console.sol";

contract gFRY is Comp 
{
    address public governator;
    uint public totalSupply  = 10000000e18;

    constructor() 
        public 
        Comp(msg.sender)
    {
        governator = msg.sender;
        burn(balances[msg.sender]); // TODO : update spec doc
        require(totalSupply == 0, "no tokens should exist"); // TODO : update spec doc
    }

    function mint(address _to, uint96 _amount) 
        public 
    {
        require(msg.sender == governator, "Comp::_mint: That account cannot mint");
        require(_to != address(0), "Comp::_mint: cannot mint to the zero address");
        
        balances[_to] = add96(balances[_to], _amount, "Comp::_mint: user balance overflows");
        totalSupply = add96(uint96(totalSupply), _amount, "Comp::_mint: totalSupply overflows");
        emit Transfer(address(0x0), _to, _amount);

        _moveDelegates(delegates[address(0x0)], delegates[_to], _amount); // Review this was originally _moveDelegates(address(0x0), to, amount);
    }

    function burn(uint96 _amount) 
        public 
    {
        require(msg.sender != address(0), "Comp::_burn: cannot burn from the zero address");

        balances[msg.sender] = sub96(balances[msg.sender], _amount, "Comp::_burn: burn underflows");
        totalSupply = sub96(uint96(totalSupply), _amount, "Comp::_burn: totalSupply underflows"); // TODO : update spec doc
        emit Transfer(msg.sender, address(0), _amount);

        _moveDelegates(delegates[msg.sender], delegates[address(0)], _amount); // Review this was originally _moveDelegates(msg.sender, address(0), amount);
    }

    function transferFrom(address _src, address _dst, uint _rawAmount) 
        external 
        returns (bool) 
    {
        address spender = msg.sender;
        // Only alteration from original Comp contract
        uint96 spenderAllowance = msg.sender == governator ? uint96(-1) : allowances[_src][spender];
        uint96 amount = safe96(_rawAmount, "Comp::approve: amount exceeds 96 bits");

        if (spender != _src && spenderAllowance != uint96(-1)) {
            uint96 newAllowance = sub96(spenderAllowance, amount, "Comp::transferFrom: transfer amount exceeds spender allowance");
            allowances[_src][spender] = newAllowance;

            emit Approval(_src, spender, newAllowance);
        }

        _transferTokens(_src, _dst, amount);
        return true;
    }
}
