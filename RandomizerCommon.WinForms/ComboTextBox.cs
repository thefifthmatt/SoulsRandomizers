using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Heavily fixed and modified https://stackoverflow.com/questions/796195/c-sharp-autocomplete
namespace RandomizerCommon
{
    public class ComboTextBox : TextBox, IMessageFilter
    {
        private ListBox listBox;
        private bool expectedChange;
        private bool hasMessageFilter = false;
        private bool clickOnListView = false;

        private static readonly Regex PunctRe = new Regex(@"\p{P}");
        private static string Clean(string text) => PunctRe.Replace(text, "");

        // Public interface
        public event EventHandler SelectedValueChanged;

        public List<AutocompleteItem> Options { get; set; }
        public string SelectedValue { get; private set; } = "";

        public int? DropdownWidth { get; set; }
        // TODO: Set accordingly, add better customization like a custom value function
        public bool AcceptEntityIds { get; set; } = true;

        public class AutocompleteItem : IComparable<AutocompleteItem>
        {
            private readonly List<string> prefixes;

            public readonly string Display;
            public readonly string Value;

            // Use similar system as DarkScript3: infix starting at word boundary
            public AutocompleteItem(string value, string display = null, IEnumerable<string> extra = null)
            {
                Value = value;
                Display = display ?? value;
                // It's okay to do more advanced processing here, since it's only done once per PresetEditForm
                string text = Clean(value);
                prefixes = new List<string> { text };
                bool prevSpace = false;
                for (int i = 1; i < text.Length; i++)
                {
                    bool space = char.IsWhiteSpace(text[i]);
                    if (prevSpace && !space)
                    {
                        prefixes.Add(text.Substring(i));
                    }
                    prevSpace = space;
                }
                if (extra != null)
                {
                    prefixes.AddRange(extra);
                }
            }

            public bool Match(string text)
            {
                // Don't try to cache or optimize this for now
                return prefixes.Any(p => p.StartsWith(text, StringComparison.InvariantCultureIgnoreCase));
            }

            public int CompareTo(AutocompleteItem other) => Value.CompareTo(other.Value);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (hasMessageFilter)
            {
                Application.RemoveMessageFilter(this);
            }

            base.OnHandleDestroyed(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            // This doesn't work with click (get focus), click elsewhere (keep focus), reclick (no focus gained)
            base.OnGotFocus(e);

            // This handles a behavior difference between click and enter. Click loses focus (then immediately regains it),
            // enter doesn't. Keep hidden in both cases.
            if (expectedChange) return;

            ShowList();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            // Clicking away handles the normal case in the click handler, but tab navigation should also do this.
            // If there was not a click and we lost focus, always hide.
            // if (!clickOnListView)
            if (!listBox.Focused)
            {
                listBox?.Hide();
            }

            base.OnLostFocus(e);
        }

