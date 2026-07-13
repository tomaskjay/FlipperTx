using System.Net;
using System.Net.Sockets;
using System.Text;
using MedDeviceSim.Communication;
using MedDeviceSim.Workflow;

namespace MedDeviceSim.Simulator;

// Tracks its own state per connection and enforces real rules - rejecting
// ARM without a loaded plan, LOAD_PLAN before connecting, etc. - the way an
// actual device should. Reuses TreatmentState purely as a data shape (the
// states a treatment goes through are the same regardless of which side is
// looking at them); the transition RULES below are an independent,
// separately-written implementation, not a call into TreatmentWorkflow -
// see the Phase 6 checkpoint 1 design discussion for why: an integration
// test against an independently-implemented device is a meaningfully
// stronger test than one where both sides share the same rules by
// construction.
//
// Checkpoint-2 scope: START responds with a single RUNNING, immediately.
// No autonomous, timed PROGRESS/COMPLETE simulation yet - TreatmentSession
// can't currently consume unsolicited updates after its one read per
// action, so building that now would be for a client that can't use it
// yet. That's checkpoint 3, alongside extending TreatmentSession itself.
//
// Handles one client connection at a time, matching how a real device
// would only have one active session - not a general-purpose concurrent
// TCP server.
public sealed class SimulatedDeviceServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    // port = 0 lets the OS assign an available port - used by tests to
    // avoid collisions; the standalone host passes an explicit port.
    public SimulatedDeviceServer(int port = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await HandleClientAsync(client, cancellationToken);
        }
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            var lineReader = new LineReader(stream);
            TreatmentState state = new TreatmentState.Disconnected();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string line = await lineReader.ReadLineAsync(cancellationToken);
                    string response;
                    (state, response) = HandleLine(line, state);
                    byte[] bytes = Encoding.ASCII.GetBytes(response);
                    await stream.WriteAsync(bytes, cancellationToken);
                }
            }
            catch (IOException)
            {
                // Client disconnected - not an error for the server itself.
            }
            catch (OperationCanceledException)
            {
                // Server is shutting down.
            }
        }
    }

    private static (TreatmentState NewState, string Response) HandleLine(string line, TreatmentState state)
    {
        string trimmed = line.Trim();
        string[] parts = trimmed.Split(' ', 2);
        string keyword = parts[0];
        string? arg = parts.Length > 1 ? parts[1] : null;

        return (keyword, state) switch
        {
            ("CONNECT", TreatmentState.Disconnected) =>
                (new TreatmentState.Connected(), "CONNECTED\r\n"),

            ("LOAD_PLAN", TreatmentState.Connected) when arg is not null =>
                (new TreatmentState.PlanLoaded(arg), $"PLAN_LOADED {arg}\r\n"),

            ("ARM", TreatmentState.PlanLoaded planLoaded) =>
                (new TreatmentState.Armed(planLoaded.PlanId), "READY\r\n"),

            ("START", TreatmentState.Armed armed) =>
                (new TreatmentState.Running(armed.PlanId, 0), "RUNNING\r\n"),

            ("STOP", TreatmentState.Armed armed) =>
                (new TreatmentState.Stopped(armed.PlanId), "STOPPED\r\n"),

            ("STOP", TreatmentState.Running running) =>
                (new TreatmentState.Stopped(running.PlanId), "STOPPED\r\n"),

            _ => (state, $"ERROR Cannot process '{trimmed}' while {state}\r\n"),
        };
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener.Stop();

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch
            {
                // Best-effort shutdown - the accept loop's own exception
                // handling already covers expected cases.
            }
        }
    }
}
