using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public class RandomizerOptionsFile
    {
        public string Game { get; set; }
        public string Version { get; set; }
        public string Options { get; set; }
        public string EnemyPreset { get; set; }
        public string ItemPreset { get; set; }
        public string HelperConfig { get; set; }
        [YamlIgnore]
        public RandomizerOptions OptionsValue { get; set; }
        [YamlIgnore]
        public EnemyPreset EnemyPresetValue { get; set; }
        [YamlIgnore]
        public ItemPreset ItemPresetValue { get; set; }

        private static readonly Regex commentLine = new Regex(@"^\s*;|^\s*$");

        public static RandomizerOptionsFile Create(
            string version,
            RandomizerOptions opt,
            EnemyPreset enemyPreset = null,
            ItemPreset itemPreset = null,
            string helperConfig = null)
        {
            if (helperConfig != null)
            {
                // This formats it every other line. I don't know why. It's stupid
                helperConfig = string.Join("", helperConfig.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Where(l => !commentLine.IsMatch(l)).Select(l => $"{l}{Environment.NewLine}"));
                if (string.IsNullOrWhiteSpace(helperConfig)) helperConfig = null;
            }
            RandomizerOptionsFile val = new RandomizerOptionsFile
            {
                Game = opt.GameNameForFile,
                Version = version,
                Options = opt.FullString(),
                HelperConfig = helperConfig,
            };
            if (enemyPreset != null)
            {
                val.EnemyPreset = enemyPreset.ToYamlString();
            }
            if (itemPreset != null)
            {
                val.ItemPreset = itemPreset.ToYamlString();
            }
            return val;
        }

        public void Save(TextWriter writer)
        {
            // I guess don't skip null attributes? Otherwise use GameData serializer
            ISerializer serializer = new SerializerBuilder().DisableAliases().Build();
            serializer.Serialize(writer, this);
        }

        public static RandomizerOptionsFile Load(string path)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            RandomizerOptionsFile ret;
            using (var reader = File.OpenText(path))
            {
                ret = deserializer.Deserialize<RandomizerOptionsFile>(reader);
            }
            if (ret.Options == null || ret.Game == null || ret.Version == null)
            {
                throw new Exception($"Error: missing required field in options file");
            }
            if (ret.Game != "ER")
            {
                throw new Exception($"Error: unsupported or unknown game \"{ret.Game}\" in options file");
            }
            ret.OptionsValue = RandomizerOptions.Parse(ret.Options.Split(' '), FromGame.ER);
            // Do options validation to avoid overwriting local files, but it may be less error prone to trust the input
            if (ret.EnemyPreset != null && (ret.OptionsValue["customenemy"] || !string.IsNullOrWhiteSpace(ret.OptionsValue.Preset)))
            {
                ret.EnemyPresetValue = RandomizerCommon.EnemyPreset.ParsePreset(ret.OptionsValue.Preset, ret.EnemyPreset);
            }
            if (ret.ItemPreset != null && ret.OptionsValue["customitem"])
            {
                ret.ItemPresetValue = RandomizerCommon.ItemPreset.ParsePreset(ret.ItemPreset);
            }
            if (!ret.OptionsValue["helper"])
            {
                ret.HelperConfig = null;
            }
            return ret;
        }
    }
}
