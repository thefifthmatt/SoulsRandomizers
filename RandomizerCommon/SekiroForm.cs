using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RandomizerCommon.Properties;
using static RandomizerCommon.Util;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public partial class SekiroForm : Form
    {
        private static readonly string enemySeedPlaceholder = "(same as fixed seed)";

        private Messages messages = new Messages(null);
        private RandomizerOptions options = new RandomizerOptions(FromGame.SDT);
        private string defaultOpts = null;
        private HashSet<string> previousOpts = new HashSet<string>();
        private bool simultaneousUpdate;
        private bool working;
        private bool error;

        public SekiroForm()
        {
            InitializeComponent();
            // One-time initialization for errors and things
            if (!MiscSetup.CheckRequiredSekiroFiles(out string req))
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
            RandomizerOptions initialOpts = new RandomizerOptions(FromGame.SDT);
            SetControlFlags(this, initialOpts);
            defaultOpts = initialOpts.FullString();

            string existingOpts = Settings.Default.Options;
            if (string.IsNullOrWhiteSpace(existingOpts))
            {
                options.Difficulty = difficulty.Value;
                SetControlFlags(this);
                // Seed and checkboxes will be updated in UpdateEnabled
            }
            else
            {
                SetOptionsString(existingOpts);
                if (options.Seed != 0)
                {
                    defaultReroll.Enabled = true;
                    defaultReroll.Checked = false;
                }
                // Enemy seed and checkboxes will be updated in UpdateEnabled
            }
            SetStatus(null);

            UpdateEnabled();
            UpdateLabels();

            void parentImage(PictureBox child)
            {
                child.Location = new Point(child.Location.X - title.Location.X, child.Location.Y - title.Location.Y);
                child.Parent = title;
            }
            parentImage(mascot);
            parentImage(itemPic);
            parentImage(catPic);
            RefreshImage();
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
            options = RandomizerOptions.Parse(previousOpts, FromGame.SDT, isValidOption);

            // New defaults
            if (previousOpts.Contains("v1"))
            {
                options["veryearlyhirata"] = true;
                options["openstart"] = true;
            }
            if (previousOpts.Contains("v1") || previousOpts.Contains("v2"))
            {
                options["scale"] = true;
                options["edittext"] = true;
            }

            simultaneousUpdate = true;
            InsertControlFlags(this);
            difficulty.Value = options.Difficulty;
            if (!enemytoitem.Checked)
            {
                defaultAllowReroll.Checked = true;
            }
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
            bool fatal = MiscSetup.CheckSekiroModEngine(out string err);
            if (!MiscSetup.CheckSFX())
            {
                List<string> maps = Directory.GetFiles(@"dists\Base", "*.msb.dcx").Select(m => Path.GetFileName(m).Replace(".msb.dcx", "")).ToList();
                if (!MiscSetup.CombineSFX(maps, "."))
                {
                    if (err == null)
                    {
                        err = "Cross-map SFX is missing. For SFX to show up, either download it (it is a separate download),\r\nor extract the entire game with UXM and reopen the randomizer.";
                    }
                    // err = err == null ? sfx : $"{err}\r\n{sfx}";
                }
            }
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
                DateTime now = DateTime.Now;
                msg = "Created by thefifthmatt. Art by Souv" + (now.Month == 3 && now.Day == 22 ? ". Happy Birthday Sekiro!" : ".") +$" Current config hash: {options.ConfigHash()}";
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

        private void defaultReroll_CheckedChanged(object sender, EventArgs e)
        {
            if (defaultReroll.Checked)
            {
                randomize.Text = "Randomize new run!";
            }
            else
            {
                if (defaultRerollEnemy.Checked)
                {
                    randomize.Text = "Reroll enemy rando";
                }
                else
                {
                    randomize.Text = "Run with fixed seed";
                }
            }
            if (!defaultRerollEnemy.Enabled)
            {
                defaultRerollEnemy.Checked = defaultReroll.Checked;
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
            // Mass enables/disables
            Dictionary<Control, bool> toEnable = new Dictionary<Control, bool>();
            MassEnable(toEnable, this, "item", "itemGroup");
            MassEnable(toEnable, this, "enemy", "enemyGroup");
            // Individual updates
            void setCheck(CheckBox check, bool enabled, bool defaultState, bool disabledState, string overrideDisable)
            {
                bool prevEnabled = check.Enabled;
                if (overrideDisable == null || options[overrideDisable])
                {
                    toEnable[check] = enabled;
                }
                if (!enabled && prevEnabled && check.Checked != disabledState)
                {
                    check.Checked = disabledState;
                    changes = true;
                }
                else if (enabled && !prevEnabled && check.Checked != defaultState)
                {
                    check.Checked = defaultState;
                    changes = true;
                }
            };
            // Treat Headless like miniboss for item placement. or, Make Headless not required. (If Headless are not randomized, don't put key or important items there)
            setCheck(weaponprogression, !options["norandom_dmg"], true, false, "item");
            setCheck(healthprogression, !options["norandom_health"], true, false, "item");
            setCheck(skillprogression, !options["norandom_skills"], true, false, "item");
            setCheck(splitskills, !options["norandom_skills"], false, false, "item");
            setCheck(headlessignore, !(options["enemy"] && options["headlessmove"]), true /* ? */, false, "item");
            setCheck(phasebuff, options["phases"], true, false, "enemy");

            // Manage enemy/item/skill interaction separately
            bool enemyItemEligible = options["enemy"] && options["item"] && !options["norandom_skills"] && options["skillprogression"];
            bool enemyItemPrev = enemytoitem.Enabled;
            toEnable[enemytoitem] = enemyItemEligible;
            if (enemyItemEligible && !enemyItemPrev && !enemytoitem.Checked)
            {
                // Don't check it automatically, because a lot of things can cause this option to be available again, and usually this won't be desired.
                // enemytoitem.Checked = true;
                changes = true;
            }
            else if (!enemyItemEligible && enemyItemPrev && enemytoitem.Checked)
            {
                defaultAllowReroll.Checked = true;
                changes = true;
            }
            if (enemytoitem.Checked || !options["enemy"] || !options["item"])
            {
                defaultRerollEnemy.Checked = defaultReroll.Checked;
                defaultRerollEnemy.Enabled = false;
                enemyseed.Enabled = false;
                updateEnemySeed();
            }
            else
            {
                // Allowing enemy reroll
                enemyseed.Enabled = true;
                updateEnemySeed();
            }

            foreach (KeyValuePair<Control, bool> enable in toEnable)
            {
                enable.Key.Enabled = enable.Value;
            }
            randomize.Enabled = (options["enemy"] || options["item"]) && !error;
            // Updates
            if (changes) SetControlFlags(this);
            simultaneousUpdate = false;
        }

        private void UpdateLabels()
        {
            string unfairText = "";
            // if (options.GetNum("veryunfairweight") > 0.5) unfairText = " and very unfair";
            // else if (options.GetNum("unfairweight") > 0.5) unfairText = " and unfair";
            string loc;
            if (options.GetNum("allitemdifficulty") > 0.86) loc = $"Much better rewards for difficult and late{unfairText} locations.";
            else if (options.GetNum("allitemdifficulty") > 0.55) loc = $"Better rewards for difficult and late{unfairText} locations.";
            else if (options.GetNum("allitemdifficulty") > 0.3) loc = $"Slightly better rewards for difficult and late{unfairText} locations.";
            else if (options.GetNum("allitemdifficulty") > 0.001) loc = "Most locations for items are equally likely. Often results in a lot of early memories and prayer beads.";
            else loc = "All locations for items are equally likely. Often results in a lot of early memories and prayer beads.";
            string chain = "";
            if (!options["norandom"])
            {
                if (options.GetNum("keyitemchainweight") <= 3) chain = "Key items will usually be easy to find and not require much side content.";
                else if (options.GetNum("keyitemchainweight") <= 6) chain = "Key items will usually be in different areas and depend on each other.";
                // else if (options.GetNum("keyitemchainweight") <= 10) chain = "Key items will usually form long chains across different areas.";
                else chain = "Key items will usually be in different areas and form interesting chains.";
            }
            difficultyL.Text = $"{loc}\r\n{chain}";
            difficultyAmtL.Text = $"{options.Difficulty}%";
        }

        private Preset selectedPreset;
        private void preset_Click(object sender, EventArgs e)
        {
            using (PresetForm presetForm = new PresetForm("dists"))
            {
                DialogResult result = presetForm.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    bool prevNull = selectedPreset == null;
                    SetPreset(presetForm.Preset);
                    // Rewrite options based on preset selection, when done from UI, if not going from none -> none
                    if (!(prevNull && selectedPreset == null))
                    {
                        bool fullRando = selectedPreset == null ? false : selectedPreset.RecommendFullRandomization;
                        bool progression = selectedPreset == null ? true : !selectedPreset.RecommendNoEnemyProgression;
                        bosses.Checked = true;
                        minibosses.Checked = true;
                        headlessmove.Checked = fullRando;
                        enemies.Checked = true;
                        phases.Checked = progression;
                        phasebuff.Checked = progression;
                        earlyreq.Checked = progression;
                    }
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

        private async void randomize_Click(object sender, EventArgs e)
        {
            if (working) return;
            SetWarning();
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
            if (!defaultRerollEnemy.Checked && enemyseed.Text.Trim() != "" && enemyseed.Text != enemySeedPlaceholder)
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
            // TODO: What is exact condition to use for when rerolling should be allowed? What is condition for enemySeedPlaceholder about to be set?
            else if (defaultRerollEnemy.Checked && defaultRerollEnemy.Enabled)
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
            fixedseed.Text = rand.Seed.ToString();
            if (newEnemySeed)
            {
                enemyseed.Text = rand.Seed2.ToString();
                enemyseed.ForeColor = SystemColors.WindowText;
            }

            bool success = false;
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
                    randomizer.Randomize(rand, FromGame.SDT, status => { SetStatus(status); }, preset: selectedPreset);
                    SetStatus($"Done! Hints and spoilers in spoiler_logs directory as {runId} - Restart your game!!", success: true);
                    success = true;
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
            if (success) RefreshImage();
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

                    UpdateEnabled();
                    UpdateLabels();
                    SaveOptions();
                    SetStatus(null);
                }
            }
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

        private void updateEnemySeed()
        {
            // Manage placeholder text, because winforms is bad
            if (enemyseed.Focused && enemyseed.Enabled && enemyseed.Text == enemySeedPlaceholder)
            {
                enemyseed.Text = "";
                enemyseed.ForeColor = SystemColors.WindowText;
            }
            if ((!enemyseed.Focused && string.IsNullOrWhiteSpace(enemyseed.Text)) || !enemyseed.Enabled)
            {
                enemyseed.Text = enemySeedPlaceholder;
                enemyseed.ForeColor = SystemColors.GrayText;
            }
            // Manage checkbox
            if (enemyseed.Text == enemySeedPlaceholder)
            {
                defaultRerollEnemy.Enabled = enemyseed.Enabled && defaultReroll.Enabled;
                return;
            }
            string text = enemyseed.Text.Trim();
            bool valid = uint.TryParse(text, out uint val) && val != 0;
            if (defaultAllowReroll.Checked)
            {
                defaultRerollEnemy.Enabled = valid;
                if (!valid && options.Seed2 == 0)
                {
                    defaultRerollEnemy.Checked = true;
                }
            }
        }

        private void enemyseed_TextChanged(object sender, EventArgs e)
        {
            updateEnemySeed();
        }

        // Title images
        private Random random = new Random();
        private void RefreshImage()
        {
#if DEV_RELEASE
            List<Image> mascots = new List<Image>
            {
                Resources.WolfSip,
                Resources.EmmaSip,
                Resources.IdolSip
            };
            List<Image> items = new List<Image>
            {
                Resources.Ako,
                Resources.AromaticFlower,
                Resources.Ash,
                Resources.BellCharm,
                Resources.BellDemon,
                Resources.Bulging,
                Resources.CarpScale,
                Resources.Divine,
                Resources.DriedSerpent,
                Resources.Droplet,
                Resources.EsotericText,
                Resources.Firecrackers,
                Resources.FreshSerpent,
                Resources.Gachiin,
                Resources.Gokan,
                Resources.GourdSeed,
                Resources.Grass,
                // Resources.HallBell,
                Resources.HealingGourd,
                Resources.HomewardIdol,
                Resources.Jizo,
                Resources.Lapis,
                Resources.Lotus,
                Resources.Malcontent,
                Resources.MistRaven,
                Resources.MonkeyBooze,
                Resources.MortalBlade,
                Resources.Pellet,
                Resources.PrayerBead,
                Resources.PromissoryNote,
                Resources.RedPinwheel,
                Resources.Rice,
                Resources.Sabimaru,
                Resources.Sake,
                Resources.ShelterStone,
                Resources.Shuriken,
                Resources.SnapSeed,
                Resources.SpiritEmblem,
                Resources.SweetRiceBall,
                Resources.Tally,
                Resources.TaroPersimmon,
                Resources.Ungo,
                Resources.WhitePinwheel,
                Resources.Yashariku,
            };
            List<Image> cats = new List<Image>
            {
                Resources.CatBlame,
                Resources.CatBlush,
                Resources.CatFat,
                Resources.CatRing,
                Resources.CatStare,
                Resources.CatTrash,
                Resources.CatTrash2,
            };
            bool look = random.NextDouble() > 0.35;
            int randomImage(int lastImage, PictureBox pic, List<Image> images)
            {
                int item = Choice(random, Enumerable.Range(0, images.Count).Where(i => i != lastImage).ToList());
                pic.Image = images[item];
                return item;
            }
            if (look)
            {
                lastItemPic = randomImage(lastItemPic, itemPic, items);
                if (random.NextDouble() > 0.99)
                {
                    catPic.Image = Resources.CatCapy;
                }
                else
                {
                    lastCatPic = randomImage(lastCatPic, catPic, cats);
                }
            }
            else
            {
                lastMascot = randomImage(lastMascot, mascot, mascots);
            }
            mascot.Visible = !look;
            itemPic.Visible = look;
            catPic.Visible = look;
#endif
        }
        private int lastItemPic = -1;
        private int lastCatPic = -1;
        private int lastMascot = -1;

        private void title_Click(object sender, EventArgs e)
        {
            RefreshImage();
        }
    }
}
