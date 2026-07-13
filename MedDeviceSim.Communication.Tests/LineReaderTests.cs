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
}
