pragma solidity ^0.5.0;

import "./Comp.sol";

contract gFRY is Comp 
{
    address public governator;
    uint public totalSupply;

    constructor() 
        public 
        Comp(address(this))
    {
        governator = msg.sender;
        totalSupply = 0;
    }

    function mint(address to, uint96 amount) 
        public 
    {
        require(msg.sender == governator, "Comp::_mint: That account cannot mint");
        require(to != address(0), "Comp::_mint: cannot mint to the zero address");
        
        balances[to] = add96(balances[to], amount, "Comp::_mint: user balance overflows");
        totalSupply = add96(uint96(totalSupply), amount, "Comp::_mint: totalSupply overflows");
        emit Transfer(address(0x0), to, amount);

        _moveDelegates(address(0x0), to, amount);
    }

    function burn(uint96 amount) 
        public 
    {
        require(msg.sender != address(0), "Comp::_burn: cannot burn from the zero address");

        balances[msg.sender] = sub96(balances[msg.sender], amount, "Comp::_burn: burn underflows");
        emit Transfer(msg.sender, address(0x0), amount);

        _moveDelegates(msg.sender, address(0x0), amount);
    }

    function transferFrom(address src, address dst, uint rawAmount) 
        external 
        returns (bool) 
    {
        address spender = msg.sender;
        // Only alteration from original Comp contract
        uint96 spenderAllowance = msg.sender == governator ? uint96(-1) : allowances[src][spender];
        uint96 amount = safe96(rawAmount, "Comp::approve: amount exceeds 96 bits");

        if (spender != src && spenderAllowance != uint96(-1)) {
            uint96 newAllowance = sub96(spenderAllowance, amount, "Comp::transferFrom: transfer amount exceeds spender allowance");
            allowances[src][spender] = newAllowance;

            emit Approval(src, spender, newAllowance);
        }

        _transferTokens(src, dst, amount);
        return true;
    }
}
