using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Session;

// Bridges the pure TreatmentWorkflow to an ITransport. Option A
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
// Takes ownership of the ITransport it's given (disposes it), since this
// class directs the transport's lifecycle for the life of a session - same
// reasoning SerialTransport uses for the SerialPort it constructs.
//
// Depends on the ITransport interface, not the concrete SerialTransport -
// this is what lets a test substitute a fake in place of real hardware.
public sealed class TreatmentSession : IDisposable
{
    private readonly ITransport _transport;
    private readonly TreatmentWorkflow _workflow = new();

    public TreatmentSession(ITransport transport)
    {
        _transport = transport;
    }

    public TreatmentState CurrentState => _workflow.CurrentState;

    // Opens the underlying COM port itself - separate from ConnectAsync(),
    // which sends the CONNECT protocol command. Must be called first; a
    // real SerialTransport's Send/Read would otherwise throw, since the
    // port was never opened. Fake transports in tests don't check this, so
    // this gap went unnoticed until a real transport actually needed it.
    public Task OpenAsync(CancellationToken cancellationToken = default) =>
        _transport.OpenAsync(cancellationToken);

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

        try
        {
            await _transport.SendAsync(accepted.Command.ToWireFormat(), cancellationToken);

            string line = await _transport.ReadLineAsync(cancellationToken);
            DeviceResponse response = DeviceResponse.Parse(line);
            _workflow.OnResponse(response);

            return new SessionResult.Sent(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Our own caller's token fired - deliberate cancellation, not a
            // communication failure. Propagate normally, matching the
            // standard .NET convention for cancellation.
            throw;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            // Anything else here - a write/read failure, or an
            // OperationCanceledException NOT caused by our own token (e.g.
            // a physical disconnect mid-read, per Phase 1's finding) - is a
            // genuine communication failure. Per spec, this must land in a
            // defined, safe state, not an undefined one.
            _workflow.OnDisconnected();
            return new SessionResult.CommunicationFailed(ex.Message);
        }
    }

    public void Dispose()
    {
        _transport.Dispose();
        _workflow.OnDisconnected();
    }
}
