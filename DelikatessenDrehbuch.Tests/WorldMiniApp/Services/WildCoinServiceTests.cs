using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Services;

public class WildCoinServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly WildCoinService _service;

    public WildCoinServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqlite();
        var config = new Mock<IConfiguration>();
        var logger = new Mock<ILogger<WildCoinService>>();
        _service = new WildCoinService(_context, config.Object, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<WorldAppUser> SeedUser(string hash, decimal balance = 0)
    {
        var user = new WorldAppUser
        {
            UserHash = hash,
            IsVerified = "orb",
            WildCoinBalance = balance,
            Lastlogin = DateTime.Now
        };
        _context.WorldAppUser.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetBalanceAsync_ExistingUser_ReturnsBalance()
    {
        await SeedUser("user1", 100m);

        var balance = await _service.GetBalanceAsync("user1");

        balance.Should().Be(100m);
    }

    [Fact]
    public async Task GetBalanceAsync_UnknownUser_ReturnsZero()
    {
        var balance = await _service.GetBalanceAsync("nonexistent");

        balance.Should().Be(0m);
    }

    [Fact]
    public async Task TransferAsync_SufficientFunds_TransfersCorrectly()
    {
        await SeedUser("sender", 50m);
        await SeedUser("receiver", 10m);

        var result = await _service.TransferAsync("sender", "receiver", 30m, "test-transfer");

        result.Should().BeTrue();

        var senderBalance = await _service.GetBalanceAsync("sender");
        var receiverBalance = await _service.GetBalanceAsync("receiver");

        senderBalance.Should().Be(20m);
        receiverBalance.Should().Be(40m);
    }

    [Fact]
    public async Task TransferAsync_InsufficientFunds_ReturnsFalse()
    {
        await SeedUser("sender", 10m);
        await SeedUser("receiver", 0m);

        var result = await _service.TransferAsync("sender", "receiver", 50m, "test");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TransferAsync_NegativeAmount_ReturnsFalse()
    {
        await SeedUser("sender", 100m);
        await SeedUser("receiver", 0m);

        var result = await _service.TransferAsync("sender", "receiver", -5m, "test");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RewardAsync_ValidReward_IncreasesBalance()
    {
        await SeedUser("user1", 10m);

        var result = await _service.RewardAsync("user1", 25m, "daily-reward");

        result.Should().BeTrue();
        (await _service.GetBalanceAsync("user1")).Should().Be(35m);
    }

    [Fact]
    public async Task GetTransactionsAsync_ReturnsUserTransactions()
    {
        await SeedUser("sender", 100m);
        await SeedUser("receiver", 0m);

        await _service.TransferAsync("sender", "receiver", 10m, "tx1");
        await _service.TransferAsync("sender", "receiver", 20m, "tx2");

        var senderTx = await _service.GetTransactionsAsync("sender");

        senderTx.Should().HaveCount(2);
        senderTx.Should().OnlyContain(t => t.UserHash == "sender");
    }

    [Fact]
    public async Task CreateListingAsync_UserNotFound_ThrowsNotFound()
    {
        var act = () => _service.CreateListingAsync("unknown_seller", 1, "title", null, 5m);

        await act.Should().ThrowAsync<WorldMiniAppNotFoundException>();
    }
}
