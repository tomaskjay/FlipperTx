using MedDeviceSim.Communication;

namespace MedDeviceSim.Communication.Tests;

public class SerialTransportHardwareTests
{
    // Manual test: requires a real Flipper Zero connected via USB. Not part
    // of the automated suite - remove the Skip (and adjust the port name
    // below if needed) to run it locally.
    [Fact(Skip = "requires a real Flipper Zero connected via USB")]
    public async Task OpenSendReadLine_AgainstRealFlipper_ReceivesWelcomeBanner()
    {
        using var transport = new SerialTransport("COM7");
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        CancellationToken cancellationToken = timeoutSource.Token;

        await transport.OpenAsync(cancellationToken);
        await transport.SendAsync("help\r\n", cancellationToken);

        var lines = new List<string>();
        for (int i = 0; i < 30; i++)
        {
            lines.Add(await transport.ReadLineAsync(cancellationToken));
        }

        Assert.Contains(lines, line => line.Contains("Flipper"));
    }
}
