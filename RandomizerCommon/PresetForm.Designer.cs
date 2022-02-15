namespace RandomizerCommon
{
    partial class PresetForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PresetForm));
            this.select = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.submit = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.enemy = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.desc = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // select
            // 
            this.select.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.select.Enabled = false;
            this.select.FormattingEnabled = true;
            this.select.Location = new System.Drawing.Point(57, 56);
            this.select.Name = "select";
            this.select.Size = new System.Drawing.Size(301, 21);
            this.select.TabIndex = 2;
            this.select.SelectedIndexChanged += new System.EventHandler(this.preset_Changed);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 59);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(40, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Preset:";
            // 
            // submit
            // 
            this.submit.Location = new System.Drawing.Point(283, 199);
            this.submit.Name = "submit";
            this.submit.Size = new System.Drawing.Size(75, 23);
            this.submit.TabIndex = 6;
            this.submit.Text = "Select";
            this.submit.UseVisualStyleBackColor = true;
            this.submit.Click += new System.EventHandler(this.submit_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(7, 86);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(42, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Enemy:";
            // 
            // enemy
            // 
            this.enemy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.enemy.Enabled = false;
            this.enemy.FormattingEnabled = true;
            this.enemy.Location = new System.Drawing.Point(57, 83);
            this.enemy.Name = "enemy";
            this.enemy.Size = new System.Drawing.Size(301, 21);
            this.enemy.TabIndex = 4;
            this.enemy.SelectedIndexChanged += new System.EventHandler(this.enemy_Changed);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(276, 39);
            this.label3.TabIndex = 0;
            this.label3.Text = "Presets are loaded from the \'presets\' directory.\r\nYou can edit them and add your " +
    "own!\r\nThey\'re mainly for challenge runs, races, and showcases.";
            // 
            // desc
            // 
            this.desc.Location = new System.Drawing.Point(13, 113);
            this.desc.Name = "desc";
            this.desc.Size = new System.Drawing.Size(345, 78);
            this.desc.TabIndex = 5;
            this.desc.Text = "By default, all enemies are randomized within their own pools.";
            this.desc.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PresetForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(367, 233);
            this.Controls.Add(this.desc);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.enemy);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.submit);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.select);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "PresetForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Preset Selection";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox select;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button submit;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox enemy;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label desc;
    }
}