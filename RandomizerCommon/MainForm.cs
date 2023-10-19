using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RandomizerCommon.Properties;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public partial class MainForm : Form
    {
        private static readonly string enemySeedPlaceholder = "(same as overall seed)";

        private Messages messages = new Messages(null);
        private RandomizerOptions options = new RandomizerOptions(FromGame.DS3);
        private string defaultOpts = null;
        private HashSet<string> previousOpts = new HashSet<string>();
        private bool simultaneousUpdate;
        private bool working;
        private bool error;
        private bool encrypted;

        public MainForm()
        {
            InitializeComponent();
            if (!MiscSetup.CheckRequiredDS3Files(out string req))
            {
                SetError(req, true);
            }
            else
            {
                SetWarning();
            }
            SetStatus(null);
            presetL.Text = "";
            enemyseed.GotFocus += enemyseed_TextChanged;
            enemyseed.LostFocus += enemyseed_TextChanged;

            // The rest of initialization
            RandomizerOptions initialOpts = new RandomizerOptions(FromGame.DS3);
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
                    defaultReroll.Enabled = true;
                    defaultReroll.Checked = false;
                }
            }
            // defaultRerollEnemy.Checked = defaultReroll.Checked && options.Seed2 != 0;
            SetStatus(null);

            UpdateEnabled();
            UpdateLabels();
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
            options = RandomizerOptions.Parse(previousOpts, FromGame.DS3, isValidOption);

            // New defaults
            if (previousOpts.Contains("v2") || previousOpts.Contains("v3"))
            {
                options["item"] = true;
                options["enemy"] = true;
                options["mimics"] = true;
                options["lizards"] = true;
                options["earlyreq"] = true;
                options["scale"] = true;
                options["edittext"] = true;
            }

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
            bool fatal = MiscSetup.CheckDS3ModEngine(enemy.Checked, out string err, out encrypted);
            SetError(err, fatal);
        }

        private void SetError(string text, bool fatal = false)
        {
            warningL.Text = text ?? "";
            warningL.Visible = true;
#if DEBUG
            fatal = false;
#endif
            if (fatal)
            {
                randomize.Enabled = false;
                error = true;
            }
        }

        private void SetStatus(string msg, bool error = false, bool success = false)
        {
            if (msg == null)
            {
                msg = $"Created by thefifthmatt. Current config hash: {options.ConfigHash()}";
            }
            statusL.Text = msg;
            statusStrip1.BackColor = error ? Color.IndianRed : (success ? Color.PaleGreen : SystemColors.Control);
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
            void setCheck(Control control, bool enabled, bool defaultState, bool disabledState, string overrideDisable)
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
                else if (enabled && !prevEnabled && prevChecked != defaultState)
                {
                    if (check != null) check.Checked = defaultState;
                    else radio.Checked = defaultState;
                    changes = true;
                }
            };
            setCheck(earlydlc, options["dlc1"], false, false, "item");
            setCheck(dlc2fromdlc1, options["dlc1"] && options["dlc2"], true, false, "item");
            setCheck(racemode_health, options["racemode"], false, false, "item");
            if (!racemode_health.Checked && !norandom_health.Checked)
            {
                defaultHealth.Checked = true;
                changes = true;
            }
            if (!racemode.Checked && !norandom.Checked)
            {
                defaultKey.Checked = true;
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

        private void UpdateLabels()
        {
            string unfairText = "";
            if (options.GetNum("veryunfairweight") > 0.5) unfairText = " and very unfair";
            else if (options.GetNum("unfairweight") > 0.5) unfairText = " and unfair";
            string loc;
            if (options.GetNum("allitemdifficulty") > 0.7) loc = $"Much better rewards for difficult and late{unfairText} locations.";
            else if (options.GetNum("allitemdifficulty") > 0.3) loc = $"Better rewards for difficult and late{unfairText} locations.";
            else if (options.GetNum("allitemdifficulty") > 0.1) loc = $"Slightly better rewards for difficult and late{unfairText} locations.";
            else if (options.GetNum("allitemdifficulty") > 0.001) loc = "Most locations for items are equally likely.";
            else loc = "All possible locations for items are equally likely.";
            string chain;
            if (options.GetNum("keyitemchainweight") <= 3) chain = "Key items will usually be easy to find and not require much side content.";
            else if (options.GetNum("keyitemchainweight") <= 4.001) chain = "Key items will usually be in different areas and depend on each other.";
            else if (options.GetNum("keyitemchainweight") <= 10) chain = "Key items will usually be in different areas and form interesting chains.";
            else chain = "Key items will usually form long chains across different areas.";
            if (options["norandom"]) chain = "";
            difficultyL.Text = $"{loc}\r\n{chain}";
            difficultyAmtL.Text = $"{options.Difficulty}%";
            string weaponText = "Comparable difficulty to base game";
            string estusText = "Comparable difficulty to base game";
            string soulsText = "Comparable difficulty to base game";
            if (!options["weaponprogression"])
            {
                if (options.GetNum("allitemdifficulty") > 0.3) weaponText = "May be more difficult than base game";
                else if (options.GetNum("allitemdifficulty") < 0.2) weaponText = "Easier than base game";
            }
            if (!options["estusprogression"])
            {
                if (options.GetNum("allitemdifficulty") > 0.5) estusText = "You will get almost no estus upgrades until the very end of the game";
                else if (options.GetNum("allitemdifficulty") > 0.15) estusText = "More difficult than base game";
            }
            if (!options["soulsprogression"])
            {
                soulsText = "Easier than base game";
            }
            weaponprogressionL.Text = weaponText;
            estusprogressionL.Text = estusText;
            soulsprogressionL.Text = soulsText;
            string dancerLevel = "high";
            string dancerWeapon = "+7";
            if (options["earlylothric"])
            {
                dancerLevel = "low";
                dancerWeapon = "+3 to +5";
            }
            else if (options["middancer"]) dancerLevel = "medium";
            if (!options["weaponprogression"]) dancerWeapon = "no guaranteed";
            earlylothricL.Text = $"May require Dancer at {dancerLevel} soul level with {dancerWeapon} weapon";
            string friedeEstus = options["estusprogression"] ? "most" : "no guaranteed";
            string friedeWeapon = "+10";
            string friedeLevel = "high";
            if (options["earlydlc"])
            {
                friedeLevel = "medium";
                friedeWeapon = "+7";
            }
            if (!options["weaponprogression"]) friedeWeapon = "no guaranteed";
            earlydlcL.Text = $"May require Friede at {friedeLevel} soul level, {friedeEstus} estus, and {friedeWeapon} weapon";

            chests.Text = "Turn all chests into mimics" + (options["mimics"] ? " (randomized)" : "");
        }

        private async void randomize_Click(object sender, EventArgs e)
        {
            if (working) return;
            SetWarning();
            if (error) return;
            Random seedRandom = new Random();
            if (!defaultReroll.Checked && fixedseed.Text.Trim() != "")
            {
                if (uint.TryParse(fixedseed.Text.Trim(), out uint seed))
                {
                    options.Seed = seed;
                }
                else
                {
                    SetStatus("Invalid fixed seed", true);
                    return;
                }
            }
            else
            {
                options.Seed = (uint)seedRandom.Next();
            }
            bool newEnemySeed = false;
            if (defaultRerollEnemy.Enabled && !defaultRerollEnemy.Checked && enemyseed.Text.Trim() != "" && enemyseed.Text != enemySeedPlaceholder)
            {
                if (uint.TryParse(enemyseed.Text.Trim(), out uint seed))
                {
                    options.Seed2 = seed;
                }
                else
                {
                    SetStatus("Invalid enemy seed", true);
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
            SaveOptions();
            RandomizerOptions rand = options.Copy();
            working = true;
            string buttonText = randomize.Text;
            randomize.Text = $"Running...";
            randomize.BackColor = Color.LightYellow;
            fixedseed.Text = $"{rand.Seed}";
            if (newEnemySeed)
            {
                enemyseed.Text = rand.Seed2.ToString();
                enemyseed.ForeColor = SystemColors.WindowText;
            }

            Randomizer randomizer = new Randomizer();
            string seed2 = rand.Seed2 == 0 || rand.Seed2 == rand.Seed ? "" : $"_{rand.Seed2}";
            string runId = $"{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}_log_{rand.Seed}{seed2}_{rand.ConfigHash()}.txt";
            Exception ex = null;
            await Task.Factory.StartNew(() => {
                Directory.CreateDirectory("spoiler_logs");
                TextWriter log = File.CreateText($@"spoiler_logs\{runId}");
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(rand, SoulsIds.GameSpec.FromGame.DS3, status => { statusL.Text = status; }, preset: selectedPreset, encrypted: encrypted);
                }
                catch (Exception ex_)
                {
                    ex = ex_;
                    Console.WriteLine(ex);
                }
                finally
                {
                    log.Close();
                    Console.SetOut(stdout);
                }
            });

            // The actual randomization logic is done in a Task, but the main UI's components can't
            // be modified from a Task so we have to update them outside it.
            if (ex != null)
            {
                SetError($"Error encountered: {ex.Message}\r\nIt may work to try again with a different seed. See most recent file in spoiler_logs directory for the full error.");
                SetStatus($"Error! Partial log in spoiler_logs directory as {runId}", true);
            }
            else
            {
                SetStatus($"Done! Hints and spoilers in spoiler_logs directory as {runId} - Restart your game!!", success: true);
            }

            randomize.Text = buttonText;
            randomize.BackColor = SystemColors.Control;
            working = false;
        }

        private void option_alwaysEnable(object sender, EventArgs e)
        {
            // For always-on racemode categories
            CheckBox box = (CheckBox)sender;
            box.Checked = true;
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
                    if (string.IsNullOrEmpty(text) || int.TryParse(text, out var _) || !text.Contains("v"))
                    {
                        SetStatus("Invalid options string", error: true);
                        return;
                    }
                    SetOptionsString(text);
                    if (options.Seed != 0) defaultReroll.Checked = false;

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
            using (PresetForm presetForm = new PresetForm("dist"))
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
            selectedPreset = preset;
            presetL.Text = selectedPreset == null ? "" : "Preset: " + selectedPreset.DisplayName;
            options.Preset = selectedPreset?.DisplayName;
            SaveOptions();
            SetStatus(null);
        }

        private void fixedseed_TextChanged(object sender, EventArgs e)
        {
            string text = fixedseed.Text.Trim();
            defaultReroll.Enabled = uint.TryParse(text, out uint val) && val != 0;
            if (!defaultReroll.Enabled)
            {
                defaultReroll.Checked = true;
            }
        }

        // Copy this from Sekiro
        private void enemyseed_TextChanged(object sender, EventArgs e)
        {
            // Manage placeholder text, because winforms is bad
            if (enemyseed.Focused && enemyseed.Enabled && enemyseed.Text == enemySeedPlaceholder)
            {
                enemyseed.Text = "";
                enemyseed.ForeColor = SystemColors.WindowText;
            }
            if (!enemyseed.Focused && string.IsNullOrWhiteSpace(enemyseed.Text))
            {
                enemyseed.Text = enemySeedPlaceholder;
                enemyseed.ForeColor = SystemColors.GrayText;
            }
            // Manage checkbox
            if (enemyseed.Text == enemySeedPlaceholder || enemyseed.Text == "")
            {
                // I guess let it be separate from enemy seed
                defaultRerollEnemy.Enabled = enemyseed.Enabled;
                return;
            }
            string text = enemyseed.Text.Trim();
            bool valid = uint.TryParse(text, out uint val) && val != 0;
            if (defaultReroll.Checked)
            {
                defaultRerollEnemy.Enabled = valid;
                if (!valid && options.Seed2 == 0)
                {
                    defaultRerollEnemy.Checked = true;
                }
            }
            reroll_CheckedChanged(null, null);
        }

        private void reroll_CheckedChanged(object sender, EventArgs e)
        {
            if (defaultReroll.Checked)
            {
                if (enemy.Checked && !defaultRerollEnemy.Checked && uint.TryParse(enemyseed.Text.Trim(), out _))
                {
                    randomize.Text = "Reroll items (same enemies)";
                }
                else
                {
                    randomize.Text = "Randomize new run!";
                }
            }
            else
            {
                if (enemy.Checked && defaultRerollEnemy.Checked)
                {
                    randomize.Text = "Reroll enemies (same items)";
                }
                else
                {
                    randomize.Text = "Run with fixed seed";
                }
            }
        }

        private void archipelagoButton_Click(object sender, EventArgs e)
        {
            var dialog = new ArchipelagoForm();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                SetStatus("Archipelago config loaded into game!");
            }
        }
    }
}
