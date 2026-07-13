using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Session.Tests;

public class TreatmentSessionTests
{
    [Fact]
    public async Task ConnectAsync_WhenAccepted_SendsConnectAndUpdatesState()
    {
        var fake = new FakeTransport();
        fake.EnqueueLine("CONNECTED");
        using var session = new TreatmentSession(fake);

        SessionResult result = await session.ConnectAsync();

        var sent = Assert.IsType<SessionResult.Sent>(result);
        Assert.IsType<DeviceResponse.Connected>(sent.Response);
        Assert.Contains("CONNECT\r\n", fake.SentText);
        Assert.IsType<TreatmentState.Connected>(session.CurrentState);
    }

    [Fact]
    public async Task ArmAsync_WithoutPlanLoaded_IsRejectedAndSendsNothing()
    {
        var fake = new FakeTransport();
        using var session = new TreatmentSession(fake);

        SessionResult result = await session.ArmAsync();

        Assert.IsType<SessionResult.Rejected>(result);
        Assert.Empty(fake.SentText);
    }

    [Fact]
    public async Task FullHappyPath_ConnectThroughRunning_ProgressesStateEachStep()
    {
        var fake = new FakeTransport();
        fake.EnqueueLine("CONNECTED");
        fake.EnqueueLine("PLAN_LOADED plan-1");
        fake.EnqueueLine("READY");
        fake.EnqueueLine("RUNNING");
        using var session = new TreatmentSession(fake);

        await session.ConnectAsync();
        Assert.IsType<TreatmentState.Connected>(session.CurrentState);

        await session.LoadPlanAsync("plan-1");
        Assert.IsType<TreatmentState.PlanLoaded>(session.CurrentState);

        await session.ArmAsync();
        Assert.IsType<TreatmentState.Armed>(session.CurrentState);

        await session.StartAsync();
        var running = Assert.IsType<TreatmentState.Running>(session.CurrentState);
        Assert.Equal("plan-1", running.PlanId);

        Assert.Equal(["CONNECT\r\n", "LOAD_PLAN plan-1\r\n", "ARM\r\n", "START\r\n"], fake.SentText);
    }
}
