using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Preset;

namespace RandomizerCommon
{
    public partial class PresetEditForm : Form
    {
        public Preset Preset;
        public Preset SavedPreset;
        private EnemyAnnotations ann;
        private Messages messages;
        private PresetEnemyControl dontRandomize;
        private PresetEnemyControl removeSource;
        private PresetEnemyControl classRemoveSource;
        private EnemyClass? currentClass;
        // Be careful, this can get out of sync
        private List<PresetEnemyControl> classControls = new List<PresetEnemyControl>();
        // Preventing event handlers from triggering each other
        private bool simultaneousUpdate = false;
        // Whether custom pools may be edited
        private bool editCustomPool = false;
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

        // [Localize]
        // private static readonly Text poolTitleFullText = new Text("{0} Configuration", "PresetEditForm_poolTitleFull");
        [Localize]
        private static readonly Text dontRandomizeText = new Text("Not randomized:", "PresetEditForm_dontRandomize");
        [Localize]
        private static readonly Text removeSourceText = new Text("Excluded everywhere:", "PresetEditForm_removeSource");
        [Localize]
        private static readonly Text classRemoveSourceText = new Text("Excluded:", "PresetEditForm_classRemoveSource");
        [Localize]
        private static readonly Text adjustSourceText = new Text("Frequency Adjustments", "PresetEditForm_adjustSource");
        private string adjustSourceLabel;
        [Localize]
        private static readonly Text classInheritText = new Text("Copy config from {0}", "PresetEditForm_classInheritName");
        private string classInheritLabel;
        [Localize]
        private static readonly Text classMergeText = new Text("Merge with {0}", "PresetEditForm_classMergeName");
        private string classMergeLabel;
        // TODO: Parameterize
        [Localize]
        private static readonly Text poolDefaultText = new Text("Self", "PresetEditForm_poolDefault");
        [Localize]
        private static readonly Text poolEnemiesText = new Text("%", "PresetEditForm_poolEnemies");

        private readonly Dictionary<string, string> toInternal = new Dictionary<string, string>();
        private readonly Dictionary<string, string> toDisplay = new Dictionary<string, string>();
        private readonly Dictionary<EnemyClass, string> toDisplayClass = new Dictionary<EnemyClass, string>();
        private readonly Dictionary<string, EnemyClass> toInternalClass = new Dictionary<string, EnemyClass>();

        private readonly List<string> enemyOptions;
        private readonly List<string> enemyPoolOptions;
        private readonly List<string> oopsAllOptions;

        // 5 seems the limit of stability, if not past it
        // The 1 is here to enable the mode for individual categories
        private static readonly List<int> multipliers = new List<int> { 1, 2, 3, 4, 5 };

        public PresetEditForm(EnemyAnnotations ann, Messages messages, Preset init)
        {
            this.ann = ann;
            this.messages = messages;
            InitializeComponent();
            messages.SetFormText(this, messages.GetFormText(this));

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
                // TODO: Map defaults one-way
            }
            foreach (EnemyClassGroup clg in (EnemyClassGroup[])Enum.GetValues(typeof(EnemyClassGroup)))
            {
                string text = clg.ToString();
                if (ClassGroupNames.TryGetValue(clg, out Text t)) text = messages.Get(t);
                mapName(clg.ToString(), text);
            }
            string defaultText = messages.Get(poolDefaultText);
            mapName("default", defaultText);
            // Blank needed at start to prevent autofill
            oopsAllOptions = new List<string> { "" };
            enemyOptions = new List<string> { "" };
            List<string> singletons = ann.Singletons ?? new List<string>();
            foreach (EnemyCategory cat in ann.Categories)
            {
                if (cat.Name == null || cat.Hidden) continue;
                List<string> subnames = new List<string> { cat.Name };
                subnames.AddRange(new[] { cat.Partition, cat.Partial, cat.Instance }.Where(g => g != null).SelectMany(g => g));
                foreach (string subname in subnames)
                {
                    toDisplay.TryGetValue(subname, out string display);
                    // For now, indent makes autocomplete difficult
                    string option = (subname == cat.Name ? "" : "") + (display ?? subname);
                    enemyOptions.Add(option);
                    if (singletons.Contains(subname)) continue;
                    oopsAllOptions.Add(option);
                }
            }
            enemyPoolOptions = enemyOptions.ToList();
            // Same thing but with Self
            // TODO: Do this dynamically when classes are switched
            enemyPoolOptions.Insert(1, defaultText);

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
            classMultiplyBox.DataSource = multipliers.Select(n => messages.Get(multiplyOptionText, n)).ToList();

