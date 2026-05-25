using H.Pipes;
using MessagePack;

namespace H.Ipc.Generator.IntegrationTests;

[TestClass]
public class IpcGeneratorIntegrationTests
{
    [TestMethod]
    public async Task GeneratesIpcClientAndIpcServerCorrectly()
    {
        const string serverName = "H.Ipc";
        // Server initialization
        await using var server = new PipeServer<string>(serverName);
        var service = new ActionService();
        Exception? serverException = null;
        service.ExceptionOccurred += (_, exception) => serverException = exception;
        service.Initialize(server);
        await server.StartAsync();

        // Client initialization
        await using var client = new PipeClient<string>(serverName);
        var serviceClient = new ActionServiceClient();
        Exception? clientException = null;
        serviceClient.ExceptionOccurred += (_, exception) => clientException = exception;
        serviceClient.Initialize(client);
        await client.ConnectAsync();

        // Client usage
        await serviceClient.ShowTrayIcon();
        await serviceClient.HideTrayIcon();
        await serviceClient.SendText("Hello, World!");

        DataPoint dataPointResult;
        try
        {
            dataPointResult = await serviceClient.GetMeasurement();
        }
        catch
        {
            serverException.Should().BeNull();
            clientException.Should().BeNull();
            throw;
        }
        serverException.Should().BeNull();
        clientException.Should().BeNull();
        dataPointResult.X.Should().Be(10);
        dataPointResult.Y.Should().Be(20);
        dataPointResult.Temperature.Should().Be(25);

        var result = await serviceClient.GetPoints();
        result.Should().Be(42);

        await Task.Delay(TimeSpan.FromSeconds(5));

        service.ShowTrayIconResult.Should().BeTrue();
        service.HideTrayIconResult.Should().BeTrue();
        service.SendTextResult.Should().Be("Hello, World!");
    }
}
