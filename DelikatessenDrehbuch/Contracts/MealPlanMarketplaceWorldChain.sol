// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

interface IERC20 {
    function transferFrom(address from, address to, uint256 value) external returns (bool);
}

/// @title MealPlan Marketplace (World Chain)
/// @notice Minimales Beispiel für WLD-basierte Zahlungen.
/// @dev Dieses Contract-Pattern kann mit Access Control / Signaturen erweitert werden.
contract MealPlanMarketplaceWorldChain {
    IERC20 public immutable wldToken;
    address public treasury;

    mapping(uint256 => uint256) public listingPrice;
    mapping(uint256 => bool) public listingActive;

    event ListingConfigured(uint256 indexed listingId, uint256 price, bool active);
    event ListingPurchased(uint256 indexed listingId, address indexed buyer, uint256 amount, string buyerHash);
    event TreasuryUpdated(address indexed treasury);

    error NotOwner();
    error ListingInactive();
    error PriceMismatch();
    error TransferFailed();

    address private _owner;

    modifier onlyOwner() {
        if (msg.sender != _owner) revert NotOwner();
        _;
    }

    constructor(address wldTokenAddress, address treasuryAddress) {
        wldToken = IERC20(wldTokenAddress);
        treasury = treasuryAddress;
        _owner = msg.sender;
    }

    function owner() external view returns (address) {
        return _owner;
    }

    function setTreasury(address treasuryAddress) external onlyOwner {
        treasury = treasuryAddress;
        emit TreasuryUpdated(treasuryAddress);
    }

    function configureListing(uint256 listingId, uint256 price, bool active) external onlyOwner {
        listingPrice[listingId] = price;
        listingActive[listingId] = active;
        emit ListingConfigured(listingId, price, active);
    }

    function buyListing(uint256 listingId, uint256 amount, string calldata buyerHash) external {
        if (!listingActive[listingId]) revert ListingInactive();
        if (listingPrice[listingId] != amount) revert PriceMismatch();

        bool ok = wldToken.transferFrom(msg.sender, treasury, amount);
        if (!ok) revert TransferFailed();

        emit ListingPurchased(listingId, msg.sender, amount, buyerHash);
    }
}
