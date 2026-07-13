using System.Net;
using System.Net.Sockets;
using System.Text;
using MedDeviceSim.Communication;

namespace MedDeviceSim.Communication.Tests;

public class LineReaderTests
{
    [Fact]
    public async Task ReadLineAsync_SingleCompleteLine_ReturnsLineWithoutTerminator()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("help\r\n"));
        var reader = new LineReader(stream);

        string line = await reader.ReadLineAsync();

        Assert.Equal("help", line);
    }

    [Fact]
    public async Task ReadLineAsync_MultipleLinesInOneRead_ReturnsEachLineSeparately()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("PLAN_LOADED\r\nREADY\r\n"));
        var reader = new LineReader(stream);

        string first = await reader.ReadLineAsync();
        string second = await reader.ReadLineAsync();

        Assert.Equal("PLAN_LOADED", first);
        Assert.Equal("READY", second);
    }

    [Fact]
    public async Task ReadLineAsync_LineSplitAcrossManyReads_StillAssemblesCorrectly()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("help\r\n"));
        // A tiny read buffer forces many partial reads even though all the
        // data is already sitting in the MemoryStream, exercising the same
        // accumulation logic that a slow real serial link would.
        var reader = new LineReader(stream, readBufferSize: 2);

        string line = await reader.ReadLineAsync();

        Assert.Equal("help", line);
    }

    [Fact]
    public async Task ReadLineAsync_StreamEndsBeforeCompleteLine_ThrowsIOException()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("incomplete"));
        var reader = new LineReader(stream);

        await Assert.ThrowsAsync<IOException>(() => reader.ReadLineAsync());
    }

    // MemoryStream can't reproduce this - it never blocks or times out.
    // A real loopback socket is needed to prove LineReader actually
    // survives NetworkStream's read-timeout shape (IOException wrapping a
    // SocketException) rather than letting it escape as a failure, the
    // behavior TcpTransport depends on to avoid hanging forever.
    [Fact]
    public async Task ReadLineAsync_NoDataWithinReadTimeout_RetriesUntilLineArrives()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using TcpClient server = await acceptTask;

        NetworkStream clientStream = client.GetStream();
        clientStream.ReadTimeout = 100;
        var reader = new LineReader(clientStream);

        // Nothing sent yet - forces ReadLineAsync to hit at least one real
        // socket read timeout and retry internally before any data exists.
        Task<string> readTask = reader.ReadLineAsync();
        await Task.Delay(250);
        Assert.False(readTask.IsCompleted);

        await server.GetStream().WriteAsync(Encoding.ASCII.GetBytes("help\r\n"));

        string line = await readTask;

        Assert.Equal("help", line);
    }
}
