using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using SoulsIds;

namespace RandomizerCommon
{
    public class ScalingEffects
    {
        private readonly GameData game;
        public ScalingEffects(GameData game)
        {
            this.game = game;
        }

        private class ScalingData
        {
            public int ScalingBase { get; set; }
            public int NewScalingBase { get; set; }
            public Dictionary<string, List<string>> ScalingFields { get; set; }
            public Dictionary<string, List<double>> ScalingMatrix { get; set; }
        }

        private readonly static ScalingData sekiroScaling = new ScalingData
        {
            ScalingBase = 7000,
            NewScalingBase = 7200,
            ScalingMatrix = new Dictionary<string, List<double>>
            {
                ["health"] = new List<double>()
                {
                    1.36668,
                    1.79345, 1.08729,
                    2.23100, 1.82860, 1.75986,
                    2.96018, 2.15446, 1.84980, 1.27038,
                },
                ["damage"] = new List<double>()
                {
                    1.34859,
                    1.75701, 1.08164,
                    2.06106, 1.73130, 1.66855,
                    2.59672, 1.96505, 1.70489, 1.21831,
                },
                ["xp"] = new List<double>()
                {
                    2.22234,
                    6.27407, 1.64918,
                    7.37703, 3.74878, 2.00944,
                    8.59301, 4.57776, 3.25542, 1.40736,
                },
                ["posturerate"] = new List<double>()
                {
                    1.36668,
                    1.79345, 1.08729,
                    2.23100, 1.82860, 1.75986,
                    2.96018, 2.15446, 1.84980, 1.27038,
                },
            },
            ScalingFields = new Dictionary<string, List<string>>
            {
                ["health"] = new List<string> { "maxHpRate", "maxStaminaCutRate" },
                ["damage"] = new List<string> { "physAtkPowerRate", "magicAtkPowerRate", "fireAtkPowerRate", "thunderAtkPowerRate", "staminaAttackRate", "darkAttackPowerRate" },
                ["xp"] = new List<string> { "Unk85" },
                ["posturerate"] = new List<string> { "NewGameBonusUnk" },
            },
        };

        private readonly static ScalingData ds3Scaling = new ScalingData
        {
            // 7000 has non-default values, but they're all overwritten by below fields when non-default
            ScalingBase = 7000,
            // Fog gate randomizer starts at 7900
            NewScalingBase = 7600,
            ScalingMatrix = new Dictionary<string, List<double>>
            {
                ["health"] = new List<double>()
                {
                    1.35821,
                    2.07059, 1.78840,
                    2.95985, 2.18871, 1.37372,
                    3.78240, 3.06370, 1.78912, 1.23455,
                },
                ["stamina"] = new List<double>()
                {
                    1.11544,
                    1.31081, 1.26948,
                    1.58038, 1.41078, 1.18697,
                    1.69088, 1.61275, 1.26725, 1.04486,
                },
                ["damage"] = new List<double>()
                {
                    1.20130,
                    1.53597, 1.41291,
                    1.82360, 1.49408, 1.11230,
                    1.91034, 1.71028, 1.24039, 1.08641,
                },
                ["defense"] = new List<double>()
                {
                    1.02790,
                    1.07998, 1.06751,
                    1.13644, 1.10062, 1.04934,
                    1.20131, 1.17717, 1.10881, 1.04707,
                },
                ["buildup"] = new List<double>()
                {
                    1.02046,
                    1.05868, 1.04994,
                    1.10035, 1.07431, 1.03676,
                    1.29765, 1.26261, 1.22276, 1.17125,
                },
                // These are largely made up and made require further manual tuning, and examination of the SL curve.
                ["xp"] = new List<double>()
                {
                    1.24892,
                    1.87669, 2.30078,
                    2.95937, 2.73450, 2.46316,
                    3.66667, 3.25000, 1.88855, 1.33333,
                },
            },
            ScalingFields = new Dictionary<string, List<string>>
            {
                ["health"] = new List<string> { "maxHpRate" },
                ["stamina"] = new List<string> { "maxStaminaCutRate" },
                ["damage"] = new List<string> { "physAtkPowerRate", "magicAtkPowerRate", "fireAtkPowerRate", "thunderAtkPowerRate", "staminaAttackRate", "darkAttackPowerRate" },
                ["defense"] = new List<string> { "physDefRate", "magicDefRate", "fireDefRate", "thunderDefRate", "darkDefRate" },
                ["buildup"] = new List<string> { "registPoisonChangeRate", "registToxicChangeRate", "registBloodChangeRate", "registCurseChangeRate", "registFrostChangeRate" },
                ["xp"] = new List<string> { "haveSoulRate" },
            },
        };

