using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RandomizerCommon.Messages;

namespace RandomizerCommon
{
    public partial class OptionsForm : Form
    {
        private Messages messages;
        public string OptionString { get; set; }

        public OptionsForm(Messages messages, string opt)
        {
            this.messages = messages;
            InitializeComponent();
            messages.SetFormText(this, messages.GetFormText(this));
            str.Text = opt;
        }

        private void select_Click(object sender, EventArgs e)
        {
            string prefix = "Options and seed: ";
            OptionString = str.Text;
            OptionString = OptionString.Trim();
            if (OptionString.StartsWith(prefix))
            {
                OptionString = OptionString.Substring(prefix.Length);
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void str_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                select.PerformClick();
            }
        }

        [Localize]
        private static readonly Text errorInternal = new Text(
            "Invalid options: internal error",
            "OptionsForm_errorInternal");
        [Localize]
        private static readonly Text errorNoOptions = new Text(
            "Invalid options: must contain options",
            "OptionsForm_errorNoOptions");
        [Localize]
        private static readonly Text errorNoVersion = new Text(
            "Invalid options: must contain a version number",
            "OptionsForm_errorNoVersion");

        public bool HasOptionsError(string text, out string msg)
        {
            msg = null;
            if (string.IsNullOrEmpty(text))
            {
                msg = messages.Get(errorInternal);
            }
            else if (int.TryParse(text, out var _))
            {
                msg = messages.Get(errorNoOptions);
            }
            else if (!text.Contains("v"))
            {
                msg = messages.Get(errorNoVersion);
            }
            return msg != null;
        }
    }
}
