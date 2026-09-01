using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RandomizerCommon.ComboTextBox;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Messages;
using static RandomizerCommon.EnemyPreset;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public partial class PresetEditForm : Form
    {
#if DEBUG
        internal static bool DebugOpen = false;
#endif

        public EnemyPreset Preset;
        public EnemyPreset SavedPreset;
        private readonly EnemyAnnotations ann;
        private readonly Messages messages;
        private readonly bool dlcSeparate;
        private readonly CheckBox overallSilo;
        private PresetEnemyControl dontRandomize;
        private PresetEnemyControl removeSource;
        // Preventing event handlers from triggering each other
        private bool simultaneousUpdate = false;
        // Whether custom pools may be edited
        private bool editCustomPool = false;
        private bool editAssignPool = false;
        // Special tracking of multiply mode, to ask about randomization
        private bool multiplyMode = false;

        // Attempted dirty bit, for intercepting window close
        private bool modified_;
        private bool Modified
        {
            get => modified_;
            set
            {
                modified_ = value;
                applyButton.Enabled = value;
            }
        }

        [Localize]
        private static readonly Text multiplyOptionText = new Text("{0}x", "PresetEditForm_multiplyOption");
        [Localize]
        private static readonly Text createNewText = new Text("Create new...", "PresetEditForm_createNew");

        [Localize]
        private static readonly Text dontRandomizeText = new Text("Not randomized:", "PresetEditForm_dontRandomize");
        [Localize]
        private static readonly Text removeSourceText = new Text("Excluded everywhere:", "PresetEditForm_removeSource");
        [Localize]
        private static readonly Text classRemoveSourceText = new Text("Excluded:", "PresetEditForm_classRemoveSource");
        [Localize]
        private static readonly Text enemyCategoryText = new Text("Enemy Categories", "PresetEditForm_enemyCategory");
        [Localize]
        private static readonly Text adjustSourceText = new Text("Frequency Adjustments", "PresetEditForm_adjustSource");
        [Localize]
        private static readonly Text manualText = new Text("Manual Placement", "PresetEditForm_manual");
        [Localize]
        private static readonly Text manualDescText = new Text(
            "Manual placement replaces individual enemies by id or type."
            + " It overrides all other configuration and ignores ALL placement safety heuristics."
            + " To replace specific enemy instances, you can input their unique numerical id found in spoiler logs.",
            "PresetEditForm_manualDesc");
        [Localize]
        private static readonly Text classInheritDefaultText = new Text("Same as parent", "PresetEditForm_classInherit");
        [Localize]
        private static readonly Text classInheritText = new Text("Copy config from {0}", "PresetEditForm_classInheritName");
        [Localize]
        private static readonly Text classMergeDefaultText = new Text("Merge with parent", "PresetEditForm_classMerge");
        [Localize]
        private static readonly Text classMergeText = new Text("Merge with {0}", "PresetEditForm_classMergeName");
        [Localize]
        private static readonly Text classNorandomText = new Text("Not randomized", "PresetEditForm_classNorandom");
        [Localize]
        private static readonly Text classRandomText = new Text("Randomized", "PresetEditForm_classRandom");
        [Localize]
        private static readonly Text classCustomText = new Text("Custom", "PresetEditForm_classCustom");

        [Localize]
        private static readonly Text dlcSiloText = new Text("Randomize DLC separately for all bosses and enemies", "PresetEditForm_dlcsilo");
        [Localize]
        private static readonly Text dlcSiloClassText = new Text("Randomize DLC separately for {0}", "PresetEditForm_dlcsiloClass");

        [Localize]
        private static readonly Text poolDefaultText = new Text("Self", "PresetEditForm_poolDefault");
        [Localize]
        private static readonly Text poolDefaultExplainText = new Text("Same category ({0})", "PresetEditForm_poolDefaultExplain");
        [Localize]
        private static readonly Text poolEnemiesText = new Text("%", "PresetEditForm_poolEnemies");
        [Localize]
        private static readonly Text poolTargetText = new Text("Enemy:", "PresetEditForm_poolTarget");
        [Localize]
        private static readonly Text poolSourceText = new Text("Replacement:", "PresetEditForm_poolSource");

        private readonly Dictionary<string, string> toInternal = new Dictionary<string, string>();
        private readonly Dictionary<string, string> toDisplay = new Dictionary<string, string>();
        private readonly Dictionary<EnemyClass, string> toDisplayClass = new Dictionary<EnemyClass, string>();
        private readonly Dictionary<string, EnemyClass> toInternalClass = new Dictionary<string, EnemyClass>();
        // Mainly for redundant categories
        private readonly Dictionary<string, List<string>> displayParents = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, EnemyCategory> displayCats = new Dictionary<string, EnemyCategory>();

        private readonly List<AutocompleteItem> enemyOptions;
        private readonly List<AutocompleteItem> enemyPoolOptions;
        private readonly List<AutocompleteItem> enemyManualOptions;
        private readonly HashSet<string> allEnemyPoolOptions;
        private readonly List<string> oopsAllOptions;

        // 5 seems the limit of stability, if not past it
        // The 1 is here to enable the mode for individual categories
        private static readonly List<int> multipliers = new List<int> { 1, 2, 3, 4, 5 };

        public PresetEditForm(EnemyAnnotations ann, Messages messages, EnemyPreset init, bool dlcSeparate)
        {
            this.ann = ann;
            this.messages = messages;
            this.dlcSeparate = dlcSeparate;
            InitializeComponent();
            FormMessages.SetFormText(messages, this, FormMessages.GetFormText(this));

            if (string.IsNullOrWhiteSpace(init.Name))
            {
                throw new Exception("Internal error: preset given without name");
            }

            // Set up ComboBox mapping, and internal<->display names
            void mapName(string intern, string display)
            {
                toDisplay[intern] = display;
                toInternal[display] = intern;
            }
            void mapClass(EnemyClass intern, string display)
            {
                toDisplayClass[intern] = display;
                toInternalClass[display] = intern;
                mapName(intern.ToString(), display);
            }
            foreach (EnemyClass cl in (EnemyClass[])Enum.GetValues(typeof(EnemyClass)))
            {
                string text = cl.ToString();
                if (ClassNames.TryGetValue(cl, out Text t)) text = messages.Get(t);
                mapClass(cl, text);
            }
            foreach (EnemyClassGroup clg in (EnemyClassGroup[])Enum.GetValues(typeof(EnemyClassGroup)))
            {
                string text = clg.ToString();
                if (ClassGroupNames.TryGetValue(clg, out Text t)) text = messages.Get(t);
                mapName(clg.ToString(), text);
            }
            // Blank needed at start to prevent autofill
            oopsAllOptions = new List<string> { "" };
            enemyOptions = new();
            enemyManualOptions = new();
            List<string> singletons = ann.Singletons ?? new List<string>();
            void addCategories(List<EnemyCategory> cats, List<string> strs, List<EnemyCategory> subs)
            {
                if (subs != null)
                {
                    cats.AddRange(subs);
                }
                if (strs != null)
                {
                    cats.AddRange(strs.Select(s => new EnemyCategory { Name = s }));
                }
            }
            foreach (EnemyCategory cat in ann.AllCategories)
            {
                if (cat.Name == null || cat.Hidden) continue;
                List<EnemyCategory> subs = new List<EnemyCategory> { cat };
                addCategories(subs, cat.Partition, cat.Partitions);
                addCategories(subs, cat.Partial, cat.Partials);
                addCategories(subs, cat.Instance, cat.Instances);
                string firstOption = null;
                foreach (EnemyCategory sub in subs)
                {
                    string option = ToDisplay(sub.Name);
                    displayCats[option] = sub;
                    string dispOption = (sub == cat ? "" : "- ") + option + (sub.Count > 1 ? $" ({sub.Count})" : "");
                    string[] keywords = cat.Keywords?.Split(' ');
                    AutocompleteItem item = new AutocompleteItem(option, dispOption, keywords);
                    enemyOptions.Add(item);
                    if (sub.Classes == null && sub.Contains == null) enemyManualOptions.Add(item);
                    if (!singletons.Contains(sub.Name))
                    {
                        oopsAllOptions.Add(option);
                    }
                    if (firstOption == null)
                    {
                        firstOption = option;
                        // For now, only support Contains at top-level
                        if (sub.Contains != null)
                        {
                            // More broad category than model name. This won't result in a consistent order but whatever
                            foreach (string contain in sub.Contains)
                            {
                                Util.AddMulti(displayParents, contain, option);
                            }
                        }
                    }
                    else
                    {
                        // The suggestions should go from most broad to most specific
                        Util.AddMulti(displayParents, option, firstOption);
                    }
                    // Finally, most specific of all, manually listed parents
                    if (sub.Parents != null)
                    {
                        foreach (string parent in sub.Parents)
                        {
                            Util.AddMulti(displayParents, option, parent);
                        }
                    }
                }
            }
            enemyPoolOptions = enemyOptions.ToList();
            // Same thing but with Self
            // This is rewritten in AddEnemyEntry per-category
            string defaultText = messages.Get(poolDefaultText);
            string norandomText = messages.Get(poolNorandomText);
            mapName("default", defaultText);
            mapName("norandom", norandomText);
            enemyPoolOptions.InsertRange(0, new[] { new AutocompleteItem(defaultText), new AutocompleteItem(norandomText) });
            allEnemyPoolOptions = new HashSet<string>(enemyPoolOptions.Select(i => i.Value));
            foreach (EnemyClass cl in (EnemyClass[])Enum.GetValues(typeof(EnemyClass)))
            {
                // For parents, these behave similarly to classes themselves, with a few differences.
                // They are higher in priority, they do not have any parents themselves, and are the parents of those categories.
                string text = GetSelfName(cl);
                toInternal[text] = "default";
                allEnemyPoolOptions.Add(text);
                // This is just for redundancy calculations
                displayCats[text] = new EnemyCategory { Name = "default", Classes = new List<EnemyClass> { cl } };
            }

            oopsAllBox.GotFocus += (sender, e) =>
            {
                if (oopsAllBox.DataSource == null)
                {
                    oopsAllBox.DataSource = oopsAllOptions;
                }
            };
            oopsAllBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            oopsAllBox.AutoCompleteSource = AutoCompleteSource.ListItems;
            oopsAllBox.DropDownStyle = ComboBoxStyle.DropDownList;

            multiplyBox.DataSource = multipliers.Select(n => messages.Get(multiplyOptionText, n)).ToList();
            // classMultiplyBox.DataSource = multipliers.Select(n => messages.Get(multiplyOptionText, n)).ToList();

            {
                overallSilo = new CheckBox();
                // Exclude from messages with prefix
                overallSilo.Name = "custom_overallSilo";
                overallSilo.Text = messages.Get(dlcSiloText);
                overallSilo.AutoSize = true;
                overallSilo.CheckedChanged += overallSilo_Changed;
                globalPanel.Controls.Add(overallSilo);
                AddLineBreak(globalPanel);
            }

            // TODO: Prevent these from getting localized separately
            // Their check state is checked in LoadPreset
            foreach (KeyValuePair<string, Text> entry in PresetOptionsText)
            {
                CheckBox check = new CheckBox();
                check.Name = "custom_" + entry.Key;
                check.Text = messages.Get(entry.Value);
                check.AutoSize = true;
                check.CheckedChanged += customOpt_Changed;
                globalPanel.Controls.Add(check);
            }

            // newPoolButton.Click += NewPoolButton_Click;

            Preset = init.CloneConfiguration();
            // Disable Apply (hopefully)
            Modified = false;
        }

        private string ToDisplay(string val) => toDisplay.TryGetValue(val, out string res) ? res : val;
        private string ToInternal(string val) => toInternal.TryGetValue(val, out string res) ? res : val;

        private string GetSelfName(EnemyClass cl) => messages.Get(poolDefaultExplainText, toDisplayClass[cl]);

        private void PresetEditForm_Load(object sender, EventArgs e)
        {
            tabControl.Appearance = TabAppearance.FlatButtons;
            tabControl.ItemSize = new Size(0, 1);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.TabStop = false;

            // Delayed so that setting things to Checked actually takes effect
            LoadPreset(Preset);
        }

        private void UpdatePresetList()
        {
            // Various options, including current one
            List<string> presets = GetPresetNames();
            if (!presets.Contains(Preset.Name))
            {
                presets.Add(Preset.Name);
            }
            presets.Sort();
            presets.Add(messages.Get(createNewText));
            simultaneousUpdate = true;
            presetSelect.DataSource = presets;
            presetSelect.SelectedItem = Preset.Name;
            simultaneousUpdate = false;
        }

        private void directoryButton_Click(object sender, EventArgs e)
        {
            DirectoryInfo dir = new DirectoryInfo("presets");
            if (!dir.Exists)
            {
                dir.Create();
            }
            Process.Start(new ProcessStartInfo(dir.FullName) { UseShellExecute = true });
        }

        [Localize]
        private static readonly Text customHelpText = new Text(@"Enemy randomizer works by swapping out enemies with other enemies taken from elsewhere in the game. It does this independently for each enemy category. Here you can configure a custom list of source enemies which can replace enemies in this category. You can also merge some categories together, by selecting ""Merge with"" in subcategories, or clicking on ""Quick edit"" links below to do this automatically.

When configuring source enemies, you can click the + button to add a new source group. The % probability across all groups in a category must add up to 100%. If you want 10% of enemies in this category to become any boss, use a 90% group with ""Same category"" and a 10% group with ""All Bosses"". If you want only 50% of enemies in this category to be randomized, use a 50% group with ""Same category"" and a 50% group with ""Not randomized"".", "PresetEditForm_customHelpText");

        private void classCustomHelp_LinkClicked(object sender, EventArgs e)
        {
            ScrollDialog.Show(this, messages.Get(customHelpText));
        }

        [Localize]
        private static readonly Text enterNameText = new Text("Enter new preset name", "PresetEditForm_enterName");
        [Localize]
        private static readonly Text invalidNameText = new Text("Invalid preset name: {0}", "PresetEditForm_invalidName");

        [Localize]
        private static readonly Text confirmNorandomText = new Text("\"Not randomized\" can't be added to this source group because other enemies are already configured. Would you like to create a new group to set a percentage for non-random enemies?", "PresetEditForm_confirmNorandom");
        [Localize]
        private static readonly Text redundantEnemyTooltipText = new Text("\"{0}\" can be removed from this list because it's already included in \"{1}\".", "PresetEditForm_redundantEnemyTooltip");
        [Localize]
        private static readonly Text redundantEnemyTooltipPoolText = new Text("Use the + button if you want to make a separate source group with this enemy.", "PresetEditForm_redundantEnemyTooltipPool");

        private void saveAsButton_Click(object sender, EventArgs e)
        {
            string name = PromptPresetName();
            if (name == null) return;
            SaveUI();
            EnemyPreset replace = Preset.CloneConfiguration();
            replace.Name = name;
            if (!ContinueAfterSaveAs(replace)) return;
            Preset = replace;
            // A defensive copy for further saving. Hopefully not too time-consuming with enemy lists inside it.
            SavedPreset = Preset.CloneConfiguration();
            Modified = false;
            UpdatePresetList();
        }

        private void applyButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveUI();
                Preset.SavePreset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messages.Get(FormText.SaveFailureText, Preset.FileName, ex), messages.Get(FormText.ErrorTitleText),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SavedPreset = Preset.CloneConfiguration();
            Modified = false;
            UpdatePresetList();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            applyButton_Click(sender, e);
            Close();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                messages.Get(FormText.ConfirmResetPresetText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.OKCancel);
            if (result != DialogResult.OK)
            {
                return;
            }
            EnemyPreset replace = MakeClassBasedDefault(ann);
            replace.Name = Preset.Name;
            replace.Description = Preset.Description;
            Preset = replace;
            Modified = true;
            LoadPreset(Preset);
        }

        private void presetSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Only manual changes are of interest
            if (simultaneousUpdate) return;
            bool createNew = presetSelect.SelectedIndex == presetSelect.Items.Count - 1;
            string loadName = (string)presetSelect.SelectedItem;
            if (!createNew && loadName == Preset.Name)
            {
                // No-op in this case, hopefully
                return;
            }
            if (!ContinueAfterSave())
            {
                // This manages its own Modified/UpdatePresetList state
                return;
            }
            try
            {
                EnemyPreset replace;
                if (createNew)
                {
                    string name = PromptPresetName();
                    if (name == null) return;
                    replace = MakeClassBasedDefault(ann);
                    replace.Name = name;
                    if (!ContinueAfterSaveAs(replace)) return;
                }
                else
                {
                    try
                    {
                        replace = EnemyPreset.LoadPreset(loadName);
                        replace.MigrateVersion(ann);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(FormText.LoadFailureText, Preset.FileName, ex), messages.Get(FormText.ErrorTitleText),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                Preset = replace;
                SavedPreset = Preset.CloneConfiguration();
                Modified = false;
                LoadPreset(Preset);
            }
            finally
            {
                UpdatePresetList();
            }
        }

        private void PresetEditForm_FormClosing(object sender, FormClosingEventArgs e)
        {
#if DEBUG
            if (DebugOpen)
            {
                Application.Exit();
                return;
            }
#endif
            if (!ContinueAfterSave())
            {
                e.Cancel = true;
            }
        }

        private string PromptPresetName()
        {
            string filename = PromptText(Preset.Name, messages.Get(enterNameText));
            if (filename == null) return null;
            filename = string.Concat(filename.Trim().Split(Path.GetInvalidFileNameChars()));
            // Also, edge case for supporting option strings... better quoting would be smart
            filename = Regex.Replace(filename, @"-+", "-");
            filename = Regex.Replace(filename, @" +", " ");
            if (string.IsNullOrWhiteSpace(filename) || InvalidPresetNames.Contains(filename))
            {
                MessageBox.Show(
                    messages.Get(invalidNameText, filename), messages.Get(FormText.ErrorTitleText),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            return filename;
        }

        private bool ContinueAfterSaveAs(EnemyPreset replace)
        {
            if (File.Exists(replace.FileName))
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(FormText.ConfirmOverwriteText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK)
                {
                    return false;
                }
            }
            try
            {
                SaveUI();
                replace.SavePreset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messages.Get(FormText.SaveFailureText, replace.FileName, ex), messages.Get(FormText.ErrorTitleText),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private bool ContinueAfterSave()
        {
            if (Modified)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(FormText.ConfirmLeaveText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        SaveUI();
                        Preset.SavePreset();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(FormText.SaveFailureText, Preset.FileName, ex), messages.Get(FormText.ErrorTitleText),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                    SavedPreset = Preset;
                    // In theory, immediately exiting, but just in case
                    Modified = false;
                    UpdatePresetList();
                }
                else if (result == DialogResult.No)
                {
                    // Nothing to do
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void LoadPreset(EnemyPreset preset)
        {
            InitializeClasses();

            Preset = preset;
            simultaneousUpdate = true;
            modeCustom.Checked = true;
            multiplyMode = false;
            if (preset.EnemyMultiplier > 0)
            {
                modeMultiply.Checked = true;
                multiplyMode = true;
                SetDropdownMultiplier(multiplyBox, preset.EnemyMultiplier);
            }
            else if (preset.OopsAll != null)
            {
                string text = ToDisplay(preset.OopsAll);
                if (oopsAllOptions.Contains(text))
                {
                    if (oopsAllBox.DataSource == null)
                    {
                        oopsAllBox.DataSource = oopsAllOptions;
                    }
                    modeOopsall.Checked = true;
                    oopsAllBox.SelectedItem = text;
                }
            }
            if (!multiplyMode)
            {
                // Set to 2x by default
                multiplyBox.SelectedIndex = 1;
            }
            description.Text = preset.Description ?? "";

            foreach (string opt in PresetOptionsText.Keys)
            {
                if (globalPanel.Controls[$"custom_{opt}"] is not CheckBox check) continue;
                // Options. null means unset, "none" means no options enabled
                if (preset.Options == null)
                {
                    check.Checked = EldenDefaultOptions.Contains(opt);
                }
                else
                {
                    check.Checked = preset[opt];
                }
            }
            if (dlcSeparate)
            {
                overallSilo.Checked = true;
                overallSilo.Enabled = false;
            }
            else
            {
                overallSilo.Checked = preset.OverallSilo == LocationData.AreaSiloType.DLC;
                overallSilo.Enabled = true;
            }

            simultaneousUpdate = false;

            // Overall, clear everything, then initialize everything
            if (dontRandomize != null) RemoveEnemyEntry(globalPanel, dontRandomize);

            dontRandomize = AddEnemyEntry(globalPanel, dontRandomizeText, null, 0, init: preset.DontRandomize);
            if (removeSource != null) RemoveEnemyEntry(globalPanel, removeSource);
            removeSource = AddEnemyEntry(globalPanel, removeSourceText, null, 1, init: preset.RemoveSource);

            if (classesView.Nodes.Count > 0)
            {
                simultaneousUpdate = true;
                EnemyClass startClass = ann.Classes[0].Class;
                classesView.SelectedNode = classesView.Nodes[0];
                simultaneousUpdate = false;
                // This has its own simultaneousUpdate
                // It would nice to do viewmodel updates in a coherent way
                InitializeClass(startClass);
            }
            UpdatePresetList();
        }

        // Don't access before InitializeTabs
        private List<PageControls> Pages { get; set; }
        private class PageControls
        {
            public string Name { get; set; }
            public PageKey Key { get; set; }
            public TabPage Tab { get; set; }
            public FlowLayoutPanel Panel { get; set; }

            public Label ClassBreak { get; set; }
            public RadioButton ClassInherit { get; set; }
            public RadioButton ClassMerge { get; set; }
            public RadioButton ClassMerge2 { get; set; }
            public RadioButton ClassNorandom { get; set; }
            public RadioButton ClassRandom { get; set; }
            public RadioButton ClassCustom { get; set; }
            public ComboBox ClassMultiplyBox { get; set; }
            public LinkLabel ClassCustomHelp {get; set;}
            public Label DlcSiloBreak { get; set; }
            public CheckBox DlcSilo { get; set; }
            public Label NewPoolBreak { get; set; }
            public Button NewPoolButton { get; set; }

            public PresetEnemyControl ClassRemoveSource { get; set; }
            // Be careful, this can get out of sync
            public List<PresetEnemyControl> ClassControls { get; set; } = new List<PresetEnemyControl>();
            // Used for TabIndex ordering within classControls
            public int TabCount = 0;
        }
        private PageKey CurrentPage;

        private record PageKey(TabType Type, EnemyClass Class = EnemyClass.Default)
        {
            public static PageKey For(EnemyClass type) => new PageKey(TabType.Class, type);
        }
        private enum TabType
        {
            Class,
            AdjustSource,
            Manual,
        }

        private PageControls GetPage(PageKey key) => Pages.Find(p => p.Key.Equals(key));

        private void InitializeClasses()
        {
            // Initialize tree view
            classesView.BeginUpdate();
            classesView.Nodes.Clear();

            Pages = new();
            tabControl.Controls.Clear();

            List<EnemyClass> toAdd = ann.Classes.Select(c => c.Class).ToList();
            void addClass(TreeNodeCollection parent, PageKey key, ClassConfig conf, string text)
            {
                if (conf != null)
                {
                    if (toAdd.Contains(conf.Class))
                    {
                        toAdd.Remove(conf.Class);
                    }
                    else
                    {
                        return;
                    }
                    text = toDisplayClass[conf.Class];
                }
                if (text == null) return;
                TreeNode node = new TreeNode(text);
                node.Tag = key;
                parent.Add(node);
                TabPage tabPage = new();
                tabControl.Controls.Add(tabPage);
                tabControl.Text = text;
                PageControls page = new()
                {
                    Name = text,
                    Key = key,
                    Tab = tabPage,
                };
                Pages.Add(page);
                // Could also look at the ClassConfig for parent-child relationships but this is a bad idea
            }
            TreeNode classParent = new TreeNode(messages.Get(enemyCategoryText));
            classesView.Nodes.Add(classParent);
            foreach (ClassConfig conf in ann.Classes)
            {
                addClass(classParent.Nodes, PageKey.For(conf.Class), conf, null);
            }
            addClass(classesView.Nodes, new PageKey(TabType.AdjustSource), null, messages.Get(adjustSourceText));
            addClass(classesView.Nodes, new PageKey(TabType.Manual), null, messages.Get(manualText));

            classesView.ExpandAll();
            classesView.EndUpdate();
        }

        private void classesView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Avoid SavePool accidentally using a currentClass of empty
            if (simultaneousUpdate) return;
            SaveClass();
            PageKey key = (PageKey)e.Node.Tag;
            if (key == null) return;
            InitializeClass(key);
        }

        private void SaveUI()
        {
            SaveClass();
            // Supported non-pool currently: Oops All, RemoveSource, DontRandomize, Description
            Preset.OopsAll = null;
            Preset.EnemyMultiplier = 0;
            if (modeMultiply.Checked)
            {
                Preset.EnemyMultiplier = GetDropdownMultiplier(multiplyBox);
            }
            else if (modeOopsall.Checked && !string.IsNullOrWhiteSpace(oopsAllBox.Text))
            {
                Preset.OopsAll = ToInternal(oopsAllBox.Text);
            }
            Preset.RemoveSource = GetEnemyLabelString(removeSource);
            Preset.DontRandomize = GetEnemyLabelString(dontRandomize);
            Preset.Description = string.IsNullOrWhiteSpace(description.Text) ? null : description.Text;
            foreach (string opt in PresetOptionsText.Keys)
            {
                if (globalPanel.Controls[$"custom_{opt}"] is not CheckBox check) continue;
                Preset[opt] = check.Checked;
            }
        }

        private static Label AddLineBreak(FlowLayoutPanel p)
        {
            Label dummyLabel = new Label { Width = 0, Height = 0, Margin = new Padding(0), Padding = new Padding(0) };
            p.Controls.Add(dummyLabel);
            p.SetFlowBreak(dummyLabel, true);
            return dummyLabel;
        }

        private void InitializeClass(EnemyClass cl)
        {
            InitializeClass(PageKey.For(cl));
        }

        private void InitializeClass(PageKey key)
        {
            PageControls page = GetPage(key);
            if (page == null) return;
            simultaneousUpdate = true;
            CurrentPage = key;

            // Maybe page.Name would work here
            string titleText = page.Name;
            string descText = null;
            if (CurrentPage.Type == TabType.Class)
            {
                EnemyClass titleCl = CurrentPage.Class;
                if (ClassDocs.TryGetValue(titleCl, out Text desc))
                {
                    descText = messages.Get(desc);
                }
            }
            else if (CurrentPage.Type == TabType.AdjustSource)
            {
                descText = messages.Get(AdjustSourceDoc);
            }
            else if (CurrentPage.Type == TabType.Manual)
            {
                descText = messages.Get(manualDescText);
            }

            // Create the tab the first time, then update pass after
            if (page.Tab.Controls.Count == 0)
            {
                FlowLayoutPanel pagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
                page.Panel = pagePanel;
                page.Tab.Controls.Add(pagePanel);
                RadioButton addRadio(Text text)
                {
                    RadioButton radio = new RadioButton { AutoSize = true, Text = messages.Get(text) };
                    radio.CheckedChanged += classMode_CheckChanged;
                    radio.Margin = new Padding(4, 3, 4, 3);
                    pagePanel.Controls.Add(radio);
                    return radio;
                }

                Label title = new Label { Text = page.Name, AutoSize = true };
                title.Margin = new Padding(0, 0, 0, 0);
                title.Font = new Font(title.Font.FontFamily, title.Font.Size * 1.25f, title.Font.Style | FontStyle.Bold);
                pagePanel.Controls.Add(title);
                if (descText != null)
                {
                    AddLineBreak(pagePanel);
                    Label desc = new Label { Text = descText, AutoSize = true };
                    desc.Margin = new Padding(0, 0, 0, 8);
                    pagePanel.Controls.Add(desc);
                    // pagePanel.SetFlowBreak(desc, true);
                }
                page.ClassBreak = AddLineBreak(pagePanel);

                page.ClassInherit = addRadio(classInheritDefaultText);
                page.ClassMerge = addRadio(classMergeDefaultText);
                page.ClassMerge2 = addRadio(classMergeDefaultText);
                page.ClassNorandom = addRadio(classNorandomText);
                page.ClassRandom = addRadio(classRandomText);
                page.ClassCustom = addRadio(classCustomText);

                LinkLabel customHelp = new LinkLabel();
                customHelp.AutoSize = true;
                customHelp.Margin = new Padding(0);
                customHelp.Padding = new Padding(0, 4, 0, 0);
                // customHelp.TabStop = true;
                customHelp.Text = "?";
                customHelp.Click += classCustomHelp_LinkClicked;
                pagePanel.Controls.Add(customHelp);
                page.ClassCustomHelp = customHelp;

                ComboBox multBox = new ComboBox();
                multBox.DataSource = multipliers.Select(n => messages.Get(multiplyOptionText, n)).ToList();
                multBox.DropDownStyle = ComboBoxStyle.DropDown;
                multBox.FormattingEnabled = true;
                multBox.Margin = new Padding(4, 3, 4, 3);
                multBox.Size = new Size(76, 24);
                multBox.TextChanged += opt_Changed;
                multBox.BindingContext = BindingContext;
                pagePanel.Controls.Add(multBox);
                page.ClassMultiplyBox = multBox;

                page.DlcSiloBreak = AddLineBreak(pagePanel);

                CheckBox check = new CheckBox();
                check.Text = messages.Get(dlcSiloClassText, page.Name);
                check.AutoSize = true;
                check.CheckedChanged += opt_Changed;
                pagePanel.Controls.Add(check);
                page.DlcSilo = check;

                Label poolBreak = new Label();
                poolBreak.AutoSize = true;
                poolBreak.Margin = new Padding(4, 0, 4, 0);
                poolBreak.Size = new Size(0, 16);
                pagePanel.Controls.Add(poolBreak);
                pagePanel.SetFlowBreak(poolBreak, true);
                page.NewPoolBreak = poolBreak;

                Button poolButton = new Button();
                poolButton.Size = new Size(50, 23);
                poolButton.TabIndex = 9999999;
                poolButton.Text = "+";
                poolButton.UseVisualStyleBackColor = true;
                poolButton.Click += NewPoolButton_Click;
                pagePanel.Controls.Add(poolButton);
                pagePanel.SetFlowBreak(poolButton, true);
                page.NewPoolButton = poolButton;
            }

            bool isPool = false;
            string removeSource = null;
            List<PoolAssignment> sources = new List<PoolAssignment>();
            if (CurrentPage.Type == TabType.Class)
            {
                EnemyClass cl = CurrentPage.Class;
                ClassConfig conf = ann.Classes.Find(c => c.Class == cl);
                // Visibility is determined by config, do selection after
                bool hasParent = conf.Parent != EnemyClass.Default;
                EnemyClass altParent = EnemyClass.Default;
                if (hasParent && conf.AltParent != null)
                {
                    altParent = conf.AltParent.Where(c => c != conf.Parent).FirstOrDefault();
                }
                // TODO: Stop using visible as a condition anywhere
                page.ClassBreak.Visible = true;
                bool showInherit = page.ClassInherit.Visible = hasParent && !conf.NoMerge && !conf.NoSelfRandom;
                bool showMerge = page.ClassMerge.Visible = hasParent && !conf.NoMerge;
                bool showMerge2 = page.ClassMerge2.Visible = hasParent && !conf.NoMerge && altParent != EnemyClass.Default;
                bool showNorandom = page.ClassNorandom.Visible = cl != EnemyClass.Spectator;
                // NoSelfRandom isn't used by preset, but ignored at top level in randomizer
                bool randomAllowed = cl != EnemyClass.Spectator && !conf.NoSelfRandom;
                bool showRandom = page.ClassRandom.Visible = randomAllowed && conf.NoMerge;
                bool showCustom = page.ClassCustom.Visible = randomAllowed && !conf.NoMerge;
                page.ClassCustomHelp.Visible = showCustom;
                isPool = (!conf.NoSelfRandom && !conf.NoMerge) || cl == EnemyClass.Spectator;
                page.NewPoolButton.Visible = isPool;
                if (hasParent)
                {
                    page.ClassInherit.Text = messages.Get(classInheritText, toDisplayClass[conf.Parent]);
                    page.ClassMerge.Text = messages.Get(classMergeText, toDisplayClass[conf.Parent]);
                    if (altParent == EnemyClass.Default)
                    {
                        page.ClassMerge2.Text = messages.Get(classMergeDefaultText);
                    }
                    else
                    {
                        page.ClassMerge2.Text = messages.Get(classMergeText, toDisplayClass[altParent]);
                    }
                }
                else
                {
                    page.ClassInherit.Text = messages.Get(classInheritDefaultText);
                    page.ClassMerge.Text = messages.Get(classMergeDefaultText);
                    page.ClassMerge2.Text = messages.Get(classMergeDefaultText);
                }
                Preset.Classes.TryGetValue(cl, out ClassAssignment assign);
                if (conf.DlcSilo)
                {
                    page.DlcSilo.Visible = page.DlcSiloBreak.Visible = true;
                    if (overallSilo.Checked)
                    {
                        // Ignore class silo in this case
                        page.DlcSilo.Checked = true;
                        page.DlcSilo.Enabled = false;
                    }
                    else
                    {
                        page.DlcSilo.Checked = assign.ClassSilo == LocationData.AreaSiloType.DLC;
                        page.DlcSilo.Enabled = true;
                    }
                }
                else
                {
                    page.DlcSilo.Visible = page.DlcSiloBreak.Visible = false;
                }
                bool inherit = assign == null ? conf.DefaultInherit : assign.InheritParent;
                bool merge = assign != null && !conf.NoMerge && assign.MergeParent;
                bool norandom = (assign != null && assign.NoRandom) || conf.NoSelfRandom;
                if (showInherit && inherit)
                {
                    page.ClassInherit.Checked = true;
                }
                else if (showMerge && merge)
                {
                    if (altParent != EnemyClass.Default && assign.ManualParent == altParent)
                    {
                        page.ClassMerge2.Checked = true;
                    }
                    else
                    {
                        page.ClassMerge.Checked = true;
                    }
                }
                else if (showNorandom && norandom)
                {
                    page.ClassNorandom.Checked = true;
                }
                else if (showRandom)
                {
                    page.ClassRandom.Checked = true;
                }
                else if (showCustom)
                {
                    page.ClassCustom.Checked = true;
                }
                if (isPool && assign != null)
                {
                    sources = assign.Pools;
                    removeSource = assign.RemoveSource;
                }
                // Attempt a basic initial value for multiplier. Set unconditionally tbh, it's probably fine
                UpdateMultiplierConfigurable();
                {
                    // Inherit by default, use stored index if available
                    // This may be undone by UpdateCustomPoolEnable
                    if (assign != null && assign.EnemyMultiplier > 0)
                    {
                        SetDropdownMultiplier(page.ClassMultiplyBox, assign.EnemyMultiplier);
                    }
                    else
                    {
                        SetDropdownMultiplier(page.ClassMultiplyBox, GetDropdownMultiplier(multiplyBox));
                    }
                }
            }
            else
            {
                // Adjust source and manual
                page.ClassBreak.Visible = false;
                page.ClassInherit.Visible = false;
                page.ClassMerge.Visible = false;
                page.ClassMerge2.Visible = false;
                page.ClassNorandom.Visible = false;
                page.ClassRandom.Visible = false;
                page.ClassCustom.Visible = false;
                page.ClassMultiplyBox.Visible = false;
                page.DlcSilo.Visible = page.DlcSiloBreak.Visible = false;
                page.ClassCustomHelp.Visible = false;
                page.NewPoolButton.Visible = true;
                if (CurrentPage.Type == TabType.AdjustSource && Preset.AdjustSource != null)
                {
                    foreach (SourceAdjustment adjust in Preset.AdjustSource)
                    {
                        // Just reuse PoolAssignment here
                        // Assume total of 1000 in all cases. There is no global adjust here
                        int amount = 1000;
                        if (adjust.Total > 0)
                        {
                            float mult = Math.Max(0, Math.Min(1, (float)adjust.Keep / adjust.Total));
                            amount = (int)Math.Round(mult * 1000);
                        }
                        sources.Add(new PoolAssignment
                        {
                            Pool = adjust.Source,
                            Weight = amount,
                        });
                    }
                }
            }
            // Clear existing entries if any
            if (page.ClassRemoveSource != null)
            {
                RemoveEnemyEntry(page.Panel, page.ClassRemoveSource);
                page.ClassRemoveSource = null;
            }
            foreach (PresetEnemyControl exist in page.ClassControls)
            {
                RemoveEnemyEntry(page.Panel, exist);
            }
            page.ClassControls.Clear();
            page.TabCount = 0;

            if (isPool)
            {
                page.ClassRemoveSource = AddEnemyEntry(
                    page.Panel, classRemoveSourceText, null, page.TabCount++, init: removeSource);
            }
            if (sources != null)
            {
                Text enemyText = poolEnemiesText;
                foreach (PoolAssignment assign in sources)
                {
                    page.ClassControls.Add(AddEnemyEntry(
                        page.Panel, enemyText, CurrentPage, page.TabCount++, init: assign.Pool, initVal: assign.Weight));
                }
            }
            if (CurrentPage.Type == TabType.Manual && Preset.Enemies != null)
            {
                foreach ((string target, string source) in Preset.Enemies)
                {
                    page.ClassControls.Add(
                        AddEnemyEntry(page.Panel, poolTargetText, CurrentPage, page.TabCount++, init: target));
                    page.ClassControls.Add(
                        AddEnemyEntry(page.Panel, poolSourceText, CurrentPage, page.TabCount++, init: source));
                }
            }
            AdjustPercentages();
            UpdateCustomPoolEnable();
            tabControl.SelectedTab = page.Tab;
            simultaneousUpdate = false;
        }

        private void NewPoolButton_Click(object sender, EventArgs e)
        {
            if (editCustomPool)
            {
                PageControls page = GetPage(CurrentPage);
                bool full = page.ClassControls.Count == 0 || CurrentPage.Type == TabType.AdjustSource;
                string initTags = null;
                if (page.ClassControls.Count == 0 && CurrentPage.Type == TabType.Class)
                {
                    initTags = "default";
                }
                page.ClassControls.Add(
                    AddEnemyEntry(page.Panel, poolEnemiesText, CurrentPage, page.TabCount++, init: initTags, initVal: full ? 1000 : 0));
                Modified = true;
            }
            else if (editAssignPool)
            {
                PageControls page = GetPage(CurrentPage);
                page.ClassControls.Add(
                    AddEnemyEntry(page.Panel, poolTargetText, CurrentPage, page.TabCount++));
                page.ClassControls.Add(
                    AddEnemyEntry(page.Panel, poolSourceText, CurrentPage, page.TabCount++));
                Modified = true;
            }
        }

        private void SaveClass()
        {
            PageControls page = GetPage(CurrentPage);
            if (page == null) return;
            // This happens when switching away from an old pool, as well as saving overall
            AdjustPercentages(preSave: true);
            if (CurrentPage.Type == TabType.Class)
            {
                EnemyClass cl = CurrentPage.Class;
                ClassConfig conf = ann.Classes.Find(c => c.Class == cl);
                ClassAssignment assign = new ClassAssignment();
                if (page.ClassNorandom.Visible && page.ClassNorandom.Checked)
                {
                    assign.NoRandom = true;
                }
                else if (page.ClassMerge.Visible && page.ClassMerge.Checked)
                {
                    assign.MergeParent = true;
                }
                else if (page.ClassMerge2.Visible && page.ClassMerge2.Checked)
                {
                    assign.MergeParent = true;
                    if (conf != null && conf.AltParent != null)
                    {
                        // If not present, this will fallback to main parent
                        assign.ManualParent = conf.AltParent.Where(c => c != conf.Parent).FirstOrDefault();
                    }
                }
                else if (page.ClassInherit.Visible && page.ClassInherit.Checked)
                {
                    assign.InheritParent = true;
                }
                // This can be the else branch, but preserve it even if disabled
                if (page.ClassRemoveSource != null)
                {
                    assign.RemoveSource = GetEnemyLabelString(page.ClassRemoveSource);
                }
                if (page.DlcSilo.Visible)
                {
                    // If overall silo is checked, leave it the same
                    if (page.DlcSilo.Enabled)
                    {
                        assign.ClassSilo = page.DlcSilo.Checked ? LocationData.AreaSiloType.DLC : LocationData.AreaSiloType.None;
                    }
                }
                else
                {
                    assign.ClassSilo = LocationData.AreaSiloType.None;
                }
                foreach (PresetEnemyControl enemy in page.ClassControls)
                {
                    if (enemy.Tags.Count == 0) continue;
                    int amt = 0;
                    if (enemy.Percent != null && double.TryParse(enemy.Percent.Text, out double val))
                    {
                        amt = (int)Math.Round(val * 10);
                    }
                    amt = Math.Max(0, Math.Min(amt, 1000));
                    if (assign.Pools == null) assign.Pools = new List<PoolAssignment>();
                    assign.Pools.Add(new PoolAssignment
                    {
                        Pool = GetEnemyLabelString(enemy),
                        Weight = amt,
                    });
                }
                if (page.ClassMultiplyBox.Visible)
                {
                    assign.EnemyMultiplier = GetDropdownMultiplier(page.ClassMultiplyBox);
                }
                if (Preset.Classes == null) Preset.Classes = new Dictionary<EnemyClass, ClassAssignment>();
                Preset.Classes[cl] = assign;
            }
            else if (CurrentPage.Type == TabType.AdjustSource)
            {
                // Adjust sources
                Preset.AdjustSource = new List<SourceAdjustment>();
                foreach (PresetEnemyControl enemy in page.ClassControls)
                {
                    if (enemy.Tags.Count == 0) continue;
                    int amt = 0;
                    if (enemy.Percent != null && double.TryParse(enemy.Percent.Text, out double val))
                    {
                        amt = (int)Math.Round(val * 10);
                    }
                    amt = Math.Max(0, Math.Min(amt, 1000));
                    Preset.AdjustSource.Add(new SourceAdjustment
                    {
                        Source = GetEnemyLabelString(enemy),
                        Keep = amt,
                        Total = 1000,
                    });
                }
                if (Preset.AdjustSource.Count == 0) Preset.AdjustSource = null;
            }
            else if (CurrentPage.Type == TabType.Manual)
            {
                Preset.Enemies = new();
                for (int i = 0; i < page.ClassControls.Count - 1; i += 2)
                {
                    string target = GetEnemyLabelString(page.ClassControls[i]);
                    string source = GetEnemyLabelString(page.ClassControls[i + 1]);
                    if (target != null && source != null && !target.Contains(';'))
                    {
                        Preset.Enemies[target] = source;
                    }
                }
                if (Preset.Enemies.Count == 0) Preset.Enemies = null;
            }
        }

        private void AdjustPercentages(bool requireNoFocus = false, bool preSave = false, Control justFocused = null)
        {
            PageControls page = GetPage(CurrentPage);
            if (page == null) return;
            if (CurrentPage.Type == TabType.Manual) return;

            if (page.ClassControls.Count == 0) return;
            if (requireNoFocus)
            {
                Control foc = FindFocusedControl();
                if (foc != null && page.ClassControls.Any(e => foc == e.Percent))
                {
                    return;
                }
            }

            List<int> amts = new List<int>();
            int defaultVal = CurrentPage.Type == TabType.Class ? 0 : 1000;
            int keepSameIndex = -1;
            foreach (PresetEnemyControl enemy in page.ClassControls)
            {
                if (enemy.Percent == null)
                {
                    // Shouldn't happen, but probably fine to ignore
                    amts.Add(defaultVal);
                    continue;
                }
                int amt;
                if (preSave && enemy.Tags.Count == 0)
                {
                    // This is only done sometimes, to avoid people filling in percentage
                    // and getting it immediately overwritten.
                    amt = 0;
                    enemy.Percent.Text = FormatPercent(amt / 10.0);
                }
                else if (double.TryParse(enemy.Percent.Text, out double val))
                {
                    amt = (int)Math.Round(val * 10);
                    int clamp = Math.Max(0, Math.Min(amt, 1000));
                    if (amt != clamp)
                    {
                        amt = clamp;
                        enemy.Percent.Text = FormatPercent(amt / 10.0);
                    }
                }
                else
                {
                    amt = defaultVal;
                    enemy.Percent.Text = FormatPercent(amt / 10.0);
                }
                if (justFocused != null && enemy.Percent == justFocused)
                {
                    keepSameIndex = amts.Count();
                }
                amts.Add(amt);
            }
            if (CurrentPage.Type == TabType.Class)
            {
                AdjustClassPercentages(amts, keepSameIndex);
                for (int i = 0; i < page.ClassControls.Count; i++)
                {
                    PresetEnemyControl enemy = page.ClassControls[i];
                    if (enemy.Percent == null) break;
                    enemy.Percent.Text = FormatPercent(amts[i] / 10.0);
                }
            }
            // For frequency adjustment, it checks between 0 and 100% above
        }

        private static void AdjustClassPercentages(List<int> amts, int keepSameIndex = -1)
        {
            // Edit the amts to add up to 1000.
            int iter = 0;
            int total = amts.Sum();
            while (iter++ < 100 && total != 1000)
            {
                if (total > 1000)
                {
                    int max = amts.Max();
                    List<int> maxIndices = amts
                        .Select((a, i) => (a, i))
                        .Where(e => e.Item1 == max)
                        .Select(e => e.Item2)
                        .ToList();
                    if (maxIndices.Count == 0) break;
                    // Somewhat tricky thing: if there are multiple max values,
                    // potentially decrease them below 1000. next iteration should
                    // bump one of them back up again.
                    // e.g. if two values are 800 out of total 1601, reduction is 601,
                    // individual reductions are 301.
                    // More complicated resolution would involve looking at second highest
                    // and not reducing it lower than that, but meh even more edge cases there.
                    int reduce = (int)Math.Ceiling((total - 1000.0) / maxIndices.Count);
                    int amt = Math.Max(0, max - reduce);
                    foreach (int index in maxIndices)
                    {
                        amts[index] = amt;
                    }
                }
                else if (total < 1000)
                {
                    // Find the biggest entry and expand it further
                    // There shouldn't be more than one pass of this, probably?
                    int index = -1;
                    int amt = -1;
                    int avoidIndex = keepSameIndex;
                    while (true)
                    {
                        List<int> partialMaxes = amts.Where((a, i) => a < 1000 && (avoidIndex < 0 || i != avoidIndex)).ToList();
                        if (partialMaxes.Count > 0)
                        {
                            int max = partialMaxes.Max();
                            index = amts.IndexOf(max);
                            if (index >= 0)
                            {
                                amt = max + Math.Min(1000 - total, 1000 - max);
                                break;
                            }
                        }
                        if (avoidIndex < 0)
                        {
                            break;
                        }
                        else
                        {
                            avoidIndex = -1;
                        }
                    }
                    if (amt == -1) break;
                    amts[index] = amt;
                }
                total = amts.Sum();
            }
            while (iter++ < 100 && total < 1000)
            {
                // Find the biggest entry and shrink it down
                total = amts.Sum();
            }
        }

        private Control FindFocusedControl()
        {
            // Used for detecting if percentage textboxes have been exited out of
            Control control = this;
            ContainerControl container = control as ContainerControl;
            while (container != null)
            {
                control = container.ActiveControl;
                container = control as ContainerControl;
            }
            return control;
        }

        private PresetEnemyControl AddEnemyEntry(
            FlowLayoutPanel panel, Text label, PageKey key, int tabIndex, string init = null, int? initVal = null)
        {
            // This list may need to be copied for use as a DataSource? Edit it based on the category, though.
            bool hasPercent = false;
            bool hasRemove = false;
            string defaultDisplay = null;
            List<AutocompleteItem> opts = null;
            if (key != null)
            {
                if (key.Type == TabType.Manual)
                {
                    // Yikes
                    hasRemove = label == poolTargetText;
                    opts = enemyManualOptions.ToList();
                }
                else
                {
                    hasRemove = true;
                    hasPercent = true;
                }
                if (key.Type == TabType.Class)
                {
                    opts = enemyPoolOptions.ToList();
                    // Indices set up in constructor
                    defaultDisplay = GetSelfName(key.Class);
                    opts[0] = new AutocompleteItem(defaultDisplay);
                }
            }
            if (opts == null)
            {
                opts = enemyOptions.ToList();
            }
            PresetEnemyControl enemy = new PresetEnemyControl(
                label == null ? "" : messages.Get(label),
                opts,
                tabIndex,
                hasPercent: hasPercent,
                hasRemove: hasRemove);
            panel.SuspendLayout();
            panel.Controls.Add(enemy);
            if (enemy.Remove != null)
            {
                panel.Controls.Add(enemy.Remove);
                enemy.Remove.Click += (sender, e) =>
                {
                    if (editCustomPool)
                    {
                        PageControls page = GetPage(CurrentPage);
                        if (page == null) return;
                        RemoveEnemyEntry(panel, enemy);
                        page.ClassControls.Remove(enemy);
                        AdjustPercentages();
                        Modified = true;
                    }
                    else if (editAssignPool)
                    {
                        PageControls page = GetPage(CurrentPage);
                        if (page == null) return;
                        List<PresetEnemyControl> toRemove = new() { enemy };
                        int controlIndex = page.ClassControls.IndexOf(enemy);
                        if (controlIndex != -1 && controlIndex + 1 < page.ClassControls.Count)
                        {
                            toRemove.Add(page.ClassControls[controlIndex + 1]);
                        }
                        foreach (PresetEnemyControl rem in toRemove)
                        {
                            RemoveEnemyEntry(panel, rem);
                            page.ClassControls.Remove(rem);
                        }
                        Modified = true;
                    }
                };
            }
            if (enemy.Percent != null)
            {
                panel.Controls.Add(enemy.Percent);
                // The weights are out of 1000
                enemy.Percent.Text = initVal is int val ? FormatPercent(val / 10f) : "0";
                enemy.Percent.TextChanged += (sender, e) => Modified = true;
                enemy.Percent.Leave += (sender, e) => AdjustPercentages(requireNoFocus: true, justFocused: enemy.Percent);
            }
            panel.Controls.Add(enemy.Pre);
            panel.Controls.Add(enemy.Selector);
            panel.SetFlowBreak(enemy, true);
            enemy.Selector.SelectedValueChanged += (sender, e) =>
            {
                AddEnemyLabel(panel, enemy, interactive: true);
                Modified = true;
            };
            UpdatePlusButton(panel);
            panel.ResumeLayout();
            if (!string.IsNullOrWhiteSpace(init))
            {
                foreach (string tag in PhraseRe.Split(init.Trim()))
                {
                    if (tag == "none") continue;
                    string text = ToDisplay(tag);
                    // Hardcode "default" conversion here so it finds something present in opts above
                    if (tag == "default" && defaultDisplay != null)
                    {
                        text = defaultDisplay;
                    }
                    if (IsEntityId(text, out _) || opts.Any(i => i.Value == text))
                    {
                        AddEnemyLabel(panel, enemy, text, batch: true);
                    }
                }
                UpdateRedundant(enemy);
            }
            return enemy;
        }

        private void RemoveEnemyEntry(FlowLayoutPanel panel, PresetEnemyControl enemy)
        {
            panel.SuspendLayout();
            panel.Controls.Remove(enemy.Pre);
            panel.Controls.Remove(enemy.Selector);
            if (enemy.Remove != null) panel.Controls.Remove(enemy.Remove);
            if (enemy.Percent != null) panel.Controls.Remove(enemy.Percent);
            foreach (LinkLabel tag in enemy.Tags) panel.Controls.Remove(tag);
            enemy.Tip.RemoveAll();
            panel.Controls.Remove(enemy);
            UpdatePlusButton(panel);
            panel.ResumeLayout();
        }

        // The pure syntax way was label.Text.TrimEnd('⨉', ' ') - maybe more info can be added to tag later
        private static string TagText(LinkLabel label) => (string)label.Tag;

        private string GetEnemyLabelString(PresetEnemyControl enemy)
        {
            if (enemy == null) return null;
            List<string> tags = new List<string>();
            // List<string> opts = (List<string>)enemy.Selector.DataSource;
            foreach (LinkLabel tagLabel in enemy.Tags)
            {
                string tag = TagText(tagLabel);
                if (string.IsNullOrWhiteSpace(tag) || !(allEnemyPoolOptions.Contains(tag) || IsEntityId(tag, out _))) continue;
                string value = ToInternal(tag);
                // Avoid mixed norandom here, as otherwise the preset will error out (or silently ignore)
                if (value == "norandom" && enemy.Tags.Count > 1) continue;
                tags.Add(value);
            }
            return tags.Count > 0 ? string.Join("; ", tags) : null;
        }

        private void AddEnemyLabel(FlowLayoutPanel panel, PresetEnemyControl enemy, string text = null, bool interactive = false, bool batch = false)
        {
            if (text == null)
            {
                // Autoselect from adjacent dropdown
                // if (enemy.Selector.SelectedIndex == 0 || !(enemy.Selector.SelectedValue is string value))
                if (string.IsNullOrEmpty(enemy.Selector.SelectedValue))
                {
                    return;
                }
                // Assume this value is legitimate. It may be silently filtered during save if not
                // if (string.IsNullOrWhiteSpace(value)) return;
                text = enemy.Selector.SelectedValue;
                // enemy.Selector.SelectedIndex = 0;
                // enemy.Selector.Text = "";
            }
            // Check to replace entry for targets, as only one is allowed at a time
            // Note: CurrentPage may be null for global panels
            PageControls page = GetPage(CurrentPage);
            // TODO: Better way of identifying target entries
            if (page != null && CurrentPage.Type == TabType.Manual && panel == page.Panel && enemy.Remove != null)
            {
                foreach (LinkLabel tag in enemy.Tags.ToList())
                {
                    RemoveEnemyLabel(panel, enemy, tag);
                }
            }
            int panelIndex = panel.Controls.IndexOf(enemy.Selector);
            if (panelIndex == -1) return;
            panelIndex++;
            int linkIndex = 0;
            // TODO: Use logic from PresetItemForm with tags etc, this is too fragile
            while (panelIndex < panel.Controls.Count)
            {
                Control nextCon = panel.Controls[panelIndex];
                if (nextCon is LinkLabel existLabel)
                {
                    if (TagText(existLabel) == text) return;
                }
                else break;
                linkIndex++;
                panelIndex++;
            }
            // Helper confirmation for norandom
            if (interactive
                && linkIndex > 0
                && page != null
                && CurrentPage.Type == TabType.Class
                && toInternal.TryGetValue(text, out string internalText) && internalText == "norandom")
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(confirmNorandomText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    page.ClassControls.Add(
                        AddEnemyEntry(page.Panel, poolEnemiesText, CurrentPage, page.TabCount++, init: "norandom", initVal: 0));
                }
                // There's no harm in adding it here, since it's filtered later, but it may be confusing.
                return;
            }
            // Labels have no info about their format representation. GetEnemyLabelString does the reverse mapping
            LinkLabel label = new LinkLabel();
            label.Tag = text;
            label.AutoSize = true;
            label.Text = text + "⨉";
            label.Font = enemy.Pre.Font;
            label.Margin = new Padding(0, 8, 0, 0);
            label.LinkBehavior = LinkBehavior.NeverUnderline;
            label.Links[0].Length = 1;
            label.Links[0].Start = label.Text.Length - 1;
            label.LinkClicked += (sender, e) =>
            {
                RemoveEnemyLabel(panel, enemy, label);
                AdjustPercentages();
                Modified = true;
            };
            label.TabIndex = enemy.NewTabIndex();
            // Very functional, thanks
            panel.SuspendLayout();
            panel.Controls.Add(label);
            panel.Controls.SetChildIndex(label, panelIndex);
            enemy.Tags.Add(label);
            UpdatePlusButton(panel);
            panel.ResumeLayout();
            if (!batch)
            {
                UpdateRedundant(enemy);
            }
        }

        private void RemoveEnemyLabel(FlowLayoutPanel panel, PresetEnemyControl enemy, LinkLabel label)
        {
            if (!panel.Controls.Contains(label)) return;
            enemy.Tags.Remove(label);
            panel.Controls.Remove(label);
            UpdatePlusButton(panel);
            UpdateRedundant(enemy);
        }

        private void UpdateRedundant(PresetEnemyControl enemy)
        {
            enemy.Tip.RemoveAll();
            HashSet<string> allDisplayOptions = new HashSet<string>(enemy.Tags.Select(TagText));
            List<(EnemyCategory, string)> allClassOptions = new();
            foreach (LinkLabel label in enemy.Tags)
            {
                string text = TagText(label);
                if (displayCats.TryGetValue(text, out EnemyCategory cat) && cat.Classes != null)
                {
                    cat.ClassSet ??= new(cat.Classes);
                    if (cat.Name == "default")
                    {
                        allClassOptions.Insert(0, (cat, text));
                    }
                    else
                    {
                        allClassOptions.Add((cat, text));
                    }
                }
            }
            foreach (LinkLabel label in enemy.Tags)
            {
                string text = TagText(label);
                string parent = null;
                if (displayCats.TryGetValue(text, out EnemyCategory cat))
                {
                    List<EnemyClass> catClasses = cat.Classes ?? cat.ParentClasses;
                    // Never suggest removing self category, since it may be different in other categories.
                    if (cat.Name != "default" && catClasses != null)
                    {
                        // There is some oddness about inherited classes *not* having redundancy in the same way.
                        // e.g. Demi-Human in Regular Enemies technically adds it to Wildlife too.
                        // There's also the two equivalent classes with 'default', which could be checked for SetEquals.
                        // For the most part, don't try to catch all false positives as it makes basic feedback useless.
                        parent = allClassOptions.Find(i => i.Item1.ClassSet.IsSupersetOf(catClasses) && i.Item1 != cat).Item2;
                    }
                }
                if (parent == null && displayParents.TryGetValue(text, out List<string> catParents))
                {
                    parent = catParents.Intersect(allDisplayOptions).FirstOrDefault();
                }
                if (parent == null)
                {
                    label.ForeColor = DefaultForeColor;
                }
                else
                {
                    label.ForeColor = Color.Gray;
                    string tip = messages.Get(redundantEnemyTooltipText, text, parent);
                    if (enemy.Remove != null)
                    {
                        tip += "\n" + messages.Get(redundantEnemyTooltipPoolText);
                    }
                    enemy.Tip.SetToolTip(label, tip);
                }
            }
        }

        private void UpdatePlusButton(FlowLayoutPanel panel)
        {
            // This is called even for global panel, just have special behavior for current page
            PageControls page = GetPage(CurrentPage);
            if (page == null || page.Panel != panel) return;
            // This is moved based on the invisible element
            int index = panel.Controls.IndexOf(page.NewPoolButton);
            if (index == -1) return;
            if (index != panel.Controls.Count - 1)
            {
                panel.Controls.SetChildIndex(page.NewPoolButton, panel.Controls.Count - 1);
                index = panel.Controls.IndexOf(page.NewPoolBreak);
                if (index != -1)
                {
                    panel.Controls.SetChildIndex(page.NewPoolBreak, panel.Controls.Count - 2);
                }
            }
        }

        private void classMode_CheckChanged(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
            UpdateCustomPoolEnable();
        }

        private void opt_Changed(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
        }

        private void customOpt_Changed(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
        }

        private void overallSilo_Changed(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
            // Do the update here, as it affects each category
            if (!dlcSeparate)
            {
                Preset.OverallSilo = overallSilo.Checked ? LocationData.AreaSiloType.DLC : LocationData.AreaSiloType.None;
            }
            // State for the category itself
            if (CurrentPage.Type != TabType.Class || !Preset.Classes.TryGetValue(CurrentPage.Class, out ClassAssignment assign)) return;
            PageControls page = GetPage(CurrentPage);
            if (page == null || !page.DlcSilo.Visible) return;
            if (overallSilo.Checked)
            {
                // Ignore class silo in this case
                page.DlcSilo.Checked = true;
                page.DlcSilo.Enabled = false;
            }
            else
            {
                page.DlcSilo.Checked = assign.ClassSilo == LocationData.AreaSiloType.DLC;
                page.DlcSilo.Enabled = true;
            }
        }

        [Localize]
        private static readonly Text confirmMultiplyRandomText = new Text("Disable enemy randomization with Enemy Onslaught?", "PresetEditForm_confirmMultiplyRandom");
        // private static readonly Text confirmMultiplyDlcText = new Text("Note: Enemy Onslaught does not support DLC enemies yet", "PresetEditForm_confirmMultiplyDlc");

        private void mode_Changed(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
            bool newMode = modeMultiply.Checked;
            if (newMode && !multiplyMode)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(confirmMultiplyRandomText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    if (toDisplay.TryGetValue("AllEnemies", out string all))
                    {
                        AddEnemyLabel(globalPanel, dontRandomize, all);
                    }
                }
            }
            multiplyMode = newMode;
            UpdateMultiplier(false);
        }

        private void multiplyBox_TextChanged(object sender, EventArgs e)
        {
            // Initializing the box, before the Preset is set, can cause this
            if (simultaneousUpdate || Preset == null) return;
            Modified = true;
            UpdateMultiplier(true);
        }

        private void UpdateMultiplier(bool setAll)
        {
            PageControls page = GetPage(CurrentPage);
            if (multiplyMode)
            {
                Preset.EnemyMultiplier = GetDropdownMultiplier(multiplyBox);
                if (page != null && (!page.ClassMultiplyBox.Visible || setAll))
                {
                    SetDropdownMultiplier(page.ClassMultiplyBox, GetDropdownMultiplier(multiplyBox));
                }
                UpdateMultiplierConfigurable();
                if (setAll)
                {
                    foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
                    {
                        ClassAssignment assign = entry.Value;
                        if (assign != null)
                        {
                            // This is probably fine for all currently supported classes
                            assign.EnemyMultiplier = Preset.EnemyMultiplier;
                        }
                    }
                }
                SaveClass();
            }
            else
            {
                Preset.EnemyMultiplier = 0;
                if (page != null) page.ClassMultiplyBox.Visible = false;
                foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
                {
                    ClassAssignment assign = entry.Value;
                    if (assign != null)
                    {
                        // This is probably fine for all currently supported classes
                        assign.EnemyMultiplier = 0;
                    }
                }
            }
        }

        // Utility functions for enemy multiplier with custom amounts
        private int GetDropdownMultiplier(ComboBox multiplyBox)
        {
            // Unfortunately, SelectedIndex can be 0 instead of -1 with custom text, so don't use it here
            string text = new string(multiplyBox.Text.TakeWhile(char.IsNumber).ToArray());
            if (int.TryParse(text, out int mult))
            {
                return mult;
            }
            else
            {
                // If this is being called, onslaught is selected, so make a valid value
                return 2;
            }
        }

        private void SetDropdownMultiplier(ComboBox multiplyBox, int multiplier)
        {
            int index = multipliers.IndexOf(multiplier);
            if (index >= 0)
            {
                multiplyBox.SelectedIndex = index;
            }
            else
            {
                multiplyBox.Text = messages.Get(multiplyOptionText, multiplier);
            }
        }

        private void UpdateCustomPoolEnable()
        {
            PageControls page = GetPage(CurrentPage);
            if (page == null) return;
            editCustomPool = false;
            editAssignPool = false;
            if (CurrentPage.Type == TabType.Class)
            {
                editCustomPool = page.ClassCustom.Checked || CurrentPage.Class == EnemyClass.Spectator;
            }
            else if (CurrentPage.Type == TabType.AdjustSource)
            {
                editCustomPool = true;
            }
            else if (CurrentPage.Type == TabType.Manual)
            {
                editAssignPool = true;
            }
            bool anyEdit = editCustomPool || editAssignPool;
            if (page.ClassRemoveSource != null) page.ClassRemoveSource.Enabled = anyEdit;
            if (page.DlcSilo != null) page.DlcSilo.Enabled = !overallSilo.Checked && (page.ClassCustom.Checked || page.ClassRandom.Checked);
            foreach (PresetEnemyControl enemy in page.ClassControls)
            {
                enemy.Enabled = anyEdit;
            }
            page.NewPoolButton.Enabled = anyEdit;
            UpdateMultiplierConfigurable();
        }

        private bool UpdateMultiplierConfigurable()
        {
            PageControls page = GetPage(CurrentPage);
            if (page == null) return false;
            // HostileNPC kinda works now
            bool independentMultiply = CurrentPage.Type == TabType.Class
                && !page.ClassMerge.Checked && !page.ClassMerge2.Checked && !page.ClassInherit.Checked;
            bool showMultiplier = multiplyMode && Preset.EnemyMultiplier > 0 && independentMultiply;
            page.ClassMultiplyBox.Visible = showMultiplier;
            return showMultiplier;
        }

        private void quickSeparate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SaveClass();
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                ClassAssignment assign = entry.Value;
                ClassConfig conf = ann.Classes.Find(c => c.Class == entry.Key);
                if (assign.MergeParent)
                {
                    assign.MergeParent = false;
                    if (conf != null && conf.DefaultInherit)
                    {
                        assign.InheritParent = true;
                    }
                }
            }
            InitializeClass(CurrentPage);
        }

        private void quickMergeBoss_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SaveClass();
            List<EnemyClass> bossClasses = new List<EnemyClass>
            {
                EnemyClass.MinorBoss, EnemyClass.Miniboss,
                EnemyClass.DragonMiniboss, EnemyClass.NightMiniboss, EnemyClass.Evergaol,
            };
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                if (!bossClasses.Contains(entry.Key)) continue;
                SetMerged(entry.Key, entry.Value);
            }
            InitializeClass(CurrentPage);
        }

        private void quickMergeMinor_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SaveClass();
            List<EnemyClass> bossClasses = new List<EnemyClass>
            {
                EnemyClass.Miniboss,
                EnemyClass.DragonMiniboss, EnemyClass.NightMiniboss, EnemyClass.Evergaol,
            };
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                if (!bossClasses.Contains(entry.Key)) continue;
                ClassAssignment assign = entry.Value;
                SetMerged(entry.Key, assign);
                if (entry.Key == EnemyClass.Miniboss)
                {
                    assign.ManualParent = EnemyClass.MinorBoss;
                }
            }
            InitializeClass(CurrentPage);
        }

        private void quickMergeAll_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SaveClass();
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                SetMerged(entry.Key, entry.Value);
            }
            InitializeClass(CurrentPage);
        }

        private void SetMerged(EnemyClass cl, ClassAssignment assign)
        {
            ClassConfig conf = ann.Classes.Find(c => c.Class == cl);
            if (conf != null && !conf.NoMerge)
            {
                assign.MergeParent = true;
                assign.NoRandom = false;
                assign.InheritParent = false;
            }
        }

        private void quickReducePassive_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null || ann.PassiveAdjustments == null) return;
            Modified = true;
            SaveClass();
            HashSet<string> existing = new HashSet<string>();
            if (Preset.AdjustSource != null)
            {
                foreach (SourceAdjustment adjust in Preset.AdjustSource)
                {
                    if (adjust.Source == null) continue;
                    foreach (string enemy in PhraseRe.Split(adjust.Source))
                    {
                        existing.Add(enemy);
                    }
                }
            }
            foreach (PassiveAdjustment adj in ann.PassiveAdjustments)
            {
                if (adj.Source == null) continue;
                List<string> sources = PhraseRe.Split(adj.Source).Where(s => !existing.Contains(s)).ToList();
                if (sources.Count > 0)
                {
                    Preset.AdjustSource.Add(new SourceAdjustment
                    {
                        Source = string.Join("; ", sources),
                        Keep = adj.Percent * 10,
                        Total = 1000,
                    });
                }
            }
            InitializeClass(CurrentPage);
        }

        private void quickReplace10_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SaveClass();
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                EnemyClass cl = entry.Key;
                ClassAssignment assign = entry.Value;
                if (cl != EnemyClass.Basic && cl != EnemyClass.Wildlife) continue;
                if (cl == EnemyClass.Wildlife && (assign.InheritParent || assign.MergeParent)) break;
                assign.InheritParent = assign.MergeParent = assign.NoRandom = false;
                if (assign.Pools == null) assign.Pools = new List<PoolAssignment>();
                if (assign.Pools.Count == 0)
                {
                    assign.Pools.Add(new PoolAssignment
                    {
                        Pool = "default",
                        Weight = 1000,
                    });
                }
                // Very simple protection against doing it twice
                PoolAssignment pool = assign.Pools.Find(p => p.Pool == "AllBosses");
                if (pool == null)
                {
                    pool = new PoolAssignment
                    {
                        Pool = "AllBosses",
                    };
                    assign.Pools.Add(pool);
                }
                pool.Weight = 100;
                if (CurrentPage.Type == TabType.Class && CurrentPage.Class != cl)
                {
                    List<int> amts = assign.Pools.Select(p => p.Weight).ToList();
                    AdjustClassPercentages(amts);
                    for (int i = 0; i < assign.Pools.Count; i++)
                    {
                        assign.Pools[i].Weight = amts[i];
                    }
                }
            }
            // Reload class, which also adjusts percentages.
            // This is done manually above for offscreen classes.
            InitializeClass(CurrentPage);
        }

        private void quickReplaceWildlife_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SaveClass();
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                EnemyClass cl = entry.Key;
                ClassAssignment assign = entry.Value;
                if (cl != EnemyClass.Wildlife) continue;
                assign.InheritParent = assign.MergeParent = assign.NoRandom = false;
                if (assign.Pools == null) assign.Pools = new List<PoolAssignment>();
                if (assign.Pools.Count == 0)
                {
                    assign.Pools.Add(new PoolAssignment
                    {
                        Pool = "Basic",
                        Weight = 1000,
                    });
                }
                else
                {
                    List<string> replaces = new List<string> { "default", "Wildlife" };
                    foreach (PoolAssignment pool in assign.Pools)
                    {
                        if (pool.Pool == null) continue;
                        pool.Pool = string.Join(
                            "; ",
                            PhraseRe.Split(pool.Pool).Select(x => replaces.Contains(x) ? "Basic" : x));
                    }
                }
            }
            // This also adjusts percentages
            InitializeClass(CurrentPage);
        }

        // https://stackoverflow.com/questions/5427020/prompt-dialog-in-windows-forms
        // I do not really feel like making a whole other form
        private string PromptText(string text, string caption)
        {
            Form prompt = new Form
            {
                Width = 500,
                Height = 130,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            prompt.Icon = Icon;
            prompt.Font = globalPanel.Font;
            TextBox textBox = new TextBox
            {
                Left = 20,
                Top = 20,
                Width = 440,
                Text = text,
            };
            Button confirmation = new Button
            {
                // Reuse this here
                Text = okButton.Text,
                Left = 360,
                Width = 100,
                Top = 50,
                DialogResult = DialogResult.OK,
            };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
