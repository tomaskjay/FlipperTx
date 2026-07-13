using System.IO.Ports;
using System.Text;

namespace MedDeviceSim.Communication;

public sealed class SerialTransport : IDisposable
{
    private readonly SerialPort _port;

    public SerialTransport(string portName, int baudRate = 115200)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
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

        return Task.CompletedTask;
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        byte[] bytes = _port.Encoding.GetBytes(text);
        await _port.BaseStream.WriteAsync(bytes, cancellationToken);
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
