using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Messaging.ServiceBus;

namespace ProcessingStarter.Tests.Unit;

public class ProcessRedFunctionTests
{
    [Fact]
    public async Task HttpTrigger_ShouldReturnHttpResponseAndMessageAndBlobContent_WhenCalledAsync()
    {
        // Arrange

        var context = new Mock<FunctionContext>();
        var request = new Mock<HttpRequestData>(context.Object);
        request.Setup(r => r.Body).Returns(new MemoryStream());
        request.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var response = new Mock<HttpResponseData>(context.Object);
            response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            response.SetupProperty(r => r.StatusCode);
            response.SetupProperty(r => r.Body, new MemoryStream());
            return response.Object;
        });

        var outputTopicName = "Output_topic";
        var outputContainerName = "Output_container";
        Environment.SetEnvironmentVariable("Output_container", outputContainerName);

        var logger = new NullLoggerFactory().CreateLogger<ProcessRed>();
        var topicOutput = new Mock<IServiceBusOutputToTopic>();
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet( p => p["ProcessingStarter:Output_topic"]).Returns("Output_topic");

        // Act

        var process = new ProcessRed(logger, topicOutput.Object, configuration.Object);
        var response = await process.Run(request.Object);

        // Assert
        
        var expectedNumberOfSBCalls = 1;
        var expectedCorrelationId = "red";
        var expectedHttpStatus = HttpStatusCode.OK;
        var expectedHttpBody = $$"""
Sending message to [{{outputTopicName}}] topic
and storing payload in [{{outputContainerName}}] container
""";
        var expectedColor = "red";
        var expectedText = "Text payload.";
        var expectedNumber = 100;
        var expectedBlobContent = $$"""
{ 
"color" :  {{expectedColor}} 
"text" :  {{expectedText}} 
"number" :  {{expectedNumber}}
 }
""";

        Assert.Equal(expectedNumberOfSBCalls, topicOutput.Invocations.Count);
        Assert.Equal(expectedCorrelationId, ((ServiceBusMessage)topicOutput.Invocations[0].Arguments[0]).CorrelationId);

        response?.HttpResponse?.Body?.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(response?.HttpResponse?.Body ?? new MemoryStream());
        var responseBody = reader.ReadToEnd();

        Assert.Equal(expectedHttpStatus, response?.HttpResponse?.StatusCode);
        Assert.StartsWith(expectedHttpBody.Replace("\r", ""), responseBody.Replace("\r", ""));
        
        Assert.Equal(expectedBlobContent.Replace("\r", ""), response?.BlobContent?.Replace("\r", ""));
    }
}
