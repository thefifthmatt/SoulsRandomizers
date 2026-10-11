using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static RandomizerCommon.LocationData;
using static SoulsIds.GameSpec;
#if ARCHIPELAGO
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
#endif

namespace RandomizerCommon
{
    public class ExternalItemPreset
    {
        // -- Required info
        public string LogInfo { get; private set; }
        // From external item id
        public Dictionary<long, LocalItem> LocalItems { get; private set; }
        // From external location id (item id may be shared between games + also local game)
        public Dictionary<long, ExternalItem> ExternalItems { get; private set; }
        // From external location id
        public Dictionary<long, LocalLocation> LocalLocations { get; private set; }
        // From external location id to external item id. Any finite locations not covered are not randomized.
        public Dictionary<long, long> FiniteItemMapping { get; private set; }

        // -- Other info
        // From target boss id to source boss id.
        // The source boss must appear exactly once at the given location (this could alternatively be a set of locations to allow for placement heuristics)
        public Dictionary<uint, uint> ForceBossTargets { get; private set; }
        // From Item Name Shard to required shard count. Any item split into shards must have an entry here or errors may result from multiple placement.
        public Dictionary<string, int> RequiredShardCounts { get; private set; }

        public class LocalItem
        {
            // Combined mask + id format
            public uint ID { get; set; }
            // Quantity to give all at once
            public int Quantity { get; set; }
            // For debugging missing items (and also titles?)
            public string Name { get; set; }
            // Some measure of rarity (cosmetic)
            public ExternalRarity Rarity { get; set; }

            // Added after loading locations
            public ItemKey Key { get; set; }
        }

        public class ExternalItem
        {
            // Item name to be shown in all languages
            public string Name { get; set; }
            // Short item info/caption
            public string Desc { get; set; }
            // Sort key below 1 million
            public int SortOffset { get; set; }
            // Some measure of rarity (cosmetic)
            public ExternalRarity Rarity { get; set; }

            // Added after loading locations
            public ItemKey Key { get; set; }
            public ItemKey LocalKey { get; set; }
        }

        public enum ExternalRarity
        {
            Unspecified,
            Common,
            Rare,
            Legendary,
            // Use random rarity
            Trap,
        }

        public class LocalLocation
        {
            // Several external locations can map to the same slot, but each must map to a unique ItemLocKey currently.
            // TODO warn about locations which are not covered
            public string LocationScopeKey { get; set; }
            // External description of this location, mainly for error reporting.
            // Avoid using it for resolving ambiguities or anything semantic.
            public string Desc { get; set; }
            // Unfortunately, deduplicated description to use for resolving ambiguities for the time being.
            public string BaseDesc { get; set; }
            // Param row ids for resolving the item in the case of ambiguities within a SlotKey.
            // Currently this is formatted like lot:<id> and slot:<id> with no subtype support at present.
            public List<string> LocationIDs { get; set; }
            // Some item ID could be useful here (stable id might would be preferable to name because of ambiguities)
        }

#if ARCHIPELAGO
        private static Dictionary<long, T> GetApIdMapping<T>(Dictionary<string, object> slotData, string key, bool required = false)
        {
            if (GetSlotData(slotData, key, out Dictionary<long, T> mapping))
            {
                return mapping;
            }
            if (required)
            {
                throw new Exception($"Archipelago slot data missing required {key}");
            }
            return null;
        }

        private static bool GetSlotData<T>(Dictionary<string, object> slotData, string key, out T val)
        {
            if (slotData.TryGetValue(key, out object data))
            {
                val = ((JObject)data).ToObject<T>();
                return true;
            }
            else
            {
                val = default;
                return false;
            }
        }

