using MedDeviceSim.Communication;

namespace MedDeviceSim.Workflow;

// Pure - no I/O, no async. Requesting an action only validates it against
// CurrentState and returns the DeviceCommand to send; CurrentState itself
// only changes once OnResponse confirms the device actually did it. An
// orchestrator (not built yet) is responsible for actually calling
// SerialTransport and feeding responses back in here.
public sealed class TreatmentWorkflow
{
    public TreatmentState CurrentState { get; private set; } = new TreatmentState.Disconnected();

    public WorkflowResult RequestConnect()
    {
        if (CurrentState is not TreatmentState.Disconnected)
        {
            return new WorkflowResult.Rejected($"Cannot connect while {StateName}.");
        }

        return new WorkflowResult.Accepted(new DeviceCommand.Connect());
    }

    public WorkflowResult RequestLoadPlan(string planId)
    {
        if (CurrentState is not TreatmentState.Connected)
        {
            return new WorkflowResult.Rejected($"Cannot load a plan while {StateName}.");
        }

        return new WorkflowResult.Accepted(new DeviceCommand.LoadPlan(planId));
    }

    public WorkflowResult RequestArm()
    {
        if (CurrentState is not TreatmentState.PlanLoaded)
        {
            return new WorkflowResult.Rejected($"Cannot arm while {StateName}.");
        }

        return new WorkflowResult.Accepted(new DeviceCommand.Arm());
    }

    public WorkflowResult RequestStart()
    {
        if (CurrentState is not TreatmentState.Armed)
        {
            return new WorkflowResult.Rejected($"Cannot start while {StateName}.");
        }

        return new WorkflowResult.Accepted(new DeviceCommand.Start());
    }

    public void OnResponse(DeviceResponse response)
    {
        CurrentState = (CurrentState, response) switch
        {
            (TreatmentState.Disconnected, DeviceResponse.Connected) =>
                new TreatmentState.Connected(),

            (TreatmentState.Connected, DeviceResponse.PlanLoaded planLoaded) =>
                new TreatmentState.PlanLoaded(planLoaded.PlanId),

            (TreatmentState.PlanLoaded planLoaded, DeviceResponse.Ready) =>
                new TreatmentState.Armed(planLoaded.PlanId),

            (TreatmentState.Armed armed, DeviceResponse.Running) =>
                new TreatmentState.Running(armed.PlanId, PercentComplete: 0),

            (TreatmentState.Running running, DeviceResponse.Progress progress) =>
                new TreatmentState.Running(running.PlanId, progress.Percent),

            (TreatmentState.Running running, DeviceResponse.Complete) =>
                new TreatmentState.Complete(running.PlanId),

            // A response that doesn't match an expected transition for the
            // current state is ignored for now - revisit once Fault
            // handling exists to give this a real, defined outcome.
            _ => CurrentState,
        };
    }

    private string StateName => CurrentState.GetType().Name;
}
