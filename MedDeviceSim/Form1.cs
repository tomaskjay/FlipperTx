using System.IO.Ports;
using MedDeviceSim.Communication;
using MedDeviceSim.Session;
using MedDeviceSim.Workflow;

namespace MedDeviceSim
{
    public partial class Form1 : Form
    {
        private TreatmentSession? _session;
        private string? _connectionDescription;
        private CancellationTokenSource? _updateLoopCts;
        private Task? _updateLoopTask;

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
        // TreatmentSession (and the underlying transport) rather than
        // closing it - relying on the GC to eventually finalize a real OS
        // resource like a COM port or socket is not something to depend on.
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Best-effort signal only - the form is closing regardless, so
            // there's no point awaiting the update loop's full shutdown
            // here (that would require an async handler for a rare edge
            // case: closing the window mid-run).
            _updateLoopCts?.Cancel();
            _session?.Dispose();
            _session = null;
        }

        private void serialRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            portComboBox.Visible = serialRadioButton.Checked;
            tcpPortTextBox.Visible = !serialRadioButton.Checked;
        }

        private async void connectButton_Click(object sender, EventArgs e)
        {
            ITransport transport;

            if (serialRadioButton.Checked)
            {
                if (portComboBox.SelectedItem is not string portName)
                {
                    MessageBox.Show("Select a COM port first.");
                    return;
                }

                transport = new SerialTransport(portName);
                _connectionDescription = portName;
            }
            else
            {
                if (!int.TryParse(tcpPortTextBox.Text, out int tcpPort))
                {
                    MessageBox.Show("Enter a valid TCP port number.");
                    return;
                }

                transport = new TcpTransport("127.0.0.1", tcpPort);
                _connectionDescription = $"127.0.0.1:{tcpPort}";
            }

            SetConnectionControlsEnabled(false);

            _session = new TreatmentSession(transport);

            try
            {
                await _session.OpenAsync();

                // Transport is open now, regardless of what happens next -
                // reflect that immediately, separate from workflow state.
                UpdateLabels();
                Log($"Transport opened on {_connectionDescription}.");

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
                    SetConnectionControlsEnabled(true);
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
                MessageBox.Show($"Could not open {_connectionDescription}: {ex.Message}");
                _session?.Dispose();
                _session = null;
                UpdateLabels();
                SetConnectionControlsEnabled(true);
                UpdateButtonStates();
            }
        }

        private async void disconnectButton_Click(object sender, EventArgs e)
        {
            await StopUpdateLoopAsync();
            _session?.Dispose();
            _session = null;
            UpdateLabels();
            Log("Disconnected.");
            SetConnectionControlsEnabled(true);
            disconnectButton.Enabled = false;
            UpdateButtonStates();
        }

        // Can't switch transport type or edit connection details mid-session
        // - grouped together since they always change as one unit.
        private void SetConnectionControlsEnabled(bool enabled)
        {
            serialRadioButton.Enabled = enabled;
            tcpRadioButton.Enabled = enabled;
            portComboBox.Enabled = enabled;
            tcpPortTextBox.Enabled = enabled;
            connectButton.Enabled = enabled;
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

            // PROGRESS/COMPLETE now arrive on their own - start watching for
            // them so the UI reflects them without another button click.
            if (_session.CurrentState is TreatmentState.Running)
            {
                _updateLoopCts = new CancellationTokenSource();
                _updateLoopTask = ObserveUpdatesAsync(_session, _updateLoopCts.Token);
            }
        }

        private async void stopButton_Click(object sender, EventArgs e)
        {
            if (_session is null)
            {
                return;
            }

            // TreatmentSession only supports one caller reading at a time -
            // the update loop's in-flight read must fully finish before
            // StopAsync sends STOP and reads its reply, or the two could
            // race on the same transport.
            await StopUpdateLoopAsync();
            await ExecuteActionAsync("STOP", () => _session.StopAsync());
        }

        // Watches for PROGRESS/COMPLETE arriving unprompted while Running.
        // Takes the session as a parameter rather than reading the _session
        // field, so this keeps working safely against the session it was
        // started for even if _session is reassigned by a concurrent
        // disconnect - callers cancel and await this loop before touching
        // the transport again (see StopUpdateLoopAsync).
        private async Task ObserveUpdatesAsync(TreatmentSession session, CancellationToken cancellationToken)
        {
            try
            {
                while (session.CurrentState is TreatmentState.Running)
                {
                    SessionResult result = await session.ReadNextUpdateAsync(cancellationToken);
                    LogUpdate(result);
                    UpdateLabels();
                    UpdateButtonStates();
                }
            }
            catch (OperationCanceledException)
            {
                // Stop or disconnect requested this - expected, not an error.
            }
        }

        // Cancels the update loop and waits for it to fully finish before
        // returning. Cancellation isn't instant: both transports use a
        // 2-second ReadTimeout, and a blocked synchronous Read() can't be
        // interrupted mid-call - it only unwinds at the next timeout tick.
        private async Task StopUpdateLoopAsync()
        {
            if (_updateLoopCts is null)
            {
                return;
            }

            _updateLoopCts.Cancel();

            if (_updateLoopTask is not null)
            {
                try
                {
                    await _updateLoopTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            _updateLoopCts.Dispose();
            _updateLoopCts = null;
            _updateLoopTask = null;
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

        // Separate from LogResult: an update was never "sent" by us, so
        // reusing that phrasing here would be misleading.
        private void LogUpdate(SessionResult result)
        {
            switch (result)
            {
                case SessionResult.Sent sent:
                    Log($"Received: {sent.Response}");
                    break;
                case SessionResult.CommunicationFailed failed:
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
                ? $"Transport: Open on {_connectionDescription}"
                : "Transport: Closed";

            // TreatmentState's cases are records, so ToString() already
            // includes their data (plan ID, percent complete, fault
            // reason) for free - same trick used for DeviceResponse in the
            // event log.
            TreatmentState state = _session?.CurrentState ?? new TreatmentState.Disconnected();
            stateLabel.Text = $"Workflow: {state}";

            if (state is TreatmentState.Running running)
            {
                progressBar.Visible = true;
                progressBar.Value = Math.Clamp(running.PercentComplete, 0, 100);
            }
            else
            {
                progressBar.Visible = false;
            }
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
