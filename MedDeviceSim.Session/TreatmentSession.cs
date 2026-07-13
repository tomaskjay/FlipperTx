using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Session;

// Bridges the pure TreatmentWorkflow to a real SerialTransport. Option A
// shape (decided explicitly, not defaulted to): one request/response
// exchange at a time - send a command, read exactly one line back, parse
// and feed it to the workflow, return. No persistent background read loop;
// that's deferred until something concrete (e.g. live UI progress updates)
// actually needs it, since it would require making TreatmentWorkflow
// thread-safe, which nothing justifies yet.
//
// GET_STATUS is intentionally not wired up here - TreatmentWorkflow never
// got a RequestGetStatus(), since a status query doesn't fit the
// "valid-from-exactly-one-state" pattern the other actions share. Left as
// an explicit gap, not forgotten.
//
// Takes ownership of the SerialTransport it's given (disposes it), since
// this class directs the transport's lifecycle for the life of a session -
// same reasoning SerialTransport uses for the SerialPort it constructs.
public sealed class TreatmentSession : IDisposable
{
    private readonly SerialTransport _transport;
    private readonly TreatmentWorkflow _workflow = new();

    public TreatmentSession(SerialTransport transport)
    {
        _transport = transport;
    }

    public TreatmentState CurrentState => _workflow.CurrentState;

    public Task<SessionResult> ConnectAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(_workflow.RequestConnect(), cancellationToken);

    public Task<SessionResult> LoadPlanAsync(string planId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(_workflow.RequestLoadPlan(planId), cancellationToken);

    public Task<SessionResult> ArmAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(_workflow.RequestArm(), cancellationToken);

    public Task<SessionResult> StartAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(_workflow.RequestStart(), cancellationToken);

    public Task<SessionResult> StopAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(_workflow.RequestStop(), cancellationToken);

    private async Task<SessionResult> ExecuteAsync(WorkflowResult requestResult, CancellationToken cancellationToken)
    {
        if (requestResult is WorkflowResult.Rejected rejected)
        {
            return new SessionResult.Rejected(rejected.Reason);
        }

        var accepted = (WorkflowResult.Accepted)requestResult;
        await _transport.SendAsync(accepted.Command.ToWireFormat(), cancellationToken);

        string line = await _transport.ReadLineAsync(cancellationToken);
        DeviceResponse response = DeviceResponse.Parse(line);
        _workflow.OnResponse(response);

        return new SessionResult.Sent(response);
    }

    public void Dispose()
    {
        _transport.Dispose();
        _workflow.OnDisconnected();
    }
}
