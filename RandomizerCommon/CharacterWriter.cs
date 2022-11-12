using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class CharacterWriter
    {
        private GameData game;
        private LocationData data;

        public CharacterWriter(GameData game, LocationData data)
        {
            this.game = game;
            this.data = data;
        }

        private class GameCharacters
        {
            public int StartId { get; set; }
            public Dictionary<string, EquipCategory> BaseStart { get; set; }
            public List<CharacterClass> Classes { get; set; }
            public List<string> Stats { get; set; }
            public List<string> WeaponSlots { get; set; }
            // Items which wouldn't be considered weapons, and optional category
            public Dictionary<int, EquipCategory> WeaponCategories { get; set; }
            public List<string> ArmorTypes { get; set; }
            public List<string> ArmorSlots { get; set; }
            public List<EquipCategory> MagicTypes { get; set; }

        }

        public enum EquipCategory
        {
            UNSET,
            WEAPON, SHIELD, DOUBLE_WEAPON,
            CATALYST, TALISMAN, FLAME,
            SORCERY, MIRACLE, PYROMANCY,
            BOW, ARROW, BOLT,
            HEAD, BODY, ARM, LEG
        }

        public class CharacterClass
        {
            public string Name { get; set; }
            public Dictionary<string, EquipCategory> Start { get; set; }
        }

        private class ArmorSet
        {
            public int[] Ids = new int[4];
            public float Weight { get; set; }
        }

        private struct StatReq
        {
            public int Str { get; set; }
            public int Dex { get; set; }
            public int Int { get; set; }
            public int Fai { get; set; }
            public int Arc { get; set; }
            public int Att { get; set; }
            public bool TwoHand { get; set; }
            public int Eligible(StatReq ch)
            {
                int[] comps = new int[] {
                    Str == 0 ? 0 : ch.Str - Str,
                    Dex == 0 ? 0 : ch.Dex - Dex,
                    Int == 0 ? 0 : ch.Int - Int,
                    Arc == 0 ? 0 : ch.Arc - Arc,
                    Fai == 0 ? 0 : ch.Fai - Fai,
                };
                int miss = comps.Where(c => c < 0).Sum();
                if (miss < 0) return miss;
                // Weapons with no requirements, slight penalty
                if (Str == 0 && Dex == 0 && Int == 0 && Fai == 0 && Arc == 0) return 4;
                return comps.Sum();
            }
            public void Adjust(StatReq wep)
            {
                if (wep.Str > Str) Str = wep.Str;
                if (wep.Dex > Dex) Dex = wep.Dex;
                if (wep.Int > Int) Int = wep.Int;
                if (wep.Fai > Fai) Fai = wep.Fai;
                if (wep.Arc > Arc) Arc = wep.Arc;
            }
        }

        private static readonly Dictionary<string, EquipCategory> DS3BaseStart = new Dictionary<string, EquipCategory>()
        {
                { "equip_Wep_Right", EquipCategory.WEAPON },
                { "equip_Wep_Left", EquipCategory.SHIELD },
        };
        private static readonly List<CharacterClass> DS3Classes = new List<CharacterClass>()
        {
            new CharacterClass
            {
                Name = "Knight",
                Start = new Dictionary<string, EquipCategory>(),
            },
            new CharacterClass
            {
                Name = "Mercenary",
                Start = new Dictionary<string, EquipCategory>(),
            },
            new CharacterClass
            {
                Name = "Warrior",
                Start = new Dictionary<string, EquipCategory>(),
            },
            new CharacterClass
            {
                Name = "Herald",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.TALISMAN },
                    { "equip_Spell_01", EquipCategory.MIRACLE },
                },
            },
            new CharacterClass
            {
                Name = "Thief",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.BOW },
                    // Depends on selection of bow/crossbow
                    { "equip_Arrow", EquipCategory.ARROW },
                    { "equip_Bolt", EquipCategory.BOLT },
                },
            },
            new CharacterClass
            {
                Name = "Assassin",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.CATALYST },
                    { "equip_Spell_01", EquipCategory.SORCERY },
                },
            },
            new CharacterClass
            {
                Name = "Sorcerer",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Right", EquipCategory.CATALYST },
                    { "equip_Spell_01", EquipCategory.SORCERY },
                    { "equip_Spell_02", EquipCategory.SORCERY },
                },
            },
            new CharacterClass
            {
                Name = "Pyromancer",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.FLAME },
                    { "equip_Spell_01", EquipCategory.PYROMANCY },
                },
            },
            new CharacterClass
            {
                Name = "Cleric",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Right", EquipCategory.TALISMAN },
                    { "equip_Spell_01", EquipCategory.MIRACLE },
                    { "equip_Spell_02", EquipCategory.MIRACLE },
                },
            },
            new CharacterClass
            {
                Name = "Deprived",
                Start = new Dictionary<string, EquipCategory>(),
            },
        };

        private static readonly Dictionary<string, EquipCategory> EldenBaseStart = new Dictionary<string, EquipCategory>();
        private static readonly List<CharacterClass> EldenClasses = new List<CharacterClass>()
        {
            new CharacterClass
            {
                Name = "Vagabond",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Subwep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.SHIELD },
                },
            },
            new CharacterClass
            {
                Name = "Warrior",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.DOUBLE_WEAPON },
                    { "equip_Subwep_Left", EquipCategory.SHIELD },
                },
            },
            new CharacterClass
            {
                Name = "Hero",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.SHIELD },
                },
            },
            new CharacterClass
            {
                Name = "Bandit",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.SHIELD },
                    { "equip_Subwep_Left", EquipCategory.BOW },
                    { "equip_Arrow", EquipCategory.ARROW },
                },
            },
            new CharacterClass
            {
                Name = "Astrologer",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.CATALYST },
                    { "equip_Subwep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.SHIELD },
                    { "equip_Spell_01", EquipCategory.SORCERY },
                    { "equip_Spell_02", EquipCategory.SORCERY },
                },
            },
            new CharacterClass
            {
                Name = "Prophet",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.TALISMAN },
                    { "equip_Subwep_Left", EquipCategory.SHIELD },
                    { "equip_Spell_01", EquipCategory.MIRACLE },
                    { "equip_Spell_02", EquipCategory.MIRACLE },
                },
            },
            new CharacterClass
            {
                Name = "Confessor",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.SHIELD },
                    { "equip_Subwep_Left", EquipCategory.TALISMAN },
                    { "equip_Spell_01", EquipCategory.MIRACLE },
                    { "equip_Spell_02", EquipCategory.MIRACLE },
                },
            },
            new CharacterClass
            {
                Name = "Samurai",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.BOW },
                    { "equip_Subwep_Left", EquipCategory.SHIELD },
                    { "equip_Arrow", EquipCategory.ARROW },
                    { "equip_SubArrow", EquipCategory.ARROW },
                },
            },
            new CharacterClass
            {
                Name = "Prisoner",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                    { "equip_Wep_Left", EquipCategory.CATALYST },
                    { "equip_Subwep_Left", EquipCategory.SHIELD },
                    { "equip_Spell_01", EquipCategory.SORCERY },
                },
            },
            new CharacterClass
            {
                Name = "Wretch",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.WEAPON },
                },
            },
        };

        private readonly List<EquipCategory> ArmorCats = new List<EquipCategory> { EquipCategory.HEAD, EquipCategory.BODY, EquipCategory.ARM, EquipCategory.LEG };

        public void Write(Random random, RandomizerOptions opt)
        {
            if (opt["nooutfits"] && opt["nostarting"])
            {
                return;
            }
            GameCharacters g = new GameCharacters
            {
                StartId = 3000,
                WeaponSlots = new List<string> { "equip_Wep_Right", "equip_Subwep_Right", "equip_Wep_Left", "equip_Subwep_Left" },
                ArmorTypes = new List<string> { "headEquip", "bodyEquip", "armEquip", "legEquip" },
            };
            if (game.DS3)
            {
                g.Stats = new List<string> { "Vit", /* vigor */ "Wil" /* attunement */, "End", "Str", "Dex", "Mag", "Fai", "Luc", "Durability" /* vit */ };
                g.ArmorSlots = new List<string> { "equip_Helm", "equip_Armor", "equip_Gaunt", "equip_Leg" };
                g.BaseStart = DS3BaseStart;
                g.Classes = DS3Classes;
                g.WeaponCategories = new Dictionary<int, EquipCategory>
                {
                    { 8, EquipCategory.UNSET },  // catalyst/talisman/flame - will be added separately
                    { 10, EquipCategory.BOW },
                    { 11, EquipCategory.BOW },
                    { 13, EquipCategory.ARROW },
                    { 14, EquipCategory.BOLT },
                };
                g.MagicTypes = new List<EquipCategory> { EquipCategory.SORCERY, EquipCategory.MIRACLE, EquipCategory.PYROMANCY };
            }
            else if (game.EldenRing)
            {
                g.Stats = new List<string> { "Vit", /* vigor */ "Wil" /* mind */, "End", "Str", "Dex", "Mag", "Fai", "Luc" /* arcane */ };
                g.ArmorSlots = new List<string> { "equip_Helm", "equip_Armer", "equip_Gaunt", "equip_Leg" };
                g.BaseStart = EldenBaseStart;
                g.Classes = EldenClasses;
                g.WeaponCategories = new Dictionary<int, EquipCategory>
                {
                    { 8, EquipCategory.UNSET },  // Also magic-doers
                    { 10, EquipCategory.BOW },
                    { 11, EquipCategory.UNSET },  // Don't deal with crossbows
                    { 13, EquipCategory.ARROW },
                    { 14, EquipCategory.UNSET },
                };
                g.MagicTypes = new List<EquipCategory> { EquipCategory.SORCERY, EquipCategory.MIRACLE };
            }

            Dictionary<EquipCategory, List<ItemKey>> items = new Dictionary<EquipCategory, List<ItemKey>>();
            Dictionary<ItemKey, float> weights = new Dictionary<ItemKey, float>();
            Dictionary<ItemKey, StatReq> requirements = new Dictionary<ItemKey, StatReq>();
            HashSet<ItemKey> crossbows = new HashSet<ItemKey>();
            PARAM magics = game.Param("Magic");
            bool twoHand = !opt["onehand"];
            SortedDictionary<int, List<ItemKey>> cats = new SortedDictionary<int, List<ItemKey>>();
            foreach (ItemKey key in data.Data.Keys)
            {
                if (key.Type == ItemType.WEAPON)
                {
                    PARAM.Row row = game.Item(key);
                    if (row == null)
                    {
                        Console.WriteLine($"Warning: nonexistent {key} found in character equipment data");
                        continue;
                    }
                    EquipCategory mainCat = EquipCategory.WEAPON;
                    int weaponCategory = (byte)row["weaponCategory"].Value;
                    AddMulti(cats, weaponCategory, key);
                    if (g.WeaponCategories.ContainsKey(weaponCategory))
                    {
                        mainCat = g.WeaponCategories[weaponCategory];
                    }
                    if ((byte)row["enableGuard"].Value == 1)
                    {
                        mainCat = EquipCategory.SHIELD;
                    }
                    if (mainCat == EquipCategory.BOW || mainCat == EquipCategory.ARROW || mainCat == EquipCategory.BOLT)
                    {
                        // Disable greatbow for starting - requirements too far off
                        if (game.DS3)
                        {
                            if ((byte)row["DisableShoot"].Value == 1) continue;
                        }
                        else
                        {
                            // Who knows what this is in ER
                            if (game.Name(key).Contains("Great")) continue;
                        }
                    }
                    if (mainCat == EquipCategory.BOW)
                    {
                        if ((byte)row["boltSlotEquipable"].Value == 1) crossbows.Add(key);
                    }
                    if (mainCat != EquipCategory.UNSET)
                    {
                        AddMulti(items, mainCat, key);
                    }
                    if ((byte)row["enableMagic"].Value == 1) AddMulti(items, EquipCategory.CATALYST, key);
                    if ((byte)row["enableMiracle"].Value == 1) AddMulti(items, EquipCategory.TALISMAN, key);
                    if (game.DS3 && (byte)row["enablePyromancy"].Value == 1) AddMulti(items, EquipCategory.FLAME, key);

                    int str = (byte)row["properStrength"].Value;
                    // Add two hand adjustment for weapons. Note this doesn't work exactly for casting items, but does not affect casting.
                    if (twoHand && (byte)row[game.DS3 ? "Unk14" : "bothHandEquipable"].Value == 0 && (mainCat == EquipCategory.WEAPON || mainCat == EquipCategory.UNSET))
                    {
                        str = (int)Math.Ceiling(str / 1.5);
                    }
                    // TODO: Why sbyte again?
                    requirements[key] = new StatReq
                    {
                        Str = str,
                        Dex = (byte)row["properAgility"].Value,
                        Int = (byte)row["properMagic"].Value,
                        Fai = (byte)row["properFaith"].Value,
                        Arc = game.EldenRing ? (byte)row["properLuck"].Value : 0,
                    };
                    weights[key] = (float)row["weight"].Value;
                }
                else if (key.Type == ItemType.ARMOR)
                {
                    PARAM.Row row = game.Item(key);
                    if (row == null)
                    {
                        Console.WriteLine($"Warning: nonexistent {key} found in character equipment data");
                        continue;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        if ((byte)row[g.ArmorTypes[i]].Value == 1)
                        {
                            AddMulti(items, ArmorCats[i], key);
                            weights[key] = (float)row["weight"].Value;
                            break;
                        }
                    }
                }
                else if (key.Type == ItemType.GOOD)
                {
                    PARAM.Row magic = magics[key.ID];
                    // Exclude Spook and Tears of Denial as they can be a key item, useful though they are
                    if (magic != null && key.ID != 1354000 && key.ID != 3520000)
                    {
                        int magicCat = (byte)magic["ezStateBehaviorType"].Value;
                        AddMulti(items, g.MagicTypes[magicCat], key);
                        requirements[key] = new StatReq
                        {
                            Str = 0,
                            Dex = 0,
                            Int = (byte)magic["requirementIntellect"].Value,
                            Fai = (byte)magic["requirementFaith"].Value,
                            Arc = game.EldenRing ? (byte)magic["requirementLuck"].Value : 0,
                            Att = (byte)magic["slotLength"].Value,
                        };
                    }
                }
            }
            // foreach (var v in cats) Console.WriteLine($"{v.Key}: {string.Join(", ", v.Value.Select(k => game.Name(k)))}");
            // Generate some armor sets. One downside of this approach is that each piece is represented only once - but it is just one shuffle per category, and tends to result in a similar distribution to normal.
            List<List<ItemKey>> weightedArmors = new List<List<ItemKey>>();
            for (int i = 0; i < 4; i++)
            {
                weightedArmors.Add(WeightedShuffle(random, items[ArmorCats[i]], item => 1/weights[item]));
            }
            List<ArmorSet> armors = new List<ArmorSet>();
            int maxArmors = weightedArmors.Select(rank => rank.Count).Min();
            for (int num = 0; num < maxArmors; num++)
            {
                ArmorSet armor = new ArmorSet();
                for (int i = 0; i < 4; i++)
                {
                    ItemKey item = weightedArmors[i][num];
                    armor.Ids[i] = item.ID;
                    armor.Weight += weights[item];
                }
                armors.Add(armor);
            }
            armors.Sort((a, b) => a.Weight.CompareTo(b.Weight));

            PARAM chara = game.Param("CharaInitParam");
            bool allowCheat = false;
            bool printChars = true;
#if DEBUG
            allowCheat = true;
            printChars = false;
#endif
            bool cheat = allowCheat && opt["cheat"];

            List<float> eldenWeights = new List<float>
            {
                45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 46.6f,
                48.2f, 49.8f, 51.4f, 52.9f, 54.5f, 56.1f, 57.7f, 59.3f, 60.9f, 62.5f,
                64.1f, 65.6f, 67.2f, 68.8f, 70.4f, 72.0f, 73.0f, 74.1f, 75.2f, 76.4f,
                77.6f, 78.9f, 80.2f, 81.5f, 82.8f, 84.1f, 85.4f, 86.8f, 88.1f, 89.5f,
                90.9f, 92.3f, 93.7f, 95.1f, 96.5f, 97.9f, 99.4f, 100.8f, 102.2f, 103.7f,
                105.2f, 106.6f, 108.1f, 109.6f, 111.0f, 112.5f, 114.0f, 115.5f, 117.0f, 118.5f,
                120.0f, 121.0f, 122.1f, 123.1f, 124.1f, 125.1f, 126.2f, 127.2f, 128.2f, 129.2f,
                130.3f, 131.3f, 132.3f, 133.3f, 134.4f, 135.4f, 136.4f, 137.4f, 138.5f, 139.5f,
                140.5f, 141.5f, 142.6f, 143.6f, 144.6f, 145.6f, 146.7f, 147.7f, 148.7f, 149.7f,
                150.8f, 151.8f, 152.8f, 153.8f, 154.9f, 155.9f, 156.9f, 157.9f, 159.0f, 160.0f,
            };
            float getMaxWeight(PARAM.Row row)
            {
                if (game.DS3)
                {
                    return 40 + (sbyte)row["baseDurability"].Value;
                }
                else
                {
                    int end = (byte)row["baseEnd"].Value;
                    return eldenWeights[end];
                }
            }

            for (int i = 0; i < 10; i++)
            {
                if (opt["nostarting"]) break;
                PARAM.Row row = chara[g.StartId + i];
                int getStat(string name)
                {
                    return game.EldenRing ? (int)(byte)row[name].Value : (sbyte)row[name].Value;
                }
                void setStat(string name, int val)
                {
                    if (game.EldenRing)
                    {
                        row[name].Value = (byte)val;
                    }
                    else
                    {
                        row[name].Value = (sbyte)val;
                    }
                }
                // First, always fudge magic to 10, so that Orbeck quest is possible.
                // This could alternatively be an ESD edit.
                if (game.DS3 && getStat("baseMag") < 10)
                {
                    setStat("baseMag", 10);
                }
                if (cheat)
                {
                    foreach (string stat in g.Stats) setStat($"base{stat}", 90);
                }

                // Then, see stat diffs for weapons/spells/catalysts, and fudge if necessary
                CharacterClass chClass = g.Classes[i];
                // In Elden Ring, everyone starts with 2
                int attAmt = 2;
                if (game.DS3)
                {
                    int attStat = getStat("baseWil");
                    attAmt = attStat < 10 ? 0 : attStat < 14 ? 1 : 2;
                }
                StatReq chReqs = new StatReq
                {
                    Str = getStat("baseStr"),
                    Dex = getStat("baseDex"),
                    Int = getStat("baseMag"),
                    Fai = getStat("baseFai"),
                    Arc = getStat("baseLuc"),
                    Att = attAmt,
                };
                if (opt["nohand"])
                {
                    // To ignore requirements: simulate being at very high stats
                    chReqs.Str = chReqs.Dex = chReqs.Int = chReqs.Fai = chReqs.Arc = 90;
                }
                StatReq dynamicReqs = chReqs;
                double fudgeFactor = 1.5;
                float weaponWeight = 0f;
                int attSlots = 0;
                bool crossbowSelected = false;
                if (printChars) Console.WriteLine($"Randomizing starting equipment for {chClass.Name}");
                foreach (KeyValuePair<string, EquipCategory> entry in g.BaseStart.Concat(chClass.Start))
                {
                    EquipCategory originalCat = entry.Value;
                    EquipCategory cat = originalCat;
                    if (cat == EquipCategory.DOUBLE_WEAPON)
                    {
                        cat = EquipCategory.WEAPON;
                    }
                    // TODO: If a catalyst etc also doubles as a weapon, maybe skip its slot.
                    // This crossbow/bow logic relies on iteration order - try to make the order fixed...
                    if ((cat == EquipCategory.ARROW && crossbowSelected) || (cat == EquipCategory.BOLT && !crossbowSelected)) continue;
                    // Console.WriteLine(originalCat);
                    Dictionary<ItemKey, int> statDiffs = items[cat].ToDictionary(item => item, item => requirements[item].Eligible(dynamicReqs));
                    List<ItemKey> candidates = items[cat];
                    if (cat == EquipCategory.SHIELD || chClass.Name == "Deprived" || chClass.Name == "Wretch")
                    {
                        candidates = candidates.Where(item => statDiffs[item] >= 0).ToList();
                    }
                    if (cat == EquipCategory.SORCERY || cat == EquipCategory.MIRACLE || cat == EquipCategory.PYROMANCY)
                    {
                        // Fit within attunement slots. Alternatively could increase attunement, but that unbalances things potentially.
                        // Unfortunately means that pyromancer can't start with Chaos Bed Vestiges. Maybe for the best.
                        if (attSlots == chReqs.Att)
                        {
                            row[entry.Key].Value = -1;
                            continue;
                        }
                        candidates = candidates.Where(item => attSlots + requirements[item].Att <= chReqs.Att).ToList();
                    }
                    // Select weapon and adjust stats if necessary
                    List<ItemKey> weightKeys = WeightedShuffle(random, candidates, item =>
                    {
                        int diff = statDiffs[item];
                        if (diff >= 4) return (float)Math.Pow(2, -4 * (Math.Min(diff, 20) / 20.0));
                        if (diff >= 0) return 2;
                        return (float)Math.Pow(fudgeFactor, diff);
                    });
                    ItemKey selected = weightKeys[0];
                    items[cat].Remove(selected);
                    if (statDiffs[selected] < 0)
                    {
                        dynamicReqs.Adjust(requirements[selected]);
                        fudgeFactor *= -statDiffs[selected];
                    }
                    row[entry.Key].Value = selected.ID;
                    int quantity = 1;
                    if (originalCat == EquipCategory.DOUBLE_WEAPON)
                    {
                        row["equip_Wep_Left"].Value = selected.ID;
                        quantity = 2;
                    }
                    if (weights.ContainsKey(selected))
                    {
                        weaponWeight += quantity * weights[selected];
                    }
                    attSlots += requirements[selected].Att;
                    if (printChars) Console.WriteLine($"  {entry.Key} is now {game.Name(selected)}, meets requirements by {statDiffs[selected]}");
                }
                // In Elden Ring, also change display characters
                if (game.EldenRing)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        PARAM.Row row2 = chara[3100 + i * 2 + j];
                        foreach (string field in g.BaseStart.Keys
                            .Concat(chClass.Start.Keys)
                            .Concat(g.Stats.Select(s => $"base{s}")))
                        {
                            row2[field].Value = row[field].Value;
                        }
                    }
                }
                int statChange = dynamicReqs.Eligible(chReqs);
                if (statChange < 0 && !opt["nohand"])
                {
                    setStat("baseStr", dynamicReqs.Str);
                    setStat("baseDex", dynamicReqs.Dex);
                    setStat("baseMag", dynamicReqs.Int);
                    setStat("baseFai", dynamicReqs.Fai);
                    if (game.EldenRing)
                    {
                        setStat("baseLuc", dynamicReqs.Arc);
                        row["soulLv"].Value = (short)((short)row["soulLv"].Value - statChange);
                    }
                    else
                    {
                        row["soulLvl"].Value = (short)((short)row["soulLvl"].Value - statChange);
                    }
                }
                // Armor time
                float totalWeight = getMaxWeight(row);
                List<ArmorSet> availableSets = armors.TakeWhile(armor => armor.Weight + weaponWeight < totalWeight * 0.69f).ToList();
                if (availableSets.Count == 0) availableSets = new List<ArmorSet> { armors[0] };
                ArmorSet selectedArmor = Choice(random, availableSets);
                armors.Remove(selectedArmor);
                if (printChars)
                {
                    Console.WriteLine($"  Armor: {string.Join(", ", selectedArmor.Ids.Select(id => game.Name(new ItemKey(ItemType.ARMOR, id))))}");
                    Console.WriteLine($"  Weight: weapons {weaponWeight:0.##} + armor {selectedArmor.Weight:0.##} / {totalWeight:0.##} = {100 * (weaponWeight + selectedArmor.Weight) / totalWeight:0.##}%");
                }
                for (int j = 0; j < 4; j++)
                {
                    if ((int)row[g.ArmorSlots[j]].Value != -1)
                    {
                        row[g.ArmorSlots[j]].Value = selectedArmor.Ids[j];
                    }
                }

                if (cheat)
                {
                    PARAM reinforce = game.Param("ReinforceParamWeapon");
                    HashSet<int> reinforceLevels = new HashSet<int>(reinforce.Rows.Select(r => (int)r.ID));
                    foreach (string wep in g.WeaponSlots)
                    {
                        int id = (int)row[wep].Value;
                        if (id > 0)
                        {
                            id = id - (id % 100);
                            PARAM.Row item = game.Item(new ItemKey(ItemType.WEAPON, id));
                            int reinforceId = (short)item["reinforceTypeId"].Value;
                            while (reinforceLevels.Contains(reinforceId + 5))
                            {
                                reinforceId += 5;
                                id += 5;
                            }
                            row[wep].Value = id;
                        }
                    }
                }
            }

            // Now, have fun with NPCs
            if (opt["nooutfits"]) return;
            // Just remove Symbol of Avarice first (may not matter with Irregulator, but those are more chaotic anyway)
            if (game.DS3) armors.RemoveAll(set => set.Ids[0] == 78500000);
            Dictionary<int, ArmorSet> npcArmors = new Dictionary<int, ArmorSet>();
            Dictionary<string, int> npcNameRows = new Dictionary<string, int>();
            Func<ItemType, PARAM.Cell, float> cellWeight = (type, cell) =>
            {
                int id = (int)cell.Value;
                if (id == -1) return 0;
                ItemKey key = new ItemKey(type, id);
                if (!weights.ContainsKey(key)) return 0;
                return weights[key];
            };
            foreach (PARAM.Row row in chara.Rows.Where(r => r.ID >= 10000))
            {
                string name = game.CharacterName(row.ID);
                if (name == null || name.Contains("Mimic Tear") || name == "Human") continue;
                ArmorSet selectedArmor;
                if (!npcNameRows.TryGetValue(name, out int baseId))
                {
                    npcNameRows[name] = baseId = row.ID / 10;
                }
                if (!npcArmors.ContainsKey(baseId))
                {
                    float weaponWeight = g.WeaponSlots.Select(slot => cellWeight(ItemType.WEAPON, row[slot])).Sum();
                    float armorWeight = g.ArmorSlots.Select(slot => cellWeight(ItemType.ARMOR, row[slot])).Sum();
                    float weightLimit = weaponWeight + armorWeight;
                    float totalWeight = getMaxWeight(row);
                    int armorLimit = armors.FindIndex(armor => armor.Weight + weaponWeight > weightLimit);
                    if (armorLimit == -1)
                    {
                        armorLimit = armors.Count - 1;
                    }
                    armorLimit = Math.Min(20, armorLimit);
                    npcArmors[baseId] = selectedArmor = armors[random.Next(armorLimit)];
                    armors.Remove(selectedArmor);
                    if (printChars) Console.WriteLine($"Armor for {name}: {100 * weightLimit / totalWeight:0.##}% -> {100 * (selectedArmor.Weight + weaponWeight) / totalWeight:0.##}%: {string.Join(", ", selectedArmor.Ids.Select(id => game.Name(new ItemKey(ItemType.ARMOR, id))))}");
                }
                selectedArmor = npcArmors[baseId];
                for (int j = 0; j < 4; j++)
                {
                    if ((int)row[g.ArmorSlots[j]].Value != -1)
                    {
                        row[g.ArmorSlots[j]].Value = selectedArmor.Ids[j];
                    }
                }
            }
        }
    }
}