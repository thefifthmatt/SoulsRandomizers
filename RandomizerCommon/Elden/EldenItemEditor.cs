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
using static RandomizerCommon.ExternalItemPreset;
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
        private readonly ExternalItemPreset externalPreset;
        private readonly ExternalLocationData externalData;
        private readonly Events events;
        private readonly ItemEventConfig eventConfig;
        private readonly Messages messages;

        public EldenItemEditor(
            GameData game,
            LocationData data,
            AnnotationData ann,
            ExternalItemPreset externalPreset,
            ExternalLocationData externalData,
            Events events,
            ItemEventConfig eventConfig,
            Messages messages)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.externalPreset = externalPreset;
            this.externalData = externalData;
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

        // (event flag, location) which should be mapped in param data
        private SortedSet<(uint, long)> trackedLotLocations = new();
        private SortedSet<(uint, long)> trackedShopLocations = new();
        // By item id, to include quantity changes
        private Dictionary<long, ItemKey> fakeLocalItems = new();
        private HashSet<ItemKey> copiedItems = new();
        private Dictionary<ItemType, int> fakeIds = new()
        {
            [ItemType.Weapon] = 81_000_000,
            [ItemType.Protector] = 8_100_000,
            [ItemType.Accessory] = 8_100_000,
            [ItemType.Goods] = 8_100_000,
            [ItemType.Gem] = 8_100_000,
            [ItemType.Custom] = 8_100_000,
        };

        private int GetNewFakeID(ItemKey item)
        {
            if (!fakeIds.TryGetValue(item.Type, out int baseId))
            {
                throw new Exception($"Rewrite for {item} is not implemented");
            }
            int newId;
            if (item.Type == ItemType.Weapon)
            {
                newId = baseId + (item.ID % 10000);
                baseId += 10000;
            }
            else if (item.Type == ItemType.Protector)
            {
                newId = baseId + (item.ID % 1000);
                baseId += 1000;
            }
            else
            {
                newId = baseId++;
            }
            fakeIds[item.Type] = baseId;
            return newId;
        }

        public override void ExternalItemOverride(ItemLocation source, Location target, ItemRow row)
        {
            if (externalPreset == null)
            {
                return;
            }
            // Use the original location data
            Location loc = source.Locs.Find(l => l.Type == LocationType.External);
            if (loc == null)
            {
                return;
            }
            long locationId = loc.ExternalID;
            long itemId = externalPreset.FiniteItemMapping[locationId];
            ItemKey fakeItem = null;
            if (externalPreset.LocalItems.TryGetValue(itemId, out LocalItem localItem) && !externalPreset.ExternalItems.ContainsKey(locationId))
            {
                if (!source.Item.Equals(localItem.Key))
                {
                    throw new Exception($"Mismatch for location {locationId}<-{itemId}, assigned {source.Item} but expected {localItem.Key}");
                }
                // All items are 'realistic' items because both item popups and shops use item metadata.
                // This indirection is only for safety if the randomizer is enabled without the dll.
                // The items should be made unusable, though.
                ItemKey baseItem = game.NormalizeWeapon(source.Item);
                if (!fakeLocalItems.TryGetValue(itemId, out fakeItem))
                {
                    int newId = GetNewFakeID(baseItem);
                    copiedItems.Add(baseItem);
                    if (baseItem.Type == ItemType.Custom)
                    {
                        copiedItems.Add(game.GetBaseWeapon(baseItem));
                    }
                    fakeLocalItems[itemId] = fakeItem = new ItemKey(baseItem.Type, newId);
                }
            }
            // These error messages are bad
            void validate(ItemKey rowKey, int eventFlag)
            {
                if (rowKey == null)
                {
                    throw new Exception($"No item in lot");
                }
                if (!source.Item.Equals(rowKey))
                {
                    throw new Exception($"Item [{game.Name(rowKey)}] found in lot where only [{source.Item}] was expected");
                }
                if (eventFlag <= 0)
                {
                    throw new Exception($"Item [{source.Item}] assigned to location with no event flag");
                }
            }
            if (row is LotCells lotCells)
            {
                if (fakeItem != null)
                {
                    ItemKey rowKey = lotCells[1];
                    validate(rowKey, lotCells.EventFlag);
                    lotCells[1] = fakeItem;
                    lotCells.SetQuantity(1, 1);
                }
                for (int i = 2; i <= 8; i++)
                {
                    if (lotCells[i] != null)
                    {
                        throw new Exception($"Lot cell {i} filled");
                    }
                }
                // Add extra data, for now not using API
                (int embed1, uint embed2) = EmbedLong(locationId);
                lotCells.Cells["lotItemId08"] = embed1;
                lotCells.Cells["getItemFlagId08"] = embed2;
                trackedLotLocations.Add(((uint)lotCells.EventFlag, locationId));
            }
            else if (row is ShopCells shopCells)
            {
                if (fakeItem != null)
                {
                    ItemKey rowKey = shopCells.Item;
                    validate(rowKey, shopCells.EventFlag);
                    if (shopCells.Quantity <= 0)
                    {
                        throw new Exception($"Shop cell invalid quantity");
                    }
                    shopCells.Item = fakeItem;
                    shopCells.Value *= shopCells.Quantity;
                    shopCells.Quantity = 1;
                }
                trackedShopLocations.Add(((uint)shopCells.EventFlag, locationId));
            }
        }

        // Turn given id using fewer than 56 into (item id, event flag) pair
        private (int, uint) EmbedLong(long id)
        {
            if (id < 0 || id >= (1 << 56)) throw new Exception($"Location id {id} out of range and not supported");
            // For the event flag, make sure it's an invalid range just in case. Investigate _ResetCumulativeNum
            return ((int)id, (uint)(id >> 32) | 0xE000_0000);
        }

        public override void EditLocations(RandomizerOptions opt, ItemFlagMapping mapping)
        {
            // TODO this could be generic for all gestures
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

            List<EMEVD.Event> flagSyncEvents = new();
            uint entityIdBase = 1324030000;
            int extSyncEventBase = 1324036000;
            int gateEventBase = 1324037000;
            int miscEventBase = 1324038000;
            HashSet<ItemType> scriptCheckableTypes = new() { ItemType.Weapon, ItemType.Protector, ItemType.Accessory, ItemType.Goods };
            bool tryScriptCheck(ItemKey key, out int itemType)
            {
                // Dependency on ItemType value
                itemType = (int)key.Type;
                return scriptCheckableTypes.Contains(key.Type);
            };

            Dictionary<ItemType, List<string>> copyFmgNames = new()
            {
                // DLC variants are automatically used where necessary
                [ItemType.Weapon] = new() { "WeaponName", "WeaponInfo", "WeaponCaption" },
                [ItemType.Protector] = new() { "ProtectorName", "ProtectorInfo", "ProtectorCaption" },
                [ItemType.Accessory] = new() { "AccessoryName", "AccessoryInfo", "AccessoryCaption" },
                [ItemType.Goods] = new() { "GoodsName", "GoodsInfo", "GoodsCaption" },
                [ItemType.Gem] = new() { "GemName", "GemInfo", "GemCaption" },
            };
            void copyFmgs(ItemKey from, ItemKey to, int quantity = 1, string overrideName = null)
            {
                if (!copyFmgNames.TryGetValue(from.Type, out List<string> fmgNames))
                {
                    return;
                }
                foreach (FMGDictionary itemFmgs in game.AllItemFMGs.Values)
                {
                    foreach (string fmgName in fmgNames)
                    {
                        string entry = itemFmgs[fmgName][from.ID];
                        if (entry != null)
                        {
                            if ((overrideName != null || quantity > 1) && fmgName == fmgNames[0])
                            {
                                // More recognizable to put (x{quantity}) in front but make it consistent with external items
                                entry = overrideName ?? $"{entry} x{quantity}";
                            }
                            itemFmgs[fmgName][to.ID] = entry;
                        }
                    }
                }
            }

            // From Dectus Medallion (Left)
            ItemKey baseKeyItem = new ItemKey(ItemType.Goods, 8105);
            PARAM.Row baseKeyItemRow = game.Item(baseKeyItem);
            foreach (NewItemAnnotation newItem in ann.NewItems.Values)
            {
                // TODO: sortId, correct icon
                int id = newItem.Key.ID;
                PARAM.Row row = GameEditor.AddRow(game.ItemParam(baseKeyItem), id, baseKeyItemRow);
                row["rarity"].Value = (byte)2;
                if (newItem.Switch == "traps")
                {
                    // This should probably be done as tag, combined with shard-editing
                    row["isConsume"].Value = (byte)1;
                    row["maxNum"].Value = (short)999;
                    row["maxRepositoryNum"].Value = (short)999;
                }
                // Hardcoded routine for now for Stonesword Master Keys
                // TODO: Can be localized? Requires config support
                if (newItem.Switch != null && newItem.Switch.StartsWith("stonesword"))
                {
                    row["iconId"].Value = 228;
                    string areaInfo = null;
                    if (newItem.Logic != null)
                    {
                        IEnumerable<string> areas = newItem.Logic
                            .Select(l => l.BlockArea != null && ann.Areas.TryGetValue(l.BlockArea, out AreaAnnotation area) ? (area.FullText ?? area.Text) : null)
                            .Where(name => name != null)
                            .Distinct();
                        areaInfo = $"\n\nGrants access to the following areas:\n{string.Join("\n", areas.Select(a => $"- {a}"))}";
                    }
                    foreach (FMGDictionary fmgs in game.AllItemFMGs.Values)
                    {
                        fmgs["GoodsName"][id] = newItem.Name;
                        fmgs["GoodsInfo"][id] = "Breaks many imp statue seals";
                        fmgs["GoodsCaption"][id] = $"Breaks many imp statue seals.{areaInfo}";
                    }
                }
                else
                {
                    row["iconId"].Value = 448;
                    foreach (FMGDictionary fmgs in game.AllItemFMGs.Values)
                    {
                        fmgs["GoodsName"][id] = newItem.Name;
                        fmgs["GoodsInfo"][id] = newItem.Desc ?? "";
                        fmgs["GoodsCaption"][id] = newItem.Desc ?? "";
                    }
                }
            }

            if (externalPreset != null)
            {
                // TODO: Copy rarity from other souls games?
                Dictionary<ExternalRarity, byte> rarityMapping = new()
                {
                    [ExternalRarity.Common] = 1,
                    [ExternalRarity.Rare] = 2,
                    [ExternalRarity.Legendary] = 3,
                };
                foreach ((long locationId, ItemKey itemKey) in externalData.ExternalItemKeys)
                {
                    // Maybe ItemKey should be in the preset as state added later? Stateful but easier to handle than multiple mappings
                    if (externalPreset.ExternalItems.TryGetValue(locationId, out ExternalItem extItem))
                    {
                        int id = itemKey.ID;
                        ItemKey paramKey = extItem.LocalKey == null ? null : game.GetBaseWeapon(extItem.LocalKey);
                        PARAM.Row localRow = paramKey == null ? null : game.Item(paramKey);
                        PARAM.Row row = GameEditor.AddRow(game.ItemParam(baseKeyItem), id, baseKeyItemRow);
                        row["iconId"].Value = 90;
                        row["isDiscard"].Value = (byte)1;
                        byte rarity;
                        if (localRow != null)
                        {
                            // Items from Elden Ring keep rarity. This could be done for other souls games in theory, and more could be copied.
                            rarity = (byte)row["rarity"].Value;
                        }
                        else if (!rarityMapping.TryGetValue(extItem.Rarity, out rarity))
                        {
                            if (extItem.Rarity == ExternalRarity.Trap)
                            {
                                uint bucket = Util.XxStringHash(locationId.ToString()) % 100;
                                if (bucket < 45) rarity = 1;
                                else if (bucket < 90) rarity = 2;
                                else rarity = 3;
                            }
                            // Otherwise rarity 0 is fine
                        }
                        row["rarity"].Value = rarity;
                        // TODO: What starting index to use?
                        row["sortId"].Value = 2000000 + extItem.SortOffset;
                        if (paramKey != null)
                        {
                            // Also have local description for other Elden Ring instances I guess. This could be skipped
                            copyFmgs(paramKey, itemKey, overrideName: extItem.Name);
                        }
                        else
                        {
                            // TODO: Localize somehow
                            foreach (FMGDictionary fmgs in game.AllItemFMGs.Values)
                            {
                                fmgs["GoodsName"][id] = extItem.Name;
                                fmgs["GoodsInfo"][id] = extItem.Desc;
                                fmgs["GoodsCaption"][id] = extItem.Desc;
                            }
                        }
                    }
                }
                // For Elden Ring, potentially a *lot* of items are getting added so prefer to make lookup not quadratic
                // Some of this could be shared between games
                Dictionary<ItemKey, PARAM.Row> itemCache = new();
                foreach (ItemType copyType in fakeIds.Keys)
                {
                    foreach (PARAM.Row row in game.ItemParam(copyType).Rows)
                    {
                        ItemKey key = new ItemKey(copyType, row.ID);
                        if (copiedItems.Contains(key))
                        {
                            itemCache.TryAdd(key, row);
                        }
                    }
                }
                Dictionary<int, PARAM.Row> magicDict = GameEditor.ParamToDictionary(game.Params["Magic"]);
                foreach ((long itemId, ItemKey targetItem) in fakeLocalItems)
                {
                    LocalItem localItem = externalPreset.LocalItems[itemId];
                    ItemKey baseItem = game.NormalizeWeapon(localItem.Key);
                    if (baseItem.Type != targetItem.Type || !itemCache.TryGetValue(baseItem, out PARAM.Row baseRow))
                    {
                        throw new Exception($"Internal error: did not track {baseItem}->{targetItem} from {itemId}");
                    }
                    // This is still quadratic, however
                    PARAM.Row newRow = GameEditor.AddRow(game.ItemParam(targetItem), targetItem.ID, baseRow);
                    copyFmgs(baseItem, targetItem, localItem.Quantity);
                    // Duplicate aux rows for descriptions and other metadata
                    // TODO: These fake items are too real. Make them not actually work.
                    if (baseItem.Type == ItemType.Goods && magicDict.TryGetValue(baseItem.ID, out PARAM.Row magicRow))
                    {
                        GameEditor.AddRow(game.Params["Magic"], targetItem.ID, magicRow);
                    }
                    if (baseItem.Type == ItemType.Custom)
                    {
                        // This is untested as currently gem-attached weapons are not added as sources
                        ItemKey baseWeapon = game.GetBaseWeapon(baseItem);
                        if (baseWeapon.Type != ItemType.Weapon || !itemCache.TryGetValue(baseWeapon, out PARAM.Row baseWepRow))
                        {
                            throw new Exception($"Internal error: did not track {baseItem} {baseWeapon} from {itemId}");
                        }
                        ItemKey newWeapon = new ItemKey(ItemType.Weapon, GetNewFakeID(baseWeapon));
                        GameEditor.AddRow(game.ItemParam(targetItem), targetItem.ID, baseWepRow);
                        newRow["baseWepId"].Value = newWeapon.ID;
                        copyFmgs(baseWeapon, newWeapon);
                    }
                }
#if DEBUG
                bool debugMapping = false;
                if (debugMapping)
                {
                    SortedSet<(uint, long)> trackedFlagLocations = new(trackedLotLocations.Union(trackedShopLocations));
                    Dictionary<uint, List<long>> multiFlagLocs = trackedFlagLocations
                        .GroupBy(e => e.Item1)
                        .Where(g => g.Count() > 1)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.Item2).ToList());
                    Console.WriteLine($"{multiFlagLocs.Count} overloaded flags (expected): {string.Join(" ", multiFlagLocs.Keys)}");
                    Dictionary<long, List<uint>> multiLocFlags = trackedFlagLocations
                        .GroupBy(e => e.Item2)
                        .Where(g => g.Count() > 1)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.Item1).ToList());
                    Console.WriteLine($"{multiLocFlags.Count} overloaded locs (should be shops only): {string.Join(" ", multiLocFlags.Select(e => $"[{e.Key}->{string.Join(",", e.Value)}]"))}");
                }
