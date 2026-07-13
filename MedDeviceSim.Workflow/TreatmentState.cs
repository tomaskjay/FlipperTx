namespace MedDeviceSim.Workflow;

// Same closed-hierarchy shape as DeviceResponse/DeviceCommand in
// MedDeviceSim.Communication. PlanLoaded/Armed/Running carry the plan ID
// forward so later states know which plan is active; Running also carries
// the last known progress percentage.
public abstract record TreatmentState
{
    private TreatmentState()
    {
    }

    public sealed record Disconnected : TreatmentState;
    public sealed record Connected : TreatmentState;
    public sealed record PlanLoaded(string PlanId) : TreatmentState;
    public sealed record Armed(string PlanId) : TreatmentState;
    public sealed record Running(string PlanId, int PercentComplete) : TreatmentState;
    public sealed record Complete(string PlanId) : TreatmentState;
    public sealed record Stopped(string PlanId) : TreatmentState;
    public sealed record Fault(string Reason) : TreatmentState;
}
