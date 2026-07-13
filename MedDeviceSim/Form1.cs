using System.IO.Ports;
using MedDeviceSim.Communication;
using MedDeviceSim.Session;

namespace MedDeviceSim
{
    public partial class Form1 : Form
    {
        private TreatmentSession? _session;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            portComboBox.Items.AddRange(SerialPort.GetPortNames());
            if (portComboBox.Items.Count > 0)
            {
                portComboBox.SelectedIndex = 0;
            }
        }

        private async void connectButton_Click(object sender, EventArgs e)
        {
            if (portComboBox.SelectedItem is not string portName)
            {
                MessageBox.Show("Select a COM port first.");
                return;
            }

            connectButton.Enabled = false;

            var transport = new SerialTransport(portName);
            _session = new TreatmentSession(transport);

            try
            {
                await _session.OpenAsync();
                SessionResult result = await _session.ConnectAsync();
                UpdateStateLabel();

                if (result is SessionResult.CommunicationFailed failed)
                {
                    MessageBox.Show($"Connection failed: {failed.Reason}");
                    _session.Dispose();
                    _session = null;
                    connectButton.Enabled = true;
                    return;
                }

                disconnectButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open {portName}: {ex.Message}");
                _session?.Dispose();
                _session = null;
                connectButton.Enabled = true;
            }
        }

        private void disconnectButton_Click(object sender, EventArgs e)
        {
            _session?.Dispose();
            _session = null;
            UpdateStateLabel();
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
        }

        private void UpdateStateLabel()
        {
            stateLabel.Text = $"State: {_session?.CurrentState.GetType().Name ?? "Disconnected"}";
        }
    }
}
