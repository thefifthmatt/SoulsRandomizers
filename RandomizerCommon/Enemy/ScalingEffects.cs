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
            public int AutoScaleStart { get; set; }
            public int AutoScaleEnd { get; set; }
            public int MaxTier { get; set; }
            public List<double> SoulScaling { get; set; }
            public List<(int, int)> SectionPairs { get; set; }
            public string NpcField { get; set; }
            public Dictionary<string, List<string>> ScalingFields { get; set; }
            public Dictionary<string, List<double>> ScalingMatrix { get; set; }
            public Dictionary<string, List<double>> UniqueScalingMatrix { get; set; }
        }

        private readonly ScalingData sekiroScaling = new ScalingData
        {
            ScalingBase = 7000,
            NewScalingBase = 7200,
            MaxTier = 5,
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
                ["health"] = new List<string> { "maxHpRate", "maxStaminaRate" },
                ["damage"] = new List<string> { "physicsAttackPowerRate", "magicAttackPowerRate", "fireAttackPowerRate", "thunderAttackPowerRate", "staminaAttackRate", "darkAttackPowerRate" },
                ["xp"] = new List<string> { "haveSkillPointRate" },
                ["posturerate"] = new List<string> { "staminaRecoverSpeedRate" },
            },
        };

        private readonly ScalingData ds3Scaling = new ScalingData
        {
            // 7000 has non-default values, but they're all overwritten by below fields when non-default
            ScalingBase = 7000,
            // Fog gate randomizer starts at 7900
            NewScalingBase = 760000,
            AutoScaleStart = 7000,
            AutoScaleEnd = 7170,
            NpcField = "spEffectId3",
            // MaxTier = 5,
            MaxTier = 20,
            SoulScaling = new() { 0.5, 1, 1, 1.4, 2, 2.3, 2.6, 3.5, 3, 3, 4, 4.4, 4.9, 6.4, 5.4, 6, 6.9, 6.4, 7.4, 8 },
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
                ["stamina"] = new List<string> { "maxStaminaRate" },
                ["staminadamage"] = new List<string> { "staminaAttackRate" },
                ["damage"] = new List<string> { "physicsAttackPowerRate", "magicAttackPowerRate", "fireAttackPowerRate", "thunderAttackPowerRate", "darkAttackPowerRate" },
                ["defense"] = new List<string> { "physicsDiffenceRate", "magicDiffenceRate", "fireDiffenceRate", "thunderDiffenceRate", "darkDiffenceRate" },
                ["buildup"] = new List<string> { "registPoizonChangeRate", "registDiseaseChangeRate", "registBloodChangeRate", "registCurseChangeRate", "registFreezeChangeRate" },
                ["xp"] = new List<string> { "haveSoulRate" },
            },
        };


        private readonly ScalingData eldenScaling = new ScalingData
        {
            ScalingBase = 7000,
            // 7910 only allows for 90, we need ~150
            // Previously 77700
            NewScalingBase = 7800000,
            MaxTier = 34,
            // This is filled in dynamically
            SectionPairs = null,
            ScalingMatrix = null,
            ScalingFields = new Dictionary<string, List<string>>
            {
                ["health"] = new List<string> { "maxHpRate" },
                ["stamina"] = new List<string> { "maxStaminaRate" },
                ["staminadamage"] = new List<string> { "staminaAttackRate" },
                ["damage"] = new List<string> {"physicsAttackPowerRate", "magicAttackPowerRate", "fireAttackPowerRate", "thunderAttackPowerRate", "darkAttackPowerRate" },
                ["damagerate"] = new List<string> { "physicsAttackRate", "magicAttackRate", "fireAttackRate", "thunderAttackRate", "darkAttackRate" },
                ["defense"] = new List<string> { "physicsDiffenceRate", "magicDiffenceRate", "fireDiffenceRate", "thunderDiffenceRate", "darkDiffenceRate" },
                ["buildup"] = new List<string> { "registPoizonChangeRate", "registDiseaseChangeRate", "registBloodChangeRate", "registFreezeChangeRate", "registSleepChangeRate", "registMadnessChangeRate" },
                ["xp"] = new List<string> { "haveSoulRate" },
            },
        };

        private readonly ScalingData ds1Scaling = new ScalingData
        {
            ScalingBase = 7001,
            NewScalingBase = 77000,
            AutoScaleStart = 7001,
            AutoScaleEnd = 7015,
            NpcField = "spEffectID4",
            MaxTier = 15,
            // This is filled in dynamically
            SectionPairs = null,
            ScalingMatrix = null,
            ScalingFields = new Dictionary<string, List<string>>
            {
                ["health"] = new List<string> { "maxHpRate" },
                ["stamina"] = new List<string> { "maxStaminaRate" },
                ["staminadamage"] = new List<string> { "staminaAttackRate" },
                ["damage"] = new List<string> { "physicsAttackPowerRate", "magicAttackPowerRate", "fireAttackPowerRate", "thunderAttackPowerRate" },
                ["defense"] = new List<string> { "physicsDiffenceRate", "magicDiffenceRate", "fireDiffenceRate", "thunderDiffenceRate" },
                ["xp"] = new List<string> { "haveSoulRate" },
            },
        };

        // TODO: Scaling for all tiers. And separate enemy/boss scaling.
        public Dictionary<uint, int> InitializeScaling(Dictionary<uint, EnemyData> defaultData, Dictionary<uint, List<uint>> dupeMap)
        {
            // First, calculate scaling multipliers
            ScalingData scaling = GetGameScaling();
            Dictionary<int, int> speffectTiers = new Dictionary<int, int>();
            Dictionary<string, List<double>> scalingMult = new Dictionary<string, List<double>>();
            // DS3 is 7000 to 7170
            // DS1 is 7001 to 7015
            int baseTier = 0;
            if (scaling.AutoScaleStart == 0) throw new Exception($"Internal error: Missing scaling data");
            foreach (PARAM.Row row in game.Params["SpEffectParam"].Rows)
            {
                int spId = row.ID;
                if (spId < scaling.AutoScaleStart || spId > scaling.AutoScaleEnd)
                {
                    continue;
                }
                baseTier++;
                speffectTiers[spId] = baseTier;
                PARAM.Row spRow = game.Params["SpEffectParam"][spId];
                if (spRow == null)
                {
                    throw new Exception($"Error: Missing expected scaling speffect {spId}");
                }
                foreach (KeyValuePair<string, List<string>> fields in scaling.ScalingFields)
                {
                    AddMulti(scalingMult, fields.Key, (double)(float)spRow[fields.Value[0]].Value);
                }
            }
            if (baseTier != scaling.MaxTier)
            {
                throw new Exception($"Error: expected to find {scaling.MaxTier} scaling tiers between {scaling.AutoScaleStart} and {scaling.AutoScaleEnd}, found {baseTier}");
            }

            scaling.SectionPairs = new List<(int, int)>();
            for (int i = 2; i <= scaling.MaxTier; i++)
            {
                for (int j = 1; j < i; j++)
                {
                    scaling.SectionPairs.Add((j, i));
                }
            }
            // Console.WriteLine($"{string.Join(" ", scaling.SectionPairs)}");

            // For now, ignore dampening. Do this from scratch if needed (or hardcode breakpoints in loop)

            // Just use HP scaling for now. There isn't really a coherent scheme within any enemy in multiple tiers
            // scalingMult["xp"] = Enumerable.Repeat(1.0, 15).ToList();
            scalingMult["xp"] = scaling.SoulScaling ?? scalingMult["health"];
            // Build matrix from that
            Dictionary<string, List<double>> makeScalingMatrix(Dictionary<string, List<double>> scalingDict)
            {
                Dictionary<string, List<double>> scalingMatrix = new Dictionary<string, List<double>>();
                foreach (KeyValuePair<string, List<double>> mult in scalingDict)
                {
                    List<double> vals = mult.Value;
                    List<double> pairs = new List<double>();
                    foreach ((int i, int j) in scaling.SectionPairs)
                    {
                        pairs.Add(vals[j - 1] / vals[i - 1]);
                    }
                    scalingMatrix[mult.Key] = pairs;
                    // Console.WriteLine($"{mult.Key}: {string.Join(", ", eldenScaling.SectionPairs.Select((p, k) => $"{p.Item1}->{p.Item2}={pairs[k]}"))}");
                }
                return scalingMatrix;
            }
            scaling.ScalingMatrix = makeScalingMatrix(scalingMult);

            // Finally do classifications, from entity id to scaling tier
            Dictionary<uint, int> ret = new();
            // Existing NPC speffects
            Dictionary<int, int> npcEffects = game.Params["NpcParam"].Rows.ToDictionary(r => r.ID, r => (int)r[scaling.NpcField].Value);
            // Record effects used in maps to fill in blanks. map name -> speffect id -> count
            Dictionary<string, Dictionary<int, int>> mapCounts = new Dictionary<string, Dictionary<int, int>>();
            // First pass, directly available speffects
            foreach (KeyValuePair<uint, EnemyData> entry in defaultData)
            {
                EnemyData data = entry.Value;
                if (!npcEffects.TryGetValue(data.NPC, out int sp) || sp <= 0)
                {
                    if (data.Model != "c0000")
                    {
                        // This is mainly bonfires and Elizabeth in DS1
                        // Console.WriteLine($"Not filled {data.NPC}");
                        // ret[entry.Key] = 1;
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
            Dictionary<string, int> mapEffects = mapCounts.ToDictionary(e => e.Key, e => e.Value.OrderByDescending(s => s.Value).First().Key);
            // Second pass, try to infer
            foreach (KeyValuePair<uint, EnemyData> entry in defaultData)
            {
                if (ret.ContainsKey(entry.Key)) continue;
                EnemyData data = entry.Value;
                if (mapEffects.TryGetValue(data.MainMap, out int sp))
                {
                    // Console.WriteLine($"Filling in {entry.Key} in {data.MainMap} -> {sp} (from {string.Join(",", mapCounts[data.MainMap])})");
                    ret[entry.Key] = speffectTiers[sp];
                }
            }
            // Needed for DS1/DS3?
            foreach (KeyValuePair<uint, int> entry in manualEntityTiers)
            {
                uint entityId = entry.Key;
                int tier = entry.Value;
                ret[entityId] = tier;
                // Need to update dupes, or else non-random dupes may get scaled.
                // Alternatively, could put manual overrides in EnemyInfo and use DupeFrom
                if (dupeMap.TryGetValue(entityId, out List<uint> dupes))
                {
                    foreach (uint dupe in dupes)
                    {
                        ret[dupe] = tier;
                    }
                }
            }

            return ret;
        }

        // TODO: Scaling for all tiers. And separate enemy/boss scaling.
        public Dictionary<uint, int> InitializeEldenScaling(Dictionary<uint, EnemyData> defaultData, Dictionary<uint, List<uint>> dupeMap)
        {
            // First, calculate scaling multipliers
            Dictionary<int, int> speffectTiers = new Dictionary<int, int>();
            Dictionary<int, int> ignoreSpeffectTiers = new Dictionary<int, int>();
            Dictionary<string, List<double>> scalingMult = new Dictionary<string, List<double>>();
            for (int i = 1; i <= 34; i++)
            {
                int spId;
                if (i <= 20)
                {
                    spId = 7000 + 10 * i;
                    speffectTiers[spId] = i;
                    // Human speffect
                    speffectTiers[19350 + i] = i;
                }
                else
                {
                    // DLC ones, 20007000 to 20007130 for enemies (technically goes up to 7150), 20007200 to 20007330 for NPCs.
                    spId = 20007000 + 10 * (i - 21);
                    speffectTiers[spId] = i;
                    // Don't bother with humans, their scaling is completely inconsistent
                    ignoreSpeffectTiers[spId + 200] = i;
                }
                PARAM.Row spRow = game.Params["SpEffectParam"][spId];
                foreach (KeyValuePair<string, List<string>> fields in eldenScaling.ScalingFields)
                {
                    AddMulti(scalingMult, fields.Key, (double)(float)spRow[fields.Value[0]].Value);
                }
            }
            
            // Also merge in no-scaling into first tier. This is only Cave of Knowledge, and 20 is a cleaner total.
            speffectTiers[7000] = 1;

            eldenScaling.SectionPairs = new List<(int, int)>();
            for (int i = 2; i <= eldenScaling.MaxTier; i++)
            {
                for (int j = 1; j < i; j++)
                {
                    eldenScaling.SectionPairs.Add((j, i));
                }
            }
            // Console.WriteLine($"{string.Join(" ", eldenScaling.SectionPairs)}");

            // Some attempt to reduce the effect of scaling up/down, due to how later
            // game bosses are often manually tuned differently.
            // 1.1 ends up being a factor of 1.61 after 5 iterations, 1.05 is 1.276, 1.06 is 1.33
            // With 20 tiers, there are 19 iterations, but damage has 4 identical tiers.
            double target = 1; // 1.275;
            Dictionary<string, double> dampenTypes = new Dictionary<string, double>
            {
                ["damage"] = Math.Pow(target, 1.0 / 19),
                ["health"] = Math.Pow(target, 1.0 / 15),
            };

            Dictionary<string, List<double>> altScalingMult = new Dictionary<string, List<double>>(scalingMult);
            bool debugDampen = false;
            foreach (KeyValuePair<string, List<double>> mult in scalingMult.Where(m => dampenTypes.ContainsKey(m.Key)))
            {
                double factor = 1;
                double dampen = dampenTypes[mult.Key];
                List<double> vals = mult.Value.ToList();
                bool comparePrev = false;
                for (int i = 0; i < vals.Count; i++)
                {
                    // DLC<->DLC notes.
                    // Without any dampening at all, HP seems basically fine.
                    // Low->high damage is inconsequential, high->low damage is somewhat high.
                    // DLC<->Base notes.
                    // Early base->early DLC, HP fine, damage super low.
                    // Late base->early DLC, HP fine, damage a bit low.
                    // Base->late DLC, HP fine, damage super low
                    // Late DLC->early base, HP fine, damage super high
                    // Early DLC->early base, HP probably fine, damage a bit high
                    if (i == 20 && mult.Key == "health")
                    {
                        break;
                    }
                    // This should be kinda correct even if scadu scaling is removed, because DLC damage is super overtuned.
                    else if (i == 21 && mult.Key == "damage")
                    {
                        // Hardcode this based on rough testing. factor is at target at this point.
                        // There's a 1.18 difference from start to end of DLC but base values get inflated more than that.
                        dampen = 1.0 / Math.Pow(1.5, 1.0 / 15);
                        factor /= 1.5;
                        // 1     Lion->Radahn: 3.12287660767850 / 3.0734980370572855 = 1.0160659190361787
                        // 1/1.5 Lion->Radahn: 4.31944383978235 / 3.3331218600201646 = 1.2959153673896033
                        // 1/2   Lion->Radahn: 5.43724397983762 / 3.5305221392543205 = 1.5400679461497495
                        // 1/3   Lion->Radahn: 7.52058853569698 / 3.8287515976098190 = 1.9642403911478297
                        // factor = 1;
                        comparePrev = false;
                    }
                    double nodamp = vals[i] / factor;
                    double damp = vals[i] / (factor * dampen);
                    if (comparePrev && damp <= vals[i - 1])
                    {
                        vals[i] = nodamp;
                        if (debugDampen) Console.WriteLine($"Skipping {mult.Key} {i}: value {vals[i-1]:f4}->{nodamp:f4}->{damp:f4} with factor {factor:f4} ({mult.Value[i-1]:f4}->{mult.Value[i]:f4})");
                        continue;
                    }
                    vals[i] = damp;
                    factor *= dampen;
                    comparePrev = true;
                }
                if (debugDampen) Console.WriteLine($"{mult.Key} scaling: {string.Join(", ", mult.Value.Select((v, i) => $"{i}: {v:f4}->{vals[i]:f4}"))}");
                altScalingMult[mult.Key] = vals;
            }

            // Manually construct the XP list.
            scalingMult["xp"] = EldenSoulScaling;
            // Build matrix from that
            Dictionary<string, List<double>> makeScalingMatrix(Dictionary<string, List<double>> scalingDict)
            {
                Dictionary<string, List<double>> scalingMatrix = new Dictionary<string, List<double>>();
                foreach (KeyValuePair<string, List<double>> mult in scalingDict)
                {
                    List<double> vals = mult.Value;
                    List<double> pairs = new List<double>();
                    foreach ((int i, int j) in eldenScaling.SectionPairs)
                    {
                        pairs.Add(vals[j - 1] / vals[i - 1]);
                        if (debugDampen)
                        {
                            if (mult.Key == "damage" && i == 24 && j == 33) Console.WriteLine($"Lion->Radahn: {vals[j - 1]} / {vals[i - 1]} = {vals[j - 1] / vals[i - 1]}");
                        }
                    }
                    scalingMatrix[mult.Key] = pairs;
                    // Console.WriteLine($"{mult.Key}: {string.Join(", ", eldenScaling.SectionPairs.Select((p, k) => $"{p.Item1}->{p.Item2}={pairs[k]}"))}");
                }
                return scalingMatrix;
            }
            eldenScaling.ScalingMatrix = makeScalingMatrix(scalingMult);
            eldenScaling.UniqueScalingMatrix = makeScalingMatrix(altScalingMult);
            eldenScaling.UniqueScalingMatrix = eldenScaling.ScalingMatrix;

            // Finally do classifications, from entity id to scaling tier
            Dictionary<uint, int> ret = new();
            // Existing NPC speffects
            Dictionary<int, int> npcEffects = game.Params["NpcParam"].Rows
                .ToDictionary(r => r.ID, r => (int)r["spEffectID3"].Value);
            // Record effects used in maps to fill in blanks. map name -> speffect id -> count
            Dictionary<string, Dictionary<int, int>> mapCounts = new Dictionary<string, Dictionary<int, int>>();
            // First pass, directly available speffects
            foreach (KeyValuePair<uint, EnemyData> entry in defaultData)
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
            // mapEffects["m11_10_00_00"] = 7030;
            // Second pass, try to infer. Needed for DLC NPCs
            bool debugUnknown = !false;
            foreach (KeyValuePair<uint, EnemyData> entry in defaultData)
            {
                if (ret.ContainsKey(entry.Key)) continue;
                EnemyData data = entry.Value;
                if (mapEffects.TryGetValue(data.MainMap, out int sp))
                {
                    ret[entry.Key] = speffectTiers[sp];
                    if (debugUnknown)
                    {
                        npcEffects.TryGetValue(data.NPC, out int npcSp);
                        ignoreSpeffectTiers.TryGetValue(npcSp, out int ignoreTier);
                        // Console.WriteLine($"Filling in {data.Name} #{entry.Key} in {data.MainMap}, {ignoreTier} -> {speffectTiers[sp]} (from {string.Join(",", mapCounts[data.MainMap])})");
                    }
                }
            }
            foreach (KeyValuePair<uint, int> entry in manualEntityTiers)
            {
                uint entityId = entry.Key;
                int tier = entry.Value;
                ret[entityId] = tier;
                // Need to update dupes, or else non-random dupes may get scaled.
                // Alternatively, could put manual overrides in EnemyInfo and use DupeFrom
                if (dupeMap.TryGetValue(entityId, out List<uint> dupes))
                {
                    foreach (uint dupe in dupes)
                    {
                        ret[dupe] = tier;
                    }
                }
            }

            return ret;
        }

        private static readonly Dictionary<uint, int> manualEntityTiers = new()
        {
            // Ekzykes is normally 11, but its base HP (5753) and damage is higher than
            // Greyll (tier 17, 1639) and Agheel (tier 5, 1639) - however, it has low defense
            // [1048370800] = 15,
            // Removed after revamp, which deals with boundaries better
            // Nerf Rykard from 4 to 3, to match Godskin Noble
            // [16000800] = 3,
            // [16000801] = 3,
            // Bump Godrick up a tier?
            // [10000800] = 2,
        };

        // Hand-crafted attempt at finding the soul scaling curve.
        // This is used for turning bosses into regular enemies with an appropriate soul drop amount
        // Before revamp
        // public readonly static List<double> EldenSoulScaling = new List<double> { 1, 2.9, 4.785, 10, 20, 30 };
        // These are selected to fit various cross-tier constraints. Making them additive is easier to reason about.
        private static readonly List<double> eldenExps = new List<double>
        {
            0, 23, 43, 188, 233, 285, 487, 743, 769, 925,
            970, 1091, 1107, 1192, 1277, 1430, 1438, 1458, 1478, 1478,
            // TODO: Calculate for DLC. Use 14 tiers, but max is basically 12 (last 2 are boss-only).
            // Go up to 45x, given diff for 52500030
            1492, 1506, 1520, 1534, 1548, 1562, 1576, 1590, 1604, 1618, 1632, 1646, 1654, 1654,
        };
        public readonly static List<double> EldenSoulScaling = eldenExps.Select(exp => Math.Pow(10, exp / 1000)).ToList();

        // Scaling tiers
        // There are also NPC speffects 13941 to 19397, but only 193[56]0 range correspond to real scaling.
        // 0: do not scale
        // -- HP rates 0 to 1.813
        // 7000: Stranded Graveyard
        // 7010: Limgrave, Stormhill
        // 7020: Weeping Peninsula
        // 7030 7040: Stormveil, Peninsula Minibosses
        // -- HP rates 1.953 to 2.688
        // 7050: Early Ainsel
        // 7060: Anticipation, Liurnia, Bellum, Siofra, Academy, Fringefolk Grave - 19356
        // 7070: Rennala, Caelid, Precipice
        // 7080: Caria Manor, Abductor Virgins, Altus, Redmane Castle
        // -- HP rates 3.25 to 4.125
        // 7090: Nokron, Mt. Gelmir - 19359
        // 7100: Radahn, Capital Outskirts, Leyndell, Liurnia Tower - 19360
        // 7110: Leyndell, Capital Outskirts, Ainsel (incl Astel), Deeproot, Volcano Manor - 19361 (19360 for ghiza)
        // -- HP rates 4.844 to 5.484
        // 7120: Rykard, Mountaintops, Flame Peak
        // 7130: Subterranean, Moonlight Altar
        // -- HP rates 6.563 to 6.875
        // 7140: Fire Giant, Farum Azula
        // 7150: Maliketh, Dragonbarrow, Ashen Leyndell - 19365
        // 7160: Ashen Leyndell Bosses, Consecrated Snowfield
        // -- HP rates 7.047 to 7.422
        // 7170: Final bosses, Mohgwyn
        // 7180: Mohg, Haligtree, Snowfield Bosses
        // 7190: More Haligtree
        // 7200: Malenia

        private ScalingData GetGameScaling() => game.Sekiro ? sekiroScaling : (game.EldenRing ? eldenScaling : (game.DS1 ? ds1Scaling : ds3Scaling));

        // Returns a mapping from (source section, target section) to (scaling without xp, scaling with xp)
        public SpEffectValues EditScalingSpEffects()
        {
            SpEffectValues ret = new SpEffectValues();
            ScalingData d = GetGameScaling();
            if (d.ScalingMatrix.Any(e => !d.ScalingFields.ContainsKey(e.Key) || e.Value.Count != d.SectionPairs.Count))
            {
                throw new Exception($"Internal error: bad scaling values");
            }
            ret.MaxTier = d.MaxTier;

            int newSpBase = d.NewScalingBase;
            PARAM.Row defaultSp = game.Params["SpEffectParam"][d.ScalingBase];

            PARAM.Row createCustomEffect(string name)
            {
                int newCustomSp = newSpBase++;
                PARAM.Row customSp = game.AddRow("SpEffectParam", newCustomSp);
#if DEBUG
                customSp.Name = name;
#endif
                GameEditor.CopyRow(defaultSp, customSp);
                return customSp;
            }

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

                    int fillFields(Dictionary<string, List<double>> scalingMatrix, bool includeXp, string desc)
                    {
                        PARAM.Row sp = createCustomEffect(desc);
                        foreach (KeyValuePair<string, List<string>> entry in d.ScalingFields)
                        {
                            double val = scalingMatrix[entry.Key][index];
                            // If scaling down, just take opposite of calculated difference. If scaling up, nerf a bit, since these numbers tend to come from simpler enemies.
                            if (invert) val = 1 / val;
                            else if (game.Sekiro) val = val / 1.333333;
                            // if (i == 3 && j == 33 && entry.Key == "damage") Console.WriteLine($"{i}->{j} {entry.Key}: {val}");
                            foreach (string field in entry.Value)
                            {
                                if (!includeXp && entry.Key == "xp") continue;
                                sp[field].Value = (float)val;
                            }
                        }
                        return sp.ID;
                    }
                    AreaScalingValue sps = new AreaScalingValue();
                    ret.Areas[(i, j)] = sps;
                    sps.FixedScaling = fillFields(d.ScalingMatrix, false, $"Randomizer scaling fixedxp {i}->{j}");
                    sps.RegularScaling = fillFields(d.ScalingMatrix, true, $"Randomizer scaling regular {i}->{j}");
                    if (d.UniqueScalingMatrix != null)
                    {
                        sps.UniqueFixedScaling = fillFields(d.UniqueScalingMatrix, false, $"Randomizer scaling fixedxp unique {i}->{j}");
                        sps.UniqueRegularScaling = fillFields(d.UniqueScalingMatrix, true, $"Randomizer scaling regular unique {i}->{j}");
                    }
                    else
                    {
                        sps.UniqueFixedScaling = sps.FixedScaling;
                        sps.UniqueRegularScaling = sps.RegularScaling;
                    }
                }
            }

            // A few more for special cases
            // Tutorial bosses, before upgrading anything is possible
            PARAM.Row tutSp = createCustomEffect("Randomizer scaling early boss");
            ret.TutorialScaling = tutSp.ID;
            foreach (string fieldType in new[] { "health", "damage" })
            {
                foreach (string field in d.ScalingFields[fieldType])
                {
                    tutSp[field].Value = 0.75f;
                }
            }
            // Boss health scaling option
            PARAM.Row basicSp = createCustomEffect("Randomizer scaling boss->basic");
            ret.BossAsBasicScaling = basicSp.ID;
            foreach (string fieldType in new[] { "health", "xp" })
            {
                foreach (string field in d.ScalingFields[fieldType])
                {
                    // This may reduce xp a bit much, though since bosshp is a default option,
                    // the values can be made slightly higher to start with.
                    basicSp[field].Value = 0.25f;
                }
            }
            // Enemy multiplier scaling, for 2-5 multipliers
            int amt = 2;
            foreach (double reduce in bossDupeScaling)
            {
                PARAM.Row dupeSp = createCustomEffect($"Randomizer scaling {amt++}x boss");
                ret.Dupes.Add(dupeSp.ID);
                foreach (string fieldType in new[] { "health" })
                {
                    foreach (string field in d.ScalingFields[fieldType])
                    {
                        dupeSp[field].Value = reduce;
                    }
                }
            }
            // Phase changes, and special Rykard scaling (Wyvern is 5% and done in NpcParam)
            PARAM.Row phaseDown = createCustomEffect("Randomizer phase down");
            PARAM.Row phaseUp = createCustomEffect("Randomizer phase up");
            PARAM.Row rykardSp = createCustomEffect("Randomizer Rykard field boss");
            PARAM.Row tutDown = createCustomEffect("Randomizer early boss health down");
            PARAM.Row tutUp = createCustomEffect("Randomizer early boss health up");
            ret.PhaseDownScaling = phaseDown.ID;
            ret.PhaseUpScaling = phaseUp.ID;
            ret.RykardScaling = rykardSp.ID;
            ret.TutorialDownScaling = tutDown.ID;
            ret.TutorialUpScaling = tutUp.ID;
            foreach (string fieldType in new[] { "health" })
            {
                foreach (string field in d.ScalingFields[fieldType])
                {
                    phaseDown[field].Value = 0.7f;
                    phaseUp[field].Value = 1.4f;
                    rykardSp[field].Value = 0.2f;
                    tutDown[field].Value = 1 / 2f;
                    tutUp[field].Value = 2f;
                }
            }

            game.Params["SpEffectParam"].Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
            return ret;
        }

        // Utility for boss dupes, whose drops can be scaled with multiplier
        // This scales both health and XP when the option is enabled.
        private static readonly List<double> bossDupeScaling = Enumerable.Range(2, 4).Select(c => Math.Sqrt(1.0 / c)).ToList();

        public static double GetXpModifier(int dupeCount, bool scaleDown)
        {
            if (dupeCount <= 0) return 1;
            int dupeIndex = dupeCount - 1;
            double multiplier = 1;
            if (scaleDown)
            {
                int scaleIndex = Math.Min(dupeIndex, bossDupeScaling.Count - 1);
                multiplier = bossDupeScaling[scaleIndex];
            }
            return multiplier;
        }

        public static double GetXpRate(int dupeCount, bool scaleDown)
        {
            if (dupeCount <= 0) return 1;
            int dupeIndex = dupeCount - 1;
            double multiplier = GetXpModifier(dupeCount, scaleDown);
            return (dupeIndex + 2) * multiplier;
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
            public int RykardScaling { get; set; }
            public int TutorialUpScaling { get; set; }
            public int TutorialDownScaling { get; set; }
            public int MaxTier { get; set; }

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
            public int UniqueRegularScaling { get; set; }
            public int UniqueFixedScaling { get; set; }
        }

        public class DupeScalingValue
        {
            public int NoHealthScaling { get; set; }
            public int HealthScaling { get; set; }
        }
    }
}