#endif
                // Shops aren't big enough to fit a long in Elden Ring so just make fake lots instead
                SortedSet<(uint, long)> untrackedLocations = new(trackedShopLocations.Except(trackedLotLocations));
                int extraRowId = 1_810_000_001;
                foreach ((uint eventFlag, long location) in untrackedLocations)
                {
                    PARAM.Row row = game.AddRow("ItemLotParam_enemy", extraRowId);
                    extraRowId += 2;
                    (int embed1, uint embed2) = EmbedLong(location);
                    row["getItemFlagId"].Value = eventFlag;
                    row["lotItemId08"].Value = embed1;
                    row["getItemFlagId08"].Value = embed2;
                }

                // It should be safe to populate the entire mapping. Doing this eagerly is probably more stable
                int extSyncFlagBase = GameData.EldenRingBase + 8000;
                // Bounded by size of static config so this shouldn't happen from *only* getting unlucky. If it gets close to the limit then reconsider
                if (mapping.TrackedFlagItems.Count > 1000 || eventConfig.StableFlagList == null)
                {
                    throw new Exception($"Cannot allocate flags external tracked items ({mapping.TrackedFlagItems.Count} items, {eventConfig.StableFlagList?.Count} mapped flags)");
                }
                Dictionary<int, int> stableFlagIndex = eventConfig.StableFlagList.Select((f, i) => (f, i)).ToDictionary(e => e.Item1, e => e.Item2);
                foreach ((int flag, ItemKey item) in mapping.TrackedFlagItems.OrderBy(e => e.Key))
                {
                    if (!stableFlagIndex.TryGetValue(flag, out int index))
                    {
                        throw new Exception($"Internal error: Unknown tracked flag {flag}");
                    }
                    if (item == null || (mapping.ItemEventFlags.TryGetValue(item, out int existFlag) && existFlag > 0))
                    {
                        continue;
                    }
                    // Script checks can't handle Ashes of War at this point, nor custom weapons, nor weapons which are pre-upgraded. Maybe client should do this
                    // Current behavior is to use original location as fallback
                    if (tryScriptCheck(item, out int itemType))
                    {
                        // Some overlap with gate item checks below, but make this 'sticky' because items can be consumed or dropped
                        // It will very likely cause Seamless Co-op sync issues if it makes items unacquirable
                        int targetFlag = extSyncFlagBase + index;
                        EMEVD.Event newEvent = events.GetSimpleEvent(
                            extSyncEventBase + index,
                            new[]
                            {
                                $"IfPlayerHasdoesntHaveItem(MAIN, {itemType}, {item.ID}, OwnershipState.Owns)",
                                $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, ON)",
                            },
                            EMEVD.Event.RestBehaviorType.Restart);
                        flagSyncEvents.Add(newEvent);
                        // As an alternative to modifying these, could use a separate editor-local mapping
                        mapping.ItemEventFlags[item] = targetFlag;
                        if (item.Equals(mapping.GestureItem) && mapping.GestureFlag <= 0)
                        {
                            mapping.GestureFlag = targetFlag;
                        }
                    }
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

            foreach (NewItemAnnotation newItem in ann.NewItems.Values)
            {
                // Everything in the ann dictionary is to be enabled in-game. TODO add any extra information if needed
                itemGates[newItem.Key] = newItem.ConfigName;
            }

            Dictionary<string, ItemKey> gateItems = itemGates.ToDictionary(e => e.Value, e => e.Key);
            HashSet<string> enabledGates = new(shardItems.Keys.Concat(blockGates).Concat(ann.NewItems.Keys));

            // Lazily initialized flags, to create events as needed
            Dictionary<string, int> gateFlags = new();
            int gateBase = GameData.EldenRingBase + 200;
            // Check enabledGates before calling
            int getGateFlag(string gate)
            {
                if (gateFlags.TryGetValue(gate, out int existFlag))
                {
                    return existFlag;
                }
                EMEVD.Event newEvent = null;
                int targetFlag;
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
                    else
                    {
                        throw new Exception($"Internal error: Cannot create flag for item {gate} without config defined");
                    }
                }
                else if (ann.NewItems.TryGetValue(gate, out NewItemAnnotation newItem))
                {
                    if (logicFlagIndices.TryGetValue(gate, out int index) && tryScriptCheck(newItem.Key, out int itemType))
                    {
                        targetFlag = gateBase + index;
                        newEvent = events.GetSimpleEvent(
                            gateEventBase + index,
                            new[]
                            {
                                $"IfPlayerHasdoesntHaveItem(AND_10, {itemType}, {newItem.Key.ID}, OwnershipState.Owns)",
                                $"SkipIfConditionGroupStateUncompiled(2, PASS, AND_10)",
                                $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, OFF)",
                                $"IfPlayerHasdoesntHaveItem(MAIN, {itemType}, {newItem.Key.ID}, OwnershipState.Owns)",
                                $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, ON)",
                            },
                            EMEVD.Event.RestBehaviorType.Restart);
                    }
                    // TODO: Replace with function for Archipelago etc
                    else if (!mapping.ItemEventFlags.TryGetValue(newItem.Key, out targetFlag))
                    {
                        throw new Exception($"Internal error: Item {gate} not tracked");
                    }
                }
                else if (blockGates.Contains(gate))
                {
                    if (!gates.TryGetValue(gate, out LogicGate logicGate) || logicGate.Cond == null || logicGate.Cond == "none")
                    {
                        // Just require explicitly set for now
                        throw new Exception($"Internal error: no flag condition for gate {gate}");
                    }
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
                            // (unless this can use BlockExpr instead?)
                            int gateOrItemFlag(string item)
                            {
                                if (enabledGates.Contains(item))
                                {
                                    return getGateFlag(item);
                                }
                                else if (ann.Items.TryGetValue(item, out ItemKey key) && mapping.ItemEventFlags.TryGetValue(key, out int getFlag) && getFlag > 0)
                                {
                                    return getFlag;
                                }
                                else throw new Exception($"Somehow can't find requirements for {item} for custom Altus access (flag {mapping.ItemEventFlags.GetValueOrDefault(key, -1)})");
                            }
                            ;
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
                        else if (gate == "dragonbarrow" && logicFlagIndices.TryGetValue(gate, out index))
                        {
                            targetFlag = gateBase + index;
                            // Version of event 11103770 that runs everywhere. Also include Ashen I guess. In both cases, exclude Divine Bridge
                            newEvent = events.GetSimpleEvent(
                                gateEventBase + index,
                                new[]
                                {
                                    "IfBatchEventFlags(OR_01, LogicalOperationType.NotAllOFF, TargetEventFlagType.EventFlag, 71100, 71108)",
                                    "IfBatchEventFlags(OR_01, LogicalOperationType.NotAllOFF, TargetEventFlagType.EventFlag, 71120, 71124)",
                                    "IfConditionGroup(MAIN, PASS, OR_01)",
                                    $"SetEventFlag(TargetEventFlagType.EventFlag, {targetFlag}, ON)",
                                },
                                EMEVD.Event.RestBehaviorType.Restart);
                        }
                    }
                    if (targetFlag == 0)
                    {
                        throw new Exception($"Internal error: no condition found for gate {gate} with type {logicGate.Cond}");
                    }
                }
                else throw new Exception($"Internal error: Attempting to use disabled or unknown gate {gate}");
                if (newEvent != null)
                {
                    flagSyncEvents.Add(newEvent);
                }
                processedGates.Add(gate);
                gateFlags[gate] = targetFlag;
                return targetFlag;
            }
            // Condition for item use, if tracked. This is for all-or-nothing cases and does not support non-shard multi-items
            bool getItemUseFlag(ItemKey key, out int useFlag)
            {
                if (itemGates.TryGetValue(key, out string shardGate))
                {
                    useFlag = getGateFlag(shardGate);
                    return true;
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
#if DEBUG
                bool debugUntrackedItems = false;
                if (debugUntrackedItems && type.StartsWith("item"))
                {
                    getFlagAcquire(flag, debugId, out ItemKey supItem, out int itemFlag);
                    Console.WriteLine($"Tracked flag not found for {type} {debugId}: {flag} -> {itemFlag} (item: {(supItem == null ? "none" : game.Name(supItem))})");
                }
#endif
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
            Dictionary<uint, bool> inplaceEdited = new();
            Dictionary<uint, EMEVD.Event> commonEvents = game.Emevds["common_func"].Events.ToDictionary(e => (uint)e.ID, e => e);
            HashSet<string> specialEdits = new HashSet<string>
            {
                "ashdupe", "singleton", "removecheck", "runearg", "forge",
                "volcanoreq", "leyndell",
            };

            // Area gates. This could probably handle cases currently handled by custom conditions, mainly dependent on extracting it from logic.
            // Item logic names become item checks (no shards currently). This should handle boss defeats and GR requirements as well
            // TODO: Verify all areas which should be edited are edited (presence of BlockExpr sufficient?)
            void addAreaGateInstructions(Expr blockExpr, int startCond, List<string> cmds, List<int> usedConds = null)
            {
                int andCond, orCond;
                if (startCond > 0)
                {
                    andCond = startCond + 1;
                    orCond = -startCond;
                }
                else
                {
                    orCond = startCond - 1;
                    andCond = -startCond;
                }
                void recAreaGate(Expr expr, int cond)
                {
                    if (expr.GetName(out string name))
                    {
                        // Like item gate flags, require that they can be waited on, so no direct item quantity checks for now
                        if (!ann.Items.TryGetValue(name, out ItemKey item) || !tryScriptCheck(item, out int itemType))
                        {
                            throw new Exception($"Internal error: ineligible {name} {item} in {blockExpr}");
                        }
                        processedGates.Add(name);
                        cmds.Add($"IfPlayerHasdoesntHaveItem({cond}, {itemType}, {item.ID}, OwnershipState.Owns)");
                    }
                    // Try to use existing cond
                    else if (expr.GetChildren(out List<Expr> children, out bool isAnd))
                    {
                        int subcond = cond;
                        if ((subcond > 0) != isAnd)
                        {
                            // More rigorous would be a list of all free registers
                            subcond = isAnd ? andCond++ : orCond--;
                            if (Math.Abs(subcond) > 15)
                            {
                                throw new Exception($"Not enough condition groups to represent logic {blockExpr} from {startCond}");
                            }
                            usedConds.Add(subcond);
                        }
                        children.ForEach(c => recAreaGate(c, subcond));
                        if (subcond != cond)
                        {
                            cmds.Add($"IfConditionGroup({cond}, PASS, {subcond})");
                        }
                    }
                    else throw new Exception($"Internal error: bad {blockExpr}");
                }
                usedConds.Add(startCond);
                recAreaGate(blockExpr, startCond);
            }

            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                if (!opt["dlc"] && game.IsEldenDlcMap(entry.Key))
                {
                    // Needed as there's some DLC MSB-editing below
                    continue;
                }
                EMEVD emevd = entry.Value;
                Dictionary<uint, EMEVD.Event> fileEvents = entry.Value.Events.ToDictionary(e => (uint)e.ID, e => e);
                // TODO: Genericize enemy rando system for event passes (or combine them haha)
                List<EMEVD.Event> newEvents = new();
                List<EMEVD.Instruction> newInits = new();
                foreach (EMEVD.Event e in emevd.Events)
                {
                    OldParams initPre = OldParams.Preprocess(e);
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr originalInit = events.Parse(e.Instructions[i], initPre);
                        if (!originalInit.Init) continue;
                        int offset = originalInit.Offset;
                        uint callee = originalInit.Callee;
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
                        void registerEventEdit(bool inplace)
                        {
                            if (inplaceEdited.TryGetValue(callee, out bool existInplace) && inplace != existInplace)
                            {
                                throw new Exception($"Internal error: inplace and non-inplace edit both performed for {callee}");
                            }
                            inplaceEdited[callee] = inplace;
                        }
                        List<(EMEVD.Event, ItemTemplate, List<TemplateFilter>)> mainPasses = new();
                        foreach (ItemTemplate t in ev.Template ?? new())
                        {
                            if (t.Type == "default")
                            {
                                continue;
                            }
                            List<TemplateFilter> selected = new();
                            if (t.Filter != null)
                            {
                                foreach (TemplateFilter filter in t.Filter)
                                {
                                    bool eligible = true;
                                    if (filter.Gate != null && !enabledGates.Contains(filter.Gate))
                                    {
                                        eligible = false;
                                    }
                                    if (filter.Gates != null && !filter.Gates.Split(' ').Any(enabledGates.Contains))
                                    {
                                        eligible = false;
                                    }
                                    if (filter.BlockArea != null && !ann.GetAreaBlock(filter.BlockArea, out _))
                                    {
                                        eligible = false;
                                    }
                                    if (filter.Content == "dlc" && !opt["dlc"])
                                    {
                                        eligible = false;
                                    }
                                    if (filter.Args != null)
                                    {
                                        foreach ((string argName, long val) in filter.Args)
                                        {
                                            if (!TryFullArgSpec(argName, out int pos))
                                            {
                                                throw new Exception($"Internal error: Bad {argName}");
                                            }
                                            int arg = (int)originalInit[offset + pos];
                                            if (arg != (int)val)
                                            {
                                                eligible = false;
                                            }
                                        }
                                    }
                                    if (eligible)
                                    {
                                        selected.Add(filter);
                                    }
                                }
                                if (selected.Count == 0)
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
                                mainPasses.Add((null, t, selected));
                            }
                            else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent) || commonEvents.TryGetValue(callee, out theEvent))
                            {
                                if (t.Type == "copyloc")
                                {
                                    mainPasses.Add((events.CopyEvent(theEvent, miscEventBase++), t, selected));
                                    registerEventEdit(inplace: false);
                                }
                                else
                                {
                                    if (completedTemplates.Add((t, 0)))
                                    {
                                        mainPasses.Add((theEvent, t, selected));
                                    }
                                    registerEventEdit(inplace: true);
                                }
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
                                int argFlag = (int)originalInit[offset + pos];
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
                            if (!t.Type.Contains("arg"))
                            {
                                registerEventEdit(inplace: true);
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
                        bool copyRemoved = false;
                        if (inplaceEdited.TryGetValue(callee, out bool inplace) && !inplace)
                        {
                            e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                            copyRemoved = true;
                        }

                        // Passes
                        foreach ((EMEVD.Event e2, ItemTemplate t, List<TemplateFilter> filters) in mainPasses)
                        {
                            string filterGate()
                            {
                                if (filters.Count != 1) throw new Exception($"Internal error: Cond flag required in {callee} but {filters.Count} logic gates defined");
                                string gate = filters[0].Gate;
                                if (gate == null) throw new Exception($"Internal error: Cond flag required in {callee} but no logic gate defined");
                                return gate;
                            }
                            int filterGateFlag() => getGateFlag(filterGate());
                            Instr newInit = null;
                            void editInit(Action<Instr> edit)
                            {
                                newInit ??= events.Parse(events.CopyInstruction(originalInit.Val), initPre);
                                edit(newInit);
                            }
                            if (e2 == null)
                            {
                                if (t.SetArg != null)
                                {
                                    string[] parts = t.SetArg.Split(' ');
                                    if (parts.Length != 2 || !TryArgSpec(parts[0], out int pos)) throw new Exception($"Internal error: Bad {t.SetArg} in config");
                                    // This happens due to imprecise callee resolution and duplicate map ids
                                    if (pos > originalInit.Count) continue;
                                    int flag = parts[1] == "gate" ? filterGateFlag() : int.Parse(parts[1]);
                                    editInit(init => init[offset + pos] = flag);
                                }
                                if (t.ItemArg != null)
                                {
                                    // TODO: Replace this copyloc alongside reading args from ItemUse per-init
                                    foreach (string part in t.ItemArg.Split(' '))
                                    {
                                        if (!TryArgSpec(part, out int pos)) throw new Exception($"Internal error: Bad {t.ItemArg} in config");
                                        if (pos > originalInit.Count) throw new Exception($"Too big: {entry.Key} {callee}, pos {pos} in ({originalInit})");
                                        int itemId = (int)originalInit[offset + pos];
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
                                        editInit(init => init[offset + pos] = useFlag);
                                    }
                                }
                                if (newInit != null)
                                {
                                    newInit.Save(initPre);
                                    e.Instructions[i] = newInit.Val;
                                }
                                game.WriteEmevds.Add(entry.Key);
                                continue;
                            }
                            EventEdits edits = new EventEdits();
                            List<int> addedConds = new();
                            OldParams pre = OldParams.Preprocess(e2);
                            if (t.Edits != null)
                            {
                                foreach (DynamicEdit edit in t.Edits)
                                {
                                    List<string> newCmds = edit.BeforeCmds?.ToList() ?? new();
                                    if (edit.FilterCond != 0)
                                    {
                                        if (filters.Count == 0) throw new Exception($"Internal error: no condition defined for editing gate in {callee}");
                                        // To have other combinations, integrate that into BlockExpr (which could also be per-gate)
                                        if (filters.Count > 1 && edit.FilterCond < 0) throw new Exception($"Internal error: {edit.FilterCond} cannot have multiple conditions in {callee}");
                                        // BlockArea helps ensure consistency with logic but it might be a lot simpler if everything is a gate
                                        foreach (TemplateFilter filter in filters)
                                        {
                                            if (filter.BlockArea != null)
                                            {
                                                if (!ann.GetAreaBlock(filter.BlockArea, out Expr blockExpr))
                                                {
                                                    throw new Exception($"Internal(?) error: area {filter.BlockArea} is is blocked but is missing or has no associated block condition");
                                                }
                                                addAreaGateInstructions(blockExpr, edit.FilterCond, newCmds, addedConds);
                                            }
                                            else if (filter.Gate != null)
                                            {
                                                int flag = getGateFlag(filter.Gate);
                                                newCmds.Add($"IfEventFlag({edit.FilterCond}, ON, TargetEventFlagType.EventFlag, {flag})");
                                                addedConds.Add(edit.FilterCond);
                                            }
                                            else throw new Exception($"Internal error: filter for {callee} has no usable condition defined");
                                        }
                                    }
                                    // Giving items is only supported for new items given a location source in AddSpecialItems
                                    if (edit.ItemLocCond != 0)
                                    {
                                        string gate = filterGate();
                                        if (!ann.NewItems.TryGetValue(gate, out NewItemAnnotation newItem) || newItem.NewLocation == null)
                                        {
                                            throw new Exception($"Internal error: new item drops not supported for {gate}");
                                        }
                                        int locFlag = (int)newItem.NewLocation.Flag;
                                        if (mapping.RewrittenFlags.TryGetValue(locFlag, out int newFlag))
                                        {
                                            locFlag = newFlag;
                                        }
                                        newCmds.Add($"IfEventFlag({edit.ItemLocCond}, ON, TargetEventFlagType.EventFlag, {locFlag})");
                                        addedConds.Add(edit.ItemLocCond);
                                    }
                                    if (edit.GiveItem != null)
                                    {
                                        // Currently only supported for new items which have a standardized location source in AddSpecialItems
                                        string gate = edit.GiveItem == "gate" ? filterGate() : edit.GiveItem;
                                        if (!ann.NewItems.TryGetValue(gate, out NewItemAnnotation newItem) || newItem.NewLocation == null || newItem.NewLocation.ItemLot <= 0)
                                        {
                                            throw new Exception($"Internal error: new item drops not supported for {gate}");
                                        }
                                        newCmds.Add($"AwardItemLot({newItem.NewLocation.ItemLot})");
                                        processedGates.Add(gate);
                                    }
                                    if (edit.AfterCmds != null)
                                    {
                                        newCmds.AddRange(edit.AfterCmds);
                                    }
                                    if (edit.Replace != null)
                                    {
                                        events.RemoveMacro(edits, edit.Replace);
                                        events.AddMacro(edits, new EventAddCommand { After = edit.Replace, Cmds = newCmds });
                                    }
                                    else if (edit.Before != null)
                                    {
                                        events.AddMacro(edits, new EventAddCommand { Before = edit.Before, Cmds = newCmds });
                                    }
                                    else
                                    {
                                        events.AddMacro(edits, new EventAddCommand { After = edit.After, Cmds = newCmds });
                                    }
                                }
                            }
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
                            if (t.CondFlag != null)
                            {
                                events.ReplaceMacro(edits, t.CondFlag, filterGateFlag().ToString(), EventValueType.Flag);
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
                                            Cmd = $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {filterGateFlag()})",
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
                                        events.ReplaceMacro(edits, useCmd, $"IfEventFlag({ins[0]}, {check}, TargetEventFlagType.EventFlag, {filterGateFlag()})");
                                    }
                                }
                            }
                            bool adjustGroups = game.HasMods;
#if DEBUG
                            adjustGroups = true;
#endif
                            if (adjustGroups)
                            {
                                string condOrder = t.CondOrder;
                                if (addedConds.Count > 0)
                                {
                                    // Should be extra argument? Ideally Events should help identify unused conds in the first place
                                    string newCondStr = string.Join(' ', addedConds.Distinct().Select(c => $"+{c}"));
                                    condOrder = condOrder == null ? newCondStr : $"{condOrder} {newCondStr}";
                                }
                                events.AdjustConditionGroups(edits, e2, pre, condOrder, !game.HasMods, callee);
                            }
                            events.ApplyAllEdits(e2, edits, () => $"{debugId} has unapplied edits in item randomizer");
                            if (e2.ID != callee)
                            {
                                // Event is moved, ensure init removed and add new init
                                if (!copyRemoved)
                                {
                                    throw new Exception($"Internal error: Expected {callee} to be removed for non-in-place edit");
                                }
                                Instr initInstr = newInit ?? originalInit;
                                initInstr = events.CopyInit(initInstr, e2, initPre);
                                initInstr.Save(initPre);
                                newInits.Add(initInstr.Val);
                                newEvents.Add(e2);
                            }
                            else if (newInit != null)
                            {
                                newInit.Save(initPre);
                                e.Instructions[i] = newInit.Val;
                            }
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
                                int activateFlag = (int)originalInit[offset + activatePos];
                                if (activateFlag >= 191 && activateFlag <= 196)
                                {
                                    int getFlag = activateFlag - 20;
                                    // TODO: Unify system here and in enemy randomizer
                                    originalInit[offset + showPos] = getFlag;
                                    originalInit.Save();
                                    e.Instructions[i] = originalInit.Val;
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
                                // TODO: In some of these cases, the event should probably be removed? Like if item is removed, don't activate based on location flag
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
                                    originalInit[offset + pos] = targetFlag;
                                    originalInit.Save();
                                    e.Instructions[i] = originalInit.Val;
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
                                        if (item != null && tryScriptCheck(item, out int itemType) && ins.Bank == 3 && ins.ID == 0)
                                        {
                                            // 3[00] IfEventFlag(sbyte group, byte flagState, byte flagType, int flag)
                                            args = ins.UnpackArgs(new[] { ArgType.SByte, ArgType.Byte, ArgType.Byte, ArgType.Int32 });
                                            // 3[04] IfPlayerHasdoesntHaveItem(sbyte group, byte itemType, int itemId, byte ownState)
                                            e2.Instructions[j] = ins = new EMEVD.Instruction(3, 4);
                                            // Dependency on ItemType value
                                            ins.PackArgs(new List<object> { args[0], (byte)itemType, item.ID, args[1] });
                                            edited = true;
                                        }
                                        // This is an even more involved rewrite for Goto/End, which is needed for consistency with the first case
                                        else if (t.ItemCond != 0 && item != null && tryScriptCheck(item, out itemType) && ins.Bank == 1003 && (ins.ID == 1 || ins.ID == 2 || ins.ID == 101))
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
                                                3, 4, new List<object> { (sbyte)t.ItemCond, (byte)itemType, item.ID, args[1] });
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
                    if (opt["dlc"] && mapping.GestureFlag <= 0 && !ann.NorandomItems.Contains(mapping.GestureItem))
                    {
                        throw new Exception("Error: O Mother was not assigned to any location, but key items are randomized");
                    }
                    if (mapping.GestureFlag > 0)
                    {
                        if (enabledGates.Contains("omother"))
                        {
                            events.AddSimpleEvent(emevd, 19003131, new[]
                            {
                                "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 60864)",
                                $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {getGateFlag("omother")})",
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

                emevd.Events.AddRange(newEvents);
                if (newInits.Count > 0)
                {
                    emevd.Events[0].Instructions.AddRange(newInits);
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
                            if (item != null && tryScriptCheck(item, out int itemType))
                            {
                                modified = true;
                                // DoesPlayerHaveItem(type, id)
                                if (debugEsd) Console.WriteLine($"  - Rewriting flag {flag} to item {game.Name(item)}");
                                // Dependency on ItemType value
                                return esdFunction("f16", new List<int> { itemType, item.ID });
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
                                // Slightly cleaner shard condition can be used in ESD
                                configExpr = esdFunction("f47", new() { 3, itemId, (int)ESDEdits.ComparisonType.GreaterOrEqual, multiItem.ReqCount, 0 });
                                processedGates.Add(gate);
                            }
                            else
                            {
                                configExpr = esdFunction("f15", new List<int> { getGateFlag(gate) });
                            }
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
                    if (!gateItems.TryGetValue(gate, out ItemKey key)) throw new Exception($"Internal error: logic gate {gate} must be an item to edit locks");
                    int flag = getGateFlag(gate);
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
                        if (logicGate.Cond != "none")
                        {
                            int flag = getGateFlag(gate);
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
                    }
                }
                // Additional edits
                if (gate == "dragonbarrow")
                {
                    // TODO: This will need to incorporate item
                    int flag = getGateFlag(gate);
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
            flagSyncEvents.Sort((a, b) => a.ID.CompareTo(b.ID));
            game.Emevds["common"].Events.AddRange(flagSyncEvents);
            // Add instructions to the start to make sure flags are updated before any usages of them
            game.Emevds["common"].Events[0].Instructions.InsertRange(
                0, flagSyncEvents.Select(ev => new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 })));

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
                    foreach (Location locKey in location.Locs)
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
