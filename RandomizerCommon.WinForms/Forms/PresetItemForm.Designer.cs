namespace RandomizerCommon
{
    partial class PresetItemForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PresetItemForm));
            classesView = new System.Windows.Forms.TreeView();
            globalPanel = new System.Windows.Forms.FlowLayoutPanel();
            applyButton = new System.Windows.Forms.Button();
            okButton = new System.Windows.Forms.Button();
            resetButton = new System.Windows.Forms.Button();
            tabControl = new System.Windows.Forms.TabControl();
            alphaL = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // classesView
            // 
            classesView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            classesView.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            classesView.Location = new System.Drawing.Point(19, 170);
            classesView.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            classesView.Name = "classesView";
            classesView.Size = new System.Drawing.Size(266, 357);
            classesView.TabIndex = 6;
            classesView.AfterSelect += classesView_AfterSelect;
            // 
            // globalPanel
            // 
            globalPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            globalPanel.AutoScroll = true;
            globalPanel.BackColor = System.Drawing.Color.Transparent;
            globalPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            globalPanel.Location = new System.Drawing.Point(19, 12);
            globalPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            globalPanel.Name = "globalPanel";
            globalPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 20);
            globalPanel.Size = new System.Drawing.Size(1052, 152);
            globalPanel.TabIndex = 5;
            // 
            // applyButton
            // 
            applyButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            applyButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            applyButton.Location = new System.Drawing.Point(714, 568);
            applyButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            applyButton.Name = "applyButton";
            applyButton.Size = new System.Drawing.Size(175, 32);
            applyButton.TabIndex = 12;
            applyButton.Text = "Apply";
            applyButton.UseVisualStyleBackColor = true;
            applyButton.Click += applyButton_Click;
            // 
            // okButton
            // 
            okButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            okButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            okButton.Location = new System.Drawing.Point(896, 568);
            okButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(175, 32);
            okButton.TabIndex = 13;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // resetButton
            // 
            resetButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            resetButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            resetButton.Location = new System.Drawing.Point(531, 568);
            resetButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            resetButton.Name = "resetButton";
            resetButton.Size = new System.Drawing.Size(175, 32);
            resetButton.TabIndex = 10;
            resetButton.Text = "Reset...";
            resetButton.UseVisualStyleBackColor = true;
            resetButton.Click += resetButton_Click;
            // 
            // tabControl
            // 
            tabControl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tabControl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            tabControl.Location = new System.Drawing.Point(292, 170);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new System.Drawing.Size(780, 357);
            tabControl.TabIndex = 14;
            // 
            // alphaL
            // 
            alphaL.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            alphaL.BackColor = System.Drawing.Color.Transparent;
            alphaL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            alphaL.Location = new System.Drawing.Point(19, 530);
            alphaL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            alphaL.Name = "alphaL";
            alphaL.Size = new System.Drawing.Size(1052, 34);
            alphaL.TabIndex = 15;
            alphaL.Text = resources.GetString("alphaL.Text");
            // 
            // PresetItemForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1084, 611);
            Controls.Add(alphaL);
            Controls.Add(tabControl);
            Controls.Add(okButton);
            Controls.Add(applyButton);
            Controls.Add(globalPanel);
            Controls.Add(classesView);
            Controls.Add(resetButton);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MinimumSize = new System.Drawing.Size(1000, 500);
            Name = "PresetItemForm";
            Text = "Customize item placement";
            FormClosing += PresetItemForm_FormClosing;
            Load += PresetItemForm_Load;
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.TreeView classesView;
        private System.Windows.Forms.FlowLayoutPanel globalPanel;
        private System.Windows.Forms.Button applyButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.Label alphaL;
    }
}
