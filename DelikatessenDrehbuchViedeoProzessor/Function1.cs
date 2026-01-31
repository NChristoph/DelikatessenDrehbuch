using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DelikatessenDrehbuchViedeoProzessor
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Function1))]
        public async Task Run([BlobTrigger("samples-workitems/{name}", Connection = "DefaultEndpointsProtocol=https;AccountName=blobdelikatessendrehbuch;AccountKey=NNJKin4e0NxZwD8XpLgZC+21vgcvkMd5tcp1gXiM4+zSAYV2DGDBx7unmFglQrs9YQH:RdtJIMME+AStw4Espg==;EndpointSuffix=core.windows.net")] Stream stream, string name)
        {
            using var blobStreamReader = new StreamReader(stream);
            var content = await blobStreamReader.ReadToEndAsync();
            _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");
        }
    }
}
