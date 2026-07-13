using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Session;

// Bridges the pure TreatmentWorkflow to an ITransport. Option A
// shape (decided explicitly, not defaulted to): one request/response
// exchange at a time - send a command, read lines (applying any unsolicited
// PROGRESS/COMPLETE along the way - see ReadCommandReplyAsync) until the
// actual reply arrives, parse it, feed it to the workflow, return. No
// persistent background read loop; that's deferred until something
// concrete (e.g. live progress updates while nothing is actively calling
// in) actually needs it, since it would require making TreatmentWorkflow
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

    // Whether the underlying transport (the physical COM port) is open -
    // deliberately separate from CurrentState, which reflects the custom
    // protocol workflow. A real device that doesn't speak our protocol can
    // have the transport open while the workflow never reaches Connected.
    public bool IsOpen => _transport.IsOpen;

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

    // Waits for and processes the next unsolicited line from the device -
    // e.g. a PROGRESS update or COMPLETE arriving on its own while Running,
    // not in direct response to any request. A caller loops on this after
    // a successful StartAsync() to observe the rest of a run. Still
    // caller-driven, not a background task - CurrentState only ever
    // changes on whatever thread the caller is actually running on, same
    // single-threaded model as every other method here.
    public Task<SessionResult> ReadNextUpdateAsync(CancellationToken cancellationToken = default) =>
        ReadAndProcessOneResponseAsync(cancellationToken);

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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            _workflow.OnDisconnected();
            return new SessionResult.CommunicationFailed(ex.Message);
        }

        return await ReadCommandReplyAsync(cancellationToken);
    }

    // After sending a command, the next line off the wire might instead be
    // an unsolicited PROGRESS/COMPLETE that the device wrote on its own
    // schedule while nothing was reading (confirmed live: STOP's reply sat
    // behind an already-queued PROGRESS line, and the naive one-line read
    // consumed the PROGRESS line as if it were STOP's reply). PROGRESS and
    // COMPLETE are the only response kinds the current protocol ever sends
    // unprompted - anything else is guaranteed to be a direct reply - so
    // classifying by response type is enough here; keep applying and
    // skipping unsolicited lines until the actual reply arrives.
    private async Task<SessionResult> ReadCommandReplyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            SessionResult result = await ReadAndProcessOneResponseAsync(cancellationToken);

            if (result is not SessionResult.Sent { Response: DeviceResponse.Progress or DeviceResponse.Complete })
            {
                return result;
            }
        }
    }

    private async Task<SessionResult> ReadAndProcessOneResponseAsync(CancellationToken cancellationToken)
    {
        try
        {
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