            // TODO: Prevent these from getting localized separately
            // Their check state is checked in LoadPreset
            foreach (KeyValuePair<string, Text> entry in OptionsText)
            {
                CheckBox check = new CheckBox();
                check.Name = "custom_" + entry.Key;
                check.Text = messages.Get(entry.Value);
                check.AutoSize = true;
                check.CheckedChanged += customOpt_Changed;
                globalPanel.Controls.Add(check);
            }

            InitializeClasses();

            newPoolButton.Click += (sender, e) =>
            {
                if (editCustomPool)
                {
                    bool full = classControls.Count == 0 || currentClass == null;
                    string initTags = null;
                    if (classControls.Count == 0 && currentClass != null)
                    {
                        initTags = "default";
                    }
                    classControls.Add(
                        AddEnemyEntry(customPanel, poolEnemiesText, true, initTags, full ? 1000 : 0));
                    Modified = true;
                }
            };

            Preset = init.CloneConfiguration();
            // Disable Apply (hopefully)
            Modified = false;
        }

        private void PresetEditForm_Load(object sender, EventArgs e)
        {
            // Delayed so that setting things to Checked actually takes effect
            LoadPreset(Preset);
        }

        private void LoadPreset(Preset preset)
        {
            Preset = preset;
            simultaneousUpdate = true;
            modeCustom.Checked = true;
            multiplyMode = false;
            if (multipliers.Contains(preset.EnemyMultiplier))
            {
                modeMultiply.Checked = true;
                multiplyBox.SelectedIndex = multipliers.IndexOf(preset.EnemyMultiplier);
                multiplyMode = true;
            }
            else if (preset.OopsAll != null)
            {
                toDisplay.TryGetValue(preset.OopsAll, out string text);
                text = text ?? preset.OopsAll;
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

            foreach (string opt in OptionsText.Keys)
            {
                if (globalPanel.Controls[$"custom_{opt}"] is not CheckBox check) continue;
                // Options. null means unset, "none" means no options enabled
                if (preset.Options == null)
                {
                    check.Checked = DefaultOptions.Contains(opt);
                }
                else
                {
                    check.Checked = preset[opt];
                }
            }

            simultaneousUpdate = false;

            // Overall, clear everything, then initialize everything
            if (dontRandomize != null) RemoveEnemyEntry(globalPanel, dontRandomize);

            dontRandomize =
                AddEnemyEntry(globalPanel, dontRandomizeText, false, preset.DontRandomize, null);
            if (removeSource != null) RemoveEnemyEntry(globalPanel, removeSource);
            removeSource =
                AddEnemyEntry(globalPanel, removeSourceText, false, preset.RemoveSource, null);

            if (classesView.Nodes.Count > 0)
            {
                simultaneousUpdate = true;
                currentClass = ann.Classes[0].Class;
                classesView.SelectedNode = classesView.Nodes[0];
                simultaneousUpdate = false;
                // This has its own simultaneousUpdate
                // It would nice to do viewmodel updates in a coherent way
                InitializeClass(currentClass);
            }
            UpdatePresetList();
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
        private static readonly Text errorTitleText = new Text("Error", "PresetEditForm_errorTitle");
        [Localize]
        private static readonly Text enterNameText = new Text("Enter new preset name", "PresetEditForm_enterName");
        [Localize]
        private static readonly Text invalidNameText = new Text("Invalid preset name: {0}", "PresetEditForm_invalidName");
        [Localize]
        private static readonly Text loadFailureText = new Text("Failed to load preset {0}\n{1}", "PresetEditForm_loadFailure");
        [Localize]
        private static readonly Text saveFailureText = new Text("Failed to save preset {0}\n{1}", "PresetEditForm_saveFailure");

        // Used by other forms. TODO rename in localized files
        [Localize]
        internal static readonly Text confirmTitleText = new Text("Confirm", "PresetEditForm_confirmTitle");
        [Localize]
        internal static readonly Text confirmOverwriteText = new Text("Overwrite existing file?", "PresetEditForm_confirmOverwrite");
        [Localize]
        internal static readonly Text confirmLeaveText = new Text("Save changes before exiting?", "PresetEditForm_confirmLeave");
        [Localize]
        internal static readonly Text confirmResetText = new Text("Reset this preset to default?", "PresetEditForm_confirmReset");

        private void saveAsButton_Click(object sender, EventArgs e)
        {
            string name = PromptPresetName();
            if (name == null) return;
            SaveUI();
            Preset replace = Preset.CloneConfiguration();
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
                    messages.Get(saveFailureText, Preset.FileName, ex), messages.Get(errorTitleText),
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
                messages.Get(confirmResetText), messages.Get(confirmTitleText), MessageBoxButtons.OKCancel);
            if (result != DialogResult.OK)
            {
                return;
            }
            Preset replace = MakeClassBasedDefault(ann);
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
                Preset replace;
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
                        replace = Preset.LoadPreset(loadName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(loadFailureText, Preset.FileName, ex), messages.Get(errorTitleText),
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
            if (string.IsNullOrWhiteSpace(filename))
            {
                MessageBox.Show(
                    messages.Get(invalidNameText, filename), messages.Get(errorTitleText),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            return filename;
        }

        private bool ContinueAfterSaveAs(Preset replace)
        {
            if (File.Exists(replace.FileName))
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(confirmOverwriteText), messages.Get(confirmTitleText), MessageBoxButtons.OKCancel);
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
                    messages.Get(saveFailureText, replace.FileName, ex), messages.Get(errorTitleText),
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
                    messages.Get(confirmLeaveText), messages.Get(confirmTitleText), MessageBoxButtons.YesNoCancel);
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
                            messages.Get(saveFailureText, Preset.FileName, ex), messages.Get(errorTitleText),
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

        private void InitializeClasses()
        {
            // Initialize tree view
            classesView.BeginUpdate();
            classesView.Nodes.Clear();

            // Also a view for AdjustSource
            adjustSourceLabel = messages.Get(adjustSourceText);
            List<EnemyClass> toAdd = ann.Classes.Select(c => c.Class).ToList();
            void addClass(TreeNodeCollection parent, ClassConfig conf, string text)
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
                parent.Add(node);
                // Maybe keep it linear for now, since parent relationship isn't the clearest
                if (false && conf != null)
                {
                    List<ClassConfig> children = ann.Classes
                        .Where(c => toAdd.Contains(c.Class) && c.Parent == conf.Class)
                        .ToList();
                    foreach (ClassConfig conf2 in children)
                    {
                        addClass(node.Nodes, conf2, null);
                    }
                }
            }
            foreach (ClassConfig conf in ann.Classes)
            {
                addClass(classesView.Nodes, conf, null);
            }
            addClass(classesView.Nodes, null, adjustSourceLabel);

            classesView.ExpandAll();
            classesView.EndUpdate();
        }

        private void classesView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Avoid SavePool accidentally using a currentClass of empty
            if (simultaneousUpdate) return;
            SavePool();
            if (e.Node.Text == adjustSourceLabel)
            {
                InitializeClass(null);
            }
            else if (toInternalClass.TryGetValue(e.Node.Text, out EnemyClass cl))
            {
                InitializeClass(cl);
            }
        }

        private void SaveUI()
        {
            SavePool();
            // Supported non-pool currently: Oops All, RemoveSource, DontRandomize, Description
            Preset.OopsAll = null;
            Preset.EnemyMultiplier = 0;
            if (modeMultiply.Checked)
            {
                Preset.EnemyMultiplier = multipliers[multiplyBox.SelectedIndex];
            }
            else if (modeOopsall.Checked && !string.IsNullOrWhiteSpace(oopsAllBox.Text))
            {
                string text = oopsAllBox.Text;
                toInternal.TryGetValue(text, out string value);
                Preset.OopsAll = value ?? text;
            }
            Preset.RemoveSource = GetEnemyLabelString(removeSource);
            Preset.DontRandomize = GetEnemyLabelString(dontRandomize);
            Preset.Description = string.IsNullOrWhiteSpace(description.Text) ? null : description.Text;
            foreach (string opt in OptionsText.Keys)
            {
                if (globalPanel.Controls[$"custom_{opt}"] is not CheckBox check) continue;
                Preset[opt] = check.Checked;
            }
        }

        private void InitializeClass(EnemyClass? maybeCl)
        {
            simultaneousUpdate = true;
            if (classInheritLabel == null)
            {
                classInheritLabel = classInherit.Text;
            }
            if (classMergeLabel == null)
            {
                classMergeLabel = classMerge.Text;
            }
            // Assume no class is Frequency Adjustments
            currentClass = maybeCl;
            if (maybeCl is EnemyClass titleCl)
            {
                poolTitle.Text = toDisplayClass[titleCl];
                if (ClassDocs.TryGetValue(titleCl, out Text desc))
                {
                    poolDesc.Text = messages.Get(desc);
                    poolDesc.Visible = true;
                }
                else
                {
                    poolDesc.Visible = false;
                }
            }
            else
            {
                poolTitle.Text = adjustSourceLabel;
                poolDesc.Text = messages.Get(AdjustSourceDoc);
                poolDesc.Visible = true;
            }
            bool isPool = false;
            string removeSource = null;
            List<PoolAssignment> sources = new List<PoolAssignment>();
            if (maybeCl is EnemyClass cl)
            {
                ClassConfig conf = ann.Classes.Find(c => c.Class == cl);
                // Visibility is determined by config, do selection after
                bool hasParent = conf.Parent != EnemyClass.Default;
                EnemyClass altParent = EnemyClass.Default;
                if (hasParent && conf.AltParent != null)
                {
                    altParent = conf.AltParent.Where(c => c != conf.Parent).FirstOrDefault();
                }
                classInherit.Visible = hasParent && !conf.NoMerge && !conf.NoSelfRandom;
                classMerge.Visible = hasParent && !conf.NoMerge;
                classMerge2.Visible = hasParent && !conf.NoMerge && altParent != EnemyClass.Default;
                classNorandom.Visible = cl != EnemyClass.Spectator;
                // NoSelfRandom isn't used by preset, but ignored at top level in randomizer
                bool randomAllowed = cl != EnemyClass.Spectator && !conf.NoSelfRandom;
                classRandom.Visible = randomAllowed && conf.NoMerge;
                classCustom.Visible = randomAllowed && !conf.NoMerge;
                isPool = (!conf.NoSelfRandom && !conf.NoMerge) || cl == EnemyClass.Spectator;
                newPoolButton.Visible = isPool;
                if (hasParent)
                {
                    classInherit.Text = messages.Get(classInheritText, toDisplayClass[conf.Parent]);
                    classMerge.Text = messages.Get(classMergeText, toDisplayClass[conf.Parent]);
                    if (altParent == EnemyClass.Default)
                    {
                        classMerge2.Text = classMergeLabel;
                    }
                    else
                    {
                        classMerge2.Text = messages.Get(classMergeText, toDisplayClass[altParent]);
                    }
                }
                else
                {
                    classInherit.Text = classInheritLabel;
                    classMerge.Text = classMergeLabel;
                    classMerge2.Text = classMergeLabel;
                }

                Preset.Classes.TryGetValue(cl, out ClassAssignment assign);
                bool inherit = assign == null ? conf.DefaultInherit : assign.InheritParent;
                bool merge = assign != null && !conf.NoMerge && assign.MergeParent;
                bool norandom = (assign != null && assign.NoRandom) || conf.NoSelfRandom;
                if (classInherit.Visible && inherit)
                {
                    classInherit.Checked = true;
                }
                else if (classMerge.Visible && merge)
                {
                    if (altParent != EnemyClass.Default && assign.ManualParent == altParent)
                    {
                        classMerge2.Checked = true;
                    }
                    else
                    {
                        classMerge.Checked = true;
                    }
                }
                else if (classNorandom.Visible && norandom)
                {
                    classNorandom.Checked = true;
                }
                else if (classRandom.Visible)
                {
                    classRandom.Checked = true;
                }
                else if (classCustom.Visible)
                {
                    classCustom.Checked = true;
                }
                if (isPool && assign != null)
                {
                    sources = assign.Pools;
                    removeSource = assign.RemoveSource;
                }
                // Attempt a basic initial value for multiplier
                UpdateMultiplierConfigurable();
                if (classMultiplyBox.Visible)
                {
                    // Inherit by default, use stored index if available
                    // This may be undone by UpdateCustomPoolEnable
                    if (assign != null && multipliers.Contains(assign.EnemyMultiplier))
                    {
                        classMultiplyBox.SelectedIndex = multipliers.IndexOf(assign.EnemyMultiplier);
                    }
                    else
                    {
                        classMultiplyBox.SelectedIndex = multiplyBox.SelectedIndex;
                    }
                }
            }
            else
            {
                // Adjust source
                classInherit.Visible = false;
                classMerge.Visible = false;
                classNorandom.Visible = false;
                classRandom.Visible = false;
                classCustom.Visible = false;
                classMultiplyBox.Visible = false;
                newPoolButton.Visible = true;
                if (Preset.AdjustSource != null)
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
            if (classRemoveSource != null)
            {
                RemoveEnemyEntry(customPanel, classRemoveSource);
                classRemoveSource = null;
            }
            foreach (PresetEnemyControl exist in classControls)
            {
                RemoveEnemyEntry(customPanel, exist);
            }
            classControls.Clear();

            if (isPool)
            {
                classRemoveSource = AddEnemyEntry(
                    customPanel, classRemoveSourceText, false, removeSource, null);
            }
            if (sources != null)
            {
                Text enemyText = poolEnemiesText;
                foreach (PoolAssignment assign in sources)
                {
                    classControls.Add(AddEnemyEntry(
                        customPanel, enemyText, true, assign.Pool, assign.Weight));
                }
            }
            AdjustPercentages();
            UpdateCustomPoolEnable();
            simultaneousUpdate = false;
        }

        private void SavePool()
        {
            // This happens when switching away from an old pool, as well as saving overall
            AdjustPercentages(preSave: true);
            if (currentClass is EnemyClass cl)
            {
                ClassConfig conf = ann.Classes.Find(c => c.Class == cl);
                ClassAssignment assign = new ClassAssignment();
                if (classNorandom.Visible && classNorandom.Checked)
                {
                    assign.NoRandom = true;
                }
                else if (classMerge.Visible && classMerge.Checked)
                {
                    assign.MergeParent = true;
                }
                else if (classMerge2.Visible && classMerge2.Checked)
                {
                    assign.MergeParent = true;
                    if (conf != null && conf.AltParent != null)
                    {
                        // If not present, this will fallback to main parent
                        assign.ManualParent = conf.AltParent.Where(c => c != conf.Parent).FirstOrDefault();
                    }
                }
                else if (classInherit.Visible && classInherit.Checked)
                {
                    assign.InheritParent = true;
                }
                else
                {
                    if (classRemoveSource != null)
                    {
                        assign.RemoveSource = GetEnemyLabelString(classRemoveSource);
                    }
                    foreach (PresetEnemyControl enemy in classControls)
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
                }
                if (classMultiplyBox.Visible)
                {
                    assign.EnemyMultiplier = multipliers[classMultiplyBox.SelectedIndex];
                }
                if (Preset.Classes == null) Preset.Classes = new Dictionary<EnemyClass, ClassAssignment>();
                Preset.Classes[cl] = assign;
            }
            else
            {
                // Adjust sources
                Preset.AdjustSource = new List<SourceAdjustment>();
                foreach (PresetEnemyControl enemy in classControls)
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
            }
        }

        private void AdjustPercentages(bool requireNoFocus = false, bool preSave = false, Control justFocused = null)
        {
            if (classControls.Count == 0) return;
            if (requireNoFocus)
            {
                Control foc = FindFocusedControl();
                if (foc != null && classControls.Any(e => foc == e.Percent))
                {
                    return;
                }
            }

            List<int> amts = new List<int>();
            int defaultVal = currentClass is EnemyClass ? 0 : 1000;
            int keepSameIndex = -1;
            foreach (PresetEnemyControl enemy in classControls)
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
            if (currentClass is EnemyClass)
            {
                AdjustClassPercentages(amts, keepSameIndex);
                for (int i = 0; i < classControls.Count; i++)
                {
                    PresetEnemyControl enemy = classControls[i];
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
            FlowLayoutPanel panel, Text label, bool isPool, string init, int? initVal)
        {
            List<string> opts = isPool ? enemyPoolOptions.ToList() : enemyOptions.ToList();
            PresetEnemyControl enemy = new PresetEnemyControl(
                label == null ? "" : messages.Get(label),
                opts,
                isPool || initVal is int,
                isPool);
            panel.SuspendLayout();
            panel.Controls.Add(enemy);
            if (enemy.Remove != null)
            {
                panel.Controls.Add(enemy.Remove);
                enemy.Remove.Click += (sender, e) =>
                {
                    if (editCustomPool)
                    {
                        RemoveEnemyEntry(panel, enemy);
                        classControls.Remove(enemy);
                        AdjustPercentages();
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
            panel.Controls.Add(enemy.Dropdown);
            panel.SetFlowBreak(enemy, true);
            enemy.Dropdown.SelectedIndexChanged += (sender, e) =>
            {
                AddEnemyLabel(panel, enemy);
                Modified = true;
            };
            UpdatePlusButton(panel);
            panel.ResumeLayout();
            if (!string.IsNullOrWhiteSpace(init))
            {
                foreach (string tag in PhraseRe.Split(init.Trim()))
                {
                    if (tag == "none") continue;
                    toDisplay.TryGetValue(tag, out string text);
                    text = text ?? tag;
                    if (opts.Contains(text))
                    {
                        AddEnemyLabel(panel, enemy, text);
                    }
                }
            }
            return enemy;
        }

        private void RemoveEnemyEntry(FlowLayoutPanel panel, PresetEnemyControl enemy)
        {
            panel.SuspendLayout();
            panel.Controls.Remove(enemy.Pre);
            panel.Controls.Remove(enemy.Dropdown);
            if (enemy.Remove != null) panel.Controls.Remove(enemy.Remove);
            if (enemy.Percent != null) panel.Controls.Remove(enemy.Percent);
            foreach (LinkLabel tag in enemy.Tags) panel.Controls.Remove(tag);
            panel.Controls.Remove(enemy);
            UpdatePlusButton(panel);
            panel.ResumeLayout();
        }

        private static string TagText(LinkLabel label) => label.Text.TrimEnd('⨉', ' ');
        private string GetEnemyLabelString(PresetEnemyControl enemy)
        {
            if (enemy == null) return null;
            List<string> tags = new List<string>();
            List<string> opts = (List<string>)enemy.Dropdown.DataSource;
            foreach (LinkLabel tagLabel in enemy.Tags)
            {
                string tag = TagText(tagLabel);
                if (string.IsNullOrWhiteSpace(tag) || !enemyPoolOptions.Contains(tag)) continue;
                toInternal.TryGetValue(tag, out string value);
                value = value ?? tag;
                tags.Add(value);
            }
            return tags.Count > 0 ? string.Join("; ", tags) : null;
        }

        private void AddEnemyLabel(FlowLayoutPanel panel, PresetEnemyControl enemy, string text = null)
        {
            if (text == null)
            {
                if (enemy.Dropdown.SelectedIndex == 0
                    || !(enemy.Dropdown.SelectedValue is string value))
                {
                    return;
                }
                // Assume this value is legitimate. It may be silently filtered during save if not
                if (string.IsNullOrWhiteSpace(value)) return;
                enemy.Dropdown.SelectedIndex = 0;
                enemy.Dropdown.Text = "";
                text = value;
            }
            int panelIndex = panel.Controls.IndexOf(enemy.Dropdown);
            if (panelIndex == -1) return;
            panelIndex++;
            while (panelIndex < panel.Controls.Count)
            {
                Control nextCon = panel.Controls[panelIndex];
                if (nextCon is LinkLabel existLabel)
                {
                    if (TagText(existLabel) == text) return;
                }
                else break;
                panelIndex++;
            }
            LinkLabel label = new LinkLabel();
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
            // Very functional, thanks
            panel.SuspendLayout();
            panel.Controls.Add(label);
            panel.Controls.SetChildIndex(label, panelIndex);
            enemy.Tags.Add(label);
            UpdatePlusButton(panel);
            panel.ResumeLayout();
        }

        private void RemoveEnemyLabel(FlowLayoutPanel panel, PresetEnemyControl enemy, LinkLabel label)
        {
            if (!panel.Controls.Contains(label)) return;
            enemy.Tags.Remove(label);
            panel.Controls.Remove(label);
            UpdatePlusButton(panel);
        }

        private void UpdatePlusButton(FlowLayoutPanel panel)
        {
            // Could also include an invisible element here, but then would have to move that too
            int index = panel.Controls.IndexOf(newPoolButton);
            if (index == -1) return;
            if (index != panel.Controls.Count - 1)
            {
                panel.Controls.SetChildIndex(newPoolButton, panel.Controls.Count - 1);
                index = panel.Controls.IndexOf(newPoolBreak);
                if (index != -1)
                {
                    panel.Controls.SetChildIndex(newPoolBreak, panel.Controls.Count - 2);
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

        [Localize]
        private static readonly Text confirmMultiplyRandomText = new Text("Disable enemy randomization with Enemy Onslaught?", "PresetEditForm_confirmMultiplyRandom");

        private void mode_Changed(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
            bool newMode = modeMultiply.Checked;
            if (newMode && !multiplyMode)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(confirmMultiplyRandomText), messages.Get(confirmTitleText), MessageBoxButtons.YesNo);
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

        private void multiplyBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Initializing the box, before the Preset is set, can cause this
            if (simultaneousUpdate || Preset == null) return;
            Modified = true;
            UpdateMultiplier(true);
        }

        private void UpdateMultiplier(bool setAll)
        {
            if (multiplyMode)
            {
                Preset.EnemyMultiplier = multipliers[multiplyBox.SelectedIndex];
                if (!classMultiplyBox.Visible || setAll)
                {
                    classMultiplyBox.SelectedIndex = multiplyBox.SelectedIndex;
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
                SavePool();
            }
            else
            {
                Preset.EnemyMultiplier = 0;
                classMultiplyBox.Visible = false;
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

        private void UpdateCustomPoolEnable()
        {
            editCustomPool = classCustom.Checked || currentClass == null || currentClass == EnemyClass.Spectator;
            if (classRemoveSource != null) classRemoveSource.Enabled = editCustomPool;
            foreach (PresetEnemyControl enemy in classControls)
            {
                enemy.Enabled = editCustomPool;
            }
            newPoolButton.Enabled = editCustomPool;
            UpdateMultiplierConfigurable();
        }

        private void UpdateMultiplierConfigurable()
        {
            // TODO: Make HostileNPC work with this, it's hardcoded in randomizer
            bool independentMultiply = currentClass != null && currentClass != EnemyClass.HostileNPC
                && !classMerge.Checked && !classMerge2.Checked && !classInherit.Checked;
            classMultiplyBox.Visible = multiplyMode && Preset.EnemyMultiplier > 0 && independentMultiply;
        }

        private void quickSeparate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SavePool();
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
            InitializeClass(currentClass);
        }

        private void quickMergeBoss_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SavePool();
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
            InitializeClass(currentClass);
        }

        private void quickMergeMinor_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SavePool();
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
            InitializeClass(currentClass);
        }

        private void quickMergeAll_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SavePool();
            foreach (KeyValuePair<EnemyClass, ClassAssignment> entry in Preset.Classes)
            {
                SetMerged(entry.Key, entry.Value);
            }
            InitializeClass(currentClass);
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
            SavePool();
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
                    if (Preset.AdjustSource == null) Preset.AdjustSource = new List<SourceAdjustment>();
                    Preset.AdjustSource.Add(new SourceAdjustment
                    {
                        Source = string.Join("; ", sources),
                        Keep = adj.Percent * 10,
                        Total = 1000,
                    });
                }
            }
            InitializeClass(currentClass);
        }

        private void quickReplace10_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SavePool();
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
                if (currentClass is EnemyClass current && current != cl)
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
            InitializeClass(currentClass);
        }

        private void quickReplaceWildlife_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (Preset.Classes == null) return;
            Modified = true;
            SavePool();
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
            InitializeClass(currentClass);
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
            prompt.Font = customPanel.Font;
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
