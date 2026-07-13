using MedDeviceSim.Communication;

namespace MedDeviceSim.Communication.Tests;

public class DeviceResponseTests
{
    [Fact]
    public void Parse_Connected_ReturnsConnected()
    {
        Assert.Equal(new DeviceResponse.Connected(), DeviceResponse.Parse("CONNECTED"));
    }

    [Fact]
    public void Parse_PlanLoaded_ReturnsPlanLoadedWithId()
    {
        Assert.Equal(new DeviceResponse.PlanLoaded("abc123"), DeviceResponse.Parse("PLAN_LOADED abc123"));
    }

    [Fact]
    public void Parse_Ready_ReturnsReady()
    {
        Assert.Equal(new DeviceResponse.Ready(), DeviceResponse.Parse("READY"));
    }

    [Fact]
    public void Parse_Running_ReturnsRunning()
    {
        Assert.Equal(new DeviceResponse.Running(), DeviceResponse.Parse("RUNNING"));
    }

    [Fact]
    public void Parse_Progress_ReturnsProgressWithPercent()
    {
        Assert.Equal(new DeviceResponse.Progress(42), DeviceResponse.Parse("PROGRESS 42"));
    }

    [Fact]
    public void Parse_Complete_ReturnsComplete()
    {
        Assert.Equal(new DeviceResponse.Complete(), DeviceResponse.Parse("COMPLETE"));
    }

    [Fact]
    public void Parse_Stopped_ReturnsStopped()
    {
        Assert.Equal(new DeviceResponse.Stopped(), DeviceResponse.Parse("STOPPED"));
    }

    [Fact]
    public void Parse_Error_ReturnsErrorWithReason()
    {
        Assert.Equal(new DeviceResponse.Error("Plan not loaded"), DeviceResponse.Parse("ERROR Plan not loaded"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("GARBAGE")]
    [InlineData("PROGRESS")]
    [InlineData("PROGRESS notanumber")]
    [InlineData("CONNECTED extra")]
    [InlineData("PLAN_LOADED")]
    [InlineData("STOPPED extra")]
    [InlineData("ERROR")]
    public void Parse_MalformedOrUnrecognizedInput_ReturnsUnknown(string line)
    {
        DeviceResponse result = DeviceResponse.Parse(line);

        Assert.IsType<DeviceResponse.Unknown>(result);
    }
}
