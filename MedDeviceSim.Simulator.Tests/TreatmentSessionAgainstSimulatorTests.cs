using MedDeviceSim.Communication;
using MedDeviceSim.Session;
using MedDeviceSim.Simulator;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Simulator.Tests;

// The flagship test this checkpoint exists for: TreatmentSession and
// TreatmentWorkflow (the host side, built in Phases 3-5) driven against
// SimulatedDeviceServer (the device side, built independently in this
// phase) over a real TCP socket. Two separately-reasoned-about
// implementations of the same protocol agreeing is a meaningfully stronger
// proof than either one tested alone.
public class TreatmentSessionAgainstSimulatorTests
{
    [Fact]
    public async Task FullHappyPath_ConnectThroughRunning_AgainstRealSimulator()
    {
        await using var server = new SimulatedDeviceServer();
        server.Start();

        var transport = new TcpTransport("127.0.0.1", server.Port);
        using var session = new TreatmentSession(transport);

        await session.OpenAsync();

        SessionResult connectResult = await session.ConnectAsync();
        Assert.IsType<SessionResult.Sent>(connectResult);
        Assert.IsType<TreatmentState.Connected>(session.CurrentState);

        SessionResult loadPlanResult = await session.LoadPlanAsync("plan-1");
        Assert.IsType<SessionResult.Sent>(loadPlanResult);
        var planLoaded = Assert.IsType<TreatmentState.PlanLoaded>(session.CurrentState);
        Assert.Equal("plan-1", planLoaded.PlanId);

        await session.ArmAsync();
        var armed = Assert.IsType<TreatmentState.Armed>(session.CurrentState);
        Assert.Equal("plan-1", armed.PlanId);

        await session.StartAsync();
        var running = Assert.IsType<TreatmentState.Running>(session.CurrentState);
        Assert.Equal("plan-1", running.PlanId);
    }

    [Fact]
    public async Task FullHappyPath_ConnectThroughComplete_AgainstRealSimulator()
    {
        await using var server = new SimulatedDeviceServer(progressInterval: TimeSpan.FromMilliseconds(20));
        server.Start();

        var transport = new TcpTransport("127.0.0.1", server.Port);
        using var session = new TreatmentSession(transport);

        await session.OpenAsync();
        await session.ConnectAsync();
        await session.LoadPlanAsync("plan-1");
        await session.ArmAsync();
        await session.StartAsync();
        Assert.IsType<TreatmentState.Running>(session.CurrentState);

        // PROGRESS/COMPLETE arrive unprompted - loop on ReadNextUpdateAsync
        // to observe them, with a bound so a real failure shows up as a
        // clear assertion failure instead of an infinite loop.
        for (int i = 0; i < 10 && session.CurrentState is not TreatmentState.Complete; i++)
        {
            await session.ReadNextUpdateAsync();
        }

        var complete = Assert.IsType<TreatmentState.Complete>(session.CurrentState);
        Assert.Equal("plan-1", complete.PlanId);
    }

    // Reproduces a bug found via live manual testing: StopAsync() was
    // consuming an already-queued, unsolicited PROGRESS line as if it were
    // STOP's own reply, leaving CurrentState stuck on Running instead of
    // advancing to Stopped. The delay below gives the simulator's
    // autonomous run task time to write at least one PROGRESS line before
    // StopAsync ever reads anything, so the read really does have to skip
    // past it.
    [Fact]
    public async Task StopAsync_WhileUnreadProgressIsQueued_StillReachesStopped()
    {
        await using var server = new SimulatedDeviceServer(progressInterval: TimeSpan.FromMilliseconds(20));
        server.Start();

        var transport = new TcpTransport("127.0.0.1", server.Port);
        using var session = new TreatmentSession(transport);

        await session.OpenAsync();
        await session.ConnectAsync();
        await session.LoadPlanAsync("plan-1");
        await session.ArmAsync();
        await session.StartAsync();

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        SessionResult result = await session.StopAsync();

        Assert.IsType<SessionResult.Sent>(result);
        var stopped = Assert.IsType<TreatmentState.Stopped>(session.CurrentState);
        Assert.Equal("plan-1", stopped.PlanId);
    }

    [Fact]
    public async Task ArmAsync_WithoutPlanLoaded_IsRejectedByWorkflow_NeverReachesSimulator()
    {
        await using var server = new SimulatedDeviceServer();
        server.Start();

        var transport = new TcpTransport("127.0.0.1", server.Port);
        using var session = new TreatmentSession(transport);

        await session.OpenAsync();
        await session.ConnectAsync();

        SessionResult result = await session.ArmAsync();

        Assert.IsType<SessionResult.Rejected>(result);
        // Still Connected, not something the simulator would have said -
        // confirms TreatmentWorkflow rejected this locally, matching REQ:
        // the UI (and everything above the workflow) cannot bypass it.
        Assert.IsType<TreatmentState.Connected>(session.CurrentState);
    }
}
