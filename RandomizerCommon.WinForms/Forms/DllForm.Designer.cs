namespace RandomizerCommon
{
    partial class DllForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DllForm));
            listbox = new System.Windows.Forms.ListBox();
            addButton = new System.Windows.Forms.Button();
            removeButton = new System.Windows.Forms.Button();
            okButton = new System.Windows.Forms.Button();
            infoL = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // listbox
            // 
            listbox.AllowDrop = true;
            listbox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            listbox.ItemHeight = 16;
            listbox.Location = new System.Drawing.Point(13, 63);
            listbox.Name = "listbox";
            listbox.Size = new System.Drawing.Size(760, 292);
            listbox.TabIndex = 0;
            listbox.DragDrop += listbox_DragDrop;
            listbox.DragOver += listbox_DragOver;
            listbox.DragLeave += listbox_DragLeave;
            listbox.MouseDown += listbox_MouseDown;
            // 
            // addButton
            // 
            addButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            addButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            addButton.Location = new System.Drawing.Point(13, 366);
            addButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            addButton.Name = "addButton";
            addButton.Size = new System.Drawing.Size(175, 36);
            addButton.TabIndex = 15;
            addButton.Text = "Add...";
            addButton.UseVisualStyleBackColor = true;
            addButton.Click += addButton_Click;
            // 
            // removeButton
            // 
            removeButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            removeButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            removeButton.Location = new System.Drawing.Point(195, 366);
            removeButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            removeButton.Name = "removeButton";
            removeButton.Size = new System.Drawing.Size(175, 36);
            removeButton.TabIndex = 16;
            removeButton.Text = "Remove";
            removeButton.UseVisualStyleBackColor = true;
            removeButton.Click += removeButton_Click;
            // 
            // okButton
            // 
            okButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            okButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            okButton.Location = new System.Drawing.Point(596, 366);
            okButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(175, 36);
            okButton.TabIndex = 17;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // infoL
            // 
            infoL.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            infoL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            infoL.Location = new System.Drawing.Point(13, 7);
            infoL.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            infoL.Name = "infoL";
            infoL.Size = new System.Drawing.Size(758, 53);
            infoL.TabIndex = 18;
            infoL.Text = resources.GetString("infoL.Text");
            // 
            // DllForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(784, 411);
            Controls.Add(infoL);
            Controls.Add(okButton);
            Controls.Add(removeButton);
            Controls.Add(addButton);
            Controls.Add(listbox);
            Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            MinimumSize = new System.Drawing.Size(650, 300);
            Name = "DllForm";
            Text = "Dll mods";
            FormClosing += DllForm_FormClosing;
            Load += DllForm_Load;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ListBox listbox;
        private System.Windows.Forms.Button addButton;
        private System.Windows.Forms.Button removeButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Label infoL;
    }
}