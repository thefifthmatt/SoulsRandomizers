using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SoulsIds;
using static SoulsIds.GameSpec;
using static RandomizerCommon.Messages;

namespace RandomizerCommon
{
    public partial class MainForm : Form
    {
        private static readonly string enemySeedPlaceholder = "(same as overall seed)";
        private static string defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\DarkSoulsIII.exe";
        private static string gameDirSubstring = @"DARK SOULS III\Game\";

        private Messages messages = new Messages(null);
        private RandomizerOptions options = new RandomizerOptions(FromGame.DS3);
        private ModRunner Runner;
        private List<string> extraDlls = new();
        private string defaultOpts = null;
        private HashSet<string> previousOpts = new HashSet<string>();
        private bool simultaneousUpdate;
        private bool working;
        private bool error;

        public MainForm()
        {
            // TODO: Load exe, dlls
            // Heuristics for initial dir, also looking at parent dir

            InitializeComponent();
            Text = $"DS3 Static Item and Enemy Randomizer " + Randomizer.DS3Version;
            Runner = new ModRunner(
                FromGame.DS3,
                "config_ds3randomizer.toml", @"dist\ModEngine2\modengine2_launcher.exe");
            if (!MiscSetup.CheckRequiredDS3Files(messages, out string req))
            {
                SetError(req, true);
            }
            // SetWarning is only about exe option, not file setup, so don't call it here
            presetL.Text = "";
            enemyseed.GotFocus += enemyseed_TextChanged;
            enemyseed.LostFocus += enemyseed_TextChanged;

            // The rest of initialization
            RandomizerOptions initialOpts = new RandomizerOptions(FromGame.DS3);
            SetControlFlags(this, initialOpts);
            defaultOpts = initialOpts.FullString();

            string existingOpts = RandomizerOptions.ReadOptions();
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

            extraDlls = RandomizerOptions.ReadExtraDlls() ?? new();
            string defaultExe = RandomizerOptions.ReadExe();
            if (string.IsNullOrEmpty(defaultExe))
            {
                // A bit awkward, but look in parent dir since that was previously the required setup
                string gameDir = Directory.GetCurrentDirectory();
                int gameDirIndex = gameDir.LastIndexOf(gameDirSubstring, StringComparison.InvariantCultureIgnoreCase);
                if (gameDirIndex != -1)
                {
                    gameDir = gameDir.Substring(0, gameDirIndex + gameDirSubstring.Length) + "DarkSoulsIII.exe";
                    if (File.Exists(gameDir))
                    {
                        defaultExe = gameDir;
                    }
                }
            }
            if (string.IsNullOrEmpty(defaultExe) && File.Exists(defaultPath))
            {
                defaultExe = defaultPath;
            }
            if (!string.IsNullOrEmpty(defaultExe))
            {
                exe.Text = defaultExe;
            }

            // defaultRerollEnemy.Checked = defaultReroll.Checked && options.Seed2 != 0;
            SetStatus(null);

            UpdateEnabled();
            UpdateLabels();
            UpdateRandomizeButtonText();
            UpdateExtraDlls();

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
        }

        private static readonly Text launchFailedTitle = new Text(
            "Launch failed", "MainForm_launchFailedTitle");
        private static readonly Text launchFailed = new Text(
            "Built-in Mod Engine launcher appeared to fail.\n\nMake sure Steam is running. If the game is currently running, close it and wait until it is fully shut down.\n\nCheck installation instructions on https://www.nexusmods.com/darksouls3/mods/361 to see when Mod Engine might fail. You may need to try using a manual Mod Engine launcher.", "MainForm_launchFailed");

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
            if (options.StoredVersion < 4)
            {
                options["item"] = true;
                options["enemy"] = true;
                options["mimics"] = true;
                options["lizards"] = true;
                options["earlyreq"] = true;
                options["scale"] = true;
                options["edittext"] = true;
            }
            if (options.StoredVersion < 5)
            {
                options["raceloc_health"] = true;
                options["phasehp"] = true;
                options["yhormruler"] = true;
                options["supermimic"] = true;
                options["crashfix"] = true;
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
                    EnemyPreset preset = EnemyPreset.LoadPreset(options.Preset, extractOopsAll: true);
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
            bool fatal = MiscSetup.CheckDS3Mods(messages, exe.Text, out string err);
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
                randomizeButton.Enabled = false;
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
            setCheck(earlydlc, options["dlc1"] && !options["dlckeysilo"], false, false, "item");
            setCheck(dlc2fromdlc1, options["dlc1"] && options["dlc2"], true, false, "item");
            // setCheck(racemode_health, options["racemode"], false, false, "item");

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
            checkDll("crashfix", HelperForm.FixDllPath);
            if (dllChanged)
            {
                UpdateExtraDlls();
            }

            if (!racemode_health.Checked && !norandom_health.Checked && !defaultHealth.Checked)
            {
                racemode_health.Checked = true;
                changes = true;
            }
            if (!racemode.Checked && !norandom.Checked && !defaultKey.Checked)
            {
                racemode.Checked = true;
                changes = true;
            }
            foreach (KeyValuePair<Control, bool> enable in toEnable)
            {
                enable.Key.Enabled = enable.Value;
            }

            enemyseed_TextChanged(null, null);
            randomizeButton.Enabled = (options["enemy"] || options["item"]) && !error;
            if (changes) SetControlFlags(this);
            UpdateLaunchGame();
            simultaneousUpdate = false;
        }

