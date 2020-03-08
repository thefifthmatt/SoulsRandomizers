using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RandomizerCommon
{
    public partial class MainForm : Form
    {
        private RandomizerOptions options = new RandomizerOptions(false);
        private Color initialColor;
        private Color dangerColor = Color.IndianRed;
        private bool simultaneousUpdate;

        public MainForm()
        {
            InitializeComponent();
            initialColor = BackColor;
            // One-time initialization for directory position
            if (File.Exists("DarkSoulsIII.exe"))
            {
                randomize.Enabled = false;
                warningL.Visible = true;
                warningL.Text = "Running from same directory as DarkSoulsIII.exe\r\nRun from randomizer subdirectory instead";
            }
            else if (!File.Exists(@"..\DarkSoulsIII.exe"))
            {
                warningL.Visible = true;
                warningL.Text = "DarkSoulsIII.exe not found in parent directory\r\nRandomization may not apply to game";
            }
            // The rest of initialization
            options.Difficulty = difficulty.Value;
            SetControlFlags(this);
            UpdateLabels();
        }

        private void difficulty_Scroll(object sender, EventArgs e)
        {
            options.Difficulty = difficulty.Value;
            UpdateLabels();
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
        }

        private void SetControlFlags(Control control)
        {
            if (control is RadioButton radio)
            {
                options[control.Name] = radio.Checked;
            }
            else if (control is CheckBox check)
            {
                options[control.Name] = check.Checked;
            }
            else
            {
                foreach (Control sub in control.Controls)
                {
                    SetControlFlags(sub);
                }
            }
        }

        private void UpdateEnabled()
        {
            simultaneousUpdate = true;
            bool changes = false;
            Action<CheckBox, Boolean> setCheck = (check, enabled) =>
            {
                check.Enabled = enabled;
                if (check.Checked && !enabled)
                {
                    check.Checked = false;
                    changes = true;
                }
            };
            setCheck(earlyfriede, options["dlc1"]);
            setCheck(vilhelmskip, options["dlc1"]);
            setCheck(dlc2fromdlc1, options["dlc1"] && options["dlc2"]);
            setCheck(dlc2fromkiln, options["dlc2"]);
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
            if (options.GetNum("keyitemchainweight") == 1) chain = "Key items may depend on each other. The game can sometimes be completed very early.";
            else if (options.GetNum("keyitemchainweight") <= 4.001) chain = "Key items will usually be in different areas and depend on each other.";
            else if (options.GetNum("keyitemchainweight") <= 10) chain = "Key items will usually be in different areas and form interesting chains.";
            else chain = "Key items will usually form long chains across different areas.";
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
                if (options.GetNum("allitemdifficulty") > 0.5) estusText = "Unreasonably difficult compared to base game";
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
            if (options["earlyfriede"])
            {
                friedeLevel = "mid";
                friedeWeapon = "+7";
            }
            if (!options["weaponprogression"]) friedeWeapon = "no guaranteed";
            earlyfriedeL.Text = $"Fight Friede at {friedeLevel} soul level, {friedeEstus} estus, and {friedeWeapon} weapon";
            // Fun with colors
            if (options.Difficulty <= 85)
            {
                BackColor = initialColor;
                randomizeL.BackColor = BackColor;
            }
            else
            {
                // Ugly blending. But because there's only one hue involved, it's ok
                double blend = (options.Difficulty - 85) / 15.0;
                BackColor = Color.FromArgb(
                    (int)(dangerColor.R * blend + initialColor.R * (1 - blend)),
                    (int)(dangerColor.G * blend + initialColor.G * (1 - blend)),
                    (int)(dangerColor.B * blend + initialColor.B * (1 - blend)));
                randomizeL.BackColor = BackColor;
            }
        }

        private async void randomize_Click(object sender, EventArgs e)
        {
            RandomizerOptions rand = options.Copy();
            if (fixedseed.Text.Trim() != "")
            {
                if (uint.TryParse(fixedseed.Text.Trim(), out uint seed))
                {
                    rand.Seed = seed;
                }
                else
                {
                    statusL.Text = "Invalid fixed seed";
                    return;
                }
            }
            else
            {
                rand.Seed = (uint)new Random().Next();
            }
            // Hacky translation of dlc1/dlc2 options
            // This is fine because no other code in the form relies on these values
            if (options["dlc2fromdlc1"] && options["dlc2fromkiln"])
            {
                rand["dlc2fromdlc1"] = rand["dlc2fromkiln"] = false;
                rand["dlc2fromeither"] = true;
            }
            randomize.Enabled = false;
            randomizeL.Text = $"Seed: {rand.Seed}";
            Randomizer randomizer = new Randomizer();
            await Task.Factory.StartNew(() => {
                Directory.CreateDirectory("runs");
                string runId = $@"runs\{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}_log_{rand.Seed}_{rand.ConfigHash()}.txt";
                TextWriter log = File.CreateText(runId);
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(rand, status => { statusL.Text = status; });
                    statusL.Text = $"Done. Hints and spoilers in {runId}";
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    statusL.Text = $"Error! Partial log in {runId}";
                }
                finally
                {
                    log.Close();
                    Console.SetOut(stdout);
                }
            });
            randomize.Enabled = true;
        }
    }
}