        public static ExternalItemPreset FromArchipelago(
            string logInfo,
            int mySlot,
            Dictionary<string, object> slotData,
            Dictionary<long, ScoutedItemInfo> locations)
        {
            // Loading this data should work across multiple games with game-specific stuff later
            ExternalItemPreset ret = new()
            {
                LogInfo = logInfo,
                LocalItems = new(),
                ExternalItems = new(),
                LocalLocations = new(),
                FiniteItemMapping = new(),
            };

            // ap item id (long) to uint local item id
            Dictionary<long, uint> itemIds = GetApIdMapping<uint>(slotData, "apIdsToItemIds", true);
            // ap item id to int
            Dictionary<long, int> itemCounts = GetApIdMapping<int>(slotData, "itemCounts", true);
            foreach ((long extId, uint localId) in itemIds)
            {
                LocalItem local = new()
                {
                    ID = localId,
                    Quantity = itemCounts.GetValueOrDefault(extId, 1)
                };
                ret.LocalItems[extId] = local;
            }

            // ap location id to slot key
            Dictionary<long, string> locationSlots = GetApIdMapping<string>(slotData, "locationIdsToKeys", true);
            // ap location id to list of lot:xx or shop:yy
            Dictionary<long, List<string>> locationLocs = GetApIdMapping<List<string>>(slotData, "locationIdsToTargets");
            // locationIdsToName is also supported in ER but try to rely on ScoutedItemInfo for now

            GetSlotData(slotData, "duplicatedLocations", out Dictionary<string, string> baseDescs);

            // Look at all locations meant for this slot to populate local mapping and necessary external items
            List<string> errors = new();
            foreach ((long id, ScoutedItemInfo info) in locations)
            {
                if (string.IsNullOrEmpty(info.LocationName))
                {
                    // This is an error in the client
                    errors.Add($"No location name for {id} in {info.LocationGame}");
                    continue;
                }
                if (!locationSlots.TryGetValue(id, out string locKey))
                {
                    errors.Add($"No location in locationIdsToKeys for assigned location [{info.LocationName}] in [{info.LocationGame}]");
                    continue;
                }
                LocalLocation local = new()
                {
                    LocationScopeKey = locKey,
                    LocationIDs = locationLocs?.GetValueOrDefault(id),
                    Desc = info.LocationName,
                    BaseDesc = baseDescs?.GetValueOrDefault(info.LocationName),
                };
                ret.LocalLocations[id] = local;
                ExternalRarity rarity = ExternalRarity.Common;
                if (info.Flags.HasFlag(ItemFlags.Advancement)) rarity = ExternalRarity.Legendary;
                else if (info.Flags.HasFlag(ItemFlags.NeverExclude)) rarity = ExternalRarity.Rare;
                else if (info.Flags.HasFlag(ItemFlags.Trap)) rarity = ExternalRarity.Trap;
                if (info.Player.Slot == mySlot)
                {
                    if (ret.LocalItems.TryGetValue(info.ItemId, out LocalItem localItem))
                    {
                        localItem.Name ??= info.ItemName;
                        // This ultimately comes from ItemInfo and doesn't need to come from scouted locations.
                        // It's just for info and doesn't override pickup color.
                        localItem.Rarity = rarity;
                    }
                    else
                    {
                        errors.Add($"No item in apIdsToItemIds for [{info.ItemName}] in [{info.ItemGame}] and slot {mySlot} [{info.Player}]");
                        continue;
                    }
                }
                else
                {
                    ExternalItem ext = new()
                    {
                        // Use same values as fswap archi. TODO shortening routine, or do in PermutationWriter from pieces if game-specific
                        Name = $"{info.Player}'s {info.ItemName}",
                        Desc = $"An object from a mysterious world known only as \"{info.Player.Game}\".",
                        // Alternatively, sort items after collecting them all and use result index? This avoids spacing issues and allows e.g. handling rarity
                        SortOffset = info.Player.Slot * 10000 + (int)(info.ItemId % 10000),
                        Rarity = rarity,
                    };
                    ret.ExternalItems[id] = ext;
                }
                ret.FiniteItemMapping[id] = info.ItemId;
            }
            if (errors.Count > 0)
            {
                throw new Exception($"Incomplete slot data received from Archipelago server. Cannot proceed with randomization. Issues:\n{string.Join("\n", errors)}");
            }

            if (GetSlotData(slotData, "forceBosses", out Dictionary<uint, uint> bosses) && bosses.Count > 0)
            {
                ret.ForceBossTargets = bosses;
            }

            if (GetSlotData(slotData, "requiredShards", out Dictionary<string, int> shards))
            {
                ret.RequiredShardCounts = shards;
            }

            return ret;
        }

