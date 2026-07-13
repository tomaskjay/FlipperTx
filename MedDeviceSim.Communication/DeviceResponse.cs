namespace MedDeviceSim.Communication;

// Private constructor means only types nested inside this one can derive
// from it (they have access to private members of their enclosing type) -
// the closest C# gets to a closed/sealed hierarchy without a language
// feature for it. Anything outside this file cannot add new response kinds.
public abstract record DeviceResponse
{
    private DeviceResponse()
    {
    }

    public sealed record Connected : DeviceResponse;
    public sealed record PlanLoaded(string PlanId) : DeviceResponse;
    public sealed record Ready : DeviceResponse;
    public sealed record Running : DeviceResponse;
    public sealed record Progress(int Percent) : DeviceResponse;
    public sealed record Complete : DeviceResponse;
    public sealed record Stopped : DeviceResponse;
    public sealed record Error(string Reason) : DeviceResponse;

    // A line that didn't match any known response shape. Parsing never
    // throws - malformed/unrecognized device output is an expected outcome
    // here, not an exceptional one, so callers can pattern-match on it like
    // any other response instead of needing try/catch.
    public sealed record Unknown(string RawLine) : DeviceResponse;

    public static DeviceResponse Parse(string line)
    {
        string trimmed = line.Trim();
        string[] parts = trimmed.Split(' ', 2);
        string keyword = parts[0];
        string? rest = parts.Length > 1 ? parts[1] : null;

        return keyword switch
        {
            "CONNECTED" when rest is null => new Connected(),
            "PLAN_LOADED" when rest is not null => new PlanLoaded(rest),
            "READY" when rest is null => new Ready(),
            "RUNNING" when rest is null => new Running(),
            "PROGRESS" when rest is not null && int.TryParse(rest, out int percent) => new Progress(percent),
            "COMPLETE" when rest is null => new Complete(),
            "STOPPED" when rest is null => new Stopped(),
            "ERROR" when rest is not null => new Error(rest),
            _ => new Unknown(trimmed),
        };
    }
}
