using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RandomizerCommon.ComboTextBox;

namespace RandomizerCommon
{
    public partial class PresetEnemyControl : UserControl
    {
        // Artificial holder for these fields, since embedding fails
        public Label Pre { get; }
        public ComboTextBox Selector { get; }
        public List<LinkLabel> Tags = new List<LinkLabel>();
        public ToolTip Tip { get; }
        public bool Editable { get; }
        // Optional
        public Button Remove { get; }
        public TextBox Percent { get; }
        // Externally managed
        public bool Modified { get; set; }

        // This is a bit too confusing to expose, people can just make other pool entries in this case
        // public CheckBox RandomByType { get; set; }
        private int tabIndex;

        // This could be reset when it goes past 100, but allow some weirdness for now
        public int NewTabIndex() => tabIndex++;
        
        public PresetEnemyControl(
            string text, List<AutocompleteItem> opts, int indexForTabs,
            bool hasPercent = false, bool hasRemove = false, bool editable = true)
        {
            // Unfortunately, FlowLayoutPanels embedded in each other just doesn't work.
            // So use this as a sentinel control for parent flow.
            InitializeComponent();

            Tip = new ToolTip();
            Tip.AutoPopDelay = 30000;
            Tip.InitialDelay = 200;
            Tip.ReshowDelay = 40;
            Tip.ShowAlways = true;

            tabIndex = (indexForTabs + 1) * 100;

            if (hasRemove)
            {
                Remove = new Button();
                Remove.Text = "-";
                Remove.Size = new Size(50, 23);
                Remove.TabIndex = NewTabIndex();
            }
            if (hasPercent)
            {
                Percent = new TextBox();
                Percent.Width = 50;
                Percent.TextAlign = HorizontalAlignment.Right;
                Percent.TabIndex = NewTabIndex();
            }
            Pre = new Label();
            Pre.AutoSize = true;
            Pre.Text = text;
            Pre.Margin = new Padding(0, 5, 0, 0);
            Pre.TabIndex = NewTabIndex();

            Editable = editable;
            if (Editable)
            {
                // Do something like https://stackoverflow.com/questions/11780558/c-sharp-winforms-combobox-dynamic-autocomplete
                // Actually no https://stackoverflow.com/questions/2259067/override-winforms-combobox-autocomplete-suggest-rule
                // Or wait https://stackoverflow.com/questions/796195/c-sharp-autocomplete
                Selector = new ComboTextBox();
                Selector.Width = 275;
                Selector.DropdownWidth = 275;
                Selector.Options = opts;
                Selector.TabIndex = NewTabIndex();
            }

            EnabledChanged += PresetEnemyControl_EnabledChanged;
            Disposed += PresetEnemyControl_Disposed;
        }

        private void PresetEnemyControl_Disposed(object sender, EventArgs e)
        {
            Tip.Dispose();
        }

        // Hacks since these aren't children, because of flow panel
        private void PresetEnemyControl_EnabledChanged(object sender, EventArgs e)
        {
            foreach (LinkLabel l in Tags)
            {
                l.Enabled = Enabled;
            }
            if (Selector != null) Selector.Enabled = Enabled;
            if (Remove != null) Remove.Enabled = Enabled;
            if (Percent != null) Percent.Enabled = Enabled;
            Tip.Active = Enabled;
        }

        public void SetVisible(bool Visible)
        {
            // Using Show/Hide causes delays which are not acceptable for initialization, makes them not apply
            // We must do this in order, because it can change the element order otherwise?????????
            this.Visible = Visible;
            Pre.Visible = Visible;
            if (Selector != null) Selector.Visible = Visible;
            foreach (LinkLabel l in Tags)
            {
                // Console.WriteLine($"  Set visibility for {l.Text} to {Visible}");
                l.Visible = Visible;
            }
            if (Remove != null) Remove.Visible = Visible;
            if (Percent != null) Percent.Visible = Visible;
            // Tooltip?
        }
    }
}
