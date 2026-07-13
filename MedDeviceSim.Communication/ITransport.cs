namespace MedDeviceSim.Communication;

// Extracted from SerialTransport's existing public surface, now that a
// second implementation (a test fake) actually needs one - deliberately
// not introduced back in Phase 2, when only one implementation existed.
public interface ITransport : IDisposable
{
    bool IsOpen { get; }

    Task OpenAsync(CancellationToken cancellationToken = default);

    Task SendAsync(string text, CancellationToken cancellationToken = default);

    Task<string> ReadLineAsync(CancellationToken cancellationToken = default);
}
