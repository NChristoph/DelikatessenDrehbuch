using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Logging
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // BlobServiceClient registrieren
        var blobConnection =
            context.Configuration["BlobStorageConnection"]
            ?? context.Configuration["AzureWebJobsStorage"];

        if (string.IsNullOrWhiteSpace(blobConnection))
        {
            throw new InvalidOperationException("Blob storage connection string fehlt!");
        }

        services.AddSingleton(new BlobServiceClient(blobConnection));
    })
    .Build();

host.Run();
