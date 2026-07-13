using MedDeviceSim.Communication;

namespace MedDeviceSim.Session;

// Mirrors WorkflowResult's shape, but at the session level the interesting
// question shifts from "what would we send" to "what came back": Sent
// carries the parsed DeviceResponse that resulted from the exchange, not
// just confirmation that a command went out.
public abstract record SessionResult
{
    private SessionResult()
    {
    }

    public sealed record Sent(DeviceResponse Response) : SessionResult;
    public sealed record Rejected(string Reason) : SessionResult;
}
