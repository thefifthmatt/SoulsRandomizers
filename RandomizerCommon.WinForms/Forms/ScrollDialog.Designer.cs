namespace RandomizerCommon
{
    partial class ScrollDialog
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
            textbox = new System.Windows.Forms.RichTextBox();
            act = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // textbox
            // 
            textbox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            textbox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            textbox.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textbox.Location = new System.Drawing.Point(14, 14);
            textbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            textbox.Name = "textbox";
            textbox.ReadOnly = true;
            textbox.Size = new System.Drawing.Size(1008, 451);
            textbox.TabIndex = 0;
            textbox.Text = "";
            // 
            // act
            // 
            act.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            act.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            act.Location = new System.Drawing.Point(862, 472);
            act.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            act.Name = "act";
            act.Size = new System.Drawing.Size(161, 33);
            act.TabIndex = 11;
            act.Text = "OK";
            act.UseVisualStyleBackColor = true;
            act.Click += act_Click;
            // 
            // ScrollDialog
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1037, 519);
            Controls.Add(act);
            Controls.Add(textbox);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "ScrollDialog";
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.RichTextBox textbox;
        private System.Windows.Forms.Button act;
    }
}