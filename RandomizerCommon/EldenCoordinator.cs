using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class EldenCoordinator
    {
        // Map id to (world X tile, world Z tile, offset)
        private Dictionary<string, (int, int, Vector3)> dungeonOffsets = new Dictionary<string, (int, int, Vector3)>();
        // Map from (world X tile, world Z tile) to list of (name, global coord)
        private Dictionary<string, string> bonfireShortNames = new Dictionary<string, string>();
        // Map from (world X tile, world Z tile) to (name, position)
        private Dictionary<(int, int), List<(string, Vector3)>> landmarks = new Dictionary<(int, int), List<(string, Vector3)>>();
        // Map from map id to landmarks inaccessible from there
        private Dictionary<string, HashSet<string>> cutOffLandmarks = new Dictionary<string, HashSet<string>>();
        // Map from map id to landmarks *only* accessible there
        private Dictionary<string, HashSet<string>> isolatedLandmarks = new Dictionary<string, HashSet<string>>();
        private HashSet<string> allIsolatedLandmarks = new HashSet<string>();

        private static readonly List<string> srcParts = new List<string> { "srcAreaNo", "srcGridXNo", "srcGridZNo" };
        private static readonly List<string> dstParts = new List<string> { "dstAreaNo", "dstGridXNo", "dstGridZNo" };

        private static Vector3 GetPos(PARAM.Row row, string type)
        {
            return new Vector3((float)row[$"{type}X"].Value, (float)row[$"{type}Y"].Value, (float)row[$"{type}Z"].Value);
        }
        private string Vec(Vector3 v) => $"({v.X}, {v.Y}, {v.Z})";

        public EldenCoordinator(GameData game, bool debugCoords)
        {
            Dictionary<string, string> acceptableTiles = new Dictionary<string, string>
            {
                // For some reason, these maps have several incompatible connections with no clear distinguishing characteristics
                // Farum Azula from Bestial Sanctum - not Forge of the Giants (m60_54_53_00) oddly enough
                ["m13_00_00_00"] = "m60_51_43_00",
                // Haligtree from Ordina
                ["m15_00_00_00"] = "m60_48_57_00",
            };
            // Calculate all dungeon coordinate offsets. After this, ToGlobal can be called
            foreach (PARAM.Row row in game.Params["WorldMapLegacyConvParam"].Rows)
            {
                // Dungeon -> World conversions
                if ((byte)row["dstAreaNo"].Value != 60) continue;
                if ((byte)row["isBasePoint"].Value == 0) continue;
                string mapId = GameData.FormatMap(game.GetMapParts(row, srcParts));
                string mapId2 = GameData.FormatMap(game.GetMapParts(row, dstParts));
                if (dungeonOffsets.ContainsKey(mapId)) continue;
                if (acceptableTiles.TryGetValue(mapId, out string trueConnect) && mapId2 != trueConnect) continue;
                Vector3 mapPos = GetPos(row, "srcPos");
                Vector3 mapPos2 = GetPos(row, "dstPos");
                int tileX = (byte)row["dstGridXNo"].Value;
                int tileZ = (byte)row["dstGridZNo"].Value;
                dungeonOffsets[mapId] = (tileX, tileZ, mapPos2 - mapPos);
            }
            foreach (PARAM.Row row in game.Params["WorldMapLegacyConvParam"].Rows)
            {
                // Dungeon -> Dungeon
                if ((byte)row["dstAreaNo"].Value == 60) continue;
                string mapId = GameData.FormatMap(game.GetMapParts(row, srcParts));
                string mapId2 = GameData.FormatMap(game.GetMapParts(row, dstParts));
                if (!dungeonOffsets.ContainsKey(mapId2) && dungeonOffsets.TryGetValue(mapId, out var val))
                {
                    (int tileX, int tileZ, Vector3 dungeonOffset) = val;
                    Vector3 mapPos = GetPos(row, "srcPos");
                    Vector3 mapPos2 = GetPos(row, "dstPos");
                    dungeonOffsets[mapId2] = (tileX, tileZ, dungeonOffset + mapPos - mapPos2);
                }
            }
            dungeonOffsets["m11_71_00_00"] = dungeonOffsets["m11_05_00_00"];

            // Landmarks
            // Map from map id to all inaccessible maps from there
            Dictionary<string, List<string>> cutOffMaps = new Dictionary<string, List<string>>
            {
                ["m11_00_00_00"] = new List<string> { "m11_05_00_00", "m60_45_52_10" },
                ["m60_45_52_00"] = new List<string> { "m11_05_00_00", "m60_45_52_10" },
                ["m11_05_00_00"] = new List<string> { "m11_00_00_00", "m60_45_52_00" },
                ["m60_45_52_10"] = new List<string> { "m11_00_00_00", "m60_45_52_00" },
            };
            // Maps which are mutually inaccessible with all other maps. Failsafe to avoid positional overlap issues
            List<string> fullyIsolatedMaps = new List<string> { "m13_00_00_00", "m15_00_00_00" };
            void cutOffLandmark(string mapId, string fullName)
            {
                if (cutOffMaps.TryGetValue(mapId, out List<string> badMaps))
                {
                    foreach (string badMap in badMaps)
                    {
                        AddMulti(cutOffLandmarks, badMap, fullName);
                    }
                }
                if (fullyIsolatedMaps.Contains(mapId))
                {
                    AddMulti(isolatedLandmarks, mapId, fullName);
                    allIsolatedLandmarks.Add(fullName);
                }
            }

            // First, bonfires
            FMG placeFmg = game.ItemFMGs["PlaceName"];
            FMG warpFmg = game.MenuFMGs["GR_MenuText"];
            Dictionary<int, string> cats = new Dictionary<int, string>();
            foreach (PARAM.Row row in game.Params["BonfireWarpSubCategoryParam"].Rows)
            {
                string name = warpFmg[(int)row["textId"].Value];
                if (string.IsNullOrWhiteSpace(name)) continue;
                int tab = (ushort)row["tabId"].Value;
                cats[row.ID] = name;
            }
            HashSet<string> bonfireNames = new HashSet<string>();
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
                bonfireShortNames[fullName] = name;
                List<byte> mapParts = game.GetMapParts(row);
                string mapId = GameData.FormatMap(mapParts);
                Vector3 mapPos = GetPos(row, "pos");
                (Vector3 pos, int tileX, int tileZ) = ToGlobalCoords(mapParts, mapPos);
                AddMulti(landmarks, (tileX, tileZ), (fullName, pos));
                if (debugCoords) Console.WriteLine($"{row["eventflagId"].Value}: Bonfire {fullName} - at {mapId} {Vec(pos)}");
                cutOffLandmark(mapId, fullName);
                bonfireNames.Add(name);
            }
            foreach (PARAM.Row row in game.Params["WorldMapPointParam"].Rows)
            {
                int nameId = (int)row["textId1"].Value;
                // if (nameId == -1) nameId = (int)row["TempNameId"].Value;
                string name = placeFmg[nameId];
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (bonfireNames.Contains(name)) continue;
                int type = (ushort)row["iconId"].Value;
                string typeStr = ((MarkerType)type).ToString().Replace('_', ' ');
                if (int.TryParse(typeStr, out _))
                {
                    typeStr = "Marker";
                }
                string fullName = $"{name} ({typeStr})";
                List<byte> mapParts = game.GetMapParts(row);
                string mapId = GameData.FormatMap(mapParts);
                Vector3 mapPos = GetPos(row, "pos");
                (Vector3 pos, int tileX, int tileZ) = ToGlobalCoords(mapParts, mapPos);
                AddMulti(landmarks, (tileX, tileZ), (fullName, pos));
                cutOffLandmark(mapId, fullName);
            }
            if (debugCoords)
            {
                TestLegacyConv(game);
                TestBonfireCoords(game);
            }
        }

        public string ClosestLandmark(string mapId, Vector3 local, bool lowLevel = false)
        {
            (Vector3 pos, int tileX, int tileZ) = ToGlobalCoords(GameData.ParseMap(mapId), local);
            float distSq = float.PositiveInfinity;
            string name = null;
            Vector3 closePos = new Vector3();
            isolatedLandmarks.TryGetValue(mapId, out HashSet<string> isolated);
            cutOffLandmarks.TryGetValue(mapId, out HashSet<string> cutOff);
            cutOff = cutOff ?? new HashSet<string>();
            bool debug = false;
            string info = null;
            int bound = 2;
            for (int x = tileX - bound; x <= tileX + bound; x++)
            {
                for (int z = tileZ - bound; z <= tileZ + bound; z++)
                {
                    if (landmarks.TryGetValue((x, z), out List<(string, Vector3)> marks))
                    {
                        foreach ((string, Vector3) mark in marks)
                        {
                            string markName = mark.Item1;
                            if (cutOff.Contains(markName)) continue;
                            if (allIsolatedLandmarks.Contains(markName))
                            {
                                if (isolated == null || !isolated.Contains(markName)) continue;
                            }
                            float cmp = Vector3.DistanceSquared(pos, mark.Item2);
                            if (cmp < distSq)
                            {
                                distSq = cmp;
                                (name, closePos) = mark;
                                if (debug) info = $"({x}, {z}) {Math.Sqrt(distSq)}";
                            }
                        }
                    }
                }
            }
            if (name == null || debug)
            {
                string pastName = name;
                name = null;
                distSq = float.PositiveInfinity;
                string info2 = null;
                // Just find the closest overall, this is quite expensive
                foreach (KeyValuePair<(int, int), List<(string, Vector3)>> markEntry in landmarks)
                {
                    foreach ((string, Vector3) mark in markEntry.Value)
                    {
                        if (cutOff.Contains(mark.Item1)) continue;
                        float cmp = Vector3.DistanceSquared(pos, mark.Item2);
                        if (cmp < distSq)
                        {
                            distSq = cmp;
                            (name, closePos) = mark;
                            if (debug) info2 = $"{Math.Sqrt(distSq)} ({markEntry.Key.Item1}, {markEntry.Key.Item2})";
                        }
                    }
                }
                if (debug && pastName != null && name != pastName) Console.WriteLine($"Bad {tileX} {tileZ}! was {pastName} {info} -> {info2}");
            }
            if (name == null) throw new Exception();
            double angle = Math.Atan2(pos.X - closePos.X, pos.Z - closePos.Z);
            float vert = pos.Y - closePos.Y;
            pos -= new Vector3(0, vert, 0);
            double clockFacing = ((angle / Math.PI * 6) + 24) % 12;
            string clock;
            if (lowLevel)
            {
                clock = $"{clockFacing:f2}";
                clock = $", {clock} o'clock";
            }
            else
            {
                clock = $"{Math.Round(clockFacing):f0}";
                if (clock == "0") clock = "12";
                clock = $" in {clock} o'clock direction";
                if (bonfireShortNames.TryGetValue(name, out string shortName))
                {
                    name = $"{shortName} (Site of Grace)";
                }
            }
            return $"{name} - {Vector3.Distance(pos, closePos):f2} away{clock}, {vert:f2} height offset";
        }

        public (Vector3, int, int) ToGlobalCoords(string mapId, Vector3 local)
        {
            return ToGlobalCoords(GameData.ParseMap(mapId), local);
        }

        // Returns a global coordinate for the given map-local coordinate.
        // For derivative offset calculations, ToGlobalCoords(x + y) = ToGlobalCoords(x) + y
        public (Vector3, int, int) ToGlobalCoords(IList<byte> mapId, Vector3 local)
        {
            int tileX, tileZ;
            if (mapId[0] == 60)
            {
                int scale = mapId[3] % 10;
                int scaleFactor = 1;
                if (scale == 1)
                {
                    scaleFactor = 2;
                    local += new Vector3(128, 0, 128);
                }
                else if (scale == 2)
                {
                    scaleFactor = 4;
                    local += new Vector3(384, 0, 384);
                }
                tileX = mapId[1] * scaleFactor;
                tileZ = mapId[2] * scaleFactor;
            }
            else
            {
                string mapIdStr = GameData.FormatMap(mapId);
                if (!dungeonOffsets.TryGetValue(mapIdStr, out var val)) throw new Exception($"Unknown map {mapIdStr}");
                Vector3 dungeonOffset;
                (tileX, tileZ, dungeonOffset) = val;
                local += dungeonOffset;
            }
            // Offset for tile based on arbitrary origin at the center of a hypothetical m60_16_16_00, plus local coords
            Vector3 ret = local + new Vector3((tileX - 16) * 256, 0, (tileZ - 16) * 256);
            // Recompute tiles based on the origin, for searching purposes
            tileX = (int)Math.Round(ret.X / 256) + 16;
            tileZ = (int)Math.Round(ret.Z / 256) + 16;
            return (ret, tileX, tileZ);
        }

        public Vector3 ToLocalCoords(string mapId, Vector3 global)
        {
            return ToLocalCoords(GameData.ParseMap(mapId), global);
        }

        public Vector3 ToLocalCoords(IList<byte> mapId, Vector3 global)
        {
            if (!(mapId[0] == 60 && mapId[3] % 10 == 0)) throw new Exception($"Called with non-overworld map {GameData.FormatMap(mapId)}");
            int tileX = mapId[1];
            int tileZ = mapId[2];
            return global - new Vector3((tileX - 16) * 256, 0, (tileZ - 16) * 256);
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

        internal void DumpJS(GameData game)
        {
            Console.WriteLine($"// This file was automatically generated for EldenCoordinator by thefifthmatt.");
            Console.WriteLine($"// Warning: This data is imprecise and available in cleaner formats elsewhere.");
            Console.WriteLine("const offsets = {");
            foreach (KeyValuePair<string, (int, int, Vector3)> entry in dungeonOffsets.OrderBy(e => e.Key))
            {
                (int tileX, int tileZ, Vector3 v) = entry.Value;
                Console.WriteLine($"    '{entry.Key}': {{tileX: {tileX}, tileZ: {tileZ}, offset: Float64Array.from([{v.X}, {v.Y}, {v.Z}])}},");
            }
            Console.WriteLine("};");
            Console.WriteLine();
            GameSpec defaultSpec = GameSpec.ForGame(GameSpec.FromGame.ER);
            Console.WriteLine("const maps = {");
            SortedSet<string> includedMaps = new SortedSet<string>();
            string msbDir = $@"{defaultSpec.GameDir}\{defaultSpec.MsbDir}";
            foreach (string filename in Directory.GetFiles(msbDir, "*.msb.dcx"))
            {
                string id = GameEditor.BaseName(filename);
                if (id.EndsWith("_99")) continue;
                game.LocationNames.TryGetValue(id, out string name);
                if (string.IsNullOrWhiteSpace(name)) name = null;
                if (id.StartsWith("m60"))
                {
                    // Don't count duplicates
                    if (id.EndsWith("_10") || id.EndsWith("_11") || id.EndsWith("_12")) continue;
                }
                else
                {
                    if (name == null) continue;
                }
                Console.WriteLine($"    '{id}': {(name == null ? "null" : $"\"{name}\"")},");
                includedMaps.Add(id);
            }
            Console.WriteLine("};");
            Console.WriteLine();
            Console.WriteLine("const connects = {");
            Dictionary<string, MSBE> maps = game.EldenMaps;
            foreach (string id in includedMaps)
            {
                maps.TryGetValue(id, out MSBE msb);
                if (msb == null)
                {
                    msb = MSBE.Read($@"{msbDir}\{id}.msb.dcx");
                }
                SortedSet<string> connects = new SortedSet<string>();
                foreach (MSBE.Part.ConnectCollision col in msb.Parts.ConnectCollisions)
                {
                    string mapId = GameData.FormatMap(col.MapID.Select(i => i == 0xFF ? (byte)0 : i));
                    if (includedMaps.Contains(mapId))
                    {
                        connects.Add(mapId);
                    }
                }
                if (connects.Count > 0)
                {
                    Console.WriteLine($"    '{id}': [{string.Join(", ", connects.Select(c => $"'{c}'"))}],");
                }
            }
            Console.WriteLine("};");
        }

        internal void TestLegacyConv(GameData game)
        {
            foreach (PARAM.Row row in game.Params["WorldMapLegacyConvParam"].Rows)
            {
                string mapId = GameData.FormatMap(game.GetMapParts(row, srcParts));
                string mapId2 = GameData.FormatMap(game.GetMapParts(row, dstParts));
                if (!game.Maps.ContainsKey(mapId) || !game.Maps.ContainsKey(mapId2)) continue;
                Vector3 mapPos = GetPos(row, "srcPos");
                Vector3 mapPos2 = GetPos(row, "dstPos");
                Vector3 mapCoord = ToGlobalCoords(mapId, mapPos).Item1;
                Vector3 mapCoord2 = ToGlobalCoords(mapId2, mapPos2).Item1;
                float dist = Vector3.Distance(mapCoord, mapCoord2);
                string alert = dist > 1 ? "!!!!!!!!!!!!!" : "";
                Console.WriteLine($"{mapId} {Vec(mapCoord)} - {mapId2} {Vec(mapCoord2)} - dist {dist} {alert} - {row["isBasePoint"].Value}");
            }
        }

        internal void TestBonfireCoords(GameData game)
        {
            FMG placeFmg = game.ItemFMGs["PlaceName"];
            HashSet<uint> bonfireObjs = new HashSet<uint>(
                game.Params["BonfireWarpParam"].Rows
                    .Select(row => (uint)row["bonfireEntityId"].Value)
                    .Where(o => o > 0));
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                MSBE msb = entry.Value;
                foreach (MSBE.Part.Asset part in msb.Parts.Assets)
                {
                    if (!bonfireObjs.Contains(part.EntityID)) continue;
                    PARAM.Row row = game.Params["BonfireWarpParam"].Rows.Find(r => (int)r["LocationEventId"].Value == part.EntityID);
                    string name = placeFmg[(int)row["BonfireNameId"].Value];
                    // if (string.IsNullOrWhiteSpace(name)) continue;
                    List<byte> mapParts = game.GetMapParts(row);
                    string mapId = GameData.FormatMap(mapParts);
                    Vector3 localPos = GetPos(row, "pos");
                    Vector3 mapPos = ToGlobalCoords(GameData.ParseMap(entry.Key), part.Position).Item1;
                    Vector3 paramPos = ToGlobalCoords(mapParts, localPos).Item1;
                    Vector3 diff = mapPos - paramPos;
                    diff -= new Vector3(0, diff.Y, 0);
                    Console.WriteLine($"{entry.Key}: {Vec(mapPos)} vs {Vec(paramPos)} param - diff {Vec(diff)} - {name} - {diff.Length()} - {(diff.Length() > 100 ? " !!!!!!!!!!!!!!!! " : "")}");
                }
            }
        }
    }
}