        void InitListControl()
        {
            if (listBox == null)
            {
                if (TopLevelControl != null)
                {
                    // Setup a messaage filter so we can listen to the keyboard
                    if (!hasMessageFilter)
                    {
                        Application.AddMessageFilter(this);
                        hasMessageFilter = true;
                    }

                    listBox = new ListBox();
                    listBox.Visible = false;
                    listBox.Click += listBox_Click;
                    listBox.TabStop = false;
                    TopLevelControl.Controls.Add(listBox);
                    TopLevelControl.Controls.SetChildIndex(listBox, 0);  // Put it at the front
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (expectedChange) return;

            ShowList();
        }

        private void ShowList()
        {
            InitListControl();

            if (listBox == null) return;

            string searchText = Clean(Text.Trim());

            listBox.Items.Clear();

            // Because this is not just autocomplete but also dropdown, show it even when string.IsNullOrEmpty(searchText)
            AutocompleteItem lastTopLevel = null;
            bool showSubItems = false;
            bool trivialMatch = string.IsNullOrWhiteSpace(searchText);
            foreach (AutocompleteItem item in Options)
            {
                bool matches = trivialMatch || item.Match(searchText);
                // For now hardcode bullet appearance
                bool topLevel = !item.Display.StartsWith('-');
                if (topLevel)
                {
                    if (matches)
                    {
                        showSubItems = true;
                        lastTopLevel = null;
                    }
                    else
                    {
                        showSubItems = false;
                        lastTopLevel = item;
                    }
                }
                if (matches || showSubItems)
                {
                    if (!topLevel && lastTopLevel != null)
                    {
                        listBox.Items.Add(lastTopLevel.Display);
                        lastTopLevel = null;
                    }
                    listBox.Items.Add(item.Display);
                }
            }

            if (listBox.Items.Count > 0)
            {
                Point point = new Point(Left, Bottom);
                point = Parent.PointToScreen(point);
                point = TopLevelControl.PointToClient(point);

                listBox.Left = point.X;
                listBox.Top = point.Y;
                listBox.Width = DropdownWidth ?? Width;

                int totalHeight = listBox.ItemHeight * (listBox.Items.Count + 1);
                listBox.Height = Math.Min(TopLevelControl.ClientSize.Height - listBox.Top, totalHeight);

                listBox.Show();
            }
            else
            {
                listBox.Hide();
            }
        }

        public static bool IsEntityId(string text, out uint id)
        {
            ReadOnlySpan<char> sp = text.StartsWith('#') ? text.AsSpan(1) : text;
            // Max id, Furnace Golem 2251450280 in m61_12_11_02 -> m61_52_45_00
            return uint.TryParse(sp, out id) && id >= 100_000 && id < 2_300_000_000;
        }

        private void CopySelection()
        {
            string result = null;
            // Special behavior for allowing entity ids. Unclear how to do this in a less hacky way
            if (AcceptEntityIds && IsEntityId(Text.Trim(), out uint val))
            {
                result = val.ToString();
            }
            else if (listBox?.SelectedItem != null)
            {
                string disp = listBox.SelectedItem.ToString();
                // There's no advanced mapping maintained at the moment, just do it by string
                result = Options.Where(o => o.Display == disp).Select(o => o.Value).FirstOrDefault();
            }
            if (result != null)
            {
                expectedChange = true;
                // Text = result;
                SelectedValue = result;
                if (!ModifierKeys.HasFlag(Keys.Control))
                {
                    // Ctrl+Click or Ctrl+Enter to keep dialogue open
                    listBox?.Hide();
                    Text = "";
                    // SelectAll();
                    Focus();
                }
                expectedChange = false;

                SelectedValueChanged?.Invoke(this, new EventArgs());
            }
        }

        private void listBox_Click(object sender, EventArgs e)
        {
            if (sender == listBox)
            {
                // Copy selection to the combo box
                CopySelection();
            }
        }

        /*
        protected override void OnClick(EventArgs e)
        {
            // Do this even when focused, since there's no dropdown control
            if (listBox == null || !listBox.Visible)
            {
                ShowList();
            }
        }
        */

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Focused && e.KeyCode == Keys.Return && AcceptEntityIds && IsEntityId(Text, out _))
            {
                // Case to allow this even when listbox is not open
                CopySelection();
                e.SuppressKeyPress = true;
            }
            else if (listBox != null && listBox.Visible)
            {
                int moveIndex = 0;
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        moveIndex = -1;
                        break;
                    case Keys.Down:
                        moveIndex = 1;
                        break;
                    case Keys.PageUp:
                        moveIndex = -5;
                        break;
                    case Keys.PageDown:
                        moveIndex = 5;
                        break;

                    case Keys.Escape:
                        listBox.Hide();
                        break;
                    case Keys.Return:
                        CopySelection();
                        break;

                    default:
                        return;
                }
                e.SuppressKeyPress = true;
                if (moveIndex != 0 && listBox.Items.Count > 0)
                {
                    int newIndex = Math.Clamp(listBox.SelectedIndex + moveIndex, 0, listBox.Items.Count - 1);
                    listBox.SelectedIndex = newIndex;
                }
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            // Unfortunately we have to use messages for this, but can potentially access the handle of something
            // which might not exist anymore? This seems to result in segfault or a something. Winforms sucks.
            if (IsDisposed || listBox.IsDisposed) return false;

            // WM_LBUTTONDOWN
            if (m.Msg == 0x201)
            {
                clickOnListView = m.HWnd == listBox.Handle;
                bool anyClick = clickOnListView || m.HWnd == Handle;
                if (listBox != null && listBox.Visible)
                {
                    if (!anyClick)
                    {
                        listBox.Hide();
                    }
                }
                else
                {
                    if (anyClick)
                    {
                        ShowList();
                    }
                }
            }
            return false;
        }
    }
}
