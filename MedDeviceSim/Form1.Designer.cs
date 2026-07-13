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
        private System.Windows.Forms.Label stateLabel;

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
            stateLabel = new Label();
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
            // stateLabel
            //
            stateLabel.AutoSize = true;
            stateLabel.Location = new Point(12, 50);
            stateLabel.Name = "stateLabel";
            stateLabel.Size = new Size(114, 15);
            stateLabel.TabIndex = 3;
            stateLabel.Text = "State: Disconnected";
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(portComboBox);
            Controls.Add(connectButton);
            Controls.Add(disconnectButton);
            Controls.Add(stateLabel);
            Name = "Form1";
            Text = "Medical Device Control Simulator";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
