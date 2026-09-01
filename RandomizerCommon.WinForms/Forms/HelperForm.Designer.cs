namespace RandomizerCommon
{
    partial class HelperForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            helperL = new System.Windows.Forms.Label();
            panel = new System.Windows.Forms.FlowLayoutPanel();
            okButton = new System.Windows.Forms.Button();
            resetButton = new System.Windows.Forms.Button();
            activity = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // helperL
            // 
            helperL.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            helperL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            helperL.Location = new System.Drawing.Point(13, 9);
            helperL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            helperL.Name = "helperL";
            helperL.Size = new System.Drawing.Size(658, 36);
            helperL.TabIndex = 1;
            helperL.Text = "When RandomizerHelper is active, changes to these settings will immediately apply without needing to rerun randomizer or restart the game.";
            // 
            // panel
            // 
            panel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel.AutoScroll = true;
            panel.BackColor = System.Drawing.Color.Transparent;
            panel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panel.Location = new System.Drawing.Point(13, 48);
            panel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            panel.Name = "panel";
            panel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 20);
            panel.Size = new System.Drawing.Size(658, 531);
            panel.TabIndex = 6;
            // 
            // okButton
            // 
            okButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            okButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            okButton.Location = new System.Drawing.Point(496, 614);
            okButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(175, 36);
            okButton.TabIndex = 15;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // resetButton
            // 
            resetButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            resetButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            resetButton.Location = new System.Drawing.Point(313, 614);
            resetButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            resetButton.Name = "resetButton";
            resetButton.Size = new System.Drawing.Size(175, 36);
            resetButton.TabIndex = 14;
            resetButton.Text = "Reset...";
            resetButton.UseVisualStyleBackColor = true;
            resetButton.Click += resetButton_Click;
            // 
            // activity
            // 
            activity.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            activity.BackColor = System.Drawing.Color.Transparent;
            activity.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            activity.Location = new System.Drawing.Point(13, 586);
            activity.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            activity.Name = "activity";
            activity.Size = new System.Drawing.Size(658, 20);
            activity.TabIndex = 16;
            activity.Text = "No activity detected";
            // 
            // HelperForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(684, 661);
            Controls.Add(activity);
            Controls.Add(okButton);
            Controls.Add(resetButton);
            Controls.Add(panel);
            Controls.Add(helperL);
            MaximumSize = new System.Drawing.Size(700, 800);
            MinimumSize = new System.Drawing.Size(700, 300);
            Name = "HelperForm";
            Text = "Configure RandomizerHelper.dll";
            FormClosing += HelperForm_FormClosing;
            Load += HelperForm_Load;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label helperL;
        private System.Windows.Forms.FlowLayoutPanel panel;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Label activity;
    }
}