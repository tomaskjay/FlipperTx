using MedDeviceSim.Communication;

namespace MedDeviceSim.Communication.Tests;

public class SerialTransportHardwareTests
{
    // Reads until no new line arrives within idleTimeout, treating that as
    // "the device is done talking," not a failure - avoids assuming a
    // specific response length, which bit the ARM test below.
    private static async Task<List<string>> ReadAllAvailableLinesAsync(SerialTransport transport, TimeSpan idleTimeout)
    {
        var lines = new List<string>();
        using var cts = new CancellationTokenSource(idleTimeout);

        try
        {
            while (true)
            {
                lines.Add(await transport.ReadLineAsync(cts.Token));
            }
        }
        catch (OperationCanceledException)
        {
        }

        return lines;
    }

    // Manual tests: require a real Flipper Zero connected via USB. Not part
    // of the automated suite - remove Skip (and adjust the port name below
    // if needed) to run locally.
    [Fact(Skip = "requires a real Flipper Zero connected via USB")]
    public async Task OpenSendReadLine_AgainstRealFlipper_ReceivesWelcomeBanner()
    {
        using var transport = new SerialTransport("COM7");

        await transport.OpenAsync();
        await transport.SendAsync("help\r\n");

        List<string> lines = await ReadAllAvailableLinesAsync(transport, TimeSpan.FromSeconds(3));

        Assert.Contains(lines, line => line.Contains("Flipper"));
    }

    // The stock Flipper CLI doesn't speak our custom protocol at all, so
    // sending one of our commands is a real source of genuinely unexpected
    // device output - unlike hand-written strings in DeviceResponseTests,
    // this checks the Unknown fallback against something we didn't invent.
    [Fact(Skip = "requires a real Flipper Zero connected via USB")]
    public async Task SendArm_AgainstRealFlipperStockCli_AllOutputParsesAsUnknown()
    {
        using var transport = new SerialTransport("COM7");

        await transport.OpenAsync();
        await transport.SendAsync("ARM\r\n");

        List<string> lines = await ReadAllAvailableLinesAsync(transport, TimeSpan.FromSeconds(3));

        // Sanity check: confirm the device actually saw and echoed our
        // command, not just silence or an unrelated banner.
        Assert.Contains(lines, line => line.Contains("ARM"));

        // None of the real output (banner, prompt, echoed command, the
        // stock CLI's own error message) should accidentally collide with
        // one of our defined response shapes.
        Assert.All(lines, line => Assert.IsType<DeviceResponse.Unknown>(DeviceResponse.Parse(line)));
    }
}
