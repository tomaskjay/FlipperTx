using System.IO.Ports;
using MedDeviceSim.Communication;
using MedDeviceSim.Session;
using MedDeviceSim.Workflow;

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

            UpdateButtonStates();
        }

        // Without this, closing the window while connected would leak the
        // TreatmentSession (and the underlying COM port) rather than
        // closing it - relying on the GC to eventually finalize a real OS
        // resource like a COM port is not something to depend on.
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _session?.Dispose();
            _session = null;
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
                Log($"Transport opened on {portName}.");

                SessionResult result = await _session.ConnectAsync();
                UpdateLabels();
                LogResult("CONNECT", result);

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
                    UpdateButtonStates();
                    return;
                }

                // Whether or not the workflow actually reached Connected
                // (e.g. a real device that doesn't speak our protocol would
                // leave it at Disconnected), the transport itself is open
                // and usable, so Disconnect should be available.
                disconnectButton.Enabled = true;
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open {portName}: {ex.Message}");
                _session?.Dispose();
                _session = null;
                UpdateLabels();
                connectButton.Enabled = true;
                UpdateButtonStates();
            }
        }

        private void disconnectButton_Click(object sender, EventArgs e)
        {
            _session?.Dispose();
            _session = null;
            UpdateLabels();
            Log("Disconnected.");
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
            UpdateButtonStates();
        }

        private async void loadPlanButton_Click(object sender, EventArgs e)
        {
            if (_session is null)
            {
                return;
            }

            string planId = planIdTextBox.Text.Trim();
            if (planId.Length == 0)
            {
                MessageBox.Show("Enter a plan ID first.");
                return;
            }

            await ExecuteActionAsync($"LOAD_PLAN {planId}", () => _session.LoadPlanAsync(planId));
        }

        private async void armButton_Click(object sender, EventArgs e)
        {
            if (_session is null)
            {
                return;
            }

            await ExecuteActionAsync("ARM", () => _session.ArmAsync());
        }

        private async void startButton_Click(object sender, EventArgs e)
        {
            if (_session is null)
            {
                return;
            }

            await ExecuteActionAsync("START", () => _session.StartAsync());
        }

        private async void stopButton_Click(object sender, EventArgs e)
        {
            if (_session is null)
            {
                return;
            }

            await ExecuteActionAsync("STOP", () => _session.StopAsync());
        }

        // Shared by all four action buttons: log the attempt, run it, log
        // whatever came back, then refresh labels and button states -
        // avoids repeating this five-step sequence in each click handler.
        private async Task ExecuteActionAsync(string actionName, Func<Task<SessionResult>> action)
        {
            SessionResult result = await action();
            LogResult(actionName, result);
            UpdateLabels();
            UpdateButtonStates();
        }

        private void LogResult(string actionName, SessionResult result)
        {
            switch (result)
            {
                case SessionResult.Sent sent:
                    Log($"Sent: {actionName}");
                    Log($"Received: {sent.Response}");
                    break;
                case SessionResult.Rejected rejected:
                    Log($"Rejected: {actionName} - {rejected.Reason}");
                    break;
                case SessionResult.CommunicationFailed failed:
                    Log($"Sent: {actionName}");
                    Log($"Communication failed: {failed.Reason}");
                    break;
            }
        }

        private void Log(string message)
        {
            eventLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }

        private void UpdateLabels()
        {
            transportStatusLabel.Text = _session is { IsOpen: true }
                ? $"Transport: Open on {portComboBox.SelectedItem}"
                : "Transport: Closed";

            stateLabel.Text = $"Workflow: {_session?.CurrentState.GetType().Name ?? "Disconnected"}";
        }

        // Ties each action button's availability to the workflow state that
        // actually allows it - not required for safety (TreatmentWorkflow
        // rejects invalid actions regardless of what the UI allows), but
        // better UX than letting the user click something guaranteed to be
        // rejected.
        private void UpdateButtonStates()
        {
            bool isOpen = _session is { IsOpen: true };
            loadPlanButton.Enabled = isOpen && _session!.CurrentState is TreatmentState.Connected;
            armButton.Enabled = isOpen && _session!.CurrentState is TreatmentState.PlanLoaded;
            startButton.Enabled = isOpen && _session!.CurrentState is TreatmentState.Armed;
            stopButton.Enabled = isOpen && _session!.CurrentState is TreatmentState.Armed or TreatmentState.Running;
        }
    }
}
