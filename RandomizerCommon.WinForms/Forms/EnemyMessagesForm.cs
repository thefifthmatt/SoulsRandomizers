using SoulsIds;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RandomizerCommon
{
    public partial class EnemyMessagesForm : Form
    {
        private readonly EnemyAnnotations ann;

        public EnemyMessagesForm(Messages messages, EnemyAnnotations ann, string initialPath = null)
        {
            InitializeComponent();
            FormMessages.SetFormText(messages, this, FormMessages.GetFormText(this));
            this.ann = ann;
            filename.Text = initialPath ?? "";
        }

        public static DialogResult Show(Form parent, Messages messages, EnemyAnnotations ann, string initialPath = null)
        {
            using (EnemyMessagesForm form = new EnemyMessagesForm(messages, ann, initialPath))
            {
                form.Icon = parent.Icon;
                form.StartPosition = FormStartPosition.CenterParent;
                return form.ShowDialog(parent);
            }
        }

        private void selectFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select .enemy.json file";
                dialog.Filter = ".enemy.json file (*.enemy.json)|*.enemy.json|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                if (Directory.Exists(@"diste\Messages"))
                {
                    dialog.InitialDirectory = @"diste\Messages";
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    filename.Text = dialog.FileName;
                }
            }
        }

        private void check_Click(object sender, EventArgs e)
        {
            // https://stackoverflow.com/questions/3678620/c-sharp-richtextbox-selection-problem
            result.AutoWordSelection = true;
            result.AutoWordSelection = false;
            result.Text = EnemyMessages.Validate(filename.Text, ann, input.Text, false);
        }

        private void input_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                check_Click(sender, e);
            }
        }
    }
}
