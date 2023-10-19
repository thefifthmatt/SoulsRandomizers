namespace RandomizerCommon
{
    partial class ArchipelagoForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ArchipelagoForm));
            url = new System.Windows.Forms.TextBox();
            submit = new System.Windows.Forms.Button();
            status = new System.Windows.Forms.Label();
            name = new System.Windows.Forms.TextBox();
            password = new System.Windows.Forms.TextBox();
            SuspendLayout();
            // 
            // url
            // 
            url.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            url.Location = new System.Drawing.Point(20, 19);
            url.Margin = new System.Windows.Forms.Padding(5);
            url.Name = "url";
            url.PlaceholderText = "Room URL (archipelago.gg:12345)";
            url.Size = new System.Drawing.Size(637, 44);
            url.TabIndex = 1;
            // 
            // submit
            // 
            submit.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            submit.Location = new System.Drawing.Point(495, 272);
            submit.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            submit.Name = "submit";
            submit.Size = new System.Drawing.Size(162, 48);
            submit.TabIndex = 4;
            submit.Text = "Load";
            submit.UseVisualStyleBackColor = true;
            submit.Click += submit_Click;
            // 
            // status
            // 
            status.AutoSize = true;
            status.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            status.ForeColor = System.Drawing.SystemColors.GrayText;
            status.Location = new System.Drawing.Point(21, 219);
            status.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            status.Name = "status";
            status.Size = new System.Drawing.Size(140, 26);
            status.TabIndex = 18;
            status.Text = "Connecting...";
            status.Visible = false;
            // 
            // name
            // 
            name.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            name.Location = new System.Drawing.Point(21, 85);
            name.Margin = new System.Windows.Forms.Padding(5);
            name.Name = "name";
            name.PlaceholderText = "Player name";
            name.Size = new System.Drawing.Size(637, 44);
            name.TabIndex = 2;
            // 
            // password
            // 
            password.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            password.Location = new System.Drawing.Point(21, 152);
            password.Margin = new System.Windows.Forms.Padding(5);
            password.Name = "password";
            password.PlaceholderText = "Password";
            password.Size = new System.Drawing.Size(637, 44);
            password.TabIndex = 3;
            // 
            // ArchipelagoForm
            // 
            AcceptButton = submit;
            AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(672, 337);
            Controls.Add(password);
            Controls.Add(name);
            Controls.Add(status);
            Controls.Add(submit);
            Controls.Add(url);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(5);
            Name = "ArchipelagoForm";
            Text = "Connect to Archipelago";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox url;
        private System.Windows.Forms.Button submit;
        private System.Windows.Forms.Label status;
        private System.Windows.Forms.TextBox name;
        private System.Windows.Forms.TextBox password;
    }
}