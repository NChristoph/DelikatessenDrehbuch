using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Models;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Services;

public class WorldAppMealPlanServiceTests
{
    private const string OrbUserHash = "orb_user_hash_123";
    private const string DeviceUserHash = "device_user_hash_456";

    private WorldAppMealPlanService CreateService(out Data.ApplicationDbContext context)
    {
        context = TestDbContextFactory.CreateInMemory();
        return new WorldAppMealPlanService(context);
    }

    private static MiniAppSetupModel DefaultSettings() => new()
    {
        DietType = "Alles",
        DayCount = 7,
        PersonCount = 2
    };

    private async Task SeedOrbUser(Data.ApplicationDbContext ctx)
    {
        ctx.WorldAppUser.Add(new WorldAppUser
        {
            UserHash = OrbUserHash,
            IsVerified = "orb",
            Lastlogin = DateTime.Now
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedDeviceUser(Data.ApplicationDbContext ctx)
    {
        ctx.WorldAppUser.Add(new WorldAppUser
        {
            UserHash = DeviceUserHash,
            IsVerified = "device",
            Lastlogin = DateTime.Now
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CheckVerifyAsync_OrbUser_CreatesMealPlan()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        await service.CheckVerifyAsync(DefaultSettings(), OrbUserHash, "Wochenplan");

        var plans = ctx.WorldUserMealPlan.Where(x => x.UserHash == OrbUserHash).ToList();
        plans.Should().HaveCount(1);
        plans[0].Title.Should().Be("Wochenplan");
    }

    [Fact]
    public async Task CheckVerifyAsync_OrbUser_CanCreateMultiplePlans()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        await service.CheckVerifyAsync(DefaultSettings(), OrbUserHash, "Plan A");
        await service.CheckVerifyAsync(DefaultSettings(), OrbUserHash, "Plan B");

        ctx.WorldUserMealPlan.Count(x => x.UserHash == OrbUserHash).Should().Be(2);
    }

    [Fact]
    public async Task CheckVerifyAsync_DeviceUser_CreatesOnlyOnePlan()
    {
        var service = CreateService(out var ctx);
        await SeedDeviceUser(ctx);

        await service.CheckVerifyAsync(DefaultSettings(), DeviceUserHash, "Plan 1");
        await service.CheckVerifyAsync(DefaultSettings(), DeviceUserHash, "Plan 2");

        ctx.WorldUserMealPlan.Count(x => x.UserHash == DeviceUserHash).Should().Be(1);
    }

    [Fact]
    public async Task CheckVerifyAsync_UnknownUser_ThrowsNotFound()
    {
        var service = CreateService(out _);

        var act = () => service.CheckVerifyAsync(DefaultSettings(), "unknown_hash", "Plan");

        await act.Should().ThrowAsync<WorldMiniAppNotFoundException>();
    }

    [Fact]
    public async Task SaveNewMealPlanAsync_OrbUser_SavesPlan()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        ctx.WorldUserMealPlan.Add(new WorldUserMealPlan
        {
            UserHash = OrbUserHash,
            Title = "Mein Plan"
        });
        await ctx.SaveChangesAsync();

        var recipes = new List<MealPlanerModel>
        {
            new() { Index = 0, Recipes = new Recipes { Id = 1 } }
        };

        await service.SaveNewMealPlanAsync(OrbUserHash, recipes, "Mein Plan");

        var plan = ctx.WorldUserMealPlan.First(x => x.UserHash == OrbUserHash);
        plan.MealPlan.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveNewMealPlanAsync_UserNotFound_ThrowsNotFound()
    {
        var service = CreateService(out _);

        var act = () => service.SaveNewMealPlanAsync("unknown", new List<MealPlanerModel>(), "title");

        await act.Should().ThrowAsync<WorldMiniAppNotFoundException>();
    }

    [Fact]
    public async Task SaveNewMealPlanAsync_PlanNotFound_ThrowsNotFound()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        var act = () => service.SaveNewMealPlanAsync(OrbUserHash, new List<MealPlanerModel>(), "Nicht vorhanden");

        await act.Should().ThrowAsync<WorldMiniAppNotFoundException>();
    }

    [Fact]
    public async Task DeleteMealPlanAsync_ExistingPlan_Deletes()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        var plan = new WorldUserMealPlan { UserHash = OrbUserHash, Title = "Löschen" };
        ctx.WorldUserMealPlan.Add(plan);
        await ctx.SaveChangesAsync();

        await service.DeleteMealPlanAsync(plan.Id, OrbUserHash);

        ctx.WorldUserMealPlan.Any(x => x.Id == plan.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteMealPlanAsync_WrongUser_ThrowsNotFound()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        var plan = new WorldUserMealPlan { UserHash = OrbUserHash, Title = "Plan" };
        ctx.WorldUserMealPlan.Add(plan);
        await ctx.SaveChangesAsync();

        var act = () => service.DeleteMealPlanAsync(plan.Id, "other_user");

        await act.Should().ThrowAsync<WorldMiniAppNotFoundException>();
    }

    [Fact]
    public async Task GetMealPlansByHashAsync_ReturnsOnlyUserPlans()
    {
        var service = CreateService(out var ctx);

        ctx.WorldUserMealPlan.AddRange(
            new WorldUserMealPlan { UserHash = "user1", Title = "A" },
            new WorldUserMealPlan { UserHash = "user1", Title = "B" },
            new WorldUserMealPlan { UserHash = "user2", Title = "C" }
        );
        await ctx.SaveChangesAsync();

        var result = await service.GetMealPlansByHashAsync("user1");

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.UserHash == "user1");
    }

    [Fact]
    public async Task CheckTitleAsync_DuplicateTitle_Increments()
    {
        var service = CreateService(out var ctx);
        await SeedOrbUser(ctx);

        await service.CheckVerifyAsync(DefaultSettings(), OrbUserHash, "Wochenplan");
        await service.CheckVerifyAsync(DefaultSettings(), OrbUserHash, "Wochenplan");

        var titles = ctx.WorldUserMealPlan
            .Where(x => x.UserHash == OrbUserHash)
            .Select(x => x.Title)
            .ToList();

        titles.Should().Contain("Wochenplan");
        titles.Should().Contain("Wochenplan 1");
    }
}
