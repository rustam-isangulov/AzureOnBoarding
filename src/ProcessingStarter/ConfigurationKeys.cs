namespace ProcessingStarter;

public static class ConfigurationKeys
{
    public static readonly string AppConfigEndpoint = "AppConfigEndPoint";
    public static readonly string ServiceBusConnection = "ProcessingStarter:ServiceBusConnection";
    public static readonly string ServiceBusTopic = "ProcessingStarter:Output_topic";
    public static readonly string BlobContainer = "Output_container";
}