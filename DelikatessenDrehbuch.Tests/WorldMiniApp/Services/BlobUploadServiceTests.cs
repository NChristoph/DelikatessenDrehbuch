using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DelikatessenDrehbuch.Tests.WorldMiniApp.Services;

public class BlobUploadServiceTests
{
    private static BlobUploadService CreateService()
    {
        var config = new Mock<IConfiguration>();
        var logger = new Mock<ILogger<BlobUploadService>>();
        return new BlobUploadService(config.Object, logger.Object);
    }

    [Fact]
    public async Task UploadContentToBlob_NullFile_ThrowsArgumentException()
    {
        var service = CreateService();

        var act = () => service.UploadContentToBlob(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadContentToBlob_EmptyFile_ThrowsArgumentException()
    {
        var service = CreateService();
        var file = new FormFile(Stream.Null, 0, 0, "file", "empty.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var act = () => service.UploadContentToBlob(file);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadContentToBlob_OversizedImage_ThrowsInvalidOperation()
    {
        var service = CreateService();
        // Create a mock file that claims to be > 15MB
        var stream = new MemoryStream(new byte[100]);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(16L * 1024 * 1024); // 16 MB
        file.Setup(f => f.FileName).Returns("big.jpg");
        file.Setup(f => f.ContentType).Returns("image/jpeg");
        file.Setup(f => f.OpenReadStream()).Returns(stream);

        var act = () => service.UploadContentToBlob(file.Object);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*15 MB*");
    }

    [Fact]
    public async Task UploadContentToBlob_InvalidFormat_ThrowsInvalidOperation()
    {
        var service = CreateService();
        var stream = new MemoryStream(new byte[100]);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(100);
        file.Setup(f => f.FileName).Returns("doc.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(stream);

        var act = () => service.UploadContentToBlob(file.Object);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Dateityp*");
    }

    [Fact]
    public async Task UploadContentToBlobFromUrl_InvalidHost_ThrowsArgumentException()
    {
        var service = CreateService();

        var act = () => service.UploadContentToBlobFromUrl("https://evil-site.com/image.jpg");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Host*");
    }
}
