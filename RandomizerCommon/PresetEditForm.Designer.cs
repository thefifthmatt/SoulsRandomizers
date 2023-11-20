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
            this.description = new System.Windows.Forms.TextBox();
            this.presetL = new System.Windows.Forms.Label();
            this.descriptionL = new System.Windows.Forms.Label();
            this.customPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.poolTitle = new System.Windows.Forms.Label();
            this.poolDesc = new System.Windows.Forms.Label();
            this.classInherit = new System.Windows.Forms.RadioButton();
            this.classMerge = new System.Windows.Forms.RadioButton();
            this.classMerge2 = new System.Windows.Forms.RadioButton();
            this.classNorandom = new System.Windows.Forms.RadioButton();
            this.classRandom = new System.Windows.Forms.RadioButton();
            this.classCustom = new System.Windows.Forms.RadioButton();
            this.classMultiplyBox = new System.Windows.Forms.ComboBox();
            this.newPoolBreak = new System.Windows.Forms.Label();
            this.newPoolButton = new System.Windows.Forms.Button();
            this.classesView = new System.Windows.Forms.TreeView();
            this.modeOopsall = new System.Windows.Forms.RadioButton();
            this.modeMultiply = new System.Windows.Forms.RadioButton();
            this.modeCustom = new System.Windows.Forms.RadioButton();
            this.oopsAllBox = new System.Windows.Forms.ComboBox();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.multiplyBox = new System.Windows.Forms.ComboBox();
            this.globalPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.presetSelect = new System.Windows.Forms.ComboBox();
            this.applyButton = new System.Windows.Forms.Button();
            this.saveAsButton = new System.Windows.Forms.Button();
            this.directoryButton = new System.Windows.Forms.Button();
            this.quickL = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.quickSeparate = new System.Windows.Forms.LinkLabel();
            this.quickMergeMinor = new System.Windows.Forms.LinkLabel();
            this.quickMergeBoss = new System.Windows.Forms.LinkLabel();
            this.quickMergeAll = new System.Windows.Forms.LinkLabel();
            this.quickReplace10 = new System.Windows.Forms.LinkLabel();
            this.quickReplaceWildlife = new System.Windows.Forms.LinkLabel();
            this.quickReducePassive = new System.Windows.Forms.LinkLabel();
            this.okButton = new System.Windows.Forms.Button();
            this.resetButton = new System.Windows.Forms.Button();
            this.customPanel.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // description
            // 
            this.description.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.description.Location = new System.Drawing.Point(146, 42);
            this.description.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.description.Multiline = true;
            this.description.Name = "description";
            this.description.Size = new System.Drawing.Size(482, 56);
            this.description.TabIndex = 3;
            this.description.TextChanged += new System.EventHandler(this.opt_Changed);
            // 
            // presetL
            // 
            this.presetL.AutoSize = true;
            this.presetL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.presetL.Location = new System.Drawing.Point(14, 15);
            this.presetL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.presetL.Name = "presetL";
            this.presetL.Size = new System.Drawing.Size(49, 16);
            this.presetL.TabIndex = 0;
            this.presetL.Text = "Preset:";
            // 
            // descriptionL
            // 
            this.descriptionL.AutoSize = true;
            this.descriptionL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.descriptionL.Location = new System.Drawing.Point(14, 43);
            this.descriptionL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.descriptionL.Name = "descriptionL";
            this.descriptionL.Size = new System.Drawing.Size(78, 16);
            this.descriptionL.TabIndex = 2;
            this.descriptionL.Text = "Description:";
            // 
            // customPanel
            // 
            this.customPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.customPanel.AutoScroll = true;
            this.customPanel.Controls.Add(this.poolTitle);
            this.customPanel.Controls.Add(this.poolDesc);
            this.customPanel.Controls.Add(this.classInherit);
            this.customPanel.Controls.Add(this.classMerge);
            this.customPanel.Controls.Add(this.classMerge2);
            this.customPanel.Controls.Add(this.classNorandom);
            this.customPanel.Controls.Add(this.classRandom);
            this.customPanel.Controls.Add(this.classCustom);
            this.customPanel.Controls.Add(this.classMultiplyBox);
            this.customPanel.Controls.Add(this.newPoolBreak);
            this.customPanel.Controls.Add(this.newPoolButton);
            this.customPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.customPanel.Location = new System.Drawing.Point(292, 264);
            this.customPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.customPanel.Name = "customPanel";
            this.customPanel.Size = new System.Drawing.Size(784, 278);
            this.customPanel.TabIndex = 7;
            // 
            // poolTitle
            // 
            this.poolTitle.AutoSize = true;
            this.customPanel.SetFlowBreak(this.poolTitle, true);
            this.poolTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.poolTitle.Location = new System.Drawing.Point(4, 6);
            this.poolTitle.Margin = new System.Windows.Forms.Padding(4, 6, 4, 0);
            this.poolTitle.Name = "poolTitle";
            this.poolTitle.Size = new System.Drawing.Size(98, 16);
            this.poolTitle.TabIndex = 0;
            this.poolTitle.Text = "Configuration";
            // 
            // poolDesc
            // 
            this.poolDesc.AutoSize = true;
            this.customPanel.SetFlowBreak(this.poolDesc, true);
            this.poolDesc.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.poolDesc.Location = new System.Drawing.Point(4, 28);
            this.poolDesc.Margin = new System.Windows.Forms.Padding(4, 6, 4, 0);
            this.poolDesc.Name = "poolDesc";
            this.poolDesc.Size = new System.Drawing.Size(85, 16);
            this.poolDesc.TabIndex = 1;
            this.poolDesc.Text = "Configuration";
            // 
            // classInherit
            // 
            this.classInherit.AutoSize = true;
            this.classInherit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classInherit.Location = new System.Drawing.Point(4, 51);
            this.classInherit.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classInherit.Name = "classInherit";
            this.classInherit.Size = new System.Drawing.Size(120, 20);
            this.classInherit.TabIndex = 2;
            this.classInherit.Text = "Same as parent";
            this.classInherit.UseVisualStyleBackColor = true;
            this.classInherit.CheckedChanged += new System.EventHandler(this.classMode_CheckChanged);
            // 
            // classMerge
            // 
            this.classMerge.AutoSize = true;
            this.classMerge.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classMerge.Location = new System.Drawing.Point(132, 51);
            this.classMerge.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classMerge.Name = "classMerge";
            this.classMerge.Size = new System.Drawing.Size(130, 20);
            this.classMerge.TabIndex = 3;
            this.classMerge.Text = "Merge with parent";
            this.classMerge.UseVisualStyleBackColor = true;
            this.classMerge.CheckedChanged += new System.EventHandler(this.classMode_CheckChanged);
            // 
            // classMerge2
            // 
            this.classMerge2.AutoSize = true;
            this.classMerge2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classMerge2.Location = new System.Drawing.Point(270, 51);
            this.classMerge2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classMerge2.Name = "classMerge2";
            this.classMerge2.Size = new System.Drawing.Size(130, 20);
            this.classMerge2.TabIndex = 9;
            this.classMerge2.Text = "Merge with parent";
            this.classMerge2.UseVisualStyleBackColor = true;
            this.classMerge2.CheckedChanged += new System.EventHandler(this.classMode_CheckChanged);
            // 
            // classNorandom
            // 
            this.classNorandom.AutoSize = true;
            this.classNorandom.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classNorandom.Location = new System.Drawing.Point(408, 51);
            this.classNorandom.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classNorandom.Name = "classNorandom";
            this.classNorandom.Size = new System.Drawing.Size(120, 20);
            this.classNorandom.TabIndex = 4;
            this.classNorandom.Text = "Not randomized";
            this.classNorandom.UseVisualStyleBackColor = true;
            this.classNorandom.CheckedChanged += new System.EventHandler(this.classMode_CheckChanged);
            // 
            // classRandom
            // 
            this.classRandom.AutoSize = true;
            this.classRandom.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classRandom.Location = new System.Drawing.Point(536, 51);
            this.classRandom.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classRandom.Name = "classRandom";
            this.classRandom.Size = new System.Drawing.Size(102, 20);
            this.classRandom.TabIndex = 5;
            this.classRandom.Text = "Randomized";
            this.classRandom.UseVisualStyleBackColor = true;
            this.classRandom.CheckedChanged += new System.EventHandler(this.classMode_CheckChanged);
            // 
            // classCustom
            // 
            this.classCustom.AutoSize = true;
            this.classCustom.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classCustom.Location = new System.Drawing.Point(646, 51);
            this.classCustom.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classCustom.Name = "classCustom";
            this.classCustom.Size = new System.Drawing.Size(70, 20);
            this.classCustom.TabIndex = 6;
            this.classCustom.Text = "Custom";
            this.classCustom.UseVisualStyleBackColor = true;
            this.classCustom.CheckedChanged += new System.EventHandler(this.classMode_CheckChanged);
            // 
            // classMultiplyBox
            // 
            this.classMultiplyBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.classMultiplyBox.FormattingEnabled = true;
            this.classMultiplyBox.Location = new System.Drawing.Point(4, 77);
            this.classMultiplyBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classMultiplyBox.Name = "classMultiplyBox";
            this.classMultiplyBox.Size = new System.Drawing.Size(76, 24);
            this.classMultiplyBox.TabIndex = 10;
            this.classMultiplyBox.SelectedIndexChanged += new System.EventHandler(this.opt_Changed);
            // 
            // newPoolBreak
            // 
            this.newPoolBreak.AutoSize = true;
            this.customPanel.SetFlowBreak(this.newPoolBreak, true);
            this.newPoolBreak.Location = new System.Drawing.Point(88, 74);
            this.newPoolBreak.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.newPoolBreak.Name = "newPoolBreak";
            this.newPoolBreak.Size = new System.Drawing.Size(0, 16);
            this.newPoolBreak.TabIndex = 7;
            // 
            // newPoolButton
            // 
            this.customPanel.SetFlowBreak(this.newPoolButton, true);
            this.newPoolButton.Location = new System.Drawing.Point(4, 107);
            this.newPoolButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.newPoolButton.Name = "newPoolButton";
            this.newPoolButton.Size = new System.Drawing.Size(58, 27);
            this.newPoolButton.TabIndex = 8;
            this.newPoolButton.Text = "+";
            this.newPoolButton.UseVisualStyleBackColor = true;
            // 
            // classesView
            // 
            this.classesView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.classesView.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.classesView.Location = new System.Drawing.Point(18, 264);
            this.classesView.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.classesView.Name = "classesView";
            this.classesView.Size = new System.Drawing.Size(266, 277);
            this.classesView.TabIndex = 6;
            this.classesView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.classesView_AfterSelect);
            // 
            // modeOopsall
            // 
            this.modeOopsall.AutoSize = true;
            this.modeOopsall.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.modeOopsall.Location = new System.Drawing.Point(4, 29);
            this.modeOopsall.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.modeOopsall.Name = "modeOopsall";
            this.modeOopsall.Size = new System.Drawing.Size(76, 20);
            this.modeOopsall.TabIndex = 1;
            this.modeOopsall.Text = "Oops All";
            this.modeOopsall.UseVisualStyleBackColor = true;
            this.modeOopsall.CheckedChanged += new System.EventHandler(this.mode_Changed);
            // 
            // modeMultiply
            // 
            this.modeMultiply.AutoSize = true;
            this.modeMultiply.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.modeMultiply.Location = new System.Drawing.Point(4, 58);
            this.modeMultiply.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.modeMultiply.Name = "modeMultiply";
            this.modeMultiply.Size = new System.Drawing.Size(130, 20);
            this.modeMultiply.TabIndex = 3;
            this.modeMultiply.Text = "Enemy Onslaught";
            this.modeMultiply.UseVisualStyleBackColor = true;
            this.modeMultiply.CheckedChanged += new System.EventHandler(this.mode_Changed);
            // 
            // modeCustom
            // 
            this.modeCustom.AutoSize = true;
            this.modeCustom.Checked = true;
            this.flowLayoutPanel2.SetFlowBreak(this.modeCustom, true);
            this.modeCustom.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.modeCustom.Location = new System.Drawing.Point(4, 3);
            this.modeCustom.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.modeCustom.Name = "modeCustom";
            this.modeCustom.Size = new System.Drawing.Size(70, 20);
            this.modeCustom.TabIndex = 0;
            this.modeCustom.TabStop = true;
            this.modeCustom.Text = "Custom";
            this.modeCustom.UseVisualStyleBackColor = true;
            this.modeCustom.CheckedChanged += new System.EventHandler(this.mode_Changed);
            // 
            // oopsAllBox
            // 
            this.flowLayoutPanel2.SetFlowBreak(this.oopsAllBox, true);
            this.oopsAllBox.FormattingEnabled = true;
            this.oopsAllBox.Location = new System.Drawing.Point(88, 29);
            this.oopsAllBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.oopsAllBox.Name = "oopsAllBox";
            this.oopsAllBox.Size = new System.Drawing.Size(248, 23);
            this.oopsAllBox.TabIndex = 2;
            this.oopsAllBox.SelectedIndexChanged += new System.EventHandler(this.opt_Changed);
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel2.BackColor = System.Drawing.Color.Transparent;
            this.flowLayoutPanel2.Controls.Add(this.modeCustom);
            this.flowLayoutPanel2.Controls.Add(this.modeOopsall);
            this.flowLayoutPanel2.Controls.Add(this.oopsAllBox);
            this.flowLayoutPanel2.Controls.Add(this.modeMultiply);
            this.flowLayoutPanel2.Controls.Add(this.multiplyBox);
            this.flowLayoutPanel2.Location = new System.Drawing.Point(636, 10);
            this.flowLayoutPanel2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(440, 115);
            this.flowLayoutPanel2.TabIndex = 4;
            // 
            // multiplyBox
            // 
            this.multiplyBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.flowLayoutPanel2.SetFlowBreak(this.multiplyBox, true);
            this.multiplyBox.FormattingEnabled = true;
            this.multiplyBox.Location = new System.Drawing.Point(142, 58);
            this.multiplyBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.multiplyBox.Name = "multiplyBox";
            this.multiplyBox.Size = new System.Drawing.Size(76, 23);
            this.multiplyBox.TabIndex = 4;
            this.multiplyBox.SelectedIndexChanged += new System.EventHandler(this.multiplyBox_SelectedIndexChanged);
            // 
            // globalPanel
            // 
            this.globalPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.globalPanel.AutoScroll = true;
            this.globalPanel.BackColor = System.Drawing.Color.Transparent;
            this.globalPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.globalPanel.Location = new System.Drawing.Point(18, 106);
            this.globalPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.globalPanel.Name = "globalPanel";
            this.globalPanel.Size = new System.Drawing.Size(1058, 152);
            this.globalPanel.TabIndex = 5;
            // 
            // presetSelect
            // 
            this.presetSelect.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.presetSelect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.presetSelect.FormattingEnabled = true;
            this.presetSelect.Location = new System.Drawing.Point(146, 12);
            this.presetSelect.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.presetSelect.Name = "presetSelect";
            this.presetSelect.Size = new System.Drawing.Size(482, 23);
            this.presetSelect.TabIndex = 1;
            this.presetSelect.SelectedIndexChanged += new System.EventHandler(this.presetSelect_SelectedIndexChanged);
            // 
            // applyButton
            // 
            this.applyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.applyButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.applyButton.Location = new System.Drawing.Point(719, 603);
            this.applyButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.applyButton.Name = "applyButton";
            this.applyButton.Size = new System.Drawing.Size(175, 32);
            this.applyButton.TabIndex = 12;
            this.applyButton.Text = "Apply";
            this.applyButton.UseVisualStyleBackColor = true;
            this.applyButton.Click += new System.EventHandler(this.applyButton_Click);
            // 
            // saveAsButton
            // 
            this.saveAsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveAsButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.saveAsButton.Location = new System.Drawing.Point(537, 603);
            this.saveAsButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.saveAsButton.Name = "saveAsButton";
            this.saveAsButton.Size = new System.Drawing.Size(175, 32);
            this.saveAsButton.TabIndex = 11;
            this.saveAsButton.Text = "Save as...";
            this.saveAsButton.UseVisualStyleBackColor = true;
            this.saveAsButton.Click += new System.EventHandler(this.saveAsButton_Click);
            // 
            // directoryButton
            // 
            this.directoryButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.directoryButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.directoryButton.Location = new System.Drawing.Point(14, 603);
            this.directoryButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.directoryButton.Name = "directoryButton";
            this.directoryButton.Size = new System.Drawing.Size(271, 32);
            this.directoryButton.TabIndex = 9;
            this.directoryButton.Text = "Open presets directory";
            this.directoryButton.UseVisualStyleBackColor = true;
            this.directoryButton.Click += new System.EventHandler(this.directoryButton_Click);
            // 
            // quickL
            // 
            this.quickL.AutoSize = true;
            this.quickL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickL.Location = new System.Drawing.Point(4, 0);
            this.quickL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickL.Name = "quickL";
            this.quickL.Size = new System.Drawing.Size(76, 16);
            this.quickL.TabIndex = 0;
            this.quickL.Text = "Quick edits:";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel1.BackColor = System.Drawing.Color.Transparent;
            this.flowLayoutPanel1.Controls.Add(this.quickL);
            this.flowLayoutPanel1.Controls.Add(this.quickSeparate);
            this.flowLayoutPanel1.Controls.Add(this.quickMergeMinor);
            this.flowLayoutPanel1.Controls.Add(this.quickMergeBoss);
            this.flowLayoutPanel1.Controls.Add(this.quickMergeAll);
            this.flowLayoutPanel1.Controls.Add(this.quickReplace10);
            this.flowLayoutPanel1.Controls.Add(this.quickReplaceWildlife);
            this.flowLayoutPanel1.Controls.Add(this.quickReducePassive);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(18, 545);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(1058, 57);
            this.flowLayoutPanel1.TabIndex = 8;
            // 
            // quickSeparate
            // 
            this.quickSeparate.AutoSize = true;
            this.quickSeparate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickSeparate.Location = new System.Drawing.Point(88, 0);
            this.quickSeparate.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickSeparate.Name = "quickSeparate";
            this.quickSeparate.Size = new System.Drawing.Size(213, 16);
            this.quickSeparate.TabIndex = 1;
            this.quickSeparate.TabStop = true;
            this.quickSeparate.Text = "Split up enemy categories (default)";
            this.quickSeparate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickSeparate_LinkClicked);
            // 
            // quickMergeMinor
            // 
            this.quickMergeMinor.AutoSize = true;
            this.quickMergeMinor.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickMergeMinor.Location = new System.Drawing.Point(309, 0);
            this.quickMergeMinor.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickMergeMinor.Name = "quickMergeMinor";
            this.quickMergeMinor.Size = new System.Drawing.Size(157, 16);
            this.quickMergeMinor.TabIndex = 7;
            this.quickMergeMinor.TabStop = true;
            this.quickMergeMinor.Text = "Merge non-major bosses";
            this.quickMergeMinor.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickMergeMinor_LinkClicked);
            // 
            // quickMergeBoss
            // 
            this.quickMergeBoss.AutoSize = true;
            this.quickMergeBoss.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickMergeBoss.Location = new System.Drawing.Point(474, 0);
            this.quickMergeBoss.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickMergeBoss.Name = "quickMergeBoss";
            this.quickMergeBoss.Size = new System.Drawing.Size(111, 16);
            this.quickMergeBoss.TabIndex = 2;
            this.quickMergeBoss.TabStop = true;
            this.quickMergeBoss.Text = "Merge all bosses";
            this.quickMergeBoss.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickMergeBoss_LinkClicked);
            // 
            // quickMergeAll
            // 
            this.quickMergeAll.AutoSize = true;
            this.quickMergeAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickMergeAll.Location = new System.Drawing.Point(593, 0);
            this.quickMergeAll.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickMergeAll.Name = "quickMergeAll";
            this.quickMergeAll.Size = new System.Drawing.Size(192, 16);
            this.quickMergeAll.TabIndex = 3;
            this.quickMergeAll.TabStop = true;
            this.quickMergeAll.Text = "Merge all bosses and enemies";
            this.quickMergeAll.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickMergeAll_LinkClicked);
            // 
            // quickReplace10
            // 
            this.quickReplace10.AutoSize = true;
            this.quickReplace10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickReplace10.Location = new System.Drawing.Point(4, 16);
            this.quickReplace10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickReplace10.Name = "quickReplace10";
            this.quickReplace10.Size = new System.Drawing.Size(275, 16);
            this.quickReplace10.TabIndex = 4;
            this.quickReplace10.TabStop = true;
            this.quickReplace10.Text = "Replace 10% of regular enemies with bosses";
            this.quickReplace10.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickReplace10_LinkClicked);
            // 
            // quickReplaceWildlife
            // 
            this.quickReplaceWildlife.AutoSize = true;
            this.quickReplaceWildlife.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickReplaceWildlife.Location = new System.Drawing.Point(287, 16);
            this.quickReplaceWildlife.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickReplaceWildlife.Name = "quickReplaceWildlife";
            this.quickReplaceWildlife.Size = new System.Drawing.Size(227, 16);
            this.quickReplaceWildlife.TabIndex = 5;
            this.quickReplaceWildlife.TabStop = true;
            this.quickReplaceWildlife.Text = "Replace wildlife with regular enemies";
            this.quickReplaceWildlife.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickReplaceWildlife_LinkClicked);
            // 
            // quickReducePassive
            // 
            this.quickReducePassive.AutoSize = true;
            this.quickReducePassive.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.quickReducePassive.Location = new System.Drawing.Point(522, 16);
            this.quickReducePassive.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.quickReducePassive.Name = "quickReducePassive";
            this.quickReducePassive.Size = new System.Drawing.Size(212, 16);
            this.quickReducePassive.TabIndex = 6;
            this.quickReducePassive.TabStop = true;
            this.quickReducePassive.Text = "Reduce passive enemy frequency";
            this.quickReducePassive.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.quickReducePassive_LinkClicked);
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.okButton.Location = new System.Drawing.Point(901, 603);
            this.okButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(175, 32);
            this.okButton.TabIndex = 13;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // resetButton
            // 
            this.resetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.resetButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.resetButton.Location = new System.Drawing.Point(355, 603);
            this.resetButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(175, 32);
            this.resetButton.TabIndex = 10;
            this.resetButton.Text = "Reset...";
            this.resetButton.UseVisualStyleBackColor = true;
            this.resetButton.Click += new System.EventHandler(this.resetButton_Click);
            // 
            // PresetEditForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1090, 647);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.directoryButton);
            this.Controls.Add(this.saveAsButton);
            this.Controls.Add(this.applyButton);
            this.Controls.Add(this.presetSelect);
            this.Controls.Add(this.globalPanel);
            this.Controls.Add(this.flowLayoutPanel2);
            this.Controls.Add(this.classesView);
            this.Controls.Add(this.presetL);
            this.Controls.Add(this.customPanel);
            this.Controls.Add(this.descriptionL);
            this.Controls.Add(this.description);
            this.Controls.Add(this.resetButton);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MinimumSize = new System.Drawing.Size(1047, 456);
            this.Name = "PresetEditForm";
            this.Text = "Customize enemy placement";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PresetEditForm_FormClosing);
            this.Load += new System.EventHandler(this.PresetEditForm_Load);
            this.customPanel.ResumeLayout(false);
            this.customPanel.PerformLayout();
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox description;
        private System.Windows.Forms.Label presetL;
        private System.Windows.Forms.Label descriptionL;
        private System.Windows.Forms.FlowLayoutPanel customPanel;
        private System.Windows.Forms.TreeView classesView;
        private System.Windows.Forms.RadioButton modeOopsall;
        private System.Windows.Forms.RadioButton modeMultiply;
        private System.Windows.Forms.RadioButton modeCustom;
        private System.Windows.Forms.ComboBox oopsAllBox;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.RadioButton classInherit;
        private System.Windows.Forms.RadioButton classMerge;
        private System.Windows.Forms.RadioButton classNorandom;
        private System.Windows.Forms.RadioButton classCustom;
        private System.Windows.Forms.FlowLayoutPanel globalPanel;
        private System.Windows.Forms.ComboBox presetSelect;
        private System.Windows.Forms.Button applyButton;
        private System.Windows.Forms.Button saveAsButton;
        private System.Windows.Forms.Button directoryButton;
        private System.Windows.Forms.RadioButton classRandom;
        private System.Windows.Forms.Label poolTitle;
        private System.Windows.Forms.Button newPoolButton;
        private System.Windows.Forms.Label newPoolBreak;
        private System.Windows.Forms.Label poolDesc;
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
        private System.Windows.Forms.RadioButton classMerge2;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.ComboBox classMultiplyBox;
    }
}