        private void UpdateLabels()
        {
            string unfairText = "";
            RandomizerOptions.BiasWeights weights = options.GetWeights();
            if (weights.VeryUnfair > 0.5) unfairText = " and very unfair";
            else if (weights.Unfair > 0.5) unfairText = " and unfair";
            string loc;
            if (weights.SpreadRatio > 0.7) loc = $"Much better rewards for difficult and late{unfairText} locations.";
            else if (weights.SpreadRatio > 0.3) loc = $"Better rewards for difficult and late{unfairText} locations.";
            else if (weights.SpreadRatio > 0.1) loc = $"Slightly better rewards for difficult and late{unfairText} locations.";
            else if (weights.SpreadRatio > 0.001) loc = "Most locations for items are equally likely.";
            else loc = "All possible locations for items are equally likely.";
            string chain;
            if (weights.KeyItemChain <= 3) chain = "Key items will usually be easy to find and not require much side content.";
            else if (weights.KeyItemChain <= 4.001) chain = "Key items will usually be in different areas and depend on each other.";
            else if (weights.KeyItemChain <= 10) chain = "Key items will usually be in different areas and form interesting chains.";
            else chain = "Key items will usually form long chains across different areas.";
            if (options["norandom"]) chain = "";
            difficultyL.Text = $"{loc}\r\n{chain}";
            difficultyAmtL.Text = $"{options.Difficulty}%";
            string weaponText = "Comparable difficulty to base game";
            string estusText = "Comparable difficulty to base game";
            string soulsText = "Comparable difficulty to base game";
            if (!options["weaponprogression"])
            {
                if (weights.SpreadRatio > 0.3) weaponText = "May be more difficult than base game";
                else if (weights.SpreadRatio < 0.2) weaponText = "Easier than base game";
            }
            if (!options["estusprogression"])
            {
                if (weights.SpreadRatio > 0.5) estusText = "You will get almost no estus upgrades until the very end of the game";
                else if (weights.SpreadRatio > 0.15) estusText = "More difficult than base game";
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

            chests.Text = "Turn all chests into " + (options["mimics"] ? "random enemies" : "mimics");
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
            string buttonText = randomizeButton.Text;
            randomizeButton.Text = $"Running...";
            randomizeButton.BackColor = Color.LightYellow;
            fixedseed.Text = $"{rand.Seed}";
            if (newEnemySeed)
            {
                enemyseed.Text = rand.Seed2.ToString();
                enemyseed.ForeColor = SystemColors.WindowText;
            }

            Runner.DeleteLaunchFile();
            UpdateLaunchGame();
            // Create save file backup
            MiscSetup.ModifyDS3Files();

            string modDir = null;
            if (rand["mergemods"])
            {
                // Previous Elden Ring UXM merge behavior
                // modDir = Path.GetDirectoryName(gameExe);
                string modPath = "mod";
                DirectoryInfo modDirInfo = new DirectoryInfo($@"..\{modPath}");
                modDir = modDirInfo.FullName;
            }
            MergedMods mods = new MergedMods(modDir);

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
                    wrapInvoke(() =>
                    {
                        SetError(messages.Get(MiscSetup.CreateFileError, ex.Message));
                        SetStatus("Error encountered", true);
                    });
                    return;
                }
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(rand, FromGame.DS3, status => { wrapInvoke(() => statusL.Text = status); }, gameExe: exe.Text, modDirs: mods, enemyPreset: selectedPreset);
                    CreateLaunchFile(mods);
                    wrapInvoke(() =>
                    {
                        SetStatus($"Done! Hints and spoilers in spoiler_logs directory as {runId} - Restart your game!!", success: true);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    wrapInvoke(() =>
                    {
                        SetError($"Error encountered: {ex.Message}\r\nIt may work to try again with a different seed. See most recent file in spoiler_logs directory for the full error.");
                        SetStatus($"Error! Partial log in spoiler_logs directory as {runId}", true);
                    });
                }
                finally
                {
                    log.Close();
                    Console.SetOut(stdout);
                }
            });
            randomizeButton.Text = buttonText;
            randomizeButton.BackColor = SystemColors.Control;
            randomizeButton.UseVisualStyleBackColor = true;
            UpdateLaunchGame();
            working = false;
            // Postprocess button, to switch between set seed and randomize
            UpdateRandomizeButtonText();
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
                form.StartPosition = FormStartPosition.CenterParent;
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

