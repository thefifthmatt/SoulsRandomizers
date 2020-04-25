using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SoulsFormats;
using SoulsIds;
using static SoulsIds.Events;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class SkillSplitter
    {
        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        private Events events;

        public SkillSplitter(GameData game, LocationData data, AnnotationData ann, Events events)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.events = events;
        }

        public Assignment SplitAll()
        {
            // Add skills as item drops in the world. Esoteric texts still work, but they are removed from randomizer pool.
            Assignment ret = new Assignment();

            // First, create skill items, including editing item fmgs
            SortedDictionary<int, ItemKey> newSkills = new SortedDictionary<int, ItemKey>();
            SortedDictionary<int, ItemKey> oldSkills = new SortedDictionary<int, ItemKey>();

            Dictionary<int, ItemKey> texts = new Dictionary<int, ItemKey>
            {
                [0] = game.ItemForName("Shinobi Esoteric Text"),
                [1] = game.ItemForName("Prosthetic Esoteric Text"),
                [2] = game.ItemForName("Ashina Esoteric Text"),
                [3] = game.ItemForName("Senpou Esoteric Text"),
                [4] = game.ItemForName("Mushin Esoteric Text"),
            };

            // Note: there are some events in common which can be used to detect skills which are already granted by emevd.
            // But they all have a text of -1 so it's unnecessary to scan emevd for this.
            // For example:
            // Initialize Event (Event Slot ID: 4, Event ID: 450, Parameters: 6719){, 3, 2450, 620}
            // Initialize Event (Event Slot ID: 0, Event ID: 460, Parameters: 6710){, 2470, 600}

            PARAM.Row baseGood = game.Params["EquipParamGoods"][2470];
            int baseId = 6405;

            FMG itemName = game.ItemFMGs["アイテム名"];
            FMG itemDesc = game.ItemFMGs["アイテム説明"];
            FMG weaponName = game.ItemFMGs["武器名"];
            FMG weaponDesc = game.ItemFMGs["武器説明"];
            SortedDictionary<ItemKey, string> gameNames = game.Names();

            bool explain = false;
            HashSet<int> copiedWeapons = new HashSet<int>();
            foreach (PARAM.Row r in game.Params["SkillParam"].Rows)
            {
                int skillId = (int)r.ID;
                if (skillId >= 700) continue;

                int text = (byte)r["Unk7"].Value;
                if (!texts.ContainsKey(text)) continue;

                int descItem = (int)r["SkilLDescriptionId"].Value;
                if (copiedWeapons.Contains(descItem)) continue;
                copiedWeapons.Add(descItem);

                PARAM.Row weaponRow = game.Params["EquipParamWeapon"][descItem];
                int sortId = (int)weaponRow["sortId"].Value;
                short iconId = (short)weaponRow["iconId"].Value;

                int good = baseId++;
                PARAM.Row newGood = game.AddRow("EquipParamGoods", good);
                GameEditor.CopyRow(baseGood, newGood);
                ItemKey goodKey = new ItemKey(ItemType.GOOD, good);
                newSkills[skillId] = goodKey;

                gameNames[goodKey] = weaponName[descItem];
                itemName[good] = weaponName[descItem];
                itemDesc[good] = weaponDesc[descItem];

                newGood["sortId"].Value = sortId;
                newGood["iconId"].Value = iconId;
                // These should be set in base row, but do this just in case
                // Don't show up in inventory
                newGood["goodsType"].Value = (byte)7;
                // But pop up on acquisition
                newGood["Unk20"].Value = (byte)6;

                if (explain) Console.WriteLine($"-- {r.ID} -> {good}: {descItem}, {weaponName[descItem]}");
                ret.Assign[new ItemKey(ItemType.WEAPON, descItem)] = goodKey;
            }
            game.Params["EquipParamGoods"].Rows.Sort((a, b) => a.ID.CompareTo(b.ID));

            // Second, add event scripting to grant skills, with new common_func
            EMEVD common = game.Emevds["common"];
            int grantId = 11615600;
            EMEVD.Event grantEv = new EMEVD.Event(grantId);
            List<string> grantCmds = new List<string> { "IF Player Has/Doesn't Have Item (0,3,X0_4,1)", "Grant Skill (X4_4)" };
            for (int i = 0; i < grantCmds.Count; i++)
            {
                (EMEVD.Instruction ins, List<EMEVD.Parameter> ps) = events.ParseAddArg(grantCmds[i], i);
                grantEv.Instructions.Add(ins);
                grantEv.Parameters.AddRange(ps);
            }
            common.Events.Add(grantEv);

            int slot = 0;
            foreach (KeyValuePair<int, ItemKey> entry in newSkills)
            {
                common.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { slot++, grantId, entry.Value.ID, entry.Key }));
            }

            // Third, edit drops
            // Remove text drops
            ann.ItemGroups["remove"].AddRange(texts.Values);

            // Add skill drops
            foreach (ItemKey item in newSkills.Values)
            {
                data.AddLocationlessItem(item);
            }

            // Copy restrictions from the weapons over to the goods
            ann.CopyRestrictions(ret.Assign);
            foreach (KeyValuePair<ItemKey, ItemKey> entry in ret.Assign)
            {
                ItemKey weapon = entry.Key;
                ItemKey good = entry.Value;
                // Mikiri Counter in hint log
                if (weapon.ID == 200300)
                {
                    ann.ItemGroups["upgradehints"].Add(good);
                }
                // Carp scalesmen
                if (!ann.ExcludeTags.ContainsKey(weapon))
                {
                    ann.ItemGroups["premium"].Add(good);
                }
            }

            // For balancing Dancing Dragon Mask, greatly reduce enemy xp drops
            // All NG bosses together give 93k XP. This gives enough for 45 skill points.
            // So allow 15 levels/3 AP upgrades, or 9 XP. (Next threshhold: 4 AP upgrades, 13k XP)
            // (Or not, since only got 9 levels in a full run, just double it.)
            foreach (PARAM.Row row in game.Params["NpcParam"].Rows)
            {
                row["Experience"].Value = (int)row["Experience"].Value / 5;
            }
            foreach (PARAM.Row row in game.Params["GameAreaParam"].Rows)
            {
                row["BonusExperience"].Value = (int)row["BonusExperience"].Value / 5;
            }

            // Also in this mode, acquire skills option is removed from Sculptor's Idols, in case it has been there from previous runs. Done in PermutationWriter.

            return ret;
        }

        public class Assignment
        {
            public Dictionary<ItemKey, ItemKey> Assign = new Dictionary<ItemKey, ItemKey>();
        }
    }
}
