using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using static RandomizerCommon.LocationData;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class DS3ItemEditor : ItemEditor
    {
        private readonly GameData game;
        private readonly AnnotationData ann;
        private readonly Events events;

        public DS3ItemEditor(GameData game, AnnotationData ann, Events events)
        {
            this.game = game;
            this.ann = ann;
            this.events = events;
        }

        public override void EditLocations(RandomizerOptions opt, ItemFlagMapping mapping)
        {
            if (mapping.GestureFlag <= 0 && !ann.NorandomItems.Contains(mapping.GestureItem))
            {
                throw new Exception("Internal error: Path of the dragon not assigned to any location, but key items are randomized");
            }

            // Disable Firelink Shrine bonfire without Coiled Sword, with special event flag
            game.Params["ActionButtonParam"][9351]["grayoutFlag"].Value = 14005108;

            // Description for path of the dragon so it's not ?GoodsInfo?
            FMGX goodsShort = game.ItemFMGs["アイテム説明"];
            FMGX goodsLong = game.ItemFMGs["アイテムうんちく"];
            string dragonInfo = "A gesture of meditation channeling the eternal essence of the ancient dragons";
            goodsShort[9030] = dragonInfo;
            goodsLong[9030] = $"{dragonInfo}.\n\nThe path to ascendence can be achieved only by the most resolute of seekers. Proper utilization of this technique can grant deep inner focus.";
            // Make it appear as a key item in shops
            game.Params["EquipParamGoods"][9030]["goodsType"].Value = (byte)1;

            // Replace transpose qwc flags with soul get flags
            foreach (PARAM.Row row in game.Params["ShopLineupParam"].Rows)
            {
                if (mapping.BossShopItems.TryGetValue(row.ID, out ItemKey soul) && mapping.ItemEventFlags.TryGetValue(soul, out int soulFlag) && soulFlag > 0)
                {
                    row["eventFlag_forRelease"].Value = soulFlag;
                }
            }

            Dictionary<string, EMEVD> emevds = game.Emevds;

            // Do this all manually for the moment, rather than from config
            // Can revisit this later if it needs to scale up
            List<string> toEdit = new List<string> { "common", "common_func", "m30_00_00_00", "m30_01_00_00", "m31_00_00_00", "m40_00_00_00" };
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                if (!toEdit.Contains(entry.Key)) continue;
                string map = entry.Key;
                EMEVD emevd = entry.Value;
                foreach (EMEVD.Event ev in emevd.Events)
                {
                    EventEdits edits = null;
                    if (ev.ID == 0 && map == "m30_00_00_00" && mapping.GestureFlag > 0)
                    {
                        // Remove visual sfx for Path of the Dragon pickup
                        ev.Instructions.RemoveAll(i =>
                        {
                            Instr instr = events.Parse(i);
                            return instr.Init && instr.Callee == 13000901;
                        });
                    }
                    else if (ev.ID == 0 && map == "m30_01_00_00")
                    {
                        // Fix Lothric Castle Crystal Lizard so one doesn't despawn when the other one gets killed (use new event flag)
                        foreach (EMEVD.Instruction i in ev.Instructions)
                        {
                            Instr instr = events.Parse(i);
                            if (instr.Init && instr.Callee == 20005341 && (int)instr[instr.Offset + 1] == 3010311)
                            {
                                instr[instr.Offset] = 13010594;
                                instr.Save();
                            }
                        }
                    }
                    else if (ev.ID == 0 && map == "m31_00_00_00")
                    {
                        // Remove Undead Settlement birch tree, which is a duplicate pickup which makes one unavailable
                        ev.Instructions.RemoveAll(i =>
                        {
                            Instr instr = events.Parse(i);
                            return instr.Init && instr.Callee == 20005525 && (int)instr[instr.Offset] == 53100660;
                        });
                    }
                    else if (ev.ID == 710)
                    {
                        // Grand Archives Key softlock fix
                        edits = new EventEdits();
                        events.RemoveMacro(edits, "EndIfConditionGroupStateUncompiled(EventEndType.End, PASS, AND_15)");
                    }
                    else if (ev.ID == 20005523)
                    {
                        if (opt["ngplusrings"])
                        {
                            edits = new EventEdits();
                            events.AddMacro(edits, EditType.AddBefore, "GOTO Unconditionally (0)");
                        }
                    }
                    else if (ev.ID == 13905870)
                    {
                        // Prevent Storm Ruler infinite shiny from appearing, since it's randomized elsewhere
                        edits = new EventEdits();
                        events.AddMacro(edits, EditType.AddAfter, "EndUnconditionally(EventEndType.End)", "SetObjectTreasureState");
                    }
                    if (edits != null)
                    {
                        events.ApplyAllEdits(ev, edits);
                        if (edits.PendingEdits.Count != 0)
                        {
                            throw new Exception($"{ev.ID} has unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                        }
                    }
                }
                // These should probably be in a config, although some of them would need to take args
                void addNewEvent(int id, IEnumerable<string> instrs, EMEVD.Event.RestBehaviorType rest = EMEVD.Event.RestBehaviorType.Default)
                {
                    EMEVD.Event ev = new EMEVD.Event(id, rest);
                    ev.Instructions.AddRange(instrs.Select(t => events.ParseAdd(t)));
                    emevd.Events.Add(ev);
                    emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)id, (uint)0 }));
                }
                if (map == "common")
                {
                    // Hacky Greirat Lothric Castle softlock fix
                    // If you don't have Grand Archives key yet, mark him as having talked about looting Lothric
                    // (74000308) so the actual looting flag (74000309) isn't touched by ESD.
                    addNewEvent(13000905, new string[]
                    {
                        "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventIDSlotNumber, 0)",
                        "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 74000309)",
                        "SetEventFlag(74000308, ON)",
                        "IfPlayerHasdoesntHaveItem(MAIN, ItemType.Goods, 2014, OwnershipState.Owns)",
                        "SetEventFlag(74000308, OFF)",
                    });
                    // Make Firelink Shrine greyed out by default, without having the Coiled Sword, in combination with param change above
                    // This doesn't always work just on its own, so there is a backup edit above.
                    addNewEvent(14005107, new string[]
                    {
                        "Set Event Flag (14005108,1)",
                        "IF Player Has/Doesn't Have Item (0,3,2137,1)",
                        "Set Event Flag (14005108,0)",
                    },
                    EMEVD.Event.RestBehaviorType.Restart);
                    if (mapping.GestureFlag > 0)
                    {
                        // Do the Path of the Dragon swap
                        // We can't just use the item all of the time, since it would appear as a double drop.
                        addNewEvent(13000904, new string[]
                        {
                            "END IF Event Flag (0,1,0,6079)",
                            $"IF Event Flag (0,1,0,{mapping.GestureFlag})",
                            "Remove Item From Player (3,9030,1)",
                            "Award Gesture Item (29,3,9030)",
                            "Set Event Flag (6079,1)",
                        });
                    }
                }
            }
        }
    }
}
