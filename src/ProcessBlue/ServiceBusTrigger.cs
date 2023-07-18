using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessBlue;

public class ServiceBusTrigger
{
    private readonly ILogger _logger;

    public ServiceBusTrigger(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ServiceBusTrigger>();
    }

    [Function(nameof(ServiceBusTrigger))]
    [BlobOutput("%Output_container%/output_{DateTime}_{rand-guid}.txt", Connection = "BlobConnection")]
    public string Run
    ([ServiceBusTrigger("%Trigger_topic%","%Trigger_subscription%", Connection = "ServiceBusConnection")]
    string myQueueItem, FunctionContext context)
    {
        _logger.LogInformation("PROCESS BLUE : SERVICE BUS TRIGGER : RECIEVED : ITEM : {queueItem}", myQueueItem);

        var appPropertiesJson = context.BindingContext.BindingData["ApplicationProperties"];

        _logger.LogInformation("PROCESS BLUE : SERVICE BUS TRIGGER : RECIEVED : APP PROPS: {appPropsJson}", appPropertiesJson);

        var appPropertiesDict = context.GetServiceBusMessageHeaders();

        _logger.LogInformation("PROCESS BLUE : SERVICE BUS TRIGGER : HEADER CONTENT : COLOR : {colorValue}", appPropertiesDict["color"]);

        var blobContent = $"{appPropertiesJson}";

        _logger.LogInformation("PROCESS BLUE : SERVICE BUS TRIGGER : BLOB PAYLOAD : {blobContent}", blobContent);

        return blobContent;
    }
}

// source: https://github.com/MicrosoftDocs/azure-docs/issues/89765#issuecomment-1241829535	 
public static class FunctionContextExtensions
{
    public static IReadOnlyDictionary<string, object> GetServiceBusMessageHeaders
        (this FunctionContext functionContext)
    {
        if (!functionContext.BindingContext.BindingData
        .TryGetValue("ApplicationProperties", out var applicationPropertiesString))
        {
            return new Dictionary<string, object>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, object>>
        (applicationPropertiesString?.ToString()!, new JsonSerializerOptions
        {
            Converters = { new ObjectToInferredTypesConverter() }
        })!;
    }
}

public class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
            JsonTokenType.String => reader.GetString()!,
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };

    public override void Write(
        Utf8JsonWriter writer,
        object objectToWrite,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
}
