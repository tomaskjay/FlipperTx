using MedDeviceSim.Communication;

namespace MedDeviceSim.Communication.Tests;

public class DeviceCommandTests
{
    [Fact]
    public void ToWireFormat_Connect_ReturnsConnect()
    {
        Assert.Equal("CONNECT\r\n", new DeviceCommand.Connect().ToWireFormat());
    }

    [Fact]
    public void ToWireFormat_LoadPlan_IncludesPlanId()
    {
        Assert.Equal("LOAD_PLAN abc123\r\n", new DeviceCommand.LoadPlan("abc123").ToWireFormat());
    }

    [Fact]
    public void ToWireFormat_Arm_ReturnsArm()
    {
        Assert.Equal("ARM\r\n", new DeviceCommand.Arm().ToWireFormat());
    }

    [Fact]
    public void ToWireFormat_Start_ReturnsStart()
    {
        Assert.Equal("START\r\n", new DeviceCommand.Start().ToWireFormat());
    }

    [Fact]
    public void ToWireFormat_Stop_ReturnsStop()
    {
        Assert.Equal("STOP\r\n", new DeviceCommand.Stop().ToWireFormat());
    }

    [Fact]
    public void ToWireFormat_GetStatus_ReturnsGetStatus()
    {
        Assert.Equal("GET_STATUS\r\n", new DeviceCommand.GetStatus().ToWireFormat());
    }
}
