using System.Net.Sockets;
using System.Text;

namespace MedDeviceSim.Communication;

// Reuses LineReader unmodified for reads - it only depends on Stream, and
// NetworkStream is a Stream, exactly the payoff that dependency was
// designed for back in Phase 2. That reuse also means reads inherit
// LineReader's Task.Run-wrapped-synchronous-Read approach (originally built
// around SerialStream's specific behavior), which is only safe if the
// stream has a real ReadTimeout configured - verified necessary here: an
// early version without this hung indefinitely and ignored its
// CancellationToken entirely once no more data was coming, since Task.Run
// cannot interrupt an already-blocked synchronous call. NetworkStream's
// read timeout throws IOException, not SerialStream's TimeoutException -
// LineReader was extended to recognize that shape too.
//
// SendAsync, unlike SendAsync in SerialTransport, uses genuine
// WriteAsync directly rather than a Task.Run-wrapped synchronous Write -
// NetworkStream's async methods are well-established .NET, unlike
// SerialStream's older internals, and are not known to have the same
// cancellation gap. Not separately verified in isolation the way
// SerialStream's bug was, though - a reasonable assumption based on
// NetworkStream's track record, not a proven fact.
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

        NetworkStream stream = _client.GetStream();
        stream.ReadTimeout = 2000;
        stream.WriteTimeout = 2000;
        _lineReader = new LineReader(stream);
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
