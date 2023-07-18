using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace ProcessingStarter;

public class ProcessRed
{
    private readonly ILogger _logger;
    private readonly IServiceBusOutputToTopic _serviceBusTopicOutput;
    private readonly IConfiguration _configuration;

    public ProcessRed(ILogger<ProcessRed> logger, IServiceBusOutputToTopic serviceBusTopicOutput, IConfiguration configuration)
    {
        _logger = logger;
        _serviceBusTopicOutput = serviceBusTopicOutput;
        _configuration = configuration;
    }

    [Function(nameof(ProcessRed))]
    public async Task<MultiOutputRed> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("PROCESSING STARTER : HTTP TRIGGER : PROCESS RED : RECIEVED");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString(
@$"Sending message to [{_configuration[ConfigurationKeys.ServiceBusTopic]}] topic
and storing payload in [{GetEnvironmentVariable(ConfigurationKeys.BlobContainer)}] container
at {DateTime.Now}
");

        var busMessage = new ServiceBusMessage($"Red message to process")
        {
            CorrelationId = "red",
            ApplicationProperties =
                {
                    { "color", "red" },
                    { "text", "Text payload." },
                    { "number", 100}
                },
        };

        _logger.LogInformation("PROCESSING STARTER : HTTP TRIGGER : PROCESS RED : SENDING MESSAGE: CORRELATION ID: {correlationID}", busMessage.CorrelationId);

        await _serviceBusTopicOutput.SendMessageAsync(busMessage);

        var blobPayload = $"{{ {ParseCustomHeaders(busMessage.ApplicationProperties)}\n }}";

        _logger.LogInformation("PROCESSING STARTER : HTTP TRIGGER : PROCESS RED : BLOB PAYLOAD: {blobPayload}", blobPayload);

        return new MultiOutputRed()
        {
            BlobContent = blobPayload,
            HttpResponse = response
        };
    }

    private static string ParseCustomHeaders(IDictionary<string, object> headers)
    {
        return string.Join
        (" ", headers.Select
         (x => string.Format("\n\"{0}\"{1} {2}", x.Key, " : ", x.Value)));
    }

    private static string GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable
        (variableName, EnvironmentVariableTarget.Process) ?? $"BAD {variableName}";
    }
}

public class MultiOutputRed
{
    [BlobOutput("%Output_container%/output_{DateTime}_{rand-guid}.txt", Connection = "BlobConnection")]
    public string? BlobContent { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
