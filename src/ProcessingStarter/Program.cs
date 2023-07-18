using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;
using ProcessingStarter;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddAzureAppConfiguration(options =>
        {
            options.Connect(
                new Uri(Environment.GetEnvironmentVariable(ConfigurationKeys.AppConfigEndpoint)
                ?? throw new ArgumentNullException($"Environment variable {ConfigurationKeys.AppConfigEndpoint} is null!")),
                new DefaultAzureCredential())
            .Select("ProcessingStarter:*", LabelFilter.Null)
            .Select("ProcessingStarter:*", "CUSTOM");
        });
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IServiceBusOutputToTopic, ServiceBusOutputToColorsToProcess>();
    })
    .Build();

host.Run();
