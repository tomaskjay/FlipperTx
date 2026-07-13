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
            int bytesRead = await _stream.ReadAsync(_readBuffer, cancellationToken);
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