        // Returns a mapping from (source section, target section) to (scaling without xp, scaling with xp)
        public Dictionary<(int, int), (int, int)> EditScalingSpEffects()
        {
            Dictionary<(int, int), (int, int)> scalingSpEffects = new Dictionary<(int, int), (int, int)>();
            ScalingData d = game.Sekiro ? sekiroScaling : ds3Scaling;
            List<(int, int)> sectionPairs = new List<(int, int)>
            {
                (1, 2),
                (1, 3), (2, 3),
                (1, 4), (2, 4), (3, 4),
                (1, 5), (2, 5), (3, 5), (4, 5),
            };
            if (d.ScalingMatrix.Any(e => !d.ScalingFields.ContainsKey(e.Key) || e.Value.Count != sectionPairs.Count))
            {
                throw new Exception($"Internal error: bad scaling values");
            }

            int newSpBase = d.NewScalingBase;
            PARAM.Row defaultSp = game.Params["SpEffectParam"][d.ScalingBase];
            for (int i = 1; i <= 5; i++)
            {
                for (int j = 1; j <= 5; j++)
                {
                    // Making scaling sp for going from section i to section j
                    if (i == j) continue;
                    int scaleUp = sectionPairs.IndexOf((i, j));
                    int scaleDown = sectionPairs.IndexOf((j, i));
                    if (scaleUp == -1 && scaleDown == -1) throw new Exception($"Internal error: no scaling values defined for section transfer {i}->{j}");
                    int index = scaleUp == -1 ? scaleDown : scaleUp;
                    bool invert = scaleUp == -1;

                    int newSp = newSpBase++;
                    int newSpXp = newSpBase++;
                    PARAM.Row sp = game.AddRow("SpEffectParam", newSp);
                    PARAM.Row spXp = game.AddRow("SpEffectParam", newSpXp);
                    GameEditor.CopyRow(defaultSp, sp);
                    GameEditor.CopyRow(defaultSp, spXp);
                    scalingSpEffects[(i, j)] = (newSp, newSpXp);
                    foreach (KeyValuePair<string, List<string>> entry in d.ScalingFields)
                    {
                        double val = d.ScalingMatrix[entry.Key][index];
                        // If scaling down, just take opposite of calculated difference. If scaling up, nerf a bit, since these numbers tend to come from simpler enemies.
                        if (invert) val = 1 / val;
                        else if (game.Sekiro) val = val / 1.333333;
                        // Console.WriteLine($"{i}->{j} {entry.Key}: {val}");
                        foreach (string field in entry.Value)
                        {
                            spXp[field].Value = (float)val;
                            if (entry.Key == "xp") continue;
                            sp[field].Value = (float)val;
                        }
                    }
                }
            }
            // Make one more for tutorial bosses, before upgrading anything is possible
            int newTutSp = newSpBase++;
            PARAM.Row tutSp = game.AddRow("SpEffectParam", newTutSp);
            GameEditor.CopyRow(defaultSp, tutSp);
            scalingSpEffects[(1, 0)] = (newTutSp, newTutSp);
            foreach (string tutField in new[] { "health", "damage" })
            {
                foreach (string field in d.ScalingFields[tutField])
                {
                    tutSp[field].Value = 0.75f;
                }
            }
            game.Params["SpEffectParam"].Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
            return scalingSpEffects;
        }
    }
}
