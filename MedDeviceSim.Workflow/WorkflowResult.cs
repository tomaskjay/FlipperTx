using MedDeviceSim.Communication;

namespace MedDeviceSim.Workflow;

// The outcome of asking the workflow "may I do X right now?" - never
// throws for an invalid action, since a user attempting something not
// currently allowed is an expected, normal outcome, not an exceptional one.
public abstract record WorkflowResult
{
    private WorkflowResult()
    {
    }

    public sealed record Accepted(DeviceCommand Command) : WorkflowResult;
    public sealed record Rejected(string Reason) : WorkflowResult;
}
