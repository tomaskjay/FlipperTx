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

    [Fact]
    public async Task ArmWithoutPlanLoaded_ReceivesError()
    {
        await using var server = new SimulatedDeviceServer();
        server.Start();

        using var transport = new TcpTransport("127.0.0.1", server.Port);
        await transport.OpenAsync();

        await transport.SendAsync("CONNECT\r\n");
        await transport.ReadLineAsync();

        await transport.SendAsync("ARM\r\n");
        string line = await transport.ReadLineAsync();

        Assert.StartsWith("ERROR", line);
    }

    [Fact]
    public async Task FullHappyPath_RawProtocol_ConnectThroughStop()
    {
        await using var server = new SimulatedDeviceServer();
        server.Start();

        using var transport = new TcpTransport("127.0.0.1", server.Port);
        await transport.OpenAsync();

        await transport.SendAsync("CONNECT\r\n");
        Assert.Equal("CONNECTED", await transport.ReadLineAsync());

        await transport.SendAsync("LOAD_PLAN plan-1\r\n");
        Assert.Equal("PLAN_LOADED plan-1", await transport.ReadLineAsync());

        await transport.SendAsync("ARM\r\n");
        Assert.Equal("READY", await transport.ReadLineAsync());

        await transport.SendAsync("START\r\n");
        Assert.Equal("RUNNING", await transport.ReadLineAsync());

        await transport.SendAsync("STOP\r\n");
        Assert.Equal("STOPPED", await transport.ReadLineAsync());
    }
}
