using MedDeviceSim.Communication;

namespace MedDeviceSim.Session.Tests;

// A controllable test double, not a "smart" simulated device - a test
// scripts exactly what ReadLineAsync should return next via EnqueueLine,
// and can inspect SentText afterward to verify what was actually sent.
public sealed class FakeTransport : ITransport
{
    private readonly Queue<string> _linesToReturn = new();

    public List<string> SentText { get; } = [];

    public bool IsOpen { get; private set; }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        SentText.Add(text);
        return Task.CompletedTask;
    }

    public Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (_linesToReturn.Count == 0)
        {
            throw new InvalidOperationException("No scripted line available - test forgot to call EnqueueLine.");
        }

        return Task.FromResult(_linesToReturn.Dequeue());
    }

    public void EnqueueLine(string line) => _linesToReturn.Enqueue(line);

    public void Dispose()
    {
        IsOpen = false;
    }
}
