using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Workflow.Tests;

public class TreatmentWorkflowTests
{
    [Fact]
    public void InitialState_IsDisconnected()
    {
        var workflow = new TreatmentWorkflow();

        Assert.IsType<TreatmentState.Disconnected>(workflow.CurrentState);
    }

    [Fact]
    public void RequestConnect_WhileDisconnected_IsAcceptedWithConnectCommand()
    {
        var workflow = new TreatmentWorkflow();

        WorkflowResult result = workflow.RequestConnect();

        var accepted = Assert.IsType<WorkflowResult.Accepted>(result);
        Assert.IsType<DeviceCommand.Connect>(accepted.Command);
    }

    [Fact]
    public void RequestConnect_WhileAlreadyConnected_IsRejected()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());

        WorkflowResult result = workflow.RequestConnect();

        Assert.IsType<WorkflowResult.Rejected>(result);
    }

    [Fact]
    public void OnResponse_Connected_WhileDisconnected_TransitionsToConnected()
    {
        var workflow = new TreatmentWorkflow();

        workflow.OnResponse(new DeviceResponse.Connected());

        Assert.IsType<TreatmentState.Connected>(workflow.CurrentState);
    }

    [Fact]
    public void RequestLoadPlan_WhileConnected_IsAcceptedWithPlanId()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());

        WorkflowResult result = workflow.RequestLoadPlan("plan-1");

        var accepted = Assert.IsType<WorkflowResult.Accepted>(result);
        var command = Assert.IsType<DeviceCommand.LoadPlan>(accepted.Command);
        Assert.Equal("plan-1", command.PlanId);
    }

    [Fact]
    public void RequestLoadPlan_WhileDisconnected_IsRejected()
    {
        var workflow = new TreatmentWorkflow();

        WorkflowResult result = workflow.RequestLoadPlan("plan-1");

        Assert.IsType<WorkflowResult.Rejected>(result);
    }

    // Directly exercises the spec's example: "LOAD_PLAN should not be
    // accepted while treatment is running."
    [Fact]
    public void RequestLoadPlan_WhileRunning_IsRejected()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());
        workflow.OnResponse(new DeviceResponse.Running());

        WorkflowResult result = workflow.RequestLoadPlan("plan-2");

        Assert.IsType<WorkflowResult.Rejected>(result);
    }

    [Fact]
    public void OnResponse_PlanLoaded_WhileConnected_TransitionsToPlanLoadedWithId()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());

        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));

        var state = Assert.IsType<TreatmentState.PlanLoaded>(workflow.CurrentState);
        Assert.Equal("plan-1", state.PlanId);
    }

    // Directly exercises the spec's example: "ARM should fail if no plan
    // has been loaded."
    [Fact]
    public void RequestArm_WithoutPlanLoaded_IsRejected()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());

        WorkflowResult result = workflow.RequestArm();

        Assert.IsType<WorkflowResult.Rejected>(result);
    }

    [Fact]
    public void RequestArm_WithPlanLoaded_IsAccepted()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));

        WorkflowResult result = workflow.RequestArm();

        var accepted = Assert.IsType<WorkflowResult.Accepted>(result);
        Assert.IsType<DeviceCommand.Arm>(accepted.Command);
    }

    [Fact]
    public void OnResponse_Ready_WhilePlanLoaded_TransitionsToArmedWithSamePlanId()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));

        workflow.OnResponse(new DeviceResponse.Ready());

        var state = Assert.IsType<TreatmentState.Armed>(workflow.CurrentState);
        Assert.Equal("plan-1", state.PlanId);
    }

    // Directly exercises the spec's example: "START should fail if the
    // device has not been armed."
    [Fact]
    public void RequestStart_WithoutArming_IsRejected()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));

        WorkflowResult result = workflow.RequestStart();

        Assert.IsType<WorkflowResult.Rejected>(result);
    }

    [Fact]
    public void RequestStop_WhileArmed_IsAccepted()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());

        WorkflowResult result = workflow.RequestStop();

        var accepted = Assert.IsType<WorkflowResult.Accepted>(result);
        Assert.IsType<DeviceCommand.Stop>(accepted.Command);
    }

    [Fact]
    public void RequestStop_WhileRunning_IsAccepted()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());
        workflow.OnResponse(new DeviceResponse.Running());

        WorkflowResult result = workflow.RequestStop();

        Assert.IsType<WorkflowResult.Accepted>(result);
    }

    [Fact]
    public void RequestStop_WhileConnected_IsRejected()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());

        WorkflowResult result = workflow.RequestStop();

        Assert.IsType<WorkflowResult.Rejected>(result);
    }

    [Fact]
    public void OnResponse_Stopped_WhileArmed_TransitionsToStoppedWithPlanId()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());

        workflow.OnResponse(new DeviceResponse.Stopped());

        var state = Assert.IsType<TreatmentState.Stopped>(workflow.CurrentState);
        Assert.Equal("plan-1", state.PlanId);
    }

    [Fact]
    public void OnResponse_Stopped_WhileRunning_TransitionsToStoppedWithPlanId()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());
        workflow.OnResponse(new DeviceResponse.Running());

        workflow.OnResponse(new DeviceResponse.Stopped());

        var state = Assert.IsType<TreatmentState.Stopped>(workflow.CurrentState);
        Assert.Equal("plan-1", state.PlanId);
    }

    [Fact]
    public void OnResponse_Error_WhileRunning_TransitionsToFaultWithReason()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());
        workflow.OnResponse(new DeviceResponse.Running());

        workflow.OnResponse(new DeviceResponse.Error("Overheat detected"));

        var state = Assert.IsType<TreatmentState.Fault>(workflow.CurrentState);
        Assert.Equal("Overheat detected", state.Reason);
    }

    [Fact]
    public void OnResponse_Error_WhileConnected_TransitionsToFault()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());

        workflow.OnResponse(new DeviceResponse.Error("Unexpected state"));

        Assert.IsType<TreatmentState.Fault>(workflow.CurrentState);
    }

    [Fact]
    public void OnDisconnected_WhileRunning_ForcesDisconnected()
    {
        var workflow = new TreatmentWorkflow();
        workflow.OnResponse(new DeviceResponse.Connected());
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));
        workflow.OnResponse(new DeviceResponse.Ready());
        workflow.OnResponse(new DeviceResponse.Running());

        workflow.OnDisconnected();

        Assert.IsType<TreatmentState.Disconnected>(workflow.CurrentState);
    }

    [Fact]
    public void OnDisconnected_WhileAlreadyDisconnected_StaysDisconnected()
    {
        var workflow = new TreatmentWorkflow();

        workflow.OnDisconnected();

        Assert.IsType<TreatmentState.Disconnected>(workflow.CurrentState);
    }

    [Fact]
    public void FullHappyPath_ConnectThroughComplete_EndsInCompleteWithPlanId()
    {
        var workflow = new TreatmentWorkflow();

        Assert.IsType<WorkflowResult.Accepted>(workflow.RequestConnect());
        workflow.OnResponse(new DeviceResponse.Connected());

        Assert.IsType<WorkflowResult.Accepted>(workflow.RequestLoadPlan("plan-1"));
        workflow.OnResponse(new DeviceResponse.PlanLoaded("plan-1"));

        Assert.IsType<WorkflowResult.Accepted>(workflow.RequestArm());
        workflow.OnResponse(new DeviceResponse.Ready());

        Assert.IsType<WorkflowResult.Accepted>(workflow.RequestStart());
        workflow.OnResponse(new DeviceResponse.Running());

        workflow.OnResponse(new DeviceResponse.Progress(50));
        var running = Assert.IsType<TreatmentState.Running>(workflow.CurrentState);
        Assert.Equal(50, running.PercentComplete);

        workflow.OnResponse(new DeviceResponse.Complete());
        var complete = Assert.IsType<TreatmentState.Complete>(workflow.CurrentState);
        Assert.Equal("plan-1", complete.PlanId);
    }

    [Fact]
    public void OnResponse_UnexpectedForCurrentState_IsIgnored()
    {
        var workflow = new TreatmentWorkflow();

        workflow.OnResponse(new DeviceResponse.Progress(50));

        Assert.IsType<TreatmentState.Disconnected>(workflow.CurrentState);
    }
}
