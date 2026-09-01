using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RandomizerCommon
{
    public partial class SearchScrollForm : Form
    {
        private static bool currentAlt = true;
        private readonly Messages messages;

        public SearchScrollForm(Messages messages, string title, string text)
        {
            InitializeComponent();
            this.messages = messages;
            FormMessages.SetFormText(messages, this, FormMessages.GetFormText(this));
            Text = title;
            // this.text = text;
            textbox.Text = text;
        }

        public static DialogResult Show(Form parent, Messages messages, string text, string title)
        {
            using (SearchScrollForm form = new SearchScrollForm(messages, title, text))
            {
                form.Icon = parent.Icon;
                form.StartPosition = FormStartPosition.CenterParent;
                return form.ShowDialog(parent);
            }
        }

        private void act_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Borrowing from Chrome:
            // Ctrl+F, go to textbox
            // Ctrl+G or F3: next
            // With shift: prev
            if (keyData == Keys.Escape)
            {
                if (string.IsNullOrEmpty(search.Text))
                {
                    Close();
                }
                else
                {
                    search.Text = "";
                }
            }
            else if (keyData == (Keys.Control | Keys.F))
            {
                search.Focus();
                search.SelectAll();
            }
            else if (search.Focused && keyData == Keys.Enter)
            {
                Jump(true);
            }
            else if (keyData == (Keys.Control | Keys.G) || keyData == Keys.F3)
            {
                Jump(true);
            }
            else if (keyData == (Keys.Control | Keys.Shift | Keys.G) || keyData == (Keys.Shift | Keys.F3))
            {
                Jump(false);
            }
            else
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }

        private void ClearHighlight()
        {
            textbox.SelectAll();
            textbox.SelectionBackColor = SystemColors.Control;
        }

        private void Jump(bool? next)
        {
            // For some reason, this does automatic caret jumping if textbox is focused
            search.Focus();
            int initStart = textbox.SelectionStart;
            int initLen = textbox.SelectionLength;
            ClearHighlight();
            if (string.IsNullOrEmpty(search.Text))
            {
                count.Text = "";
                textbox.SelectionStart = initStart;
                textbox.SelectionLength = initLen;
                return;
            }
            Regex regex = new Regex(Regex.Escape(search.Text), RegexOptions.IgnoreCase);
            MatchCollection matches = regex.Matches(textbox.Text);
            if (matches.Count == 0)
            {
                count.Text = "0/0";
                textbox.SelectionStart = initStart;
                textbox.SelectionLength = initLen;
                return;
            }
            int findIndex = 0;
            bool containsCaret(Match match) => match.Index >= initStart && match.Index + match.Length <= initStart + initLen;
            if (next is null || next is true)
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i].Index >= initStart)
                    {
                        findIndex = i;
                        break;
                    }
                }
                if (next is true && containsCaret(matches[findIndex]))
                {
                    findIndex = (findIndex + 1) % matches.Count;
                }
            }
            else
            {
                for (int i = matches.Count - 1; i >= 0; i--)
                {
                    if (matches[i].Index <= initStart)
                    {
                        findIndex = i;
                        break;
                    }
                }
                if (containsCaret(matches[findIndex]))
                {
                    findIndex = (findIndex - 1 + matches.Count) % matches.Count;
                }
            }
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                textbox.Select(match.Index, match.Length);
                textbox.SelectionBackColor = findIndex == i ? Color.Goldenrod : Color.Yellow;
            }
            // This jumps to middle, but we can't set it to earlier because of needing to do prev/next
            textbox.SelectionStart = matches[findIndex].Index;
            textbox.SelectionLength = matches[findIndex].Length;
            if (!IsCaretVisible())
            {
                textbox.ScrollToCaret();
            }
            count.Text = $"{findIndex + 1}/{matches.Count}";
        }

        private bool IsCaretVisible()
        {
            return textbox.ClientRectangle.Contains(textbox.GetPositionFromCharIndex(textbox.SelectionStart));
        }

        private void search_TextChanged(object sender, EventArgs e)
        {
            Jump(null);
        }

        private void FogListForm_Shown(object sender, EventArgs e)
        {
            search.Focus();
            // act.Focus();
        }
    }
}
