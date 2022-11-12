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
using Newtonsoft.Json;
using RandomizerCommon.Properties;
using YamlDotNet.Serialization;
using static RandomizerCommon.Messages;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public partial class EldenForm : Form
    {
        // There is a lot of Enemy Randomizer placeholder here because we'll need it eventually.
        [Localize]
        private static readonly Text enemyseedPlaceholderText = new Text(
            "(same as overall seed)", "EldenForm_enemyseedPlaceholder");
        private static string defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe";
        private static string unseenTabs = "enemy,misc";

        private Messages messages;

        private RandomizerOptions options = new RandomizerOptions(FromGame.ER);
        private string defaultOpts = null;
        private Dictionary<string, string> originalLabels = new Dictionary<string, string>();
        private bool enemyseedPlaceholder;
        private bool simultaneousUpdate;
        private bool working;
        private bool error;

        public EldenForm(bool dumpMessages = false, bool loadEmpty = false)
        {
            InitializeComponent();
            Text = $"Elden Ring Item and Enemy Randomizer {Randomizer.EldenVersion}";
            // Set up tabs real quick
            tabControl.TabDrawer = new GrayIris.Utilities.UI.Controls.XlTabDrawer();
            tabControl.SelectedIndex = 0;
            if (Settings.Default.ViewedTabs != unseenTabs)
            {
                enemyPage.Blink = true;
                miscPage.Blink = true;
                tabControl.Blink = true;
                tabControl.BlinkColor = Color.FromArgb(255, 220, 230);
            }

            messages = new Messages("diste", loadEmpty);
            if (dumpMessages)
            {
                OptionsForm optionsForm = new OptionsForm(messages, "");
                if (!LoadEnemyAnnotations(out string annErr)) throw new Exception(annErr);
                Preset defaultPreset = Preset.MakeClassBasedDefault(enemyAnn);
                PresetEditForm presetForm = new PresetEditForm(enemyAnn, messages, defaultPreset);
                DumpEnglishMessages(
                    new List<Form> { this, optionsForm, presetForm },
                    new List<Type> {
                            typeof(EldenForm), typeof(OptionsForm), typeof(PresetEditForm),
                            typeof(MiscSetup), typeof(Preset), typeof(EnemyAnnotations),
                            typeof(Randomizer), typeof(HintMarker), typeof(Permutation), typeof(PermutationWriter),
                    });
            }
            enemyseed.GotFocus += enemyseed_TextChanged;
            enemyseed.LostFocus += enemyseed_TextChanged;

            RandomizerOptions initialOpts = new RandomizerOptions(FromGame.ER);
            SetControlFlags(this, initialOpts);
            initialOpts.Difficulty = 20;
            defaultOpts = initialOpts.FullString();

            string existingOpts = Settings.Default.Options;
            if (string.IsNullOrWhiteSpace(existingOpts))
            {
                options.Difficulty = difficulty.Value;
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

            // All the stuff forwards on can depend on localization
            SetCulture(null);

            string defaultExe = Settings.Default.Exe;
            if (!string.IsNullOrWhiteSpace(defaultExe))
            {
                exe.Text = defaultExe;
            }
            else if (File.Exists(defaultPath))
            {
                exe.Text = defaultPath;
            }
            string defaultMod = Settings.Default.Mod;
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

            if (messages.InitErrors.Count > 0)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, messages.InitErrors),
                    "Invalid messages", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            presetL.VisibleChanged += (sender, e) =>
            {
                // For some horrific godforsaken reason, the \r is stripped from TextBox
                // when initialized while it's not visible, probably?
                // ?????????????????????????????????????????????????????????????????????????
                presetL.Text = Regex.Replace(presetL.Text, "\r?\n", "\r\n");
            };
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

            // New defaults
            // Please oh god make a new system for this
            bool preItem1 = previousOpts.Contains("v1") || previousOpts.Contains("v2");
            bool preEnemy1 = preItem1 || previousOpts.Contains("v3");
            bool preEnemy2 = preEnemy1 || previousOpts.Contains("v4") || previousOpts.Contains("v5") || previousOpts.Contains("v6");
            if (preItem1)
            {
                options["weaponprogression"] = true;
            }
            if (preEnemy1)
            {
                options["enemy"] = true;
                options["scale"] = true;
                options["editnames"] = true;
                options["regularhp"] = true;
                options["bosshp"] = true;
            }
            if (preEnemy2)
            {
                options["phasehp"] = true;
            }
            // Misc required options
            options["racemode"] = true;
            // options["mergemods"] = false;

            simultaneousUpdate = true;
            InsertControlFlags(this);
            difficulty.Value = options.Difficulty;
            simultaneousUpdate = false;

            fixedseed.Text = options.Seed == 0 ? "" : $"{options.Seed}";
            enemyseed.Text = options.Seed2 == 0 || options.Seed == options.Seed2 ? "" : $"{options.Seed2}";
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
                    Preset preset = Preset.LoadPreset(options.Preset, extractOopsAll: false);
                    SetPreset(preset, false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    SetPreset(null, false);
                }
            }
        }

        private void UpdateAllOptions()
        {
            if (options.Seed != 0) default_reroll.Checked = false;
            SetStatus(null);
            UpdateEnabled();
            UpdateLabels();
            SaveOptions();
        }

        private void SetWarning()
        {
            bool fatal = !MiscSetup.CheckRequiredEldenFiles(messages, out string err)
                || MiscSetup.CheckEldenRingMods(messages, options["uxm"], exe.Text, out err);
            SetError(err, fatal);
        }

        private void SetError(string text, bool fatal = false)
        {
            warningL.Text = text ?? "";
            warningL.Visible = true;
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
                originalText = messages.GetFormText(this);
            }
            if (defaultCulture == null)
            {
                defaultCulture = Thread.CurrentThread.CurrentCulture;
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
            List<LanguageMessages> localLangs = messages.GetAllLanguageMessages(culture);
            translators = localLangs.Count > 0 ? localLangs[0].Translators : null;
            originalLabels.Clear();

            // Restore original text positions and save original autosizes
            HashSet<Control> noAuto = new HashSet<Control>();
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
            messages.SetFormText(this, originalText);
            // Also update our fake placeholder text
            if (enemyseedPlaceholder && enemyseed.ForeColor == SystemColors.GrayText)
            {
                enemyseed.Text = messages.Get(enemyseedPlaceholderText);
            }
            UpdateRandomizeButtonText();
            if (enemyAnn != null && selectedPreset != null)
            {
                presetL.Text = selectedPreset.FullDescription(messages, enemyAnn);
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
            repositionCheckbox(enemy, 1);

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
                            int extraHeight = size.Height + 2;
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
        }

        private void tabControl_TabChanged(object sender, EventArgs e)
        {
            tabControl.SelectedTab.Blink = false;
            if (tabControl.Blink && !enemyPage.Blink && !miscPage.Blink)
            {
                tabControl.Blink = false;
                if (Settings.Default.ViewedTabs != unseenTabs)
                {
                    Settings.Default.ViewedTabs = unseenTabs;
                    Settings.Default.Save();
                }
            }
        }

        private void SaveOptions()
        {
            Settings.Default.Options = options.FullString();
            Settings.Default.Save();
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
            if (simultaneousUpdate)
            {
                return;
            }
            SetControlFlags(this);
            UpdateEnabled();
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
            if (control is RadioButton radio)
            {
                getOpt[control.Name] = radio.Checked;
            }
            else if (control is CheckBox check)
            {
                getOpt[control.Name] = check.Checked;
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
            if (control.Name.StartsWith("default") || control.Name.EndsWith("_copy")) return;
            if (control is RadioButton radio)
            {
                radio.Checked = options[control.Name];
            }
            else if (control is CheckBox check)
            {
                check.Checked = options[control.Name];
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
            if (control.Name.StartsWith("default") || control.Name.EndsWith("_copy")) return;
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

        private void MassEnable(Dictionary<Control, bool> toEnable, Control control, string enableName, string filter)
        {
            if (control.Name == enableName) return;
            if (control is RadioButton || control is CheckBox || control is TrackBar || control is Label)
            {
                if (filter == null) toEnable[control] = options[enableName];
            }
            else
            {
                if (filter == null) toEnable[control] = options[enableName];
                foreach (Control sub in control.Controls)
                {
                    MassEnable(toEnable, sub, enableName, filter != null && filter == control.Name ? null : filter);
                }
            }
        }

        private void UpdateEnabled()
        {
            simultaneousUpdate = true;
            bool changes = false;
            Dictionary<Control, bool> toEnable = new Dictionary<Control, bool>();
            MassEnable(toEnable, this, "item", "itemPage");
            MassEnable(toEnable, this, "enemy", "enemyPage");
            // Individual updates
            // TODO: Move this to a shared file at some point!
            void setSimpleEnable(Control control, bool enabled, string overrideDisable)
            {
                if (overrideDisable == null || options[overrideDisable])
                {
                    toEnable[control] = enabled;
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
                if (!enabled && prevEnabled && prevChecked != disabledState)
                {
                    if (check != null) check.Checked = disabledState;
                    else radio.Checked = disabledState;
                    changes = true;
                }
                else if (maybeDefaultState is bool defaultState && enabled && !prevEnabled && prevChecked != defaultState)
                {
                    if (check != null) check.Checked = defaultState;
                    else radio.Checked = defaultState;
                    changes = true;
                }
            };

            // raceloc_health includes all racemode_health locations
            setCheck(raceloc_health, !options["norandom_health"], true, false, "item");
            // After some testing: racemode_upgrades seems to require altboss/health/talisman
            // (shops are not enough, likely due to farumazula)
            // Flask upgrades are less restricted so they're fine
            bool upgrades = options["raceloc_health"] || options["raceloc_altboss"] || options["raceloc_talisman"];
            setCheck(racemode_upgrades, upgrades, null, false, "item");
            setCheck(night, options["raceloc_altboss"], false, false, "item");
            setCheck(markitems, options["markareas"], false, false, "item");
            // Customization options. Depend on scaling.
            // May not want to depend on customization, since that can take some time to load.
            setCheck(regularhp, options["scale"], true, false, "enemy");
            setCheck(bosshp, options["scale"], true, false, "enemy");
            setCheck(multhp, options["scale"], false, false, "enemy");
            setCheck(phasehp, options["scale"], true, false, "enemy");
            setSimpleEnable(presetButton, selectedPreset != null, "enemy");
            // Misc
            setCheck(default_twohand, !options["nostarting"], true, true, null);
            setCheck(onehand, !options["nostarting"], false, false, null);
            setCheck(nohand, !options["nostarting"], false, false, null);
            // Universal options not in the misc tab
            toEnable[language] = true;
            toEnable[languageL] = true;
            toEnable[runereq] = true;

            // Make sure a single radio button of any group is checked
            if (!racemode_upgrades.Checked && !norandom_upgrades.Checked && !default_upgrades.Checked)
            {
                default_upgrades.Checked = true;
                changes = true;
            }
            if (!default_key.Checked && !norandom.Checked)
            {
                default_key.Checked = true;
                changes = true;
            }
            if (!default_twohand.Checked && !onehand.Checked && !nohand.Checked)
            {
                default_twohand.Checked = true;
                changes = true;
            }
            foreach (KeyValuePair<Control, bool> enable in toEnable)
            {
                enable.Key.Enabled = enable.Value;
            }
            enemyseed_TextChanged(null, null);
            randomize.Enabled = (options["enemy"] || options["item"]) && !error;
            if (changes) SetControlFlags(this);
            UpdateLaunchGame();
            simultaneousUpdate = false;
        }

        string modengineConfig = "config_eldenringrandomizer.toml";
        string modengineLauncher = @"diste\ModEngine\modengine2_launcher.exe";

        private void UpdateLaunchGame()
        {
            launchButton.Enabled =
                File.Exists(modengineConfig) && File.Exists(modengineLauncher) && !options["uxm"];
        }

        private static readonly Dictionary<string, (int, int)> checkLabels = new Dictionary<string, (int, int)>
        {
            ["racemodeL"] = (15, 0),
            ["default_bossL"] = (30, 0),
            ["raceloc_healthL"] = (40, 0),
            ["raceloc_shopsL"] = (22, 21),
            ["raceloc_altbossL"] = (105, 55),
            ["nightL"] = (20, 0),
            ["raceloc_talismanL"] = (65, 50),
        };

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

        private void UpdateLabels()
        {
            // Fairness is not used in Elden Ring (yet)
            // if (options.GetNum("veryunfairweight") > 0.5) unfairText = " and very unfair";
            // else if (options.GetNum("unfairweight") > 0.5) unfairText = " and unfair";
            Text loc;
            if (options.GetNum("allitemdifficulty") > 0.7) loc = allitem3;
            else if (options.GetNum("allitemdifficulty") > 0.3) loc = allitem2;
            else if (options.GetNum("allitemdifficulty") > 0.001) loc = allitem1;
            else loc = allitem0;
            Text chain;
            if (options.GetNum("keyitemchainweight") <= 3) chain = keyitem0;
            else if (options.GetNum("keyitemchainweight") <= 4.001) chain = keyitem1;
            else if (options.GetNum("keyitemchainweight") <= 10) chain = keyitem2;
            else chain = keyitem3;
            if (options["norandom"]) chain = null;
            difficultyL.Text = $"{messages.Get(loc)}\r\n{messages.Get(chain)}";
            difficultyAmtL.Text = $"{options.Difficulty}%";

            void updateRec(Control control)
            {
                if (control is Label label && checkLabels.TryGetValue(label.Name, out (int, int) val))
                {
                    if (!originalLabels.TryGetValue(label.Name, out string originalText))
                    {
                        originalLabels[label.Name] = originalText = label.Text;
                    }
                    int count = options["nocaves"] && val.Item2 > 0 ? val.Item2 : val.Item1;
                    label.Text = originalText.Replace("{0}", $"{count}");
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
            Random seedRandom = new Random();
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
            SaveOptions();
            RandomizerOptions rand = options.Copy();
            working = true;
            string buttonText = randomize.Text;
            randomize.Text = messages.Get(running);
            randomize.BackColor = Color.LightYellow;
            fixedseed.Text = $"{rand.Seed}";
            if (newEnemySeed)
            {
                enemyseed.Text = rand.Seed2.ToString();
                enemyseed.ForeColor = SystemColors.WindowText;
            }
            if (File.Exists(modengineConfig)) File.Delete(modengineConfig);

            Randomizer randomizer = new Randomizer();
            await Task.Factory.StartNew(() => {
                string seed2 = rand.Seed2 == 0 || rand.Seed2 == rand.Seed ? "" : $"_{rand.Seed2}";
                string runId = $"{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}_log_{rand.Seed}{seed2}_{rand.ConfigHash()}.txt";
                TextWriter log;
                try
                {
                    Directory.CreateDirectory("spoiler_logs");
                    log = File.CreateText($@"spoiler_logs\{runId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    SetError(messages.Get(MiscSetup.CreateFileError, ex.Message));
                    SetStatus(messages.Get(runErrorGeneric), true);
                    return;
                }
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(
                        rand, FromGame.ER, status => { statusL.Text = status; },
                        messages: messages, preset: selectedPreset,
                        gameExe: exe.Text, modDir: mergemod.Text);
                    if (!rand["uxm"])
                    {
                        CreateLaunchFile();
                    }
                    SetStatus(messages.Get(runSuccessStatus, runId), success: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    SetError(messages.Get(runError, ex.Message));
                    SetStatus(messages.Get(runErrorStatus, runId), true);
                }
                finally
                {
                    log.Close();
                    Console.SetOut(stdout);
                }
            });
            randomize.Text = buttonText;
            randomize.BackColor = SystemColors.Control;
            UpdateLaunchGame();
            working = false;
        }

        private EnemyAnnotations enemyAnn;
        private bool LoadEnemyAnnotations(out string error)
        {
            error = null;
            if (enemyAnn != null) return true;
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                string enemyConfigPath = $"diste/Base/enemy.txt";
                using (var reader = File.OpenText(enemyConfigPath))
                {
                    enemyAnn = deserializer.Deserialize<EnemyAnnotations>(reader);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private Preset selectedPreset;
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
                    simultaneousUpdate = true;
                    default_custom.Checked = false;
                    simultaneousUpdate = false;
                }
            }
            else
            {
                SetPreset(null, false);
                presetButton.Enabled = false;
            }
        }

        private void preset_Click(object sender, EventArgs e)
        {
            // At this point, a preset must already exist, and also be valid
            if (selectedPreset == null) return;
            using (PresetEditForm presetForm = new PresetEditForm(enemyAnn, messages, selectedPreset))
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

        [Localize]
        private static readonly Text presetFileError = new Text(
            "Error: failed to load file required for customization",
            "EldenForm_presetFileError");

        private void SetPreset(Preset preset, bool createIfAbsent)
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
                    preset = Preset.LoadPreset(lastPreset ?? Preset.DefaultName);
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
                    Preset defaultPreset = Preset.MakeClassBasedDefault(enemyAnn);
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
            if (selectedPreset == null)
            {
                presetL.Text = "";
            }
            else
            {
                presetL.Text = selectedPreset.FullDescription(messages, enemyAnn);
                // In case this happened at initialization, update enable states etc.
                simultaneousUpdate = true;
                default_custom.Checked = true;
                presetButton.Enabled = true;
                simultaneousUpdate = false;
                lastPreset = preset.Name;                
            }
            options.Preset = preset?.Name;
            SaveOptions();
            SetStatus(null);
        }

        private void fixedseed_TextChanged(object sender, EventArgs e)
        {
            string text = fixedseed.Text.Trim();
            default_reroll.Enabled = uint.TryParse(text, out uint val) && val != 0;
            if (!default_reroll.Enabled)
            {
                default_reroll.Checked = true;
            }
            UpdateRandomizeButtonText();
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
                enemyseed.Text = messages.Get(enemyseedPlaceholderText);
            }
            // Manage checkbox
            if (enemyseedPlaceholder || enemyseed.Text == "")
            {
                // I guess let it be separate from enemy seed
                defaultRerollEnemy.Enabled = enemyseed.Enabled;
                UpdateRandomizeButtonText();
                return;
            }
            string text = enemyseed.Text.Trim();
            bool valid = uint.TryParse(text, out uint val) && val != 0;
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

        [Localize]
        private static readonly Text randomizeNothing = new Text(
            "???",
            "EldenForm_randomizeNothing");
        [Localize]
        private static readonly Text randomizeBoth = new Text(
            "Randomize items and enemies",
            "EldenForm_randomizeBoth");
        [Localize]
        private static readonly Text randomizeItem = new Text(
            "Randomize items",
            "EldenForm_randomizeItem");
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
                text = randomizeNothing;
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
                Settings.Default.Exe = exe.Text;
                Settings.Default.Save();
                // Somewhat hacky, turn back on to revalidate in SetWarning
                randomize.Enabled = true;
            }
            SetWarning();
        }

        private void mergemod_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string mod = mergemod.Text.Trim();
                if (mod == "" || File.Exists(mod) || Directory.Exists(mod))
                {
                    Settings.Default.Mod = mod;
                    Settings.Default.Save();
                }
            }
            catch (ArgumentException) { }
        }

        private void exeButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog exeDialog = new OpenFileDialog())
            {
                exeDialog.Title = "Select Elden Ring install location";
                exeDialog.Filter = "Elden Ring exe|eldenring.exe";
                exeDialog.RestoreDirectory = true;
                try
                {
                    if (Directory.Exists(exe.Text))
                    {
                        exeDialog.InitialDirectory = exe.Text;
                    }
                    else
                    {
                        string gamePath = Path.GetDirectoryName(exe.Text);
                        if (Directory.Exists(gamePath))
                        {
                            exeDialog.InitialDirectory = gamePath;
                        }
                    }
                }
                catch (ArgumentException) { }
                if (exeDialog.ShowDialog() == DialogResult.OK)
                {
                    exe.Text = exeDialog.FileName;
                }
            }
        }

        private void mergeButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog modDialog = new OpenFileDialog())
            {
                modDialog.Title = "Select other mod";
                modDialog.Filter = "Regulation file|regulation.bin";
                modDialog.RestoreDirectory = true;
                try
                {
                    // TODO: See if this can be combined with other routine.
                    // InitialDirectory is poorly documented in its interaction with RestoreDirectory.
                    if (Directory.Exists(mergemod.Text))
                    {
                        modDialog.InitialDirectory = mergemod.Text;
                    }
                    else
                    {
                        string gamePath = Path.GetDirectoryName(mergemod.Text);
                        if (Directory.Exists(gamePath))
                        {
                            modDialog.InitialDirectory = gamePath;
                        }
                    }
                }
                catch (ArgumentException) { }
                if (modDialog.ShowDialog() == DialogResult.OK)
                {
                    mergemod.Text = Path.GetDirectoryName(modDialog.FileName);
                }
            }
        }

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
            List<string> restoreFiles = GameData.GetBackupFiles(gamePath);
            if (restoreFiles.Count == 0)
            {
                MessageBox.Show(messages.Get(restoreNothingToDo, gamePath), title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (ScrollDialog form = new ScrollDialog(messages.Get(restoreConfirmTitle), string.Join(Environment.NewLine, restoreFiles)))
            {
                form.Icon = Icon;
                DialogResult result = form.ShowDialog(this);
                if (result != DialogResult.OK)
                {
                    return;
                }
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
            using (ScrollDialog form = new ScrollDialog(title, string.Join(Environment.NewLine, restoreLog)))
            {
                form.Icon = Icon;
                form.ShowDialog(this);
            }
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
            return RandomizerOptionsFile.Create(Randomizer.EldenVersion, opt, selectedPreset);
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
                dialog.Filter = "Randomizer options files (*.randomizeopt)|*.randomizeopt|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != DialogResult.OK) return;
                RandomizerOptionsFile newOpts = RandomizerOptionsFile.Load(dialog.FileName);
                if (newOpts.Version != Randomizer.EldenVersion)
                {
                    DialogResult result = MessageBox.Show(
                        messages.Get(versionMismatch, newOpts.Version, Randomizer.EldenVersion),
                        messages.Get(PresetEditForm.confirmTitleText),
                        MessageBoxButtons.OKCancel);
                    if (result != DialogResult.OK)
                    {
                        return;
                    }
                }
                Preset enemyPreset = newOpts.PresetValue;
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
                        messages.Get(PresetEditForm.confirmTitleText),
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
                messages.Get(resetOptionsText), messages.Get(PresetEditForm.confirmTitleText), MessageBoxButtons.OKCancel);
            if (result == DialogResult.OK)
            {
                SetOptionsString(defaultOpts);
                // I guess also reset this? It's outside of normal options.
                mergemod.Text = "";
                UpdateAllOptions();
            }
        }

        private void CreateLaunchFile()
        {
            StringWriter writer = new StringWriter();
            RandomizerOptionsFile.Create(Randomizer.EldenVersion, options, selectedPreset).Save(writer);
            string hash = MiscSetup.GetMD5TextHash(writer.ToString());
            string formatLine(string name, string path)
            {
                return $"    {{ enabled = true, name = \"{name}\", path = {JsonConvert.ToString(path)} }},";
            }
            List<string> modLines = new List<string>();
            modLines.Add(formatLine("randomizer", Directory.GetCurrentDirectory()));
            if (!string.IsNullOrWhiteSpace(mergemod.Text))
            {
                DirectoryInfo modDir = new DirectoryInfo(mergemod.Text);
                if (modDir.Exists)
                {
                    modLines.Add(formatLine("mod", modDir.FullName));
                }
            }
            string file = $@"# DO NOT MODIFY THIS FILE!
# AUTO-GENERATED
# CONTENTS WILL BE AUTOMATICALLY OVERWRITTEN

# Produced by Elden Ring Randomizer {Randomizer.EldenVersion}
# <hash>{hash}</hash>

[modengine]
debug = false
[extension.mod_loader]
enabled = true
loose_params = false
mods = [
{string.Join(Environment.NewLine, modLines)}
]
";
            File.WriteAllText(modengineConfig, file);
        }

        [Localize]
        private static readonly Text launchMismatch = new Text(
            "Randomizer options appear to be changed from the last time randomizer was run. Launch anyway?", "EldenForm_launchMismatch");
        [Localize]
        private static readonly Text launchRunning = new Text(
            "Elden Ring appears to be already running, so launching with Mod Engine will have no effect. Try anyway?", "EldenForm_launchRunning");
        [Localize]
        private static readonly Text launchFailedTitle = new Text(
            "Launch failed", "EldenForm_launchFailedTitle");
        [Localize]
        private static readonly Text launchFailed = new Text(
            "Automatic Mod Engine launcher appeared to fail.\n\nMake sure Steam is running. If the game is currently running, close it and wait until it is fully shut down.\n\nCheck Troubleshooting on https://www.nexusmods.com/eldenring/mods/428/ to see when Mod Engine might fail. You may need to try using a manual Mod Engine launcher or UXM installation.", "EldenForm_launchFailed");

        private Process currentProcess = null;
        private async void launchButton_Click(object sender, EventArgs e)
        {
            if (currentProcess != null) return;
            UpdateLaunchGame();
            if (!launchButton.Enabled) return;

            StringWriter writer = new StringWriter();
            RandomizerOptionsFile.Create(Randomizer.EldenVersion, options, selectedPreset).Save(writer);
            string hash = MiscSetup.GetMD5TextHash(writer.ToString());

            string fileHash = null;
            Regex hashRe = new Regex(@"<hash>([0-9A-Za-z]+)</hash>");
            string contents = File.ReadAllText(modengineConfig);
            Match match = hashRe.Match(contents);
            if (match.Success)
            {
                fileHash = match.Groups[1].Value;
            }

            if (hash != fileHash)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(launchMismatch),
                    messages.Get(PresetEditForm.confirmTitleText),
                    MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK) return;
            }
            bool? checkRunning()
            {
                try
                {
                    return Process.GetProcessesByName("eldenring").Length > 0;
                }
                catch (Exception) { }
                return null;
            }
            if (checkRunning() ?? false)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(launchRunning),
                    messages.Get(PresetEditForm.confirmTitleText),
                    MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK) return;
            }

            using (Process process = new Process())
            {
                process.StartInfo.FileName = modengineLauncher;
                process.StartInfo.Arguments = $@"-t er -c ..\..\{modengineConfig}";
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(modengineLauncher);
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = true;

                // Start
                currentProcess = process;
                DateTime startTime = DateTime.Now;
                launchButton.BackColor = Color.LightYellow;

                process.Start();
                await WaitForExitAsync(process);

                int timePassed = (int)(DateTime.Now - startTime).TotalMilliseconds;
                // Originally this was a way of "if exited early, warn about broken",
                // but Mod Engine in non-debug mode exits early anyway, so check it regardless.
                if (timePassed < 5000)
                {
                    int waitRest = Math.Max(0, Math.Min(5000 - timePassed, 5000));
                    await Task.Delay(waitRest);
                }
                bool running = checkRunning() ?? true;
                if (!running)
                {
                    using (ScrollDialog form = new ScrollDialog(
                        messages.Get(launchFailedTitle),
                        messages.Get(launchFailed)))
                    {
                        form.Icon = Icon;
                        form.ShowDialog(this);
                    }
                }

                launchButton.BackColor = SystemColors.Control;
                currentProcess = null;
            }
        }

        // https://stackoverflow.com/questions/470256/process-waitforexit-asynchronously
        private static Task WaitForExitAsync(
            Process process,
            CancellationToken cancellationToken = default)
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (cancellationToken != default)
            {
                cancellationToken.Register(() => tcs.SetCanceled());
            }
            return process.HasExited ? Task.CompletedTask : tcs.Task;
        }

        private void updateButton_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.nexusmods.com/eldenring/mods/428?tab=files");
        }
    }
}
