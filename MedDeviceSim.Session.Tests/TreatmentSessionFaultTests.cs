using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Session.Tests;

public class TreatmentSessionFaultTests
{
    private static async Task<TreatmentSession> ConnectedAndPlanLoadedAsync(FakeTransport fake)
    {
        fake.EnqueueLine("CONNECTED");
        fake.EnqueueLine("PLAN_LOADED plan-1");
        var session = new TreatmentSession(fake);
        await session.ConnectAsync();
        await session.LoadPlanAsync("plan-1");
        return session;
    }

    [Fact]
    public async Task ArmAsync_WhenTransportTimesOut_ReturnsCommunicationFailedAndForcesDisconnected()
    {
        var fake = new FakeTransport();
        using var session = await ConnectedAndPlanLoadedAsync(fake);
        fake.ThrowOnNextRead(new TimeoutException("simulated timeout"));

        SessionResult result = await session.ArmAsync();

        var failed = Assert.IsType<SessionResult.CommunicationFailed>(result);
        Assert.Contains("simulated timeout", failed.Reason);
        Assert.IsType<TreatmentState.Disconnected>(session.CurrentState);
    }

    [Fact]
    public async Task ArmAsync_WhenUnexpectedDisconnectOccurs_ReturnsCommunicationFailedAndForcesDisconnected()
    {
        var fake = new FakeTransport();
        using var session = await ConnectedAndPlanLoadedAsync(fake);
        // Not tied to any CancellationToken we control - simulates a real
        // physical disconnect (Phase 1 proved this exact exception type is
        // what a mid-read unplug throws), not deliberate cancellation.
        fake.ThrowOnNextRead(new OperationCanceledException("simulated physical disconnect"));

        SessionResult result = await session.ArmAsync();

        Assert.IsType<SessionResult.CommunicationFailed>(result);
        Assert.IsType<TreatmentState.Disconnected>(session.CurrentState);
    }

    [Fact]
    public async Task ArmAsync_WhenCallerCancels_PropagatesOperationCanceledExceptionInstead()
    {
        var fake = new FakeTransport();
        using var session = await ConnectedAndPlanLoadedAsync(fake);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        fake.ThrowOnNextRead(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => session.ArmAsync(cts.Token));
    }

    [Fact]
    public async Task ArmAsync_WhenResponseIsMalformed_ReturnsSentWithUnknownAndLeavesStateUnchanged()
    {
        var fake = new FakeTransport();
        using var session = await ConnectedAndPlanLoadedAsync(fake);
        fake.EnqueueLine("GARBAGE_NONSENSE");

        SessionResult result = await session.ArmAsync();

        var sent = Assert.IsType<SessionResult.Sent>(result);
        Assert.IsType<DeviceResponse.Unknown>(sent.Response);
        // Arm was sent but never confirmed - state stays at PlanLoaded.
        Assert.IsType<TreatmentState.PlanLoaded>(session.CurrentState);
    }

    [Fact]
    public async Task ArmAsync_WhenDeviceReportsError_TransitionsToFault()
    {
        var fake = new FakeTransport();
        using var session = await ConnectedAndPlanLoadedAsync(fake);
        fake.EnqueueLine("ERROR Overheat detected");

        SessionResult result = await session.ArmAsync();

        var sent = Assert.IsType<SessionResult.Sent>(result);
        var error = Assert.IsType<DeviceResponse.Error>(sent.Response);
        Assert.Equal("Overheat detected", error.Reason);
        var fault = Assert.IsType<TreatmentState.Fault>(session.CurrentState);
        Assert.Equal("Overheat detected", fault.Reason);
    }
}