        // TODO: Should be client's responsibility if possible
        public static bool DeathLinkEnabled(Dictionary<string, object> slotData)
        {
            JObject options = (JObject)slotData["options"];
            return options.TryGetValue("death_link", out JToken value) && value.ToObject<int>() == 1;
        }

        public static RandomizerOptionsFile MakeEldenRingOptions(Dictionary<string, object> slotData)
        {
            // This code is fairly load-bearing and is a pile of strings on top of that.
            // It must be updated when randomizer options or apworld options change, and version numbers can be used if necessary.
            // TODO: Also reverse sync?
            JObject apOptions = (JObject)slotData["options"];
            bool apOpt(string name, bool defaultValue = false)
            {
                if (!apOptions.TryGetValue(name, out JToken token))
                {
                    return defaultValue;
                }
                // bool options are represented with numbers
                // Try using apInt when this is actually an enum
                return token.ToObject<int>() == 1;
            }
            bool apInt(string name, out int value)
            {
                if (apOptions.TryGetValue(name, out JToken token))
                {
                    value = token.ToObject<int>();
                    return true;
                }
                value = default;
                return false;
            }
            List<string> errors = new();
            RandomizerOptions opt = new RandomizerOptions(FromGame.ER);
            // This could alternatively add the slot number to the hash output
            string seed = (string)slotData["seed"];
            string slot = (string)slotData["slot"];
            opt.Seed = Util.XxStringHash($"{seed}|{slot}");
            opt["archipelago"] = true;
            // -- Item
            opt["item"] = true;
            opt["mats"] = apOpt("material_rando");
            // -- Logic
            if (apInt("great_runes_required_leyndell", out int runesLeyndell))
            {
                opt.SetNonDefaultInt(RandomizerOptions.IntOpt.RunesLeyndell, runesLeyndell);
            }
            if (apInt("great_runes_required_mountain", out int runesRold))
            {
                opt.SetNonDefaultInt(RandomizerOptions.IntOpt.RunesRold, runesRold);
            }
            if (apInt("great_runes_required_erdtree", out int runesEnd))
            {
                opt.SetNonDefaultInt(RandomizerOptions.IntOpt.RunesEnd, runesEnd);
            }
            opt["royalaccess"] = apOpt("royal_access");
            // Hard logic options: rustykey liurniagate latedragonbarrow altusgate deathless
            bool regionlock = !apOpt("world_logic");
            opt["regionlock"] = regionlock;
            opt["altusgate"] = regionlock;
            // This could be a separate AP option, but still better for AP to make the item work as intended
            opt["rustykey"] = true;
            if (apInt("use_master_key", out int useMasterKey))
            {
                if (useMasterKey == 1)
                {
                    opt["stoneswordregion"] = true;
                }
                else if (useMasterKey == 2)
                {
                    opt["stoneswordall"] = true;
                }
            }
            // Does nothing except maybe affect location lateness
            opt["dlckeysilo"] = apOpt("separate_progression");
            // It is harmless to always create the items, but try to use option naming convention for clarity in the UI.
            opt["traps"] = apOptions.Properties().Any(prop => prop.Name.EndsWith("_trap_count") && apInt(prop.Name, out int count) && count > 0);
            opt["invertautohunt"] = apOpt("rykard_encounter");
            // -- Enemy
            opt["enemy"] = apOpt("enemy_rando");
            // Some of these can be configured independent of slot data mid-run
            opt["scale"] = true;
            if (apInt("boss_scaling_percent", out int scalePercent) && scalePercent != 100)
            {
                errors.Add($"Not supported: boss_scaling_percent = {scalePercent} (must be 100)");
            }
            opt["phasehp"] = true;
            opt["editnames"] = true;
            opt["bossbgm"] = true;
            opt["swaprewards"] = false;
            opt["swapboss"] = false;
            opt["nightday"] = apOpt("night_bosses");
            opt["ignoresize"] = !apOpt("restrictive_bosses");
            // TODO: Random enemy preset with customenemy
            // TODO: Option for merge bosses
            if (slotData.TryGetValue("random_enemy_preset", out object presetObj) && presetObj is string preset
                && !string.IsNullOrWhiteSpace(preset) && preset != "{}")
            {
                errors.Add($"Not supported: random_enemy_preset");
            }
            // DLC
            opt["dlc"] = apOpt("enable_dlc");
            opt["tarnished"] = apOpt("enable_tp_dlc");
            opt["maxweapon"] = apOpt("dlc_max_level_weapons");
            opt["woodsfast"] = apOpt("dlc_abyssal_torrent");
            opt["springsanity"] = apOpt("spiritspring_stones");
            if (apInt("dlc_start", out int dlcStart))
            {
                if (dlcStart == 1)
                {
                    opt["dlcstart"] = true;
                    opt["dlchand"] = true;
                }
                else if (dlcStart == 2)
                {
                    opt["quickstart"] = true;
                }
            }
            opt["dlcshop"] = apOpt("dlc_starting_shop");
            opt["dlcpackage"] = apOpt("dlc_care_package");
            opt["dlcroundtable"] = true;
            if (apInt("dlc_initial_rune_level", out int playerLevel))
            {
                opt.SetNonDefaultInt(RandomizerOptions.IntOpt.PlayerLevel, playerLevel);
            }
            // This is currently hardcoded
            List<string> dlcStartItems = new();
            if (apOptions.TryGetValue("dlc_starting_items", out JToken startItemList))
            {
                dlcStartItems = startItemList.ToObject<List<string>>();
            }
            opt["maxflasknum"] = dlcStartItems.Contains("Golden Seeds");
            opt["maxflask"] = dlcStartItems.Contains("Sacred Tears");
            opt["maxpouch"] = dlcStartItems.Contains("Talisman Pouches");
            opt["maxmemslot"] = dlcStartItems.Contains("Memory Stones");
            opt["maxwhetblade"] = dlcStartItems.Contains("Whetblades");
            opt["maxbb"] = dlcStartItems.Contains("Upgrade Bell Bearings");
            // Misc
            opt["invertstarting"] = apOpt("random_start");
            if (apOpt("remove_weapon_and_spell_requirements"))
            {
                opt["nohand"] = true;
            }
            else if (apOpt("require_one_handed_starting_weapons"))
            {
                opt["onehand"] = true;
            }
            opt["changestats"] = true;
            opt["invertgifts"] = apOpt("randomize_starting_keepsakes");
            opt["invertoutfits"] = true;
            opt["invertenvbgm"] = true;
            opt["invertgestures"] = true;
            opt["weaponreqs"] = apOpt("remove_weapon_and_spell_requirements");
            opt["allmaps"] = apInt("map_option", out int mapOption) && mapOption == 1;
            opt["nerfgargoyles"] = apOpt("disable_gargoyle_poison_cloud_damage");
            opt["sombermode"] = apOpt("reduce_non_somber_upgrade_cost");
            opt["snowfast"] = apOpt("snowfast");
            opt["crashfix"] = true;
            string helperConfig = null;
            bool autoEquip = apOpt("auto_equip");
            bool autoUpgrade = apOpt("auto_upgrade") || apOpt("dlc_max_level_weapons");
            if (autoEquip || autoUpgrade)
            {
                // The mechanism for this can be finetuned in the future but for now, use RandomizerHelper as-is
                // and set only the configured fields. The rest will be filled in with defaults.
                helperConfig = @$"
[settings]
autoEquip = {(autoEquip ? "true" : "false")}
autoUpgrade = {(autoUpgrade ? "true" : "false")}
";
            }
            opt["helper"] = helperConfig != null;
            opt["me3"] = true;

            if (errors.Count > 0)
            {
                throw new Exception($"Encountered errors from Archipelago options:{string.Join("", errors.Select(s => "\n- " + s))}");
            }

            return RandomizerOptionsFile.Create(Randomizer.EldenVersion, opt, helperConfig: helperConfig);
        }
#endif
    }
}
