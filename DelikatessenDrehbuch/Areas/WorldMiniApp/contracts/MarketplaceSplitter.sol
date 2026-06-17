// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/**
 * MarketplaceSplitter — atomarer 80/20-Split für den Avocado-Marktplatz (World Chain).
 *
 * ZWECK (Modell A): Der Käufer zahlt EINMAL; der Contract leitet im selben Tx atomar
 * den Verkäufer-Anteil an den Verkäufer und die Plattform-Gebühr an die Plattform-Wallet.
 * Keine zwei Bestätigungen, keine Auszahlungs-Schuld, Gebühr garantiert.
 *
 * ABLAUF (Frontend, World App):
 *   1. Käufer gibt dem Contract eine ERC-20-Allowance über `amount` frei (approve / Permit2).
 *   2. Käufer ruft `purchase(token, seller, amount, reference)` (via MiniKit.sendTransaction).
 *   3. Contract zieht `amount` vom Käufer (transferFrom) und splittet:
 *        sellerAmount = amount * (10000 - feeBps) / 10000
 *        feeAmount    = amount - sellerAmount   -> an platformWallet
 *   4. Event `Purchase(...)` wird emittiert -> Backend verifiziert den Kauf anhand der Logs.
 *
 * HINWEIS: Bewusst ohne externe Imports gehalten (leicht zu auditieren/deployen).
 * Vor Produktion: Audit + Tests! Token müssen reguläre ERC-20 sein (WLD, USDC.e).
 */
interface IERC20 {
    function transferFrom(address from, address to, uint256 value) external returns (bool);
}

contract MarketplaceSplitter {
    address public owner;
    address public platformWallet;
    uint16 public feeBps;          // Plattform-Gebühr in Basispunkten (2000 = 20%)
    bool private _locked;          // simple reentrancy guard

    event Purchase(
        bytes32 indexed reference, // z.B. keccak256("listing-{id}-{ts}")
        address indexed buyer,
        address indexed seller,
        address token,
        uint256 amount,
        uint256 sellerAmount,
        uint256 feeAmount
    );
    event PlatformWalletChanged(address wallet);
    event FeeChanged(uint16 feeBps);
    event OwnerChanged(address owner);

    modifier onlyOwner() {
        require(msg.sender == owner, "not owner");
        _;
    }
    modifier nonReentrant() {
        require(!_locked, "reentrant");
        _locked = true;
        _;
        _locked = false;
    }

    constructor(address _platformWallet, uint16 _feeBps) {
        require(_platformWallet != address(0), "platform=0");
        require(_feeBps <= 10000, "fee>100%");
        owner = msg.sender;
        platformWallet = _platformWallet;
        feeBps = _feeBps;
    }

    /**
     * Führt den Kauf + Split atomar aus. Käufer muss vorher eine Allowance über `amount`
     * für `token` an diesen Contract erteilt haben.
     */
    function purchase(
        address token,
        address seller,
        uint256 amount,
        bytes32 reference
    ) external nonReentrant {
        require(token != address(0), "token=0");
        require(seller != address(0), "seller=0");
        require(amount > 0, "amount=0");

        uint256 feeAmount = (amount * feeBps) / 10000;
        uint256 sellerAmount = amount - feeAmount;

        // Direkt vom Käufer an Verkäufer und Plattform (Contract hält keine Gelder).
        require(IERC20(token).transferFrom(msg.sender, seller, sellerAmount), "seller transfer failed");
        if (feeAmount > 0) {
            require(IERC20(token).transferFrom(msg.sender, platformWallet, feeAmount), "fee transfer failed");
        }

        emit Purchase(reference, msg.sender, seller, token, amount, sellerAmount, feeAmount);
    }

    // --- Admin ---
    function setPlatformWallet(address _wallet) external onlyOwner {
        require(_wallet != address(0), "platform=0");
        platformWallet = _wallet;
        emit PlatformWalletChanged(_wallet);
    }
    function setFeeBps(uint16 _feeBps) external onlyOwner {
        require(_feeBps <= 10000, "fee>100%");
        feeBps = _feeBps;
        emit FeeChanged(_feeBps);
    }
    function transferOwnership(address _owner) external onlyOwner {
        require(_owner != address(0), "owner=0");
        owner = _owner;
        emit OwnerChanged(_owner);
    }
}
