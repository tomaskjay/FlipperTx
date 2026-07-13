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

    [Fact]
    public async Task Start_WithoutStopping_AutonomouslyEmitsProgressThenComplete()
    {
        await using var server = new SimulatedDeviceServer(progressInterval: TimeSpan.FromMilliseconds(20));
        server.Start();

        using var transport = new TcpTransport("127.0.0.1", server.Port);
        await transport.OpenAsync();

        await transport.SendAsync("CONNECT\r\n");
        await transport.ReadLineAsync();
        await transport.SendAsync("LOAD_PLAN plan-1\r\n");
        await transport.ReadLineAsync();
        await transport.SendAsync("ARM\r\n");
        await transport.ReadLineAsync();

        await transport.SendAsync("START\r\n");
        Assert.Equal("RUNNING", await transport.ReadLineAsync());

        // Nothing further was sent - these all arrive unprompted.
        Assert.Equal("PROGRESS 25", await transport.ReadLineAsync());
        Assert.Equal("PROGRESS 50", await transport.ReadLineAsync());
        Assert.Equal("PROGRESS 75", await transport.ReadLineAsync());
        Assert.Equal("COMPLETE", await transport.ReadLineAsync());
    }

    [Fact]
    public async Task Stop_DuringRun_CancelsRemainingProgressAndComplete()
    {
        await using var server = new SimulatedDeviceServer(progressInterval: TimeSpan.FromMilliseconds(20));
        server.Start();

        using var transport = new TcpTransport("127.0.0.1", server.Port);
        await transport.OpenAsync();

        await transport.SendAsync("CONNECT\r\n");
        await transport.ReadLineAsync();
        await transport.SendAsync("LOAD_PLAN plan-1\r\n");
        await transport.ReadLineAsync();
        await transport.SendAsync("ARM\r\n");
        await transport.ReadLineAsync();
        await transport.SendAsync("START\r\n");
        await transport.ReadLineAsync();

        // Let at least one PROGRESS arrive, then interrupt the run.
        Assert.Equal("PROGRESS 25", await transport.ReadLineAsync());
        await transport.SendAsync("STOP\r\n");
        Assert.Equal("STOPPED", await transport.ReadLineAsync());

        // Confirm no further PROGRESS/COMPLETE follows - a read that times
        // out (rather than returning more data) is the expected outcome.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAsync<OperationCanceledException>(() => transport.ReadLineAsync(cts.Token));
    }
}
