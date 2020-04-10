using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RandomizerCommon
{
    public partial class OptionsForm : Form
    {
        public string OptionString { get; set; }

        public OptionsForm(string opt)
        {
            InitializeComponent();
            str.Text = opt;
        }

        private void select_Click(object sender, EventArgs e)
        {
            OptionString = str.Text;
            OptionString = OptionString.Split(':').Last();
            OptionString = OptionString.Trim();
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
    }
}
