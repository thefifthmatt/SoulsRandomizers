using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static SoulsFormats.EMEVD.Instruction;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.LocationKey;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class HintMarker
    {
        private static readonly Regex phraseRe = new Regex(@"\s*;\s*");
        private static readonly Regex miniRe = new Regex(@"^m3[0-2]|^m18");

        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        private Messages messages;
        private EldenCoordinator coord;

        public HintMarker(GameData game, LocationData data, AnnotationData ann, Messages messages, EldenCoordinator coord)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.messages = messages;
            this.coord = coord;
        }

        public class HintMarkerConfig
        {
            public List<string> DebugInfo { get; set; }
            // Used to enable the hints system in-game
            public int EnableFlag { get; set; }
            public List<HintArea> Areas { get; set; }
            public List<HintItem> Items { get; set; }
        }

        public class HintArea
        {
            // Game areas, space-delimited. This should completely partition all logical areas.
            public string Includes { get; set; }
            // The bonfire name which hints this area overall
            public string MainBonfire { get; set; }
            // All bonfire in this area, semicolon-delimited. Visiting any of these is counted for visiting the area.
            // Subcategories can be used to mean every bonfire in that subcategory. !Bonfire is used to exclude a bonfire.
            public string Bonfires { get; set; }
            // Inherits Bonfires field from parent area
            public string Parent { get; set; }
            // The name to show on the marker instead of the first of Bonfires (if defined) or MainBonfire (if defined).
            // Currently this must exist in PlaceName FMG already.
            public string DisplayName { get; set; }
            // Event name for the area boss, or entity id otherwise.
            // This should match the one in itemlocations.txt and it should be associated with a boss or altboss event.
            public string Boss { get; set; }
            // Defeat flag. Normally filled in from itemlocations.txt, but should be specified otherwise
            public int BossFlag { get; set; }
            // The bonfire name used for the boss location. We can just use the entity id for now?
            // public string BossBonfire { get; set; }
            public string Comment { get; set; }
            [YamlIgnore]
            public List<int> BonfireIds = new List<int>();
            [YamlIgnore]
            public int MainBonfireId { get; set; }
            [YamlIgnore]
            public EntityId BossId { get; set; }
            [YamlIgnore]
            public int BossMarker { get; set; }
        }

        public class HintItem
        {
            // An item with check handling, or a space-delimited list of several
            public string Name { get; set; }
            // Name for showing in spoiler logs
            public string DisplayName { get; set; }
            public string Comment { get; set; }
            // A way to check the item's area has actually been explored
            public string UnexploredCheck { get; set; }
            // Alternate ways to check for item hint necessity or dependency satisfaction.
            // Conditional checks are ignored if the condition is not met.
            // If this is null, it is assumed to be a single req with CheckItem = self.
            // If it is empty, the item is assumed to be always available.
            // If there are multiple eligible checks/alts, all of them will be required (AND condition).
            public List<HintReq> Reqs { get; set; }
            // The same list but with conditions evaluated
            [YamlIgnore]
            public List<HintReq> FilterReqs { get; set; }
        }

        public class HintReq
        {
            // An area which must be required (or not) for this check to be considered. This is ignored on the second pass.
            public string AreaCondition { get; set; }
            public string NotAreaCondition { get; set; }
            // An option which must be enabled (or not) for this check to be considered. This is ignored on the second pass.
            public string OptionCondition { get; set; }
            public string NotOptionCondition { get; set; }
            // Which pass to check it in, first or second
            public string PassCondition { get; set; }
            // The item to use a flag for direct hinting, and item possession for dependencies.
            // Either "self" or null, currently
            public string CheckItem { get; set; }
            // A flag being on, which should be checked instead of checking item possession/flags.
            // 182 for initial Great Runes, individual gate activation for Imbued Sword Keys.
            public int CheckFlag { get; set; }
            // A flag being on, which should be checked as a redundant alternative to getting the item,
            // either when hinting it directly or using it as a dependency.
            // Like Altus flag for Dectus, gate flag for Rusty Key.
            // But - don't account for skips not part of logic, like the one down to Snowfield.
            public int AltFlag { get; set; }
            // An item in one's possession, which should be checked as a redundant alternative to getting this time.
            // Like Academy Glintstone Key for Drawing Room Key (assuming a NotAreaCondition of the drawing room)
            public string AltItem { get; set; }
        }

        public enum MarkerType
        {
            Other = 0,
            Church = 3,
            Catacombs = 4,
            Ruins = 5,
            Shack = 6,
            Tower = 8,
            Evergaol = 9,
            Gate = 10,
            Bridge_Of_Sacrifice = 11,
            Cave = 13,
            Tunnel = 14,
            Well = 15,
            Grave = 16,
            Rise = 17,
            Fort = 18,
            Pasture = 19,
            Cathedral = 20,
            Lift = 21,
            Divine_Tower = 23,
            Castle_Morne = 25,
            Caria_Manor = 26,
            Shaded_Castle = 27,
            Redmane_Castle = 28,
            Castle_Sol = 29,
            Erdtree = 30,
            Lake_Town = 32,
            Sellia = 33,
            Ordina = 34,
            Frenzied_Flame_Village = 35,
            Windmill_Village = 36,
            Ailing_Village = 37,
            Jarburg = 38,
            Albinaurics_Village = 39,
            Hermit_Village = 40,
            Mohgwyn_Mausoleum = 45,
            Underground = 46,
            Underground_Ruins = 47,
            Stormveil_Castle = 50,
            Raya_Lucaria = 51,
            Four_Belfries = 52,
            Precipice = 53,
            Bestial_Sanctum = 54,
            Haligtree = 55,
            Haligtree_Brace = 56,
            Forge = 57,
            Volcano_Manor = 58,
            Farum_Azula = 59,
            Leyndell = 60,
            Subterranean = 61,
            Carian_Study_Hall = 66,
            NPC = 80,
            Grace_Arrow = 83,
            Roots_Guide_Arrow = 84,
            Roots_Guide = 85,
            Red_Mark = 87,
            Rold_Route = 88,
        }

        private string formatMap(IEnumerable<byte> bytes) => "m" + string.Join("_", bytes.Select(b => b == 0xFF ? "XX" : $"{b:d2}"));
        private List<byte> parseMap(string map) => map.TrimStart('m').Split('_').Select(p => byte.Parse(p)).ToList();

        [Localize]
        private static readonly Text hintPurchase =
            new Text("Purchase a required item marker", "GameMenu_hintPurchase");
        [Localize]
        private static readonly Text hintRequirementFailed =
            new Text("Get to the Altus Plateau first!", "GameMenu_hintRequirementFailed");
        [Localize]
        private static readonly Text hintRequirementFailedBoss =
            new Text("Exact location hints in an area are only\n" +
                     "available after defeating the area's main boss.", "GameMenu_hintRequirementFailedBoss");
        [Localize]
        private static readonly Text hintRequirementFailedOption =
            new Text("The randomizer option for exact location hints was not enabled.", "GameMenu_hintRequirementFailedOption");
        [Localize]
        private static readonly Text hintMarkArea =
            new Text("Mark a required item's overall area", "GameMenu_hintMarkArea");
        [Localize]
        private static readonly Text hintMarkItem =
            new Text("Mark a required item's exact location (more expensive)", "GameMenu_hintMarkItem");
        [Localize]
        private static readonly Text hintNoItems =
            new Text("No key items left!", "GameMenu_hintNoItems");
        [Localize]
        private static readonly Text hintLabelArea =
            new Text("Somewhere in {0}", "GameMenu_hintLabelArea");

        public void Write(RandomizerOptions opt, Permutation perm, PermutationWriter.Result permResult)
        {
            // Get some bonfire names
            FMG placeFmg = game.ItemFMGs["PlaceName"];
            FMG warpFmg = game.MenuFMGs["GR_MenuText"];
            int newPlaceBase = 9404000;
            Dictionary<string, int> revNameIds = new Dictionary<string, int>();
            foreach (FMG.Entry entry in placeFmg.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Text))
                {
                    revNameIds[entry.Text] = entry.ID;
                }
            }
            Dictionary<int, string> cats = new Dictionary<int, string>();
            foreach (PARAM.Row row in game.Params["BonfireWarpSubCategoryParam"].Rows)
            {
                int nameId = (int)row["textId"].Value;
                string name = warpFmg[nameId];
                if (string.IsNullOrWhiteSpace(name)) continue;
                int tab = (ushort)row["tabId"].Value;
                cats[row.ID] = name;
            }

            // Category/bonfire name to row ID
            Dictionary<string, List<int>> bonfiresByCats = new Dictionary<string, List<int>>();
            Dictionary<string, List<int>> bonfiresByNames = new Dictionary<string, List<int>>();
            foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
            {
                int nameId = (int)row["textId1"].Value;
                string name = placeFmg[nameId];
                if (string.IsNullOrWhiteSpace(name)) continue;
                int cat = (int)row["bonfireSubCategoryId"].Value;
                if (cats.TryGetValue(cat, out string subName))
                {
                    AddMulti(bonfiresByCats, subName, row.ID);
                }
                AddMulti(bonfiresByNames, name, row.ID);
            }

            // Use Melina's red mark as a basis
            PARAM.Row rold = game.Params["WorldMapPointParam"][85495300];
            int rowStart = 85606000;
            bool ignoreDisappear = false;
            void addConditionalMark(int placeNameId, string mapId, Vector3 pos, int appearFlag, int disappearFlag, bool ignore = false)
            {
                PARAM.Row row = game.AddRow("WorldMapPointParam", rowStart++);
                GameEditor.CopyRow(rold, row);
                // eventFlagId=11109687, iconId=87, dispMask00=1,
                // textId3=placename, textEnableFlagId3=11109687, textDisableFlagId3=62528
                row["eventFlagId"].Value = ignore ? 6001u : (uint)appearFlag;
                row["textEnableFlagId3"].Value = ignore ? 6001u : (uint)appearFlag;
                row["textDisableFlagId3"].Value = ignore && ignoreDisappear ? 0u : (uint)disappearFlag;
                // Aboveground, underground
                row["dispMask00"].Value = (byte)1;
                row["dispMask01"].Value = (byte)1;
                row["textId3"].Value = placeNameId;
                List<byte> mapParts = parseMap(mapId);
                row["areaNo"].Value = mapParts[0];
                row["gridXNo"].Value = mapParts[1];
                row["gridZNo"].Value = mapParts[2];
                row["posX"].Value = pos.X;
                row["posY"].Value = pos.Y;
                row["posZ"].Value = pos.Z;
            }
            if (opt["markbonfires"])
            {
                // Test for global position mapping (not needed for any other markers, at least)
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    string name = placeFmg[(int)row["textId1"].Value];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int cat = (int)row["bonfireSubCategoryId"].Value;
                    List<byte> mapParts = game.GetMapParts(row);
                    string mapId = formatMap(mapParts);
                    Vector3 mapPos = new Vector3((float)row["posX"].Value, (float)row["posY"].Value, (float)row["posZ"].Value);
                    (Vector3 pos, int tileX, int tileZ) = coord.ToGlobalCoords(mapParts, mapPos);
                    List<byte> toMap = new List<byte> { 60, (byte)tileX, (byte)tileZ, 0 };
                    Vector3 mapPos2 = coord.ToLocalCoords(toMap, pos);
                    addConditionalMark((int)row["textId1"].Value, formatMap(toMap), mapPos2, 6001, 6001);
                }
            }

            IDeserializer deserializer = new DeserializerBuilder().Build();
            string path = $@"{game.Dir}\Base\hintmarks.txt";
            HintMarkerConfig config;
            using (var reader = File.OpenText(path))
            {
                config = deserializer.Deserialize<HintMarkerConfig>(reader);
            }

            // Process config first
            Dictionary<string, HintItem> items = new Dictionary<string, HintItem>();
            foreach (HintItem item in config.Items)
            {
                if (item.Reqs == null)
                {
                    item.Reqs = new List<HintReq> { new HintReq { CheckItem = "self" } };
                }
                foreach (HintReq req in item.Reqs)
                {
                    if (req.AreaCondition != null && !ann.Areas.ContainsKey(req.AreaCondition)) throw new Exception(req.AreaCondition);
                    if (req.NotAreaCondition != null && !ann.Areas.ContainsKey(req.NotAreaCondition)) throw new Exception(req.NotAreaCondition);
                }
                List<string> itemNames = item.Name.Split(' ').ToList();
                foreach (string itemName in itemNames)
                {
                    if (!ann.Items.ContainsKey(itemName)) throw new Exception($"Internal error: missing {itemName}");
                    items[itemName] = item;
                }
            }
            HashSet<ItemKey> hintItemKeys = new HashSet<ItemKey>(items.Select(e => ann.Items[e.Key]));
            List<ItemKey> missingItems = ann.ItemGroups["keyitems"].Where(i => !hintItemKeys.Contains(i)).ToList();
            if (missingItems.Count > 0)
            {
                throw new Exception($"Internal error: missing key items {string.Join(", ", missingItems.Select(game.DisplayName))}");
            }
            if (!perm.IncludedItems.TryGetValue(opt["runereq"] ? "erdtree" : "leyndell2_throne", out HashSet<string> erdtree))
            {
                throw new Exception($"Can't add hints, no required items found (internal error?)");
            }
            List<string> requiredItems = erdtree.Where(t => ann.Items.ContainsKey(t)).ToList();
            List<string> requiredAreas = erdtree.Where(t => ann.Areas.ContainsKey(t)).ToList();
            foreach (HintItem item in items.Values)
            {
                if (item.FilterReqs == null)
                {
                    item.FilterReqs = item.Reqs.ToList();
                    item.FilterReqs.RemoveAll(req =>
                    {
                        if (req.AreaCondition != null && !requiredAreas.Contains(req.AreaCondition)) return true;
                        if (req.NotAreaCondition != null && requiredAreas.Contains(req.NotAreaCondition)) return true;
                        if (req.OptionCondition != null && !opt[req.OptionCondition]) return true;
                        if (req.NotOptionCondition != null && opt[req.NotOptionCondition]) return true;
                        // In first pass, filters the checks to only required items
                        if (req.PassCondition == "second") return true;
                        return false;
                    });
                    item.Reqs.RemoveAll(req =>
                    {
                        // In second pass, use full reqs list
                        if (req.PassCondition == "first") return true;
                        return false;
                    });
                }
            }

            Dictionary<string, HintArea> areas = new Dictionary<string, HintArea>();
            Dictionary<int, EntityId> bossIds = new Dictionary<int, EntityId>();
            Dictionary<string, EntityId> bossEvents = new Dictionary<string, EntityId>();
            foreach (HintArea area in config.Areas)
            {
                if (string.IsNullOrWhiteSpace(area.Includes)) throw new Exception("Malformed hint marker entry");
                List<string> includes = area.Includes.Split(' ').ToList();
                foreach (string include in includes)
                {
                    if (!ann.Areas.ContainsKey(include)) throw new Exception($"Bad hint area {include}");
                }
                if (area.Bonfires != null)
                {
                    foreach (string bonfire in phraseRe.Split(area.Bonfires))
                    {
                        if (area.DisplayName == null)
                        {
                            area.DisplayName = bonfire;
                        }
                        if (bonfiresByCats.TryGetValue(bonfire, out List<int> bonfires))
                        {
                            area.BonfireIds.AddRange(bonfires);
                        }
                        else if (bonfiresByNames.TryGetValue(bonfire, out bonfires))
                        {
                            if (bonfires.Count > 1) throw new Exception($"[{area.Includes}]: ambiguous hint bonfire {area.MainBonfire}");
                            area.BonfireIds.AddRange(bonfires);
                        }
                        else if (bonfire.StartsWith("!") && bonfiresByNames.TryGetValue(bonfire.Substring(1), out bonfires))
                        {
                            area.BonfireIds.RemoveAll(x => bonfires.Contains(x));
                        }
                        else throw new Exception($"[{area.Includes}]: unknown hint bonfire {bonfire}");
                    }
                }
                if (area.MainBonfire != null)
                {
                    if (area.DisplayName == null)
                    {
                        area.DisplayName = area.MainBonfire;
                    }
                    // Defined for almost all areas except isolates
                    if (!bonfiresByNames.TryGetValue(area.MainBonfire, out List<int> bonfires))
                    {
                        throw new Exception($"[{area.Includes}]: unknown bonfire [{area.MainBonfire}]");
                    }
                    if (bonfires.Count > 1) throw new Exception($"[{area.Includes}]: ambiguous hint bonfire {area.MainBonfire}");
                    area.MainBonfireId = bonfires[0];
                }
                if (area.Boss != null)
                {
                    if (int.TryParse(area.Boss, out int bossId))
                    {
                        bossIds[bossId] = null;
                    }
                    else
                    {
                        bossEvents[area.Boss] = null;
                    }
                }
                if (area.DisplayName == null) throw new Exception($"[{area.Includes}]: no display name");
                if (!revNameIds.ContainsKey(area.DisplayName)) throw new Exception($"[{area.Includes}]: unknown name [{area.DisplayName}]");

                foreach (string include in includes)
                {
                    areas[include] = area;
                }
            }
            foreach (HintArea area in config.Areas)
            {
                if (area.Parent == null) continue;
                if (!areas.TryGetValue(area.Parent, out HintArea parentArea)) throw new Exception($"Parent {area.Parent} not found");
                if (parentArea.Parent != null) throw new Exception($"Parent {area.Parent} is nested");
                area.BonfireIds.AddRange(parentArea.BonfireIds);
            }
            List<string> missingAreas = ann.Areas.Keys.Where(a => !areas.ContainsKey(a)).ToList();
            if (missingAreas.Count > 0)
            {
                throw new Exception($"Internal error: missing areas {string.Join(", ", missingAreas)}");
            }

            // Infer some boss locations and flags
            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
            {
                SlotAnnotation slotAnn = entry.Value;
                if (!slotAnn.HasTag("boss") && !slotAnn.HasTag("altboss")) continue;
                foreach (SlotKey key in data.Location(entry.Key))
                {
                    ItemLocation itemLoc = data.Location(key);
                    foreach (LocationKey loc in itemLoc.Keys)
                    {
                        foreach (EntityId id in loc.Entities)
                        {
                            if (id.EntityID <= 0) continue;
                            if (slotAnn.Event != null && bossEvents.ContainsKey(slotAnn.Event))
                            {
                                bossEvents[slotAnn.Event] = id;
                            }
                            else if (bossIds.ContainsKey(id.EntityID))
                            {
                                bossIds[id.EntityID] = id;
                            }
                        }
                    }
                }
            }
            // One more iteration for filling in boss entities and flags
            foreach (HintArea area in config.Areas)
            {
                if (area.Boss == null) continue;
                EntityId id = int.TryParse(area.Boss, out int bossId) ? bossIds[bossId] : bossEvents[area.Boss];
                if (id == null) throw new Exception($"[{area.Includes}]: entity {area.Boss} not found");
                area.BossId = id;
                if (area.BossFlag <= 0)
                {
                    if (!data.BossFlags.TryGetValue(id.EntityID, out int bossFlag))
                    {
                        throw new Exception($"[{area.Includes}]: no boss flag found for {id.EntityID}");
                    }
                    area.BossFlag = bossFlag;
                }
            }

            // Add all of the markers.
            if (perm == null) return;
            Dictionary<ItemKey, string> itemAreas = new Dictionary<ItemKey, string>();
            Dictionary<ItemKey, EntityId> itemLocations = new Dictionary<ItemKey, EntityId>();
            foreach (KeyValuePair<SlotKey, List<SlotKey>> entry in perm.Silos[Permutation.RandomSilo.FINITE].Mapping)
            {
                foreach (SlotKey source in entry.Value)
                {
                    if (!hintItemKeys.Contains(source.Item)) continue;
                    SlotKey target = entry.Key;
                    ItemLocation itemLoc = data.Location(target);
                    if (!ann.Slots.TryGetValue(itemLoc.LocScope, out SlotAnnotation slotAnn)) continue;
                    if (!ann.Areas.TryGetValue(slotAnn.Area, out AreaAnnotation areaAnn)) continue;
                    itemAreas[source.Item] = slotAnn.Area;

                    if (areaAnn.Maps == null) continue;
                    List<string> eligibleMaps = areaAnn.Maps.Split(' ').ToList();
                    // Console.WriteLine($"{game.Name(source.Item)} = {entry.Key}");
                    foreach (EntityId id in itemLoc.Keys.SelectMany(loc => loc.Entities))
                    {
                        if (!string.IsNullOrEmpty(id.MapName) && eligibleMaps.Contains(id.MapName))
                        {
                            itemLocations[source.Item] = id;
                            break;
                        }
                    }
                }
            }

            // After the main marker flag range, these appear to be unused
            int startMarkerFlag = 78700;
            int markerFlag = startMarkerFlag;

            Dictionary<string, int> vagueNameIds = new Dictionary<string, int>();
            int getVagueNameId(string text)
            {
                if (!revNameIds.TryGetValue(text, out int oldId))
                {
                    throw new Exception($"Internal error: unknown game location: {text}");
                }
                if (vagueNameIds.TryGetValue(text, out int newId))
                {
                    return newId;
                }
                vagueNameIds[text] = newId = newPlaceBase++;
                game.WriteFMGs = true;
                FMGArg oldName = new FMGArg { FMGName = "PlaceName", ID = oldId, BaseText = text };
                messages.SetFMGEntry(game.ItemFMGs, game.OtherItemFMGs, "PlaceName", newId, hintLabelArea, oldName);
                return newId;
            }

            void placeBonfireMarker(HintArea area, int placeNameId, int appearFlag, int disappearFlag, bool ignore)
            {
                PARAM.Row row = game.Params["BonfireWarpParam"][area.MainBonfireId];
                List<byte> mapParts = game.GetMapParts(row);
                string mapId = formatMap(mapParts);
                Vector3 mapPos = new Vector3((float)row["posX"].Value, (float)row["posY"].Value, (float)row["posZ"].Value);
                addConditionalMark(
                    placeNameId, mapId, mapPos + new Vector3(20, 0, -20),
                    appearFlag, disappearFlag, ignore);
            }

            // Boss markers by area name, with the boss EntityId and boss flag
            // These can probably be shared per DisplayName, but do this to be safer
            foreach (HintArea area in areas.Values)
            {
                if (area.BossMarker > 0 || area.BossId?.Position == null || area.BossFlag <= 0) continue;
                string mapName = area.BossId.MapName;
                int vagueNameId = getVagueNameId(area.DisplayName);
                if (area.MainBonfireId > 0 && miniRe.IsMatch(mapName))
                {
                    placeBonfireMarker(area, vagueNameId, markerFlag, area.BossFlag, false);
                }
                else
                {
                    addConditionalMark(
                        vagueNameId, area.BossId.MapName, (Vector3)area.BossId.Position,
                        markerFlag, area.BossFlag, false);
                }
                area.BossMarker = markerFlag++;
            }
            // Item markers by item, with the item EntityId and item get flag
            Dictionary<string, int> itemMarkers = new Dictionary<string, int>();
            // Area markers by item, with the item's area's bonfire and item get flag
            Dictionary<string, int> itemAreaMarkers = new Dictionary<string, int>();
            foreach (KeyValuePair<string, HintItem> entry in items)
            {
                string item = entry.Key;
                ItemKey key = ann.Items[item];
                if (!itemAreas.TryGetValue(key, out string place)) continue;
                if (!permResult.ItemEventFlags.TryGetValue(key, out int itemFlag)) continue;
                HintArea area = areas[place];
                if (area.MainBonfireId > 0)
                {
                    int vagueNameId = getVagueNameId(area.DisplayName);
                    placeBonfireMarker(area, vagueNameId, markerFlag, itemFlag, false);
                    itemAreaMarkers[item] = markerFlag++;
                }
                if (itemLocations.TryGetValue(key, out EntityId id) && id.Position is Vector3 pos)
                {
                    addConditionalMark(
                        revNameIds[area.DisplayName], id.MapName, pos,
                        markerFlag, itemFlag, false);
                    itemMarkers[item] = markerFlag++;
                }
            }

            Dictionary<string, List<string>> includedItems = new Dictionary<string, List<string>>();
            HashSet<string> flaggedItems = new HashSet<string>(ann.Items
                .Where(e => permResult.ItemEventFlags.TryGetValue(e.Value, out int flag) && flag > 0)
                .Select(e => e.Key));
            foreach (KeyValuePair<string, HashSet<string>> entry in perm.IncludedItems)
            {
                string item = entry.Key;
                if (items.ContainsKey(item) && flaggedItems.Contains(item))
                {
                    includedItems[item] = entry.Value.Where(t => t != item && items.ContainsKey(t) && flaggedItems.Contains(t)).ToList();
                    // Console.WriteLine($"{(requiredItems.Contains(item) ? "*" : "")}dep {item}: {string.Join(", ", includedItems[item])}");
                }
            }

            int getPriority(string item, int deps)
            {
                // Try to order from broad and early to specific and later
                if (!itemLocations.TryGetValue(ann.Items[item], out EntityId id) || string.IsNullOrEmpty(id.MapName)) return 80 + deps;
                if (miniRe.IsMatch(id.MapName) || id.MapName.StartsWith("m34")) return 60 + deps;
                if (id.MapName.StartsWith("m60")) return 40 + deps;
                return 40 + deps;
            }
            List<string> itemOrder = includedItems.OrderBy(e => getPriority(e.Key, e.Value.Count)).Select(e => e.Key).ToList();
            AST.Expr chainExprs(string op, List<AST.Expr> parts)
            {
                AST.Expr ret = null;
                foreach (AST.Expr part in parts)
                {
                    if (part == null)
                    {
                        continue;
                    }
                    if (ret == null)
                    {
                        ret = part;
                    }
                    else
                    {
                        ret = new AST.BinaryExpr { Op = op, Lhs = ret, Rhs = part };
                    }
                }
                return ret;
            }
            AST.Expr getItemCheck(string item, List<HintReq> reqs, bool alwaysCheckSelf)
            {
                AST.Expr eventFlag(int flag) => AST.MakeFunction("f15", flag);
                int calcFlag(int flag, string dep) => flag > 0 ? flag : permResult.ItemEventFlags[ann.Items[dep == "self" ? item : dep]];
                ItemKey key = ann.Items[item];
                List<AST.Expr> alts = new List<AST.Expr>();
                if (alwaysCheckSelf)
                {
                    alts.Add(eventFlag(calcFlag(0, "self")));
                }
                List<HintReq> checkReqs = reqs.Where(r => r.CheckFlag > 0 || (!alwaysCheckSelf && r.CheckItem != null)).ToList();
                List<HintReq> altReqs = reqs.Where(r => r.AltFlag > 0 || r.AltItem != null).ToList();
                if (checkReqs.Count > 0)
                {
                    List<AST.Expr> checkExprs = checkReqs.Select(r => eventFlag(calcFlag(r.CheckFlag, r.CheckItem))).ToList();
                    alts.Add(chainExprs("&&", checkExprs));
                }
                if (altReqs.Count > 0)
                {
                    List<AST.Expr> altExprs = altReqs.Select(r => eventFlag(calcFlag(r.AltFlag, r.AltItem))).ToList();
                    alts.Add(chainExprs("&&", altExprs));
                }
                return chainExprs("||", alts);
            }
            // Returns check expression and machine args for purchasing (item marker, area marker, boss marker, boss flag)
            (AST.Expr, object[]) checkItemHint(string item, bool onlyRequired)
            {
                ItemKey key = ann.Items[item];
                itemAreas.TryGetValue(key, out string area);
                itemMarkers.TryGetValue(item, out int itemMarker);
                itemAreaMarkers.TryGetValue(item, out int itemAreaMarker);
                if (area == null || (itemMarker == 0 && itemAreaMarker == 0))
                {
                    return (null, null);
                }
                HintItem hintItem = items[item];
                HintArea hintArea = areas[area];
                // In the check-all pass, ignore filters for the direct item check (still use them for dependencies)
                List<HintReq> reqs = hintItem.Reqs;
                if (onlyRequired)
                {
                    reqs = hintItem.FilterReqs;
                    if (reqs.Count == 0 || !requiredItems.Contains(item))
                    {
                        return (null, null);
                    }
                }
                // Show hint if NOT (check self OR all other checks OR all alts)) AND all dependency conditions
                // Dependency conditions are (all checks OR all alts)
                // Don't care about bonfires for now
                List<AST.Expr> checks = new List<AST.Expr>();
                AST.Expr mainCheck = getItemCheck(item, reqs, true);
                if (mainCheck == null) throw new Exception($"Assertion error: {item} has no self-check???");
                checks.Add(new AST.BinaryExpr { Op = "==", Lhs = mainCheck, Rhs = AST.MakeVal(0) });
                foreach (string dep in includedItems[item])
                {
                    checks.Add(getItemCheck(dep, items[dep].FilterReqs, false));
                }
                // (item marker, area marker, boss marker, boss flag)
                if (itemMarker == 0) itemMarker = itemAreaMarker;
                if (itemAreaMarker == 0) itemAreaMarker = itemMarker;
                object[] machineArgs = new object[] { itemMarker, itemAreaMarker, hintArea.BossMarker, hintArea.BossFlag };
                return (chainExprs("&&", checks), machineArgs);
            }

            // 23331000 Donate <?evntAcquittalPrice?> runes
            int donateAmtMsg = 23331000;
            // 23331001 Don't donate runes
            int donateNoMsg = 23331001;
            // 23331003 Not enough runes to donate
            int donateLackMsg = 23331003;
            // 23241004 A red mark was made on the map
            int redMarkMsg = 23241004;

            int kaleBaseNewMsg = 28000050;
            int addMsg(Text text)
            {
                int msgId = kaleBaseNewMsg++;
                game.WriteFMGs = true;
                messages.SetFMGEntry(game.MenuFMGs, game.OtherMenuFMGs, "EventTextForTalk", msgId, text);
                return msgId;
            }
            int talkListMsg = addMsg(hintPurchase);
            int markNoMsg = addMsg(hintRequirementFailed);
            int markAreaMsg = addMsg(hintMarkArea);
            int markItemMsg = addMsg(hintMarkItem);
            int noItemsMsg = addMsg(hintNoItems);
            int markNoBossMsg = addMsg(hintRequirementFailedBoss);
            int markNoOptionMsg = addMsg(hintRequirementFailedOption);

            string kaleTalkId = "t800006000";
            ESD kale = null;
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                if (entry.Value.TryGetValue(kaleTalkId, out kale))
                {
                    game.WriteESDs.Add(entry.Key);
                    break;
                }
            }
            if (kale == null || !kale.StateGroups.TryGetValue(AST.MachineForIndex(51), out Dictionary<long, ESD.State> sellMachine))
            {
                throw new Exception($"Couldn't locate Kale's ESD for adding hints");
            }

            List<ESD.State> OpenOptionMenu(Dictionary<long, ESD.State> states, ESD.State state, List<int> msgs, ref long optId)
            {
                // c1_20 ClearTalkListData()
                state.EntryCommands.Add(AST.MakeCommand(1, 20));
                // c1_110 MainMenuFlag()
                state.EntryCommands.Add(AST.MakeCommand(1, 110));
                List<AST.Expr> condExprs = new List<AST.Expr>();
                for (int i = 0; i < msgs.Count; i++)
                {
                    // c1_19 AddTalkListData(id, msg, -1)
                    state.EntryCommands.Add(AST.MakeCommand(1, 19, i + 1, msgs[i], -1));
                    // f23 GetTalkListEntryResult()
                    condExprs.Add(new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f23"), Rhs = AST.MakeVal(i + 1) });
                }
                condExprs.Add(AST.Pass);
                // c1_76 OpenConversationChoicesMenu(0)
                state.EntryCommands.Add(AST.MakeCommand(1, 76, 0));
                // f59 f58 assert CheckSpecificPersonMenuIsOpen(12, 0) == 0 or CheckSpecificPersonGenericDialogIsOpen(0)
                AST.Expr waitExpr = new AST.BinaryExpr
                {
                    Op = "||",
                    Lhs = new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f59", 12, 0), Rhs = AST.MakeVal(0) },
                    Rhs = AST.MakeFunction("f58", 0),
                };
                (long selectStateId, ESD.State selectState) = AST.AllocateState(states, ref optId);
                state.Conditions.Add(new ESD.Condition(selectStateId, AST.AssembleExpression(waitExpr)));
                return AST.AllocateBranch(states, selectState, condExprs, ref optId);
            }

            void ShowDialog(ESD.State state, long nextId, int msg)
            {
                // c1_17 OpenGenericDialog
                state.EntryCommands.Add(AST.MakeCommand(1, 17, 7, msg, 1, 0, 1));
                // f58 CheckSpecificPersonGenericDialogIsOpen
                AST.Expr noDialogExpr = new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f58", 0), Rhs = AST.MakeVal(0) };
                state.Conditions.Add(new ESD.Condition(nextId, AST.AssembleExpression(noDialogExpr)));
            }

            // Just hardcode this one for now
            bool freehints = false;
#if DEBUG
            freehints = opt["freehints"];
#endif
            int reqFlag = freehints ? 6001 : 3063;
            long baseId = 50;
            {
                // Edit Kale's talk list data to add an option. Include an if/else for reaching Altus
                // Try to make this edit future-proof, by basing it on the structure of "About Kale" option in x51.
                // State 2: ClearTalkListData c1_20
                // State 3: AddTalkListData c1_19
                // State 5: GetTalkListEntryResult == 3 condition, go to 8 - f23
                // State 8,25: Call x55, go back to 2
                long kaleLoopId = -1;
                long kaleEntryId = -1;
                long kaleCheckId = -1;
                foreach (KeyValuePair<long, ESD.State> stateEntry in sellMachine)
                {
                    ESD.State state = stateEntry.Value;
                    if (state.EntryCommands.Any(c => c.CommandBank == 1 && c.CommandID == 20)) kaleLoopId = stateEntry.Key;
                    if (state.EntryCommands.Any(c => c.CommandBank == 1 && c.CommandID == 19)) kaleEntryId = stateEntry.Key;
                    foreach (ESD.Condition cond in state.Conditions)
                    {
                        bool found = false;
                        AST.AstVisitor talkListEntryVisitor = AST.AstVisitor.PostAct(expr =>
                        {
                            found |= expr is AST.FunctionCall call && call.Name == "f23";
                        });
                        AST.DisassembleExpression(cond.Evaluator).Visit(talkListEntryVisitor);
                        if (found)
                        {
                            kaleCheckId = stateEntry.Key;
                            break;
                        }
                    }
                }
                if (kaleLoopId == -1 || kaleEntryId == -1 || kaleCheckId == -1)
                {
                    throw new Exception($"Couldn't locate Kale's ESD states for adding hints ({kaleLoopId} {kaleEntryId} {kaleCheckId})");
                }
                // Add talk list entry
                ESD.CommandCall buyHintEntry = AST.MakeCommand(1, 19, 50, talkListMsg, -1);
                sellMachine[kaleEntryId].EntryCommands.Insert(1, buyHintEntry);
                // Make state with check for Altus and call to 120
                (long buyHintId, ESD.State buyHintState) = AST.AllocateState(sellMachine, ref baseId);
                List<AST.Expr> buyHintExprs = new List<AST.Expr> { AST.MakeFunction("f15", reqFlag), AST.Pass };
                List<ESD.State> buyHintAlts = AST.AllocateBranch(sellMachine, buyHintState, buyHintExprs, ref baseId);
                AST.CallMachine(buyHintAlts[0], kaleLoopId, 120);
                // (long buyHintCantId, ESD.State buyHintCantState) = AST.AllocateState(sellMachine, ref baseId);
                ShowDialog(buyHintAlts[1], kaleLoopId, markNoMsg);
                // f58 CheckSpecificPersonGenericDialogIsOpen
                // AST.Expr noDialogExpr = new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f58", 0), Rhs = AST.MakeVal(0) };
                // buyHintCantState.Conditions.Add(new ESD.Condition(kaleLoopId, AST.AssembleExpression(noDialogExpr)));
                // Add talk condition for state
                AST.Expr buyHintCond = new AST.BinaryExpr { Op = "==", Lhs = AST.MakeFunction("f23"), Rhs = AST.MakeVal(50) };
                sellMachine[kaleCheckId].Conditions.Insert(0, new ESD.Condition(buyHintId, AST.AssembleExpression(buyHintCond)));
            }

            (Dictionary<long, ESD.State>, ESD.State) AddMachine(int id)
            {
                baseId = 0;
                Dictionary<long, ESD.State> machine = new Dictionary<long, ESD.State>();
                kale.StateGroups[AST.MachineForIndex(id)] = machine;
                ESD.State startState = machine[0] = new ESD.State();
                return (machine, startState);
            }

            // Make a singleton machine which iterates through all items until it finds one it can hint
            {
                (Dictionary<long, ESD.State> checkMachine, ESD.State checkStartState) = AddMachine(120);
                (long checkReturnId, ESD.State checkReturnState) = AST.AllocateState(checkMachine, ref baseId);
                AST.CallReturn(checkReturnState, 0);
                List<AST.Expr> condExprs = new List<AST.Expr>();
                List<object[]> machineArgs = new List<object[]>();
                foreach (bool onlyRequired in new[] { true, false })
                {
                    foreach (string item in itemOrder)
                    {
                        (AST.Expr condExpr, object[] machineArg) = checkItemHint(item, onlyRequired);
                        if (condExpr != null)
                        {
                            condExprs.Add(condExpr);
                            machineArgs.Add(machineArg);
                        }
                    }
                }
                condExprs.Add(AST.Pass);
                List<ESD.State> states = AST.AllocateBranch(checkMachine, checkStartState, condExprs, ref baseId);
                for (int i = 0; i < states.Count; i++)
                {
                    if (i < machineArgs.Count)
                    {
                        AST.CallMachine(states[i], checkReturnId, 121, machineArgs[i]);
                    }
                    else
                    {
                        ShowDialog(states[i], checkReturnId, noItemsMsg);
                    }
                }
            }

            // Make a machine that offers a hint for an item location (item marker, area marker, boss marker, boss flag)
            // More boring (but reliable) procedural ESD writing
            {
                /*
                if no boss or boss defeated:
                    give choice of area (cheap) or item marker (expensive)
                else:
                    mark boss (cheap)
                */
                (Dictionary<long, ESD.State> markMachine, ESD.State markStartState) = AddMachine(121);
                AST.Expr itemMarkerArg = AST.MakeArg(0);
                AST.Expr areaMarkerArg = AST.MakeArg(1);
                AST.Expr bossMarkerArg = AST.MakeArg(2);
                AST.Expr bossFlagArg = AST.MakeArg(3);
                (long markReturnId, ESD.State markReturnState) = AST.AllocateState(markMachine, ref baseId);
                AST.CallReturn(markReturnState, 0);
                int cheap = freehints ? 2 : 200;
                int expensive = freehints ? 5 : 500;
                if (opt["markitems"])
                {
                    AST.Expr bossDefeatedExpr = new AST.BinaryExpr
                    {
                        Op = "||",
                        Lhs = new AST.BinaryExpr { Op = "==", Lhs = bossMarkerArg, Rhs = AST.MakeVal(0) },
                        Rhs = AST.MakeFunction("f15", bossFlagArg),
                    };
                    List<ESD.State> markAlts = AST.AllocateBranch(
                        markMachine, markStartState, new List<AST.Expr> { bossDefeatedExpr, AST.Pass }, ref baseId);
                    // Call them
                    // Choose between item and area
                    List<ESD.State> itemAlts = OpenOptionMenu(
                        markMachine, markAlts[0], new List<int> { markAreaMsg, markItemMsg, donateNoMsg }, ref baseId);
                    AST.CallMachine(itemAlts[0], markReturnId, 122, areaMarkerArg, cheap);
                    AST.CallMachine(itemAlts[1], markReturnId, 122, itemMarkerArg, expensive);
                    AST.CallState(itemAlts[2], markReturnId);
                    AST.CallState(itemAlts[3], markReturnId);
                    // Show boss location
                    // Except for now, for UI simplicity, just show it as a limited option.
                    List<ESD.State> bossAlts = OpenOptionMenu(
                        markMachine, markAlts[1], new List<int> { markAreaMsg, markItemMsg, donateNoMsg }, ref baseId);
                    AST.CallMachine(bossAlts[0], markReturnId, 122, bossMarkerArg, cheap);
                    ShowDialog(bossAlts[1], markReturnId, markNoBossMsg);
                    AST.CallState(bossAlts[2], markReturnId);
                    AST.CallState(bossAlts[3], markReturnId);
                }
                else
                {
                    List<ESD.State> areaAlts = OpenOptionMenu(
                        markMachine, markStartState, new List<int> { markAreaMsg, markItemMsg, donateNoMsg }, ref baseId);
                    AST.CallMachine(areaAlts[0], markReturnId, 122, areaMarkerArg, cheap);
                    ShowDialog(areaAlts[1], markReturnId, markNoOptionMsg);
                    AST.CallState(areaAlts[2], markReturnId);
                    AST.CallState(areaAlts[3], markReturnId);
                }
            }

            // Finally, a machine to pay some amount of runes for a marker (marker flag, rune amount multiplier)
            // Basically ripped off from Thops, but with more math
            {
                (Dictionary<long, ESD.State> payMachine, ESD.State payStartState) = AddMachine(122);
                AST.Expr markerFlagArg = AST.MakeArg(0);
                AST.Expr costMultArg = AST.MakeArg(1);
                (long payReturnId, ESD.State payReturnState) = AST.AllocateState(payMachine, ref baseId);
                AST.CallReturn(payReturnState, 0);
                (long payPromptId, ESD.State payPromptState) = AST.AllocateState(payMachine, ref baseId);
                // f104 GetPlayerStats(33 = SoulLvl)
                AST.Expr soulLevel = AST.MakeFunction("f104", 33);
                int minimumLevel = 50;
                AST.Expr soulLevelMin = new AST.BinaryExpr { Op = "<", Lhs = soulLevel, Rhs = AST.MakeVal(minimumLevel) };
                // 200, 500 mult
                List<ESD.State> payCostAlts = AST.AllocateBranch(
                    payMachine, payStartState, new List<AST.Expr> { soulLevelMin, AST.Pass }, ref baseId);
                // c1_100 SetWorkValue(id, value)
                payCostAlts[0].EntryCommands.Add(AST.MakeCommand(1, 100, 1, minimumLevel));
                payCostAlts[1].EntryCommands.Add(AST.MakeCommand(1, 100, 1, soulLevel));
                AST.CallState(payCostAlts[0], payPromptId);
                AST.CallState(payCostAlts[1], payPromptId);
                // f100 GetWorkValue(id)
                AST.Expr costExpr = new AST.BinaryExpr { Op = "*", Lhs = AST.MakeFunction("f100", 1), Rhs = costMultArg };
                // c1_102 SetMessageTagValue(0, costExpr)
                payPromptState.EntryCommands.Add(AST.MakeCommand(1, 102, 0, costExpr));
                // action:23331000:"Donate <?evntAcquittalPrice?> runes"
                // action:23331001:"Don't donate runes"
                List<ESD.State> menuAlts = OpenOptionMenu(payMachine, payPromptState, new List<int> { donateAmtMsg, donateNoMsg }, ref baseId);
                // f45 ComparePlayerStat(8, 4, amount)
                AST.Expr enoughMoney = AST.MakeFunction("f45", 8, 4, costExpr);
                List<ESD.State> payAlts = AST.AllocateBranch(payMachine, menuAlts[0], new List<AST.Expr> { enoughMoney, AST.Pass }, ref baseId);
                // c1_47 ChangePlayerStat(8, 1, amount)
                payAlts[0].EntryCommands.Add(AST.MakeCommand(1, 47, 8, 1, costExpr));
                // c1_33 SetEventFlagRange... this does not seem to work. Oh well.
                for (int flag = startMarkerFlag; flag < markerFlag; flag++)
                {
                    payAlts[0].EntryCommands.Add(AST.MakeCommand(1, 11, flag, 0));
                }
                // c1_11 SetEventFlag
                payAlts[0].EntryCommands.Add(AST.MakeCommand(1, 11, markerFlagArg, 1));
                ShowDialog(payAlts[0], payReturnId, redMarkMsg);
                ShowDialog(payAlts[1], payReturnId, donateLackMsg);
                AST.CallState(menuAlts[1], payReturnId);
                AST.CallState(menuAlts[2], payReturnId);
            }

            /*
            use a topological sort of items, so that a hint for any will imply all of the previous are held.

            the first pass, only do required items.
            the second pass, do all other items and assume all checkflag conditions apply.

            foreach item in key items:
                if item not acquired and all prerequisite items are met (or excluded, if they're technically optional):
                    if no boss or boss defeated:
                        give choice of area (cheap) or item marker (expensive)
                    else:
                        mark boss (cheap)

            possible marking strategy:
                go through every single location in the area which is eligible with the current settings and mark the first one
            possible item exploration pass:
                foreach item in key items:
                    if item acquired and all post-item area prerequisites met and no bonfires in post-item area (if any defined):
                        suggest area
            */
        }

        internal void DumpMinidungeonMarkers()
        {
            List<HintArea> areas = new List<HintArea>();
            Dictionary<string, SortedSet<int>> mapBosses = new Dictionary<string, SortedSet<int>>();
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                if (!ann.Slots.TryGetValue(entry.Key, out SlotAnnotation slotAnn)) continue;
                if (slotAnn.HasTag("altboss") && slotAnn.HasTag("minidungeon"))
                {
                    foreach (SlotKey key in entry.Value)
                    {
                        ItemLocation itemLoc = data.Location(key);
                        foreach (LocationKey loc in itemLoc.Keys)
                        {
                            foreach (EntityId id in loc.Entities)
                            {
                                if (id.Type == "event enemy" && id.EntityID > 0)
                                {
                                    AddMulti(mapBosses, id.MapName, id.EntityID);
                                }
                            }
                        }
                    }
                }
            }
            foreach (AreaAnnotation annArea in ann.Areas.Values)
            {
                if (annArea.Tags == null || !annArea.Tags.Contains("minidungeon")) continue;
                string area = annArea.Name;
                string req = area == "snowfield_hiddenpath" ? "snowfield" : annArea.Req.Split(' ')[0];
                string bonfire = annArea.Text.Split('-').Last().Trim();
                HintArea mark = new HintArea
                {
                    Includes = annArea.Name,
                    MainBonfire = bonfire,
                    Parent = req,
                    Boss = mapBosses.TryGetValue(annArea.Maps, out SortedSet<int> ids) ? string.Join(" ", ids) : "?????",
                };
                areas.Add(mark);
            }
            Console.WriteLine(GameData.Serializer.Serialize(areas));

        }

        internal void AnalyzeMapPoints()
        {
            // Some attempts to understand fields in WorldMapPointParam.
            // Update this with cell row names if it'll ever be useful again.
            FMG placeFmg = game.ItemFMGs["PlaceName"];
            FMG npcFmg = game.ItemFMGs["NpcName"];
            Dictionary<string, object> ignoreCells =
                game.Params["WorldMapPointParam"].Rows[0].Cells.ToDictionary(c => c.Def.InternalName, c => c.Value);
            foreach (PARAM.Row row in game.Params["WorldMapPointParam"].Rows)
            {
                foreach (KeyValuePair<string, object> cell in ignoreCells.ToList())
                {
                    if (cell.Value == null) continue;
                    if (!row[cell.Key].Equals(cell.Value))
                    {
                        ignoreCells[cell.Key] = null;
                    }
                }
            }
            Dictionary<int, string> descs = new Dictionary<int, string>();
            SortedDictionary<string, List<int>> valueGroups = new SortedDictionary<string, List<int>>();
            string groupBy = "IconType";
            foreach (PARAM.Row row in game.Params["WorldMapPointParam"].Rows)
            {
                string formatCell(PARAM.Cell cell)
                {
                    string key = cell.Def.InternalName;
                    if (ignoreCells[key] != null) return null;
                    object val = cell.Value;
                    string disp = val.ToString();
                    if (key == "MapX" || key == "MapY" || key == "MapZ") return null;
                    if (val is int x && x <= 0) return null;
                    if (val is uint y && y == 0) return null;
                    if (key.StartsWith("MapId"))
                    {
                        if (key != "MapId1") return null;
                        List<byte> mapParts = Enumerable.Range(1, 4).Select(i => (byte)row[$"MapId{i}"].Value).ToList();
                        disp = game.MapLocationName(formatMap(mapParts));
                    }
                    else if (key.Contains("NameId") && val is int nameId && nameId > 0)
                    {
                        string place = placeFmg[nameId];
                        disp = place != null ? $"place:{place}" : $"name:{npcFmg[nameId]}";
                    }
                    else if (key == "IconType")
                    {
                        disp = ((MarkerType)(short)val).ToString().Replace("_", "");
                        if (!int.TryParse(disp, out _))
                        {
                            disp = $"{val}:{disp}";
                        }
                    }
                    return $"{key}={disp}";
                }
                descs[row.ID] = string.Join(", ", row.Cells.Select(formatCell).Where(t => t != null));
                if (groupBy != null)
                {
                    object by = row[groupBy].Value;
                    // string byStr = $"{(short)by:d2}";
                    string byStr = by.ToString();
                    AddMulti(valueGroups, byStr, row.ID);
                }
            }
            foreach (KeyValuePair<string, List<int>> entry in valueGroups)
            {
                Console.WriteLine($"------- {groupBy} = {entry.Key}");
                foreach (int val in entry.Value) Console.WriteLine($"> {val}: {descs[val]}");
            }
        }
    }
}
