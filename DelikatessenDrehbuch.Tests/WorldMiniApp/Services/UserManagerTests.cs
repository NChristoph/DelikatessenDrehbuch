using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Services;

public class UserManagerTests
{
    private UserManager CreateService(out Data.ApplicationDbContext context)
    {
        context = TestDbContextFactory.CreateInMemory();
        return new UserManager(context);
    }

    [Fact]
    public async Task CreateNewUserAsync_NewUser_CreatesInDb()
    {
        var service = CreateService(out var ctx);
        var request = new VerifyRequestDto
        {
            Payload = new VerifyPayloadDto
            {
                NullifierHash = "hash_new_user",
                VerificationLevel = "orb"
            },
            RememberLogin = true
        };

        await service.CreateNewUserAsync(request);

        var user = await ctx.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == "hash_new_user");
        user.Should().NotBeNull();
        user!.IsVerified.Should().Be("orb");
        user.RememberLogin.Should().BeTrue();
    }

    [Fact]
    public async Task CreateNewUserAsync_ExistingUser_UpdatesLogin()
    {
        var service = CreateService(out var ctx);
        ctx.WorldAppUser.Add(new WorldAppUser
        {
            UserHash = "existing_hash",
            IsVerified = "device",
            Lastlogin = DateTime.MinValue
        });
        await ctx.SaveChangesAsync();

        var request = new VerifyRequestDto
        {
            Payload = new VerifyPayloadDto
            {
                NullifierHash = "existing_hash",
                VerificationLevel = "orb"
            },
            RememberLogin = false
        };

        await service.CreateNewUserAsync(request);

        var user = await ctx.WorldAppUser.FirstAsync(x => x.UserHash == "existing_hash");
        user.IsVerified.Should().Be("orb");
        user.Lastlogin.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateOrUpdateWalletUserAsync_EmptyAddress_DoesNothing()
    {
        var service = CreateService(out var ctx);

        await service.CreateOrUpdateWalletUserAsync("", false);

        ctx.WorldAppUser.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrUpdateTestUserAsync_NewUser_CreatesWithTestVerification()
    {
        var service = CreateService(out var ctx);

        await service.CreateOrUpdateTestUserAsync("test_hash_123", true);

        var user = await ctx.WorldAppUser.FirstOrDefaultAsync(x => x.UserHash == "test_hash_123");
        user.Should().NotBeNull();
        user!.IsVerified.Should().Be("test");
        user.RememberLogin.Should().BeTrue();
    }
}
