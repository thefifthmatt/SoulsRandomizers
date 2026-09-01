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
            this.str.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.str.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.str.Location = new System.Drawing.Point(14, 68);
            this.str.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.str.Multiline = true;
            this.str.Name = "str";
            this.str.Size = new System.Drawing.Size(1035, 60);
            this.str.TabIndex = 1;
            this.str.KeyDown += new System.Windows.Forms.KeyEventHandler(this.str_KeyDown);
            // 
            // select
            // 
            this.select.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.select.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.select.Location = new System.Drawing.Point(921, 137);
            this.select.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.select.Name = "select";
            this.select.Size = new System.Drawing.Size(128, 33);
            this.select.TabIndex = 2;
            this.select.Text = "Select";
            this.select.UseVisualStyleBackColor = true;
            this.select.Click += new System.EventHandler(this.select_Click);
            // 
            // strL
            // 
            this.strL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.strL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.strL.Location = new System.Drawing.Point(13, 7);
            this.strL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.strL.Name = "strL";
            this.strL.Size = new System.Drawing.Size(1036, 55);
            this.strL.TabIndex = 0;
            this.strL.Text = resources.GetString("strL.Text");
            // 
            // OptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1064, 179);
            this.Controls.Add(this.strL);
            this.Controls.Add(this.select);
            this.Controls.Add(this.str);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
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