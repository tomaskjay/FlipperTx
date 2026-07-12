using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

// DIAGNOSTIC PROJECT - kept for the record, not part of the ongoing design.
//
// This was built to chase down a real, reproducible bug: SerialPort.Write()
// (both BaseStream.Write and the string overload) reliably failed against
// the Flipper with "The semaphore timeout period has expired" (Win32 121,
// ERROR_SEM_TIMEOUT), across many isolated variables (RTS on/off, prior
// reads or none, settle delays, baud rate). This project bypasses
// System.IO.Ports entirely and talks to the COM port with the same raw
// Win32 API (CreateFile/WriteFile/ReadFile/DCB) that SerialPort wraps
// internally, using a plain synchronous (non-overlapped) handle, to test
// whether the bug was specific to SerialPort's internal overlapped I/O.
//
// Result: the raw write failed identically (same Win32 121), which ruled
// out .NET/SerialPort as the cause. The real root cause, confirmed
// afterward, was that the Flipper's own CLI/USB input handling had gotten
// wedged from rapid repeated connect/disconnect/write cycling during
// testing - confirmed because Tera Term, a separate application, also lost
// the ability to send input to the same device at the same time. A power
// cycle of the Flipper fixed it, and the plain SerialPort.Write() approach
// (see FlipperSerialExperiment) then worked correctly with no changes.

const uint GENERIC_READ = 0x80000000;
const uint GENERIC_WRITE = 0x40000000;
const uint OPEN_EXISTING = 3;
const uint SETDTR = 5;

if (args.Length == 0)
{
    Console.WriteLine("Usage: RawSerialExperiment <COM port, e.g. COM7>");
    return 1;
}

string portName = args[0];
string devicePath = $"\\\\.\\{portName}";

Console.WriteLine($"Opening {devicePath} with raw CreateFile (no FILE_FLAG_OVERLAPPED)...");

using SafeFileHandle handle = CreateFile(
    devicePath,
    GENERIC_READ | GENERIC_WRITE,
    0,
    IntPtr.Zero,
    OPEN_EXISTING,
    0,
    IntPtr.Zero);

if (handle.IsInvalid)
{
    Console.WriteLine($"CreateFile failed. Win32 error code: {Marshal.GetLastWin32Error()}");
    return 1;
}

Console.WriteLine("Handle opened successfully.");

// ---- Configure baud rate / byte size / parity / stop bits / flow control ----
// Fetch the current DCB first so DCBlength etc. are sane, then explicitly
// override the packed flow-control bitfield instead of trusting whatever the
// driver's default was - the earlier "no flow control" assumption may have
// been wrong, and a stuck fOutxCtsFlow (wait for CTS before writing, which
// the Flipper's firmware likely never asserts) would explain the write
// consistently blocking for the full timeout.
//
// Flags = 0x1011 packs: fBinary=1 (bit 0, required),
// fDtrControl=1/ENABLE (bits 4-5), fRtsControl=1/ENABLE (bits 12-13),
// with fOutxCtsFlow, fOutxDsrFlow, fOutX, fInX all left at 0 (disabled).

var dcb = new DCB();
dcb.DCBlength = (uint)Marshal.SizeOf<DCB>();

if (!GetCommState(handle, ref dcb))
{
    Console.WriteLine($"GetCommState failed. Win32 error code: {Marshal.GetLastWin32Error()}");
    return 1;
}

dcb.BaudRate = 230400; // Flipper's docs specify this for CLI-over-serial, not 115200
dcb.ByteSize = 8;
dcb.Parity = 0;   // NOPARITY
dcb.StopBits = 0; // ONESTOPBIT
dcb.Flags = 0x1011;

if (!SetCommState(handle, ref dcb))
{
    Console.WriteLine($"SetCommState failed. Win32 error code: {Marshal.GetLastWin32Error()}");
    return 1;
}

Console.WriteLine($"DCB configured: {dcb.BaudRate} 8N1.");

// ---- Configure timeouts ----
// ReadTotalTimeoutConstant with the other read timeout fields at 0 means
// each ReadFile call waits up to that many ms total and returns whatever
// arrived (possibly zero bytes) - a fixed-timeout read, not a per-byte one.

var timeouts = new COMMTIMEOUTS
{
    ReadIntervalTimeout = 0,
    ReadTotalTimeoutMultiplier = 0,
    ReadTotalTimeoutConstant = 2000,
    WriteTotalTimeoutMultiplier = 0,
    WriteTotalTimeoutConstant = 2000,
};

if (!SetCommTimeouts(handle, ref timeouts))
{
    Console.WriteLine($"SetCommTimeouts failed. Win32 error code: {Marshal.GetLastWin32Error()}");
    return 1;
}

