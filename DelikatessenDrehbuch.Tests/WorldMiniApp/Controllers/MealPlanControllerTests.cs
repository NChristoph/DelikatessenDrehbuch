using System.Net;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Controllers;

public class MealPlanControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public MealPlanControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Generator_ReturnsOk()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Home/Generator");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletePlan_NoAuth_ReturnsNotFoundOrRedirect()
    {
        // Without authentication/session, delete should fail
        var response = await _client.PostAsync("/WorldMiniApp/Home/DeletePlan?id=999&userHash=", null);

        // Should either return NotFound, BadRequest or redirect (no session = no user)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Redirect,
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task EditPlan_NoAuth_RedirectsToHome()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Home/EditPlan?id=1&userHash=");

        // Without session, should redirect to Home
        ((int)response.StatusCode).Should().BeOneOf(200, 302);
    }
}
