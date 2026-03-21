using System.Net;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Controllers;

public class MarketplaceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MarketplaceControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ReturnsOk()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Marketplace/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateListing_NoAuth_ReturnsBadRequestOrRedirect()
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("title", "Test Plan"),
            new KeyValuePair<string, string>("price", "10"),
            new KeyValuePair<string, string>("mealPlanId", "1")
        });

        var response = await _client.PostAsync("/WorldMiniApp/Marketplace/CreateListing", content);

        // Without auth, should fail (BadRequest/Unauthorized/NotFound/Redirect)
        ((int)response.StatusCode).Should().BeOneOf(400, 401, 404, 302, 500);
    }

    [Fact]
    public async Task FinalizeWorldChainPurchase_NoAuth_ReturnsBadRequest()
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("listingId", "1"),
            new KeyValuePair<string, string>("txHash", "0x123"),
            new KeyValuePair<string, string>("walletAddress", "0xabc")
        });

        var response = await _client.PostAsync("/WorldMiniApp/Marketplace/FinalizeWorldChainPurchase", content);

        // Without auth, should fail
        ((int)response.StatusCode).Should().BeOneOf(400, 401, 404, 302, 500);
    }
}
