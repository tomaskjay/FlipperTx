using System.Net.Sockets;
using System.Text;

namespace MedDeviceSim.Communication;

// Turns a raw byte stream into discrete \r\n-terminated lines. Handles two
// behaviors confirmed against real hardware in Phase 1: a single line can
// arrive split across multiple reads, and multiple lines can arrive
// combined in one read.
public sealed class LineReader
{
    private readonly Stream _stream;
    private readonly byte[] _readBuffer;
    private readonly List<byte> _pending = [];
    private readonly Queue<string> _completedLines = new();

    public LineReader(Stream stream, int readBufferSize = 256)
    {
        _stream = stream;
        _readBuffer = new byte[readBufferSize];
    }

    public async Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        while (_completedLines.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stream.ReadAsync's CancellationToken is not reliably honored
            // by SerialStream (confirmed by direct probe against real
            // hardware: a call hung 45+ seconds past a 2-second token).
            // The synchronous Read, however, does reliably respect
            // ReadTimeout, so we fall back to that via Task.Run and poll
            // between chunks - the same pattern Phase 1's console
            // experiments used successfully.
            int bytesRead;
            try
            {
                bytesRead = await Task.Run(
                    () => _stream.Read(_readBuffer, 0, _readBuffer.Length),
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                // SerialStream's shape for "no data within ReadTimeout";
                // loop back so the cancellation check above gets another
                // chance.
                continue;
            }
            catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                // NetworkStream's shape for the same condition - verified
                // necessary when TcpTransport started hanging indefinitely
                // without it. This does mean LineReader, otherwise
                // Stream-agnostic on purpose, now has to know about one
                // Socket-specific exception shape - an acknowledged,
                // narrow compromise, not an accident.
                continue;
            }

            if (bytesRead == 0)
            {
                throw new IOException("Stream ended while waiting for a complete line.");
            }

            ExtractLines(_readBuffer.AsSpan(0, bytesRead));
        }

        return _completedLines.Dequeue();
    }

    private void ExtractLines(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            if (b == (byte)'\n')
            {
                int length = _pending.Count;
                if (length > 0 && _pending[length - 1] == (byte)'\r')
                {
                    length--;
                }

                _completedLines.Enqueue(Encoding.ASCII.GetString(_pending.ToArray(), 0, length));
                _pending.Clear();
            }
            else
            {
                _pending.Add(b);
            }
        }
    }
}
