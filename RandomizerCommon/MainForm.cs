using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RandomizerCommon.Properties;

namespace RandomizerCommon
{
    public partial class MainForm : Form
    {
        private RandomizerOptions options = new RandomizerOptions(false);
        private string defaultOpts = null;
        private HashSet<string> previousOpts = new HashSet<string>();
        private Color initialColor;
        private Color dangerColor = Color.IndianRed;
        private bool simultaneousUpdate;
        private bool working;
        private bool error;
        private bool encrypted;

        public MainForm()
        {
            InitializeComponent();
            initialColor = BackColor;

            if (!MiscSetup.CheckRequiredDS3Files(out string req))
            {
                SetError(req, true);
            }
            else
            {
                SetWarning();
            }
            SetStatus(null);

            // The rest of initialization
            RandomizerOptions initialOpts = new RandomizerOptions(false);
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
                if (uint.TryParse(s, out var ignored)) return true;
                return false;
            }
            previousOpts = new HashSet<string>(defaultOpts.Split(' '));
            options = RandomizerOptions.Parse(previousOpts, false, isValidOption);

            simultaneousUpdate = true;
            InsertControlFlags(this);
            difficulty.Value = options.Difficulty;
            simultaneousUpdate = false;

            fixedseed.Text = options.Seed == 0 ? "" : $"{options.Seed}";
        }

        private void SetWarning()
        {
            bool fatal = MiscSetup.CheckDS3ModEngine(out string err, out encrypted);
#if DEBUG
            fatal = false;
#endif
            SetError(err, fatal);
        }

        private void SetError(string text, bool fatal = false)
        {
            warningL.Text = text ?? "";
            warningL.Visible = true;
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
            SetStatus(null);
            SaveOptions();
        }

        private void reroll_CheckedChanged(object sender, EventArgs e)
        {
            if (defaultReroll.Checked)
            {
                randomize.Text = "Randomize new run!";
            }
            else
            {
                randomize.Text = "Run with fixed seed";
            }
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

        private void UpdateEnabled()
        {
            simultaneousUpdate = true;
            bool changes = false;
            void setCheck(Control control, bool enabled)
            {
                control.Enabled = enabled;
                if (control is CheckBox check && check.Checked && !enabled)
                {
                    check.Checked = false;
                    changes = true;
                }
                else if (control is RadioButton radio && radio.Checked && !enabled)
                {
                    radio.Checked = false;
                    changes = true;
                }
            };
            setCheck(earlydlc, options["dlc1"]);
            setCheck(vilhelmskip, options["dlc1"]);
            setCheck(dlc2fromdlc1, options["dlc1"] && options["dlc2"]);
            setCheck(dlc2fromkiln, options["dlc2"]);
            setCheck(racemode_health, options["racemode"]);
            if (!dlc2fromdlc1.Checked && !dlc2fromkiln.Checked)
            {
                if (dlc2fromdlc1.Enabled)
                {
                    dlc2fromdlc1.Checked = true;
                    changes = true;
                }
                else if (dlc2fromkiln.Enabled)
                {
                    dlc2fromkiln.Checked = true;
                    changes = true;
                }
            }
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
            earlylothricL.Text = $"Fight Dancer at {dancerLevel} soul level with {dancerWeapon} weapon";
            string friedeEstus = options["estusprogression"] ? "most" : "no guaranteed";
            string friedeWeapon = "+10";
            string friedeLevel = "high";
            if (options["earlydlc"])
            {
                friedeLevel = "medium";
                friedeWeapon = "+7";
            }
            if (!options["weaponprogression"]) friedeWeapon = "no guaranteed";
            earlydlcL.Text = $"Fight Friede at {friedeLevel} soul level, {friedeEstus} estus, and {friedeWeapon} weapon";
            // Fun with colors
            if (options.Difficulty <= 85)
            {
                BackColor = initialColor;
            }
            else
            {
                // Ugly blending. But because there's only one hue involved, it's ok
                double blend = (options.Difficulty - 85) / 15.0;
                // A bit less strong to preserve readability
                blend /= 2;
                BackColor = Color.FromArgb(
                    (int)(dangerColor.R * blend + initialColor.R * (1 - blend)),
                    (int)(dangerColor.G * blend + initialColor.G * (1 - blend)),
                    (int)(dangerColor.B * blend + initialColor.B * (1 - blend)));
            }
        }

        private async void randomize_Click(object sender, EventArgs e)
        {
            if (working) return;
            SetWarning();
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
                options.Seed = (uint)new Random().Next();
            }
            SaveOptions();
            RandomizerOptions rand = options.Copy();
            working = true;
            string buttonText = randomize.Text;
            randomize.Text = $"Running...";
            randomize.BackColor = Color.LightYellow;
            fixedseed.Text = $"{rand.Seed}";

            Randomizer randomizer = new Randomizer();
            await Task.Factory.StartNew(() => {
                Directory.CreateDirectory("spoiler_logs");
                string runId = $"{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}_log_{rand.Seed}_{rand.ConfigHash()}.txt";
                TextWriter log = File.CreateText($@"spoiler_logs\{runId}");
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(rand, status => { statusL.Text = status; }, encrypted: encrypted);
                    SetStatus($"Done! Hints and spoilers in spoiler_logs directory as {runId} - Restart your game!!", success: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    SetError($"Error encountered: {ex.Message}\r\nIt may work to try again with a different seed. See most recent file in spoiler_logs directory for the full error.");
                    SetStatus($"Error! Partial log in spoiler_logs directory as {runId}", true);
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

        private void option_alwaysEnable(object sender, EventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            box.Checked = true;
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

        private void optionwindow_Click(object sender, EventArgs e)
        {
            using (OptionsForm form = new OptionsForm(options.FullString()))
            {
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
    }
}
