using System;
using System.Collections.Generic;
using System.Linq;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class SkillWriter
    {
        private GameData game;
        private LocationData data;
        private AnnotationData ann;

        public SkillWriter(GameData game, LocationData data, AnnotationData ann)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
        }

        public void RandomizeTrees(Random random, Permutation permutation, SkillSplitter.Assignment split)
        {
            // >= 700: prosthetics
            // < 400: skills before mushin
            GameEditor editor = game.Editor;
            PARAM param = game.Params["SkillParam"];

            // Orderings for skills which completely supersede each other. (For prosthetics, just use their natural id ordering)
            Dictionary<int, int> skillOrderings = new Dictionary<int, int>
            {
                [110] = 111,  // Nightjar slash
                [210] = 211,  // Ichimonji
                [310] = 311,  // Praying Strikes
            };
            Dictionary<int, ItemKey> texts = new Dictionary<int, ItemKey>
            {
                [0] = game.ItemForName("Shinobi Esoteric Text"),
                [1] = game.ItemForName("Prosthetic Esoteric Text"),
                [2] = game.ItemForName("Ashina Esoteric Text"),
                [3] = game.ItemForName("Senpou Esoteric Text"),
                // [4] = game.ItemForName("Mushin Esoteric Text"),
            };
            SortedDictionary<ItemKey, string> names = game.Names();
            string descName(int desc)
            {
                return names[new ItemKey(ItemType.WEAPON, desc)];
            }
            Dictionary<int, SkillData> allData = new Dictionary<int, SkillData>();
            Dictionary<int, SkillSlot> allSlots = new Dictionary<int, SkillSlot>();
            Dictionary<ItemKey, SkillData> skillItems = new Dictionary<ItemKey, SkillData>();
            List<SkillData> skills = new List<SkillData>();
            List<SkillSlot> skillSlots = new List<SkillSlot>();
            List<SkillData> prosthetics = new List<SkillData>();
            List<SkillSlot> prostheticSlots = new List<SkillSlot>();

            bool explain = false;
            foreach (PARAM.Row r in param.Rows)
            {
                SkillData data = new SkillData
                {
                    ID = (int)r.ID,
                    Item = (int)r["SkilLDescriptionId"].Value,
                    Equip = (int)r["Unk1"].Value,
                    Flag = (int)r["EventFlagId"].Value,
                    Placeholder = (int)r["Unk5"].Value,
                    SpEffects = new[] { (int)r["Unk2"].Value, (int)r["Unk3"].Value },
                    EmblemChange = (byte)r["Unk10"].Value != 0,
                };
                data.Key = new ItemKey(ItemType.WEAPON, data.Item);
                skillItems[data.Key] = data;
                SkillSlot slot = new SkillSlot
                {
                    ID = (int)r.ID,
                    Col = (short)r["MenuDisplayPositionIndexXZ"].Value,
                    Row = (short)r["MenuDisplayPositionIndexY"].Value,
                    Text = data.ID < 400 && texts.TryGetValue((byte)r["Unk7"].Value, out ItemKey text) ? text : null,
                };
                if (explain) Console.WriteLine($"{r.ID}: {data.Item}, {data.Equip}, {descName(data.Item)}");
                if (data.ID < 400)
                {
                    skills.Add(data);
                    skillSlots.Add(slot);
                }
                else if (data.ID >= 700)
                {
                    prosthetics.Add(data);
                    prostheticSlots.Add(slot);
                }
                allData[data.ID] = data;
                allSlots[slot.ID] = slot;
            }
            void applyData(PARAM.Row r, SkillData data)
            {
                r["SkilLDescriptionId"].Value = data.Item;
                r["EventFlagId"].Value = data.Flag;
                r["Unk1"].Value = data.Equip;
                r["Unk2"].Value = data.SpEffects[0];
                r["Unk3"].Value = data.SpEffects[0];
                r["Unk5"].Value = data.Placeholder;
                r["Unk10"].Value = (byte)(data.EmblemChange ? 1 : 0);
            }
            Shuffle(random, skills);
            Shuffle(random, skillSlots);
            Shuffle(random, prosthetics);
            Shuffle(random, prostheticSlots);

            // Skills rando
            if (split == null)
            {
                Dictionary<ItemKey, string> textWeight = new Dictionary<ItemKey, string>();
                Dictionary<ItemKey, string> textLocations = texts.Values.ToDictionary(t => t, t => {
                    SlotKey target = permutation.GetFiniteTargetKey(t);
                    textWeight[t] = permutation.GetLogOrder(target);
                    SlotAnnotation sn = ann.Slot(data.Location(target).LocScope);
                    if (explain) Console.WriteLine($"{game.Name(t)} in {sn.Area} - {sn.Text}. Lateness {(permutation.ItemLateness.TryGetValue(t, out double val) ? val : -1)}");
                    return sn.Area;
                });
                List<ItemKey> textOrder = texts.Values.OrderBy(t => textWeight[t]).ToList();
                Dictionary<ItemKey, List<ItemKey>> allowedSkillLocations = new Dictionary<ItemKey, List<ItemKey>>();
                foreach (PlacementRestrictionAnnotation restrict in ann.ItemRestrict.Values)
                {
                    if (!(restrict.Key.Type == ItemType.WEAPON && (restrict.Key.ID >= 200000 || restrict.Key.ID < 690000))) continue;
                    if (restrict.Unique != null)
                    {
                        PlacementSlotAnnotation restrictSlot = restrict.Unique[0];
                        HashSet<string> allowedLocs = restrictSlot.AllowedAreas(permutation.IncludedAreas);
                        allowedSkillLocations[restrict.Key] = textOrder.Where(t => allowedLocs.Contains(textLocations[t])).ToList();
                    }
                }
                // Mapping from source to target
                Dictionary<int, int> skillMapping = new Dictionary<int, int>();
                foreach (SkillData data in skills.OrderBy(d => allowedSkillLocations.TryGetValue(d.Key, out List<ItemKey> itemTexts) ? itemTexts.Count : 99))
                {
                    SkillSlot select = null;
                    // TODO: Have different requirements for smaller texts?
                    if (allowedSkillLocations.TryGetValue(data.Key, out List<ItemKey> itemTexts))
                    {
                        select = skillSlots.Find(sl =>
                        {
                            int textIndex = itemTexts.IndexOf(sl.Text);
                            if (textIndex == -1) return false;
                            // If the skill is in the most recent two texts, it should be in the first two columns.
                            if (textIndex >= itemTexts.Count - 2) return sl.Col <= 1;
                            // Otherwise, just not the secret technique slot.
                            return sl.Col <= 2;
                        });
                    }
                    SkillSlot slot = select ?? skillSlots.First();
                    skillSlots.Remove(slot);
                    skillMapping[data.ID] = slot.ID;
                }
                foreach (KeyValuePair<int, int> order in skillOrderings)
                {
                    SkillSlot first = allSlots[skillMapping[order.Key]];
                    SkillSlot second = allSlots[skillMapping[order.Value]];
                    if (first.Text != null && first.Text.Equals(second.Text) && first.Col > second.Col)
                    {
                        skillMapping[order.Key] = second.ID;
                        skillMapping[order.Value] = first.ID;
                    }
                }
                Dictionary<int, List<int>> colCosts = new Dictionary<int, List<int>>();
                foreach (int skill in skillMapping.Keys)
                {
                    SkillData data = allData[skill];
                    SkillSlot slot = allSlots[skill];
                    PARAM.Row item = game.Item(data.Key);
                    PARAM.Row mat = game.Params["EquipMtrlSetParam"][(int)item["materialSetId"].Value];
                    if (mat != null && (int)mat["MaterialId01"].Value == 1200)
                    {
                        AddMulti(colCosts, slot.Col, (sbyte)mat["ItemNum01"].Value);
                    }
                }
                foreach (KeyValuePair<int, int> transfer in skillMapping.OrderBy(k => k.Key))
                {
                    SkillData data = allData[transfer.Key];
                    SkillSlot slot = allSlots[transfer.Value];
                    applyData(param[transfer.Value], data);
                    // Also randomize skill points while we are here
                    PARAM.Row item = game.Item(data.Key);
                    PARAM.Row mat = game.Params["EquipMtrlSetParam"][(int)item["materialSetId"].Value];
                    if (mat != null && (int)mat["MaterialId01"].Value == 1200)
                    {
                        int cost = (sbyte)mat["ItemNum01"].Value;
                        int newCost = Choice(random, colCosts.TryGetValue(slot.Col, out List<int> costs) ? costs : new List<int> { 1, 2, 3, 4, 5 });
                        newCost = Math.Min(Math.Max(cost - 2, newCost), cost + 2);
                        mat["ItemNum01"].Value = (sbyte)newCost;
                    }
                }

                Console.WriteLine("-- Skills placements");
                for (int i = 0; i <= 3; i++)
                {
                    ItemKey text = texts[i];
                    // Can also order by (allSlots[t.Value].Col, allSlots[t.Value].Row), but just do alphabetical instead
                    List<int> textSkills = skillMapping.Where(t => text.Equals(allSlots[t.Value].Text)).Select(t => t.Key).ToList();
                    Console.WriteLine($"Skills in {game.Name(text)}: {string.Join(", ", textSkills.Select(t => descName(allData[t].Item)).OrderBy(t => t))}");
                    foreach (int skill in textSkills)
                    {
                        permutation.SkillAssignment[allData[skill].Key] = text;
                    }
                }
                Console.WriteLine();
            }
            else
            {
                foreach (KeyValuePair<ItemKey, ItemKey> transfer in split.Assign)
                {
                    permutation.SkillAssignment[transfer.Key] = transfer.Value;
                }
            }

            // Prosthetics rando
            Dictionary<ItemKey, List<ItemKey>> upgrades = new Dictionary<ItemKey, List<ItemKey>>();
            Dictionary<ItemKey, ItemKey> specialMats = new Dictionary<ItemKey, ItemKey>();
            Dictionary<ItemKey, int> allMats = new Dictionary<ItemKey, int>();
            List<ItemKey> matOrder = new List<ItemKey>();
            for (int i = 0; i < 10; i++)
            {
                ItemKey baseMat = new ItemKey(ItemType.GOOD, 9700 + i * 10);
                matOrder.Add(baseMat);
                int baseWep = 700000 + i * 10000;
                for (int j = 1; j < 10; j++)
                {
                    ItemKey wep = new ItemKey(ItemType.WEAPON, baseWep + j * 1000);
                    if (names.ContainsKey(wep))
                    {
                        AddMulti(upgrades, baseMat, wep);
                        PARAM.Row item = game.Item(wep);
                        int matId = (int)item["materialSetId"].Value;
                        allMats[wep] = matId;
                        PARAM.Row mat = game.Params["EquipMtrlSetParam"][matId];
                        if (mat != null)
                        {
                            // All instances of special materials in the game are in spot 1
                            int extraMat = (int)mat["MaterialId01"].Value;
                            if (extraMat >= 9700 && extraMat < 9800)
                            {
                                specialMats[wep] = new ItemKey(ItemType.GOOD, extraMat);
                                mat["MaterialId01"].Value = -1;
                                mat["ItemNum01"].Value = (sbyte)-1;
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            matOrder = matOrder.Select(mat => (permutation.GetLogOrder(permutation.GetFiniteTargetKey(mat)), mat)).OrderBy(e => e.Item1).Select(e => e.Item2).ToList();
            List<SkillSlot> slotOrder = prostheticSlots.OrderBy(sl => (sl.Col, sl.Row)).ToList();
            List<int> initialSlots = new List<int> { 0, 1, 2, 3, 4, 5, 9, 10, 12, 13 };
            // Mapping source to target
            Dictionary<int, int> prostheticMapping = new Dictionary<int, int>();
            // First, add in first versions of all prosthetics in their vanilla spots
            for (int i = 0; i < 10; i++)
            {
                ItemKey firstUpgrade = upgrades[matOrder[i]][0];
                SkillData data = skillItems[firstUpgrade];
                SkillSlot slot = slotOrder[initialSlots[i]];
                prostheticMapping[data.ID] = slot.ID;
                prosthetics.Remove(data);
                prostheticSlots.Remove(slot);
            }
            // Add all of the rest. The only restriction is not adding skills requiring special materials too early.
            foreach (SkillData data in prosthetics)
            {
                SkillSlot select = null;
                if (specialMats.ContainsKey(data.Key))
                {
                    select = prostheticSlots.Find(sl => sl.Col >= 3);
                }
                SkillSlot slot = select ?? prostheticSlots.First();
                prostheticSlots.Remove(slot);
                prostheticMapping[data.ID] = slot.ID;
            }
            // Order all prosthetics by usual upgrade order
            foreach (List<ItemKey> keys in upgrades.Values)
            {
                List<SkillData> keyIds = keys.Select(k => skillItems[k]).ToList();
                List<SkillSlot> byCol = keyIds.Select(d => allSlots[prostheticMapping[d.ID]]).OrderBy(slot => slot.Col).ToList();
                for (int i = 0; i < keyIds.Count; i++)
                {
                    prostheticMapping[keyIds[i].ID] = byCol[i].ID;
                }
            }
            foreach (KeyValuePair<int, int> transfer in prostheticMapping)
            {
                SkillData data = allData[transfer.Key];
                applyData(param[transfer.Value], data);
                // Swap materials to the target's, including adding back special mat if any
                PARAM.Row item = game.Item(data.Key);
                int targetMat = allMats[allData[transfer.Value].Key];
                item["materialSetId"].Value = targetMat;
                PARAM.Row mat = game.Params["EquipMtrlSetParam"][targetMat];
                if (mat != null && specialMats.TryGetValue(data.Key, out ItemKey specialMat))
                {
                    int leastMat = Enumerable.Range(1, 5).OrderBy(i => (int)mat[$"MaterialId0{i}"].Value).Min();
                    mat[$"MaterialId0{leastMat}"].Value = specialMat.ID;
                    mat[$"ItemNum0{leastMat}"].Value = (sbyte)1;
                }
            }

            // Log the infos
            Console.WriteLine("-- Prosthetics placements");
            Dictionary<int, int> revMap = prostheticMapping.ToDictionary(e => e.Value, e => e.Key);
            foreach (SkillSlot slot in slotOrder)
            {
                SkillData data = allData[revMap[slot.ID]];
                Console.WriteLine($"Prosthetic upgrade tier {slot.Col + 1}: {descName(data.Item)}");
            }
            Console.WriteLine();
        }
        public class SkillData
        {
            public int ID { get; set; }
            public int Item { get; set; }
            public int Equip { get; set; }
            public int Flag { get; set; }
            public int Placeholder { get; set; }
            public int[] SpEffects { get; set; }
            public bool EmblemChange { get; set; }
            public ItemKey Key { get; set; }
        }
        public class SkillSlot
        {
            public int ID { get; set; }
            public int Row { get; set; }
            public int Col { get; set; }
            public ItemKey Text { get; set; }
        }
    }
}
