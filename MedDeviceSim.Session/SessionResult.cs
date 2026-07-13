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

    // The action was valid and we attempted it, but talking to the device
    // itself failed (write/read error, or an unexpected disconnect) -
    // distinct from Rejected, where nothing was ever attempted.
    public sealed record CommunicationFailed(string Reason) : SessionResult;
}
