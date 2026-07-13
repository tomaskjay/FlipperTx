using System.IO.Ports;
using System.Text;

namespace MedDeviceSim.Communication;

public sealed class SerialTransport : ITransport
{
    private readonly SerialPort _port;

    // Can't be constructed until the port is open (SerialPort.BaseStream
    // throws before then), so this starts null and is created in OpenAsync.
    private LineReader? _lineReader;

    public SerialTransport(string portName, int baudRate = 115200)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            // Without an explicit ReadTimeout, SerialPort defaults to -1
            // (wait forever), which would let ReadLineAsync hang
            // indefinitely if the device never responds.
            ReadTimeout = 2000,
            WriteTimeout = 2000,
            Encoding = Encoding.ASCII,
        };
    }

    public bool IsOpen => _port.IsOpen;

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        // SerialPort.Open() is a fast local operation, not network I/O, so
        // there's no genuine async work to await here. The async signature
        // is for consistency with the rest of this class's lifecycle and
        // leaves room for this to become real async work later (e.g. an
        // initial handshake read).
        _port.Open();

        // Verified against real hardware in Phase 1: the Flipper's USB
        // CDC-ACM firmware stays silent until DTR is asserted. SerialPort
        // defaults this to false.
        _port.DtrEnable = true;

        _lineReader = new LineReader(_port.BaseStream);

        return Task.CompletedTask;
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        // Same reasoning as LineReader.ReadLineAsync: Stream.WriteAsync's
        // CancellationToken is not verified to be honored by SerialStream
        // (inferred from the confirmed ReadAsync gap, not separately
        // tested), so we fall back to the synchronous Write via Task.Run,
        // which reliably respects WriteTimeout.
        byte[] bytes = _port.Encoding.GetBytes(text);
        await Task.Run(() => _port.BaseStream.Write(bytes, 0, bytes.Length), cancellationToken);
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
        if (_port.IsOpen)
        {
            _port.Close();
        }

        _port.Dispose();
    }
}
