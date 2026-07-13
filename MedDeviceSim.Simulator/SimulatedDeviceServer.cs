using System.Net;
using System.Net.Sockets;
using System.Text;
using MedDeviceSim.Communication;

namespace MedDeviceSim.Simulator;

// Checkpoint-1 scope: proves the TCP pipe works end-to-end. Only
// understands CONNECT so far - full protocol-aware, stateful behavior
// (rejecting ARM without a loaded plan, simulated Running/Progress/
// Complete, etc.) is checkpoint 2.
//
// Deliberately independent of TreatmentWorkflow's logic, not a reuse of
// it - see the Phase 6 checkpoint 1 design discussion: an integration test
// against an independently-implemented device is a meaningfully stronger
// test than one where both sides share the same rules by construction.
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

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string line = await lineReader.ReadLineAsync(cancellationToken);
                    string response = HandleLine(line);
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

    private static string HandleLine(string line) => line.Trim() switch
    {
        "CONNECT" => "CONNECTED\r\n",
        _ => "ERROR Unrecognized command\r\n",
    };

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
