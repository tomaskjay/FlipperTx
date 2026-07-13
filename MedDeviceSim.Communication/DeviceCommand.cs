namespace MedDeviceSim.Communication;

// Mirrors DeviceResponse's closed-hierarchy shape (private constructor, so
// only nested types can derive from this). Unlike DeviceResponse.Parse,
// there's no "malformed input" case to handle here - we're producing the
// wire format ourselves, not parsing untrusted device output.
public abstract record DeviceCommand
{
    private DeviceCommand()
    {
    }

    public sealed record Connect : DeviceCommand;
    public sealed record LoadPlan(string PlanId) : DeviceCommand;
    public sealed record Arm : DeviceCommand;
    public sealed record Start : DeviceCommand;
    public sealed record Stop : DeviceCommand;
    public sealed record GetStatus : DeviceCommand;

    public string ToWireFormat() => this switch
    {
        Connect => "CONNECT\r\n",
        LoadPlan loadPlan => $"LOAD_PLAN {loadPlan.PlanId}\r\n",
        Arm => "ARM\r\n",
        Start => "START\r\n",
        Stop => "STOP\r\n",
        GetStatus => "GET_STATUS\r\n",
        _ => throw new NotSupportedException($"Unhandled {nameof(DeviceCommand)} type: {GetType()}"),
    };
}
