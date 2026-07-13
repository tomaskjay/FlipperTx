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

                // Transport is open now, regardless of what happens next -
                // reflect that immediately, separate from workflow state.
                UpdateLabels();

                SessionResult result = await _session.ConnectAsync();
                UpdateLabels();

                if (result is SessionResult.CommunicationFailed failed)
                {
                    // The transport itself failed during the exchange - not
                    // just "the device didn't understand CONNECT". Treat
                    // this as a real failure and reset entirely.
                    MessageBox.Show($"Connection failed: {failed.Reason}");
                    _session.Dispose();
                    _session = null;
                    UpdateLabels();
                    connectButton.Enabled = true;
                    return;
                }

                // Whether or not the workflow actually reached Connected
                // (e.g. a real device that doesn't speak our protocol would
                // leave it at Disconnected), the transport itself is open
                // and usable, so Disconnect should be available.
                disconnectButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open {portName}: {ex.Message}");
                _session?.Dispose();
                _session = null;
                UpdateLabels();
                connectButton.Enabled = true;
            }
        }

        private void disconnectButton_Click(object sender, EventArgs e)
        {
            _session?.Dispose();
            _session = null;
            UpdateLabels();
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
        }

        private void UpdateLabels()
        {
            transportStatusLabel.Text = _session is { IsOpen: true }
                ? $"Transport: Open on {portComboBox.SelectedItem}"
                : "Transport: Closed";

            stateLabel.Text = $"Workflow: {_session?.CurrentState.GetType().Name ?? "Disconnected"}";
        }
    }
}
