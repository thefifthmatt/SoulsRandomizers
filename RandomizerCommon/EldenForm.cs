using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RandomizerCommon.Properties;
using static RandomizerCommon.Messages;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public partial class EldenForm : Form
    {
        // There is a lot of Enemy Randomizer placeholder here because we'll need it eventually.
        // TODO: Localization, especially of this string constant
        [Localize]
        private static readonly Text enemyseedPlaceholderText = new Text(
            "(same as overall seed)", "EldenForm_enemyseedPlaceholder");
        private static string defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe";

        private Messages messages;

        private RandomizerOptions options = new RandomizerOptions(FromGame.ER);
        private string defaultOpts = null;
        private HashSet<string> previousOpts = new HashSet<string>();
        private Dictionary<string, string> originalLabels = new Dictionary<string, string>();
        private bool enemyseedPlaceholder;
        private bool simultaneousUpdate;
        private bool working;
        private bool error;

        public EldenForm(bool dumpMessages = false, bool loadEmpty = false)
        {
            InitializeComponent();

            messages = new Messages("diste", loadEmpty);
            if (dumpMessages)
            {
                OptionsForm optionsForm = new OptionsForm(messages, "");
                DumpEnglishMessages(
                    new List<Form> { this, optionsForm },
                    new List<Type> {
                            typeof(EldenForm), typeof(OptionsForm), typeof(HintMarker),
                            typeof(MiscSetup), typeof(Permutation), typeof(Randomizer),
                    });
            }
            presetL.Text = "";
            enemyseed.GotFocus += enemyseed_TextChanged;
            enemyseed.LostFocus += enemyseed_TextChanged;

            RandomizerOptions initialOpts = new RandomizerOptions(FromGame.ER);
            SetControlFlags(this, initialOpts);
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
            // defaultRerollEnemy.Checked = defaultReroll.Checked && options.Seed2 != 0;

            // Now, update everything in the UI in-place
            SetStatus(null);
            if (!error)
            {
                SetWarning();
            }
            UpdateEnabled();
            UpdateLabels();

            if (messages.InitErrors.Count > 0)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, messages.InitErrors),
                    "Invalid messages", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            previousOpts = new HashSet<string>(defaultOpts.Split(' '));
            options = RandomizerOptions.Parse(previousOpts, FromGame.ER, isValidOption);

            // New defaults
            if (previousOpts.Contains("v1") || previousOpts.Contains("v2"))
            {
            }
            // Misc required options
            options["item"] = true;
            options["enemy"] = false;
            options["racemode"] = true;

            simultaneousUpdate = true;
            InsertControlFlags(this);
            difficulty.Value = options.Difficulty;
            simultaneousUpdate = false;

            fixedseed.Text = options.Seed == 0 ? "" : $"{options.Seed}";
            enemyseed.Text = options.Seed2 == 0 || options.Seed == options.Seed2 ? "" : $"{options.Seed2}";

            if (options.Preset == null)
            {
                SetPreset(null);
            }
            else
            {
                try
                {
                    Preset preset = Preset.LoadPreset(options.Preset, extractOopsAll: true);
                    SetPreset(preset);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    SetPreset(null);
                }
            }
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
            randomize.Enabled = !fatal;
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
            messages.SetFormText(this, originalText);
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
            if (control.Name.StartsWith("default")) return;
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
            if (control.Name.StartsWith("default")) return;
            if (control is RadioButton || control is CheckBox)
            {
                names.Add(control.Name);
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
            void setCheck(Control control, bool enabled, bool? maybeDefaultState, bool disabledState, string overrideDisable)
            {
                bool prevEnabled = control.Enabled;
                if (overrideDisable == null || options[overrideDisable])
                {
                    toEnable[control] = enabled;
                }
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
            // Make sure a single radio button of any group is checked
            if (!racemode_upgrades.Checked && !norandom_upgrades.Checked && !default_upgrades.Checked)
            {
                default_upgrades.Checked = true;
                changes = true;
            }
            foreach (KeyValuePair<Control, bool> enable in toEnable)
            {
                enable.Key.Enabled = enable.Value;
            }
            enemyseed_TextChanged(null, null);
            randomize.Enabled = (options["enemy"] || options["item"]) && !error;
            if (changes) SetControlFlags(this);
            simultaneousUpdate = false;
        }

        private static readonly Dictionary<string, (int, int)> checkLabels = new Dictionary<string, (int, int)>
        {
            ["racemodeL"] = (12, 0),
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

            Randomizer randomizer = new Randomizer();
            await Task.Factory.StartNew(() => {
                Directory.CreateDirectory("spoiler_logs");
                string seed2 = rand.Seed2 == 0 || rand.Seed2 == rand.Seed ? "" : $"_{rand.Seed2}";
                string runId = $"{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}_log_{rand.Seed}{seed2}_{rand.ConfigHash()}.txt";
                TextWriter log = File.CreateText($@"spoiler_logs\{runId}");
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(rand, FromGame.ER, status => { statusL.Text = status; }, messages: messages, preset: selectedPreset, gameExe: exe.Text);
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
            working = false;
        }

        private void optionwindow_Click(object sender, EventArgs e)
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
                    if (options.Seed != 0) default_reroll.Checked = false;

                    SetStatus(null);
                    UpdateEnabled();
                    UpdateLabels();
                    SaveOptions();
                }
            }
        }

        private Preset selectedPreset;
        private void preset_Click(object sender, EventArgs e)
        {
            using (PresetForm presetForm = new PresetForm("diste"))
            {
                presetForm.Icon = Icon;
                DialogResult result = presetForm.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    bool prevNull = selectedPreset == null;
                    SetPreset(presetForm.Preset);
                }
            }
        }

        private void SetPreset(Preset preset = null)
        {
            // TODO: Make this functional and localize it
            selectedPreset = preset;
            presetL.Text = selectedPreset == null ? "" : "Preset: " + selectedPreset.DisplayName;
            options.Preset = selectedPreset?.DisplayName;
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
        }

        // Copy this from Sekiro
        private void enemyseed_TextChanged(object sender, EventArgs e)
        {
            // Manage placeholder text, because winforms is bad
            if (enemyseed.Focused && enemyseed.Enabled && enemyseedPlaceholder)
            {
                enemyseed.Text = "";
                enemyseed.ForeColor = SystemColors.WindowText;
                enemyseedPlaceholder = false;
            }
            if (!enemyseed.Focused && string.IsNullOrWhiteSpace(enemyseed.Text))
            {
                enemyseed.Text = messages.Get(enemyseedPlaceholderText);
                enemyseed.ForeColor = SystemColors.GrayText;
                enemyseedPlaceholder = true;
            }
            // Manage checkbox
            if (enemyseedPlaceholder || enemyseed.Text == "")
            {
                // I guess let it be separate from enemy seed
                defaultRerollEnemy.Enabled = enemyseed.Enabled;
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
            reroll_CheckedChanged(null, null);
        }

        [Localize]
        private static readonly Text randomizeNew = new Text(
            "Randomize new run!",
            "EldenForm_randomizeNew");
        [Localize]
        private static readonly Text randomizeFixed = new Text(
            "Run with fixed seed",
            "EldenForm_randomizedFixed");
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
            Text text;
            if (default_reroll.Checked)
            {
                if (enemy.Checked && !defaultRerollEnemy.Checked && uint.TryParse(enemyseed.Text.Trim(), out _))
                {
                    text = randomizeItemOnly;
                }
                else
                {
                    text = randomizeNew;
                }
            }
            else
            {
                if (enemy.Checked && defaultRerollEnemy.Checked)
                {
                    text = randomizeEnemyOnly;
                }
                else
                {
                    text = randomizeFixed;
                }
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
            }
            SetWarning();
        }

        private void exeButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog exeDialog = new OpenFileDialog();
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

        private void restoreButton_Click(object sender, EventArgs e)
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
    }
}
