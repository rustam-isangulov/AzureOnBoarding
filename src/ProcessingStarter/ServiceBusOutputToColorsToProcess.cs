using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProcessingStarter;

public class ServiceBusOutputToColorsToProcess : IServiceBusOutputToTopic
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly ServiceBusSender _sender;

    public ServiceBusOutputToColorsToProcess(ILogger<ServiceBusOutputToColorsToProcess> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        if (string.IsNullOrEmpty(_configuration[ConfigurationKeys.ServiceBusConnection]))
        {
            _logger.LogCritical("PROCESSING STARTER : SERVICE BUS OUTPUT : CONNECTION: BAD VALUE : {connection}",
            _configuration[ConfigurationKeys.ServiceBusConnection]);

            throw new ArgumentException($"ServiceBusOutputToColorsToProcess : configuration value for {ConfigurationKeys.ServiceBusConnection} is null or empty.");
        }

        if (string.IsNullOrEmpty(_configuration[ConfigurationKeys.ServiceBusTopic]))
        {
            _logger.LogCritical("PROCESSING STARTER : SERVICE BUS OUTPUT : TOPIC: BAD VALUE : {topic}",
            _configuration[ConfigurationKeys.ServiceBusTopic]);

            throw new ArgumentException($"ServiceBusOutputToColorsToProcess : configuration value for {ConfigurationKeys.ServiceBusTopic} is null or empty.");
        }

        _sender = new ServiceBusClient(_configuration[ConfigurationKeys.ServiceBusConnection], new DefaultAzureCredential())
            .CreateSender(_configuration[ConfigurationKeys.ServiceBusTopic]);
    }

    public async Task SendMessageAsync(ServiceBusMessage message)
    {
        _logger.LogInformation("PROCESSING STARTER : SERVICE BUS OUTPUT : SENDING MESSAGE: CORRELATION ID: {correlationID}",
        message.CorrelationId);

        await _sender.SendMessageAsync(message);
    }
}