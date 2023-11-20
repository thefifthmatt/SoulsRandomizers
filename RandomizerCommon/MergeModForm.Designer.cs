namespace RandomizerCommon
{
    partial class MergeModForm
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
            this.reg = new System.Windows.Forms.Button();
            this.dir = new System.Windows.Forms.Button();
            this.toml = new System.Windows.Forms.Button();
            this.clear = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // reg
            // 
            this.reg.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.reg.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.reg.Location = new System.Drawing.Point(13, 12);
            this.reg.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.reg.Name = "reg";
            this.reg.Size = new System.Drawing.Size(528, 33);
            this.reg.TabIndex = 1;
            this.reg.Text = "Select regulation.bin to merge";
            this.reg.UseVisualStyleBackColor = true;
            this.reg.Click += new System.EventHandler(this.reg_Click);
            // 
            // dir
            // 
            this.dir.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dir.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.dir.Location = new System.Drawing.Point(13, 51);
            this.dir.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.dir.Name = "dir";
            this.dir.Size = new System.Drawing.Size(528, 33);
            this.dir.TabIndex = 2;
            this.dir.Text = "Select mod directory to merge";
            this.dir.UseVisualStyleBackColor = true;
            this.dir.Click += new System.EventHandler(this.dir_Click);
            // 
            // toml
            // 
            this.toml.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toml.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.toml.Location = new System.Drawing.Point(13, 90);
            this.toml.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.toml.Name = "toml";
            this.toml.Size = new System.Drawing.Size(528, 33);
            this.toml.TabIndex = 3;
            this.toml.Text = "Select toml file (merge multiple mods)";
            this.toml.UseVisualStyleBackColor = true;
            this.toml.Click += new System.EventHandler(this.toml_Click);
            // 
            // clear
            // 
            this.clear.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.clear.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.clear.Location = new System.Drawing.Point(13, 129);
            this.clear.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.clear.Name = "clear";
            this.clear.Size = new System.Drawing.Size(528, 33);
            this.clear.TabIndex = 4;
            this.clear.Text = "Clear merged mod";
            this.clear.UseVisualStyleBackColor = true;
            this.clear.Click += new System.EventHandler(this.clear_Click);
            // 
            // MergeModForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(554, 173);
            this.Controls.Add(this.clear);
            this.Controls.Add(this.toml);
            this.Controls.Add(this.dir);
            this.Controls.Add(this.reg);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MergeModForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select mod to merge";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button reg;
        private System.Windows.Forms.Button dir;
        private System.Windows.Forms.Button toml;
        private System.Windows.Forms.Button clear;
    }
}