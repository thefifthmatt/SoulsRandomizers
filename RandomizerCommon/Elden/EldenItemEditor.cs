using LightInject;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.ItemEventConfig;
using static RandomizerCommon.ItemLocEditor;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.Location;
using static RandomizerCommon.Messages;
using static SoulsFormats.EMEVD.Instruction;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class EldenItemEditor : ItemEditor
    {
        private readonly GameData game;
        private readonly LocationData data;
        private readonly AnnotationData ann;
        private readonly ItemLocEditor locEditor;
        private readonly Events events;
        private readonly ItemEventConfig eventConfig;
        private readonly Messages messages;

        public EldenItemEditor(
            GameData game,
            LocationData data,
            AnnotationData ann,
            ItemLocEditor locEditor,
            Events events,
            ItemEventConfig eventConfig,
            Messages messages)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.locEditor = locEditor;
            this.events = events;
            this.eventConfig = eventConfig;
            this.messages = messages;
        }

        [Localize]
        private static readonly Text receiveBellBearing = new Text("Receive Bell Bearing", "GameMenu_receiveBellBearing");
        [Localize]
        private static readonly Text messmerShard = new Text("Messmer's Kindling Shard", "GameMenu_messmerShard");
        [Localize]
        private static readonly Text messmerShardDesc = new Text("Burns the sealing tree, with {0} required", "GameMenu_messmerShardDesc");
        [Localize]
        private static readonly Text shardDesc = new Text("{0}, with {1} required", "GameMenu_shardDesc");
        [Localize]
        private static readonly Text oMotherDesc = new Text("Use to entreat Marika", "GameMenu_oMotherDesc");
        [Localize]
        private static readonly Text thopsKeyText = new Text("{0} (Thops)", "GameMenu_thopsKey");
        [Localize]
        private static readonly Text ashenGraceText = new Text("{0} (Ashen)", "GameMenu_ashenGrace");

        public override void EditLocations(RandomizerOptions opt, ItemFlagMapping mapping)
        {
            // Gesture key item
            if (opt["dlc"] && mapping.GestureFlag <= 0 && !ann.NorandomItems.Contains(mapping.GestureItem))
            {
                throw new Exception("Error: O Mother was not assigned to any location, but key items are randomized");
            }
            // Should this use GestureItem?
            messages.SetFMGEntry(game, FMGCategory.Item, "GoodsInfo", 2009004, IdentityMessage, messages.Get(oMotherDesc));
            messages.SetFMGEntry(game, FMGCategory.Item, "GoodsCaption", 2009004, IdentityMessage, FMGArg.Menu("BloodMsg", 3050, "Lore"));
            PARAM.Row fakeGesture = game.Params["EquipParamGoods"][2009004];
            // Make a copy of fake item so the gesture drop itself is unmodified
            PARAM.Row showGesture = GameEditor.AddRow(game.Params["EquipParamGoods"], 2019004, fakeGesture);
            showGesture["showDialogCondType"].Value = (byte)0; // 0 or 2
            messages.SetFMGEntry(game, FMGCategory.Item, "GoodsName", 2019004, IdentityMessage, FMGArg.Item("GoodsName", 2009004, "O Mother"));
            game.Params["GestureParam"][115]["itemId"].Value = 2019004;
            // Make it appear as a key item in shops
            fakeGesture["goodsType"].Value = (byte)1;
            fakeGesture["rarity"].Value = (byte)2;
            // Prevent popups from the fake item, unless shard
            if (!ann.MultiItems.ContainsKey(mapping.GestureItem))
            {
                fakeGesture["showLogCondType"].Value = (byte)0; // 0 or 1
                fakeGesture["showDialogCondType"].Value = (byte)0; // 0 or 2
            }

            // Synthetic Rold lot. The location flag is still 40001, but the item's flag is changed.
            // This is deprecated now so remove all of that stuff after more testing.
            int roldFlag = GameData.EldenRingBase + 11;
            int roldEventId = GameData.EldenRingBase + 10;
            if (opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out _))
            {
                ItemKey rold = ann.ItemGroups["removerold"][0];
                if (!(mapping.ItemEventFlags.TryGetValue(rold, out int flag) && flag > 0))
                {
                    // Rold being used as an item is mainly for Gideon check
                    // mapping.ItemEventFlags[rold] = roldFlag;
                }
            }
            int shardFlag = GameData.EldenRingBase + 2050;

            // Get current way of acquiring item corresponding to given flag.
            // This does not handle the full conditions for 'using' a key item if those have been modified.
            bool getFlagAcquire(int flag, string debugId, out ItemKey item, out int targetFlag)
            {
                if (mapping.TrackedFlagItems.TryGetValue(flag, out item))
                {
                    if (item == null)
                    {
                        throw new Exception($"Stale flag {flag} in {debugId}: item not found to be accessible");
                    }
                    // If an item isn't randomized or randomization doesn't change flags, it should be fine to leave the script as-is
                    if (mapping.ItemEventFlags.TryGetValue(item, out targetFlag))
                    {
                        return true;
                    }
                }
                targetFlag = 0;
                return false;
            }

            // Custom logic gates
            // This approach can be carefully extended to different games by splitting out data structures from actual game-editing
            Dictionary<string, LogicGate> gates = eventConfig.LogicGates == null ? new() : eventConfig.LogicGates.ToDictionary(e => e.Name, e => e);
            Dictionary<string, int> logicFlagIndices = eventConfig.LogicFlags == null ? new() : eventConfig.LogicFlags.ToDictionary(e => e.Name, e => e.Index);

            HashSet<string> processedGates = new();

            // This approach does not treat items as special at all if they're not currently configured as multi-items (for a full list, check multikey group)
            Dictionary<string, MultiItem> shardItems = new();
            Dictionary<ItemKey, string> itemGates = new();
            foreach ((ItemKey itemKey, MultiItem multiItem) in ann.MultiItems)
            {
                if (!multiItem.IsShard)
                {
                    continue;
                }
                if (itemKey.Type != ItemType.Goods)
                {
                    throw new NotImplementedException($"Item {game.Name(itemKey)} cannot be split into shards");
                }
                // Currently this is forbidden, but if we got here do the thing that won't softlock things based on how NorandomItems currently works
                if (ann.NorandomItems.Contains(itemKey) && multiItem.ReqCount > 1)
                {
                    throw new Exception($"Item {game.Name(itemKey)} can't be split into shards as it's not randomized");
                }
                PARAM.Row row = game.Item(itemKey);
                // Note it's fine to load into the game without maxNum set, but acquiring the item will run into issues
                row["maxNum"].Value = (short)999;
                row["maxRepositoryNum"].Value = (short)999;
                // Show quantity number, also set on Larval Tear, Seedbed Curse, etc
                row["isConsume"].Value = (byte)1;
                // Always rewrite name to indicate shard system in place
                if (multiItem.BaseName == "messmershard")
                {
                    // For now, keep using old name for Messmer Shards while the generic name is not localized
                    messages.SetFMGEntry(game, FMGCategory.Item, "GoodsName", 2008021, messmerShard);
                }
                else
                {
                    messages.SetFMGEntry(game, FMGCategory.Item, "GoodsName", itemKey.ID, ItemPreset.ShardName, FMGArg.Item("GoodsName", itemKey.ID, game.Name(itemKey)));
                }
                // Not strictly needed if ReqCount is 1 but err on the side of clarity
                if (multiItem.BaseName == "messmershard")
                {
                    messages.SetFMGEntry(game, FMGCategory.Item, "GoodsInfo", 2008021, messmerShardDesc, multiItem.ReqCount);
                }
                else
                {
                    messages.SetFMGEntry(game, FMGCategory.Item, "GoodsInfo", itemKey.ID, shardDesc, FMGArg.Item("GoodsInfo", itemKey.ID, game.Name(itemKey)), multiItem.ReqCount);
                }
                game.WriteFMGs = true;
                itemGates[itemKey] = multiItem.BaseName;
                shardItems[multiItem.BaseName] = multiItem;
            }

            HashSet<string> blockGates = new();
            if (opt["altusgate"])
            {
                blockGates.Add("altus");
            }
            if (opt["deathless"])
            {
                blockGates.Add("deathless");
            }
            if (opt["latedragonbarrow"])
            {
                blockGates.Add("dragonbarrow");
            }
            if (opt["liurniagate"])
            {
                blockGates.Add("liurnia");
            }
            if (opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out int roldRunes))
            {
                // Reuse item name routines
                if (shardItems.ContainsKey("roldmedallion")) throw new Exception($"Cannot make Rold Medallion Shard if entering Mountaintops has Great Runes requirement");
                ItemKey rold = ann.ItemGroups["removerold"][0];
                itemGates[rold] = "roldmedallion";
                blockGates.Add("roldmedallion");
            }
            if (opt.GetNonDefaultInt(RandomizerOptions.IntOpt.RunesLeyndell, out int leyndellRunes))
            {
                blockGates.Add("leyndell");
            }
            Dictionary<string, ItemKey> gateItems = itemGates.ToDictionary(e => e.Value, e => e.Key);
            HashSet<string> enabledGates = new(shardItems.Keys.Concat(blockGates));

            // Lazily initialized flags, to create events as needed
            Dictionary<string, int> gateFlags = new();
            List<EMEVD.Event> gateCheckEvents = new();
            int gateBase = GameData.EldenRingBase + 200;
            uint entityIdBase = 1324030000;
            int miscEventBase = 1324037000;
            int gateEventBase = 1324038000;
            // This should probably return the flag since I don't think it can return false? Or else allow disabled gates
            bool getGateFlag(string gate, out int targetFlag)
            {
                if (gateFlags.TryGetValue(gate, out targetFlag))
                {
                    return true;
                }
                EMEVD.Event newEvent = null;
                if (shardItems.TryGetValue(gate, out MultiItem multiItem))
                {
                    if (logicFlagIndices.TryGetValue(gate, out int index))
                    {
                        targetFlag = gateBase + index;
                        int wait = gate == "omother" ? 1 : 5;  // More noticeable for O Mother since there's an extra drop later
                        newEvent = events.GetSimpleEvent(
                            gateEventBase + index,
                            new[]
                            {
                                // It's not possible to detect quantity changes without polling or waiting for each individual item flag, so do the former for now.
                                // This unsets the flag the start of the event to try to sync the flag if conditions change for some reason.
                                // However, it doesn't restart once the flag is set, mainly because I don't think seamless coop sync for this flag can be avoided.
                                // Also hopefully it's fine to reuse this event alue flag across events
                                $"StoreItemAmountHeldInEventValue(ItemType.Goods, {multiItem.Key.ID}, {shardFlag}, 10)",
                                $"IfEventValue(AND_10, {shardFlag}, 10, ComparisonType.GreaterOrEqual, {multiItem.ReqCount})",
                                $"SkipIfConditionGroupStateUncompiled(3, PASS, AND_10)",
                                $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, OFF)",
                                $"WaitFixedTimeSeconds({wait})",
                                "EndUnconditionally(EventEndType.Restart)",
                                $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, ON)",
                            },
                            EMEVD.Event.RestBehaviorType.Restart);
                    }
                }
                else if (blockGates.Contains(gate))
                {
                    if (!gates.TryGetValue(gate, out LogicGate logicGate) || logicGate.Cond == null)
                    {
                        // Just require explicitly set for now
                        throw new Exception($"Internal error: no condition for gate {gate}");
                    }
                    if (logicGate.Cond == "none") return false;
                    // At this point, flag must exist
                    if (!int.TryParse(logicGate.Cond, out targetFlag) && logicGate.Cond == "custom")
                    {
                        if (gate == "leyndell")
                        {
                            targetFlag = 180 + opt.GetIntOrDefault(RandomizerOptions.IntOpt.RunesLeyndell);
                        }
                        else if (gate == "altus" && logicFlagIndices.TryGetValue(gate, out int index))
                        {
                            targetFlag = gateBase + index;
                            // This is a tricky one, it requires both Dectus halves. This is effectively mutual recursion with getItemUseFlag
                            int gateOrItemFlag(string item)
                            {
                                if (enabledGates.Contains(item) && getGateFlag(item, out int subFlag))
                                {
                                    return subFlag;
                                }
                                else if (ann.Items.TryGetValue(item, out ItemKey key) && mapping.ItemEventFlags.TryGetValue(key, out int getFlag) && getFlag > 0)
                                {
                                    return getFlag;
                                }
                                else throw new Exception($"Somehow can't find requirements for {item} for custom Altus access (flag {mapping.ItemEventFlags.GetValueOrDefault(key, -1)})");
                            };
                            // A more flexible approach would be forcing the LogicFlags flag for the Dectus halves to do item checks instead
                            int left = gateOrItemFlag("dectusmedallionleft");
                            int right = gateOrItemFlag("dectusmedallionright");
                            newEvent = events.GetSimpleEvent(
                                gateEventBase + index,
                                new[]
                                {
                                    $"IfEventFlag(AND_10, ON, TargetEventFlagType.EventFlag, {left})",
                                    $"IfEventFlag(AND_10, ON, TargetEventFlagType.EventFlag, {right})",
                                    $"SkipIfConditionGroupStateUncompiled(1, PASS, AND_10)",
                                    $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, OFF)",
                                    $"IfEventFlag(AND_01, ON, TargetEventFlagType.EventFlag, {left})",
                                    $"IfEventFlag(AND_01, ON, TargetEventFlagType.EventFlag, {right})",
                                    "IfConditionGroup(MAIN, PASS, AND_01)",
                                    $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, ON)",
                                },
                                EMEVD.Event.RestBehaviorType.Restart);
                        }
                        else if (gate == "roldmedallion")
                        {
                            targetFlag = 180 + opt.GetIntOrDefault(RandomizerOptions.IntOpt.RunesRold);
                        }
                    }
                    if (targetFlag == 0)
                    {
                        throw new Exception($"Internal error: no condition found for gate {gate} with type {logicGate.Cond}");
                    }
                }
                else throw new Exception($"Internal error: Attempting to use disabled or unknown gate {gate}");
                if (targetFlag == 0)
                {
                    return false;
                }
                if (newEvent != null)
                {
                    gateCheckEvents.Add(newEvent);
                }
                processedGates.Add(gate);
                gateFlags[gate] = targetFlag;
                return true;
            }
            // Condition for item use, if tracked. This is for all-or-nothing cases and does not support non-shard multi-items
            bool getItemUseFlag(ItemKey key, out int useFlag)
            {
                if (itemGates.TryGetValue(key, out string shardGate))
                {
                    return getGateFlag(shardGate, out useFlag);
                }
                return mapping.ItemEventFlags.TryGetValue(key, out useFlag) && useFlag > 0;
            }
            bool getFlagEdit(string type, int flag, string debugId, out int targetFlag, out ItemKey item)
            {
                targetFlag = 0;
                item = null;
                if (type == "itemflag")
                {
                    if (mapping.TrackedFlagItems.TryGetValue(flag, out item) && getItemUseFlag(item, out int useFlag) && useFlag != flag)
                    {
                        // Presence of item means to prioritize has-item check, which is either not possible or not correct for itemflag
                        item = null;
                        targetFlag = useFlag;
                        return true;
                    }
                }
                else if (type.StartsWith("item"))
                {
                    if (getFlagAcquire(flag, debugId, out item, out int itemFlag) && itemFlag > 0 && flag != itemFlag)
                    {
                        targetFlag = itemFlag;
                        return true;
                    }
                }
                else if (type.StartsWith("loc"))
                {
                    // rewrittenFlags is the alteration of an item lot or shop's flag, so location checks should use the new flag
                    if (mapping.RewrittenFlags.TryGetValue(flag, out int newFlag) && newFlag != flag)
                    {
                        targetFlag = newFlag;
                        return true;
                    }
                }
                return false;
            }

            // Unfortunately, clone and modify entire Sekiro emevd routine because it depends on EMEDF and we don't have a usable one
            // TODO: This can probably be rewritten now that all args have type metadata
            Dictionary<uint, ItemEventSpec> templates = eventConfig.ItemEvents.ToDictionary(e => e.ID, e => e);

            Dictionary<(int, int), (int, int)> flagPositions = new Dictionary<(int, int), (int, int)>
            {
                [(3, 0)] = (1, 1),
                [(3, 1)] = (1, 2),
                [(3, 10)] = (1, 2),
                [(3, 12)] = (1, 1),
                [(1003, 0)] = (1, 1),
                [(1003, 1)] = (1, 1),
                [(1003, 2)] = (1, 1),
                [(1003, 3)] = (1, 2),
                [(1003, 4)] = (1, 2),
                [(1003, 101)] = (1, 1),
                [(1003, 103)] = (1, 2),
                [(2003, 17)] = (0, 1),
                [(2003, 22)] = (0, 1),
                [(2003, 63)] = (0, 1),
                [(2003, 66)] = (1, 1),
                [(2003, 69)] = (1, 1),
            };

            HashSet<(object, int)> completedTemplates = new();
            Dictionary<uint, EMEVD.Event> commonEvents = game.Emevds["common_func"].Events.ToDictionary(e => (uint)e.ID, e => e);
            HashSet<string> specialEdits = new HashSet<string>
            {
                "ashdupe", "singleton", "removecheck", "runearg", "forge",
                "volcanoreq", "leyndell",
            };
            HashSet<ItemType> scriptCheckableTypes = new() { ItemType.Weapon, ItemType.Protector, ItemType.Accessory, ItemType.Goods };
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                if (!opt["dlc"] && game.IsEldenDlcMap(entry.Key))
                {
                    // Needed as there's some DLC MSB-editing below
                    continue;
                }
                EMEVD emevd = entry.Value;
                Dictionary<uint, EMEVD.Event> fileEvents = entry.Value.Events.ToDictionary(e => (uint)e.ID, e => e);
                foreach (EMEVD.Event e in emevd.Events)
                {
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction init = e.Instructions[i];
                        if (!(init.Bank == 2000 && (init.ID == 0 || init.ID == 6))) continue;
                        List<object> initArgs = init.UnpackArgs(Enumerable.Repeat(ArgType.Int32, init.ArgData.Length / 4));
                        int offset = 2;
                        uint callee = (uint)(int)initArgs[1];
                        if (!templates.TryGetValue(callee, out ItemEventSpec ev))
                        {
                            continue;
                        }
                        if (ev.FlagTemplate == null && ev.Template == null)
                        {
                            continue;
                        }
                        string debugId = $"Event {callee} in {entry.Key}";

                        // Collect templates. Some of this should possibly be merged with other games to avoid duplication, but this requires standard configs at this point.
                        bool forceRemove = false;
                        List<(EMEVD.Event, ItemTemplate)> mainPasses = new();
                        foreach (ItemTemplate t in ev.Template ?? new())
                        {
                            if (t.Type == "default")
                            {
                                continue;
                            }
                            if (t.Filter != null)
                            {
                                bool eligible = true;
                                if (t.Filter.Gate != null && !enabledGates.Contains(t.Filter.Gate))
                                {
                                    eligible = false;
                                }
                                if (t.Filter.Gates != null && !t.Filter.Gates.Split(' ').Any(enabledGates.Contains))
                                {
                                    eligible = false;
                                }
                                if (t.Filter.Content == "dlc" && !opt["dlc"])
                                {
                                    eligible = false;
                                }
                                if (t.Filter.Args != null)
                                {
                                    foreach ((string argName, long val) in t.Filter.Args)
                                    {
                                        if (!TryFullArgSpec(argName, out int pos))
                                        {
                                            throw new Exception($"Internal error: Bad {argName}");
                                        }
                                        int arg = (int)initArgs[offset + pos];
                                        if (arg != (int)val)
                                        {
                                            eligible = false;
                                        }
                                    }
                                }
                                if (!eligible)
                                {
                                    continue;
                                }
                            }
                            if (t.Type == "remove")
                            {
                                forceRemove = true;
                                continue;
                            }
                            else if (t.Type.Contains("arg"))
                            {
                                mainPasses.Add((null, t));
                            }
                            else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent) || commonEvents.TryGetValue(callee, out theEvent))
                            {
                                if (!completedTemplates.Add((t, 0))) continue;
                                mainPasses.Add((theEvent, t));
                            }
                            else
                            {
                                throw new Exception($"Initialized event {callee} but absent from {entry.Key} and not specified in args");
                            }
                        }

                        // Source flag and event to edit. We're not copying the event currently so the pass handling is relatively simple.
                        List<(int, EMEVD.Event, ItemFlagTemplate)> flagPasses = new();
                        foreach (ItemFlagTemplate t in ev.FlagTemplate ?? new())
                        {
                            if (t.Type == "remove")
                            {
                                forceRemove = true;
                                continue;
                            }
                            // Non-special types: item itemarg itemargdlc, loc locarg, fixeditem, default, remove
                            if (t.Type == "fixeditem" || t.Type == "default") continue;
                            // Without DLC data, common 65910 can fail with inaccessible item for e.g. 2046477150
                            if (t.Type.Contains("dlc") && !opt["dlc"]) continue;
                            List<int> templateFlags = t.EventFlag == null ? new List<int>() : t.EventFlag.Split(' ').Select(int.Parse).ToList();
                            List<int> flags;
                            if (specialEdits.Contains(t.Type))
                            {
                                flags = new List<int> { 0 };
                            }
                            else if (t.Type.Contains("arg"))
                            {
                                if (t.EventFlagArg == null) throw new Exception($"Internal error: No arg defined for item flag in {callee}");
                                if (!TryArgSpec(t.EventFlagArg.Split(' ').Last(), out int pos))
                                {
                                    throw new Exception($"Internal error: Bad argspec {callee}");
                                }
                                int argFlag = (int)initArgs[offset + pos];
                                if (!templateFlags.Contains(argFlag))
                                {
                                    // Console.WriteLine($"{callee}: {t.EventFlagArg} {argFlag} not an item flag");
                                    continue;
                                }
                                flags = new List<int> { argFlag };
                            }
                            else
                            {
                                flags = templateFlags;
                            }
                            foreach (int flag in flags)
                            {
                                if (t.Type.Contains("arg"))
                                {
                                    flagPasses.Add((flag, null, t));
                                }
                                else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent) || commonEvents.TryGetValue(callee, out theEvent))
                                {
                                    if (!completedTemplates.Add((t, flag))) continue;
                                    flagPasses.Add((flag, theEvent, t));
                                }
                                else
                                {
                                    throw new Exception($"Initialized event {callee} but absent from {entry.Key} and not specified in args");
                                }
                            }
                        }
                        if (forceRemove)
                        {
                            // Remove action by removing initialization, for now. Can garbage collect later if desired.
                            e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                            game.WriteEmevds.Add(entry.Key);
                            continue;
                        }

                        // Passes
                        foreach ((EMEVD.Event e2, ItemTemplate t) in mainPasses)
                        {
                            string gate = t.Filter?.Gate;
                            int gateFlag()
                            {
                                if (gate == null) throw new Exception($"Internal error: Cond flag required in {callee} but no logic gate defined");
                                if (!getGateFlag(gate, out int flag)) throw new Exception($"Internal error: No flag for enabled logic gate {gate}");
                                return flag;
                            }
                            if (e2 == null)
                            {
                                if (t.SetArg != null)
                                {
                                    string[] parts = t.SetArg.Split(' ');
                                    if (parts.Length != 2 || !TryArgSpec(parts[0], out int pos)) throw new Exception($"Internal error: Bad {t.SetArg} in config");
                                    // This happens due to imprecise callee resolution and duplicate map ids
                                    if (offset + pos > initArgs.Count) continue;
                                    int flag = parts[1] == "gate" ? gateFlag() : int.Parse(parts[1]);
                                    initArgs[offset + pos] = flag;
                                    init.PackArgs(initArgs);
                                }
                                if (t.ItemArg != null)
                                {
                                    foreach (string part in t.ItemArg.Split(' '))
                                    {
                                        if (!TryArgSpec(part, out int pos)) throw new Exception($"Internal error: Bad {t.ItemArg} in config");
                                        if (offset + pos > initArgs.Count) throw new Exception($"Too big: {entry.Key} {callee}, pos {pos} in ({string.Join(", ", initArgs)})");
                                        int itemId = (int)initArgs[offset + pos];
                                        if (itemId == 0)
                                        {
                                            continue;
                                        }
                                        ItemKey itemKey = new ItemKey(ItemType.Goods, itemId);
                                        if (!getItemUseFlag(itemKey, out int useFlag))
                                        {
                                            // If the filter passed, this edit is mandatory for all initializations.
                                            // It may cause issues merging mods where key items are removed, but at least for lifts, these events must either be all edited or none edited
                                            throw new Exception($"Item {game.Name(itemKey)} was missing or not tracked, but must be present for editing game logic");
                                        }
                                        initArgs[offset + pos] = useFlag;
                                        init.PackArgs(initArgs);
                                    }
                                }
                                game.WriteEmevds.Add(entry.Key);
                                continue;
                            }
                            EventEdits edits = new EventEdits();
                            OldParams pre = OldParams.Preprocess(e2);
                            if (t.Add != null)
                            {
                                events.AddMacro(edits, t.Add);
                            }
                            if (t.Replaces != null)
                            {
                                events.ReplaceMacro(edits, t.Replaces);
                            }
                            if (t.Removes != null)
                            {
                                foreach (string remove in events.Decomment(t.Removes))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.CondIdentity != null)
                            {
                                foreach (string check in events.Decomment(t.CondIdentity))
                                {
                                    events.IdentityMacro(edits, check);
                                }
                            }
                            if (t.CondFlag > 0)
                            {
                                events.ReplaceMacro(edits, t.CondFlag.ToString(), gateFlag().ToString(), EventValueType.Flag);
                            }
                            if (t.ItemUse != null)
                            {
                                foreach (string useCmd in t.ItemUse)
                                {
                                    if (useCmd == "start")
                                    {
                                        // Special case for events without checks that need them
                                        // This doesn't give much feedback, potentially it could show a dialog
                                        events.AddMacro(edits, new EventAddCommand
                                        {
                                            Cmd = $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {gateFlag()})",
                                            Before = "start",
                                        });
                                        continue;
                                    }
                                    // Add-then-remove is fine to use here because skip-aware mode, though in-place would be ideal
                                    Instr ins = events.ParseAddInstr(useCmd);
                                    if (ins.Name != "IfPlayerHasdoesntHaveItem")
                                    {
                                        throw new Exception($"Internal error: Can't edit item usage for {useCmd}");
                                    }
                                    int check = (byte)ins[3];
                                    if (ins[2] is string itemArg)
                                    {
                                        // Arg case, the gate comes from the parameter
                                        events.ReplaceMacro(edits, useCmd, $"IfEventFlag({ins[0]}, {check}, TargetEventFlagType.EventFlag, {itemArg})");
                                    }
                                    else
                                    {
                                        int item = (int)ins[2];
                                        events.ReplaceMacro(edits, useCmd, $"IfEventFlag({ins[0]}, {check}, TargetEventFlagType.EventFlag, {gateFlag()})");
                                    }
                                }
                            }
                            bool adjustGroups = game.HasMods;
