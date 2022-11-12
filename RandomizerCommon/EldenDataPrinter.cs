using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static SoulsFormats.EMEVD.Instruction;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.LocationKey;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class EldenDataPrinter
    {
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
            Cave = 13,
            Tunnel = 14,
            Well = 15,
            Grave = 16,
            Rise = 17,
            Fort = 18,
            Pasture = 19,
            Lift = 21,
            Divine_Tower = 23,
            Erdtree = 30,
            Lake_Town = 32,
            Underground = 46,
            Underground_Ruins = 47,
            Red_Mark = 87,
        }

        public static void WriteHTMLHeader(string title)
        {
            Console.Write($"<html><head><meta charset=\"UTF-8\"><title>{System.Net.WebUtility.HtmlEncode(title)}</title></head><body style=\"white-space: pre-wrap; font-size: 120%; margin: 1em;\">");
        }

        // TODO: Move this to a different project which can also dump all files at once
        public void PrintData(GameData game, RandomizerOptions opt)
        {
#if DEBUG
            string getModelName(string name)
            {
                if (name.StartsWith("m"))
                {
                    name = name.Split('-').Last();
                }
                name = name.Split('_')[0];
                return name;
            }
            Dictionary<string, MSBE> maps = game.EldenMaps;
            string partDetails(MSBE.Part part, bool includeEntity)
            {
                string extra = "";
                if (part is MSBE.Part.Enemy e)
                {
                    if (e.CollisionPartName != null) extra += $" col {e.CollisionPartName}";
                    if (e.NPCParamID > 0) extra += $" npc {e.NPCParamID}";
                    if (e.ThinkParamID > 0) extra += $" think {e.ThinkParamID}";
                    if (e.CharaInitID > 0) extra += $" chara {e.CharaInitID}";
                    if (e.TalkID > 0) extra += $" talk {e.TalkID}";
                }
                if (part is MSBE.Part.ConnectCollision con)
                {
                    if (con.MapID != null) extra += $" con {string.Join("_", con.MapID)}";
                }
                if (includeEntity && part.EntityID > 0) extra += $" id {part.EntityID}";
                List<uint> groupIDs = part.EntityGroupIDs.Where(groupID => groupID > 0).ToList();
                if (groupIDs.Count > 0) extra += $" group {string.Join(",", groupIDs)}";
                return extra;
            }
            if (opt["dumpid"])
            {
                if (opt["html"]) WriteHTMLHeader("Elden Ring Entity ID List");
                foreach (KeyValuePair<string, MSBE> entry in maps)
                {
                    MSBE msb = entry.Value;
                    string mapId = entry.Key;
                    string mapSuffix = "";
                    if (game.LocationNames.TryGetValue(mapId, out string mapName)) mapSuffix = $" ({mapName})";
                    foreach (MSBE.Part part in msb.Parts.GetEntries())
                    {
                        if (opt["dumpall"] || part.EntityID > 0 || part.EntityGroupIDs.Any(g => g > 100 && g != 34116150 && g != 34116160))
                        {
                            int charaId = part is MSBE.Part.EnemyBase e ? e.CharaInitID : 0;
                            Console.WriteLine($"{part.EntityID}: {mapId} {part.Name}{mapSuffix} {part.GetType()} ({game.ModelCharacterName(getModelName(part.Name), charaId)}){partDetails(part, false)}");
                        }
                    }
                    foreach (MSBE.Region region in msb.Regions.GetEntries())
                    {
                        if (opt["dumpall"] || region.EntityID > 1000)
                        {
                            Console.WriteLine($"{region.EntityID}: {entry.Key}{mapSuffix} {region.GetType()} {region.Name}");
                        }
                    }
                    foreach (MSBE.Event ev in msb.Events.GetEntries())
                    {
                        if (opt["dumpall"] || ev.EntityID > 1000)
                        {
                            Console.WriteLine($"{ev.EntityID}: {entry.Key}{mapSuffix} {ev.GetType()} {ev.Name}");
                        }
                    }
                }
            }
            if (opt["dumpchr"])
            {
                if (opt["html"]) WriteHTMLHeader("Elden Ring Enemy Locations List");
                SortedDictionary<string, SortedSet<string>> chrs = new SortedDictionary<string, SortedSet<string>>();
                foreach (KeyValuePair<string, MSBE> entry in maps)
                {
                    MSBE msb = entry.Value;
                    string mapId = entry.Key;
                    string mapSuffix = "";
                    if (game.LocationNames.TryGetValue(mapId, out string mapName)) mapSuffix = $" ({mapName})";
                    foreach (MSBE.Part.Enemy part in msb.Parts.Enemies)
                    {
                        string modelName = getModelName(part.Name);
                        string chr = modelName == "c0000" ? " " + game.ModelCharacterName(getModelName(part.Name), part.CharaInitID) + " -" : "";
                        AddMulti(chrs, modelName, $"{mapId} {part.Name}{mapSuffix}:{chr}{partDetails(part, true)}");
                    }
                }
                Dictionary<string, string> modelNames = game.Editor.LoadNames("ModelNameNT", n => n, true);
                foreach (KeyValuePair<string, SortedSet<string>> entry in chrs)
                {
                    string modelText = modelNames.TryGetValue(entry.Key, out string name) ? $" ({name})" : "";
                    Console.WriteLine($"--- {entry.Key}{modelText} - {game.ModelName(entry.Key)}");
                    foreach (string part in entry.Value)
                    {
                        Console.WriteLine($"{part}");
                    }
                }
            }
            if (opt["dumpcons"])
            {
                GameEditor g = new GameEditor(GameSpec.FromGame.ER);
                string formatCon(IEnumerable<byte> bytes) => "m" + string.Join("_", bytes.Select(b => b == 0xFF ? "XX" : $"{b:d2}"));
                foreach (KeyValuePair<string, MSBE> entry in maps)
                {
                    MSBE msb = entry.Value;
                    foreach (MSBE.Part.ConnectCollision part in msb.Parts.ConnectCollisions)
                    {
                        Console.WriteLine($"{entry.Key} {formatCon(part.MapID)}");
                    }
                }
            }
            if (opt["dumpplayers"])
            {
                GameEditor g = new GameEditor(GameSpec.FromGame.ER);
                Dictionary<string, FMG> itemFmg = g.LoadBnd($@"{g.Spec.GameDir}\msg\engus\item.msgbnd.dcx", (b, path) => FMG.Read(b));
                Dictionary<string, FMG> menuFmg = g.LoadBnd($@"{g.Spec.GameDir}\msg\engus\menu.msgbnd.dcx", (b, path) => FMG.Read(b));
                FMG placeFmg = itemFmg["PlaceName"];
                FMG warpFmg = menuFmg["GR_MenuText"];
                Dictionary<int, string> cats = new Dictionary<int, string>();
                foreach (PARAM.Row row in game.Params["BonfireWarpSubCategoryParam"].Rows)
                {
                    string name = warpFmg[(int)row["textId"].Value];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int tab = (ushort)row["tabId"].Value;
                    cats[row.ID] = name;
                }
                Dictionary<uint, string> bonfireObjs = new Dictionary<uint, string>();
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    string name = placeFmg[(int)row["textId1"].Value];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int cat = (int)row["bonfireSubCategoryId"].Value;
                    uint obj = (uint)row["bonfireEntityId"].Value;
                    string fullName = name;
                    if (cats.TryGetValue(cat, out string subName))
                    {
                        fullName = $"{name} ({subName})";
                    }
                    bonfireObjs[obj] = name;
                }

                EldenCoordinator coord = new EldenCoordinator(game, false);
                foreach (KeyValuePair<string, MSBE> entry in maps)
                {
                    if (entry.Key.StartsWith("m60") && (entry.Key.EndsWith("2") || entry.Key.EndsWith("1"))) continue;
                    MSBE msb = entry.Value;
                    bool found = false;
                    Console.WriteLine($"--- {game.MapLocationName(entry.Key)}  ");
                    foreach (MSBE.Part.Player part in msb.Parts.Players)
                    {
                        string bonfire = null;
                        uint player = part.EntityID;
                        if (part.EntityID > 0)
                        {
                            uint obj = player + 970;
                            bonfireObjs.TryGetValue(obj, out bonfire);
                        }
                        string ent = player > 0 ? $" #{player}" : "";
                        string warp = bonfire == null ? "" : $" - warp for {bonfire}";
                        Console.WriteLine($"Player {part.Name}{ent} ({part.Position.X}, {part.Position.Y}, {part.Position.Z}): {coord.ClosestLandmark(entry.Key, part.Position)}{warp}  ");
                        found = true;
                    }
                    if (!found)
                    {
                        Console.WriteLine("No players found  ");
                    }
                    Console.WriteLine();
                }
                // Other cases attempt to use a coordinate calculation routine to try to find the nearest landmark
                // (clock facing is a decimal number from 0 to 12, starting from north in clockwise order).
                // All map players are in the listed map, even if they are closer to a landmark in a different map.
            }
            if (opt["dumpchara"])
            {
                GameEditor g = new GameEditor(GameSpec.FromGame.ER);
                Dictionary<string, FMG> itemFmg = g.LoadBnd($@"{g.Spec.GameDir}\msg\engus\item.msgbnd.dcx", (b, path) => FMG.Read(b));
                FMG npcFmg = itemFmg["NpcName"];
                SortedDictionary<int, SortedSet<string>> names = new SortedDictionary<int, SortedSet<string>>();
                foreach (KeyValuePair<string, MSBE> entry in maps)
                {
                    MSBE msb = entry.Value;
                    foreach (MSBE.Part.Enemy part in msb.Parts.Enemies)
                    {
                        if (part.CharaInitID > 0)
                        {
                            PARAM.Row npcRow = game.Params["NpcParam"][part.NPCParamID];
                            if (npcRow != null)
                            {
                                string name = npcFmg[(int)npcRow["nameId"].Value];
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    AddMulti(names, part.CharaInitID, name);
                                }
                            }
                        }
                    }
                }
                foreach (KeyValuePair<int, SortedSet<string>> entry in names)
                {
                    Console.WriteLine($"{entry.Key} {string.Join(" | ", entry.Value)}");
                }
            }
            if (opt["dumpflag"])
            {
                foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
                {
                    foreach (EMEVD.Event ev in entry.Value.Events)
                    {
                        int boss = 0;
                        for (int j = 0; j < ev.Instructions.Count; j++)
                        {
                            EMEVD.Instruction ins = ev.Instructions[j];
                            if (ins.Bank == 2003 && ins.ID == 12)
                            {
                                // Boss defeat
                                List<object> args = ins.UnpackArgs(new List<ArgType> { ArgType.Int32, ArgType.Byte });
                                boss = (int)args[0];
                            }
                            if (boss > 0 && ins.Bank == 2003 && ins.ID == 66)
                            {
                                List<object> args = ins.UnpackArgs(new List<ArgType> { ArgType.Byte, ArgType.UInt32, ArgType.Byte });
                                int flag = (int)(uint)args[1];
                                if (flag >= 9100 && flag < 9300)
                                {
                                    Console.WriteLine($"  {flag}: {boss}");
                                }
                            }
                        }
                    }
                }
            }
            if (opt["dumpname"])
            {
                List<string> nonMaps = new List<string>
                {
                    "m34_16_00_00", // Non-existent divine tower
                    "m32_00_00_01", // Morne Tunnel is just _00, does it matter?
                };
                List<int> misplacedMapBonfires = new List<int>
                {
                    301700, 301800, 301900, // These belong to the catacombs below
                };
                string formatMap(IEnumerable<byte> bytes, bool checkExists = true)
                {
                    List<byte> parts = bytes.ToList();
                    // Currently, only last indices are left as XX. These all happen to be 00 though (01/02 do not use cols)
                    if (parts[3] == 0xFF)
                    {
                        parts[3] = 0;
                    }
                    string mapId = "m" + string.Join("_", parts.Select(b => b == 0xFF ? "XX" : $"{b:d2}"));
                    if (checkExists && !maps.ContainsKey(mapId) && !nonMaps.Contains(mapId))
                    {
                        throw new Exception($"Map {mapId} not found");
                    }
                    return mapId;
                }
                // Not sure if menu fmgs will be used in the randomizer so load from game dir
                GameEditor g = new GameEditor(GameSpec.FromGame.ER);
                Dictionary<string, FMG> itemFmg = g.LoadBnd($@"{g.Spec.GameDir}\msg\engus\item.msgbnd.dcx", (b, path) => FMG.Read(b));
                Dictionary<string, FMG> menuFmg = g.LoadBnd($@"{g.Spec.GameDir}\msg\engus\menu.msgbnd.dcx", (b, path) => FMG.Read(b));
                FMG placeFmg = itemFmg["PlaceName"];
                FMG warpFmg = menuFmg["GR_MenuText"];
                Dictionary<int, string> tabs = new Dictionary<int, string>();
                foreach (PARAM.Row row in game.Params["BonfireWarpTabParam"].Rows)
                {
                    string name = warpFmg[(int)row["textId"].Value];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    tabs[row.ID] = name;
                }
                // TODO: make a list of strings? the only goal here is making a full name mapping though
                Dictionary<int, List<string>> cats = new Dictionary<int, List<string>>();
                foreach (PARAM.Row row in game.Params["BonfireWarpSubCategoryParam"].Rows)
                {
                    string name = warpFmg[(int)row["textId"].Value];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int tab = (ushort)row["tabId"].Value;
                    if (tabs.TryGetValue(tab, out string subName))
                    {
                        AddMulti(cats, row.ID, subName);
                    }
                    AddMulti(cats, row.ID, name);
                    if (opt["dumpbonfire"]) Console.WriteLine($"{row.ID} {string.Join("; ", cats[row.ID])}");
                }
                Dictionary<uint, List<string>> bonfires = new Dictionary<uint, List<string>>();
                Dictionary<string, List<uint>> mapBonfires = new Dictionary<string, List<uint>>();
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    string name = placeFmg[(int)row["textId1"].Value];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int cat = (int)row["bonfireSubCategoryId"].Value;
                    uint obj = (uint)row["bonfireEntityId"].Value;
                    if (cats.TryGetValue(cat, out List<string> subNames))
                    {
                        AddMulti(bonfires, obj, subNames);
                    }
                    AddMulti(bonfires, obj, name);
                    string mapId = formatMap(game.GetMapParts(row));
                    if (opt["dumpbonfire"]) Console.WriteLine($"{row.ID} {mapId}: {string.Join(" - ", bonfires[obj])}");
                    // if (misplacedMapBonfires.Contains(obj)) continue;
                    AddMulti(mapBonfires, mapId, obj);
                }
                Dictionary<string, List<uint>> msbBonfires = new Dictionary<string, List<uint>>();
                HashSet<uint> dungeonBonfires = new HashSet<uint>();
                foreach (KeyValuePair<string, MSBE> entry in maps)
                {
                    MSBE msb = entry.Value;
                    foreach (MSBE.Part.Asset part in msb.Parts.Assets)
                    {
                        if (bonfires.ContainsKey(part.EntityID))
                        {
                            AddMulti(msbBonfires, entry.Key, part.EntityID);
                            if (!entry.Key.StartsWith("m60"))
                            {
                                dungeonBonfires.Add(part.EntityID);
                            }
                        }
                    }
                }

                Dictionary<string, List<string>> mapMarkers = new Dictionary<string, List<string>>();
                HashSet<int> uniqueMarkerTypes = new HashSet<int>();
                foreach (PARAM.Row row in game.Params["WorldMapPointParam"].Rows)
                {
                    int nameId = (int)row["textId1"].Value;
                    // if (nameId == -1) nameId = (int)row["TempNameId"].Value;
                    string name = placeFmg[nameId];
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if ((byte)row["areaNo"].Value == 0) continue;
                    string mapId = formatMap(game.GetMapParts(row));
                    int type = (ushort)row["iconId"].Value;
                    string typeStr = ((MarkerType)type).ToString().Replace('_', ' ');
                    if (int.TryParse(typeStr, out _))
                    {
                        if (!uniqueMarkerTypes.Add(type)) throw new Exception($"Multiple instances of unnamed marker type {type}");
                        typeStr = "Unique";
                    }
                    string fullName = $"{name} ({typeStr})";
                    AddMulti(mapMarkers, mapId, fullName);
                    if (opt["dumpbonfire"])
                    {
                        bool hasBonfire = bonfires.Any(b => b.Value.Last() == name);
                        Console.WriteLine($"{row.ID}: Bonfire {hasBonfire}: {(MarkerType)type} ({name} - {mapId})");
                    }
                }
                string plural(int amt, string thing)
                {
                    return amt == 1 ? $"{amt} {thing}" : $"{amt} {thing}s";
                }
                if (opt["structure"])
                {
                    bool unknownTodo = false;
                    // For m10 m20 legacy dungeons, define basic names. For m30 range, use obj bonfires (or manual names otherwise)
                    Dictionary<string, string> mapNames = game.Editor.LoadNames("MapName", n => n);
                    if (!unknownTodo)
                    {
                        mapNames = mapNames.Where(e => !e.Value.Contains("TODO")).ToDictionary(e => e.Key, e => e.Value);
                    }
                    // Exclude 01 and 00 tile names for now, as they're too busy
                    // mapNames = mapNames.Where(e => !e.Key.StartsWith("m60") || !e.Key.EndsWith("1")).ToDictionary(e => e.Key, e => e.Value);
                    // Fill in m30 map names
                    foreach (KeyValuePair<string, MSBE> entry in maps)
                    {
                        string mapId = entry.Key;
                        if ((mapId.StartsWith("m1") || mapId.StartsWith("m2")) && !mapNames.ContainsKey(mapId))
                        {
                            // This may be fine? If the map is trivial
                            // throw new Exception($"No manually defined map name for {mapId}");
                        }
                        if (!mapId.StartsWith("m3") || mapNames.ContainsKey(mapId))
                        {
                            continue;
                        }
                        if (!msbBonfires.TryGetValue(mapId, out List<uint> bonfireIds)) throw new Exception($"No bonfires in m3 map {mapId}");
                        // Divine Towers and m32_07_00_00 Gael Tunnel are the only manual ones so far
                        if (bonfireIds.Count != 1) throw new Exception($"Too many bonfires in m3 map {mapId}");
                        mapNames[mapId] = bonfires[bonfireIds[0]].Last();
                    }
                    // Other MSB processing, mainly connect cols
                    Dictionary<string, SortedSet<string>> connects = new Dictionary<string, SortedSet<string>>();
                    HashSet<string> goodMaps = new HashSet<string>();
                    HashSet<string> badMaps = new HashSet<string> { "m60_44_51_00" };
                    // Additional trivial ones (probably):
                    // m60_44_51_00 (capital tile)
                    // m60_36_53_00 (outside volcano manor) has rune [6]?
                    // m60_37_52_00 has Volcano Manor connect col?

                    List<Type> goodTypes = new List<Type> { typeof(MSBE.Part.Enemy), typeof(MSBE.Part.Asset), typeof(MSBE.Part.Player) };
                    foreach (KeyValuePair<string, MSBE> entry in maps)
                    {
                        MSBE msb = entry.Value;
                        string mapId = entry.Key;
                        foreach (MSBE.Part.ConnectCollision part in msb.Parts.ConnectCollisions)
                        {
                            AddMulti(connects, mapId, formatMap(part.MapID));
                        }
                        if (mapId.StartsWith("m60") && mapId.EndsWith("0") && !badMaps.Contains(mapId))
                        {
                            if (msb.Parts.GetEntries().Any(p => goodTypes.Contains(p.GetType())))
                            {
                                goodMaps.Add(mapId);
                            }
                            // else Console.WriteLine($"bad map: {mapId}");
                        }
                    }
                    // Structure: Big map, medium map, little map, if any cols/enemies/assets.
                    List<string> submapOrder = new List<string> { "SW", "NW", "SE", "NE" };
                    List<string> getSubmaps(string mapId)
                    {
                        List<byte> parts = mapId.TrimStart('m').Split('_').Select(p => byte.Parse(p)).ToList();
                        byte baseCol = parts[1];
                        byte baseRow = parts[2];
                        parts[3] -= 1;
                        List<string> ret = new List<string>();
                        for (int col = 0; col <= 1; col++)
                        {
                            for (int row = 0; row <= 1; row++)
                            {
                                parts[1] = (byte)(baseCol * 2 + col);
                                parts[2] = (byte)(baseRow * 2 + row);
                                ret.Add(formatMap(parts, checkExists: false));
                            }
                        }
                        return ret;
                    }
                    foreach (KeyValuePair<string, MSBE> entry in maps.OrderByDescending(k => k.Key))
                    {
                        string mapId = entry.Key;
                        if (mapId.StartsWith("m60") && !mapId.EndsWith("0") && getSubmaps(mapId).Any(m => goodMaps.Contains(m)))
                        {
                            goodMaps.Add(mapId);
                        }
                    }
                    bool onlyGood = true;
                    HashSet<uint> usedBonfires = new HashSet<uint>();
                    (string, string) getBonfireParts(uint id)
                    {
                        List<string> parts = bonfires[id];
                        return (string.Join(" - ", parts.Take(parts.Count - 1).Distinct()), parts.Last());
                    }
                    string oneLineBonfires(List<uint> ids)
                    {
                        foreach (uint id in ids)
                        {
                            if (!usedBonfires.Add(id)) throw new Exception($"Double-counted bonfire {id} ({string.Join(" > ", bonfires[id])})");
                        }
                        // Even simpler
                        if (ids.Count == 1)
                        {
                            return string.Join(" - ", bonfires[ids[0]].Distinct());
                        }
                        else
                        {
                            List<(string, string)> pairs = ids.Select(getBonfireParts).OrderBy(e => e.Item1).ToList();
                            return string.Join("; ", pairs.GroupBy(e => e.Item1).Select(e => {
                                if (e.Count() == 1)
                                {
                                    return $"{e.First().Item1} - {e.First().Item2}";
                                }
                                return $"{e.Key} ({string.Join("; ", e.Select(f => f.Item2))})";
                            }));
                        }
                    }
                    void printNames(string prefix, string one, string many, List<string> names)
                    {
                        if (names.Count == 1)
                        {
                            Console.WriteLine($"{prefix}{one}: {names[0]}");
                        }
                        else
                        {
                            Console.WriteLine($"{prefix}{many}:");
                            foreach (string name in names)
                            {
                                Console.WriteLine($" {prefix}{name}");
                            }
                        }
                    }
                    void printBonfires(string prefix, List<uint> ids)
                    {
                        List<string> names = ids.Select(getBonfireParts).Select(e => $"{e.Item1} - {e.Item2}").ToList();
                        printNames(prefix, "Site of Grace", "Sites of Grace", names);
                        usedBonfires.UnionWith(ids);
                    }
                    bool nest = !opt["flat"];
                    bool wiki = true;
                    string mapAnchor(string map)
                    {
                        if (!wiki) return map;
                        return $"[[# {map}]] {map}";
                    }
                    string mapLink(string map)
                    {
                        if (!wiki) return map;
                        return $"[#{map} {map}]";
                    }
                    string getTileName(string mapId)
                    {
                        mapNames.TryGetValue(mapId, out string mapName);
                        if (mapName == null) mapName = unknownTodo ? "TODO" : "";
                        if (!goodMaps.Contains(mapId)) mapName += " XXX";
                        return mapName;
                    }
                    // For dungeons: obj bonfires, connect cols
                    foreach (KeyValuePair<string, MSBE> entry in maps)
                    {
                        string mapId = entry.Key;
                        if (!mapNames.TryGetValue(entry.Key, out string mapName) || mapId.StartsWith("m60")) continue;
                        if (nest)
                        {
                            Console.WriteLine($"* {mapAnchor(mapId)}: {mapName}");
                            if (msbBonfires.TryGetValue(mapId, out List<uint> bids))
                            {
                                printBonfires(" * ", bids);
                            }
                            if (mapMarkers.TryGetValue(mapId, out List<string> markers))
                            {
                                printNames(" * ", "Marker", "Markers", markers);
                            }
                            if (connects.TryGetValue(mapId, out SortedSet<string> cons))
                            {
                                List<string> conNames = cons
                                    .Where(c => maps.ContainsKey(c))
                                    .Select(c => mapLink(c) + (!c.StartsWith("m60") && mapNames.TryGetValue(c, out string d) ? $" ({d})" : ""))
                                    .ToList();
                                if (conNames.Count > 0)
                                {
                                    Console.WriteLine($" * Connects to {string.Join(", ", conNames)}");
                                }
                            }
                        }
                        else
                        {
                            if (msbBonfires.TryGetValue(mapId, out List<uint> bids))
                            {
                                oneLineBonfires(bids);  // Mark as used
                            }
                            Console.WriteLine($"{mapId} {mapName}");
                        }
                    }
                    foreach (KeyValuePair<string, MSBE> entry in maps)
                    {
                        string bigMapId = entry.Key;
                        if (!(bigMapId.StartsWith("m60") && bigMapId.EndsWith("2"))) continue;
                        if (onlyGood && !goodMaps.Contains(bigMapId)) continue;
                        if (nest)
                        {
                            Console.WriteLine($"* {mapAnchor(bigMapId)}: {getTileName(bigMapId)}");
                        }
                        else
                        {
                            Console.WriteLine($"{bigMapId} {getTileName(bigMapId)}");
                        }
                        List<string> subMapIds = getSubmaps(bigMapId);
                        for (int i = 0; i < 4; i++)
                        {
                            string medMapId = subMapIds[i];
                            if (!maps.ContainsKey(medMapId)) continue;
                            if (onlyGood && !goodMaps.Contains(medMapId)) continue;
                            if (nest)
                            {
                                Console.WriteLine($" * ({submapOrder[i]}) {mapAnchor(medMapId)}: {getTileName(medMapId)}");
                            }
                            else
                            {
                                Console.WriteLine($"{medMapId} {getTileName(medMapId)}");
                            }
                            List<string> subsubMapIds = getSubmaps(medMapId);
                            for (int j = 0; j < 4; j++)
                            {
                                string mapId = subsubMapIds[j];
                                if (!maps.ContainsKey(mapId)) continue;
                                if (onlyGood && !goodMaps.Contains(mapId)) continue;
                                if (nest)
                                {
                                    Console.WriteLine($"  * ({submapOrder[j]}) {mapAnchor(mapId)}: {getTileName(mapId)}"); // {getTileName(mapId)}
                                }
                                // For little maps, list markers, including point bonfires (crossref with marker), and connect cols
                                List<string> flatInfo = new List<string>();
                                if (mapBonfires.TryGetValue(mapId, out List<uint> bids))
                                {
                                    bids = bids.Where(b => !dungeonBonfires.Contains(b)).ToList();
                                    if (bids.Count > 0)
                                    {
                                        if (nest)
                                        {
                                            printBonfires("   * ", bids);
                                        }
                                        else
                                        {
                                            flatInfo.Add(oneLineBonfires(bids));
                                        }
                                    }
                                }
                                if (mapMarkers.TryGetValue(mapId, out List<string> markers))
                                {
                                    if (nest)
                                    {
                                        printNames("   * ", "Marker", "Markers", markers);
                                    }
                                    else
                                    {
                                        flatInfo.AddRange(markers);
                                    }
                                }
                                if (connects.TryGetValue(mapId, out SortedSet<string> cons))
                                {
                                    List<string> conNames = cons
                                        .Where(c => mapNames.ContainsKey(c) && connects.ContainsKey(c) && connects[c].Contains(mapId))
                                        .Select(c => $"{mapLink(c)} ({mapNames[c]})")
                                        .ToList();
                                    if (conNames.Count > 0)
                                    {
                                        if (nest)
                                        {
                                            Console.WriteLine($"   * Connects to {string.Join(", ", conNames)}");
                                        }
                                        else
                                        {
                                            flatInfo.AddRange(conNames);
                                        }
                                    }
                                }
                                if (!nest)
                                {
                                    string info = getTileName(mapId);
                                    if (info.Contains("TODO")) info = $"TODO {string.Join("; ", flatInfo)}";
                                    Console.WriteLine($"{mapId} {info}");
                                }
                            }
                        }
                    }
                    List<uint> unusedBonfires = bonfires.Keys.ToList();
                    unusedBonfires.RemoveAll(b => usedBonfires.Contains(b));
                    foreach (uint id in unusedBonfires)
                    {
                        Console.WriteLine($"Unused bonfire: {id} {string.Join(" - ", bonfires[id])}");
                    }
                    if (unusedBonfires.Count > 0) throw new Exception($"Unused bonfires: {string.Join(", ", unusedBonfires)}");
                }
            }
#endif
        }
    }
}
