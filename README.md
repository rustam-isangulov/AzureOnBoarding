# Azure on-boarding project

Azure on-boarding project: Azure Functions, Service Bus, Blob Storage, App Configuration, Azure CLI, RBAC

## Objective

[diagram]

## ProcessingStarter

`ProcessingStarter` has two functions: `ProcessBlue` and `ProcessRed`. Both functions are triggered by http calls, return http response, write txt file into a Storage Container `container-all` and send a message to Service Bus `colors_to_process` topic.

### HttpTrigger input binding

`ProcessBlue` http triggered function is defined as follows:

```csharp
[Function(nameof(ProcessBlue))]
public async Task<MultiOutputBlue> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
{
	// ... more code ...
}
```

### Multi-output binding

Following code uses multi-output binding for http response and storage blob file:

```csharp
[Function(nameof(ProcessBlue))]
public async Task<MultiOutputBlue> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
{
	// ...

	var response = req.CreateResponse(HttpStatusCode.OK);

	// ...

	var blobPayload = $"{{ {ParseCustomHeaders(busMessage.ApplicationProperties)}\n }}";

	// ...

	return new MultiOutputBlue()
	{
        BlobContent = blobPayload,
        HttpResponse = response
	};
}
```

Output bindings are declaratively defined in the follwoing class:

```csharp
public class MultiOutputBlue
{
    [BlobOutput("%Output_container%/output_{DateTime}_{rand-guid}.txt", Connection = "BlobConnection")]
    public string? BlobContent { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
```

### App Configuration service to store binding paramaters

Values for `Output_container` and `BlobConnection` are retrieved from App Configuration service via references in appsettings, following azure cli script configures references:

```bash
configName="BlobConnection__serviceUri"
configValue="@Microsoft.AppConfiguration(Endpoint=https://$appConfigServiceName.azconfig.io; Key=$funcAppName:BlobConnection)"

az functionapp config appsettings set \
     --name $funcAppName \
     --resource-group $resourceGroup \
     --settings "$configName=$configValue"

configName="Output_container"
configValue="@Microsoft.AppConfiguration(Endpoint=https://$appConfigServiceName.azconfig.io; Key=$funcAppName:$configName)"

az functionapp config appsettings set \
     --name $funcAppName \
     --resource-group $resourceGroup \
     --settings "$configName=$configValue"
```

App Configuration service stores actual values and configured as shown below:

```json
{
  "ProcessingStarter:BlobConnection": "https://processingoutputs.blob.core.windows.net",
  "ProcessingStarter:Output_container": "container-all",
}
````

### SDK Service Bus output to a topic

`ProcessingStarter` functions send messages to Service Bus topic using SDK client via helper object as shown below:

```csharp
[Function(nameof(ProcessBlue))]
public async Task<MultiOutputBlue> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
{
	// ...

    var busMessage = new ServiceBusMessage($"Blue message to process")
        {
            CorrelationId = "blue",
            ApplicationProperties =
                {
                    { "color", "blue" },
                    { "text", "A different text payload." },
                    { "number", 1000}
				},
	    };

	// ...

    await _serviceBusTopicOutput.SendMessageAsync(busMessage);

	// ...
}
```

`_serviceBusTopicOutput` is a reference to `IServiceBusOutputToTopic` interface which is implemented by `ServiceBusOutputToColorsToProcess` which creates Service Bus sender as shown below:

```csharp
public class ServiceBusOutputToColorsToProcess : IServiceBusOutputToTopic
{
	// ...

    public ServiceBusOutputToColorsToProcess(ILogger<ServiceBusOutputToColorsToProcess> logger, IConfiguration configuration)
    {
		// ...

        _sender = new ServiceBusClient(_configuration[ConfigurationKeys.ServiceBusConnection], new DefaultAzureCredential())
            .CreateSender(_configuration[ConfigurationKeys.ServiceBusTopic]);
    }

    public async Task SendMessageAsync(ServiceBusMessage message)
    {
		// ...

        await _sender.SendMessageAsync(message);
    }
}
```

`ServiceBusConnection` and `ServiceBusTopic` parameters are stored in the App Configuration service and configured as shown below:

```json
{
  "ProcessingStarter:Output_topic": "colors_to_process",
  "ProcessingStarter:ServiceBusConnection": "ProcessingQueues.servicebus.windows.net"
}
```

### Using App Configuration service

### RBAC configuration for Blob Storage, Servcie Bus and App Config Service

### Unit test for functions

### DevOps: Creating infrastructure with azure cli

### DevOps: Creating build/test/deploy pipline with Yaml
