using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;

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
            public List<(int, int)> SectionPairs { get; set; }
            public Dictionary<string, List<string>> ScalingFields { get; set; }
            public Dictionary<string, List<double>> ScalingMatrix { get; set; }
        }

        private readonly ScalingData sekiroScaling = new ScalingData
        {
            ScalingBase = 7000,
            NewScalingBase = 7200,
            SectionPairs = new List<(int, int)>
            {
                (1, 2),
                (1, 3), (2, 3),
                (1, 4), (2, 4), (3, 4),
                (1, 5), (2, 5), (3, 5), (4, 5),
            },
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

        private readonly ScalingData ds3Scaling = new ScalingData
        {
            // 7000 has non-default values, but they're all overwritten by below fields when non-default
            ScalingBase = 7000,
            // Fog gate randomizer starts at 7900
            NewScalingBase = 7600,
            SectionPairs = new List<(int, int)>
            {
                (1, 2),
                (1, 3), (2, 3),
                (1, 4), (2, 4), (3, 4),
                (1, 5), (2, 5), (3, 5), (4, 5),
            },
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


        private readonly ScalingData eldenScaling = new ScalingData
        {
            ScalingBase = 7000,
            // 7910 only allows for 90, we need ~150
            NewScalingBase = 77700,
            SectionPairs = new List<(int, int)>
            {
                (1, 2),
                (1, 3), (2, 3),
                (1, 4), (2, 4), (3, 4),
                (1, 5), (2, 5), (3, 5), (4, 5),
                (1, 6), (2, 6), (3, 6), (4, 6), (5, 6),
            },
            // This is filled in dynamically
            ScalingMatrix = null,
            ScalingFields = new Dictionary<string, List<string>>
            {
                ["health"] = new List<string> { "maxHpRate" },
                ["stamina"] = new List<string> { "maxStaminaRate" },
                ["staminadamage"] = new List<string> { "staminaAttackRate" },
                ["damage"] = new List<string> { "physicsAttackPowerRate", "magicAttackPowerRate", "fireAttackPowerRate", "thunderAttackPowerRate", "darkAttackPowerRate" },
                ["defense"] = new List<string> { "physicsDiffenceRate", "magicDiffenceRate", "fireDiffenceRate", "thunderDiffenceRate", "darkDiffenceRate" },
                ["buildup"] = new List<string> { "registPoizonChangeRate", "registDiseaseChangeRate", "registBloodChangeRate", "registFreezeChangeRate", "registSleepChangeRate", "registMadnessChangeRate" },
                ["xp"] = new List<string> { "haveSoulRate" },
            },
        };

        public Dictionary<int, int> InitializeEldenScaling(Dictionary<int, EnemyData> defaultData, Dictionary<int, List<int>> dupeMap)
        {
            // First, calculate scaling multipliers
            Dictionary<int, int> speffectTiers = new Dictionary<int, int>();
            Dictionary<string, List<double>> scalingMult = new Dictionary<string, List<double>>();
            for (int i = 0; i < eldenScalingTiers.Count; i++)
            {
                (int start, int end) = eldenScalingTiers[i];
                Dictionary<string, List<double>> tierValues = new Dictionary<string, List<double>>();
                for (int spId = start; spId <= end; spId += 10)
                {
                    speffectTiers[spId] = i + 1;
                    PARAM.Row spRow = game.Params["SpEffectParam"][spId];
                    foreach (KeyValuePair<string, List<string>> fields in eldenScaling.ScalingFields)
                    {
                        AddMulti(tierValues, fields.Key, (double)(float)spRow[fields.Value[0]].Value);
                    }
                }
                foreach (KeyValuePair<string, List<double>> vals in tierValues)
                {
                    // Alternatively: average
                    double tierValue = vals.Value.Min();
                    AddMulti(scalingMult, vals.Key, tierValue);
                }
            }
            // Some attempt to reduce the effect of scaling up/down, due to how later
            // game bosses are often manually tuned differently.
            // 1.1 ends up being a factor of 1.61 after 5 iterations, 1.05 is 1.276, 1.06 is 1.33
            double dampen = 1.05f;
            List<string> dampenTypes = new List<string> { "damage", "health" };
            // Concurrent modification of scalingMult
            foreach (KeyValuePair<string, List<double>> mult in scalingMult.Where(m => dampenTypes.Contains(m.Key)).ToList())
            {
                // Console.WriteLine($"{mult.Key} scaling: {string.Join(", ", mult.Value)}");
                double factor = 1;
                List<double> vals = mult.Value.ToList();
                for (int i = 0; i < vals.Count; i++)
                {
                    vals[i] /= factor;
                    if (i > 0 && vals[i] <= vals[i - 1])
                    {
                        Console.WriteLine($"Warning: Can't dampen {mult.Key} scaling because of merged mod. {i-1}={mult.Value[i-1]}, {i}={mult.Value[i]}. Enemy scaling may be incorrect as a result.");
                        vals = null;
                        break;
                    }
                    factor *= dampen;
                }
                if (vals != null)
                {
                    scalingMult[mult.Key] = vals;
                }
            }

            // Manually construct the XP list.
            // TODO we could do this automatically based on NpcParam list, plus GameAreaParam... see how this does.
            scalingMult["xp"] = EldenSoulScaling;
            // Build matrix from that
            eldenScaling.ScalingMatrix = new Dictionary<string, List<double>>();
            foreach (KeyValuePair<string, List<double>> mult in scalingMult)
            {
                List<double> vals = mult.Value;
                List<double> pairs = new List<double>();
                foreach ((int i, int j) in eldenScaling.SectionPairs)
                {
                    pairs.Add(vals[j - 1] / vals[i - 1]);
                }
                eldenScaling.ScalingMatrix[mult.Key] = pairs;
                // Console.WriteLine($"{mult.Key}: {string.Join(", ", eldenScaling.SectionPairs.Select((p, k) => $"{p.Item1}->{p.Item2}={pairs[k]}"))}");
            }
            // Finally do classifications, from entity id to scaling tier
            Dictionary<int, int> ret = new Dictionary<int, int>();
            // Exiting NPC speffects
            Dictionary<int, int> npcEffects = game.Params["NpcParam"].Rows
                .ToDictionary(r => r.ID, r => (int)r["spEffectID3"].Value);
            // Record effects used in maps to fill in blanks. map name -> speffect id -> count
            Dictionary<string, Dictionary<int, int>> mapCounts = new Dictionary<string, Dictionary<int, int>>();
            // First pass, directly available speffects
            foreach (KeyValuePair<int, EnemyData> entry in defaultData)
            {
                EnemyData data = entry.Value;
                if (!npcEffects.TryGetValue(data.NPC, out int sp) || sp <= 0)
                {
                    // TODO: Is this always correct? bear 60310042 in Dragonbarrow is missing one, for instance
                    if (data.Model != "c0000")
                    {
                        ret[entry.Key] = 1;
                    }
                    continue;
                }
                if (speffectTiers.TryGetValue(sp, out int tier))
                {
                    ret[entry.Key] = tier;
                    if (!mapCounts.TryGetValue(data.MainMap, out Dictionary<int, int> counts))
                    {
                        mapCounts[data.MainMap] = counts = new Dictionary<int, int>();
                    }
                    if (!counts.ContainsKey(sp))
                    {
                        counts[sp] = 0;
                    }
                    counts[sp]++;
                }
            }
            // Find most common speffect in map and use it for humans
            // Previously this max, but e.g. Siluria bumps Deeproot up to 7130
            Dictionary<string, int> mapEffects = mapCounts.ToDictionary(
                e => e.Key, e => e.Value.OrderByDescending(s => s.Value).First().Key);
            // Roundtable Hold has no non-human enemies
            mapEffects["m11_10_00_00"] = 7030;
            // Second pass, try to infer
            foreach (KeyValuePair<int, EnemyData> entry in defaultData)
            {
                if (ret.ContainsKey(entry.Key)) continue;
                EnemyData data = entry.Value;
                if (mapEffects.TryGetValue(data.MainMap, out int sp))
                {
                    // Console.WriteLine($"Filling in {entry.Key} in {data.Map} -> {sp} (from {string.Join(",", mapCounts[data.Map])})");
                    ret[entry.Key] = speffectTiers[sp];
                }
            }
            foreach (KeyValuePair<int, int> entry in manualEntityTiers)
            {
                int entityId = entry.Key;
                int tier = entry.Value;
                ret[entityId] = tier;
                // Need to update dupes, or else non-random dupes may get scaled.
                // Alternatively, could put manual overrides in EnemyInfo and use DupeFrom
                if (dupeMap.TryGetValue(entityId, out List<int> dupes))
                {
                    foreach (int dupe in dupes)
                    {
                        ret[dupe] = tier;
                    }
                }
            }

            return ret;
        }

        private static readonly Dictionary<int, int> manualEntityTiers = new Dictionary<int, int>
        {
            // Ekzykes is normally 3, but its base HP (5753) and damage is higher than
            // Greyll (tier 6, 1639) and Agheel (tier 2, 1639)
            [1048370800] = 5,
            // Nerf Rykard from 4 to 3, to match Godskin Noble
            [16000800] = 3,
            [16000801] = 3,
            // Bump Godrick up a tier?
            [10000800] = 2,
        };

        // Hand-crafted attempt at finding the soul scaling curve.
        // This is used for turning bosses into regular enemies with an appropriate soul drop amount
        public readonly static List<double> EldenSoulScaling = new List<double> { 1, 2.9, 4.785, 10, 20, 30 };

        // Set u8 magParamChange = 1, u8 miracleParamChange = 1
        // This isn't as necessary because we're copying from row 7000 which should have it already
        // private readonly static List<string> eldenBooleans = new List<string> { "magParamChange", "miracleParamChange" };
        private readonly static List<(int, int)> eldenScalingTiers = new List<(int, int)>
        {
            (7000, 7040),
            (7050, 7080),
            (7090, 7110),
            (7120, 7130),
            (7140, 7160),
            (7170, 7200),
        };
        // There are also NPC speffects 13941 to 19397, but these don't do scaling.
        // In those cases, pick the maximum scaling speffect from the same map.
        // 0: do not scale
        // -- HP rates 0 to 1.813 - bats 42000014, 83 souls, rats 145. 1
        // 7000: Stranded Graveyard
        // 7010: Limgrave, Stormhill
        // 7020: Weeping Peninsula
        // 7030 7040: Stormveil, Peninsula Minibosses
        // -- HP rates 1.953 to 2.688 - bats 42000141, 260 souls, rats 392. 2.9
        // 7050: Early Ainsel
        // 7060: Anticipation, Liurnia, Bellum, Siofra, Academy, Fringefolk Grave
        // 7070: Rennala, Caelid, Precipice
        // 7080: Caria Manor, Abductor Virgins, Altus, Redmane Castle
        // -- HP rates 3.25 to 4.125 - bats 42000038, 434 souls, rats 416 avg 862 = 639. ulcerated 18000. 1.65 mult = 4.785
        // 7090: Nokron, Mt. Gelmir
        // 7100: Radahn, Capital Outskirts, Leyndell, Liurnia Tower
        // 7110: Leyndell, Capital Outskirts, Ainsel (incl Astel), Deeproot, Volcano Manor
        // -- HP rates 4.844 to 5.484 - bats 42000050, 575 souls, rats 862. ulcerated 48000. 2 mult = 10
        // 7120: Rykard, Mountaintops, Flame Peak
        // 7130: Subterranean, Moonlight Altar
        // -- HP rates 6.563 to 6.875 - bats 42000042, 887 souls, rats 2008. rot avatar 1051400800, 91000 souls. 2 mult = 20
        // 7140: Fire Giant, Farum Azula
        // 7150: Maliketh, Dragonbarrow, Ashen Leyndell
        // 7160: Ashen Leyndell Bosses, Consecrated Snowfield
        // -- HP rates 7.047 to 7.422 (7180 is 7.203). rot avatar id 1050570850, 160000 souls. 1.75 mult = 35
        // 7170: Final bosses, Mohgwyn
        // 7180: Mohg, Haligtree, Snowfield Bosses
        // 7190: More Haligtree
        // 7200: Malenia

        // Returns a mapping from (source section, target section) to (scaling without xp, scaling with xp)
        // TODO: Create third category for damage-only scaling?
        public SpEffectValues EditScalingSpEffects()
        {
            SpEffectValues ret = new SpEffectValues();
            ScalingData d = game.Sekiro ? sekiroScaling : (game.EldenRing ? eldenScaling : ds3Scaling);
            if (d.ScalingMatrix.Any(e => !d.ScalingFields.ContainsKey(e.Key) || e.Value.Count != d.SectionPairs.Count))
            {
                throw new Exception($"Internal error: bad scaling values");
            }

            int newSpBase = d.NewScalingBase;
            PARAM.Row defaultSp = game.Params["SpEffectParam"][d.ScalingBase];
            int maxTier = d.SectionPairs.Select(p => p.Item2).Max();
            for (int i = 1; i <= maxTier; i++)
            {
                for (int j = 1; j <= maxTier; j++)
                {
                    // Making scaling sp for going from section i to section j
                    if (i == j) continue;
                    int scaleUp = d.SectionPairs.IndexOf((i, j));
                    int scaleDown = d.SectionPairs.IndexOf((j, i));
                    if (scaleUp == -1 && scaleDown == -1) throw new Exception($"Internal error: no scaling values defined for section transfer {i}->{j}");
                    int index = scaleUp == -1 ? scaleDown : scaleUp;
                    bool invert = scaleUp == -1;

                    int newSp = newSpBase++;
                    int newSpXp = newSpBase++;
                    PARAM.Row sp = game.AddRow("SpEffectParam", newSp);
                    PARAM.Row spXp = game.AddRow("SpEffectParam", newSpXp);
                    GameEditor.CopyRow(defaultSp, sp);
                    GameEditor.CopyRow(defaultSp, spXp);
                    ret.Areas[(i, j)] = new AreaScalingValue { FixedScaling = newSp, RegularScaling = newSpXp };
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
            // A few more for special cases
            PARAM.Row createCustomEffect()
            {
                int newCustomSp = newSpBase++;
                PARAM.Row customSp = game.AddRow("SpEffectParam", newCustomSp);
                GameEditor.CopyRow(defaultSp, customSp);
                return customSp;
            }
            // Tutorial bosses, before upgrading anything is possible
            PARAM.Row tutSp = createCustomEffect();
            ret.TutorialScaling = tutSp.ID;
            foreach (string fieldType in new[] { "health", "damage" })
            {
                foreach (string field in d.ScalingFields[fieldType])
                {
                    tutSp[field].Value = 0.75f;
                }
            }
            // Boss health scaling option
            PARAM.Row basicSp = createCustomEffect();
            ret.BossAsBasicScaling = basicSp.ID;
            foreach (string fieldType in new[] { "health", "xp" })
            {
                foreach (string field in d.ScalingFields[fieldType])
                {
                    basicSp[field].Value = 0.25f;
                }
            }
            // Enemy multiplier scaling, for 2-5 multipliers
            foreach (double reduce in bossDupeScaling)
            {
                PARAM.Row dupeSp = createCustomEffect();
                ret.Dupes.Add(dupeSp.ID);
                foreach (string fieldType in new[] { "health" })
                {
                    foreach (string field in d.ScalingFields[fieldType])
                    {
                        dupeSp[field].Value = reduce;
                    }
                }
            }
            // Phase changes
            PARAM.Row phaseDown = createCustomEffect();
            PARAM.Row phaseUp = createCustomEffect();
            ret.PhaseDownScaling = phaseDown.ID;
            ret.PhaseUpScaling = phaseUp.ID;
            foreach (string fieldType in new[] { "health" })
            {
                foreach (string field in d.ScalingFields[fieldType])
                {
                    phaseDown[field].Value = 0.7f;
                    phaseUp[field].Value = 1.4f;
                }
            }

            game.Params["SpEffectParam"].Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
            return ret;
        }

        // Utility for boss dupes, whose drops can be scaled with multiplier
        private static readonly List<double> bossDupeScaling = Enumerable.Range(2, 4).Select(c => Math.Sqrt(1.0 / c)).ToList();
        public static double GetXpRate(int dupeCount, bool scaleDown)
        {
            if (dupeCount <= 0) return 1;
            int dupeIndex = dupeCount - 1;
            if (dupeIndex >= bossDupeScaling.Count)
            {
                dupeIndex = bossDupeScaling.Count - 1;
            }
            return (dupeIndex + 2) * (scaleDown ? bossDupeScaling[dupeIndex] : 1);
        }

        // A whole ton of output
        public class SpEffectValues
        {
            public Dictionary<(int, int), AreaScalingValue> Areas = new Dictionary<(int, int), AreaScalingValue>();
            public List<int> Dupes = new List<int>();
            public int TutorialScaling { get; set; }
            public int BossAsBasicScaling { get; set; }
            public int PhaseDownScaling { get; set; }
            public int PhaseUpScaling { get; set; }

            public int GetDupeScaling(int dupeCount)
            {
                if (dupeCount <= 0) throw new Exception($"Internal error: bad dupe count {dupeCount}");
                int dupeIndex = dupeCount - 1;
                return dupeIndex >= Dupes.Count ? Dupes.Last() : Dupes[dupeIndex];
            }
        }
        public class AreaScalingValue
        {
            public int RegularScaling { get; set; }
            public int FixedScaling { get; set; }
        }
        public class DupeScalingValue
        {
            public int NoHealthScaling { get; set; }
            public int HealthScaling { get; set; }
        }
    }
}
