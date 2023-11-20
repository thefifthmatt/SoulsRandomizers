using System;
using System.Windows.Forms;
using Ookii.Dialogs.WinForms;
using SoulsIds;

namespace RandomizerCommon
{
    public partial class MergeModForm : Form
    {
        private readonly string initial;

        public string FileName { get; private set; }

        public MergeModForm(string initial)
        {
            InitializeComponent();
            this.initial = initial;
        }

        private void reg_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select regulation.bin";
                dialog.Filter = "regulation.bin|regulation.bin";
                dialog.RestoreDirectory = true;
                if (ModRunner.TryGetDirectory(initial, out string dir))
                {
                    dialog.InitialDirectory = dir;
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // TODO: Check for presence of toml file and try to parse it, if config_eldenringrandomizer.toml is in the directory.
                    FileName = dialog.FileName;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        private void dir_Click(object sender, EventArgs e)
        {
            using (VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    FileName = dialog.SelectedPath;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        private void toml_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select toml file";
                dialog.Filter = "Mod Engine toml file|*.toml";
                dialog.RestoreDirectory = true;
                if (ModRunner.TryGetDirectory(initial, out string dir))
                {
                    dialog.InitialDirectory = dir;
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // Could parse file here, but delay til mod run time
                    FileName = dialog.FileName;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        private void clear_Click(object sender, EventArgs e)
        {
            FileName = null;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