#if DEBUG
                            adjustGroups = true;
#endif
                            if (adjustGroups)
                            {
                                events.AdjustConditionGroups(edits, e2, pre, t.CondOrder, !game.HasMods, callee);
                            }
                            events.ApplyAllEdits(e2, edits, () => $"{debugId} has unapplied edits in item randomizer" + (gate == null ? "" : $" (to edit {gate} logic)"));
                            game.WriteEmevds.Add(entry.Key);
                        }  // Main pass

                        foreach ((int flag, EMEVD.Event e2, ItemFlagTemplate t) in flagPasses)
                        {
                            // Types: item itemarg, loc locarg, ashdupe, singleton
                            if (t.Type == "ashdupe")
                            {
                                // In this case, edit the event. Simplified version of it:
                                // Event 65810. X0_4 = duplication shop qwc, X4_4 = get event flag
                                // EndIf(EventFlag(X0_4));
                                // WaitFor(EventFlag(X4_4));
                                // SetEventFlag(TargetEventFlagType.EventFlag, X0_4, ON);
                                // if (!EventFlag(65800)) SetEventFlag(TargetEventFlagType.EventFlag, 65800, ON);
                                // Unfortunately, PlayerHasItem doesn't work for gems. We need to do an off->on check.
                                OldParams pre = OldParams.Preprocess(e2);
                                // EndIfEventFlag(EventEndType.End, ON, TargetFlagType.EventFlag, X4_4)
                                EMEVD.Instruction check = new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)0, 0 });
                                pre.AddParameters(check, new List<EMEVD.Parameter> { new EMEVD.Parameter(0, 4, 4, 4) });
                                e2.Instructions.Insert(0, check);
                                pre.Postprocess();
                                continue;
                            }
                            else if (t.Type == "forge")
                            {
                                OldParams pre = OldParams.Preprocess(e2);
                                // EndIfEventFlag(EventEndType.End, ON, TargetFlagType.EventFlag, X4_4)
                                (EMEVD.Instruction set, List<EMEVD.Parameter> ps) = events.ParseAddArg($"SetEventFlag(TargetEventFlagType.EventFlag, {t.EventFlagArg}, ON)");
                                e2.Instructions.Add(set);
                                pre.AddParameters(set, ps);
                                pre.Postprocess();
                                continue;
                            }
                            else if (t.Type == "singleton")
                            {
                                OldParams pre = OldParams.Preprocess(e2);
                                // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventIDSlotNumber, 0)
                                EMEVD.Instruction check = new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)2, 0 });
                                e2.Instructions.Insert(0, check);
                                pre.Postprocess();
                                continue;
                            }
                            else if (t.Type == "removecheck")
                            {
                                OldParams pre = OldParams.Preprocess(e2);
                                if (TryArgSpec(t.EventFlagArg, out int pos))
                                {
                                    for (int j = e2.Instructions.Count - 1; j >= 0; j--)
                                    {
                                        EMEVD.Instruction ins = e2.Instructions[j];
                                        // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, X12_4)
                                        if (ins.Bank == 1003 && ins.ID == 2)
                                        {
                                            EMEVD.Parameter flagParam = e2.Parameters.Find(p =>
                                                p.InstructionIndex == j && p.SourceStartByte == pos * 4 && p.TargetStartByte == 4);
                                            if (flagParam != null)
                                            {
                                                e2.Instructions[j] = new EMEVD.Instruction(1014, 69);
                                                game.WriteEmevds.Add(entry.Key);
                                            }
                                        }
                                    }
                                }
                                pre.Postprocess();
                                continue;
                            }
                            else if (t.Type == "volcanoreq")
                            {
                                // Don't switch to 3106 and 3107 without joining the Volcano Manor (flag 16009208)
                                // Accomplish through label and jump
                                // TODO: Migrate to ItemTemplate. For now it works fine but super confusing.
                                bool addedLabel = false;
                                for (int j = e2.Instructions.Count - 1; j >= 0; j--)
                                {
                                    EMEVD.Instruction ins = e2.Instructions[j];
                                    // SetEventFlag(TargetEventFlagType.EventFlag, 3107, ON)
                                    if (ins.Bank == 2003 && ins.ID == 66)
                                    {
                                        List<object> args = ins.UnpackArgs(new[] { ArgType.Byte, ArgType.UInt32, ArgType.Byte });
                                        if ((uint)args[1] == 3107)
                                        {
                                            EMEVD.Instruction add = new EMEVD.Instruction(1014, 15);
                                            e2.Instructions.Insert(j + 1, add);
                                            addedLabel = true;
                                        }
                                    }
                                    // GotoIfEventFlag(Label.LABEL0, OFF, TargetEventFlagType.EventFlag, 3100)
                                    if (addedLabel && ins.Bank == 1003 && ins.ID == 101)
                                    {
                                        List<object> args = ins.UnpackArgs(new[] { ArgType.Byte, ArgType.Byte, ArgType.Byte, ArgType.UInt32 });
                                        if ((byte)args[0] == 0 && (uint)args[3] == 3100)
                                        {
                                            EMEVD.Instruction add = new EMEVD.Instruction(
                                                1003, 101, new List<object> { (byte)15, (byte)0, (byte)0, 16009208 });
                                            e2.Instructions.Insert(j + 1, add);
                                            break;
                                        }
                                    }
                                }
                                continue;
                            }
                            else if (t.Type == "runearg")
                            {
                                // First arg position is the rune activation flag (191 through 196)
                                // Second arg position is the must-show-up flag (boss defeat flag by default)
                                // Rewrite the second to match the first, when it's a valid activation flag
                                // This depends on the indices lining up; otherwise, it will require a manual list.
                                string[] parts = t.EventFlagArg?.Split(' ');
                                if (parts?.Length != 2
                                    || !TryArgSpec(parts[0], out int activatePos)
                                    || !TryArgSpec(parts[1], out int showPos))
                                {
                                    throw new Exception($"Internal error: Invalid runearg format {t.EventFlagArg}");
                                }
                                int activateFlag = (int)initArgs[offset + activatePos];
                                if (activateFlag >= 191 && activateFlag <= 196)
                                {
                                    int getFlag = activateFlag - 20;
                                    initArgs[offset + showPos] = getFlag;
                                    init.PackArgs(initArgs);
                                    game.WriteEmevds.Add(entry.Key);
                                }
                                continue;
                            }
                            else if (t.Type == "leyndell")
                            {
                                // For the leyndell edit, replace the Great Runes flag with a different amount if requested
                                // It turns out that 180 is a valid flag after 0 GRs.
                                if (!opt.GetInt(RandomizerOptions.IntOpt.RunesLeyndell, out leyndellRunes) || leyndellRunes == 2)
                                {
                                    continue;
                                }
                                int unlockFlag = 180 + leyndellRunes;
                                OldParams pre = OldParams.Preprocess(e2);
                                for (int j = e2.Instructions.Count - 1; j >= 0; j--)
                                {
                                    EMEVD.Instruction ins = e2.Instructions[j];
                                    if (flagPositions.TryGetValue((ins.Bank, ins.ID), out (int, int) range))
                                    {
                                        (int aPos, int bPos) = range;
                                        List<object> args = ins.UnpackArgs(Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4));
                                        int flagVal = (int)args[bPos];
                                        if (flagVal != 182) continue;
                                        args[aPos] = args[bPos] = unlockFlag;
                                        ins.PackArgs(args);
                                        game.WriteEmevds.Add(entry.Key);
                                    }
                                }
                                pre.Postprocess();
                                continue;
                            }
                            if (flag <= 0) throw new Exception($"Internal error: Flag missing for {callee} item flag rewrite");

                            if (!getFlagEdit(t.Type, flag, debugId, out int targetFlag, out ItemKey item))
                            {
                                continue;
                            }

                            bool edited = false;
                            if (t.EventFlagArg != null)
                            {
                                foreach (string arg in t.EventFlagArg.Split(' '))
                                {
                                    if (!TryArgSpec(arg, out int pos))
                                    {
                                        throw new Exception($"Internal error: Bad argspec {callee}");
                                    }
                                    initArgs[offset + pos] = targetFlag;
                                    init.PackArgs(initArgs);
                                    edited = true;
                                }
                            }
                            else if (e2 != null)
                            {
                                OldParams pre = OldParams.Preprocess(e2);
                                for (int j = e2.Instructions.Count - 1; j >= 0; j--)
                                {
                                    EMEVD.Instruction ins = e2.Instructions[j];
                                    if (flagPositions.TryGetValue((ins.Bank, ins.ID), out (int, int) range))
                                    {
                                        (int aPos, int bPos) = range;
                                        List<object> args = ins.UnpackArgs(Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4));
                                        int flagVal = (int)args[bPos];
                                        if (flag != flagVal) continue;
                                        // Custom case for item checks: check item directly, if it can be done in-place
                                        // This doesn't get all of them, there are a few skips/gotos/ends e.g. in 12042400, 1050563700
                                        if (item != null && scriptCheckableTypes.Contains(item.Type) && ins.Bank == 3 && ins.ID == 0)
                                        {
                                            // 3[00] IfEventFlag(sbyte group, byte flagState, byte flagType, int flag)
                                            args = ins.UnpackArgs(new[] { ArgType.SByte, ArgType.Byte, ArgType.Byte, ArgType.Int32 });
                                            // 3[04] IfPlayerHasdoesntHaveItem(sbyte group, byte itemType, int itemId, byte ownState)
                                            e2.Instructions[j] = ins = new EMEVD.Instruction(3, 4);
                                            // Dependency on ItemType value
                                            ins.PackArgs(new List<object> { args[0], (byte)item.Type, item.ID, args[1] });
                                            edited = true;
                                        }
                                        // This is an even more involved rewrite for Goto/End, which is needed for consistency with the first case
                                        else if (t.ItemCond != 0 && item != null && scriptCheckableTypes.Contains(item.Type) && ins.Bank == 1003 && (ins.ID == 1 || ins.ID == 2 || ins.ID == 101))
                                        {
                                            // TODO: This is very iffy. Should use EMEDF for this to pre-transform the event instead.
                                            if (t.ItemCond > 15)
                                            {
                                                throw new Exception($"Cannot rewrite {callee} with item {item}, ran out of conds");
                                            }
                                            // 1003[1/2/101] [Skip/End/Goto]IfEventFlag(byte control, byte flagState, byte flagType, int flag)
                                            args = ins.UnpackArgs(new[] { ArgType.Byte, ArgType.Byte, ArgType.Byte, ArgType.Int32 });
                                            // 3[04] IfPlayerHasdoesntHaveItem(sbyte group, byte itemType, int itemId, byte ownState)
                                            // Dependency on ItemType value
                                            e2.Instructions[j] = new EMEVD.Instruction(
                                                3, 4, new List<object> { (sbyte)t.ItemCond, (byte)item.Type, item.ID, args[1] });
                                            // 1000[1/2/101] [Skip/End/Goto]IfConditionGroupStateUncompiled(byte control, byte state, sbyte group)
                                            e2.Instructions.Insert(j + 1, new EMEVD.Instruction(
                                                1000, ins.ID, new List<object> { args[0], (byte)1, (sbyte)t.ItemCond }));
                                            edited = true;
                                            // >:(
                                            t.ItemCond++;
                                        }
                                        else if (targetFlag > 0)
                                        {
                                            args[aPos] = args[bPos] = targetFlag;
                                            ins.PackArgs(args);
                                            edited = true;
                                        }
                                    }
                                }
                                pre.Postprocess();
                            }
                            if (!edited) new Exception($"Couldn't apply flag edit {flag} -> {targetFlag} to {callee}");
                            if (e2 != null && commonEvents.ContainsKey(callee))
                            {
                                game.WriteEmevds.Add("common_func");
                            }
                            else
                            {
                                game.WriteEmevds.Add(entry.Key);
                            }
                        }  // Flag pass
                    }
                }
                // New events
                if (entry.Key == "common")
                {
                    List<EMEVD.Instruction> runeInstrs = new List<EMEVD.Instruction>
                    {
                        // SetNetworkSyncState(Disabled = 0)
                        new EMEVD.Instruction(2000, 2, new List<object> { (byte)0 }),
                    };
                    // Basic basic structure: if get either runes, and the base flag is not set
                    int reg = 1;
                    foreach (KeyValuePair<int, (ItemKey, ItemKey)> rune in PermutationWriter.GreatRuneItems)
                    {
                        int flag = rune.Key;
                        ItemKey a = rune.Value.Item1;
                        ItemKey b = rune.Value.Item2;
                        // Dependency on ItemType value
                        runeInstrs.AddRange(new List<EMEVD.Instruction>
                        {
                            // IfEventFlag(reg, OFF, TargetEventFlagType.EventFlag, flag)
                            new EMEVD.Instruction(3, 0, new List<object> { (sbyte)reg, (byte)0, (byte)0, flag }),
                            // IfPlayerHasdoesntHaveItem(-reg, a.Type, a.ID, OwnershipState.Owns = 1)
                            // IfPlayerHasdoesntHaveItem(-reg, b.Type, b.ID, OwnershipState.Owns = 1)
                            new EMEVD.Instruction(3, 4, new List<object> { (sbyte)-reg, (byte)a.Type, a.ID, (byte)1 }),
                            new EMEVD.Instruction(3, 4, new List<object> { (sbyte)-reg, (byte)b.Type, b.ID, (byte)1 }),
                            // IfConditionGroup(reg, PASS, -reg)
                            new EMEVD.Instruction(0, 0, new List<object> { (sbyte)reg, (byte)1, (sbyte)-reg }),
                            // IfConditionGroup(-10, PASS, reg)
                            new EMEVD.Instruction(0, 0, new List<object> { (sbyte)-10, (byte)1, (sbyte)reg }),
                        });
                        reg++;
                    }
                    // IfConditionGroup(MAIN, PASS, -10)
                    runeInstrs.Add(new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)-10 }));
                    // runeInstrs.Add(new EMEVD.Instruction(2003, 4, new List<object> { 997220 }));
                    reg = 1;
                    foreach (KeyValuePair<int, (ItemKey, ItemKey)> rune in PermutationWriter.GreatRuneItems)
                    {
                        int flag = rune.Key;
                        runeInstrs.AddRange(new List<EMEVD.Instruction>
                        {
                            // SkipIfConditionGroupStateCompiled(1, OFF, reg)
                            new EMEVD.Instruction(1000, 7, new List<object> { (byte)1, (byte)0, (byte)reg }),
                            // SetEventFlag(TargetEventFlagType.EventFlag, flag, ON)
                            new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, flag, (byte)1 }),
                        });
                        reg++;
                    }
                    // We could loop this, but there are weird states (like flag on but no item)
                    // so it's simpler just to make it happen a single time on reload.
                    AddSimpleEvent(emevd, 19003130, runeInstrs, EMEVD.Event.RestBehaviorType.Restart);

                    // Event for O Mother in shops, and now also drops
                    // In base game, added by InitializeCommonEvent(0, 90005570, getFlag 60864, gesture 115, asset 2050451680, actionButton 2, sfx 2, unk 0)
                    // The flag is not actually used, just used to mark a drop
                    if (mapping.GestureFlag > 0)
                    {
                        if (enabledGates.Contains("omother") && getGateFlag("omother", out int targetFlag))
                        {
                            events.AddSimpleEvent(emevd, 19003131, new[]
                            {
                                "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 60864)",
                                $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {targetFlag})",
                                "AwardGesture(115)",
                                "SetEventFlag(TargetEventFlagType.EventFlag, 60864, ON)",
                            });
                        }
                        else
                        {
                            // Try to hide item normally and remove from inventory normally
                            events.AddSimpleEvent(emevd, 19003131, new[]
                            {
                                // "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 60864)",
                                "IfPlayerHasdoesntHaveItem(MAIN, ItemType.Goods, 2009004, OwnershipState.Owns)",
                                "RemoveItemFromPlayer(ItemType.Goods, 2009004, 1)",
                                "AwardGesture(115)",
                                "SetEventFlag(TargetEventFlagType.EventFlag, 60864, ON)",
                            });
                        }
                    }
                }
                if (eventConfig.LogicFlags == null && entry.Key == "m60_49_53_00" && opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out roldRunes))
                {
                    int unlockFlag = 180 + roldRunes;
                    // Rold Medallion has been taken out of logic, so make self-contained logic to award it here.
                    // This is similar to Sekiro memory lots, which are invented from whole cloth.
                    // It precludes it from being added in hint logs easily. As an alternative, add it in data scraper.
                    ItemKey rold = ann.ItemGroups["removerold"][0];
                    LotCells roldCells = locEditor.LotCellsForItem(rold);
                    roldCells.EventFlag = roldFlag;
                    locEditor.AddLot("ItemLotParam_map", roldFlag, roldCells);

                    // Just put this in Rold map, otherwise we'd want to add a map check before the radius check
                    List<EMEVD.Instruction> runeInstrs = new List<EMEVD.Instruction>
                    {
                        // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, roldFlag)
                        new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)2, roldFlag }),
                        // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, unlockFlag)
                        new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, unlockFlag }),
                        // IfEntityInoutsideRadiusOfEntity(OR_01, InsideOutsideState.Inside = 1, 10000, <action button entity>, 10f, 1)
                        new EMEVD.Instruction(3, 3, new List<object> { (sbyte)-1, (byte)1, 10000, 1049531502, 10f, 1 }),
                        new EMEVD.Instruction(3, 3, new List<object> { (sbyte)-1, (byte)1, 10000, 1049531504, 10f, 1 }),
                        // IfConditionGroup(MAIN, PASS, OR_01)
                        new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)-1 }),
                        // IfPlayerHasdoesntHaveItem(AND_01, type, id, OwnershipState.Owns = 1)
                        new EMEVD.Instruction(3, 4, new List<object> { (byte)1, (byte)rold.Type, rold.ID, (byte)1 }),
                        // EndIfConditionGroupStateUncompiled(EventEndType.End, PASS, AND_01)
                        new EMEVD.Instruction(1000, 2, new List<object> { (byte)0, (byte)1, (sbyte)1 }),
                        // AwardItemLot(roldFlag)
                        new EMEVD.Instruction(2003, 4, new List<object> { roldFlag }),
                    };
                    AddSimpleEvent(emevd, roldEventId, runeInstrs, EMEVD.Event.RestBehaviorType.Default);
                    game.WriteEmevds.Add(entry.Key);
                    // "You do not have the required medallion" (msg 20020/20021) -> "You cannot use this without more Great Runes" 20004
                    // But the original string does not appear anywhere? So this remains as-is.
                }
                if (entry.Key == "m61_50_45_00" && mapping.GestureFlag > 0)
                {
                    // The shiny item can be reused, just give it a treasure. The corresponding gesture should be removed with removegesture above.
                    // There's not even any event edits here. Oh well
                    MSBE bonnyMsb = game.EldenMaps[entry.Key];
                    uint treeId = 2050451680;
                    MSBE.Part.Asset tree = bonnyMsb.Parts.Assets.Find(a => a.EntityID == treeId);
                    if (tree == null) throw new Exception($"Can't place O Mother gesture replacement: {entry.Key} missing asset {treeId}");
                    bonnyMsb.Events.Treasures.Add(new MSBE.Event.Treasure
                    {
                        MapID = -1,
                        UnkE0C = 255,
                        UnkS0C = -1,
                        Name = "O Mother",
                        TreasurePartName = tree.Name,
                        ItemLotID = GameData.EldenRingBase + 30,
                        ActionButtonID = -1,
                        PickupAnimID = -1,
                    });
                    game.WriteMSBs.Add(entry.Key);
                }
                if (eventConfig.LogicFlags == null && entry.Key == "common" && shardItems.TryGetValue("messmerskindling", out MultiItem multiItem))
                {
                    int shardReq = multiItem.ReqCount;
                    // The item already says this, just allow it
                    // 2030001: The sealing tree obscures the tower in shadow.\nIt cannot be burned without Messmer's kindling.
                    // game.MenuFMGs["EventTextForMap"][shardReqMsgId] = $"{shardReq} shards of Messmer's kindling required";
                    int shardReqMsgId = 2030001;
                    EMEVD.Event e = emevd.Events.Find(e => e.ID == 916);
                    if (e == null) throw new Exception($"Missing common event 916 for Messmer's Kindling Shard");
                    {
                        OldParams pre = OldParams.Preprocess(e);
                        EventEdits edits = new EventEdits();
                        events.RemoveMacro(edits, "RemoveItemFromPlayer");
                        if (shardReq == 0)
                        {
                            // Using IfPlayerHasdoesntHaveItem which requires cond group
                            events.RemoveMacro(edits, "GotoIfConditionGroupStateUncompiled");
                        }
                        else
                        {
                            events.AddMacro(edits, new EventAddCommand
                            {
                                Before = "DisplayGenericDialogAndSetEventFlags",
                                Cmds = new()
                                {
                                    $"StoreItemAmountHeldInEventValue(ItemType.Goods, 2008021, {shardFlag}, 10)",
                                    $"IfEventValue(AND_12, {shardFlag}, 10, ComparisonType.GreaterOrEqual, {shardReq})",
                                    "GotoIfConditionGroupStateUncompiled(Label.Label12, PASS, AND_12)",
                                    $"DisplayGenericDialog({shardReqMsgId}, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5)",
                                    "WaitFixedTimeSeconds(1)",
                                    "EndUnconditionally(EventEndType.Restart)",
                                    "Label12()",
                                },
                            });
                        }
                        events.ApplyAllEdits(e, edits);
                        pre.Postprocess();
                        game.WriteEmevds.Add(entry.Key);
                    }
                }
                if (eventConfig.LogicFlags == null && entry.Key == "m20_01_00_00" && shardItems.ContainsKey("messmerskindling"))
                {
                    // Quick edit to not double-remove kindling if multiple exist, related to above edit
                    EMEVD.Event e = emevd.Events.Find(e => e.ID == 20010197);
                    if (e != null)
                    {
                        OldParams pre = OldParams.Preprocess(e);
                        EventEdits edits = new EventEdits();
                        events.RemoveMacro(edits, "RemoveItemFromPlayer", optional: true);
                        events.ApplyAllEdits(e, edits);
                        pre.Postprocess();
                        game.WriteEmevds.Add(entry.Key);
                    }
                }
            }

            // Now ESDs. AST should make this a lot simpler than the Sekiro case
            Dictionary<int, ItemEventSpec> talkTemplates = eventConfig.ItemTalks.ToDictionary(e => (int)e.ID, e => e);
            bool debugEsd = false;

            // Rewrite item checks for shards and other custom features.
            // The game checks game checks >= 1, == 1, > 0 for positive checks, and == 0 for negative checks
            // To be really comprehensive just evaluate it
            Dictionary<(ESDEdits.ComparisonType, int), bool> knownQuantityChecks = new()
            {
                [(ESDEdits.ComparisonType.GreaterOrEqual, 1)] = true,
                [(ESDEdits.ComparisonType.Equal, 1)] = true,
                [(ESDEdits.ComparisonType.Greater, 0)] = true,
                [(ESDEdits.ComparisonType.Equal, 0)] = false,
            };
            AST.Expr esdFunction(string name, List<int> args)
            {
                return new AST.FunctionCall
                {
                    Name = name,
                    Args = args.Select(a => (AST.Expr)new AST.ConstExpr { Value = a }).ToList(),
                };
            }
            bool rewriteArg(AST.Expr expr, Dictionary<int, string> flagEdits, List<int> itemEdits, string debugId, out byte[] newArg)
            {
                bool modified = false;
                expr = expr.Visit(AST.AstVisitor.Post(e =>
                {
                    if (e is not AST.FunctionCall call)
                    {
                        return null;
                    }
                    // f15 EventFlag(targetFlag), f101 GetEventFlagValue(targetFlag, bits)
                    if (flagEdits.Count > 0 && (call.Name == "f15" || call.Name == "f101") && call.Args.Count >= 1)
                    {
                        if (debugEsd) Console.WriteLine($"  Check call {call} against {string.Join(", ", flagEdits)} in {debugId}");
                        if (call.Args[0].TryAsInt(out int flag)
                            && flagEdits.TryGetValue(flag, out string editType)
                            && getFlagEdit(editType, flag, debugId, out int targetFlag, out ItemKey item))
                        {
                            if (item != null && scriptCheckableTypes.Contains(item.Type))
                            {
                                modified = true;
                                // DoesPlayerHaveItem(type, id)
                                if (debugEsd) Console.WriteLine($"  - Rewriting flag {flag} to item {game.Name(item)}");
                                // Dependency on ItemType value
                                return esdFunction("f16", new List<int> { (int)item.Type, item.ID });
                            }
                            else if (targetFlag > 0)
                            {
                                modified = true;
                                // EventFlag(targetFlag)
                                if (debugEsd) Console.WriteLine($"  - Rewriting flag {flag} to new flag {targetFlag}");
                                return esdFunction("f15", new List<int> { targetFlag });
                            }
                        }
                    }
                    if (itemEdits.Count > 0 && call.Name == "f47" && call.Args.Count >= 4)
                    {
                        if (debugEsd) Console.WriteLine($"  Check call {call} against {string.Join(", ", itemEdits)}");
                        // ESD has the most flexibility for representing conditions, so implement shard conditions directly, otherwise use the flag
                        // e.g. ComparePlayerInventoryNumber(ItemType.Goods, 8175, CompareType.GreaterOrEqual, 1, False)
                        if (call.Args[0].IsInt(3) && call.Args[1].TryAsInt(out int itemId)
                            && call.Args[2].TryAsInt(out int compare) && call.Args[3].TryAsInt(out int quantity)
                            && itemEdits.Contains(itemId) && itemGates.TryGetValue(new ItemKey(ItemType.Goods, itemId), out string gate))
                        {
                            if (!knownQuantityChecks.TryGetValue(((ESDEdits.ComparisonType)compare, quantity), out bool check))
                            {
                                check = true;
                                Util.Warn($"Condition may be incorrect for logic gate {gate}: unknown quantity check {call} in {debugId}");
                            }
                            AST.Expr configExpr;
                            if (shardItems.TryGetValue(gate, out MultiItem multiItem))
                            {
                                configExpr = esdFunction("f47", new() { 3, itemId, (int)ESDEdits.ComparisonType.GreaterOrEqual, multiItem.ReqCount });
                                processedGates.Add(gate);
                            }
                            else if (getGateFlag(gate, out int gateFlag))
                            {
                                configExpr = esdFunction("f15", new List<int> { gateFlag });
                            }
                            else throw new Exception($"Internal error: No flag for enabled logic gate {gate}");
                            if (!check)
                            {
                                configExpr = AST.NegateCond(configExpr);
                            }
                            modified = true;
                            return configExpr;
                        }
                    }
                    return null;
                }));
                newArg = modified ? AST.AssembleExpression(expr) : null;
                return modified;
            }

            List<ESD.Condition> GetConditions(List<ESD.Condition> condList) => Enumerable.Concat(condList, condList.SelectMany(cond => GetConditions(cond.Subconditions))).ToList();
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                bool modified = false;
                foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                {
                    ESD esd = esdEntry.Value;
                    int esdId = int.Parse(esdEntry.Key.Substring(1));
                    if (!talkTemplates.TryGetValue(esdId, out ItemEventSpec spec) || spec.FlagTemplate == null) continue;

                    // We have some edits to do
                    foreach (KeyValuePair<long, Dictionary<long, ESD.State>> machine in esd.StateGroups)
                    {
                        int machineId = (int)machine.Key;
                        string machineName = AST.FormatMachine(machineId);
                        List<ItemFlagTemplate> machineTemplates = spec.FlagTemplate.Where(t => t.Machine == machineName).ToList();
                        if (machineTemplates.Count == 0) continue;
                        string debugId = $"ESD {esdEntry.Key}_{machineName}";

                        Dictionary<int, string> flagEdits = new Dictionary<int, string>();
                        List<int> itemEdits = new();
                        foreach (ItemFlagTemplate t in machineTemplates)
                        {
                            if (debugEsd) Console.WriteLine($"{entry.Key}: Examining {esdId} machine {t.Machine} type {t.Type}");
                            if (t.Type == "default" || t.Type == "fixeditem") continue;
                            if (t.Type == "itemgate")
                            {
                                if (t.UseItem == 0) throw new Exception($"Internal error: no item given for ESD edit {t.Type} in {esdId} {machineName}");
                                itemEdits.Add(t.UseItem);
                                continue;
                            }
                            if ((t.Type != "item" && t.Type != "loc" && t.Type != "itemflag") || t.EventFlag == null)
                            {
                                throw new Exception($"Internal error: unknown ESD edit {t.Type} in {esdId} {machineName}");
                            }
                            foreach (int flag in t.EventFlag.Split(' ').Select(int.Parse))
                            {
                                flagEdits[flag] = t.Type;
                            }
                        }
                        if (flagEdits.Count == 0 && itemEdits.Count == 0) continue;

                        foreach (KeyValuePair<long, ESD.State> stateEntry in machine.Value)
                        {
                            int stateId = (int)stateEntry.Key;
                            ESD.State state = stateEntry.Value;
                            List<ESD.Condition> conds = GetConditions(state.Conditions);
                            foreach (ESD.CommandCall cmd in new[] { state.EntryCommands, state.WhileCommands, state.ExitCommands, conds.SelectMany(c => c.PassCommands) }.SelectMany(c => c))
                            {
                                List<AST.Expr> args = cmd.Arguments.Select(AST.DisassembleExpression).ToList();
                                if (cmd.CommandBank == 1 && cmd.CommandID == 52 && args.Count == 3
                                    && args[0].IsInt(3) && args[1].TryAsInt(out int removeItem) && args[2].IsInt(-1)
                                    && itemGates.ContainsKey(new ItemKey(ItemType.Goods, removeItem)))
                                {
                                    cmd.Arguments[2] = AST.AssembleExpression(AST.MakeVal(0));
                                    continue;
                                }
                                for (int i = 0; i < args.Count; i++)
                                {
                                    if (rewriteArg(args[i], flagEdits, itemEdits, debugId, out byte[] arg2))
                                    {
                                        cmd.Arguments[i] = arg2;
                                        modified = true;
                                    }
                                }
                            }
                            foreach (ESD.Condition cond in conds)
                            {
                                AST.Expr arg = AST.DisassembleExpression(cond.Evaluator);
                                if (rewriteArg(arg, flagEdits, itemEdits, debugId, out byte[] eval2))
                                {
                                    cond.Evaluator = eval2;
                                    modified = true;
                                }
                            }
                        }
                    }
                }
                if (modified)
                {
                    if (debugEsd) Console.WriteLine($"Modified flag {entry.Key}");
                    game.WriteESDs.Add(entry.Key);
                }
            }

            // Misc map and params edits for blocks
            MiscMapEdits mapEdits = new MiscMapEdits(game);
            List<float> parseFloats(IEnumerable<string> strs) => strs.Select(c => float.Parse(c, CultureInfo.InvariantCulture)).ToList();
            foreach (string gate in enabledGates)
            {
                if (!gates.TryGetValue(gate, out LogicGate logicGate))
                {
                    continue;
                }
                if (logicGate.ObjAct)
                {
                    if (!getGateFlag(gate, out int flag)) throw new Exception($"Internal error: No flag for enabled logic gate {gate}");
                    if (!gateItems.TryGetValue(gate, out ItemKey key)) throw new Exception($"Internal error: logic gate {gate} must be an item to edit locks");
                    // It's not the most efficient thing to go through ObjAct n times but better to be sure the edit worked
                    int edits = 0;
                    foreach (PARAM.Row row in game.Params["ObjActParam"].Rows)
                    {
                        if ((byte)row["spQualifiedType"].Value == 1 && (int)row["spQualifiedId_new"].Value == key.ID)
                        {
                            row["spQualifiedType"].Value = (byte)2;
                            row["spQualifiedId"].Value = 4150;
                            row["spQualifiedId_new"].Value = 4150;
                            row["spQualifiedPassEventFlag"].Value = (uint)flag;
                            edits++;
                        }
                    }
                    if (edits == 0)
                    {
                        // If this is hard to find, hardcode row id
                        throw new Exception($"No locks found for {gate} in ObjActParam (spQualifiedId_new = {key.ID})");
                    }
                }
                if (logicGate.Block != null)
                {
                    foreach (string block in events.Decomment(logicGate.Block))
                    {
                        string[] parts = block.Split(' ');
                        // Format is: <map> <model> <base> <x> <y> <z> <y rot> [<x rot> <z rot>]
                        List<float> coords = parseFloats(parts.Skip(3));
                        Vector3 pos = new Vector3(coords[0], coords[1], coords[2]);
                        Vector3 rot = coords.Count == 6 ? new Vector3(coords[4], coords[3], coords[5]) : new Vector3(0, coords[3], 0);
                        MSBE.Part.Asset obj = mapEdits.AddDerivativeAsset(parts[0], parts[1], parts[2], pos, rot);
                        uint assetId = entityIdBase++;
                        obj.EntityID = assetId;
#if DEBUG
                        if (opt["alwaysblock"]) continue;
#endif
                        if (getGateFlag(gate, out int flag))
                        {
                            string eventMap = mapEdits.GetEventMap(parts[0], obj.Name);
                            if (!game.Emevds.TryGetValue(eventMap, out EMEVD emevd)) throw new Exception($"Event script {eventMap} is not usable for gate {gate}");
                            events.AddSimpleEvent(
                                emevd, miscEventBase++,
                                new[]
                                {
                                    // When flag is on, block is disabled. Don't try to enable again also for seamless coop sync reasons
                                    $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {flag})",
                                    $"ChangeAssetEnableState({assetId}, Disabled)",
                                },
                                EMEVD.Event.RestBehaviorType.Restart);
                        }
                        else if (logicGate.Cond != "none")
                        {
                            throw new Exception($"Internal error: No flag for enabled logic gate {gate} with cond {logicGate.Cond}");
                        }
                    }
                }
                // Additional edits
                if (gate == "dragonbarrow")
                {
                    if (!getGateFlag(gate, out int flag)) throw new Exception($"Internal error: No flag for enabled logic gate {gate}");
                    MSBE msb = game.EldenMaps["m60_50_39_00"];
                    MSBE.Region.MountJump jump = msb.Regions.MountJumps.FirstOrDefault();
                    MSBE.Region.MountJumpFall jumpFall = msb.Regions.MountJumpFalls.FirstOrDefault();
                    if (jump != null)
                    {
                        // Hopefully other mods don't add any more, it's hard to tell these apart
                        msb.Regions.MountJumps.Clear();
                        msb.Regions.MountJumpFalls.Clear();
                        MSB.Shape.Cylinder shape = jumpFall.Shape as MSB.Shape.Cylinder;
                        mapEdits.AddOverworldLockedMountJump(
                            "m60_50_39_00", jump.Position, (uint)flag,
                            jump.JumpHeight, shape?.Radius ?? 13, shape?.Height ?? 40, jump.Rotation);
                    }
                }
            }

            // These events are lazily added from various calls to getGateFlag
            foreach (EMEVD.Event checkEvent in gateCheckEvents.OrderBy(e => e.ID))
            {
                AddSimpleEventAndInit(game.Emevds["common"], checkEvent);
            }

            List<string> missingGates = enabledGates.Except(processedGates).Where(gate => !(gates.TryGetValue(gate, out LogicGate lg) && lg.Cond == "none")).ToList();
            if (missingGates.Count > 0)
            {
                throw new Exception($"Gates were enabled but not edited: {string.Join(", ", missingGates)}");
            }

            // Misc edits which could probably be moved into event config. Some of these do not strictly require item rando.
            // First change Thops key if item rando enabled, not really worth a setting
            PARAM.Row thopsKey = game.Params["EquipParamGoods"][8174];
            if (thopsKey != null)
            {
                thopsKey["iconId"].Value = (ushort)3005;
            }
            // Okay I guess the name too
            // This used to just append 133300 but is now a message
            messages.SetFMGEntry(game, FMGCategory.Item, "GoodsName", 8174, thopsKeyText, FMGArg.Item("GoodsName", 8109, "Academy Glintstone Key"));
            game.WriteFMGs = true;

            // Region progression/access
            // Access Royal Lendell after Maliketh, with separate logic changes in item rando
            // Fog gate rando has different edits/logic for blocking these paths so don't interfere with it
            if (opt["royalaccess"] && !opt["fog"])
            {
                // Main edit shared with fog rando is removing BatchSetEventFlags(71100, 71110, OFF) from 900
                // Note sitting at Morgott is currently required, so a grace should be present before Maliketh
                EMEVD common = game.Emevds["common"];
                game.WriteEmevds.Add("common");
                EMEVD.Event postMaliketh = common.Events.Find(e => e.ID == 900) ?? throw new Exception("Missing event 900 in common");
                {
                    OldParams pre = OldParams.Preprocess(postMaliketh);
                    EventEdits edits = new EventEdits();
                    events.RemoveMacro(edits, "BatchSetEventFlags(71100, 71110, OFF)", optional: true);
                    events.ApplyAllEdits(postMaliketh, edits);
                    pre.Postprocess();
                }
                // Also disable elevator to Capital Outskirts in Ashen state
                // Move elevator to bottom, ideally when not in Leyndell. 90005500 is default, 90005501 is restart
                events.AddSimpleEvent(common, miscEventBase++, new[]
                {
                    "IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 300)",
                    "SetEventFlag(TargetEventFlagType.EventFlag, 11000610, OFF)",
                    "SetEventFlag(TargetEventFlagType.EventFlag, 11000611, OFF)",
                }, EMEVD.Event.RestBehaviorType.Restart);
                // Sewer has fog asset but it doesn't load on save+quit, so close door 11001554 (id not otherwise used)
                EMEVD leyndell = game.Emevds["m11_00_00_00"];
                game.WriteEmevds.Add("m11_00_00_00");
                events.AddSimpleEvent(leyndell, miscEventBase++, new[]
                {
                    "IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 300)",
                    "SetEventFlag(TargetEventFlagType.EventFlag, 11008554, OFF)",
                    "SetObjactState(11001554, -1, Disabled)",
                    // For Outskirts elevator, similar to Shadow Keep event 21002515
                    "WaitFixedTimeFrames(2)",
                    "SetObjactState(11001611, -1, Disabled)",
                }, EMEVD.Event.RestBehaviorType.Restart);
                // The other exit to East Altus Divine Tower has fake elevator, no death plane, but no edit needed
                // Ashen Leyndell remains after Maliketh only, so no edits are needed there either

                // Finally, cosmetic map changes
                // Do this per-language as it's based on duplicate names
                Dictionary<string, HashSet<string>> royalNames = new();
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    if ((byte)row["areaNo"].Value == 11 && (byte)row["gridXNo"].Value == 0)
                    {
                        int textId = (int)row["textId1"].Value;
                        foreach ((string lang, FMGDictionary itemFmgs) in game.AllItemFMGs)
                        {
                            string text = itemFmgs["PlaceName"][textId];
                            Util.AddMulti(royalNames, lang, text);
                        }
                    }
                }
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    if ((byte)row["areaNo"].Value == 11 && (byte)row["gridXNo"].Value == 5)
                    {
                        int textId = (int)row["textId1"].Value;
                        bool changed = false;
                        foreach ((string lang, FMGDictionary itemFmgs) in game.AllItemFMGs)
                        {
                            string text = itemFmgs["PlaceName"][textId];
                            if (royalNames.TryGetValue(lang, out HashSet<string> names) && names.Contains(text))
                            {
                                itemFmgs["PlaceName"][textId] = messages.Get(ashenGraceText, text);
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            row["posX"].Value = (float)row["posX"].Value + 12;
                            row["posZ"].Value = (float)row["posZ"].Value - 12;
                        }
                    }
                }
            }
            if (opt["rustykey"])
            {
                // This is in several Gostoc ESDs, but only edit the one at the gate 10000700, t302001000
                // 23021000:"No, I'll use the main gate"
                // AddTalkListData(2, 23021000, -1)
                // 23021002:"I want to use the main gate"
                // AddTalkListDataIf(GetEventFlag(10009502), 2, 23021002, -1)
                ESD gostoc = game.Talk["m10_00_00_00"]["t302001000"];
                game.WriteESDs.Add("m10_00_00_00");
                // This could be limited to a specific machine with FindMachinesWithTalkData but similar amount of parsing work anyway
                int edits = 0;
                ESDEdits.ForEachCommand(gostoc, c =>
                {
                    if (c.CommandID != 19) return;
                    AST.Expr arg;
                    if (c.CommandBank == 1 && c.Arguments.Count > 1)
                    {
                        arg = AST.DisassembleExpression(c.Arguments[1]);
                    }
                    else if (c.CommandBank == 5 && c.Arguments.Count > 2)
                    {
                        arg = AST.DisassembleExpression(c.Arguments[2]);
                    }
                    else return;
                    if (arg.TryAsInt(out int msgId) && (msgId == 23021000 || msgId == 23021002))
                    {
                        edits++;
                        // TODO: Make rewriteArg work for this so it can do shards automatically and use a flag if needed
                        ItemKey key = new ItemKey(ItemType.Goods, 8010);
                        int minAmount = ann.MultiItems.TryGetValue(key, out MultiItem multiItem) ? multiItem.ReqCount : 1;
                        // f47 ComparePlayerInventoryNumber(itemType, itemId, comparisonType, amt, 0)
                        AST.Expr showExpr = AST.MakeFunction("f47", 3, 8010, (int)ESDEdits.ComparisonType.GreaterOrEqual, minAmount, 0);
                        if (c.CommandBank == 1)
                        {
                            c.CommandBank = 5;
                            c.Arguments.Insert(0, AST.AssembleExpression(showExpr));
                        }
                        else
                        {
                            AST.Expr existCond = AST.DisassembleExpression(c.Arguments[0]);
                            showExpr = AST.Binop(existCond, "&&", showExpr);
                            c.Arguments[0] = AST.AssembleExpression(showExpr);
                        }
                    }
                });
                if (edits == 0)
                {
                    throw new Exception($"Could not edit conditions to require Rusty Key to enter Stormveil");
                }
            }

            // Replace duplication boss defeat flags with soul get flags
            foreach (PARAM.Row row in game.Params["ShopLineupParam"].Rows)
            {
                if (mapping.BossDupeItems.TryGetValue(row.ID, out ItemKey soul) && mapping.ItemEventFlags.TryGetValue(soul, out int soulFlag) && soulFlag > 0)
                {
                    row["eventFlag_forRelease"].Value = soulFlag;
                }
            }

            // Finally, a pass for merchant Bell Bearings
            // This is mainly only relevant during item randomizer, as otherwise merchant contents are known.
            int merchantMsg = 28000070;
            game.WriteFMGs = true;
            messages.SetFMGEntry(game, FMGCategory.Menu, "EventTextForTalk", merchantMsg, receiveBellBearing);
            // Mapping from talk id to (item lot, item lot flag)
            Dictionary<int, (int, int)> talkIds = new Dictionary<int, (int, int)>();
            foreach (KeyValuePair<LocationScope, List<ItemLocKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                if (!ann.Slots.TryGetValue(locScope, out AnnotationData.SlotAnnotation slot)) continue;
                if (!slot.TagList.Contains("merchantgift")) continue;
                foreach (ItemLocKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.GetItemLoc(itemLocKey);
                    if (location.Scope.Type != ScopeType.Event) continue;
                    int eventFlag = location.Scope.EventID;
                    if (eventFlag <= 0) continue;
                    foreach (Location locKey in location.Keys)
                    {
                        if (locKey.Type != LocationType.Lot || locKey.Subtype != "map") continue;
                        int lotId = locKey.BaseID;
                        foreach (Entity entityId in locKey.Entities)
                        {
                            if (entityId.TalkID > 0)
                            {
                                talkIds[entityId.TalkID] = (lotId, eventFlag);
                                if (entityId.NameID > 0)
                                {
                                    mapping.MerchantGiftFlags[entityId.NameID] = eventFlag;
                                }
                            }
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                bool modified = false;
                foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                {
                    ESD esd = esdEntry.Value;
                    int esdId = int.Parse(esdEntry.Key.Substring(1));
                    if (!talkIds.TryGetValue(esdId, out var talkInfo)) continue;
                    (int lotId, int eventFlag) = talkInfo;
                    List<long> purchaseMachines = ESDEdits.FindMachinesWithTalkData(esd, 20000010);
                    ESDEdits.CustomTalkData merchantData = new ESDEdits.CustomTalkData
                    {
                        Msg = merchantMsg,
                        ConsistentID = 68,
                        Condition = new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f15", eventFlag), Rhs = AST.MakeVal(0) },
                        LeaveMsg = 20000009,
                    };
                    foreach (long machineId in purchaseMachines)
                    {
                        Dictionary<long, ESD.State> machine = esd.StateGroups[machineId];
                        long resultStateId = -1;
                        try
                        {
                            ESDEdits.ModifyCustomTalkEntry(machine, merchantData, true, false, out resultStateId);
                        }
                        catch (InvalidOperationException) { }
                        if (resultStateId < 0) continue;
                        ESD.State resultState = machine[resultStateId];
                        // c1_104 AwardItemLot
                        resultState.EntryCommands.Add(AST.MakeCommand(1, 104, lotId));
                        // not f25 IsMenuOpen(63) and f102 GetCurrentStateElapsedFrames() >= 1
                        resultState.Conditions[0].Evaluator = AST.AssembleExpression(new AST.BinaryExpr
                        {
                            Op = "&&",
                            Lhs = new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f25", 63), Rhs = AST.MakeVal(0) },
                            Rhs = new AST.BinaryExpr { Op = "#>=", Lhs = AST.MakeFunction("f102"), Rhs = AST.MakeVal(1) },
                        });
                        modified = true;
                    }
                }
                if (modified)
                {
                    if (debugEsd) Console.WriteLine($"Modified merchant {entry.Key}");
                    game.WriteESDs.Add(entry.Key);
                }
            }

            // This could also be applied to enemy randomizer, but it affects logic
            if (opt["allcraft"])
            {
                foreach (PARAM.Row row in game.Params["ShopLineupParam_Recipe"].Rows)
                {
                    row["eventFlag_forRelease"].Value = (uint)0;
                }
            }
            // End Elden Ring edits
        }
    }
}
