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
    public partial class PresetEnemyControl : UserControl
    {
        // Artificial holder for these fields, since embedding fails
        public Label Pre { get; set; }
        public ComboBox Dropdown { get; set; }
        public List<LinkLabel> Tags = new List<LinkLabel>();
        // Optional
        public Button Remove { get; set; }
        public TextBox Percent { get; set; }
        // This is a bit too confusing to expose, people can just make other pool entries in this case
        // public CheckBox RandomByType { get; set; }
        
        public PresetEnemyControl(string text, List<string> opts, bool hasPercent, bool isPool)
        {
            // Unfortunately, FlowLayoutPanels embedded in each other just doesn't work.
            // So use this as a sentinel control for parent flow.
            InitializeComponent();

            if (isPool)
            {
                Remove = new Button();
                Remove.Text = "-";
                Remove.Size = new Size(50, 23);
            }
            if (hasPercent)
            {
                Percent = new TextBox();
                Percent.Width = 50;
                Percent.TextAlign = HorizontalAlignment.Right;
            }
            Pre = new Label();
            Pre.AutoSize = true;
            Pre.Text = text;
            Pre.Margin = new Padding(0, 5, 0, 0);
            // Do something like https://stackoverflow.com/questions/11780558/c-sharp-winforms-combobox-dynamic-autocomplete
            Dropdown = new ComboBox();
            Dropdown.Width = 200;
            Dropdown.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            Dropdown.AutoCompleteSource = AutoCompleteSource.ListItems;
            Dropdown.DropDownStyle = ComboBoxStyle.DropDown;
            // Unfortunately, winforms is abysmally slow, so delay data items
            Dropdown.GotFocus += (sender, e) =>
            {
                if (Dropdown.DataSource == null)
                {
                    Dropdown.DataSource = opts;
                }
            };

            EnabledChanged += PresetEnemyControl_EnabledChanged;
        }

        private void PresetEnemyControl_EnabledChanged(object sender, EventArgs e)
        {
            Dropdown.Enabled = Enabled;
            foreach (LinkLabel l in Tags)
            {
                l.Enabled = Enabled;
            }
            if (Remove != null) Remove.Enabled = Enabled;
            if (Percent != null) Percent.Enabled = Enabled;
        }
    }
}
