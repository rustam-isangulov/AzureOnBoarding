using Azure.Messaging.ServiceBus;

namespace ProcessingStarter;

public interface IServiceBusOutputToTopic
{
    Task SendMessageAsync(ServiceBusMessage message);
}