using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RandomizerCommon.Properties;
using static RandomizerCommon.Preset;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public partial class SekiroForm : Form
    {
        private RandomizerOptions options = new RandomizerOptions(true);
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
            DateTime now = DateTime.Now;
            statusL.Text = "Created by thefifthmatt. Art by Souv" + (now.Month == 3 && now.Day == 22 ? ". Happy Birthday Sekiro!" : "");
            randomizeL.TabStop = false;
            presetL.Text = "";
            // The rest of initialization
            string defaultOpts = Settings.Default.Options;
            bool english = false;
            try
            {
                english = System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName == "en";
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrWhiteSpace(defaultOpts))
            {
                options.Difficulty = difficulty.Value;
                if (english) edittext.Checked = true;
                SetControlFlags(this);
                randomizeL.Text = "";
            }
            else
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
                options = RandomizerOptions.Parse(previousOpts, true, isValidOption);
                if (previousOpts.Contains("v1"))
                {
                    // New defaults
                    options["veryearlyhirata"] = true;
                    options["openstart"] = true;
                    if (english) options["edittext"] = true;
                }

                simultaneousUpdate = true;
                InsertControlFlags(this);
                difficulty.Value = options.Difficulty;
                simultaneousUpdate = false;

                randomizeL.Text = options.Seed == 0 ? "" : $"Last used seed: {options.Seed}";
                if (options.Preset != null)
                {
                    try
                    {
                        Preset preset = LoadPreset(options.Preset, extractOopsAll: true);
                        SetPreset(preset);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        SetPreset(null);
                    }
                }
            }
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

        private Random random = new Random();
        private void RefreshImage()
        {
            List<Image> mascots = new List<Image> { Resources.WolfSip, Resources.EmmaSip, Resources.IdolSip };
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
        }
        private int lastItemPic = -1;
        private int lastCatPic = -1;
        private int lastMascot = -1;

        private void title_Click(object sender, EventArgs e)
        {
            RefreshImage();
        }

        private void SetWarning()
        {
            bool fatal = MiscSetup.CheckSekiroModEngine(out string err);
            if (!MiscSetup.CheckSFX())
            {
                List<string> maps = Directory.GetFiles(@"dists\Base", "*.msb.dcx").Select(m => Path.GetFileName(m).Replace(".msb.dcx", "")).ToList();
                if (!MiscSetup.CombineSFX(maps, "."))
                {
                    string sfx = "Cross-map SFX is missing. For SFX to show up, either download it (it is a separate download),\r\nor extract the entire game with UXM and reopen the randomizer.";
                    err = err == null ? sfx : $"{err}\r\n{sfx}";
                }
            }
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

        private void SaveOptions()
        {
            Settings.Default.Options = options.ConfigString(includeSeed: true, includePreset: true);
            Settings.Default.Save();
        }

        private void difficulty_Scroll(object sender, EventArgs e)
        {
            options.Difficulty = difficulty.Value;
            UpdateLabels();
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
            setCheck(headlessignore, !(options["enemy"] && options["headlessmove"]), false, false, "item");
            setCheck(phasebuff, options["phases"], true, false, "enemy");
            setCheck(enemytoitem, options["enemy"] && options["item"] && !options["norandom_skills"] && options["skillprogression"], true, false, null);
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

        private void SetStatus(string msg, bool error = false, bool success = false)
        {
            statusL.Text = msg;
            statusStrip1.BackColor = error ? Color.IndianRed : (success ? Color.PaleGreen : SystemColors.Control);
        }

        private Preset selectedPreset;
        private void preset_Click(object sender, EventArgs e)
        {
            using (PresetForm presetForm = new PresetForm())
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
        }

        private async void randomize_Click(object sender, EventArgs e)
        {
            if (working) return;
            SetWarning();
            if (fixedseed.Text.Trim() != "")
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
            rand.Seed = options.Seed;
            rand.Preset = options.Preset;
            working = true;
            randomize.Text = $"Randomizing...";
            randomize.BackColor = Color.LightYellow;
            randomizeL.Text = $"Seed: {rand.Seed}";
            randomizeL.TabStop = true;
            bool success = false;
            Randomizer randomizer = new Randomizer();
            await Task.Factory.StartNew(() => {
                Directory.CreateDirectory("spoiler_logs");
                string runId = $"{DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")}_log_{rand.Seed}_{rand.ConfigHash()}.txt";
                TextWriter log = File.CreateText($@"spoiler_logs\{runId}");
                TextWriter stdout = Console.Out;
                Console.SetOut(log);
                try
                {
                    randomizer.Randomize(rand, status => { SetStatus(status); }, sekiro: true, preset: selectedPreset);
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
            randomize.Text = $"Randomize!";
            randomize.BackColor = SystemColors.Control;
            working = false;
            if (success) RefreshImage();
        }
    }
}
