# Azure on-boarding project

Topics explored in this project:
- [Azure Functions in isolated worker process](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Service Bus with topics and subscriptions](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-tutorial-topics-subscriptions-cli)
- [Blob Storage bindings for Azure Function](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-blob?tabs=isolated-process%2Cextensionv5%2Cextensionv3&pivots=programming-language-csharp)
- [App Configuration](https://learn.microsoft.com/en-us/azure/app-service/app-service-configuration-references)
- [Azure CLI for infrastructure as code](https://learn.microsoft.com/en-us/cli/azure/what-is-azure-cli)
- [RBAC for Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/security-concepts?tabs=v4#user-management-permissions)

## Objective

Implement a set of three function apps following the component diagram shown below:

![component diagram](AzureOnBoardingProject.jpeg)

## ProcessingStarter

`ProcessingStarter` has two functions: `ProcessBlue` and `ProcessRed`. Both functions are triggered by http calls, return http response, write txt file into a Storage Container `container-all` and send a message to Service Bus `colors_to_process` topic.

### HttpTrigger input binding

`ProcessBlue` HTTP triggered function is defined as follows:

```csharp
[Function(nameof(ProcessBlue))]
public async Task<MultiOutputBlue> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
{
	// ... more code ...
}
```

### Multi-output binding

The following code uses multi-output binding for HTTP response and storage blob file:

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

Output bindings are declaratively defined in the following class:

```csharp
public class MultiOutputBlue
{
    [BlobOutput("%Output_container%/output_{DateTime}_{rand-guid}.txt", Connection = "BlobConnection")]
    public string? BlobContent { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
```

### App Configuration service to store binding parameters

Values for `Output_container` and `BlobConnection` are retrieved from App Configuration service via references in app settings, following Azure CLI script configures references:

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

App Configuration service stores actual values and is configured as shown below:

```json
{
  "ProcessingStarter:BlobConnection": "https://processingoutputs.blob.core.windows.net",
  "ProcessingStarter:Output_container": "container-all",
}
```

### SDK Service Bus output to a topic

`ProcessingStarter` functions send messages to the Service Bus topic using SDK client via helper object as shown below:

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

`_serviceBusTopicOutput` is a reference to `IServiceBusOutputToTopic` interface which is implemented by `ServiceBusOutputToColorsToProcess` that in turn creates Service Bus sender as shown below:

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

To enable dependency injection `ServiceBusOutput` the following configuration to the `HostBuilder` was added:

```csharp
var host = new HostBuilder()

	// ...

	.ConfigureServices((context, services) =>
    {
        services.AddSingleton<IServiceBusOutputToTopic, ServiceBusOutputToColorsToProcess>();
    })
    .Build();

	// ...
```

### Using App Configuration service

The following code adds Azure App Configuration to the function app:

```csharp
var host = new HostBuilder()

	// ...

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

	// ...

    .Build();
```

Endpoint for the App Configuration service is stored in app settings, following Azure CLI commands configure it:

```bash
configName="AppConfigEndPoint"
configValue="https://$appConfigServiceName.azconfig.io"

az functionapp config appsettings set \
     --name $funcAppName \
     --resource-group $resourceGroup \
     --settings "$configName=$configValue"
```

### RBAC configuration for Blob Storage, Service Bus and App Configuration

When the function app is created the following roles are assigned to it in order to access Blob Storage, Service Bus and App Configuration:

```bash
az role assignment create \
    --role "Azure Service Bus Data Sender" \
    --assignee-object-id $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ServiceBus/namespaces/$serviceBusNamespace

az role assignment create \
     --role "Azure Service Bus Data Receiver" \
     --assignee $funcAppId \
     --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ServiceBus/namespaces/$serviceBusNamespace

az role assignment create \
    --role "Storage Blob Data Contributor" \
    --assignee $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$blobStorageAccount

az role assignment create \
    --role "Storage Blob Data Reader" \
    --assignee $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$blobStorageAccount

az role assignment create \
    --role "App Configuration Data Reader" \
    --assignee $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.AppConfiguration/configurationStores/$appConfigServiceName
```

## ProcessBlue: Service Bus trigger, Blob Storage output

### ServiceBusTrigger input binding

`ServiceBusTrigger` service bus triggered function is defined as follows:

```csharp
[Function(nameof(ServiceBusTrigger))]
[BlobOutput("%Output_container%/output_{DateTime}_{rand-guid}.txt", Connection = "BlobConnection")]
public string Run
([ServiceBusTrigger("%Trigger_topic%","%Trigger_subscription%", Connection = "ServiceBusConnection")]
  string myQueueItem, FunctionContext context)
{
	// ... more code ...
}
```

### Blob Storage output binding

The following code uses declarative output binding for storage blob files:

```csharp
[Function(nameof(ServiceBusTrigger))]
[BlobOutput("%Output_container%/output_{DateTime}_{rand-guid}.txt", Connection = "BlobConnection")]
public string Run
([ServiceBusTrigger("%Trigger_topic%","%Trigger_subscription%", Connection = "ServiceBusConnection")]
    string myQueueItem, FunctionContext context)
{
	// ...

    var blobContent = $"{appPropertiesJson}";

	// ...

    return blobContent;
}
```

### App Configuration service to store binding parameters

Values for `Output_container`, `BlobConnection`. `Trigger_topic`, `Trigger_subscription`, and `ServiceBusConnection` are retrieved from the App Configuration service via references in app settings, following Azure CLI script configures references:

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

configName="ServiceBusConnection__fullyQualifiedNamespace"
configValue="@Microsoft.AppConfiguration(Endpoint=https://$appConfigServiceName.azconfig.io; Key=$funcAppName:ServiceBusConnection)"

az functionapp config appsettings set \
     --name $funcAppName \
     --resource-group $resourceGroup \
     --settings "$configName=$configValue"

configName="Trigger_topic"
configValue="@Microsoft.AppConfiguration(Endpoint=https://$appConfigServiceName.azconfig.io; Key=$funcAppName:$configName)"

az functionapp config appsettings set \
     --name $funcAppName \
     --resource-group $resourceGroup \
     --settings "$configName=$configValue"

configName="Trigger_subscription"
configValue="@Microsoft.AppConfiguration(Endpoint=https://$appConfigServiceName.azconfig.io; Key=$funcAppName:$configName)"

az functionapp config appsettings set \
     --name $funcAppName \
     --resource-group $resourceGroup \
     --settings "$configName=$configValue"
```

App Configuration service stores actual values and is configured as shown below:

```json
{
  "ProcessBlue:BlobConnection": "https://processingoutputs.blob.core.windows.net",
  "ProcessBlue:Output_container": "container-blue",
  "ProcessBlue:ServiceBusConnection": "ProcessingQueues.servicebus.windows.net",
  "ProcessBlue:Trigger_subscription": "blue",
  "ProcessBlue:Trigger_topic": "colors_to_process",
}
```

### RBAC configuration for Blob Storage, Service Bus and App Configuration

When the function app is created the following roles are assigned to it in order to access Blob Storage, Service Bus and App Configuration:

```bash
az role assignment create \
    --role "Azure Service Bus Data Sender" \
    --assignee-object-id $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ServiceBus/namespaces/$serviceBusNamespace

az role assignment create \
     --role "Azure Service Bus Data Receiver" \
     --assignee $funcAppId \
     --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ServiceBus/namespaces/$serviceBusNamespace

az role assignment create \
    --role "Storage Blob Data Contributor" \
    --assignee $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$blobStorageAccount

az role assignment create \
    --role "Storage Blob Data Reader" \
    --assignee $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$blobStorageAccount

az role assignment create \
    --role "App Configuration Data Reader" \
    --assignee $funcAppId \
    --scope /subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.AppConfiguration/configurationStores/$appConfigServiceName
```

## DevOps: Creating infrastructure with azure cli

`azure_cli_scripts/0.1.RUN_ALL` script creates the infrastructure required for the project including the following steps:
- setup parameters that are defined in `azure_cli_scripts/0.0.SET_VARIABLES`
- create a Resource Group with a Log Analytics workspace
- create Service Bus namespace with
  - Topic
  - two Subscriptions
  - Correlation Filters for each subscription
- create Blob Storage account with
  - three Containers
- create App Configuration with
  - configuration key-value pairs from `azure_cli_scripts/app_config_KeyValue_set.json`
- create three Function Apps with
  - storage account per app
  - App Insight connected to the resource log analytics workspace
  - assigned roles to access Service Bus
  - assigned roles to access Blob Storage
  - assigned roles to access App Configuration
- configure app settings for each Function App including
  - endpoint for App Configuration
  - reference for BlobConnection strings
  - reference for Blob output container
  - references to Service Bus topic
  - references to Service Bus subscriptions


## DevOps: Creating build/test/deploy pipeline with Yaml

All three Functions are built, tested and deployed on each new pull request using the Azure pipeline defined in `pipelines/azure-pipelines.yml`. The pipeline sequence for each app looks similar to `ProcessingStarter`:

```yaml
pr:
- main

pool:
  vmImage: ubuntu-latest

steps:

- task: DotNetCoreCLI@2
  displayName: 'dotnet build'
  inputs:
    command: 'build'
    projects: '**/*.csproj'

- task: DotNetCoreCLI@2
  displayName: 'dotnet test'
  inputs:
    command: 'test'
    projects: '**/*.csproj'

- task: DotNetCoreCLI@2
  displayName: 'ProcessingStarter dotnet publish'
  inputs:
    command: publish
    arguments: '--configuration Release --output publish_output_ProcessingStarter'
    projects: '**/ProcessingStarter.csproj'
    publishWebProjects: false
    modifyOutputPath: false
    zipAfterPublish: false

- task: ArchiveFiles@2
  displayName: "ProcessingStarter archive files"
  inputs:
    rootFolderOrFile: "$(System.DefaultWorkingDirectory)/publish_output_ProcessingStarter"
    includeRootFolder: false
    archiveFile: "$(System.DefaultWorkingDirectory)/ProcessingStarter_build$(Build.BuildId).zip"

- task: AzureFunctionApp@2
  displayName: "ProcessingStarter Deploy"
  inputs:
    azureSubscription: 'isarust-conn'
    appType: 'functionApp'
    appName: 'ProcessingStarter'
    deployToSlotOrASE: true
    resourceGroupName: 'isarust-demo-rg'
    slotName: 'production'
    package: '$(System.DefaultWorkingDirectory)/ProcessingStarter_build$(Build.BuildId).zip'
    deploymentMethod: 'auto'
```

---
