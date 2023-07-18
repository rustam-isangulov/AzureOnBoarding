using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using Moq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ProcessRed.Tests.Unit;

public class ServiceBusTriggerFunctionTests
{
    [Fact]
    public void ServiceBusTrigger_ShouldWriteBlob_WhenCalledWithGoodMessage()
    {
        // Arrange

        var serviceBusMessageApplicationProperties = new Dictionary<string, object?>
        {
            { "color", "red" },
            { "num", 101 }
        };

        var context = new Mock<FunctionContext>();
        context.Setup(r => r.BindingContext).Returns(() =>
            {
                var bindingContext = new Mock<BindingContext>();
                bindingContext.Setup(r => r.BindingData).Returns(() =>
                new ReadOnlyDictionary<string, object?>(
                    new Dictionary<string, object?>
                        {
                            {"ApplicationProperties",JsonSerializer.Serialize(
                                serviceBusMessageApplicationProperties,
                                new JsonSerializerOptions
                                {
                                    Converters = { new ProcessRed.ObjectToInferredTypesConverter() }
                                })
                            }
                        }));

                return bindingContext.Object;
            });

        // Act

        var process = new ServiceBusTrigger(new NullLoggerFactory());
        var blobContent = process.Run("test", context.Object);

        // Assert

        Assert.Equal("{\"color\":\"red\",\"num\":101}", blobContent);
    }
}