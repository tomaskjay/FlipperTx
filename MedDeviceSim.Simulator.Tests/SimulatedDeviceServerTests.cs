using MedDeviceSim.Communication;
using MedDeviceSim.Simulator;

namespace MedDeviceSim.Simulator.Tests;

public class SimulatedDeviceServerTests
{
    [Fact]
    public async Task ConnectAndSendConnect_ReceivesConnected()
    {
        await using var server = new SimulatedDeviceServer();
        server.Start();

        using var transport = new TcpTransport("127.0.0.1", server.Port);
        await transport.OpenAsync();

        await transport.SendAsync("CONNECT\r\n");
        string line = await transport.ReadLineAsync();

        Assert.Equal("CONNECTED", line);
    }

    [Fact]
    public async Task SendUnrecognizedCommand_ReceivesError()
    {
        await using var server = new SimulatedDeviceServer();
        server.Start();

        using var transport = new TcpTransport("127.0.0.1", server.Port);
        await transport.OpenAsync();

        await transport.SendAsync("ARM\r\n");
        string line = await transport.ReadLineAsync();

        Assert.StartsWith("ERROR", line);
    }
}
