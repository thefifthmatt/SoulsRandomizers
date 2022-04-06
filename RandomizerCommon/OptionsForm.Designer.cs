namespace RandomizerCommon
{
    partial class OptionsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OptionsForm));
            this.str = new System.Windows.Forms.TextBox();
            this.select = new System.Windows.Forms.Button();
            this.strL = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // str
            // 
            this.str.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.str.Location = new System.Drawing.Point(12, 59);
            this.str.Name = "str";
            this.str.Size = new System.Drawing.Size(893, 22);
            this.str.TabIndex = 1;
            this.str.KeyDown += new System.Windows.Forms.KeyEventHandler(this.str_KeyDown);
            // 
            // select
            // 
            this.select.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.select.Location = new System.Drawing.Point(795, 87);
            this.select.Name = "select";
            this.select.Size = new System.Drawing.Size(110, 29);
            this.select.TabIndex = 2;
            this.select.Text = "Select";
            this.select.UseVisualStyleBackColor = true;
            this.select.Click += new System.EventHandler(this.select_Click);
            // 
            // strL
            // 
            this.strL.AutoSize = true;
            this.strL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.strL.Location = new System.Drawing.Point(11, 6);
            this.strL.Name = "strL";
            this.strL.Size = new System.Drawing.Size(821, 48);
            this.strL.TabIndex = 0;
            this.strL.Text = resources.GetString("strL.Text");
            // 
            // OptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(917, 123);
            this.Controls.Add(this.strL);
            this.Controls.Add(this.select);
            this.Controls.Add(this.str);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "OptionsForm";
            this.Text = "Set options string";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox str;
        private System.Windows.Forms.Button select;
        private System.Windows.Forms.Label strL;
    }
}