using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RandomizerCommon.Properties;
using YamlDotNet.Serialization;
using static RandomizerCommon.Messages;
using static SoulsIds.GameSpec;
using SoulsIds;
using GrayIris.Utilities.UI.Controls;
using System.Collections.Specialized;

namespace RandomizerCommon
{
    public partial class EldenForm : Form
    {
        [Localize]
        private static readonly Text enemyseedPlaceholderText = new Text(
            "(same as overall seed)", "EldenForm_enemyseedPlaceholder");
        private static string defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe";
        private static string unseenTabs = "item2,enemy,dlc,misc"; // "enemy,misc2";
        private static long defaultFlash = -10000;

        private Messages messages;
        private ModRunner Runner;
        private List<string> extraDlls = new();

        private RandomizerOptions options = new RandomizerOptions(FromGame.ER);
        private Stopwatch sw = new Stopwatch();
        private string defaultOpts = null;
        private Dictionary<string, string> originalLabels = new();
        private Dictionary<string, bool> prevCheckState = new();
        private Color fixedseedColor;
        private Color enemyseedColor;
        private long fixedseedFlash = defaultFlash;
        private long enemyseedFlash = defaultFlash;
        private bool hideseed;
        private bool simultaneousUpdate;
        private bool enemyseedPlaceholder;
        private bool working;
        private bool error;

        public EldenForm(bool dumpMessages = false, bool loadEmpty = false)
        {
            InitializeComponent();
            Text = $"Elden Ring Item and Enemy Randomizer {Randomizer.EldenVersion}";
            Runner = new ModRunner(
                FromGame.ER,
                "config_eldenringrandomizer.toml", @"diste\ModEngine\modengine2_launcher.exe", "config_eldenringrandomizer_dll.toml");
            // Set up tabs real quick
            tabControl.TabDrawer = new GrayIris.Utilities.UI.Controls.XlTabDrawer();
            tabControl.SelectedIndex = 0;
            if (RandomizerOptions.ReadViewedTabs() != unseenTabs)
            {
#if DEV_RELEASE
                item2Page.Blink = true;
                enemyPage.Blink = true;
                dlcPage.Blink = true;
                miscPage.Blink = true;
                tabControl.Blink = true;
                tabControl.BlinkColor = Color.FromArgb(255, 220, 230);
#endif
            }
            // And static dropdowns. Temporarily disable options tracking while doing so
            using (startUpdate())
            {
                int width = 2;
                string padNum(int i) => i.ToString().PadLeft(width);
                runes_end.DataSource = Enumerable.Range(0, 8).Select(padNum).ToList();
                runes_leyndell.DataSource = Enumerable.Range(0, 8).Select(padNum).ToList();
                runes_leyndell.SelectedIndex = 2;
                runes_rold.DataSource = new[] { " --" }.Concat(Enumerable.Range(0, 8).Select(padNum)).ToList();
                messmershard.DataSource = new[] { " --" }.Concat(Enumerable.Range(1, 20).Select(padNum)).ToList();
                shardreq.DataSource = new[] { " --" }.Concat(Enumerable.Range(0, 21).Select(padNum)).ToList();
                width = 4;
                playerlevel.DataSource = new[] { " --" }.Concat(new[] { 30, 60, 90, 120, 150, 200 }.Select(padNum)).ToList();
            }

            messages = new Messages("diste", loadEmpty);
            if (dumpMessages)
            {
#if DEV
                // Obsolete
                if (!LoadEnemyAnnotations(out string annErr) || !LoadItemAnnotations(out annErr) || !LoadPresetAnnotations(out annErr)) throw new Exception(annErr);
                EnemyPreset defaultPreset = EnemyPreset.MakeClassBasedDefault(enemyAnn);
                FormMessages.DumpEnglishMessages(
                    new List<Form>
                    {
                        this,
                        new OptionsForm(messages, ""),
                        new PresetEditForm(enemyAnn, messages, defaultPreset, false),
                        new PresetItemForm(itemAnn, presetAnn, messages, null, () => options),
                        new MergeModForm(messages, null),
                        new HelperForm(messages),
                        new DllForm(messages, extraDlls),
                        new SearchScrollForm(messages, "", ""),
                    },
                    new List<Type> {
                            typeof(EldenForm), typeof(OptionsForm), typeof(FormText),
                            typeof(Randomizer), typeof(MiscSetup), typeof(HelperForm), typeof(HelperOptions),
                            typeof(EnemyPreset), typeof(EnemyAnnotations),  typeof(PresetEditForm),
                            typeof(HintMarker), typeof(Permutation), typeof(PermutationWriter), typeof(CharacterWriter),
                            typeof(ItemPreset), typeof(PresetItemForm),
                    });
#endif
            }
            enemyseed.GotFocus += enemyseed_TextChanged;
            enemyseed.LostFocus += enemyseed_TextChanged;

            RandomizerOptions initialOpts = new RandomizerOptions(FromGame.ER);
            SetControlFlags(this, initialOpts);
            initialOpts.Difficulty = 20;
            defaultOpts = initialOpts.FullString();

            string existingOpts = RandomizerOptions.ReadOptions();
            if (string.IsNullOrWhiteSpace(existingOpts))
            {
                // This is fairly awkward
                using (startUpdate())
                {
                    difficulty.Value = options.Difficulty = initialOpts.Difficulty;
                }
                SetSeedVisibility(false);
                SetControlFlags(this);
            }
            else
            {
                SetOptionsString(existingOpts);
                if (options.Seed != 0)
                {
                    default_reroll.Enabled = true;
                    default_reroll.Checked = false;
                }
            }

            // Do this here as changing language can save this value
            extraDlls = RandomizerOptions.ReadExtraDlls() ?? new();

            // All the stuff forwards on can depend on localization
            SetCulture(null);

            string defaultExe = RandomizerOptions.ReadExe();
            if (!string.IsNullOrWhiteSpace(defaultExe))
            {
                exe.Text = defaultExe;
            }
            else if (File.Exists(defaultPath))
            {
                exe.Text = defaultPath;
            }
            string defaultMod = RandomizerOptions.ReadMod();
            if (!string.IsNullOrWhiteSpace(defaultMod))
            {
                mergemod.Text = defaultMod;
            }
            defaultRerollEnemy.Checked = default_reroll.Checked && options.Seed2 != 0;

            // Now, update everything in the UI in-place
            SetStatus(null);
            if (!error)
            {
                SetWarning();
            }
            UpdateEnabled();
            UpdateLabels();
            UpdateRandomizeButtonText();
            UpdateExtraDlls();
            // https://github.com/google/material-design-icons/blob/master/LICENSE
            // SetSeedVisibility(options["hideseed"]);

            if (messages.InitErrors.Count > 0)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, messages.InitErrors),
                    "Invalid messages", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // For some horrific godforsaken reason, the \r is stripped from TextBox
            // when initialized while it's not visible, probably?
            // ?????????????????????????????????????????????????????????????????????????
            presetL.VisibleChanged += (sender, e) =>
            {
                presetL.Text = Regex.Replace(presetL.Text, "\r?\n", "\r\n");
            };
            presetItemL.VisibleChanged += (sender, e) =>
            {
                presetItemL.Text = Regex.Replace(presetItemL.Text, "\r?\n", "\r\n");
            };

            Runner.StartRunning += (sender, e) =>
            {
                launchButton.BackColor = Color.LightYellow;
            };
            Runner.FailedToStart += (sender, e) =>
            {
                ScrollDialog.Show(this, messages.Get(launchFailed), messages.Get(launchFailedTitle));
            };
            Runner.DoneRunning += (sender, e) =>
            {
                launchButton.BackColor = SystemColors.Control;
                launchButton.UseVisualStyleBackColor = true;
            };
            FlashLoop();
        }

        private void EldenForm_Load(object sender, EventArgs e)
        {
#if DEBUG
            if (PresetEditForm.DebugOpen)
            {
                tabControl.SelectedIndex = 2;
                presetButton.PerformClick();
            }
            else if (PresetItemForm.DebugOpen)
            {
                presetItemButton.PerformClick();
            }
            else if (HelperForm.DebugOpen)
            {
                HelperForm.Show(this, messages);
            }
#endif
        }

        private static readonly Dictionary<string, int?> defaultDropdownValues = new Dictionary<string, int?>
        {
            ["runes_end"] = 0,
            ["runes_leyndell"] = 2,
            // Are these needed?
            ["runes_rold"] = null,
            ["playerlevel"] = null,
            ["messmershard"] = null,
            ["shardreq"] = null,
        };

        private bool SetDropdownValue(ComboBox box, int? value)
        {
            if (box.Items.Count == 0) return false;
            if (value == null && defaultDropdownValues.TryGetValue(box.Name, out int? defaultValue))
            {
                value = defaultValue;
            }
            int index = ((List<string>)box.DataSource).FindIndex(s => int.TryParse(s.Trim(), out int val) ? val == value : value == null);
            // If it didn't work for some reason (like invalid range), try set default value
            if (index == -1 && defaultDropdownValues.TryGetValue(box.Name, out defaultValue))
            {
                value = defaultValue;
                index = ((List<string>)box.DataSource).FindIndex(s => int.TryParse(s.Trim(), out int val) ? val == value : value == null);
                box.SelectedIndex = index == -1 ? 0 : index;
                return false;
            }
            box.SelectedIndex = index;
            return true;
        }

        private int? GetDropdownValue(ComboBox box)
        {
            // Returns dropdown value, and null if default or non-integer value
            defaultDropdownValues.TryGetValue(box.Name, out int? defaultValue);
            if (int.TryParse(box.SelectedValue?.ToString(), out int val) && val != defaultValue)
            {
                return val;
            }
            return null;
        }

