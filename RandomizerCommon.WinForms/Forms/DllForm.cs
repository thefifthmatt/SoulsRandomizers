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
    public partial class DllForm : Form
    {
        private readonly Messages messages;
        private readonly List<string> extraDlls;

        private bool Modified { get; set; }

        public List<string> ReturnDlls { get; set; }

        public DllForm(Messages messages, List<string> extraDlls)
        {
            InitializeComponent();
            this.messages = messages;
            FormMessages.SetFormText(messages, this, FormMessages.GetFormText(this));
            this.extraDlls = extraDlls;
        }

        public static List<string> Show(Form parent, Messages messages, List<string> extraDlls, string gameHack = null)
        {
            using (DllForm form = new DllForm(messages, extraDlls))
            {
                form.Icon = parent.Icon;
                form.StartPosition = FormStartPosition.CenterParent;
                // It's just getting replaced with Avalonia, it's fine :')
                if (gameHack != null)
                {
                    form.infoL.Text = form.infoL.Text.Replace("Elden Ring", gameHack);
                }
                DialogResult result = form.ShowDialog(parent);
                return form.ReturnDlls;
            }
        }

        private void DllForm_Load(object sender, EventArgs e)
        {
            foreach (string dll in extraDlls)
            {
                listbox.Items.Add(dll);
            }
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select dll";
                dialog.Filter = "dll file (*.dll)|*.dll|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string file = dialog.FileName;
                    if (listbox.Items.IndexOf(file) == -1)
                    {
                        listbox.Items.Insert(0, file);
                        Modified = true;
                    }
                    listbox.SelectedIndex = listbox.Items.IndexOf(file);
                }
            }
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            int removeIndex = listbox.SelectedIndex;
            if (removeIndex == -1) return;
            listbox.Items.RemoveAt(removeIndex);
            if (listbox.Items.Count > 0)
            {
                listbox.SelectedIndex = Math.Min(removeIndex, listbox.Items.Count - 1);
            }
            Modified = true;
        }

        // https://stackoverflow.com/questions/805165/reorder-a-winforms-listbox-using-drag-and-drop
        private void listbox_MouseDown(object sender, MouseEventArgs e)
        {
            if (listbox.SelectedItem == null) return;
            listbox.DoDragDrop(listbox.SelectedItem, DragDropEffects.Move);
        }

        private void listbox_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void listbox_DragLeave(object sender, EventArgs e)
        {
        }

        private void listbox_DragDrop(object sender, DragEventArgs e)
        {
            Point point = listbox.PointToClient(new Point(e.X, e.Y));
            int index = listbox.IndexFromPoint(point);
            if (index < 0) index = listbox.Items.Count - 1;
            object data = e.Data.GetData(typeof(string));
            int startIndex = listbox.Items.IndexOf(data);
            if (index == startIndex) return;
            listbox.Items.Remove(data);
            listbox.Items.Insert(index, data);
            Modified = true;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            ReturnDlls = listbox.Items.Cast<string>().Distinct().ToList();
            Modified = false;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DllForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Modified)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(FormText.ConfirmLeaveText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    ReturnDlls = listbox.Items.Cast<string>().Distinct().ToList();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
