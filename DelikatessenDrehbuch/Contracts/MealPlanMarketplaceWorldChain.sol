// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

interface IERC20 {
    function transferFrom(address from, address to, uint256 value) external returns (bool);
}

/// @title MealPlan Marketplace (World Chain)
/// @notice Marketplace mit 80/20 Revenue-Split: 80% Creator, 20% Platform.
contract MealPlanMarketplaceWorldChain {
    IERC20 public immutable wldToken;
    address public treasury;

    uint256 public creatorShareBps = 8000;  // 80% in Basis-Punkten
    uint256 public constant BPS_DENOMINATOR = 10000;

    struct Listing {
        uint256 price;
        address creator;
        bool active;
    }

    mapping(uint256 => Listing) public listings;

    event ListingConfigured(uint256 indexed listingId, uint256 price, address indexed creator, bool active);
    event ListingPurchased(
        uint256 indexed listingId,
        address indexed buyer,
        uint256 totalAmount,
        uint256 creatorAmount,
        uint256 platformAmount,
        address indexed creator,
        string buyerHash
    );
    event TreasuryUpdated(address indexed treasury);
    event CreatorShareUpdated(uint256 newShareBps);

    error NotOwner();
    error ListingInactive();
    error PriceMismatch();
    error TransferFailed();
    error InvalidShare();
    error InvalidCreator();

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

    /// @notice Creator-Anteil anpassen (in Basis-Punkten, max 10000 = 100%)
    function setCreatorShare(uint256 newShareBps) external onlyOwner {
        if (newShareBps > BPS_DENOMINATOR) revert InvalidShare();
        creatorShareBps = newShareBps;
        emit CreatorShareUpdated(newShareBps);
    }

    /// @notice Listing konfigurieren mit Creator-Wallet-Adresse
    function configureListing(uint256 listingId, uint256 price, address creator, bool active) external onlyOwner {
        if (creator == address(0)) revert InvalidCreator();
        listings[listingId] = Listing(price, creator, active);
        emit ListingConfigured(listingId, price, creator, active);
    }

    /// @notice Kauf mit automatischem 80/20 Split
    function buyListing(uint256 listingId, uint256 amount, string calldata buyerHash) external {
        Listing storage listing = listings[listingId];
        if (!listing.active) revert ListingInactive();
        if (listing.price != amount) revert PriceMismatch();

        uint256 creatorAmount = (amount * creatorShareBps) / BPS_DENOMINATOR;
        uint256 platformAmount = amount - creatorAmount;

        // 80% an Creator
        if (creatorAmount > 0) {
            bool okCreator = wldToken.transferFrom(msg.sender, listing.creator, creatorAmount);
            if (!okCreator) revert TransferFailed();
        }

        // 20% an Treasury (Platform)
        if (platformAmount > 0) {
            bool okPlatform = wldToken.transferFrom(msg.sender, treasury, platformAmount);
            if (!okPlatform) revert TransferFailed();
        }

        emit ListingPurchased(listingId, msg.sender, amount, creatorAmount, platformAmount, listing.creator, buyerHash);
    }

    // --- Legacy-Kompatibilität: Alte Getter für listingPrice/listingActive ---
    function listingPrice(uint256 listingId) external view returns (uint256) {
        return listings[listingId].price;
    }

    function listingActive(uint256 listingId) external view returns (bool) {
        return listings[listingId].active;
    }

    function listingCreator(uint256 listingId) external view returns (address) {
        return listings[listingId].creator;
    }
}