        private void SetOptionsString(string defaultOpts)
        {
            HashSet<string> validOptions = new HashSet<string>();
            GetAllControlNames(this, validOptions);
            bool isValidOption(string s)
            {
                if (validOptions.Contains(s)) return true;
                if (uint.TryParse(s, out _)) return true;
                return false;
            }
            List<string> previousOpts = defaultOpts.Split(' ').ToList();
            options = RandomizerOptions.Parse(previousOpts, FromGame.ER, isValidOption);

            // New defaults. Just in case no version, choose not to do anything.
            int prevVersion = RandomizerOptions.EldenRingVersion;
            for (int i = 1; i <= RandomizerOptions.EldenRingVersion; i++)
            {
                if (previousOpts.Contains($"v{i}")) prevVersion = i;
            }
            if (prevVersion < 3)
            {
                options["weaponprogression"] = true;
            }
            if (prevVersion < 4)
            {
                options["enemy"] = true;
                options["scale"] = true;
                options["editnames"] = true;
                // options["regularhp"] = true;
                // options["bosshp"] = true;
            }
            if (prevVersion < 7)
            {
                options["phasehp"] = true;
            }
            if (prevVersion < 9)
            {
                options["bossbgm"] = true;
            }
            if (prevVersion < 10)
            {
                options["swaprewards"] = false;
                if (options["nohand"]) options["changestats"] = false;
            }
            if (prevVersion < 12)
            {
                options["dlc"] = true;
                if (!options["norandom"])
                {
                    options["racemode_key"] = true;
                }
                options["racemode_scadu"] = true;
                options["raceloc_scadu"] = true;
                options["dlcblessing"] = true;
            }
            if (prevVersion < 13)
            {
                options["dlckeysilo"] = true;
            }
            if (prevVersion < 14)
            {
                options["crashfix"] = true;
            }

            using (startUpdate())
            {
                InsertControlFlags(this);
                // Handle non-default radio buttons without options, otherwise "To anywhere" is forgotten.
                // This is best done here, as UpdateEnabled should default to "Important locations".
                if (!options["racemode_key"] && !options["norandom"])
                {
                    default_key_any.Checked = true;
                }
                if (!options["racemode_upgrades"] && !options["norandom_upgrades"])
                {
                    default_upgrades.Checked = true;
                }
                if (!options["racemode_health"] && !options["norandom_health"])
                {
                    default_health.Checked = true;
                }
                if (!options["racemode_scadu"] && !options["norandom_scadu"])
                {
                    default_health.Checked = true;
                }
                difficulty.Value = options.Difficulty;
            }

            fixedseed.Text = options.Seed == 0 ? "" : $"{options.Seed}";
            ManualSetEnemySeed(options.Seed2);
            if (options.Seed2 != 0)
            {
                defaultRerollEnemy.Checked = false;
            }

            if (options.Preset == null)
            {
                SetPreset(null, false);
            }
            else
            {
                try
                {
                    EnemyPreset preset = EnemyPreset.LoadPreset(options.Preset, extractOopsAll: false);
                    SetPreset(preset, false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    SetPreset(null, false);
                }
            }
            // customitem checkbox is already set via control flag, so SetItemPreset can now change its state
            if (!options["customitem"])
            {
                SetItemPreset(null);
            }
            else
            {
                try
                {
                    ItemPreset itemPreset = ItemPreset.LoadPreset();
                    SetItemPreset(itemPreset);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Ignoring load error: " + e);
                    SetItemPreset(null);
                }
            }
            prevCheckState.Clear();
        }

        private void UpdateAllOptions()
        {
            if (options.Seed != 0) default_reroll.Checked = false;
            SetStatus(null);
            UpdateEnabled();
            UpdateExtraDlls();
            UpdateLabels();
            SaveOptions();
        }

        private void SetWarning()
        {
            bool fatal = !MiscSetup.CheckRequiredEldenFiles(messages, out string err)
                || MiscSetup.CheckEldenRingMods(messages, options["uxm"], exe.Text, out err);
            SetError(err, fatal);
        }

        [Localize]
        private static readonly Text overviewText = new Text(
            "Check the mod page or README for a full list of all key items, important locations, and other details. Find hints and spoilers for item locations in the spoiler_logs directory.",
            "EldenForm_overviewL");

        private void SetError(string text = null, bool fatal = false)
        {
            warningL.Text = text ?? messages.Get(overviewText);
            warningL.Visible = true;
            warningL.ForeColor = text == null ? SystemColors.ControlText : Color.DarkRed;
#if DEBUG
            // fatal = false;
#endif
            // Previousy this was a one-way transition to error, but this is affected by options now (uxm output)
            if (fatal) randomize.Enabled = false;
            error = fatal;
        }

        [Localize]
        private static readonly Text status = new Text(
            "{0} Current config hash: {1}",
            "EldenForm_status");
        [Localize]
        private static readonly Text credits = new Text(
            "Created by {0}.",
            "EldenForm_status_credits");
        [Localize]
        private static readonly Text creditsWithTranslators = new Text(
            "Created by {0}, translated by {1}.",
            "EldenForm_status_creditsWithTranslators");



        private void SetStatus(string msg, bool error = false, bool success = false)
        {
            if (msg == null)
            {
                string cred;
                if (string.IsNullOrWhiteSpace(translators))
                {
                    cred = messages.Get(credits, "thefifthmatt");
                }
                else
                {
                    cred = messages.Get(creditsWithTranslators, "thefifthmatt", translators);
                }
                msg = messages.Get(status, cred, options.ConfigHash());
            }
            statusL.Text = msg;
            statusStrip1.BackColor = error ? Color.IndianRed : (success ? Color.PaleGreen : SystemColors.Control);
        }

        private Dictionary<string, string> originalText;
        private Dictionary<Control, (int, int)> originalHeightY = new Dictionary<Control, (int, int)>();
        private HashSet<Control> noResize;
        private CultureInfo defaultCulture;
        private string translators;

        private void SetCulture(CultureInfo culture)
        {
            // Initialize state
            if (originalText == null)
            {
                originalText = FormMessages.GetFormText(this);
            }
            if (defaultCulture == null)
            {
                string existingCulture = RandomizerOptions.ReadLocale();
                if (!string.IsNullOrWhiteSpace(existingCulture))
                {
                    try
                    {
                        defaultCulture = CultureInfo.GetCultureInfo(existingCulture, true);
                    }
                    catch
                    {
                    }
                }
                if (defaultCulture == null)
                {
                    defaultCulture = Thread.CurrentThread.CurrentCulture;
                }
            }
            if (language.DataSource == null)
            {
                List<string> languages = new List<string> { "English" };
                SortedSet<string> extraLanguages = new SortedSet<string>();
                foreach (LanguageMessages lang in messages.AllMessages.Values)
                {
                    extraLanguages.Add(lang.Language);
                }
                languages.AddRange(extraLanguages);
                language.DataSource = languages;
            }
            if (culture == null)
            {
                // Try to infer the language
                LanguageMessages defaultLang = messages.GetAllLanguageMessages(defaultCulture).FirstOrDefault();
                List<string> displayLangs = (List<string>)language.DataSource;
                int langIndex = displayLangs.IndexOf(defaultLang?.Language);
                // Mutual structured recursion, don't fail me now
                language.SelectedIndex = langIndex == -1 ? 0 : langIndex;
                return;
            }
            // Swap out the entire UI. UpdateLabels/SetStatus also sets strings and should be called afterwards
            Thread.CurrentThread.CurrentCulture = culture;
            string code = culture.TwoLetterISOLanguageName;
            List<LanguageMessages> localLangs = messages.GetAllLanguageMessages(culture);
            translators = localLangs.Count > 0 ? localLangs[0].Translators : null;
            originalLabels.Clear();

            RandomizerOptions.SaveLocale(culture.ToString());

            // Restore original text positions and save original autosizes
            HashSet<Control> noAuto = new HashSet<Control>();
            Dictionary<Control, Control> manualAboveGroups = new Dictionary<Control, Control>
            {
                [presetButton] = customGroup,
                [enemyWarningL] = customGroup,
            };
            void preprocessForm(Control control)
            {
                if (originalHeightY.TryGetValue(control, out var original))
                {
                    (int height, int y) = original;
                    control.Height = height;
                    control.Location = new Point(control.Location.X, y);
                }
                if (noResize == null && !control.AutoSize)
                {
                    noAuto.Add(control);
                }
                foreach (Control sub in control.Controls.Cast<Control>().OrderBy(c => c.Location.Y))
                {
                    preprocessForm(sub);
                }
            }
            preprocessForm(this);
            noResize = noResize ?? noAuto;
            // Update text
            FormMessages.SetFormText(messages, this, originalText);
            // Also update our fake placeholder text
            if (enemyseedPlaceholder && enemyseed.ForeColor == SystemColors.GrayText)
            {
                enemyseed.Text = hideseed ? "" : messages.Get(enemyseedPlaceholderText);
            }
            UpdateRandomizeButtonText();
            UpdateEnemyPresetText();
            if (presetAnn != null && selectedItemPreset != null)
            {
                presetItemL.Text = selectedItemPreset.FullDescription(messages, presetAnn.GetItemName);
            }

            // With text/font updated, fiddle with position of tab pages
            string prefix = "    ";
            void repositionCheckbox(Control checkbox, int index)
            {
                Control tabPage = tabControl.Controls[index];
                tabPage.Text = prefix + tabPage.Text.Trim();
                Point pos = tabControl.Location;
                Rectangle tabRect = tabControl.GetTabRect(index);
                // Point is a struct, so it's fine to modify (but why no immutable method?!)
                pos.Offset(tabRect.Location);
                pos.Offset(10, tabRect.Height / 2 - 6);
                checkbox.Location = pos;
            }
            repositionCheckbox(item, 0);
            repositionCheckbox(enemy, 2);
            repositionCheckbox(dlc, 3);

            // See if any text doesn't fit, fairly hacky system
            void storeOriginal(Control control)
            {
                if (!originalHeightY.ContainsKey(control))
                {
                    originalHeightY[control] = (control.Height, control.Location.Y);
                }
            }
            Dictionary<Control, int> groupOffsets = new Dictionary<Control, int>();
            void expandForm(Control control, Control group = null)
            {
                // TODO: There's no vertical space here, try to add some in the future
                if (control == locationGroup) return;
                if (manualAboveGroups.TryGetValue(control, out Control aboveGroup))
                {
                    group = aboveGroup;
                }
                if (group != null)
                {
                    if (groupOffsets.TryGetValue(group, out int offset))
                    {
                        storeOriginal(control);
                        control.Location = new Point(control.Location.X, control.Location.Y + offset);
                    }
                    if (!noResize.Contains(control) && !string.IsNullOrWhiteSpace(control.Text))
                    {
                        Size size = TextRenderer.MeasureText(control.Text, control.Font);
                        // Horizontal offset of text after checkboxes
                        int available = group.Width - 22;
                        if (size.Width > available)
                        {
                            // Console.WriteLine($"{size.Width},{size.Height} > {available}: {control.Name}");
                            storeOriginal(control);
                            control.AutoSize = false;
                            if (control is CheckBox check)
                            {
                                check.CheckAlign = ContentAlignment.TopLeft;
                                check.TextAlign = ContentAlignment.TopLeft;
                            }
                            else if (control is Label label)
                            {
                                label.TextAlign = ContentAlignment.TopLeft;
                            }
                            control.Width = available;
                            int extraHeight = size.Height + (code == "ru" ? 0 : 2);
                            control.Height += extraHeight;
                            groupOffsets[group] = offset + extraHeight;
                        }
                    }
                }
                foreach (Control sub in control.Controls.Cast<Control>().OrderBy(c => c.Location.Y))
                {
                    expandForm(sub, control is GroupBox ? control : group);
                }
                if (groupOffsets.TryGetValue(control, out int groupOffset))
                {
                    storeOriginal(control);
                    control.Height += groupOffset;
                }
            }
            // Finally, top-level options text
            string tabPrefix = "    ";

            expandForm(this);
        }

        private void language_SelectedIndexChanged(object sender, EventArgs e)
        {
            List<string> displayLangs = (List<string>)language.DataSource;
            string displayLang = displayLangs[Math.Max(0, language.SelectedIndex)];
            LanguageMessages lang = messages.AllMessages.Values.ToList().Find(l => l.Language == displayLang);
            SetCulture(lang?.Culture ?? CultureInfo.InvariantCulture);
            SetStatus(null);
            SetWarning();
            UpdateLabels();
            UpdateExtraDlls();
        }

        private void tabControl_TabChanged(object sender, EventArgs e)
        {
            tabControl.SelectedTab.Blink = false;
            if (tabControl.Blink && !item2Page.Blink && !enemyPage.Blink && !dlcPage.Blink && !miscPage.Blink)
            {
                tabControl.Blink = false;
                if (RandomizerOptions.ReadViewedTabs() != unseenTabs)
                {
                    RandomizerOptions.SaveViewedTabs(unseenTabs);
                }
            }
        }

        private void SaveOptions()
        {
            RandomizerOptions.SaveOptions(options);
        }

        private void difficulty_Scroll(object sender, EventArgs e)
        {
            options.Difficulty = difficulty.Value;
            UpdateLabels();
            SetStatus(null);
            SaveOptions();
        }

        private void option_CheckedChanged(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            SetControlFlags(this);
            UpdateEnabled(clickOption: sender is Control c ? c.Name : null);
            UpdateLabels();
            UpdateRandomizeButtonText();
            SaveOptions();
            SetStatus(null);
            SetWarning();
        }

        private void option_AlwaysEnable(object sender, EventArgs e)
        {
            // For always-on racemode categories
            CheckBox box = (CheckBox)sender;
            box.Checked = true;
        }

        private void SetControlFlags(Control control, RandomizerOptions customOpt = null)
        {
            RandomizerOptions getOpt = customOpt ?? options;
            if (control == this) getOpt["hideseed"] = hideseed;
            if (control is RadioButton radio)
            {
                getOpt[control.Name] = radio.Checked;
            }
            else if (control is CheckBox check)
            {
                getOpt[control.Name] = check.Checked;
            }
            else if (control is ComboBox box)
            {
                getOpt.SetInt(control.Name, GetDropdownValue(box));
            }
            else
            {
                foreach (Control sub in control.Controls)
                {
                    SetControlFlags(sub, customOpt);
                }
            }
        }

        private void InsertControlFlags(Control control)
        {
            if (control == this)
            {
                // Don't turn this off when inserting, possibly turn it on
                options["hideseed"] = options["hideseed"] || hideseed;
                SetSeedVisibility(options["hideseed"]);
            }
            if (control.Name.Contains("default") || control.Name.EndsWith("_copy")) return;
            if (control is RadioButton radio)
            {
                // InsertControlFlags should be followed by calling UpdateEnabled.
                // This state change is required to avoid detecting an off->on transition.
                control.Enabled = true;
                radio.Checked = options[control.Name];
            }
            else if (control is CheckBox check)
            {
                control.Enabled = true;
                check.Checked = options[control.Name];
            }
            else if (control is ComboBox box)
            {
                control.Enabled = true;
                int? value = options.GetInt(control.Name, out int val) ? val : null;
                if (!SetDropdownValue(box, value))
                {
                    options.SetInt(control.Name, null);
                }
            }
            else
            {
                foreach (Control sub in control.Controls)
                {
                    InsertControlFlags(sub);
                }
            }
        }

        private void GetAllControlNames(Control control, HashSet<string> names)
        {
            if (control == this) names.Add("hideseed");
            if (control.Name.Contains("default") || control.Name.EndsWith("_copy")) return;
            if (control is RadioButton || control is CheckBox)
            {
                if (control.Name.StartsWith("invert"))
                {
                    // Same logic as in RandomizerOptions
                    names.Add("no" + control.Name.Substring(6));
                }
                else
                {
                    names.Add(control.Name);
                }
            }
            else
            {
                foreach (Control sub in control.Controls)
                {
                    GetAllControlNames(sub, names);
                }
            }
        }

        private void MassEnable(Dictionary<Control, bool> toEnable, Control control, string enableName, bool enable, ICollection<string> filter)
        {
            if (control.Name == enableName) return;
            bool filtered = filter == null || filter.Contains(control.Name);
            if (control is RadioButton || control is CheckBox || control is TrackBar || control is Label)
            {
                if (filtered) toEnable[control] = enable;
            }
            else
            {
                if (filtered && control is not YaTabPage) toEnable[control] = enable;
                foreach (Control sub in control.Controls)
                {
                    MassEnable(toEnable, sub, enableName, enable, filtered ? null : filter);
                }
            }
        }

        private class UpdateScope : IDisposable
        {
            private readonly bool prevState;
            private readonly Action<bool> setState;
            public UpdateScope(bool currentState, Action<bool> setState)
            {
                prevState = currentState;
                setState(true);
                this.setState = setState;
            }
            public void Dispose()
            {
                setState(prevState);
            }
        }

        private UpdateScope startUpdate()
        {
            return new UpdateScope(simultaneousUpdate, val => simultaneousUpdate = val);
        }

        private void UpdateEnabled(string clickOption = null)
        {
            using var update = startUpdate();
            bool changes = false;
            Dictionary<Control, bool> toEnable = new Dictionary<Control, bool>();
            // Mass enables, where settings updates are not involved
            MassEnable(toEnable, this, "item", options["item"], new[] { "itemPage", "item2Page" });
            MassEnable(toEnable, this, "customitem", options["item"] && !options["customitem"], new[] { "locationGroup", "keyGroup", "healthGroup", "upgradeGroup", "scaduGroup" });
            MassEnable(toEnable, this, "enemy", options["enemy"], new[] { "enemyPage" });
            MassEnable(toEnable, this, "dlc", options["dlc"], new[] { "dlcPage" });
            // Also change tab colors while we're here
            bool tabColorChange = false;
            void setTabEnable(YaTabPage tabPage, bool enable)
            {
                bool active = tabPage.TabForeColor == SystemColors.ControlText;
                if (active != enable)
                {
                    tabPage.TabForeColor = enable ? SystemColors.ControlText : SystemColors.GrayText;
                    tabColorChange = true;
                }
            }
            setTabEnable(itemPage, options["item"]);
            setTabEnable(item2Page, options["item"]);
            setTabEnable(enemyPage, options["enemy"]);
            setTabEnable(dlcPage, options["dlc"]);
            if (tabColorChange)
            {
                // Force update through setting other property
                tabControl.ActiveColor = tabControl.ActiveColor;
            }
            // Individual updates
            // TODO: Move this to a shared file at some point!
            void setSimpleEnable(Control control, bool enabled, string overrideDisable)
            {
                if (overrideDisable == null || options[overrideDisable])
                {
                    toEnable[control] = enabled;
                }
            }
            void setComboBox(ComboBox control, bool enabled, string maybeDefaultState, string disabledState, string overrideDisable)
            {
                bool prevEnabled = control.Enabled;
                setSimpleEnable(control, enabled, overrideDisable);
                if (!enabled && prevEnabled)
                {
                    int index = ((List<string>)control.DataSource).FindIndex(s => s.Trim() == disabledState);
                    if (index != -1)
                    {
                        control.SelectedIndex = index;
                        changes = true;
                    }
                }
                else if (maybeDefaultState is string defaultState && !string.IsNullOrEmpty(defaultState) && enabled && !prevEnabled)
                {
                    int index = ((List<string>)control.DataSource).FindIndex(s => s.Trim() == defaultState);
                    if (index != -1)
                    {
                        control.SelectedIndex = index;
                        changes = true;
                    }
                }
            }
            void setCheck(Control control, bool enabled, bool? maybeDefaultState, bool disabledState, string overrideDisable)
            {
                bool prevEnabled = control.Enabled;
                setSimpleEnable(control, enabled, overrideDisable);
                // Generalizing across control types, was it worth it :')
                CheckBox check = control as CheckBox;
                RadioButton radio = control as RadioButton;
                bool prevChecked = check != null ? check.Checked : radio.Checked;
                if (!enabled && prevEnabled)
                {
                    // Heuristics to try to restore previous state. Require manual click, so this doesn't happen on init
                    if (clickOption != null)
                    {
                        prevCheckState[control.Name] = prevChecked;
                    }
                    if (prevChecked != disabledState)
                    {
                        if (check != null) check.Checked = disabledState;
                        else radio.Checked = disabledState;
                        changes = true;
                        // Needed for recursive options alongside prevCheckState
                        // If Z needs Y needs X, and X is disabled and Y and Z were previously on,
                        // options["Y"] needs to be true for Z to turn on when X and Y turn on.
                        options[control.Name] = disabledState;
                    }
                }
                else if (enabled && !prevEnabled)
                {
                    bool enableState;
                    if (prevCheckState.TryGetValue(control.Name, out bool prevEnabledState)) enableState = prevEnabledState;
                    else if (maybeDefaultState is bool defaultState) enableState = defaultState;
                    else return;
                    if (prevChecked != enableState)
                    {
                        if (check != null) check.Checked = enableState;
                        else radio.Checked = enableState;
                        changes = true;
                        options[control.Name] = enableState;
                    }
                }
            };

            // As of 0.6, more combination of options should work, with some racemode location heuristics in AnnotationData.
            // setCheck(night, options["raceloc_altboss"], false, false, "item");
            // No key item options when key items are not randomized
            setSimpleEnable(raceloc_scadu, options["dlc"], "item");
            setSimpleEnable(racemode_scadu, options["dlc"], "item");
            setSimpleEnable(norandom_scadu, options["dlc"], "item");
            setCheck(default_scadu, options["dlc"], false, false, "item");
            bool allowNorandom = selectedItemPreset == null;
            setSimpleEnable(presetItemButton, selectedItemPreset != null, "item");
            // Ignore norandom when preset enabled. Ideally this stays updated as preset is edited
            bool allowLogic = !options["norandom"] || selectedItemPreset != null;
            setComboBox(runes_leyndell, allowLogic, null, "2", "item");
            setComboBox(runes_rold, allowLogic, null, "--", "item");
            setCheck(markitems, options["markareas"], false, false, "item");
            setCheck(earlylegacy, allowLogic, true, true, "item");
            setCheck(earlymedal, allowLogic, true, true, "item");
            setCheck(gaolkeys, allowLogic && options["dlc"], false, false, "item");
            setComboBox(messmershard, allowLogic && options["dlc"], "--", "--", "item");
            setComboBox(shardreq, allowLogic && options["dlc"] && options.GetInt("messmershard", out _), "--", "--", "item");
            // This logic is so bad
            bool dlcItemSilo = options["dlcsilo"] || (selectedItemPreset != null && selectedItemPreset.OverallSilo == LocationData.AreaSiloType.DLC);
            setCheck(dlckeysilo, options["dlc"] && !options["dlcstart"] && !dlcItemSilo, true, true, "item");
            // Scaling options
            // No longer includes customization options
            setCheck(phasehp, options["scale"], true, false, "enemy");
            setSimpleEnable(presetButton, selectedPreset != null, "enemy");
            // DLC start has a ton
            bool spstart = options["dlcstart"] || options["quickstart"];
            setCheck(dlcshop, spstart, false, false, "dlc");
            setCheck(dlcpackage, spstart, false, false, "dlc");
            setCheck(packagelite, spstart && options["dlcpackage"], false, false, "dlc");
            setCheck(dlcroundtable, spstart, false, false, "dlc");
            setCheck(maxflask, spstart, false, false, "dlc");
            setCheck(maxflasknum, spstart, false, false, "dlc");
            setCheck(maxpouch, spstart, false, false, "dlc");
            setCheck(maxmemslot, spstart, false, false, "dlc");
            setCheck(maxwhetblade, spstart, false, false, "dlc");
            setCheck(maxbb, spstart, false, false, "dlc");
            setComboBox(playerlevel, spstart, null, "--", "dlc");
            // Misc
            setCheck(default_twohand, !options["nostarting"], true, true, null);
            setCheck(onehand, !options["nostarting"], false, false, null);
            setCheck(nohand, !options["nostarting"], false, false, null);
            setCheck(changestats, !options["nostarting"] && !options["nohand"], true, false, null);
            setCheck(dlconlyhand, options["dlc"] && (!options["nostarting"] || !options["nogifts"]), null, false, null);
            setCheck(crawl, options["fog"], false, false, null);
            setSimpleEnable(helperButton, options["helper"], null);
            bool dllChanged = false;
            void checkDll(string opt, string dllPath)
            {
                string dllName = Path.GetFileName(dllPath);
                bool dllUsed = extraDlls.Any(path => Path.GetFileName(path).Equals(dllName, StringComparison.OrdinalIgnoreCase));
                if (options[opt] != dllUsed)
                {
                    if (options[opt])
                    {
                        extraDlls.Add(Path.GetFullPath(dllPath));
                    }
                    else
                    {
                        extraDlls.RemoveAll(path => Path.GetFileName(path).Equals(dllName, StringComparison.OrdinalIgnoreCase));
                    }
                    dllChanged = true;
                }
            }
            checkDll("helper", HelperForm.DllPath);
            checkDll("crashfix", HelperForm.FixDllPath);
            if (dllChanged)
            {
                UpdateExtraDlls();
            }

            // Universal options not in the misc tab
            toEnable[language] = true;
            toEnable[languageL] = true;
            toEnable[runes_end] = true;
            toEnable[runes_endL] = true;
            // Otherwise runes doesn't work now for some reason
            toEnable[progressGroup] = true;

            // Make sure a single radio button of any group is checked
            if (!racemode_upgrades.Checked && !norandom_upgrades.Checked && !default_upgrades.Checked)
            {
                racemode_upgrades.Checked = true;
                changes = true;
            }
            if (!racemode_health.Checked && !norandom_health.Checked && !default_health.Checked)
            {
                racemode_health.Checked = true;
                changes = true;
            }
            if (!racemode_key.Checked && !norandom.Checked && !default_key_any.Checked)
            {
                racemode_key.Checked = true;
                changes = true;
            }
            if (!racemode_scadu.Checked && !norandom_scadu.Checked && !default_scadu.Checked)
            {
                racemode_scadu.Checked = true;
                changes = true;
            }
            if (!dlcsilo.Checked && !dlcsilo_default.Checked)
            {
                dlcsilo_default.Checked = true;
                changes = true;
            }
            if (!dlcstart_default.Checked && !dlcstart.Checked && !quickstart.Checked)
            {
                dlcstart_default.Checked = true;
                changes = true;
            }
            if (!default_twohand.Checked && !onehand.Checked && !nohand.Checked)
            {
                default_twohand.Checked = true;
                changes = true;
            }
            foreach (KeyValuePair<Control, bool> enable in toEnable)
            {
                // Console.WriteLine($"{enable.Key.Name} -> {enable.Value}");
                enable.Key.Enabled = enable.Value;
            }
            enemyseed_TextChanged(null, null);
            randomize.Enabled = !error; // (options["enemy"] || options["item"]) && !error;
            if (changes) SetControlFlags(this);
            UpdateLaunchGame();
            if (selectedItemPreset != null)
            {
                ItemPreset changePreset = selectedItemPreset.UpdateFromOptions(options, itemAnn.DefaultPreset, false, clickOption);
                // if (clickOption != null) Console.WriteLine($"For {clickOption} got {changePreset}");
                if (changePreset != null)
                {
                    // Version of SetItemPreset which changes minimal state
                    ReplaceItemPreset(changePreset);
                }
            }
            // Similar thing for enemy preset, but don't actually change the preset contents
            if (clickOption == "dlcsilo")
            {
                UpdateEnemyPresetText();
            }
        }

        private void UpdateLaunchGame()
        {
            bool valid = Runner.IsValid() && !options["uxm"];
            launchButton.Enabled = valid;
            zipModFilesToolStripMenuItem.Enabled = valid;
        }

        [Localize]
        private static readonly Text allitem0 = new Text(
            "All eligible locations for items are equally likely.",
            "EldenForm_allitem0");
        [Localize]
        private static readonly Text allitem1 = new Text(
            "Most eligible locations for items are equally likely.",
            "EldenForm_allitem1");
        [Localize]
        private static readonly Text allitem2 = new Text(
            "Better rewards for difficult and late locations.",
            "EldenForm_allitem2");
        [Localize]
        private static readonly Text allitem3 = new Text(
            "Much better rewards for difficult and late locations.",
            "EldenForm_allitem3");
        [Localize]
        private static readonly Text keyitem0 = new Text(
            "Key items may be in locations that don't require completing much of the game.",
            "EldenForm_keyitem0");
        [Localize]
        private static readonly Text keyitem1 = new Text(
            "Key items will usually be in different areas and depend on each other.",
            "EldenForm_keyitem1");
        [Localize]
        private static readonly Text keyitem2 = new Text(
            "Key items will usually be in different areas and form interesting chains.",
            "EldenForm_keyitem2");
        [Localize]
        private static readonly Text keyitem3 = new Text(
            "Key items will usually form long chains across different areas.",
            "EldenForm_keyitem3");
        [Localize]
        private static readonly Text checkAmount = new Text(
            "{0} checks",
            "EldenForm_checkAmount");
        [Localize]
        private static readonly Text importantExcluded = new Text(
            "Excluded by current options:",
            "EldenForm_checkImportantExcluded");

        private void UpdateLabels()
        {
            // Fairness is not used in Elden Ring (yet)
            // if (options.GetNum("veryunfairweight") > 0.5) unfairText = " and very unfair";
            // else if (options.GetNum("unfairweight") > 0.5) unfairText = " and unfair";
            Text loc;
            RandomizerOptions.BiasWeights weights = options.GetWeights();
            if (weights.SpreadRatio > 0.7) loc = allitem3;
            else if (weights.SpreadRatio > 0.3) loc = allitem2;
            else if (weights.SpreadRatio > 0.001) loc = allitem1;
            else loc = allitem0;
            Text chain;
            if (weights.KeyItemChain <= 1.1f) chain = keyitem0;
            else if (weights.KeyItemChain <= 4.001) chain = keyitem1;
            else if (weights.KeyItemChain <= 10) chain = keyitem2;
            else chain = keyitem3;
            if (options["norandom"]) chain = null;
            difficultyL.Text = $"{messages.Get(loc)}\r\n{messages.Get(chain)}";
            difficultyAmtL.Text = $"{options.Difficulty}%";

            void updateRec(Control control)
            {
                if (control is LinkLabel checkL && AnnotationData.EldenTagCounts.TryGetValue(checkL.Name, out int[] counts))
                {
                    if (!originalLabels.TryGetValue(control.Name, out string originalText))
                    {
                        originalLabels[control.Name] = originalText = control.Text;
                    }
                    // // none; dlc; minidungeon; dlc,minidungeon
                    bool[] values = new[] { true, options["dlc"], !options["nocaves"], options["dlc"] && !options["nocaves"] };
                    int count = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (values[i]) count += counts[i];
                    }
                    string checkStr = messages.Get(checkAmount, count);
                    // label.LinkArea = new LinkArea(0, checkStr.Length);
                    int replaceIndex = originalText.IndexOf("{0}");
                    if (replaceIndex >= 0)
                    {
                        checkL.Text = originalText.Replace("{0}", checkStr);
                        if (checkL.Links.Count == 0) checkL.Links.Add(replaceIndex, checkStr.Length);
                        // Hopefully this doesn't throw and is a quicker update
                        checkL.Links[0].Start = replaceIndex;
                        checkL.Links[0].Length = checkStr.Length;
                    }
                }
                else if (control.Name == "dlcpackageL")
                {
                    if (!originalLabels.TryGetValue(control.Name, out string originalText))
                    {
                        originalLabels[control.Name] = originalText = control.Text;
                    }
                    control.Text = originalText.Replace("{0}", options["packagelite"] ? "50" : "80");
                }
                else
                {
                    foreach (Control sub in control.Controls)
                    {
                        updateRec(sub);
                    }
                }
            }
            updateRec(this);
        }

