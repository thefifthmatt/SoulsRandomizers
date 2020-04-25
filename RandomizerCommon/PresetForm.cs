using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Preset;

namespace RandomizerCommon
{
    public partial class PresetForm : Form
    {
        public Preset Preset { get; set; }

        private EnemyAnnotations ann;
        private List<string> presetNames;
        private List<string> enemyNames;
        private List<string> noEnemies = new List<string> { "(None)" };
        private string defaultText;
        private bool fatal;
        public PresetForm()
        {
            InitializeComponent();
            defaultText = desc.Text;
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                using (var reader = File.OpenText("dists/Base/enemy.txt"))
                {
                    ann = deserializer.Deserialize<EnemyAnnotations>(reader);
                }
            }
            catch (Exception e)
            {
                fatal = true;
                SetDesc($"Can't load enemy config: {e.Message}", true);
            }
            presetNames = GetPresetNames();
            if (presetNames.Count == 0)
            {
                fatal = true;
                SetDesc($"Can't find any presets", true);
            }
            presetNames.Insert(0, "(None)");
            select.DataSource = presetNames;
            enemy.DataSource = noEnemies;
            if (fatal)
            {
                submit.Enabled = false;
            }
            else
            {
                enemyNames = new List<string>();
                foreach (EnemyCategory cat in ann.Categories)
                {
                    if (cat.Name == null) continue;
                    enemyNames.Add(cat.Name);
                    foreach (string subname in new[] { cat.Partition, cat.Partial, cat.Instance }.Where(g => g != null).SelectMany(g => g))
                    {
                        enemyNames.Add("- " + subname);
                    }
                }
                if (ann.Singletons != null)
                {
                    enemyNames = enemyNames.Except(ann.Singletons).ToList();
                }
                select.Enabled = true;
                UpdateEnemyList();
            }
        }

        bool initialized = false;
        public void UpdateEnemyList()
        {
            if (Preset != null && Preset.Name == "Oops All")
            {
                enemy.DataSource = enemyNames;
                enemy.Enabled = true;
                if (!initialized)
                {
                    enemy.SelectedIndex = new Random().Next(enemyNames.Count);
                    initialized = true;
                }
                string name = enemyNames[enemy.SelectedIndex];
                Preset.OopsAll = name.StartsWith("- ") ? name.Substring(2) : name;
            }
            else
            {
                enemy.Enabled = false;
            }
        }

        public void SetDesc(string text, bool error = false)
        {
            desc.ForeColor = error ? Color.IndianRed : SystemColors.ControlText;
            desc.Text = text;
        }

        private void preset_Changed(object sender, EventArgs e)
        {
            if (fatal) return;
            if (select.SelectedIndex == 0)
            {
                Preset = null;
                SetDesc(defaultText);
                submit.Enabled = true;
            }
            else
            {
                string name = presetNames[select.SelectedIndex];
                try
                {
                    Preset = LoadPreset(name);
                    SetDesc(Preset.Description ?? "");
                    submit.Enabled = true;
                    UpdateEnemyList();
                }
                catch (Exception ex)
                {
                    SetDesc($"Error loading preset: {ex.Message}" + (ex.InnerException == null ? "" : $"\r\n{ex.InnerException.Message}"), true);
                    submit.Enabled = false;
                }
            }
        }

        private void enemy_Changed(object sender, EventArgs e)
        {
            UpdateEnemyList();
        }

        private void submit_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
