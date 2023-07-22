# Azure on-boarding project

Azure on-boarding project: Azure Functions, Service Bus, Blob Storage, App Configuration, Azure CLI, RBAC

## Objective

[diagram]

## ProcessingStarter

`ProcessingStarter` has two functions: `ProcessBlue` and `ProcessRed`. Both functions are triggered by http calls, return http response, write txt file into a Storage Container `container-all` and send a message to Service Bus `colors_to_process` topic.

### HttpTrigger input binding

`ProcessBlue` http triggered function is defined as follows:

```
[Function(nameof(ProcessBlue))]
public async Task<MultiOutputBlue> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
{
	// ... more code ...
}

```

### Declarative multi-response output binding

### SDK Service Bus output to a topic

### Using App Configuration service

### Unit test for functions

### DevOps: Creating infrastructure with azure cli

### DevOps: Creating build/test/deploy pipline with Yaml
