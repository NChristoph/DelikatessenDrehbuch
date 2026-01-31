using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var blobStorageConnection = builder.Configuration.GetValue<string>("BlobStorageConnection")
    ?? builder.Configuration.GetValue<string>("AzureWebJobsStorage");

if (string.IsNullOrWhiteSpace(blobStorageConnection))
{
    throw new InvalidOperationException("Blob storage connection string is missing. Configure 'BlobStorageConnection' or 'AzureWebJobsStorage'.");
}

builder.Services.AddSingleton(new BlobServiceClient(blobStorageConnection));

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
