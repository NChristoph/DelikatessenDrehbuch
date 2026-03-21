using System.Net;
using System.Net.Http.Json;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Tests.Helpers;
using FluentAssertions;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Controllers;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VerifyAction_InvalidPayload_ReturnsBadRequest()
    {
        var request = new VerifyRequestDto
        {
            Payload = new VerifyPayloadDto { Status = "failed" },
            Action = "test"
        };

        var response = await _client.PostAsJsonAsync("/WorldMiniApp/Auth/VerifyAction", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyAction_NullPayload_ReturnsBadRequest()
    {
        var request = new VerifyRequestDto { Payload = null!, Action = "test" };

        var response = await _client.PostAsJsonAsync("/WorldMiniApp/Auth/VerifyAction", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SessionStatus_NoSession_ReturnsNotLoggedIn()
    {
        var response = await _client.GetAsync("/WorldMiniApp/Auth/SessionStatus");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("false");
    }

    [Fact]
    public async Task SetTestHash_ValidHash_ReturnsSuccess()
    {
        var request = new RefreshLoginRequest
        {
            UserHash = "test_integration_hash",
            RememberLogin = true
        };

        var response = await _client.PostAsJsonAsync("/WorldMiniApp/Auth/SetTestHash", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }
}
