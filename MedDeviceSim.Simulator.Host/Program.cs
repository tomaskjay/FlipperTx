using MedDeviceSim.Simulator;

const int port = 9000;

var server = new SimulatedDeviceServer(port);
server.Start();

Console.WriteLine($"Simulated device listening on 127.0.0.1:{port}. Press Enter to stop.");
Console.ReadLine();

await server.DisposeAsync();
Console.WriteLine("Stopped.");