        private EnemyPreset selectedPreset;
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

        private void SetPreset(EnemyPreset preset = null)
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
            else if (!simultaneousUpdate)
            {
                // Avoid doing this here when loading things in, but do it when manually typing/pasting
                defaultReroll.Checked = false;
            }
            UpdateRandomizeButtonText();
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
            UpdateRandomizeButtonText();
        }

        private void reroll_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRandomizeButtonText();
        }

        private void UpdateRandomizeButtonText()
        {
            if (working) return;
            string text;
            if (enemy.Checked && item.Checked)
            {
                text = "Randomize items and enemies";
                if (defaultReroll.Checked)
                {
                    if (!defaultRerollEnemy.Checked && uint.TryParse(enemyseed.Text.Trim(), out _))
                    {
                        text = "Reroll items (same enemies)";
                    }
                }
                else
                {
                    if (defaultRerollEnemy.Checked)
                    {
                        text = "Reroll enemies (same items)";
                    }
                    else
                    {
                        text = "Run with set seed";
                    }
                }
            }
            else if (enemy.Checked)
            {
                text = "Randomize enemies";
            }
            else if (item.Checked)
            {
                text = "Randomize items";
            }
            else
            {
                text = "Run";
            }
            randomizeButton.Text = text;
        }

        private void UpdateLaunchGame()
        {
            bool valid = Runner.IsValid();
            launchButton.Enabled = valid;
        }

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
                string exeName = Path.GetFileName(exe.Text);
                if (!exeName.Equals("DarkSoulsIII.exe", StringComparison.InvariantCultureIgnoreCase))
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
                randomizeButton.Enabled = true;
            }
            SetWarning();
        }

        private void exeButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select DS3 install location";
                dialog.Filter = "DS3 exe|DarkSoulsIII.exe";
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

        private void dllButton_Click(object sender, EventArgs e)
        {
            List<string> newDlls = DllForm.Show(this, messages, extraDlls, "DS3");
            if (newDlls != null)
            {
                if (!extraDlls.SequenceEqual(newDlls))
                {
                    Runner.DeleteLaunchFile();
                    UpdateLaunchGame();
                }
                extraDlls = newDlls;
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
                dllInfoL.Text = "Using " + string.Join(", ", extraDlls.Select(Path.GetFileName).Distinct());
            }
            RandomizerOptions.SaveExtraDlls(extraDlls);
        }

        private void CreateLaunchFile(MergedMods mods)
        {
            List<string> comments = new List<string>
            {
                $"Produced by DS3 Randomizer",
            };
            Runner.CreateLaunchFile(comments, MiscSetup.GetModEngineOutputDirectory(), mods, extraDlls);
        }

        private async void launchButton_Click(object sender, EventArgs e)
        {
            UpdateLaunchGame();
            if (!launchButton.Enabled || Runner.IsLaunching()) return;

            if (Runner.IsGameRunning())
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(launchRunning),
                    messages.Get(FormText.ConfirmTitleText),
                    MessageBoxButtons.OKCancel);
                if (result != DialogResult.OK) return;
            }

            await Runner.LaunchGame();
        }

        private static readonly Text launchRunning = new Text(
            "DS3 Ring appears to be already running, so launching with Mod Engine will have no effect. Try anyway?", "MainForm_launchRunning");
    }
}
