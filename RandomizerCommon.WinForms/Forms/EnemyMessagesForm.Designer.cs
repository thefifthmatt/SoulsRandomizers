namespace RandomizerCommon
{
    partial class EnemyMessagesForm
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
            filename = new System.Windows.Forms.TextBox();
            selectFile = new System.Windows.Forms.Button();
            check = new System.Windows.Forms.Button();
            input = new System.Windows.Forms.TextBox();
            result = new System.Windows.Forms.RichTextBox();
            inputL = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // filename
            // 
            filename.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            filename.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            filename.Location = new System.Drawing.Point(12, 12);
            filename.Name = "filename";
            filename.Size = new System.Drawing.Size(667, 21);
            filename.TabIndex = 0;
            // 
            // selectFile
            // 
            selectFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            selectFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            selectFile.Location = new System.Drawing.Point(685, 12);
            selectFile.Name = "selectFile";
            selectFile.Size = new System.Drawing.Size(84, 22);
            selectFile.TabIndex = 1;
            selectFile.Text = "Select file...";
            selectFile.UseVisualStyleBackColor = true;
            selectFile.Click += selectFile_Click;
            // 
            // check
            // 
            check.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            check.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            check.Location = new System.Drawing.Point(685, 41);
            check.Name = "check";
            check.Size = new System.Drawing.Size(84, 22);
            check.TabIndex = 2;
            check.Text = "Check";
            check.UseVisualStyleBackColor = true;
            check.Click += check_Click;
            // 
            // input
            // 
            input.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            input.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            input.Location = new System.Drawing.Point(277, 41);
            input.Name = "input";
            input.Size = new System.Drawing.Size(402, 21);
            input.TabIndex = 4;
            input.KeyPress += input_KeyPress;
            // 
            // result
            // 
            result.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            result.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            result.Location = new System.Drawing.Point(10, 71);
            result.Name = "result";
            result.ReadOnly = true;
            result.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
            result.Size = new System.Drawing.Size(762, 478);
            result.TabIndex = 5;
            result.Text = "";
            // 
            // inputL
            // 
            inputL.AutoSize = true;
            inputL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            inputL.Location = new System.Drawing.Point(11, 43);
            inputL.Name = "inputL";
            inputL.Size = new System.Drawing.Size(245, 15);
            inputL.TabIndex = 6;
            inputL.Text = "Boss name (leave blank to check for errors):";
            // 
            // EnemyMessagesForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(784, 561);
            Controls.Add(inputL);
            Controls.Add(result);
            Controls.Add(input);
            Controls.Add(check);
            Controls.Add(selectFile);
            Controls.Add(filename);
            MinimumSize = new System.Drawing.Size(500, 200);
            Name = "EnemyMessagesForm";
            Text = "Check .enemy.json file";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox filename;
        private System.Windows.Forms.Button selectFile;
        private System.Windows.Forms.Button check;
        private System.Windows.Forms.TextBox input;
        private System.Windows.Forms.RichTextBox result;
        private System.Windows.Forms.Label inputL;
    }
}