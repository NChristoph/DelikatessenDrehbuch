-- =====================================================
-- Add second seller wallet address for USDT/USDC payouts
-- =====================================================

-- 1) Ensure legacy WLD wallet column exists for fresh installs
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanListings' AND COLUMN_NAME = 'SellerWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanListings]
    ADD [SellerWalletAddress] NVARCHAR(128) NULL;
END
GO

-- 2) Add second payout wallet column for USDT/USDC
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanListings' AND COLUMN_NAME = 'SellerUsdtWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanListings]
    ADD [SellerUsdtWalletAddress] NVARCHAR(128) NULL;
END
GO
