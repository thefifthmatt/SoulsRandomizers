using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    // TODO: Move to ER compilation dir after making use sites modular with DI
    public class EldenCoordinator
    {
        // Map id to (world X tile, world Z tile, offset)
        private readonly Dictionary<string, (Tile, Vector3)> dungeonOffsets = new();
        // Alternate names for sites of grace in pretty print view?
        private readonly Dictionary<string, string> bonfireShortNames = new();
        // Map from (world X tile, world Z tile) to (name, position)
        private readonly Dictionary<Tile, List<(string, Vector3)>> landmarks = new();
        // Map from map id to landmarks inaccessible from there
        private readonly Dictionary<string, HashSet<string>> cutOffLandmarks = new();
        // Map from map id to landmarks *only* accessible there
        private readonly Dictionary<string, HashSet<string>> isolatedLandmarks = new();
        private readonly HashSet<string> allIsolatedLandmarks = new HashSet<string>();

        public record Tile(int Type, int X, int Z);

        private static readonly List<string> srcParts = new List<string> { "srcAreaNo", "srcGridXNo", "srcGridZNo" };
        private static readonly List<string> dstParts = new List<string> { "dstAreaNo", "dstGridXNo", "dstGridZNo" };

        private static Vector3 GetPos(PARAM.Row row, string type)
        {
            return new Vector3((float)row[$"{type}X"].Value, (float)row[$"{type}Y"].Value, (float)row[$"{type}Z"].Value);
        }
        private string Vec(Vector3 v) => $"({v.X}, {v.Y}, {v.Z})";

        public EldenCoordinator(GameData game)
        {
            bool debugCoords = false;
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
                byte dstArea = (byte)row["dstAreaNo"].Value;
                if (dstArea != 60 && dstArea != 61) continue;
                if ((byte)row["isBasePoint"].Value == 0) continue;
                string mapId = GameData.FormatMap(game.GetMapParts(row, srcParts));
                string mapId2 = GameData.FormatMap(game.GetMapParts(row, dstParts));
                if (dungeonOffsets.ContainsKey(mapId)) continue;
                if (acceptableTiles.TryGetValue(mapId, out string trueConnect) && mapId2 != trueConnect) continue;
                Vector3 mapPos = GetPos(row, "srcPos");
                Vector3 mapPos2 = GetPos(row, "dstPos");
                int tileX = (byte)row["dstGridXNo"].Value;
                int tileZ = (byte)row["dstGridZNo"].Value;
                dungeonOffsets[mapId] = (new Tile(dstArea, tileX, tileZ), mapPos2 - mapPos);
            }
            foreach (PARAM.Row row in game.Params["WorldMapLegacyConvParam"].Rows)
            {
                // Dungeon -> Dungeon
                byte dstArea = (byte)row["dstAreaNo"].Value;
                if (dstArea == 60 || dstArea == 61) continue;
                string mapId = GameData.FormatMap(game.GetMapParts(row, srcParts));
                string mapId2 = GameData.FormatMap(game.GetMapParts(row, dstParts));
                if (!dungeonOffsets.ContainsKey(mapId2) && dungeonOffsets.TryGetValue(mapId, out var val))
                {
                    (Tile tile, Vector3 dungeonOffset) = val;
                    Vector3 mapPos = GetPos(row, "srcPos");
                    Vector3 mapPos2 = GetPos(row, "dstPos");
                    dungeonOffsets[mapId2] = (tile, dungeonOffset + mapPos - mapPos2);
                }
            }
            dungeonOffsets["m11_71_00_00"] = dungeonOffsets["m11_05_00_00"];
            dungeonOffsets["m45_00_00_00"] = (
                dungeonOffsets["m11_00_00_00"].Item1,
                dungeonOffsets["m11_00_00_00"].Item2 + new Vector3(-359.44f, 32.74f, -492.72f));
            dungeonOffsets["m45_01_00_00"] = (new Tile(60, 47, 42), new Vector3(-2.34f, 150.4f, -43.36f));
            dungeonOffsets["m45_02_00_00"] = (new Tile(60, 42, 40), new Vector3(-24.47f, 208.82f, -66.69f));

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
            List<string> fullyIsolatedMaps = new List<string>
            {
                "m13_00_00_00", "m15_00_00_00",
                "m22_00_00_00", "m25_00_00_00",
            };
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
            FMGX placeFmg = game.ItemFMGs["PlaceName"];
            FMGX warpFmg = game.MenuFMGs["GR_MenuText"];
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
                (Vector3 pos, Tile tile) = ToGlobalCoords(mapParts, mapPos);
                AddMulti(landmarks, tile, (fullName, pos));
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
                (Vector3 pos, Tile tile) = ToGlobalCoords(mapParts, mapPos);
                AddMulti(landmarks, tile, (fullName, pos));
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
            (Vector3 pos, Tile tile) = ToGlobalCoords(GameData.ParseMap(mapId), local);
            float distSq = float.PositiveInfinity;
            string name = null;
            Vector3 closePos = new Vector3();
            isolatedLandmarks.TryGetValue(mapId, out HashSet<string> isolated);
            cutOffLandmarks.TryGetValue(mapId, out HashSet<string> cutOff);
            cutOff = cutOff ?? new HashSet<string>();
            bool debug = false;
            string info = null;
            int bound = 2;
            for (int x = tile.X - bound; x <= tile.X + bound; x++)
            {
                for (int z = tile.Z - bound; z <= tile.Z + bound; z++)
                {
                    if (landmarks.TryGetValue(new Tile(tile.Type, x, z), out List<(string, Vector3)> marks))
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
                foreach (KeyValuePair<Tile, List<(string, Vector3)>> markEntry in landmarks)
                {
                    foreach ((string, Vector3) mark in markEntry.Value)
                    {
                        if (cutOff.Contains(mark.Item1)) continue;
                        float cmp = Vector3.DistanceSquared(pos, mark.Item2);
                        if (cmp < distSq)
                        {
                            distSq = cmp;
                            (name, closePos) = mark;
                            if (debug) info2 = $"{Math.Sqrt(distSq)} ({markEntry.Key.X}, {markEntry.Key.Z})";
                        }
                    }
                }
                if (debug && pastName != null && name != pastName) Console.WriteLine($"Bad {tile.X} {tile.Z}! was {pastName} {info} -> {info2}");
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

        Vector3 GetMapOffset(string fromArea, string toArea)
        {
            if (fromArea == toArea) return Vector3.Zero;
            Vector3 mapCoord = ToGlobalCoords(toArea, Vector3.Zero).Item1;
            Vector3 originCoord = ToGlobalCoords(fromArea, Vector3.Zero).Item1;
            return originCoord - mapCoord;
        }

        public (Vector3, Tile) ToGlobalCoords(string mapId, Vector3 local)
        {
            return ToGlobalCoords(GameData.ParseMap(mapId), local);
        }

        // Returns a global coordinate for the given map-local coordinate.
        // For derivative offset calculations, ToGlobalCoords(x + y) = ToGlobalCoords(x) + y
        public (Vector3, Tile) ToGlobalCoords(IList<byte> mapId, Vector3 local)
        {
            Tile tile;
            if (mapId[0] == 60 || mapId[0] == 61)
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
                tile = new Tile(mapId[0], mapId[1] * scaleFactor, mapId[2] * scaleFactor);
            }
            else
            {
                string mapIdStr = GameData.FormatMap(mapId);
                if (!dungeonOffsets.TryGetValue(mapIdStr, out var val)) throw new Exception($"Unknown map {mapIdStr}");
                Vector3 dungeonOffset;
                (tile, dungeonOffset) = val;
                local += dungeonOffset;
            }
            // Offset for tile based on arbitrary origin at the center of a hypothetical m60_16_16_00, plus local coords
            Vector3 ret = local + new Vector3((tile.X - 16) * 256, 0, (tile.Z - 16) * 256);
            // Recompute tiles based on the origin, for searching purposes
            tile = new Tile(tile.Type, (int)Math.Round(ret.X / 256) + 16, (int)Math.Round(ret.Z / 256) + 16);
            return (ret, tile);
        }

        public Vector3 ToLocalCoords(string mapId, Vector3 global)
        {
            return ToLocalCoords(GameData.ParseMap(mapId), global);
        }

        public Vector3 ToLocalCoords(IList<byte> mapId, Vector3 global)
        {
            if (!((mapId[0] == 60 || mapId[0] == 61) && mapId[3] % 10 == 0)) throw new Exception($"Called with non-overworld map {GameData.FormatMap(mapId)}");
            int tileX = mapId[1];
            int tileZ = mapId[2];
            return global - new Vector3((tileX - 16) * 256, 0, (tileZ - 16) * 256);
        }

        public Vector3 RelocationOffset(string fromArea, string toArea)
        {
            Vector3 originCoord = ToGlobalCoords(fromArea, Vector3.Zero).Item1;
            Vector3 mapCoord = ToGlobalCoords(toArea, Vector3.Zero).Item1;
            return originCoord - mapCoord;
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
            Colosseum = 24,
            Erdtree = 30,
            Lake_Town = 32,
            Underground = 46,
            Underground_Ruins = 47,
            Red_Mark = 87,
            // These are not the greatest names
            Cross = 208,
            DLC_Catacombs = 230,
            Gaol = 231,
            Forge = 232,
            DLC_Cave = 234,
            DLC_Church = 247,
            Finger_Ruins = 255,
            Nameless_Mausoleum = 256,
            DLC_Shack = 259,
        }

        internal void DumpJS(GameData game)
        {
            Console.WriteLine($"// This file was automatically generated for EldenCoordinator by thefifthmatt.");
            Console.WriteLine($"// Warning: This data is imprecise and available in cleaner formats elsewhere.");
            Console.WriteLine("const offsets = {");
            foreach (KeyValuePair<string, (Tile, Vector3)> entry in dungeonOffsets.OrderBy(e => e.Key))
            {
                (Tile tile, Vector3 v) = entry.Value;
                Console.WriteLine($"    '{entry.Key}': {{tileX: {tile.X}, tileZ: {tile.Z}, offset: Float64Array.from([{v.X}, {v.Y}, {v.Z}])}},");
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
            foreach (string id in includedMaps)
            {
                game.EldenMaps.TryGetValue(id, out MSBE msb);
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
                if (!game.EldenMaps.ContainsKey(mapId) || !game.EldenMaps.ContainsKey(mapId2)) continue;
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
            FMGX placeFmg = game.ItemFMGs["PlaceName"];
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
