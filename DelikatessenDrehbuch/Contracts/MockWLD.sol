// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

/// @title Mock WLD token for testnet / local testing
/// @notice Simple ERC20 with public faucet mint for development use only.
contract MockWLD {
    string public name = "Mock Worldcoin";
    string public symbol = "mWLD";
    uint8 public decimals = 18;

    uint256 public totalSupply;

    mapping(address => uint256) public balanceOf;
    mapping(address => mapping(address => uint256)) public allowance;

    event Transfer(address indexed from, address indexed to, uint256 value);
    event Approval(address indexed owner, address indexed spender, uint256 value);

    function transfer(address to, uint256 value) external returns (bool) {
        _transfer(msg.sender, to, value);
        return true;
    }

    function approve(address spender, uint256 value) external returns (bool) {
        allowance[msg.sender][spender] = value;
        emit Approval(msg.sender, spender, value);
        return true;
    }

    function transferFrom(address from, address to, uint256 value) external returns (bool) {
        uint256 currentAllowance = allowance[from][msg.sender];
        require(currentAllowance >= value, "allowance too low");

        unchecked {
            allowance[from][msg.sender] = currentAllowance - value;
        }

        _transfer(from, to, value);
        return true;
    }

    /// @dev Test faucet: anyone can mint test tokens.
    function faucetMint(uint256 value) external returns (bool) {
        _mint(msg.sender, value);
        return true;
    }

    function _transfer(address from, address to, uint256 value) internal {
        require(to != address(0), "zero address");

        uint256 bal = balanceOf[from];
        require(bal >= value, "balance too low");

        unchecked {
            balanceOf[from] = bal - value;
            balanceOf[to] += value;
        }

        emit Transfer(from, to, value);
    }

    function _mint(address to, uint256 value) internal {
        require(to != address(0), "zero address");

        totalSupply += value;
        balanceOf[to] += value;

        emit Transfer(address(0), to, value);
    }
}
