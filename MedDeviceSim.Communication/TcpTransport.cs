using System.Net.Sockets;
using System.Text;

namespace MedDeviceSim.Communication;

// Reuses LineReader unmodified - it only depends on Stream, and
// NetworkStream is a Stream, exactly the payoff that dependency was
// designed for back in Phase 2.
//
// Unlike SerialTransport, this does NOT wrap Read/Write in Task.Run to work
// around a cancellation bug - NetworkStream's async methods are a
// long-established, widely-relied-upon part of .NET (unlike SerialStream's
// older APM-based internals) and are not known to have the same issue.
// This is a reasonable assumption based on NetworkStream's track record,
// not something separately verified in isolation the way SerialStream's
// bug was.
public sealed class TcpTransport : ITransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private LineReader? _lineReader;

    public TcpTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public bool IsOpen => _client?.Connected ?? false;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, cancellationToken);
        _lineReader = new LineReader(_client.GetStream());
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new InvalidOperationException($"{nameof(OpenAsync)} must be called before {nameof(SendAsync)}.");
        }

        byte[] bytes = Encoding.ASCII.GetBytes(text);
        await _client.GetStream().WriteAsync(bytes, cancellationToken);
    }

    public Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (_lineReader is null)
        {
            throw new InvalidOperationException($"{nameof(OpenAsync)} must be called before {nameof(ReadLineAsync)}.");
        }

        return _lineReader.ReadLineAsync(cancellationToken);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
