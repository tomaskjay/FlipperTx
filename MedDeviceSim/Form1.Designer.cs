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

        private System.Windows.Forms.ComboBox portComboBox;
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
            portComboBox = new ComboBox();
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
            // portComboBox
            //
            portComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            portComboBox.Location = new Point(12, 12);
            portComboBox.Name = "portComboBox";
            portComboBox.Size = new Size(150, 23);
            portComboBox.TabIndex = 0;
            //
            // connectButton
            //
            connectButton.Location = new Point(168, 12);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(90, 23);
            connectButton.TabIndex = 1;
            connectButton.Text = "Connect";
            connectButton.UseVisualStyleBackColor = true;
            connectButton.Click += connectButton_Click;
            //
            // disconnectButton
            //
            disconnectButton.Enabled = false;
            disconnectButton.Location = new Point(264, 12);
            disconnectButton.Name = "disconnectButton";
            disconnectButton.Size = new Size(90, 23);
            disconnectButton.TabIndex = 2;
            disconnectButton.Text = "Disconnect";
            disconnectButton.UseVisualStyleBackColor = true;
            disconnectButton.Click += disconnectButton_Click;
            //
            // transportStatusLabel
            //
            transportStatusLabel.AutoSize = true;
            transportStatusLabel.Location = new Point(12, 50);
            transportStatusLabel.Name = "transportStatusLabel";
            transportStatusLabel.Size = new Size(100, 15);
            transportStatusLabel.TabIndex = 3;
            transportStatusLabel.Text = "Transport: Closed";
            //
            // stateLabel
            //
            stateLabel.AutoSize = true;
            stateLabel.Location = new Point(12, 72);
            stateLabel.Name = "stateLabel";
            stateLabel.Size = new Size(114, 15);
            stateLabel.TabIndex = 4;
            stateLabel.Text = "Workflow: Disconnected";
            //
            // progressBar
            //
            progressBar.Location = new Point(12, 95);
            progressBar.Maximum = 100;
            progressBar.Minimum = 0;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(342, 20);
            progressBar.TabIndex = 5;
            progressBar.Visible = false;
            //
            // planIdTextBox
            //
            planIdTextBox.Location = new Point(12, 125);
            planIdTextBox.Name = "planIdTextBox";
            planIdTextBox.PlaceholderText = "Plan ID";
            planIdTextBox.Size = new Size(150, 23);
            planIdTextBox.TabIndex = 6;
            //
            // loadPlanButton
            //
            loadPlanButton.Enabled = false;
            loadPlanButton.Location = new Point(168, 125);
            loadPlanButton.Name = "loadPlanButton";
            loadPlanButton.Size = new Size(90, 23);
            loadPlanButton.TabIndex = 7;
            loadPlanButton.Text = "Load Plan";
            loadPlanButton.UseVisualStyleBackColor = true;
            loadPlanButton.Click += loadPlanButton_Click;
            //
            // armButton
            //
            armButton.Enabled = false;
            armButton.Location = new Point(12, 155);
            armButton.Name = "armButton";
            armButton.Size = new Size(80, 23);
            armButton.TabIndex = 8;
            armButton.Text = "Arm";
            armButton.UseVisualStyleBackColor = true;
            armButton.Click += armButton_Click;
            //
            // startButton
            //
            startButton.Enabled = false;
            startButton.Location = new Point(98, 155);
            startButton.Name = "startButton";
            startButton.Size = new Size(80, 23);
            startButton.TabIndex = 9;
            startButton.Text = "Start";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += startButton_Click;
            //
            // stopButton
            //
            stopButton.Enabled = false;
            stopButton.Location = new Point(184, 155);
            stopButton.Name = "stopButton";
            stopButton.Size = new Size(80, 23);
            stopButton.TabIndex = 10;
            stopButton.Text = "Stop";
            stopButton.UseVisualStyleBackColor = true;
            stopButton.Click += stopButton_Click;
            //
            // eventLogTextBox
            //
            eventLogTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            eventLogTextBox.Location = new Point(12, 190);
            eventLogTextBox.Multiline = true;
            eventLogTextBox.Name = "eventLogTextBox";
            eventLogTextBox.ReadOnly = true;
            eventLogTextBox.ScrollBars = ScrollBars.Vertical;
            eventLogTextBox.Size = new Size(776, 248);
            eventLogTextBox.TabIndex = 11;
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(portComboBox);
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
