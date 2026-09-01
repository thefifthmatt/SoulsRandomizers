namespace RandomizerCommon
{
    partial class PresetEditForm
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
            description = new System.Windows.Forms.TextBox();
            presetL = new System.Windows.Forms.Label();
            descriptionL = new System.Windows.Forms.Label();
            classesView = new System.Windows.Forms.TreeView();
            modeOopsall = new System.Windows.Forms.RadioButton();
            modeMultiply = new System.Windows.Forms.RadioButton();
            modeCustom = new System.Windows.Forms.RadioButton();
            oopsAllBox = new System.Windows.Forms.ComboBox();
            flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            multiplyBox = new System.Windows.Forms.ComboBox();
            globalPanel = new System.Windows.Forms.FlowLayoutPanel();
            presetSelect = new System.Windows.Forms.ComboBox();
            applyButton = new System.Windows.Forms.Button();
            saveAsButton = new System.Windows.Forms.Button();
            directoryButton = new System.Windows.Forms.Button();
            quickL = new System.Windows.Forms.Label();
            flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            quickSeparate = new System.Windows.Forms.LinkLabel();
            quickMergeMinor = new System.Windows.Forms.LinkLabel();
            quickMergeBoss = new System.Windows.Forms.LinkLabel();
            quickMergeAll = new System.Windows.Forms.LinkLabel();
            quickReplace10 = new System.Windows.Forms.LinkLabel();
            quickReplaceWildlife = new System.Windows.Forms.LinkLabel();
            quickReducePassive = new System.Windows.Forms.LinkLabel();
            okButton = new System.Windows.Forms.Button();
            resetButton = new System.Windows.Forms.Button();
            tabControl = new System.Windows.Forms.TabControl();
            flowLayoutPanel2.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // description
            // 
            description.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            description.Location = new System.Drawing.Point(146, 42);
            description.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            description.Multiline = true;
            description.Name = "description";
            description.Size = new System.Drawing.Size(482, 56);
            description.TabIndex = 3;
            description.TextChanged += opt_Changed;
            // 
            // presetL
            // 
            presetL.AutoSize = true;
            presetL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            presetL.Location = new System.Drawing.Point(14, 15);
            presetL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            presetL.Name = "presetL";
            presetL.Size = new System.Drawing.Size(49, 16);
            presetL.TabIndex = 0;
            presetL.Text = "Preset:";
            // 
            // descriptionL
            // 
            descriptionL.AutoSize = true;
            descriptionL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            descriptionL.Location = new System.Drawing.Point(14, 43);
            descriptionL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            descriptionL.Name = "descriptionL";
            descriptionL.Size = new System.Drawing.Size(78, 16);
            descriptionL.TabIndex = 2;
            descriptionL.Text = "Description:";
            // 
            // classesView
            // 
            classesView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            classesView.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            classesView.Location = new System.Drawing.Point(18, 264);
            classesView.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            classesView.Name = "classesView";
            classesView.Size = new System.Drawing.Size(266, 277);
            classesView.TabIndex = 6;
            classesView.AfterSelect += classesView_AfterSelect;
            // 
            // modeOopsall
            // 
            modeOopsall.AutoSize = true;
            modeOopsall.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            modeOopsall.Location = new System.Drawing.Point(4, 29);
            modeOopsall.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            modeOopsall.Name = "modeOopsall";
            modeOopsall.Size = new System.Drawing.Size(76, 20);
            modeOopsall.TabIndex = 1;
            modeOopsall.Text = "Oops All";
            modeOopsall.UseVisualStyleBackColor = true;
            modeOopsall.CheckedChanged += mode_Changed;
            // 
            // modeMultiply
            // 
            modeMultiply.AutoSize = true;
            modeMultiply.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            modeMultiply.Location = new System.Drawing.Point(4, 58);
            modeMultiply.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            modeMultiply.Name = "modeMultiply";
            modeMultiply.Size = new System.Drawing.Size(130, 20);
            modeMultiply.TabIndex = 3;
            modeMultiply.Text = "Enemy Onslaught";
            modeMultiply.UseVisualStyleBackColor = true;
            modeMultiply.CheckedChanged += mode_Changed;
            // 
            // modeCustom
            // 
            modeCustom.AutoSize = true;
            modeCustom.Checked = true;
            flowLayoutPanel2.SetFlowBreak(modeCustom, true);
            modeCustom.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            modeCustom.Location = new System.Drawing.Point(4, 3);
            modeCustom.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            modeCustom.Name = "modeCustom";
            modeCustom.Size = new System.Drawing.Size(70, 20);
            modeCustom.TabIndex = 0;
            modeCustom.TabStop = true;
            modeCustom.Text = "Custom";
            modeCustom.UseVisualStyleBackColor = true;
            modeCustom.CheckedChanged += mode_Changed;
            // 
            // oopsAllBox
            // 
            flowLayoutPanel2.SetFlowBreak(oopsAllBox, true);
            oopsAllBox.FormattingEnabled = true;
            oopsAllBox.Location = new System.Drawing.Point(88, 29);
            oopsAllBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            oopsAllBox.Name = "oopsAllBox";
            oopsAllBox.Size = new System.Drawing.Size(248, 23);
            oopsAllBox.TabIndex = 2;
            oopsAllBox.SelectedIndexChanged += opt_Changed;
            // 
            // flowLayoutPanel2
            // 
            flowLayoutPanel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            flowLayoutPanel2.BackColor = System.Drawing.Color.Transparent;
            flowLayoutPanel2.Controls.Add(modeCustom);
            flowLayoutPanel2.Controls.Add(modeOopsall);
            flowLayoutPanel2.Controls.Add(oopsAllBox);
            flowLayoutPanel2.Controls.Add(modeMultiply);
            flowLayoutPanel2.Controls.Add(multiplyBox);
            flowLayoutPanel2.Location = new System.Drawing.Point(636, 10);
            flowLayoutPanel2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            flowLayoutPanel2.Name = "flowLayoutPanel2";
            flowLayoutPanel2.Size = new System.Drawing.Size(440, 115);
            flowLayoutPanel2.TabIndex = 4;
            // 
            // multiplyBox
            // 
            flowLayoutPanel2.SetFlowBreak(multiplyBox, true);
            multiplyBox.FormattingEnabled = true;
            multiplyBox.Location = new System.Drawing.Point(142, 58);
            multiplyBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            multiplyBox.Name = "multiplyBox";
            multiplyBox.Size = new System.Drawing.Size(76, 23);
            multiplyBox.TabIndex = 4;
            multiplyBox.TextChanged += multiplyBox_TextChanged;
            // 
            // globalPanel
            // 
            globalPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            globalPanel.AutoScroll = true;
            globalPanel.BackColor = System.Drawing.Color.Transparent;
            globalPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            globalPanel.Location = new System.Drawing.Point(18, 106);
            globalPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            globalPanel.Name = "globalPanel";
            globalPanel.Padding = new System.Windows.Forms.Padding(0, 0, 0, 20);
            globalPanel.Size = new System.Drawing.Size(1058, 152);
            globalPanel.TabIndex = 5;
            // 
            // presetSelect
            // 
            presetSelect.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            presetSelect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            presetSelect.FormattingEnabled = true;
            presetSelect.Location = new System.Drawing.Point(146, 12);
            presetSelect.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            presetSelect.Name = "presetSelect";
            presetSelect.Size = new System.Drawing.Size(482, 23);
            presetSelect.TabIndex = 1;
            presetSelect.SelectedIndexChanged += presetSelect_SelectedIndexChanged;
            // 
            // applyButton
            // 
            applyButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            applyButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            applyButton.Location = new System.Drawing.Point(719, 603);
            applyButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            applyButton.Name = "applyButton";
            applyButton.Size = new System.Drawing.Size(175, 32);
            applyButton.TabIndex = 12;
            applyButton.Text = "Apply";
            applyButton.UseVisualStyleBackColor = true;
            applyButton.Click += applyButton_Click;
            // 
            // saveAsButton
            // 
            saveAsButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            saveAsButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            saveAsButton.Location = new System.Drawing.Point(537, 603);
            saveAsButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            saveAsButton.Name = "saveAsButton";
            saveAsButton.Size = new System.Drawing.Size(175, 32);
            saveAsButton.TabIndex = 11;
            saveAsButton.Text = "Save as...";
            saveAsButton.UseVisualStyleBackColor = true;
            saveAsButton.Click += saveAsButton_Click;
            // 
            // directoryButton
            // 
            directoryButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            directoryButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            directoryButton.Location = new System.Drawing.Point(14, 603);
            directoryButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            directoryButton.Name = "directoryButton";
            directoryButton.Size = new System.Drawing.Size(271, 32);
            directoryButton.TabIndex = 9;
            directoryButton.Text = "Open presets directory";
            directoryButton.UseVisualStyleBackColor = true;
            directoryButton.Click += directoryButton_Click;
            // 
            // quickL
            // 
            quickL.AutoSize = true;
            quickL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickL.Location = new System.Drawing.Point(4, 0);
            quickL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickL.Name = "quickL";
            quickL.Size = new System.Drawing.Size(76, 16);
            quickL.TabIndex = 0;
            quickL.Text = "Quick edits:";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            flowLayoutPanel1.BackColor = System.Drawing.Color.Transparent;
            flowLayoutPanel1.Controls.Add(quickL);
            flowLayoutPanel1.Controls.Add(quickSeparate);
            flowLayoutPanel1.Controls.Add(quickMergeMinor);
            flowLayoutPanel1.Controls.Add(quickMergeBoss);
            flowLayoutPanel1.Controls.Add(quickMergeAll);
            flowLayoutPanel1.Controls.Add(quickReplace10);
            flowLayoutPanel1.Controls.Add(quickReplaceWildlife);
            flowLayoutPanel1.Controls.Add(quickReducePassive);
            flowLayoutPanel1.Location = new System.Drawing.Point(18, 545);
            flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new System.Drawing.Size(1058, 57);
            flowLayoutPanel1.TabIndex = 8;
            // 
            // quickSeparate
            // 
            quickSeparate.AutoSize = true;
            quickSeparate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickSeparate.Location = new System.Drawing.Point(88, 0);
            quickSeparate.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickSeparate.Name = "quickSeparate";
            quickSeparate.Size = new System.Drawing.Size(213, 16);
            quickSeparate.TabIndex = 1;
            quickSeparate.TabStop = true;
            quickSeparate.Text = "Split up enemy categories (default)";
            quickSeparate.LinkClicked += quickSeparate_LinkClicked;
            // 
            // quickMergeMinor
            // 
            quickMergeMinor.AutoSize = true;
            quickMergeMinor.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickMergeMinor.Location = new System.Drawing.Point(309, 0);
            quickMergeMinor.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickMergeMinor.Name = "quickMergeMinor";
            quickMergeMinor.Size = new System.Drawing.Size(157, 16);
            quickMergeMinor.TabIndex = 7;
            quickMergeMinor.TabStop = true;
            quickMergeMinor.Text = "Merge non-major bosses";
            quickMergeMinor.LinkClicked += quickMergeMinor_LinkClicked;
            // 
            // quickMergeBoss
            // 
            quickMergeBoss.AutoSize = true;
            quickMergeBoss.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickMergeBoss.Location = new System.Drawing.Point(474, 0);
            quickMergeBoss.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickMergeBoss.Name = "quickMergeBoss";
            quickMergeBoss.Size = new System.Drawing.Size(111, 16);
            quickMergeBoss.TabIndex = 2;
            quickMergeBoss.TabStop = true;
            quickMergeBoss.Text = "Merge all bosses";
            quickMergeBoss.LinkClicked += quickMergeBoss_LinkClicked;
            // 
            // quickMergeAll
            // 
            quickMergeAll.AutoSize = true;
            quickMergeAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickMergeAll.Location = new System.Drawing.Point(593, 0);
            quickMergeAll.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickMergeAll.Name = "quickMergeAll";
            quickMergeAll.Size = new System.Drawing.Size(192, 16);
            quickMergeAll.TabIndex = 3;
            quickMergeAll.TabStop = true;
            quickMergeAll.Text = "Merge all bosses and enemies";
            quickMergeAll.LinkClicked += quickMergeAll_LinkClicked;
            // 
            // quickReplace10
            // 
            quickReplace10.AutoSize = true;
            quickReplace10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickReplace10.Location = new System.Drawing.Point(4, 16);
            quickReplace10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickReplace10.Name = "quickReplace10";
            quickReplace10.Size = new System.Drawing.Size(275, 16);
            quickReplace10.TabIndex = 4;
            quickReplace10.TabStop = true;
            quickReplace10.Text = "Replace 10% of regular enemies with bosses";
            quickReplace10.LinkClicked += quickReplace10_LinkClicked;
            // 
            // quickReplaceWildlife
            // 
            quickReplaceWildlife.AutoSize = true;
            quickReplaceWildlife.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickReplaceWildlife.Location = new System.Drawing.Point(287, 16);
            quickReplaceWildlife.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickReplaceWildlife.Name = "quickReplaceWildlife";
            quickReplaceWildlife.Size = new System.Drawing.Size(227, 16);
            quickReplaceWildlife.TabIndex = 5;
            quickReplaceWildlife.TabStop = true;
            quickReplaceWildlife.Text = "Replace wildlife with regular enemies";
            quickReplaceWildlife.LinkClicked += quickReplaceWildlife_LinkClicked;
            // 
            // quickReducePassive
            // 
            quickReducePassive.AutoSize = true;
            quickReducePassive.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            quickReducePassive.Location = new System.Drawing.Point(522, 16);
            quickReducePassive.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            quickReducePassive.Name = "quickReducePassive";
            quickReducePassive.Size = new System.Drawing.Size(212, 16);
            quickReducePassive.TabIndex = 6;
            quickReducePassive.TabStop = true;
            quickReducePassive.Text = "Reduce passive enemy frequency";
            quickReducePassive.LinkClicked += quickReducePassive_LinkClicked;
            // 
            // okButton
            // 
            okButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            okButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            okButton.Location = new System.Drawing.Point(901, 603);
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
            resetButton.Location = new System.Drawing.Point(355, 603);
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
            tabControl.Location = new System.Drawing.Point(292, 264);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new System.Drawing.Size(784, 277);
            tabControl.TabIndex = 15;
            // 
            // PresetEditForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1090, 647);
            Controls.Add(tabControl);
            Controls.Add(okButton);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(directoryButton);
            Controls.Add(saveAsButton);
            Controls.Add(applyButton);
            Controls.Add(presetSelect);
            Controls.Add(globalPanel);
            Controls.Add(flowLayoutPanel2);
            Controls.Add(classesView);
            Controls.Add(presetL);
            Controls.Add(descriptionL);
            Controls.Add(description);
            Controls.Add(resetButton);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MinimumSize = new System.Drawing.Size(1047, 456);
            Name = "PresetEditForm";
            Text = "Customize enemy placement";
            FormClosing += PresetEditForm_FormClosing;
            Load += PresetEditForm_Load;
            flowLayoutPanel2.ResumeLayout(false);
            flowLayoutPanel2.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.TextBox description;
        private System.Windows.Forms.Label presetL;
        private System.Windows.Forms.Label descriptionL;
        private System.Windows.Forms.TreeView classesView;
        private System.Windows.Forms.RadioButton modeOopsall;
        private System.Windows.Forms.RadioButton modeMultiply;
        private System.Windows.Forms.RadioButton modeCustom;
        private System.Windows.Forms.ComboBox oopsAllBox;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.FlowLayoutPanel globalPanel;
        private System.Windows.Forms.ComboBox presetSelect;
        private System.Windows.Forms.Button applyButton;
        private System.Windows.Forms.Button saveAsButton;
        private System.Windows.Forms.Button directoryButton;
        private System.Windows.Forms.ComboBox multiplyBox;
        private System.Windows.Forms.Label quickL;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.LinkLabel quickSeparate;
        private System.Windows.Forms.LinkLabel quickMergeBoss;
        private System.Windows.Forms.LinkLabel quickMergeAll;
        private System.Windows.Forms.LinkLabel quickReplace10;
        private System.Windows.Forms.LinkLabel quickReducePassive;
        private System.Windows.Forms.LinkLabel quickReplaceWildlife;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.LinkLabel quickMergeMinor;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.TabControl tabControl;
    }
}