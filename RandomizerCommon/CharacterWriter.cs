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

        private const int startId = 3000;

        public enum EquipCategory
        {
            UNSET,
            WEAPON, SHIELD,
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
        private static readonly Dictionary<string, EquipCategory> baseStart = new Dictionary<string, EquipCategory>()
        {
                { "equip_Wep_Right", EquipCategory.WEAPON },
                { "equip_Wep_Left", EquipCategory.SHIELD },
        };
        private static readonly List<CharacterClass> classes = new List<CharacterClass>()
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
        private class ArmorSet
        {
            public int[] Ids = new int[4];
            public float Weight { get; set; }
        }
        private struct StatReq
        {
            public sbyte Str { get; set; }
            public sbyte Dex { get; set; }
            public sbyte Mag { get; set; }
            public sbyte Fai { get; set; }
            public sbyte Att { get; set; }
            public bool TwoHand { get; set; }
            public int Eligible(StatReq ch)
            {
                int[] comps = new int[] {
                    Str == 0 ? 0 : ch.Str - Str,
                    Dex == 0 ? 0 : ch.Dex - Dex,
                    Mag == 0 ? 0 : ch.Mag - Mag,
                    Fai == 0 ? 0 : ch.Fai - Fai,
                };
                int miss = comps.Where(c => c < 0).Sum();
                if (miss < 0) return miss;
                // Weapons with no requirements, slight penalty
                if (Str == 0 && Dex == 0 && Mag == 0 && Fai == 0) return 4;
                return comps.Sum();
            }
            public void Adjust(StatReq wep)
            {
                if (wep.Str > Str) Str = wep.Str;
                if (wep.Dex > Dex) Dex = wep.Dex;
                if (wep.Mag > Mag) Mag = wep.Mag;
                if (wep.Fai > Fai) Fai = wep.Fai;
            }
        }
        private readonly List<string> stats = new List<string> { "Vit", /* vigor */ "Wil", "End", "Str", "Dex", "Mag", "Fai", "Luc", "Durability" /* vit */ };
        private readonly List<string> weaponSlots = new List<string> { "equip_Wep_Right", "equip_Subwep_Right", "equip_Wep_Left", "equip_Subwep_Left" };
        // Items which wouldn't be considered weapons, and optional category
        private static readonly Dictionary<int, EquipCategory> weaponCategories = new Dictionary<int, EquipCategory>
        {
            { 8, EquipCategory.UNSET },  // catalyst/talisman/flame - will be added separately
            { 10, EquipCategory.BOW },
            { 11, EquipCategory.BOW },
            { 13, EquipCategory.ARROW },
            { 14, EquipCategory.BOLT },
        };
        private readonly List<EquipCategory> armorCats = new List<EquipCategory> { EquipCategory.HEAD, EquipCategory.BODY, EquipCategory.ARM, EquipCategory.LEG };
        private readonly List<string> armorTypes = new List<string> { "headEquip", "bodyEquip", "armEquip", "legEquip" };
        private readonly List<string> armorSlots = new List<string> { "equip_Helm", "equip_Armor", "equip_Gaunt", "equip_Leg" };
        private List<EquipCategory> magicTypes = new List<EquipCategory> { EquipCategory.SORCERY, EquipCategory.MIRACLE, EquipCategory.PYROMANCY };

        public void Write(Random random, RandomizerOptions options)
        {
            // Collect game items
            // For armor: headEquip/bodyEquip/armEquip/legEquip booleans. weight float
            // For weapons: weight float.
            // Requirements: correctStrength/correctAgility/corretMagic/corretFaith float.
            // Types: displayTypeId (maps to MenuValueTableParam, in FMGs).
            // enablePyromancy/enablePyromancy/enableMiracle bool? Check attack types other than isBlowAttackType for whether a proper weapon
            // rightHandEquipable/leftHandEquipable bool (bothHandEquipable)?
            // arrowSlotEquipable/boltSlotEquipable bool for bows. bool DisableShoot for greatbow
            // enableGuard for shield
            // Arrows/Bolts: eh a bit tricky. weaponCategory 13/14 for arrow/bolt, and bool DisableShoot for greatbow
            // Spells: in Magic table. requirementIntellect, requirementFaith. ezStateBehaviorType - 0 magic, 2 pyro, 1 miracle
            Dictionary<EquipCategory, List<ItemKey>> items = new Dictionary<EquipCategory, List<ItemKey>>();
            Dictionary<ItemKey, float> weights = new Dictionary<ItemKey, float>();
            Dictionary<ItemKey, StatReq> requirements = new Dictionary<ItemKey, StatReq>();
            HashSet<ItemKey> crossbows = new HashSet<ItemKey>();
            PARAM magics = game.Param("Magic");
            bool twoHand = options["startingtwohand"];
            foreach (ItemKey key in data.Data.Keys)
            {
                if (key.Type == ItemType.WEAPON)
                {
                    PARAM.Row row = game.Item(key);
                    EquipCategory mainCat = EquipCategory.WEAPON;
                    int weaponCategory = (byte)row["weaponCategory"].Value;
                    if (weaponCategories.ContainsKey(weaponCategory))
                    {
                        mainCat = weaponCategories[weaponCategory];
                    }
                    if ((byte)row["enableGuard"].Value == 1)
                    {
                        mainCat = EquipCategory.SHIELD;
                    }
                    if (mainCat == EquipCategory.BOW || mainCat == EquipCategory.ARROW || mainCat == EquipCategory.BOLT)
                    {
                        // Disable greatbow for starting - requirements too far off
                        if ((byte)row["DisableShoot"].Value == 1) continue;
                    }
                    if (mainCat == EquipCategory.BOW) {
                        if ((byte)row["boltSlotEquipable"].Value == 1) crossbows.Add(key);
                    }
                    if (mainCat != EquipCategory.UNSET)
                    {
                        AddMulti(items, mainCat, key);
                    }
                    if ((byte)row["enableMagic"].Value == 1) AddMulti(items, EquipCategory.CATALYST, key);
                    if ((byte)row["enableMiracle"].Value == 1) AddMulti(items, EquipCategory.TALISMAN, key);
                    if ((byte)row["enablePyromancy"].Value == 1) AddMulti(items, EquipCategory.FLAME, key);
                    int str = (byte)row["properStrength"].Value;
                    // Add two hand adjustment for weapons. Note this doesn't work exactly for casting items, but does not affect casting.
                    if (twoHand && (byte)row["Unk14"].Value == 0 && (mainCat == EquipCategory.WEAPON || mainCat == EquipCategory.UNSET))
                    {
                        str = (int)Math.Ceiling(str / 1.5);
                    }
                    requirements[key] = new StatReq
                    {
                        Str = (sbyte)str,
                        Dex = (sbyte)(byte)row["properAgility"].Value,
                        Mag = (sbyte)(byte)row["properMagic"].Value,
                        Fai = (sbyte)(byte)row["properFaith"].Value,
                    };
                    weights[key] = (float)row["weight"].Value;
                }
                else if (key.Type == ItemType.ARMOR)
                {
                    PARAM.Row row = game.Item(key);
                    for (int i = 0; i < 4; i++)
                    {
                        if ((byte)row[armorTypes[i]].Value == 1)
                        {
                            AddMulti(items, armorCats[i], key);
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
                        AddMulti(items, magicTypes[magicCat], key);
                        requirements[key] = new StatReq
                        {
                            Str = 0,
                            Dex = 0,
                            Mag = (sbyte)(byte)magic["requirementIntellect"].Value,
                            Fai = (sbyte)(byte)magic["requirementFaith"].Value,
                            Att = (sbyte)(byte)magic["slotLength"].Value,
                        };
                    }
                }
            }
            // Generate some armor sets. One downside of this approach is that each piece is represented only once - but it is just one shuffle per category, and tends to result in a similar distribution to normal.
            List<List<ItemKey>> weightedArmors = new List<List<ItemKey>>();
            for (int i = 0; i < 4; i++)
            {
                weightedArmors.Add(WeightedShuffle(random, items[armorCats[i]], item => 1/weights[item]));
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
            // Just for testing ;)
            bool cheat = false;
            for (int i = 0; i < 10; i++)
            {
                PARAM.Row row = chara[startId + i];
                // First, always fudge magic to 10, so that Orbeck quest is possible.
                if ((sbyte)row["baseMag"].Value < 10)
                {
                    row["baseMag"].Value = (sbyte)10;
                }
                if (cheat)
                {
                    foreach (string stat in stats) row[$"base{stat}"].Value = (sbyte)90;
                }
                // Then, see stat diffs for weapons/spells/catalysts, and fudge if necessary
                CharacterClass chClass = classes[i];
                int attStat = (sbyte)row["baseWil"].Value;
                StatReq chReqs = new StatReq
                {
                    Str = (sbyte)row["baseStr"].Value,
                    Dex = (sbyte)row["baseDex"].Value,
                    Mag = (sbyte)row["baseMag"].Value,
                    Fai = (sbyte)row["baseFai"].Value,
                    Att = (sbyte)(attStat < 10 ? 0 : attStat < 14 ? 1 : 2),
                };
                StatReq dynamicReqs = chReqs;
                double fudgeFactor = 1.5;
                float weaponWeight = 0f;
                int attSlots = 0;
                bool crossbowSelected = false;
                Console.WriteLine($"Randomizing starting equipment for {chClass.Name}");
                foreach (KeyValuePair<string, EquipCategory> entry in baseStart.Concat(chClass.Start))
                {
                    EquipCategory cat = entry.Value;
                    // TODO: If a catalyst etc also doubles as a weapon, maybe skip its slot.
                    // This crossbow/bow logic relies on iteration order - try to make the order fixed...
                    if ((cat == EquipCategory.ARROW && crossbowSelected) || (cat == EquipCategory.BOLT && !crossbowSelected)) continue;
                    Dictionary<ItemKey, int> statDiffs = items[entry.Value].ToDictionary(item => item, item => requirements[item].Eligible(dynamicReqs));
                    List<ItemKey> candidates = items[entry.Value];
                    if (cat == EquipCategory.SHIELD || chClass.Name == "Deprived")
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
                    items[entry.Value].Remove(selected);
                    if (statDiffs[selected] < 0)
                    {
                        dynamicReqs.Adjust(requirements[selected]);
                        fudgeFactor *= -statDiffs[selected];
                    }
                    row[entry.Key].Value = selected.ID;
                    if (weights.ContainsKey(selected))
                    {
                        weaponWeight += weights[selected];
                    }
                    attSlots = requirements[selected].Att;
                    Console.WriteLine($"  {entry.Key} is now {game.Name(selected)}, meets requirements by {statDiffs[selected]}");
                }
                int statChange = dynamicReqs.Eligible(chReqs);
                if (statChange < 0)
                {
                    row["baseStr"].Value = dynamicReqs.Str;
                    row["baseDex"].Value = dynamicReqs.Dex;
                    row["baseMag"].Value = dynamicReqs.Mag;
                    row["baseFai"].Value = dynamicReqs.Fai;
                    row["soulLvl"].Value = (short)((short)row["soulLvl"].Value - statChange);
                }
                // Armor time
                float totalWeight = 40 + (sbyte)row["baseDurability"].Value;
                List<ArmorSet> availableSets = armors.TakeWhile(armor => armor.Weight + weaponWeight < totalWeight * 0.69f).ToList();
                if (availableSets.Count == 0) availableSets = new List<ArmorSet> { armors[0] };
                ArmorSet selectedArmor = Choice(random, availableSets);
                armors.Remove(selectedArmor);
                Console.WriteLine($"  Armor: {string.Join(", ", selectedArmor.Ids.Select(id => game.Name(new ItemKey(ItemType.ARMOR, id))))}");
                Console.WriteLine($"  Weight: weapons {weaponWeight:0.##} + armor {selectedArmor.Weight:0.##} / {totalWeight:0.##} = {100*(weaponWeight+selectedArmor.Weight)/totalWeight:0.##}%");
                for (int j = 0; j < 4; j++)
                {
                    if ((int)row[armorSlots[j]].Value != -1)
                    {
                        row[armorSlots[j]].Value = selectedArmor.Ids[j];
                    }
                }

                if (cheat)
                {
                    PARAM reinforce = game.Param("ReinforceParamWeapon");
                    HashSet<int> reinforceLevels = new HashSet<int>(reinforce.Rows.Select(r => (int)r.ID));
                    foreach (string wep in weaponSlots)
                    {
                        int id = (int)row[wep].Value;
                        if (id != -1)
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
            // Just remove Symbol of Avarice first (may not matter with Irregulator, but those are more chaotic anyway)
            armors.RemoveAll(set => set.Ids[0] == 78500000);
            Dictionary<string, ArmorSet> npcArmors = new Dictionary<string, ArmorSet>();
            Func<ItemType, PARAM.Cell, float> cellWeight = (type, cell) =>
            {
                int id = (int)cell.Value;
                if (id == -1) return 0;
                ItemKey key = new ItemKey(type, id);
                if (!weights.ContainsKey(key)) return 0;
                return weights[key];
            };
            foreach (PARAM.Row row in chara.Rows.Where(r => r.ID > startId + 10))
            {
                string name = game.CharacterName((int)row.ID);
                if (name == "?CHARACTER?") continue;
                ArmorSet selectedArmor;
                if (!npcArmors.ContainsKey(name))
                {
                    float weaponWeight = weaponSlots.Select(slot => cellWeight(ItemType.WEAPON, row[slot])).Sum();
                    float armorWeight = armorSlots.Select(slot => cellWeight(ItemType.ARMOR, row[slot])).Sum();
                    float weightLimit = weaponWeight + armorWeight;
                    float totalWeight = 40 + (sbyte)row["baseDurability"].Value;
                    int armorLimit = armors.FindIndex(armor => armor.Weight + weaponWeight > weightLimit);
                    if (armorLimit == -1)
                    {
                        armorLimit = armors.Count - 1;
                    }
                    armorLimit = Math.Min(20, armorLimit);
                    selectedArmor = npcArmors[name] = armors[random.Next(armorLimit)];
                    armors.Remove(selectedArmor);
                    Console.WriteLine($"Armor for {name}: {100 * weightLimit / totalWeight:0.##}% -> {100 * (selectedArmor.Weight + weaponWeight) / totalWeight:0.##}%: {string.Join(", ", selectedArmor.Ids.Select(id => game.Name(new ItemKey(ItemType.ARMOR, id))))}");
                }
                selectedArmor = npcArmors[name];
                for (int j = 0; j < 4; j++)
                {
                    if ((int)row[armorSlots[j]].Value != -1)
                    {
                        row[armorSlots[j]].Value = selectedArmor.Ids[j];
                    }
                }
            }
        }
    }
}