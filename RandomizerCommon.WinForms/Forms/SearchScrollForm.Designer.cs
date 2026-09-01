namespace RandomizerCommon
{
    partial class SearchScrollForm
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
            search = new System.Windows.Forms.TextBox();
            searchtext = new System.Windows.Forms.Label();
            count = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // textbox
            // 
            textbox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            textbox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textbox.Location = new System.Drawing.Point(14, 41);
            textbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            textbox.Name = "textbox";
            textbox.ReadOnly = true;
            textbox.Size = new System.Drawing.Size(1008, 424);
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
            // search
            // 
            search.Location = new System.Drawing.Point(96, 12);
            search.Name = "search";
            search.Size = new System.Drawing.Size(254, 23);
            search.TabIndex = 12;
            search.TextChanged += search_TextChanged;
            // 
            // searchtext
            // 
            searchtext.AutoSize = true;
            searchtext.BackColor = System.Drawing.Color.Transparent;
            searchtext.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            searchtext.Location = new System.Drawing.Point(18, 15);
            searchtext.Name = "searchtext";
            searchtext.Size = new System.Drawing.Size(71, 15);
            searchtext.TabIndex = 14;
            searchtext.Text = "Search text:";
            searchtext.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // count
            // 
            count.AutoSize = true;
            count.Location = new System.Drawing.Point(357, 16);
            count.Name = "count";
            count.Size = new System.Drawing.Size(0, 15);
            count.TabIndex = 15;
            // 
            // SearchScrollForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1037, 519);
            Controls.Add(count);
            Controls.Add(searchtext);
            Controls.Add(search);
            Controls.Add(act);
            Controls.Add(textbox);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "SearchScrollForm";
            Shown += FogListForm_Shown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.RichTextBox textbox;
        private System.Windows.Forms.Button act;
        private System.Windows.Forms.TextBox search;
        private System.Windows.Forms.Label searchtext;
        private System.Windows.Forms.Label count;
    }
}
