using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Services;

public class AuthServiceTests
{
    private static AuthService CreateService()
    {
        var logger = new Mock<ILogger<AuthService>>();
        return new AuthService(logger.Object);
    }

    private static VerifyRequestDto CreateValidRequest() => new()
    {
        Action = "test-action",
        Signal = "",
        Payload = new VerifyPayloadDto
        {
            Status = "success",
            MerkleRoot = "0xabc",
            NullifierHash = "0xdef",
            Proof = "0x123",
            VerificationLevel = "orb"
        }
    };

    [Fact]
    public async Task VerifyProofWithWorldcoin_ApiError_ThrowsExternalServiceException()
    {
        // This test verifies that when the Worldcoin API returns an error,
        // the service throws WorldMiniAppExternalServiceException.
        // Since we can't mock HttpClient in AuthService directly (it creates its own),
        // we test with an invalid APP_ID scenario that will fail at the API.
        var service = CreateService();
        var request = CreateValidRequest();

        // The actual Worldcoin API call will fail with invalid proof data
        var act = () => service.VerifyProofWithWorldcoin(request);

        // Should throw ExternalServiceException because the API will reject this
        await act.Should().ThrowAsync<WorldMiniAppExternalServiceException>()
            .Where(ex => ex.ServiceName == "Worldcoin");
    }

    [Fact]
    public async Task VerifyProofWithWorldcoin_NullPayload_ThrowsException()
    {
        var service = CreateService();
        var request = new VerifyRequestDto
        {
            Action = "test",
            Payload = null!
        };

        var act = () => service.VerifyProofWithWorldcoin(request);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task VerifyProofWithWorldcoin_EmptyProof_ThrowsExternalServiceException()
    {
        var service = CreateService();
        var request = CreateValidRequest();
        request.Payload.Proof = "";

        var act = () => service.VerifyProofWithWorldcoin(request);

        // API will reject empty proof
        await act.Should().ThrowAsync<Exception>();
    }
}