// ---- Assert DTR ----
// Same reasoning as the SerialPort experiment: the Flipper's USB CDC-ACM
// firmware stays silent until DTR is asserted.

if (!EscapeCommFunction(handle, SETDTR))
{
    Console.WriteLine($"EscapeCommFunction(SETDTR) failed. Win32 error code: {Marshal.GetLastWin32Error()}");
    return 1;
}

Console.WriteLine("Asserted DTR.");

// ---- Drain the unsolicited boot banner ----

Console.WriteLine("\nDraining any unsolicited output for up to 2 seconds...");
ReadRawFor(handle, TimeSpan.FromSeconds(2));

// ---- Send a single byte first ----
// Isolating whether write SIZE matters, before attempting the full command.

byte[] firstByte = Encoding.ASCII.GetBytes("h");
Console.WriteLine($"\nSending 1 byte ('h') via raw WriteFile...");

bool firstByteOk = WriteFile(handle, firstByte, (uint)firstByte.Length, out uint firstByteWritten, IntPtr.Zero);
if (!firstByteOk)
{
    Console.WriteLine($"WriteFile failed. Win32 error code: {Marshal.GetLastWin32Error()}");
}
else
{
    Console.WriteLine($"WriteFile succeeded: {firstByteWritten} of {firstByte.Length} bytes written.");
}

// ---- Send the rest, only if the single byte succeeded ----

if (firstByteOk)
{
    byte[] rest = Encoding.ASCII.GetBytes("elp\r\n");
    Console.WriteLine($"\nSending {rest.Length} bytes ('elp\\r\\n') via raw WriteFile...");

    if (!WriteFile(handle, rest, (uint)rest.Length, out uint restWritten, IntPtr.Zero))
    {
        Console.WriteLine($"WriteFile failed. Win32 error code: {Marshal.GetLastWin32Error()}");
    }
    else
    {
        Console.WriteLine($"WriteFile succeeded: {restWritten} of {rest.Length} bytes written.");
    }
}

Console.WriteLine("\nReading response for up to 3 seconds...");
ReadRawFor(handle, TimeSpan.FromSeconds(3));

Console.WriteLine("\nDone.");
return 0;

// ---- Local functions ----

static void ReadRawFor(SafeFileHandle handle, TimeSpan duration)
{
    var buffer = new byte[256];
    DateTime start = DateTime.UtcNow;
    DateTime deadline = start + duration;

    while (DateTime.UtcNow < deadline)
    {
        bool ok = ReadFile(handle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero);
        if (!ok)
        {
            Console.WriteLine($"ReadFile failed. Win32 error code: {Marshal.GetLastWin32Error()}");
            break;
        }

        if (bytesRead > 0)
        {
            TimeSpan elapsed = DateTime.UtcNow - start;
            byte[] received = buffer[..(int)bytesRead];
            string hex = string.Join(' ', received.Select(b => b.ToString("X2")));
            string ascii = new(received.Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.').ToArray());
            Console.WriteLine($"[+{elapsed.TotalMilliseconds,6:F0} ms] Received {bytesRead} bytes:\n    hex:   {hex}\n    ascii: \"{ascii}\"");
        }
    }
}

// ---- Win32 P/Invoke declarations ----

[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
static extern SafeFileHandle CreateFile(
    string lpFileName,
    uint dwDesiredAccess,
    uint dwShareMode,
    IntPtr lpSecurityAttributes,
    uint dwCreationDisposition,
    uint dwFlagsAndAttributes,
    IntPtr hTemplateFile);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool GetCommState(SafeFileHandle hFile, ref DCB lpDCB);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool SetCommState(SafeFileHandle hFile, ref DCB lpDCB);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool SetCommTimeouts(SafeFileHandle hFile, ref COMMTIMEOUTS lpCommTimeouts);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool EscapeCommFunction(SafeFileHandle hFile, uint dwFunc);

[StructLayout(LayoutKind.Sequential)]
struct DCB
{
    public uint DCBlength;
    public uint BaudRate;
    public uint Flags; // packed bitfields (fBinary, fParity, fDtrControl, fRtsControl, etc.) - left as-is from GetCommState
    public ushort wReserved;
    public ushort XonLim;
    public ushort XoffLim;
    public byte ByteSize;
    public byte Parity;
    public byte StopBits;
    public byte XonChar;
    public byte XoffChar;
    public byte ErrorChar;
    public byte EofChar;
    public byte EvtChar;
    public ushort wReserved1;
}

[StructLayout(LayoutKind.Sequential)]
struct COMMTIMEOUTS
{
    public uint ReadIntervalTimeout;
    public uint ReadTotalTimeoutMultiplier;
    public uint ReadTotalTimeoutConstant;
    public uint WriteTotalTimeoutMultiplier;
    public uint WriteTotalTimeoutConstant;
}
