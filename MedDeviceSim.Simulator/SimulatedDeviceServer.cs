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
// After START, autonomously emits PROGRESS updates and COMPLETE over time,
// unprompted - while the same connection's read loop keeps watching for an
// incoming STOP that should cancel the run early. Two tasks writing to the
// same stream need synchronization (a SemaphoreSlim, since C#'s lock can't
// wrap an await) to avoid interleaving bytes and corrupting the line
// protocol.
//
// Handles one client connection at a time, matching how a real device
// would only have one active session - not a general-purpose concurrent
// TCP server.
public sealed class SimulatedDeviceServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly TimeSpan _progressInterval;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    // port = 0 lets the OS assign an available port - used by tests to
    // avoid collisions; the standalone host passes an explicit port.
    // progressInterval defaults to something realistic for manual/demo use;
    // tests pass a short interval so they don't take seconds each.
    public SimulatedDeviceServer(int port = 0, TimeSpan? progressInterval = null)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _progressInterval = progressInterval ?? TimeSpan.FromSeconds(1);
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

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            var lineReader = new LineReader(stream);
            var writeLock = new SemaphoreSlim(1, 1);
            TreatmentState state = new TreatmentState.Disconnected();
            CancellationTokenSource? runCts = null;
            Task? runTask = null;

            async Task WriteLineAsync(string line, CancellationToken ct)
            {
                await writeLock.WaitAsync(ct);
                try
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(line);
                    await stream.WriteAsync(bytes, ct);
                }
                finally
                {
                    writeLock.Release();
                }
            }

            async Task RunSimulatedTreatmentAsync(string planId, CancellationToken ct)
            {
                try
                {
                    for (int percent = 25; percent < 100; percent += 25)
                    {
                        await Task.Delay(_progressInterval, ct);
                        await WriteLineAsync($"PROGRESS {percent}\r\n", ct);
                    }

                    await Task.Delay(_progressInterval, ct);
                    await WriteLineAsync("COMPLETE\r\n", ct);
                }
                catch (OperationCanceledException)
                {
                    // Stopped early, or the connection/server is closing -
                    // nothing more to send.
                }
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string line = await lineReader.ReadLineAsync(cancellationToken);
                    TreatmentState previousState = state;
                    string response;
                    (state, response) = HandleLine(line, state);
                    await WriteLineAsync(response, cancellationToken);

                    if (previousState is TreatmentState.Running && state is TreatmentState.Stopped && runCts is not null)
                    {
                        // STOP interrupted an in-flight run - cancel it so
                        // it doesn't also try to write PROGRESS/COMPLETE
                        // after we've already responded STOPPED.
                        runCts.Cancel();
                        try
                        {
                            await runTask!;
                        }
                        catch
                        {
                            // The run task's own handling already covers
                            // the expected cancellation case.
                        }

                        runCts = null;
                        runTask = null;
                    }
                    else if (state is TreatmentState.Running running && runTask is null)
                    {
                        runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        runTask = RunSimulatedTreatmentAsync(running.PlanId, runCts.Token);
                    }
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
            finally
            {
                runCts?.Cancel();
                if (runTask is not null)
                {
                    try
                    {
                        await runTask;
                    }
                    catch
                    {
                        // Best-effort shutdown.
                    }
                }
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