        private void raceloc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is not LinkLabel label) return;
            if (!LoadPresetAnnotations(out string err))
            {
                MessageBox.Show(err, messages.Get(presetFileError), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            List<string> requireTags = AnnotationData.EldenTagOpts.Where(e => e.Value == label.Name).Select(e => e.Key).ToList();
            if (requireTags.Count == 0) return;
            List<string> excludeTags = new();
            if (!options["dlc"]) excludeTags.Add("dlc");
            if (options["nocaves"]) excludeTags.Add("minidungeon");
            // Don't make people panic and exclude minidungeons
            if (!options["gaolkeys"]) excludeTags.Add("gaolkey");
            SortedSet<(int, string)> entries = new();
            SortedSet<(int, string)> excludedEntries = new();
            foreach (AnnotationData.PresetTagInfo info in presetAnn.Locations)
            {
                if (info.Locations == null) continue;
                if (info.Tags.Intersect(requireTags).Any())
                {
                    if (info.Tags.Contains("missable"))
                    {
                        continue;
                    }
                    else if (info.Tags.Intersect(excludeTags).Any())
                    {
                        excludedEntries.UnionWith(info.Locations.Select(e => (e.Value, e.Key + "\n")));
                    }
                    else
                    {
                        entries.UnionWith(info.Locations.Select(e => (e.Value, e.Key + "\n")));
                    }
                }
            }
            string text = string.Join("", entries.Select(e => e.Item2));
            if (excludedEntries.Count > 0)
            {
                string sep = string.IsNullOrEmpty(text) ? "" : "\n";
                text = text + sep + messages.Get(importantExcluded) + "\n" + string.Join("", excludedEntries.Select(e => e.Item2));
            }
            Control check = locationGroup.Controls[label.Name.TrimEnd('L')];
            SearchScrollForm.Show(this, messages, text, check?.Text ?? "");
        }

        [Localize]
        private static readonly Text seedError = new Text(
            "Invalid fixed seed",
            "EldenForm_seedError");
        [Localize]
        private static readonly Text enemyseedError = new Text(
            "Invalid enemy seed",
            "EldenForm_enemyseedError");
        [Localize]
        private static readonly Text running = new Text(
            "Running...",
            "EldenForm_running");
        [Localize]
        private static readonly Text runSuccessStatus = new Text(
            "Done! Hints and spoilers in spoiler_logs directory as {0} - Restart your game!!",
            "EldenForm_runSuccess");
        [Localize]
        private static readonly Text runError = new Text(
            "Error encountered: {0}\nIt may work to try again with a different seed. See most recent file in spoiler_logs directory for the full error.",
            "EldenForm_runError");
        [Localize]
        private static readonly Text runErrorGeneric = new Text(
            "Error encountered",
            "EldenForm_runErrorGeneric");
        [Localize]
        private static readonly Text runErrorStatus = new Text(
            "Error! Partial log in spoiler_logs directory as {0}",
            "EldenForm_runErrorStatus");

        private async void randomize_Click(object sender, EventArgs e)
        {
            if (working) return;
            SetWarning();
            if (error) return;
            // This is probably not ideal compared to keeping a random from previous attempts, as certain seeds are impossible this way.
            // Alternatively switch to something with much more bits.
            Random seedRandom = new Random();
            bool newFixedSeed = false;
            if (!default_reroll.Checked && fixedseed.Text.Trim() != "")
            {
                if (uint.TryParse(fixedseed.Text.Trim(), out uint seed))
                {
                    options.Seed = seed;
                }
                else
                {
                    SetStatus(messages.Get(seedError), true);
                    return;
                }
            }
            else
            {
                options.Seed = (uint)seedRandom.Next();
                newFixedSeed = true;
            }
            bool newEnemySeed = false;
            if (defaultRerollEnemy.Enabled && !defaultRerollEnemy.Checked && enemyseed.Text.Trim() != "" && !enemyseedPlaceholder)
            {
                if (uint.TryParse(enemyseed.Text.Trim(), out uint seed))
                {
                    options.Seed2 = seed;
                }
                else
                {
                    SetStatus(messages.Get(enemyseedError), true);
                    return;
                }
            }
            else if (defaultRerollEnemy.Enabled && defaultRerollEnemy.Checked)
            {
                options.Seed2 = (uint)seedRandom.Next();
                newEnemySeed = true;
            }
            else
            {
                options.Seed2 = 0;
            }
            if (!MiscSetup.ModifyEldenRingFiles(messages, exe.Text, out string errorText))
            {
                SetError(errorText);
                SetStatus(messages.Get(runErrorGeneric), true);
                return;
            }
            bool randomania = false;
            if (!string.IsNullOrEmpty(mergemod.Text) && MiscSetup.ProcessRandomaniaTextures(mergemod.Text))
            {
                randomania = true;
                if (!MiscSetup.ProcessedRandomaniaTextures(mergemod.Text))
                {
                    DialogResult result = MessageBox.Show(
                        "Found Randomania items in the merged mod! They need to be processed first to be used in-game, which will take about a minute and use 4 GB of memory and 1.5 GB of disk space. Do this during randomization?",
                        messages.Get(FormText.ConfirmTitleText),
                        MessageBoxButtons.YesNo);
                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }
            }
            SaveOptions();
            RandomizerOptions rand = options.Copy();
            working = true;
            string buttonText = randomize.Text;
            randomize.Text = messages.Get(running);
            randomize.BackColor = Color.LightYellow;
            fixedseed.Text = $"{rand.Seed}";
            zipModFilesToolStripMenuItem.Enabled = false;
            if (newFixedSeed)
            {
                fixedseedFlash = sw.ElapsedMilliseconds;
            }
            if (newEnemySeed)
            {
                ManualSetEnemySeed(rand.Seed2);
                enemyseedFlash = sw.ElapsedMilliseconds;
            }
            if (randomania)
            {
                // Probably not great, mainly to change starting characters
                rand["shrooms"] = true;
            }
            Runner.DeleteLaunchFile();
            UpdateLaunchGame();

            void wrapInvoke(Action action)
            {
                if (InvokeRequired)
                {
                    Invoke(action);
                }
                else
                {
                    action();
                }
            }
            Randomizer randomizer = new Randomizer();
            await Task.Factory.StartNew(() =>
            {
                // Actual randomization
                string seed2 = rand.Seed2 == 0 || rand.Seed2 == rand.Seed ? "" : $"_{rand.Seed2}";
                string seedStr = $"{rand.Seed}{seed2}";
                string timeStr = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
                string hashStr = rand.ConfigHash();
                string runId = $"{timeStr}_log_{seedStr}_{hashStr}.txt";
                string displayFile = runId;
                if (hideseed)
                {
                    displayFile = $"{timeStr}_log_{Regex.Replace(seedStr, @"\d", "?")}_{hashStr}.txt";
                }
                TextWriter log;
                try
                {
                    Directory.CreateDirectory("spoiler_logs");
                    log = File.CreateText($@"spoiler_logs\{runId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    wrapInvoke(() =>
                    {
                        SetError(messages.Get(MiscSetup.CreateFileError, ex.Message));
                        SetStatus(messages.Get(runErrorGeneric), true);
                    });
                    return;
                }
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    MergedMods mods = MergedMods.FromPath(mergemod.Text);
                    randomizer.Randomize(
                        rand, FromGame.ER, status => { wrapInvoke(() => statusL.Text = status); },
                        messages: messages, enemyPreset: selectedPreset, itemPreset: selectedItemPreset,
                        gameExe: exe.Text, modDirs: mods);
                    if (!rand["uxm"])
                    {
                        CreateLaunchFile(mods);
                    }
                    wrapInvoke(() =>
                    {
                        SetStatus(messages.Get(runSuccessStatus, displayFile), success: true);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    wrapInvoke(() =>
                    {
                        SetError(messages.Get(runError, ex.Message));
                        SetStatus(messages.Get(runErrorStatus, displayFile), true);
                    });
                }
                finally
                {
                    log.Close();
                    Console.SetOut(stdout);
                }
            });
            // TODO: Still needed given udpate below?
            randomize.Text = buttonText;
            randomize.BackColor = SystemColors.Control;
            randomize.UseVisualStyleBackColor = true;
            UpdateLaunchGame();
            working = false;
            // Postprocess button, to switch between set seed and randomize
            UpdateRandomizeButtonText();
        }

        private EnemyAnnotations enemyAnn;
        private bool LoadEnemyAnnotations(out string error)
        {
            bool loaded = LoadAnnotations("diste/Base/enemy.txt", ref enemyAnn, out error);
            if (enemyAnn != null)
            {
                // Blank this out to improve memory slightly maybe, as this config is read separately within randomizer
                // Well, it's needed
                enemyAnn.Enemies = null;
            }
            return loaded;
        }

        private AnnotationData.Annotations itemAnn;
        private bool LoadItemAnnotations(out string error)
        {
            return LoadAnnotations("diste/Base/annotations.txt", ref itemAnn, out error);
        }

        private AnnotationData.PresetInfo presetAnn;
        private bool LoadPresetAnnotations(out string error)
        {
            return LoadAnnotations("diste/Base/iteminfo.txt", ref presetAnn, out error);
        }

        private static bool LoadAnnotations<T>(string configPath, ref T value, out string error)
        {
            error = null;
            if (value != null) return true;
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                using (var reader = File.OpenText(configPath))
                {
                    value = deserializer.Deserialize<T>(reader);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private EnemyPreset selectedPreset;
        private string lastPreset;
        private void custom_CheckChanged(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            // Assume that this is a manual click.
            if (default_custom.Checked)
            {
                SetPreset(selectedPreset, true);
                if (selectedPreset != null)
                {
                    presetButton.Enabled = true;
                }
                else
                {
                    presetButton.Enabled = false;
                    using (startUpdate())
                    {
                        default_custom.Checked = false;
                    }
                }
            }
            else
            {
                SetPreset(null, false);
                presetButton.Enabled = false;
            }
        }

        [Localize]
        private static readonly Text presetConfirmUpdate = new Text(
            "Custom item configuration already exists.\nUpdate it to reflect current options?",
            "EldenForm_presetConfirmUpdate");

        private ItemPreset selectedItemPreset;
        private void customItem_CheckedChanged(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            // Assume that this is a manual click.
            if (customitem.Checked)
            {
                // Only do this interactive thing when clicking the checkbox. Don't do it when loading into the form
                // checkbox/button state are set in SetItemPreset, so no need to do that here
                SetItemPreset(selectedItemPreset, createIfAbsent: true, promptUpdate: true);
            }
            else
            {
                SetItemPreset(null);
            }
            // For disabling important location selection. Do other stuff too? idk.
            UpdateEnabled();
            UpdateLabels();
            SaveOptions();
        }

        private void preset_Click(object sender, EventArgs e)
        {
            // At this point, a preset must already exist, and also be valid
            if (selectedPreset == null) return;
            using (PresetEditForm presetForm = new PresetEditForm(enemyAnn, messages, selectedPreset, options["dlcsilo"]))
            {
                presetForm.Icon = Icon;
                // Fine to ignore DialogResult if preset was saved in the form
                presetForm.ShowDialog(this);
                if (presetForm.SavedPreset != null)
                {
                    SetPreset(presetForm.SavedPreset, false);
                }
            }
        }

        private void presetItem_Click(object sender, EventArgs e)
        {
            // Also a preset must exist in this case, as the annotations are loaded there
            if (selectedItemPreset == null) return;
            using (PresetItemForm presetForm = new PresetItemForm(itemAnn, presetAnn, messages, selectedItemPreset, () => options))
            {
                presetForm.Icon = Icon;
                // Fine to ignore DialogResult if preset was saved in the form
                presetForm.ShowDialog(this);
                if (presetForm.SavedPreset != null)
                {
                    SetItemPreset(presetForm.SavedPreset);
                    UpdateAllOptions();
                }
            }
        }

        [Localize]
        private static readonly Text presetFileError = new Text(
            "Error: failed to load file required for customization",
            "EldenForm_presetFileError");

        private void SetPreset(EnemyPreset preset, bool createIfAbsent)
        {
            // Nothing to do here, probably?
            if (preset == null && selectedPreset == null && !createIfAbsent) return;
            if (preset != null || createIfAbsent)
            {
                if (!LoadEnemyAnnotations(out string err))
                {
                    MessageBox.Show(err, messages.Get(presetFileError), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (preset == null && createIfAbsent)
            {
                try
                {
                    preset = EnemyPreset.LoadPreset(lastPreset ?? EnemyPreset.OldDefaultName);
                }
                catch (Exception ex)
                {
#if DEBUG
                    // May not exist, that's fine
                    Console.WriteLine(ex);
#endif
                }
                if (preset == null)
                {
                    EnemyPreset defaultPreset = EnemyPreset.MakeClassBasedDefault(enemyAnn);
                    try
                    {
                        defaultPreset.SavePreset();
                        preset = defaultPreset;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(MiscSetup.CreateFileError, ex.Message), messages.Get(presetFileError),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            selectedPreset = preset;
            if (selectedPreset != null)
            {
                // At this point, preset and enemyAnn both exist. This isn't true of all LoadPreset invocations, so migrate it here.
                preset.MigrateVersion(enemyAnn);
                // In case this happened at initialization, update enable states etc. TODO copy what SetItemPreset does probably
                using (startUpdate())
                {
                    default_custom.Checked = true;
                    presetButton.Enabled = true;
                }
                lastPreset = preset.Name;
            }
            UpdateEnemyPresetText();
            options.Preset = preset?.Name;
            SaveOptions();
            SetStatus(null);
        }

        void UpdateEnemyPresetText()
        {
            if (enemyAnn != null && selectedPreset != null)
            {
                presetL.Text = selectedPreset.FullDescription(messages, enemyAnn, options["dlcsilo"]);
            }
            else
            {
                presetL.Text = DefaultEnemyPresetDesc();
            }
        }

        private string DefaultEnemyPresetDesc()
        {
            return EnemyPreset.DefaultDescription(
                messages,
                new()
                {
                    EnemyAnnotations.EnemyClass.Basic, EnemyAnnotations.EnemyClass.Boss, EnemyAnnotations.EnemyClass.MinorBoss,
                    EnemyAnnotations.EnemyClass.Miniboss, EnemyAnnotations.EnemyClass.NightMiniboss, EnemyAnnotations.EnemyClass.DragonMiniboss,
                    EnemyAnnotations.EnemyClass.Evergaol, EnemyAnnotations.EnemyClass.Wildlife, EnemyAnnotations.EnemyClass.HostileNPC,
                },
                new() { EnemyAnnotations.EnemyClass.Scarab },
                options["dlcsilo"]);
        }

        // Always call UpdateEnabled after this
        private void SetItemPreset(ItemPreset itemPreset, bool createIfAbsent = false, bool promptUpdate = false)
        {
            // Nothing to do here, probably? But we need state update below to set checkbox/settings/etc
            // if (itemPreset == null && selectedItemPreset == null && !createIfAbsent) return;
            if (itemPreset != null || createIfAbsent)
            {
                if (!LoadItemAnnotations(out string err) || !LoadPresetAnnotations(out err))
                {
                    MessageBox.Show(err, messages.Get(presetFileError), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            bool created = false;
            if (itemPreset == null && createIfAbsent)
            {
                try
                {
                    itemPreset = ItemPreset.LoadPreset();
                }
                catch (Exception ex)
                {
#if DEBUG
                    // May not exist, that's fine
                    Console.WriteLine("Ignoring error: " + ex);
#endif
                }
                if (itemPreset == null)
                {
                    try
                    {
                        ItemPreset defaultPreset = itemAnn.DefaultPreset.ProcessBuiltins(options);
                        defaultPreset.SavePreset();
                        itemPreset = defaultPreset;
                        created = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(MiscSetup.CreateFileError, ex.Message), messages.Get(presetFileError),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            if (promptUpdate && itemPreset != null && !created)
            {
                ItemPreset changePreset = itemPreset.UpdateFromOptions(options, itemAnn.DefaultPreset, true, null);
                if (changePreset != null)
                {
                    DialogResult result = MessageBox.Show(
                        messages.Get(presetConfirmUpdate),
                        messages.Get(FormText.ConfirmTitleText),
                        MessageBoxButtons.YesNoCancel);
                    if (result == DialogResult.Yes)
                    {
                        itemPreset = changePreset;
                        itemPreset.SavePreset();
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        return;
                    }
                }
            }
            selectedItemPreset = itemPreset;
            if (selectedItemPreset == null)
            {
                presetItemL.Text = "";
            }
            else
            {
                presetItemL.Text = selectedItemPreset.FullDescription(messages, presetAnn.GetItemName);
            }
            options["customitem"] = selectedItemPreset != null;
            if (selectedItemPreset != null)
            {
                options["dlckeysilo"] = selectedItemPreset.KeyItemSilo == LocationData.AreaSiloType.DLC;
            }
            using (startUpdate())
            {
                presetItemButton.Enabled = selectedItemPreset != null;
                customitem.Checked = selectedItemPreset != null;
                if (selectedItemPreset != null)
                {
                    dlckeysilo.Checked = options["dlckeysilo"];
                }
            }
            SaveOptions();
            SetStatus(null);
        }

        private void ReplaceItemPreset(ItemPreset itemPreset)
        {
            if (selectedItemPreset == null || itemPreset == null) return;
            selectedItemPreset = itemPreset;
            selectedItemPreset.SavePreset();
            presetItemL.Text = selectedItemPreset.FullDescription(messages, presetAnn.GetItemName);
        }

        private Color CalculateSeedColor(uint val)
        {
            if (val == 0)
            {
                return SystemColors.Window;
            }
            uint hash = Util.FNV1AHash(val);
            double hue = ((double)hash / uint.MaxValue) * 360;
            double sat = 0.08 + 0.08 * ((byte)hash) / 255.0;
            double value = 1 - 0.1 * ((byte)(hash >> 8)) / 255.0;
            return Util.ColorFromHSV(hue, sat, value);
        }

        private void fixedseed_TextChanged(object sender, EventArgs e)
        {
            string text = fixedseed.Text.Trim();
            uint.TryParse(text, out uint val);
            fixedseedColor = CalculateSeedColor(val);
            bool validNum = val != 0;
            default_reroll.Enabled = validNum;
            if (!validNum)
            {
                default_reroll.Checked = true;
            }
            else if (!simultaneousUpdate)
            {
                // Avoid doing this here when loading things in, but do it when manually typing/pasting
                default_reroll.Checked = false;
            }
            UpdateRandomizeButtonText();
        }

        private void ManualSetEnemySeed(uint seed)
        {
            if (seed != 0)
            {
                enemyseedPlaceholder = false;
                enemyseed.ForeColor = SystemColors.WindowText;
                enemyseed.Text = seed.ToString();
            }
            else
            {
                if (enemyseed.Focused)
                {
                    enemyseedPlaceholder = false;
                    enemyseed.ForeColor = SystemColors.WindowText;
                    enemyseed.Text = "";
                }
                else
                {
                    enemyseedPlaceholder = true;
                    enemyseed.ForeColor = SystemColors.GrayText;
                    enemyseed.Text = hideseed ? "" : messages.Get(enemyseedPlaceholderText);
                }
            }
        }

        // Copy this from Sekiro
        private void enemyseed_TextChanged(object sender, EventArgs e)
        {
            // Manage placeholder text, because winforms is bad
            if (enemyseed.Focused && enemyseed.Enabled && enemyseedPlaceholder)
            {
                enemyseedPlaceholder = false;
                enemyseed.ForeColor = SystemColors.WindowText;
                enemyseed.Text = "";
            }
            if (!enemyseed.Focused && string.IsNullOrWhiteSpace(enemyseed.Text))
            {
                enemyseedPlaceholder = true;
                enemyseed.ForeColor = SystemColors.GrayText;
                // Note this will cause a self-update, but the checking boolean has already been set
                enemyseed.Text = hideseed ? "" : messages.Get(enemyseedPlaceholderText);
            }
            string text = enemyseed.Text.Trim();
            uint.TryParse(text, out uint val);
            enemyseedColor = CalculateSeedColor(val);
            // Manage checkbox
            if (enemyseedPlaceholder || enemyseed.Text == "")
            {
                // I guess let it be separate from enemy seed
                defaultRerollEnemy.Enabled = enemyseed.Enabled;
                UpdateRandomizeButtonText();
                return;
            }
            bool valid = val != 0;
            if (default_reroll.Checked)
            {
                defaultRerollEnemy.Enabled = valid;
                if (!valid && options.Seed2 == 0)
                {
                    defaultRerollEnemy.Checked = true;
                }
            }
            UpdateRandomizeButtonText();
        }

        private void visibility_Click(object sender, EventArgs e)
        {
            SetSeedVisibility(!hideseed);
            options["hideseed"] = hideseed;
            SaveOptions();
        }

        private void SetSeedVisibility(bool value)
        {
            hideseed = value;
#if DEV_RELEASE
            visibility.Image = hideseed ? Resources.VisibilityRounded : Resources.VisibilityOffRounded;
#endif
            fixedseed.UseSystemPasswordChar = hideseed;
            enemyseed.UseSystemPasswordChar = hideseed;
            if (enemyseedPlaceholder)
            {
                // Ideally, we could change UseSystemPasswordChar based on whether it's placeholder or not,
                // but this can happen based on focus, and without hacky async delays this can stunlock winforms.
                enemyseed.Text = hideseed ? "" : messages.Get(enemyseedPlaceholderText);
            }
        }

        private async Task FlashLoop()
        {
            Color prevFixed = new Color();
            Color prevEnemy = new Color();
            bool prevHide = hideseed;
            long timerTime = 500;
            sw.Start();
            Color createColor(Color c, double remaining)
            {
                // remaining goes from 1 to 0
                // See also Util.BlendColors(c, Color.White, remaining);
                Util.ColorToHSV(c, out double hue, out double saturation, out double value);
                // Use (0.5 - saturation) to always go from 0.5
                if (hideseed)
                {
                    saturation = saturation + 0.5 * (remaining * remaining);
                }
                else
                {
                    saturation = 0.5 * (remaining * remaining);
                    value = 1 - (1 - value) * (remaining * remaining);
                }
                return Util.ColorFromHSV(hue, saturation, value);
            }
            // Why did I implement this?
            while (true)
            {
                await Task.Delay(5);
                Color actualFixed = hideseed ? fixedseedColor : SystemColors.Window;
                Color actualEnemy = hideseed ? enemyseedColor : SystemColors.Window;
                long elapsed = sw.ElapsedMilliseconds;
                // elapsed 500, flash 200. elapsed 300, result 700. elapsed 1500, flash 200. elapsed 1300, result 0.
                long remainingFixed = timerTime - Math.Clamp(elapsed - fixedseedFlash, 0, timerTime);
                long remainingEnemy = timerTime - Math.Clamp(elapsed - enemyseedFlash, 0, timerTime);
                if (hideseed == prevHide
                    && prevFixed == actualFixed && prevEnemy == actualEnemy
                    && remainingFixed == 0 && remainingEnemy == 0)
                {
                    continue;
                }
                prevHide = hideseed;
                prevFixed = actualFixed;
                prevEnemy = actualEnemy;
                if (remainingFixed > 0)
                {
                    prevFixed = createColor(fixedseedColor, (double)remainingFixed / timerTime);
                }
                if (remainingEnemy > 0)
                {
                    prevEnemy = createColor(enemyseedColor, (double)remainingEnemy / timerTime);
                }
                // These are ideally best done in the UI thread, but it should be harmless enough here, hopefully.
                fixedseed.BackColor = visibility.BackColor = prevFixed;
                enemyseed.BackColor = prevEnemy;
            }
        }

        [Localize]
        private static readonly Text randomizeRun = new Text(
            "Run",
            "EldenForm_randomizeRun");
        [Localize]
        private static readonly Text randomizeBoth = new Text(
            "Randomize items and enemies",
            "EldenForm_randomizeBoth");
        [Localize]
        private static readonly Text randomizeItem = new Text(
            "Randomize items",
            "EldenForm_randomizeItem");
        [Localize]
        private static readonly Text randomizeSame = new Text(
            "Run with set seed",
            "EldenForm_randomizedFixed");
        [Localize]
        private static readonly Text randomizeEnemy = new Text(
            "Randomize enemies",
            "EldenForm_randomizeEnemy");
        [Localize]
        private static readonly Text randomizeItemOnly = new Text(
            "Reroll items (same enemies)",
            "EldenForm_randomizeItemOnly");
        [Localize]
        private static readonly Text randomizeEnemyOnly = new Text(
            "Reroll enemies (same items)",
            "EldenForm_randomizeEnemyOnly");

        private void reroll_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRandomizeButtonText();
        }

        private void UpdateRandomizeButtonText()
        {
            if (working) return;
            Text text;
            if (enemy.Checked && item.Checked)
            {
                text = randomizeBoth;
                if (default_reroll.Checked)
                {
                    if (!defaultRerollEnemy.Checked && uint.TryParse(enemyseed.Text.Trim(), out _))
                    {
                        text = randomizeItemOnly;
                    }
                }
                else
                {
                    if (defaultRerollEnemy.Checked)
                    {
                        text = randomizeEnemyOnly;
                    }
                    else
                    {
                        text = randomizeSame;
                    }
                }
            }
            else if (enemy.Checked)
            {
                text = randomizeEnemy;
            }
            else if (item.Checked)
            {
                text = randomizeItem;
            }
            else
            {
                text = randomizeRun;
            }
            randomize.Text = messages.Get(text);
        }

        // UXM stuff
        private void exe_TextChanged(object sender, EventArgs e)
        {
            bool valid = true;
            try
            {
                string gamePath = Path.GetDirectoryName(exe.Text);
                if (exe.Text.Trim() == "" || !Directory.Exists(gamePath))
                {
                    valid = false;
                }
                string exeName = Path.GetFileName(exe.Text).ToLowerInvariant();
                if (exeName != "eldenring.exe")
                {
                    valid = false;
                }
            }
            catch (ArgumentException)
            {
                valid = false;
            }
            if (valid)
            {
                RandomizerOptions.SaveExe(exe.Text);
                // Somewhat hacky, turn back on to revalidate in SetWarning
                randomize.Enabled = true;
            }
            SetWarning();
        }

        private void exeButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Elden Ring install location";
                dialog.Filter = "Elden Ring exe|eldenring.exe";
                dialog.RestoreDirectory = true;
                if (ModRunner.TryGetDirectory(exe.Text, out string dir))
                {
                    dialog.InitialDirectory = dir;
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    exe.Text = dialog.FileName;
                }
            }
        }

        private void mergeButton_Click(object sender, EventArgs e)
        {
            // Allow selecting 3 types of things: directory, regulation.bin, toml file (merge multiple mods)
            using (MergeModForm dialog = new MergeModForm(messages, mergemod.Text))
            {
                dialog.Icon = Icon;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // This may be null
                    mergemod.Text = dialog.FileName;
                }
            }
        }

        private void mergemod_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string mod = mergemod.Text.Trim();
                if (mod == "" || File.Exists(mod) || Directory.Exists(mod))
                {
                    RandomizerOptions.SaveMod(mod);
                }
            }
            catch (ArgumentException) { }
        }

        private void dllButton_Click(object sender, EventArgs e)
        {
            List<string> newDlls = DllForm.Show(this, messages, extraDlls);
            if (newDlls != null)
            {
                extraDlls = newDlls;
                helper.Checked = extraDlls.Any(path => Path.GetFileName(path).Equals(Path.GetFileName(HelperForm.DllPath), StringComparison.OrdinalIgnoreCase));
                crashfix.Checked = extraDlls.Any(path => Path.GetFileName(path).Equals(Path.GetFileName(HelperForm.FixDllPath), StringComparison.OrdinalIgnoreCase));
                UpdateExtraDlls();
            }
        }

        private void UpdateExtraDlls()
        {
            if (extraDlls.Count == 0)
            {
                dllInfoL.Text = "";
            }
            else
            {
                dllInfoL.Text = messages.Get(dllList, string.Join(", ", extraDlls.Select(Path.GetFileName).Distinct()));
            }
            RandomizerOptions.SaveExtraDlls(extraDlls);
        }

        [Localize]
        private static readonly Text dllList = new Text(
            "Using {0}",
            "EldenForm_dllList");
        [Localize]
        private static readonly Text restoreTitle = new Text(
            "Restore files",
            "EldenForm_restoreAlertTitle");
        [Localize]
        private static readonly Text restoreErrorBadExe = new Text(
            "Game exe not found",
            "EldenForm_restoreErrorBadExe");
        [Localize]
        private static readonly Text restoreNothingToDo = new Text(
            "No .randobak files found in {0}",
            "EldenForm_restoreNothingToDo");
        [Localize]
        private static readonly Text restoreConfirmTitle = new Text(
            "Restore from these files?",
            "EldenForm_restoreConfirmTitle");
        [Localize]
        private static readonly Text restoreFileSuccess = new Text(
            "Restored from {0}",
            "EldenForm_restoreFileSuccess");
        [Localize]
        private static readonly Text restoreFileFailure = new Text(
            "Failed to restore from {0}!",
            "EldenForm_restoreFileFailure");

        private void restoreUXMFileBackupsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string title = messages.Get(restoreTitle);
            if (!File.Exists(exe.Text))
            {
                MessageBox.Show(messages.Get(restoreErrorBadExe), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string gamePath = Path.GetDirectoryName(exe.Text);
            List<string> restoreFiles = GameData.GetBackupFiles(gamePath, FromGame.ER);
            if (restoreFiles.Count == 0)
            {
                MessageBox.Show(messages.Get(restoreNothingToDo, gamePath), title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult result = ScrollDialog.Show(this, string.Join(Environment.NewLine, restoreFiles), messages.Get(restoreConfirmTitle));
            if (result != DialogResult.OK)
            {
                return;
            }
            List<string> restoreLog = new List<string>();
            foreach (string restoreFile in restoreFiles)
            {
                try
                {
                    GameData.RestoreBackup(restoreFile);
                    restoreLog.Add(messages.Get(restoreFileSuccess, restoreFile));
                }
                catch (Exception ex)
                {
                    restoreLog.Add(messages.Get(restoreFileFailure, restoreFile));
                    restoreLog.Add("");
                    restoreLog.Add(ex.ToString());
                }
            }
            ScrollDialog.Show(this, string.Join(Environment.NewLine, restoreLog), title);
        }

        private void saveOptionsFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.FileName = $"{options.ConfigHash()}.randomizeopt";
                dialog.Filter = "Randomizer options files (*.randomizeopt)|*.randomizeopt|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                dialog.OverwritePrompt = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // It appears as though Windows automatically adds an extension if none is provided, thanks Windows
                    string path = dialog.FileName;
                    using (var writer = File.CreateText(path))
                    {
                        MakeOptionsFile().Save(writer);
                    }
                }
            }
        }

        private RandomizerOptionsFile MakeOptionsFile()
        {
            RandomizerOptions opt = options.Copy();
            // TODO: What was the reason for delaying this until button click time again?
            if (uint.TryParse(fixedseed.Text.Trim(), out uint seed))
            {
                opt.Seed = seed;
            }
            if (uint.TryParse(enemyseed.Text.Trim(), out uint seed2))
            {
                opt.Seed2 = seed2;
            }
            string helperConfig = null;
            if (options["helper"] && File.Exists(HelperForm.ConfigPath))
            {
                helperConfig = File.ReadAllText(HelperForm.ConfigPath);
            }
            return RandomizerOptionsFile.Create(
                Randomizer.EldenVersion, opt,
                enemyPreset: selectedPreset, itemPreset: selectedItemPreset, helperConfig: helperConfig);
        }

        [Localize]
        private static readonly Text versionMismatch = new Text(
            "Mismatched options versions! File has version {0}, but current randomizer version is {1}. This may result in different output. Try to continue anyway?",
            "EldenForm_versionMismatch");
        [Localize]
        private static readonly Text confirmOverwritePreset = new Text(
            "Overwrite preset file?\n{0}", "EldenForm_confirmOverwritePreset");
        [Localize]
        private static readonly Text confirmCreatePreset = new Text(
            "Create preset file?\n{0}", "EldenForm_confirmCreatePreset");

        private void loadOptionsFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Randomizer options files (*.randomizeopt)|*.randomizeopt|Enemy localization files (*.enemy.json)|*.enemy.json|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != DialogResult.OK) return;
                if (dialog.FileName.EndsWith(".enemy.json"))
                {
                    ShowBossMessages(dialog.FileName);
                    return;
                }
                RandomizerOptionsFile newOpts = RandomizerOptionsFile.Load(dialog.FileName);
                if (newOpts.Version != Randomizer.EldenVersion)
                {
                    DialogResult result = MessageBox.Show(
                        messages.Get(versionMismatch, newOpts.Version, Randomizer.EldenVersion),
                        messages.Get(FormText.ConfirmTitleText),
                        MessageBoxButtons.OKCancel);
                    if (result != DialogResult.OK)
                    {
                        return;
                    }
                }
                EnemyPreset enemyPreset = newOpts.EnemyPresetValue;
                if (enemyPreset != null)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo("presets");
                    string presetPath = enemyPreset.FileName;
                    FileInfo fileInfo = new FileInfo(presetPath);
                    if (fileInfo.DirectoryName != dirInfo.FullName)
                    {
                        // Basic attempt at security check
                        throw new Exception($"Invalid preset name {enemyPreset}");
                    }
                    Text confirm = File.Exists(presetPath) ? confirmOverwritePreset : confirmCreatePreset;
                    DialogResult result = MessageBox.Show(
                        messages.Get(confirmOverwritePreset, presetPath),
                        messages.Get(FormText.ConfirmTitleText),
                        MessageBoxButtons.OKCancel);
                    if (result != DialogResult.OK)
                    {
                        return;
                    }
                    try
                    {
                        enemyPreset.SavePreset();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(MiscSetup.CreateFileError, ex.Message), messages.Get(presetFileError),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                ItemPreset itemPreset = newOpts.ItemPresetValue;
                if (itemPreset != null)
                {
                    // In this case, overwrite always, it's just a fixed location
                    try
                    {
                        itemPreset.SavePreset();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(MiscSetup.CreateFileError, ex.Message), messages.Get(presetFileError),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                if (newOpts.HelperConfig != null)
                {
                    try
                    {
                        HelperOptions.ImportSettings(HelperForm.ConfigPath, newOpts.HelperConfig);
                    }
                    catch (Exception)
                    {
                        // Don't bother for now
                    }
                }
                SetOptionsString(newOpts.OptionsValue.ToString());
                UpdateAllOptions();
            }
        }

        private void setOptionsStringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OptionsForm form = new OptionsForm(messages, options.FullString()))
            {
                form.Icon = Icon;
                DialogResult result = form.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    string text = form.OptionString;
                    if (string.IsNullOrEmpty(text))
                    {
                        text = defaultOpts;
                    }
                    if (form.HasOptionsError(text, out string msg))
                    {
                        SetStatus(msg, error: true);
                        return;
                    }
                    SetOptionsString(text);
                    UpdateAllOptions();
                }
            }
        }

        [Localize]
        private static readonly Text resetOptionsText = new Text("Reset options to default?", "EldenForm_resetOptions");

        private void resetOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                messages.Get(resetOptionsText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.OKCancel);
            if (result == DialogResult.OK)
            {
                SetOptionsString(defaultOpts);
                // I guess also reset this? It's outside of normal options.
                mergemod.Text = "";
                extraDlls.Clear();
                UpdateAllOptions();
            }
        }

        private void CreateLaunchFile(MergedMods mods)
        {
            StringWriter writer = new StringWriter();
            RandomizerOptionsFile.Create(Randomizer.EldenVersion, options).Save(writer);
            // File.WriteAllText("launch1.txt", writer.ToString());
            // Console.WriteLine($"Launch file [{writer.ToString()}]");
            string hash = Util.GetMD5TextHash(writer.ToString());
            List<string> comments = new List<string>
            {
                $"Produced by Elden Ring Randomizer {Randomizer.EldenVersion}",
                $"<hash>{hash}</hash>",
            };
            Runner.CreateLaunchFile(comments, MiscSetup.GetModEngineOutputDirectory(), mods);
            // For fog rando merging purposes only
            Runner.CreateDllLaunchFile(extraDlls);
        }

        [Localize]
        private static readonly Text launchMismatch = new Text(
            "Randomizer options appear to be changed from the last time randomizer was run. Launch anyway?", "EldenForm_launchMismatch");
        [Localize]
        private static readonly Text launchMismatchZip = new Text(
            "Randomizer options appear to be changed from the last time randomizer was run. Zip anyway?", "EldenForm_launchMismatchZip");
        [Localize]
        private static readonly Text launchRunning = new Text(
            "Elden Ring appears to be already running, so launching with Mod Engine will have no effect. Try anyway?", "EldenForm_launchRunning");
        [Localize]
        private static readonly Text launchFailedTitle = new Text(
            "Launch failed", "EldenForm_launchFailedTitle");
        [Localize]
        private static readonly Text launchFog = new Text(
            "If you're using Fog Gate Randomizer (under Misc Options), launch the game from FogMod.exe, not from here", "EldenForm_launchFog");
        [Localize]
        private static readonly Text launchFailed = new Text(
            "Automatic Mod Engine launcher appeared to fail.\n\nMake sure Steam is running. If the game is currently running, close it and wait until it is fully shut down.\n\nCheck Troubleshooting on https://www.nexusmods.com/eldenring/mods/428/ to see when Mod Engine might fail. You may need to try using a manual Mod Engine launcher or Nuxe installation.", "EldenForm_launchFailed");

        private bool CheckHashMatch()
        {
            StringWriter writer = new StringWriter();
            // Enemy presets have a bit too much variability (see e.g. NamesV1), and in-memory vs serialized differences, so exclude them for now
            RandomizerOptionsFile.Create(Randomizer.EldenVersion, options).Save(writer);
            // File.WriteAllText("launch2.txt", writer.ToString());
            // Console.WriteLine($"Launch compare [{writer.ToString()}]");
            string hash = Util.GetMD5TextHash(writer.ToString());

            string fileHash = null;
            Regex hashRe = new Regex(@"<hash>([0-9A-Za-z]+)</hash>");
            string contents;
            try
            {
                contents = Runner.ReadLaunchFileText();
            }
            catch (IOException)
            {
                return false;
            }
            Match match = hashRe.Match(contents);
            if (match.Success)
            {
                fileHash = match.Groups[1].Value;
            }
            return hash == fileHash;
        }

        private async void launchButton_Click(object sender, EventArgs e)
        {
            UpdateLaunchGame();
            if (!launchButton.Enabled || Runner.IsLaunching()) return;

            if (options["fog"])
            {
                MessageBox.Show(
                    messages.Get(launchFog), messages.Get(launchFailedTitle),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!CheckHashMatch())
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(launchMismatch),
                    messages.Get(FormText.ConfirmTitleText),
                    MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK) return;
            }

            if (Runner.IsGameRunning())
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(launchRunning),
                    messages.Get(FormText.ConfirmTitleText),
                    MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK) return;
            }

            await Runner.LaunchGame(extraDlls);
        }

        [Localize]
        private static readonly Text zipMergeConfirm = new Text(
            "Include the following merged mod directories in the zip file?", "EldenForm_zipMergeConfirm");
        [Localize]
        private static readonly Text zipStatusProgress = new Text(
            "Zipping mod files...", "EldenForm_zipStatusProgress");
        [Localize]
        private static readonly Text zipStatusDone = new Text(
            "Zipped {0}", "EldenForm_zipStatusDone");

        private async void zipModFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateLaunchGame();
            if (!launchButton.Enabled) return;
            // One more heuristic check before the zip gets generated
            if (!File.Exists("regulation.bin")) return;

            if (!CheckHashMatch())
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(launchMismatchZip),
                    messages.Get(FormText.ConfirmTitleText),
                    MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK) return;
            }

            MergedMods mods;
            try
            {
                mods = MergedMods.FromPath(mergemod.Text);
            }
            catch (Exception ex)
            {
                // Mainly TOML parse errors - probably fine to ignore, but just handle
                SetError(ex.Message);
                SetStatus(messages.Get(runErrorGeneric), true);
                return;
            }
            if (mods.Count > 0)
            {
                // Include the following merged mod directories in the zip file?
                DialogResult result = MessageBox.Show(
                    messages.Get(zipMergeConfirm) + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, mods.Dirs),
                    messages.Get(FormText.ConfirmTitleText),
                    MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.No)
                {
                    // Forget about them for now, rather than including nonexistent directories in the toml
                    mods = new MergedMods();
                }
                else if (result != DialogResult.Yes)
                {
                    return;
                }
            }
            foreach (string dir in mods.Dirs)
            {
                // Allow this if there's a UXM mod installed, but not here. Don't localize this for now (?)
                if (File.Exists(Path.Combine(dir, "eldenring.exe")))
                {
                    SetError("Merged mod directory appears to be the game directory. You shouldn't include this in the zip. Clear your merged mod directory first.");
                    SetStatus(messages.Get(runErrorGeneric), true);
                    return;
                }
            }

            string zipFile = null;
            string seedStr = "";
            string seedStrReplace = null;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                if (options.Seed != 0)
                {
                    seedStr += "_" + options.Seed;
                    if (options.Seed2 != 0 && options.Seed2 != options.Seed)
                    {
                        seedStr += "_" + options.Seed2;
                    }
                    seedStrReplace = Regex.Replace(seedStr, @"\d", "?");
                }
                dialog.FileName = $"EldenRingRandomizer{seedStr}_{options.ConfigHash()}.zip";
                dialog.Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                dialog.OverwritePrompt = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    zipFile = dialog.FileName;
                }
            }
            if (zipFile == null) return;

            List<string> modFiles = MiscSetup.GetGameFiles(Directory.GetCurrentDirectory(), false);
            // Also add dlls, it's probably fine to not filter those by hash, as dlls in other mods will also get added
            string dllDir = Path.Combine(Directory.GetCurrentDirectory(), "dll");
            if (Directory.Exists(dllDir))
            {
                foreach (string dllPath in Directory.GetFiles(dllDir))
                {
                    if (dllPath.EndsWith(".dll") || dllPath.EndsWith("_config.ini"))
                    {
                        modFiles.Add(dllPath);
                    }
                }
            }
            Dictionary<string, List<string>> explicitFiles = new() { [""] = modFiles };
            SetStatus(messages.Get(zipStatusProgress));
            try
            {
                await Task.Factory.StartNew(() =>
                {
                    Runner.ZipModEngineDir("rando", zipFile, mods, extraDlls, explicitFiles);
                });
                string fileName = Path.GetFileName(zipFile);
                if (seedStrReplace != null && hideseed) fileName = fileName.Replace(seedStr, seedStrReplace);
                SetStatus(messages.Get(zipStatusDone, fileName), success: true);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                SetStatus(messages.Get(runErrorGeneric), true);
            }
        }

        private void helperButton_Click(object sender, EventArgs e)
        {
            if (!File.Exists(HelperForm.ConfigPath))
            {
                MessageBox.Show(messages.Get(FormText.FileMissingText, HelperForm.ConfigPath), messages.Get(FormText.ErrorTitleText), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            HelperForm.Show(this, messages);
        }

        private void updateButton_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.nexusmods.com/eldenring/mods/428?tab=files") { UseShellExecute = true });
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.nexusmods.com/eldenring/mods/428?tab=files") { UseShellExecute = true });
        }

        // Not used for now, should probably go in a separate tools binary
        private void bossNameLocalizationToolStripMenuItem_Click(object sender, EventArgs e) => ShowBossMessages();

        private void ShowBossMessages(string initialPath = null)
        {
            EnemyAnnotations locEnemyAnn = null;
            bool loaded = LoadAnnotations("diste/Base/enemy.txt", ref locEnemyAnn, out string err);
            if (!loaded)
            {
                MessageBox.Show(err, messages.Get(presetFileError), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            EnemyMessagesForm.Show(this, messages, locEnemyAnn, initialPath);
        }
    }
}
