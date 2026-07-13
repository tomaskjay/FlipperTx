namespace MedDeviceSim
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private System.Windows.Forms.RadioButton serialRadioButton;
        private System.Windows.Forms.RadioButton tcpRadioButton;
        private System.Windows.Forms.ComboBox portComboBox;
        private System.Windows.Forms.TextBox tcpPortTextBox;
        private System.Windows.Forms.Button connectButton;
        private System.Windows.Forms.Button disconnectButton;
        private System.Windows.Forms.Label transportStatusLabel;
        private System.Windows.Forms.Label stateLabel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.TextBox planIdTextBox;
        private System.Windows.Forms.Button loadPlanButton;
        private System.Windows.Forms.Button armButton;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.TextBox eventLogTextBox;

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            serialRadioButton = new RadioButton();
            tcpRadioButton = new RadioButton();
            portComboBox = new ComboBox();
            tcpPortTextBox = new TextBox();
            connectButton = new Button();
            disconnectButton = new Button();
            transportStatusLabel = new Label();
            stateLabel = new Label();
            progressBar = new ProgressBar();
            planIdTextBox = new TextBox();
            loadPlanButton = new Button();
            armButton = new Button();
            startButton = new Button();
            stopButton = new Button();
            eventLogTextBox = new TextBox();
            SuspendLayout();
            //
            // serialRadioButton
            //
            serialRadioButton.AutoSize = true;
            serialRadioButton.Checked = true;
            serialRadioButton.Location = new Point(12, 12);
            serialRadioButton.Name = "serialRadioButton";
            serialRadioButton.Size = new Size(54, 19);
            serialRadioButton.TabIndex = 0;
            serialRadioButton.TabStop = true;
            serialRadioButton.Text = "Serial";
            serialRadioButton.UseVisualStyleBackColor = true;
            serialRadioButton.CheckedChanged += serialRadioButton_CheckedChanged;
            //
            // tcpRadioButton
            //
            tcpRadioButton.AutoSize = true;
            tcpRadioButton.Location = new Point(90, 12);
            tcpRadioButton.Name = "tcpRadioButton";
            tcpRadioButton.Size = new Size(97, 19);
            tcpRadioButton.TabIndex = 1;
            tcpRadioButton.Text = "TCP Simulator";
            tcpRadioButton.UseVisualStyleBackColor = true;
            //
            // portComboBox
            //
            portComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            portComboBox.Location = new Point(12, 41);
            portComboBox.Name = "portComboBox";
            portComboBox.Size = new Size(150, 23);
            portComboBox.TabIndex = 2;
            //
            // tcpPortTextBox
            //
            tcpPortTextBox.Location = new Point(12, 41);
            tcpPortTextBox.Name = "tcpPortTextBox";
            tcpPortTextBox.PlaceholderText = "TCP port";
            tcpPortTextBox.Size = new Size(150, 23);
            tcpPortTextBox.TabIndex = 3;
            tcpPortTextBox.Text = "9000";
            tcpPortTextBox.Visible = false;
            //
            // connectButton
            //
            connectButton.Location = new Point(168, 41);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(90, 23);
            connectButton.TabIndex = 4;
            connectButton.Text = "Connect";
            connectButton.UseVisualStyleBackColor = true;
            connectButton.Click += connectButton_Click;
            //
            // disconnectButton
            //
            disconnectButton.Enabled = false;
            disconnectButton.Location = new Point(264, 41);
            disconnectButton.Name = "disconnectButton";
            disconnectButton.Size = new Size(90, 23);
            disconnectButton.TabIndex = 5;
            disconnectButton.Text = "Disconnect";
            disconnectButton.UseVisualStyleBackColor = true;
            disconnectButton.Click += disconnectButton_Click;
            //
            // transportStatusLabel
            //
            transportStatusLabel.AutoSize = true;
            transportStatusLabel.Location = new Point(12, 79);
            transportStatusLabel.Name = "transportStatusLabel";
            transportStatusLabel.Size = new Size(100, 15);
            transportStatusLabel.TabIndex = 6;
            transportStatusLabel.Text = "Transport: Closed";
            //
            // stateLabel
            //
            stateLabel.AutoSize = true;
            stateLabel.Location = new Point(12, 101);
            stateLabel.Name = "stateLabel";
            stateLabel.Size = new Size(114, 15);
            stateLabel.TabIndex = 7;
            stateLabel.Text = "Workflow: Disconnected";
            //
            // progressBar
            //
            progressBar.Location = new Point(12, 124);
            progressBar.Maximum = 100;
            progressBar.Minimum = 0;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(342, 20);
            progressBar.TabIndex = 8;
            progressBar.Visible = false;
            //
            // planIdTextBox
            //
            planIdTextBox.Location = new Point(12, 154);
            planIdTextBox.Name = "planIdTextBox";
            planIdTextBox.PlaceholderText = "Plan ID";
            planIdTextBox.Size = new Size(150, 23);
            planIdTextBox.TabIndex = 9;
            //
            // loadPlanButton
            //
            loadPlanButton.Enabled = false;
            loadPlanButton.Location = new Point(168, 154);
            loadPlanButton.Name = "loadPlanButton";
            loadPlanButton.Size = new Size(90, 23);
            loadPlanButton.TabIndex = 10;
            loadPlanButton.Text = "Load Plan";
            loadPlanButton.UseVisualStyleBackColor = true;
            loadPlanButton.Click += loadPlanButton_Click;
            //
            // armButton
            //
            armButton.Enabled = false;
            armButton.Location = new Point(12, 184);
            armButton.Name = "armButton";
            armButton.Size = new Size(80, 23);
            armButton.TabIndex = 11;
            armButton.Text = "Arm";
            armButton.UseVisualStyleBackColor = true;
            armButton.Click += armButton_Click;
            //
            // startButton
            //
            startButton.Enabled = false;
            startButton.Location = new Point(98, 184);
            startButton.Name = "startButton";
            startButton.Size = new Size(80, 23);
            startButton.TabIndex = 12;
            startButton.Text = "Start";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += startButton_Click;
            //
            // stopButton
            //
            stopButton.Enabled = false;
            stopButton.Location = new Point(184, 184);
            stopButton.Name = "stopButton";
            stopButton.Size = new Size(80, 23);
            stopButton.TabIndex = 13;
            stopButton.Text = "Stop";
            stopButton.UseVisualStyleBackColor = true;
            stopButton.Click += stopButton_Click;
            //
            // eventLogTextBox
            //
            eventLogTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            eventLogTextBox.Location = new Point(12, 219);
            eventLogTextBox.Multiline = true;
            eventLogTextBox.Name = "eventLogTextBox";
            eventLogTextBox.ReadOnly = true;
            eventLogTextBox.ScrollBars = ScrollBars.Vertical;
            eventLogTextBox.Size = new Size(776, 219);
            eventLogTextBox.TabIndex = 14;
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(serialRadioButton);
            Controls.Add(tcpRadioButton);
            Controls.Add(portComboBox);
            Controls.Add(tcpPortTextBox);
            Controls.Add(connectButton);
            Controls.Add(disconnectButton);
            Controls.Add(transportStatusLabel);
            Controls.Add(stateLabel);
            Controls.Add(progressBar);
            Controls.Add(planIdTextBox);
            Controls.Add(loadPlanButton);
            Controls.Add(armButton);
            Controls.Add(startButton);
            Controls.Add(stopButton);
            Controls.Add(eventLogTextBox);
            Name = "Form1";
            Text = "Medical Device Control Simulator";
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
