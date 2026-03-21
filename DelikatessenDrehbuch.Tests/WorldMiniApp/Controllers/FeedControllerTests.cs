using System.Net;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Controllers;

public class FeedControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FeedControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ReturnsOk()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Feed/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_WithSearchTerm_ReturnsOk()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Feed/Index?searchTerm=pasta&filter=feed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MyProfile_NoAuth_ReturnsOkOrRedirect()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Feed/MyProfile");

        // Without session, controller may still return 200 (empty profile) or redirect
        ((int)response.StatusCode).Should().BeOneOf(200, 302);
    }
}
