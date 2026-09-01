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
using static RandomizerCommon.Messages;
using static RandomizerCommon.HelperOptions;

namespace RandomizerCommon
{
    public partial class HelperForm : Form
    {
#if DEBUG
        internal static bool DebugOpen = false;
#endif

        private readonly Messages messages;
        private readonly string noActivity;
        private readonly FileSystemWatcher watcher;
        private readonly HelperOptions options;

        private bool simultaneousUpdate = false;

        // For now, these can't be changed. I guess they live here now
        public static readonly string DllPath = @"dll\RandomizerHelper.dll";
        public static readonly string ConfigPath = @"dll\RandomizerHelper_config.ini";
        public static readonly string LogPath = @"dll\RandomizerHelper_log.txt";
        // Also the other dll
        public static readonly string FixDllPath = @"dll\RandomizerCrashFix.dll";

        public HelperForm(Messages messages)
        {
            InitializeComponent();
            this.messages = messages;
            FormMessages.SetFormText(messages, this, FormMessages.GetFormText(this));
            options = new HelperOptions(ConfigPath);
            noActivity = activity.Text;
            watcher = MakeFileSystemWatcher(LogPath);
            components ??= new Container();
            components.Add(watcher);
        }

        private void HelperForm_Load(object sender, EventArgs e)
        {
            Dictionary<string, int> indents = new();
            foreach (Option opt in OptionList)
            {
                int indent = opt.Parent != null && indents.TryGetValue(opt.Parent, out int ival) ? ival + 20 : 0;
                indents[opt.Name] = indent;
                if (opt.Type == OptionType.Bool)
                {
                    CheckBox input = new CheckBox
                    {
                        AutoSize = true,
                        Text = messages.Get(OptionNames[opt.Name]),
                        Checked = options.GetBool(opt),
                        Name = opt.Name,
                    };
                    input.Margin += new Padding(indent, 0, 0, 0);
                    input.CheckedChanged += (sender, e) =>
                    {
                        if (simultaneousUpdate) return;
                        options.Set(opt, input.Checked);
                        // Only checkboxes can change enable state
                        UpdateEnabled();
                    };
                    panel.Controls.Add(input);
                    panel.SetFlowBreak(input, true);
                }
                else if (opt.Type == OptionType.Int)
                {
                    int lastVal = options.GetInt(opt);
                    TextBox input = new TextBox
                    {
                        Width = 100,
                        Text = lastVal.ToString(),
                        Name = opt.Name,
                    };
                    input.Margin += new Padding(indent, 0, 0, 0);
                    bool autoset = false;
                    input.LostFocus += (sender, e) =>
                    {
                        if (autoset || simultaneousUpdate) return;
                        if (string.IsNullOrWhiteSpace(input.Text))
                        {
                            lastVal = 0;
                        }
                        else if (int.TryParse(input.Text.Trim(), out int val))
                        {
                            if (opt.Min >= opt.Max || val >= opt.Min && val <= opt.Max)
                            {
                                lastVal = val;
                            }
                            else
                            {
                                val = Math.Clamp(val, opt.Min, opt.Max);
                                lastVal = val;
                            }
                        }
                        if (input.Text != lastVal.ToString())
                        {
                            autoset = true;
                            input.Text = lastVal.ToString();
                            autoset = false;
                        }
                        options.Set(opt, lastVal);
                    };
                    panel.Controls.Add(input);
                    Label label = new Label
                    {
                        AutoSize = true,
                        Text = messages.Get(OptionNames[opt.Name]),
                    };
                    // Assume it's on a separate line and won't fit
                    panel.SetFlowBreak(input, true);
                    label.Margin += new Padding(indent, 0, 0, 0);
                    panel.Controls.Add(label);
                    panel.SetFlowBreak(label, true);
                }
            }
            UpdateActivity();
            UpdateEnabled();
        }

        private FileSystemWatcher MakeFileSystemWatcher(string filename)
        {
            // Required to exist, should exist if this form was created
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(filename), "*.txt");
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Changed += OnChanged;
            watcher.EnableRaisingEvents = true;
            watcher.SynchronizingObject = this;
            return watcher;
        }

        private void UpdateEnabled()
        {
            HashSet<string> disabled = new();
            foreach (Option opt in OptionList)
            {
                Control control = panel.Controls[opt.Name];
                if (control == null) continue;
                bool checkDisabled = control is CheckBox check && !check.Checked;
                bool parentDisabled = opt.Parent != null && disabled.Contains(opt.Parent);
                control.Enabled = !parentDisabled;
                if (checkDisabled || parentDisabled)
                {
                    disabled.Add(opt.Name);
                }
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            UpdateActivity();
        }

        private void UpdateActivity()
        {
            FileInfo info = new FileInfo(LogPath);
            if (!info.Exists)
            {
                activity.Text = noActivity;
            }
            else
            {
                string dateInfo = info.LastWriteTime.ToString("F");
                activity.Text = messages.Get(active, dateInfo);
            }
        }

        [Localize]
        private static readonly Text active = new Text("Last active at {0}", "HelperForm_active");

        // path should exist as a prerequisite
        public static DialogResult Show(Form parent, Messages messages)
        {
            using (HelperForm form = new HelperForm(messages))
            {
                form.Icon = parent.Icon;
                form.StartPosition = FormStartPosition.CenterParent;
                return form.ShowDialog(parent);
            }
        }

        private void HelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
#if DEBUG
            if (DebugOpen)
            {
                Application.Exit();
                return;
            }
#endif
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                messages.Get(FormText.ConfirmResetText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.OKCancel);
            if (result != DialogResult.OK)
            {
                return;
            }
            // This was originally copying file, but we have to reset form anyway, so just set on form level
            // Except that doesn't cause LostFocus so okay
            simultaneousUpdate = true;
            foreach (Option opt in OptionList)
            {
                Control control = panel.Controls[opt.Name];
                if (control is CheckBox check)
                {
                    check.Checked = options.GetDefaultBool(opt);
                    options.Set(opt, check.Checked);
                }
                else if (control is TextBox text)
                {
                    // No text settings at this point
                    int val = options.GetDefaultInt(opt);
                    text.Text = val.ToString();
                    options.Set(opt, val);
                }
            }
            simultaneousUpdate = false;
        }
    }
}
