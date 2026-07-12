using System.Diagnostics;
using System.IO.Ports;
using System.Text;

// ---- Port selection ----

string[] portNames = SerialPort.GetPortNames();

if (portNames.Length == 0)
{
    Console.WriteLine("No serial ports found. Is the Flipper Zero plugged in?");
    return 1;
}

Console.WriteLine("Available serial ports:");
for (int i = 0; i < portNames.Length; i++)
{
    Console.WriteLine($"  [{i}] {portNames[i]}");
}

string portName;
if (args.Length > 0)
{
    portName = args[0];
    Console.WriteLine($"Using port from command line: {portName}");
}
else
{
    Console.Write("Select a port index: ");
    string? input = Console.ReadLine();
    if (!int.TryParse(input, out int index) || index < 0 || index >= portNames.Length)
    {
        Console.WriteLine("Invalid selection.");
        return 1;
    }
    portName = portNames[index];
}

// ---- Open the port ----

using var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
port.ReadTimeout = 500;
port.WriteTimeout = 2000;
port.Encoding = Encoding.ASCII;

// Ctrl+C bypasses normal control flow (Main's try/finally won't run), so close
// the handle explicitly here as a safety net for a clean shutdown.
Console.CancelKeyPress += (_, _) =>
{
    Console.WriteLine("\nCtrl+C received, closing port...");
    if (port.IsOpen) port.Close();
};

try
{
    port.Open();
    Console.WriteLine($"Opened {portName} at {port.BaudRate} baud.");
}
catch (UnauthorizedAccessException)
{
    Console.WriteLine($"Could not open {portName}: access denied. " +
        "Is another program (Tera Term, qFlipper, etc.) already using it?");
    return 1;
}
catch (IOException ex)
{
    Console.WriteLine($"Could not open {portName}: {ex.Message}. Is the device still connected?");
    return 1;
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Could not open {portName}: {ex.Message}");
    return 1;
}

// USB CDC-ACM devices (which is what the Flipper's USB serial port is) stay
// silent until DTR is asserted, treating it as "a host application has
// connected." SerialPort defaults this to false, so we assert it explicitly.
port.DtrEnable = true;
Console.WriteLine("Asserted DTR.");

// ---- Talk to it ----

try
{
    string command = "help\r\n";
    byte[] commandBytes = Encoding.ASCII.GetBytes(command);
    Console.WriteLine($"\nSending {commandBytes.Length} bytes: {Describe(commandBytes)}");

    var writeStopwatch = Stopwatch.StartNew();
    port.Write(command);
    Console.WriteLine($"Write completed in {writeStopwatch.ElapsedMilliseconds} ms.");

    Console.WriteLine("\nReading response for up to 5 seconds...");
    ReadRawFor(port, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(400));

    // ---- Unplug test ----
    // Observing what SerialPort actually does when the device disappears
    // mid-read, rather than assuming - this was an open unknown from the
    // original Phase 1 plan.
    Console.WriteLine("\n---- Unplug test ----");
    Console.WriteLine("Physically unplug the Flipper's USB cable now.");
    Console.WriteLine("Waiting up to 15 seconds to observe how the port reports disconnection...");

    try
    {
        ReadRawFor(port, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(400));
        Console.WriteLine($"No exception occurred within the 15-second window (port.IsOpen = {port.IsOpen}).");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Unplug detected via IOException: {ex.Message}");
        Console.WriteLine($"port.IsOpen immediately after: {port.IsOpen}");
    }
    catch (OperationCanceledException ex)
    {
        // Confirmed by observation: SerialStream.Read throws this (via
        // EndRead), not IOException, when the device is physically
        // unplugged mid-read.
        Console.WriteLine($"Unplug detected via OperationCanceledException: {ex.Message}");
        Console.WriteLine($"port.IsOpen immediately after: {port.IsOpen}");
    }
}
catch (IOException ex)
{
    Console.WriteLine($"Communication error: {ex.Message}");
}
catch (OperationCanceledException ex)
{
    Console.WriteLine($"Communication canceled (device unplugged?): {ex.Message}");
}
finally
{
    if (port.IsOpen)
    {
        port.Close();
        Console.WriteLine("\nPort closed.");
    }
    Console.WriteLine($"Final port.IsOpen: {port.IsOpen}");
}

return 0;

// ---- Local functions ----

// Reads for up to `duration`, but returns early once data has arrived and then
// gone quiet for `idleGap` - avoids burning the full window (and racking up
// unnecessary Read timeouts) once a burst of data looks finished.
static void ReadRawFor(SerialPort port, TimeSpan duration, TimeSpan idleGap)
{
    var buffer = new byte[256];
    DateTime start = DateTime.UtcNow;
    DateTime deadline = start + duration;
    DateTime? lastDataAt = null;

    while (DateTime.UtcNow < deadline)
    {
        if (lastDataAt is { } last && DateTime.UtcNow - last > idleGap)
        {
            break;
        }

        try
        {
            int bytesRead = port.BaseStream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                TimeSpan elapsed = DateTime.UtcNow - start;
                Console.WriteLine($"[+{elapsed.TotalMilliseconds,6:F0} ms] Received {bytesRead} bytes: " +
                    Describe(buffer[..bytesRead]));
                lastDataAt = DateTime.UtcNow;
            }
        }
        catch (TimeoutException)
        {
            // No data arrived within port.ReadTimeout; keep polling until the overall
            // deadline, or until the idle-gap check above trips.
        }
    }
}

static string Describe(byte[] data)
{
    string hex = string.Join(' ', data.Select(b => b.ToString("X2")));
    string ascii = new(data.Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.').ToArray());
    return $"\n    hex:   {hex}\n    ascii: \"{ascii}\"";
}
