using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.ItemScope;
using YamlDotNet.Serialization.NamingConventions;
using SoulsFormats.KF4;
using Org.BouncyCastle.Utilities;

namespace RandomizerCommon
{
    // Messy code to generate various heuristics
    public class ReverseEnemyOrder
    {
        private Dictionary<int, string> GetDS3Bonfires(GameData game)
        {
            HashSet<int> allBonfires = new HashSet<int>
            {
                4001950,  // Firelink Shrine
                4001951,  // Cemetery of Ash
                4001952,  // Iudex Gundyr
                4001953,  // Untended Graves
                4001954,  // Champion Gundyr
                3001950,  // High Wall of Lothric
                // 3001960,  // High Wall of Lothric
                3001955,  // Tower on the Wall
                3001952,  // Vordt of the Boreal Valley
                3001954,  // Dancer of the Boreal Valley
                3001951,  // Oceiros, the Consumed King
                // 3001959,  // High Wall of Lothric
                // 3001958,  // High Wall of Lothric
                3011950,  // Lothric Castle
                3011952,  // Dragon Barracks
                3011951,  // Dragonslayer Armour
                3411951,  // Grand Archives
                3411950,  // Twin Princes
                3101954,  // Foot of the High Wall
                3101950,  // Undead Settlement
                3101952,  // Cliff Underside
                3101953,  // Dilapidated Bridge
                3101951,  // Pit of Hollows
                3202900,  // - Archdragon Start
                3201950,  // Archdragon Peak
                3201953,  // Dragon-Kin Mausoleum
                3201952,  // Great Belfry
                3201951,  // Nameless King
                3301956,  // Road of Sacrifices
                3301950,  // Halfway Fortress
                3301957,  // Crucifixion Woods
                3301952,  // Crystal Sage
                3301953,  // Farron Keep
                3301954,  // Keep Ruins
                3301958,  // Farron Keep Perimeter
                3301955,  // Old Wolf of Farron
                3301951,  // Abyss Watchers
                3501953,  // Cathedral of the Deep
                3501950,  // Cleansing Chapel
                3501951,  // Deacons of the Deep
                3501952,  // Rosaria's Bed Chamber
                3701957,  // Irithyll of the Boreal Valley
                3701954,  // Central Irithyll
                3701950,  // Church of Yorshka
                3701955,  // Distant Manor
                3701951,  // Pontiff Sulyvahn
                3701956,  // Water Reserve
                3701953,  // Anor Londo
                3701958,  // Prison Tower
                3701952,  // Aldrich, Devourer of Gods
                3801956,  // Catacombs of Carthus
                3801950,  // High Lord Wolnir
                3801951,  // Abandoned Tomb
                3801952,  // Old King's Antechamber
                3801953,  // Demon Ruins
                3801954,  // Old Demon King
                3901950,  // Irithyll Dungeon
                3901952,  // Profaned Capital
                3901951,  // Yhorm the Giant
                4101950,  // Flameless Shrine
                4101951,  // Kiln of the First Flame
                4101952,  // The First Flame
                4501951,  // Snowfield
                4501952,  // Rope Bridge Cave
                4501953,  // Corvian Settlement
                4501954,  // Snowy Mountain Pass
                4501955,  // Ariandel Chapel
                4501950,  // Sister Friede
                4501957,  // Depths of the Painting
                4501956,  // Champion's Gravetender
                5001951,  // The Dreg Heap
                5001952,  // Earthen Peak Ruins
                5001953,  // Within the Earthen Peak Ruins
                5001950,  // The Demon Prince
                5101952,  // Mausoleum Lookout
                5101953,  // Ringed Inner Wall
                5101954,  // Ringed City Streets
                5101955,  // Shared Grave
                5101950,  // Church of Filianore
                5101951,  // Darkeater Midir
                5111951,  // Filianore's Rest
                5111950,  // Slave Knight Gael
            };
            Dictionary<int, string> extraPoints = new Dictionary<int, string>
            {
                [4000110] = "Cemetery Start",  // Player
                [3202900] = "Archdragon Start",  // Spawn point
            };

            GameEditor editor = new GameEditor(GameSpec.FromGame.DS3);
            Dictionary<string, FMG> menuFMGs = editor.LoadBnd($@"{editor.Spec.GameDir}\msg\engus\menu_dlc2.msgbnd.dcx", (p, n) => FMG.Read(p));

            Dictionary<int, string> bonfireNames = new[] { "", "_dlc1", "_dlc2" }
                .SelectMany(s => menuFMGs[$"FDP_\u30e1\u30cb\u30e5\u30fc\u30c6\u30ad\u30b9\u30c8{s}"].Entries)
                .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                .ToDictionary(e => e.ID, e => e.Text);

            Dictionary<int, string> names = new Dictionary<int, string>();
            foreach (PARAM.Row r in game.Params["BonfireWarpParam"].Rows)
            {
                int entity = (int)r["WarpEventId"].Value;
                string bonfire = bonfireNames[(int)r["BonfireNameId"].Value];
                if (bonfire != null && entity > 0)
                {
                    names[entity] = bonfire;
                    // Console.WriteLine($"{entity},  // {bonfire}");
                }
            }
            foreach (KeyValuePair<int, string> entry in extraPoints)
            {
                names[entry.Key] = entry.Value;
            }
            return names;
        }

        public void EnemyDS3(GameData game, Dictionary<int, EnemyInfo> fullInfos)
        {
            // The main purpose here is section categorization rather than logical area categorization, so this is more compact than FogDS3
            Dictionary<int, string> names = GetDS3Bonfires(game);
            List<Path> paths = new List<Path>
            {
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "firelink" },
                    Conds = new List<int> { 1 },
                    Bonfires = new List<int>
                    {
                        4000110,  // Cemetery Start
                        4001951,  // Cemetery of Ash
                        4001952,  // Iudex Gundyr
                        4001950,  // Firelink Shrine
                    }
                },
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "highwall" },
                    Bonfires = new List<int>
                    {
                        3001950,  // High Wall of Lothric
                        3001955,  // Tower on the Wall
                        3001952,  // Vordt of the Boreal Valley
                    }
                },
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "settlement" },
                    Bonfires = new List<int>
                    {
                        3101954,  // Foot of the High Wall
                        3101950,  // Undead Settlement
                        3101952,  // Cliff Underside
                        3101953,  // Dilapidated Bridge
                        3101951,  // Pit of Hollows
                    }
                },
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "settlement" },
                    Bonfires = new List<int>
                    {
                        3301956,  // Road of Sacrifices
                        3101951,  // Pit of Hollows
                    }
                },
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "farronkeep" },
                    Bonfires = new List<int>
                    {
                        3301956,  // Road of Sacrifices
                        3301950,  // Halfway Fortress
                    }
                },
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "farronkeep" },
                    Bonfires = new List<int>
                    {
                        3301950,  // Halfway Fortress
                        3301957,  // Crucifixion Woods
                        3301952,  // Crystal Sage
                    }
                },
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "farronkeep", "cathedral" },
                    Bonfires = new List<int>
                    {
                        3301952,  // Crystal Sage
                        3501953,  // Cathedral of the Deep
                        3501950,  // Cleansing Chapel
                        3501951,  // Deacons of the Deep
                    }
                },
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "farronkeep" },
                    Bonfires = new List<int>
                    {
                        3301953,  // Farron Keep
                        3301954,  // Keep Ruins
                        3301958,  // Farron Keep Perimeter
                        3301951,  // Abyss Watchers
                    }
                },
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "catacombs" },
                    Bonfires = new List<int>
                    {
                        3301951,  // Abyss Watchers
                        3801956,  // Catacombs of Carthus
                        3801950,  // High Lord Wolnir
                        3701957,  // Irithyll of the Boreal Valley
                    }
                },
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "catacombs" },
                    Bonfires = new List<int>
                    {
                        3801950,  // High Lord Wolnir
                        3801951,  // Abandoned Tomb
                        3801952,  // Old King's Antechamber
                        3801953,  // Demon Ruins
                        3801954,  // Old Demon King
                    }
                },
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "irithyll" },
                    Bonfires = new List<int>
                    {
                        3701957,  // Irithyll of the Boreal Valley
                        3701954,  // Central Irithyll
                        3701950,  // Church of Yorshka
                        3701951,  // Pontiff Sulyvahn
                        3701956,  // Water Reserve
                        3701958,  // Prison Tower
                        3701953,  // Anor Londo
                        3701952,  // Aldrich, Devourer of Gods
                    }
                },
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "irithyll", "dungeon" },
                    Bonfires = new List<int>
                    {
                        3701955,  // Distant Manor
                        3901950,  // Irithyll Dungeon
                        3901952,  // Profaned Capital
                        3901951,  // Yhorm the Giant
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "highwall" },
                    Bonfires = new List<int>
                    {
                        3001954,  // Dancer of the Boreal Valley
                        3001951,  // Oceiros, the Consumed King
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "firelink" },
                    Conds = new List<int> { 2 },
                    Bonfires = new List<int>
                    {
                        4001953,  // Untended Graves
                        4001954,  // Champion Gundyr
                        4001950,  // Firelink Shrine
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "lothric" },
                    Bonfires = new List<int>
                    {
                        3011950,  // Lothric Castle
                        3011952,  // Dragon Barracks
                        3011951,  // Dragonslayer Armour
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "archives" },
                    Bonfires = new List<int>
                    {
                        3411951,  // Grand Archives
                        3411950,  // Twin Princes
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "archdragon" },
                    Bonfires = new List<int>
                    {
                        3202900,  // Archdragon Start
                        3201950,  // Archdragon Peak
                        3201953,  // Dragon-Kin Mausoleum
                        3201951,  // Nameless King
                        3201952,  // Great Belfry
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "ariandel" },
                    Bonfires = new List<int>
                    {
                        4501951,  // Snowfield
                        4501952,  // Rope Bridge Cave
                        4501953,  // Corvian Settlement
                        4501954,  // Snowy Mountain Pass
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "ariandel" },
                    Bonfires = new List<int>
                    {
                        4501952,  // Rope Bridge Cave
                        4501957,  // Depths of the Painting
                        4501956,  // Champion's Gravetender
                    }
                },
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "ariandel" },
                    Bonfires = new List<int>
                    {
                        4501954,  // Snowy Mountain Pass
                        4501955,  // Ariandel Chapel
                        4501950,  // Sister Friede
                    }
                },
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "dregheap" },
                    Bonfires = new List<int>
                    {
                        5001951,  // The Dreg Heap
                        5001952,  // Earthen Peak Ruins
                        5001953,  // Within the Earthen Peak Ruins
                        5001950,  // The Demon Prince
                    }
                },
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "ringedcity" },
                    Bonfires = new List<int>
                    {
                        5101952,  // Mausoleum Lookout
                        5101953,  // Ringed Inner Wall
                        5101954,  // Ringed City Streets
                        5101955,  // Shared Grave
                        5101950,  // Church of Filianore
                        // 5101951,  // Darkeater Midir
                    }
                },
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "filianore" },
                    Bonfires = new List<int>
                    {
                        5111951,  // Filianore's Rest
                        5111950,  // Slave Knight Gael
                    }
                },
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "kiln" },
                    Bonfires = new List<int>
                    {
                        4101951,  // Kiln of the First Flame
                        4101952,  // The First Flame
                    }
                },
            };

            Dictionary<int, MSB3.Part.Enemy> enemies = new Dictionary<int, MSB3.Part.Enemy>();
            Dictionary<int, Vector3> points = new Dictionary<int, Vector3>();
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;
                foreach (MSB3.Part e in msb.Parts.GetEntries())
                {
                    if (names.ContainsKey(e.EntityID)) points[e.EntityID] = e.Position;
                    if (e is MSB3.Part.Enemy enemy)
                    {
                        enemies[e.EntityID] = enemy;
                    }
                }
                foreach (MSB3.Region e in msb.Regions.GetEntries())
                {
                    if (names.ContainsKey(e.EntityID)) points[e.EntityID] = e.Position;
                }
            }

            string pathText(int p)
            {
                int first = paths[p].Bonfires.First();
                int last = paths[p].Bonfires.Last();
                return $"{paths[p].Area} [{names[first]}->{names[last]}]";
            }

            List<List<EnemyClass>> typeGroups = new List<List<EnemyClass>>
            {
                new List<EnemyClass> { EnemyClass.Boss },
                new List<EnemyClass> { EnemyClass.Basic, EnemyClass.Mimic, EnemyClass.CrystalLizard },
                // Do these NPCs' areas generally match those of surrounding enemies?
                new List<EnemyClass> { EnemyClass.Miniboss },
                new List<EnemyClass> { EnemyClass.HostileNPC },
            };
            List<EnemyClass> types = typeGroups.SelectMany(c => c).ToList();
            Dictionary<int, EnemyInfo> infos = fullInfos.Values.Where(e => types.Contains(e.Class)).ToDictionary(e => e.ID, e => e);

            Dictionary<int, (int, float)> chosenPath = new Dictionary<int, (int, float)>();
            foreach (EnemyInfo info in infos.Values)
            {
                float score = float.PositiveInfinity;
                MSB3.Part.Enemy e = enemies[info.ID];
                Vector3 pos = e.Position;
                string map = game.Locations[info.Map];
                for (int path = 0; path < paths.Count; path++)
                {
                    // Do some very basic filtering here, as it's nowhere near as involved as Sekiro
                    Path p = paths[path];
                    List<string> maps = p.Maps;
                    if (!maps.Contains(map)) continue;
                    int section = p.Section;
                    List<int> cond = p.Conds;
                    if (cond != null && cond.Count > 0)
                    {
                        bool passed = true;
                        bool firelinkLayer = (e.MapStudioLayer & 1) != 0;
                        foreach (int c in cond)
                        {
                            if (c == 1)
                            {
                                passed &= firelinkLayer;
                            }
                            else if (c == 2)
                            {
                                passed &= !firelinkLayer;
                            }
                            else throw new Exception($"{p}");
                        }
                        if (!passed) continue;
                    }
                    List<int> order = p.Bonfires;

                    for (int i = 0; i < order.Count - 1; i++)
                    {
                        Vector3 p1 = points[order[i]];
                        Vector3 p2 = points[order[i + 1]];
                        float dist1 = Vector3.Distance(p1, pos);
                        float dist2 = Vector3.Distance(p2, pos);
                        float dist = dist1 + dist2;
                        if (info.ID == 9999999) Console.WriteLine($"Found dist {dist1} to {names[order[i]]}, and {dist2} to {names[order[i + 1]]}. TOTAL {dist}");
                        if (dist < score)
                        {
                            score = dist;
                            chosenPath[info.ID] = (path, i + Vector3.Distance(p1, pos) / dist);
                        }
                    }
                }
                if (float.IsInfinity(score)) throw new Exception($"{info.ID} had nothing checked for it");
            }

            // InvestigateDS3Scaling(game, typeGroups, infos, paths, chosenPath); return;

            bool scalingSection = true;
            if (scalingSection)
            {
                Dictionary<int, int> allSections = chosenPath.ToDictionary(e => e.Key, e => paths[e.Value.Item1].Section);
                foreach (EnemyInfo info in fullInfos.Values)
                {
                    if (!allSections.ContainsKey(info.ID) && info.Class == EnemyClass.Helper && allSections.TryGetValue(info.OwnedBy, out int section))
                    {
                        allSections[info.ID] = section;
                    }
                }
                foreach (KeyValuePair<int, int> entry in allSections.OrderBy(e => (e.Value, e.Key)))
                {
                    Console.WriteLine($"  {entry.Key}: {entry.Value}");
                }
                return;
            }

            bool debugOutput = true;
            foreach (List<EnemyClass> typeGroup in typeGroups)
            {
                List<string> order = new List<string>();
                foreach (KeyValuePair<int, (int, float)> entry in chosenPath.OrderBy(e => (e.Value, e.Key)))
                {
                    int id = entry.Key;
                    EnemyInfo info = infos[id];
                    if (!typeGroup.Contains(info.Class)) continue;
                    if (debugOutput) Console.WriteLine($"{info.DebugText}\n- {pathText(entry.Value.Item1)}, progress {entry.Value.Item2}\n");
                    // TODO: Add the preset-addressable name for the enemy
                    // order.Add($"{info.ExtraName ?? names[id]} {id}");
                }
                for (int i = 0; i < order.Count; i++)
                {
                    if (!debugOutput) Console.WriteLine($"  {order[i]}: {order[order.Count - 1 - i]}");
                }
            }
        }

        class FogPartConfig { public List<FogArea> Areas { get; set; } }
        class FogArea
        {
            public string Name { get; set; }
            public int DefeatFlag { get; set; }
            public string OpenArea { get; set; }
            public string Maps { get; set; }
            public string Tags { get; set; }
        }
        // To generate
        public class DungeonItem
        {
            // EntityId map, with Roundtable preferred last
            public string Map { get; set; }
            // Copied from foglocations
            public List<string> DebugText { get; set; }
            // Base lot, from foglocations
            public string ItemLot { get; set; }
            // By default 25, Twin Maiden Husks have 100
            public string ShopRange { get; set; }
            // "Relocated to <blah>"
            public string Text { get; set; }
            public string Comment { get; set; }
            // To be filled in
            public string ToArea { get; set; }
            public string ToMap { get; set; }
            // XYZ
            public string Location { get; set; }
            // Copy from slot tags: seedtree, church, raceshop
            public string Tags { get; set; }
        }

        public void FogEldenCrawl(RandomizerOptions opt, GameData game, LocationData data, AnnotationData ann)
        {
            // Produce a rough version of the dungeon items listing, for items to relocate.
            // Golden trees (seedtree), Marika statues (church), merchants (raceshop): default crawl in m60, unless nocrawl/fortress.
            // boss: default crawl
            // racemode/altboss/talisman have default nocrawl in m60, unless crawl (not yet implemented)/fortress.
            // altboss is just cellars, not evergaols or field bosses. This is basically just Redmane Duo and Nox Duo.
            IDeserializer deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            string fogDir = "configs/diste";
            FogLocations locs = new FogLocations();
            using (var reader = File.OpenText($"{fogDir}/foglocations.txt")) locs = deserializer.Deserialize<FogLocations>(reader);

            Dictionary<string, KeyItemLoc> storedItems = locs.Items.ToDictionary(l => l.Key, l => l);
            List<DungeonItem> results = new List<DungeonItem>();
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                if (!ann.Slots.TryGetValue(locScope, out SlotAnnotation slotAnn)) continue;
                if (!storedItems.TryGetValue(slotAnn.Key, out KeyItemLoc loc)) continue;
                if (slotAnn.Area == "unknown") continue;

                string relocType = null;
                foreach (string tag in new[] { "seedtree", "church", "raceshop" })
                {
                    if (slotAnn.HasTag(tag))
                    {
                        relocType = tag;
                    }
                }
                if (relocType == null) continue;
                if (slotAnn.HasTag("nocrawl") || slotAnn.HasTag("fortress")) continue;
                // All item lots and shop items
                List<EntityId> ids = new List<EntityId>();
                List<ItemKey> items = new List<ItemKey>();
                foreach (SlotKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.Location(itemLocKey);
                    ItemScope scope = location.Scope;
                    if (scope.Type != ScopeType.EVENT && scope.Type != ScopeType.ENTITY) continue;
                    items.Add(itemLocKey.Item);
                    foreach (LocationKey locKey in location.Keys)
                    {
                        ids.AddRange(locKey.Entities);
                    }
                }
                EntityId id = ids
                    .Where(e => e.MapName != null && e.Type != null && (e.Type.Contains("asset") || e.Type.Contains("enemy")))
                    .OrderBy(e => e.MapName == "m11_10_00_00" ? 1 : 0)
                    .FirstOrDefault();
                if (id == null) continue;
                if (!id.MapName.StartsWith("m60")) continue;
                // Console.WriteLine($"{relocType} - {game.EntityName(id, true, true)} - {loc.DebugText[0]}");
                DungeonItem item = new DungeonItem
                {
                    Map = id.MapName,
                    DebugText = loc.DebugText,
                    Text = "",
                    ToArea = "",
                    ToMap = "",
                    Location = "",
                    Tags = relocType,
                };
                if (loc.Lots != null)
                {
                    item.ItemLot = loc.Lots;
                }
                else if (loc.Shops != null)
                {
                    // Assume merchant convention
                    int start = int.Parse(loc.Shops.Split(' ')[0]);
                    start = start / 25 * 25;
                    item.ShopRange = $"{start} {start + 24}";
                }
                // Set ItemLot/ShopRange
                results.Add(item);
            }
            results = results.OrderBy(loc => loc.Map).ToList();
            if (opt["outyaml"])
            {
                Console.WriteLine(GameData.Serializer.Serialize(results));
            }
        }

        public void FogElden(RandomizerOptions opt, GameData game, LocationData data, AnnotationData ann, EldenCoordinator coord)
        {
            if (opt["crawl"])
            {
                FogEldenCrawl(opt, game, data, ann);
                return;
            }
            IDeserializer deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            FogPartConfig fogConfig;
            using (var reader = File.OpenText("../FogMod/eldendata/Base/fog.txt")) fogConfig = deserializer.Deserialize<FogPartConfig>(reader);
            Dictionary<string, FogArea> fogAreas = fogConfig.Areas.ToDictionary(a => a.Name, a => a);
            Dictionary<int, FogArea> defeatFogAreas = fogConfig.Areas.Where(a => a.DefeatFlag > 0).ToDictionary(a => a.DefeatFlag, a => a);

            // First pass: generate possible area dictionary
            // Do this just based on names
            if (opt["gendict"])
            {
                foreach (FogArea area in fogConfig.Areas)
                {
                    string fog = area.Name;
                    List<string> cands = ann.Areas.Keys.Where(area => fog.StartsWith(area)).ToList();
                    string cand = cands.Count > 0 ? cands.MaxBy(m => m.Length) : "";
                    // Console.WriteLine($"[\"{fog}\"] = \"{cand}\",");
                    Console.WriteLine($"{fog}={cand}");
                }
                return;
            }

            Dictionary<string, string> fogToItemArea = game.Editor.LoadNames("../../fogboss", x => x);
            List<string> missingMapping = fogAreas.Keys.Except(fogToItemArea.Keys).ToList();
            if (missingMapping.Count > 0)
            {
                throw new Exception($"Missing areas in fogboss.txt: {string.Join(" ", missingMapping)}");
            }
            deserializer = new DeserializerBuilder().Build();

            string enemyConfigPath = $"diste/Base/enemy.txt";
            EnemyAnnotations enemyAnn;
            using (var reader = File.OpenText(enemyConfigPath)) enemyAnn = deserializer.Deserialize<EnemyAnnotations>(reader);
            Dictionary<int, EnemyInfo> infos = enemyAnn.Enemies.ToDictionary(i => i.ID, i => i);
            Dictionary<int, int> enemyIdFlags = new Dictionary<int, int>();
            Dictionary<(string, string), EnemyInfo> nameInfos = new Dictionary<(string, string), EnemyInfo>();
            foreach (EnemyInfo info in enemyAnn.Enemies)
            {
                int id = info.ID;
                if (info.OwnedBy > 0)
                {
                    id = info.OwnedBy;
                }
                if (infos[id].NextPhase > 0)
                {
                    id = infos[id].NextPhase;
                }
                if (infos[id].DefeatFlag > 0)
                {
                    enemyIdFlags[info.ID] = infos[id].DefeatFlag;
                }
                nameInfos[(info.Map, info.Name)] = info;
                if (info.DupeMap != null)
                {
                    nameInfos[(info.DupeMap, info.DupePartName ?? info.Name)] = info;
                }
            }
            // Item config doesn't have Patches
            enemyIdFlags[31000800] = 31000800;
            // Things in DisplayEntities in itemlocations.txt
            enemyIdFlags[12030700] = 12030850;
            enemyIdFlags[12021609] = 12080800;
            enemyIdFlags[12021629] = 12090800;
            Dictionary<int, List<int>> flagEnemies = enemyIdFlags.GroupBy(e => e.Value).ToDictionary(g => g.Key, g => g.Select(e => e.Key).ToList());

            // To fully classify: stormveil_start
            // peninsula_postmorne should be trivial

            // Overall approach to produce heuristic area classifications for item lots and also enemies
            // ~ if there are no items in that area in fog rando
            // If defeat flag exists, solely use that (and locked chests after boss)
            // Other exceptions: Eiglay drop (and upstairs), Gostoc, Mohg chest, Radahn many, Rykard items, Malenia needle
            // trivial areas are not added by default, but noted down for later to fill in manually.
            // EldenAreaClassifier is used for some specific disambiguations?
            Dictionary<string, List<string>> itemToFogArea = fogToItemArea.Where(e => !e.Value.StartsWith("~")).GroupBy(e => e.Value).ToDictionary(g => g.Key, g => g.Select(e => e.Key).ToList());
            HashSet<int> entities = new HashSet<int>(data.Data.Values.SelectMany(itemLocs => itemLocs.Locations.Values.SelectMany(itemLoc => itemLoc.Keys.SelectMany(loc => loc.Entities.Select(e => e.EntityID)))));
            HashSet<string> keepFogAreas = new HashSet<string>
            {
                "caelid_radahn", "leyndell_bedchamber", "leyndell2_bedchamber", "stormveil_throne",
                "limgrave_tower", "limgraver_tower2", "liurnia_tower",
                "leyndell_tower_start", "leyndell_tower", "leyndell_tower_boss",
                "roundtable",
            };
            HashSet<string> classifyLater = new HashSet<string>
            {
                "leyndell_pretower",  // This could be a simple xz check
                "academy_entrance",
            };
            List<string> trivialAreas = new List<string>();
            foreach (KeyValuePair<string, List<string>> entry in itemToFogArea)
            {
                List<string> keys = entry.Value.ToList();
                bool singleEntry = keys.Count == 1;
                foreach (string key in keys)
                {
                    FogArea area = fogAreas[key];
                    if (area.DefeatFlag > 0)
                    {
                        if (!flagEnemies.TryGetValue(area.DefeatFlag, out List<int> enemies))
                        {
                            throw new Exception($"Missing flag: {area.Name} {area.DefeatFlag}");
                        }
                        if (!enemies.Any(id => entities.Contains(id)))
                        {
                            // Console.WriteLine($"Missing item location: {area.Name} for entities {string.Join(",", enemies)}");
                        }
                        if (!keepFogAreas.Contains(key) && !singleEntry)
                        {
                            entry.Value.Remove(key);
                        }
                    }
                    if ((area.Tags != null && area.Tags.Contains("trivial")) || classifyLater.Contains(key))
                    {
                        if (!keepFogAreas.Contains(key) && !singleEntry)
                        {
                            entry.Value.Remove(key);
                            trivialAreas.Add(key);
                        }
                    }
                }
                if (entry.Value.Count == 0) throw new Exception($"No areas left in {entry.Key}: {string.Join(",", keys)}");
            }
            Dictionary<string, List<string>> mapOnlyFogAreas = new Dictionary<string, List<string>>();
            HashSet<string> itemMapAreas = new HashSet<string>
            {
                // Overworld sub-areas which should require Maps to match or else not consider them. Because the big areas are too big
                "peninsula_morne", "liurnia_manor", "caelid_redmane", "altus_shaded", "mountaintops_sol",
            };
            foreach (FogArea area in fogAreas.Values)
            {
                string name = area.Name;
                if (!itemMapAreas.Contains(name)) continue;
                foreach (string map in area.Maps.Split(' '))
                {
                    AddMulti(mapOnlyFogAreas, map, name);
                }
            }

            if (opt["trivial"])
            {
                Console.WriteLine($"Trivial areas: {string.Join(" ", trivialAreas)}");
                return;
            }

            // Iteration order is not reliable, but anyway, rely on it
            List<string> fogOrder = fogToItemArea.Select(e => e.Key).ToList();

            // string fogDir = "../FogMod/eldendata/Base";
            // Eh, just add another level of copying
            string fogDir = "configs/diste";
            FogLocations locs = new FogLocations();
            using (var reader = File.OpenText($"{fogDir}/foglocations.txt")) locs = deserializer.Deserialize<FogLocations>(reader);

            Dictionary<string, KeyItemLoc> storedItems = locs.Items.ToDictionary(l => l.Key, l => l);
            Dictionary<EntityId, KeyItemLoc> entityFogAreas = new Dictionary<EntityId, KeyItemLoc>();

            // For each item: find its old area in item rando, new proposed area in fog rando based on that, and entity id
            HashSet<string> excludeKeyItem = new HashSet<string> { }; // "missable", "enemy", "norandom", "nokey", "crow", "end" };

            EldenAreaClassifier classifier = new EldenAreaClassifier(game, coord);
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                if (!ann.Slots.TryGetValue(locScope, out SlotAnnotation slotAnn)) continue;
                if (slotAnn.Area == "unknown") continue;

                // All item lots and shop items
                SortedSet<int> lots = new SortedSet<int>();
                SortedSet<int> shops = new SortedSet<int>();
                List<EntityId> ids = new List<EntityId>();
                List<ItemKey> items = new List<ItemKey>();
                foreach (SlotKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.Location(itemLocKey);
                    ItemScope scope = location.Scope;
                    if (scope.Type != ScopeType.EVENT && scope.Type != ScopeType.ENTITY) continue;
                    items.Add(itemLocKey.Item);
                    foreach (LocationKey locKey in location.Keys)
                    {
                        if (locKey.Type == LocationKey.LocationType.LOT) lots.Add(locKey.ID);
                        else shops.Add(locKey.ID);
                        ids.AddRange(locKey.Entities);
                    }
                }
                if (items.Count == 0) continue;
                foreach (int id in lots.ToList())
                {
                    lots.Remove(id + 1);
                }
                // Tonic of Forgetfulness is given by Tanith (100700) but also on her chair (100726), and the latter
                // gets conflated with Drawing-Room Key on chair (100725).
                // Fully accounting for this may require looking at adjacent lots with different flags and checking safety.
                if (lots.Contains(100700)) lots.Remove(100726);

                // For this to be most valid: check " Area: .* "
                if (storedItems.TryGetValue(slotAnn.Key, out KeyItemLoc existLoc))
                {
                    if (existLoc.Area != null && existLoc.Area.Contains(' ')) continue;
                    foreach (EntityId id in ids)
                    {
                        entityFogAreas[id] = existLoc;
                    }
                }

                // We only care about key item eligible lots/shops, but still can care about item locations, since they can be used to find enemy locations.
                // Make sure not to set any norandom options, including setting ngplusrings
                if (!slotAnn.HasAnyTags(excludeKeyItem))
                {
                    // Console.WriteLine(slotAnn.Area + " " + slotAnn.QuestReqs + " " + slotAnn.Event);
                    // Find the best area
                    List<string> areas = new List<string>();
                    // slotAnn.Event is not used/needed, since defeat flag is used
                    List<string> fogs = null;
                    foreach (EntityId id in ids)
                    {
                        if (enemyIdFlags.TryGetValue(id.EntityID, out int flag) && defeatFogAreas.TryGetValue(flag, out FogArea fogArea))
                        {
                            fogs = new List<string> { fogArea.Name };
                            break;
                        }
                    }
                    // Beware: fogs is shared between all areas, do not mutate it directly
                    if (fogs == null && (!itemToFogArea.TryGetValue(slotAnn.Area, out fogs) || fogs.Count == 0))
                    {
                        throw new Exception($"Unknown area {slotAnn.Area}: {string.Join("\n", slotAnn.DebugText)}");
                    }
                    fogs = classifier.FogClassify(fogs, ids);
                    if (fogs.Any(f => itemMapAreas.Contains(f)))
                    {
                        // Console.WriteLine($"Removing {string.Join(",", fogs)} - {string.Join(" ", ids)}");
                        List<string> maps = ids.Select(id => id.MapName).Where(x => x != null).Distinct().ToList();
                        // If it's in map-only areas, require at least one map to belong to the area
                        fogs = fogs.Where(f => !itemMapAreas.Contains(f) || maps.Any(m => mapOnlyFogAreas.TryGetValue(m, out List<string> mapAreas) && mapAreas.Contains(f))).ToList();
                    }

                    areas.AddRange(fogs);
                    // Handle QuestReqs in a later pass, using slotAnn.Key to find the actual areas
                    string text = $"{slotAnn.Text.TrimEnd('.')}. ";
                    if (text.Contains("aaaaa"))
                    {
                        if (slotAnn.DebugText.Last().Contains("o'clock"))
                        {
                            text = slotAnn.DebugText.Last().Split(" - ")[0] + ". ";
                        }
                        else
                        {
                            text = "";
                        }
                    }
                    List<string> debug = new List<string>
                    {
                        $"{text}Replaces {string.Join(", ", items.Select(i => game.Name(i)))}",
                    };
                    if (ids.Count > 0)
                    {
                        debug.Add($"{string.Join(", ", ids.Select(i => game.EntityName(i, true, true)).Distinct())}");
                    }
                    KeyItemLoc loc = existLoc;
                    if (loc == null)
                    {
                        loc = new KeyItemLoc { Key = slotAnn.Key };
                        locs.Items.Add(loc);
                    }
                    loc.DebugText = debug;
                    loc.AArea = string.Join(" ", areas);
                    loc.Lots = lots.Count == 0 ? null : string.Join(" ", lots);
                    loc.Shops = shops.Count == 0 ? null : string.Join(" ", shops);
                    if (loc.Area == null && areas.Count > 1)
                    {
                        // To disambiguate. Put more specific areas first since they usually have the other area as prefix
                        loc.Area = string.Join(" ", Enumerable.Reverse(areas));
                    }
                }
            }
            // Rold flag and lot is GameData.EldenRingBase + 2010, or 1032502010
            // Instead of adding it here, preserve the behavior of claiming it doesn't exist in fog gate rando
            locs.Items = locs.Items.OrderBy(l => fogOrder.IndexOf(l.OrderArea)).ToList();

            // --------------------------------------------------------------------------------------------------------

            FogLocations enemyLocs = new FogLocations();
            // To combine them
            // if (locs.Enemies != null) enemyLocs.Enemies = locs.Enemies;
            using (var reader = File.OpenText($"{fogDir}/foglocations2.txt")) enemyLocs = deserializer.Deserialize<FogLocations>(reader);
            Dictionary<(string, string), EnemyLoc> existEnemyLocs = enemyLocs.Enemies
                .ToDictionary(e => (e.Map, e.ID), e => e);

            if (opt["outmaps"])
            {
                SortedSet<string> mapTiers = new SortedSet<string>();
                foreach (EnemyLocArea area in enemyLocs.EnemyAreas)
                {
                    if (area.MainMap == null) continue;
                    string name = area.Name;
                    int tier = area.ScalingTier;
                    foreach (string map in area.MainMap.Split(' '))
                    {
                        // Transform _10, and don't include _01 _02 etc
                        if (!map.EndsWith("_00")) continue;
                        List<byte> mapId = GameData.ParseMap(map);
                        string parts = string.Join("", mapId.Select(p => $"{p:X2}"));
                        mapTiers.Add($"    {{0x{parts}, {tier}}}, // {map} {name}");
                    }
                }
                Console.Write("#pragma once\n\n#include \"pch.h\"\n\n#include <map>\n\n");
                Console.Write($"std::map<uint32_t, int> mapTiers = {{\n");
                foreach (string line in mapTiers) Console.Write(line + "\n");
                Console.Write($"}};\n");
                return;
            }

            // Continuing to use items for enemy locations
            (string, Vector3) entityMapName(string fullMap, string partName, Vector3 pos)
            {
                if (!fullMap.EndsWith("0") && partName.StartsWith("m") && partName.Contains('-'))
                {
                    // 02 parts will contain the actual map in the name
                    string partMap = partName.Split('-').First();
                    return (partMap, pos + coord.RelocationOffset(fullMap, partMap));
                }
                return (fullMap, pos);
            }
            List<FogCoordinate> coords = new List<FogCoordinate>();
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = entry.Key;
                MSBE msb = entry.Value;
                foreach (MSBE.Part e in msb.Parts.GetEntries())
                {
                    // Item ids are rewritten to their entity map
                    (string entMap, Vector3 pos) = entityMapName(map, e.Name, e.Position);
                    EntityId id = new EntityId(entMap, e.Name);
                    if (entityFogAreas.TryGetValue(id, out KeyItemLoc area))
                    {
                        coords.Add(new FogCoordinate
                        {
                            Map = entMap,
                            Pos = pos,
                            Area = area.ActualArea,
                        });
                    }
                    // Feedback loop from definitely classified enemies
                    else if (existEnemyLocs.TryGetValue((map, e.Name), out EnemyLoc loc) && loc.Area != null && !loc.Area.Contains(' '))
                    {
                        coords.Add(new FogCoordinate
                        {
                            Map = entMap,
                            Name = e.Name,
                            Pos = pos,
                            Area = loc.Area,
                        });
                    }
                }
            }
            // Is there a scaled c1000? like in Archdragon
            HashSet<string> excludeModel = new HashSet<string>
            {
                "c0100", "c1000", "c0110",
            };
            Dictionary<string, List<string>> mapFogAreas = new Dictionary<string, List<string>>();
            HashSet<string> mixedBossAreas = new HashSet<string> { "caelid_radahn", "volcano_rykard", "erdtree" }; // Fire Giant also?
            HashSet<string> trivialEnemyAreas = new HashSet<string> { "liurnia_slumbering" };
            foreach (FogArea area in fogAreas.Values)
            {
                string name = area.Name;
                if (area.DefeatFlag > 0 && !mixedBossAreas.Contains(name)) continue;
                if (name.EndsWith("_chest") || trivialEnemyAreas.Contains(name)) continue;
                if (area.Maps != null)
                {
                    foreach (string map in area.Maps.Split(' '))
                    {
                        AddMulti(mapFogAreas, map, name);
                    }
                }
            }
            // 11050320 is leyndell2_divinebridge, but the real area is leyndell2
            // c6001_9176 (Eagle) in m12_02_00_00 (Siofra River) should not in aqueduct, from AEG099_620_9074 in siofra_nokron
            // c3670_9037 (Albinauric Lookout) in m16_00_00_00 (Volcano Manor) is inquisition chamber
            // 34130298 is caelid tower basement, just combine with caelid tower
            // _91 name in m35 may be catacombs
            string[][] similarLocGroups = new[]
            {
                new[]{ "altus", "gelmir", "outskirts" },
                new[]{ "bellum", "liurnia", "liurnia_postmanor" },
                new[]{ "limgrave", "stormhill", "peninsula", "caelid", "dragonbarrow" },
            };
            Dictionary<string, HashSet<string>> similarLocs = similarLocGroups
                .SelectMany(gs => gs.Select(g => (g, new HashSet<string>(gs))))
                .ToDictionary(e => e.Item1, e => e.Item2);
            Dictionary<int, EnemyData> defaultData = new Dictionary<int, EnemyData>();
            Dictionary<string, List<EnemyInfo>> areaInfos = new Dictionary<string, List<EnemyInfo>>();
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = entry.Key;
                MSBE msb = entry.Value;
                foreach (MSBE.Part.Enemy e in msb.Parts.Enemies)
                {
                    if (excludeModel.Contains(e.ModelName)) continue;
                    (string entMap, Vector3 pos) = entityMapName(map, e.Name, e.Position);
                    EntityId id = new EntityId(entMap, e.Name, (int)e.EntityID, e.NPCParamID, e.CharaInitID, e.EntityGroupIDs.Where(g => g > 0).Select(e => (int)e).ToList());
                    id.OriginalMapName = map;
                    id.Position = pos;
                    string name = $"{game.EntityName(id, true, true)}";
                    (string, string) key = (map, e.Name);
                    if (!nameInfos.TryGetValue(key, out EnemyInfo info)) throw new Exception($"Unknown {name}?");
                    // Make fake default data for scaling. NPC, Model, MainMap are needed. Also Col, Group
                    defaultData[info.ID] = new EnemyData
                    {
                        Name = e.Name,
                        NPC = e.NPCParamID,
                        Model = e.ModelName,
                        MainMap = map,
                        Col = e.CollisionPartName,
                        Group = id.GroupIds,
                    };

                    // TODO: Include summonable helpers and quest invasions, to avoid e.g. Alexander completely wrecking
                    // TODO: What about some DupeOnly enemies like clumps and snails?
                    // if (info.Class == EnemyClass.None) continue;
                    if (!mapFogAreas.TryGetValue(entMap, out List<string> compat)) throw new Exception($"Unknown fog map {entMap} for {name}");
                    string guess;
                    if (enemyIdFlags.TryGetValue(info.ID, out int flag) && defeatFogAreas.TryGetValue(flag, out FogArea fogArea))
                    {
                        guess = fogArea.Name;
                        name += " - from boss";
                    }
                    else if (entityFogAreas.TryGetValue(id, out KeyItemLoc area) && compat.Contains(area.ActualArea))
                    {
                        guess = area.ActualArea;
                        name += " - from item";
                    }
                    else
                    {
                        List<FogCoordinate> topCoords = coords
                            .Where(c => c.Map == entMap && c.Name != e.Name)
                            .OrderBy(f => Vector3.DistanceSquared(f.Pos, pos))
                            .ToList();
                        List<string> areas = topCoords.Take(5).Select(a => a.Area).Where(a => compat.Contains(a)).Distinct().ToList();
                        if (areas.Count > 1 && similarLocs.TryGetValue(areas[0], out HashSet<string> sim) && areas.All(a => sim.Contains(a)))
                        {
                            areas = new List<string> { areas[0] };
                        }
                        if (areas.Count == 0)
                        {
                            areas = compat;
                        }
                        areas = classifier.FogClassify(areas, id);
                        guess = string.Join(" ", areas);
                    }
                    string colName = null;
                    // This is not reliably present, mainly legacy dungeons.
                    // TODO: Generalize to play regions? Randomizer will preserve existing ones, anyway.
                    if (e.CollisionPartName != null)
                    {
                        // MSBE.Part.Collision col = msb.Parts.Collisions.Find(c => c.Name == e.CollisionPartName);
                        // if (col.PlayRegionID > 0) colName = $"{col.PlayRegionID}";
                        // Console.WriteLine($"{name} - {guess}: {e.CollisionPartName}: {col.PlayRegionID}");
                        colName = e.CollisionPartName;
                    }
                    existEnemyLocs.TryGetValue(key, out EnemyLoc loc);
                    if (loc == null)
                    {
                        loc = new EnemyLoc
                        {
                            Map = map,
                            ID = e.Name,
                        };
                        enemyLocs.Enemies.Add(loc);
                        existEnemyLocs[key] = loc;
                    }
                    // TODO: Null these out, not part of enemy definition
                    loc.Col = colName;
                    loc.DebugText = name;
                    loc.AArea = guess;
                    if ((loc.Area == null && guess.Contains(' ')) || (loc.Area != null && loc.Area.Contains(' ') && guess.Contains(' ')))
                    {
                        // To disambiguate, but also it can become more ambiguous expanding neighbor count
                        loc.Area = loc.AArea;
                    }
                    else if (loc.Area == loc.AArea)
                    {
                        // Actually, if we've definitely classified it, keep that
                        // loc.Area = null;
                    }
                    if (loc.Area == "correct me" && !loc.AArea.Contains(' '))
                    {
                        loc.Area = null;
                    }
                    // Console.WriteLine($"{name}: {guess}");
                    string result = loc.ActualArea;
                    if (!string.IsNullOrEmpty(result) && !result.Contains(' '))
                    {
                        AddMulti(areaInfos, result, info);
                    }
                }
            }
            // I guess keep them separate for now
            enemyLocs.Enemies = enemyLocs.Enemies.OrderBy(e => (e.Map, e.ID)).ToList();
            // Fill in scaling data now
            ScalingEffects scaling = new ScalingEffects(game);
            Dictionary<int, int> scalingTiers = scaling.InitializeEldenScaling(defaultData, new Dictionary<int, List<int>>());
            // Make a bunch of maps for area mapping
            // Majority per enemy in area: ScalingTier, can do last
            // Majority per map: MainMap
            // Exclusive per area: Groups, Cols
            Dictionary<T, int> getHist<T>(IEnumerable<T> es) => es.GroupBy(e => e).ToDictionary(g => g.Key, g => g.Count());
            Dictionary<string, List<int>> areaScaling = new Dictionary<string, List<int>>();
            SortedDictionary<string, List<string>> mapAreas = new();
            SortedDictionary<int, SortedSet<string>> groupAreas = new();
            SortedDictionary<string, SortedSet<string>> colAreas = new();
            Dictionary<string, EnemyLocArea> enemyLocAreas = new();
            enemyLocs.EnemyAreas = new List<EnemyLocArea>();
            HashSet<string> tier1Areas = new HashSet<string> { "limgrave", "stormhill" };
            Dictionary<string, int> manualAreas = new Dictionary<string, int>
            {
                ["limgrave_murkwatercave_boss"] = 3, // Patches
                ["volcano"] = 10, // Ghiza
            };
            foreach ((string area, List<EnemyInfo> enemies) in areaInfos.OrderBy(e => fogConfig.Areas.FindIndex(ar => ar.Name == e.Key)))
            {
                // Console.WriteLine($"{area}: {enemies.Count}");
                foreach (EnemyInfo info in enemies)
                {
                    EnemyData enemy = defaultData[info.ID];
                    if (scalingTiers.TryGetValue(info.ID, out int tier))
                    {
                        AddMulti(areaScaling, area, tier);
                        if (opt["enemyarea"] && area == "x" && tier > 0) Console.WriteLine($"{area} tier {tier}: {existEnemyLocs[(enemy.MainMap, enemy.Name)].DebugText}");
                    }
                    AddMulti(mapAreas, enemy.MainMap, area);
                    if (enemy.Col != null)
                    {
                        string col = $"{enemy.MainMap}_{enemy.Col}";
                        AddMulti(colAreas, col, area);
                    }
                    foreach (int group in enemy.Group)
                    {
                        AddMulti(groupAreas, group, area);
                    }
                }
                Dictionary<int, int> scalingHist = getHist(areaScaling[area]);
                int areaTier = scalingHist.MaxBy(e => e.Value).Key;
                if (scalingHist.Count > 1)
                {
                    bool simple = false;
                    if (manualAreas.TryGetValue(area, out int manualTier))
                    {
                        simple = true;
                        areaTier = manualTier;
                    }
                    else if (fogAreas[area].DefeatFlag > 0)
                    {
                        simple = true;
                        areaTier = scalingHist.MaxBy(e => e.Key).Key;
                    }
                    else if (scalingHist.ContainsKey(1) && !tier1Areas.Contains(area))
                    {
                        simple = scalingHist.Count == 2;
                        areaTier = scalingHist.Where(e => e.Key > 1).MaxBy(e => e.Value).Key;
                    }
                    if (opt["enemyarea"] && !simple) Console.WriteLine($"{area} ({areaTier}) has tiers: {string.Join(", ", scalingHist.OrderBy(e => e.Key).Select(e => $"tier {e.Key} = {e.Value} count"))}");
                }
                EnemyLocArea enemyArea = new EnemyLocArea { Name = area, ScalingTier = areaTier };
                enemyLocAreas[area] = enemyArea;
                enemyLocs.EnemyAreas.Add(enemyArea);
            }
            // Go through all of the dictionaries and fill in items
            string addSpaceItem(string exist, object obj) => exist == null ? $"{obj}" : $"{exist} {obj}";
            HashSet<string> separateBosses = new HashSet<string>(fogAreas.Values.Where(area => area.DefeatFlag > 0 && !mixedBossAreas.Contains(area.Name)).Select(area => area.Name));
            foreach ((string map, List<string> areas) in mapAreas)
            {
                Dictionary<string, int> hist = getHist(areas);
                string main = hist.MaxBy(e => e.Value).Key;
                if (hist.Count > 1)
                {
                    bool simple = false;
                    List<string> nonBoss = hist.OrderByDescending(e => e.Value).Select(e => e.Key).Where(a => !separateBosses.Contains(a)).ToList();
                    if (nonBoss.Count > 0)
                    {
                        simple = nonBoss.Count == 1;
                        main = nonBoss[0];
                    }
                    // Exclude boss areas
                    if (opt["enemyarea"] && !simple) Console.WriteLine($"Map {map} has areas: {string.Join(", ", hist.OrderByDescending(e => e.Value).Select(e => $"{e.Key} = {e.Value} count"))}");
                }
                EnemyLocArea loc = enemyLocAreas[main];
                loc.MainMap = addSpaceItem(loc.MainMap, map);
            }
            foreach ((int group, SortedSet<string> areas) in groupAreas)
            {
                if (opt["enemyarea"] && areas.Count > 1 && areas.Any(a => separateBosses.Contains(a)))
                {
                    Console.WriteLine($"Group {group} has areas {string.Join(", ", areas)}");
                }
                if (areas.Count == 1)
                {
                    EnemyLocArea loc = enemyLocAreas[areas.First()];
                    loc.Groups = addSpaceItem(loc.Groups, group);
                }
            }
            foreach ((string col, SortedSet<string> areas) in colAreas)
            {
                if (opt["enemyarea"] && areas.Count > 1)
                {
                    Console.WriteLine($"Col {col} has areas {string.Join(", ", areas)}");
                }
                if (areas.Count == 1)
                {
                    EnemyLocArea loc = enemyLocAreas[areas.First()];
                    loc.Cols = addSpaceItem(loc.Cols, col);
                }
            }
            foreach (EnemyLocArea loc in enemyLocs.EnemyAreas)
            {
                FogArea fogArea = fogAreas[loc.Name];
                // Note: evergaol is currently open area, but it can have added enemies
                if (opt["enemyarea"] && fogArea.DefeatFlag > 0 && fogArea.OpenArea == null && loc.Cols == null && loc.Groups == null)
                {
                    Console.WriteLine($"Missing cols or groups: {loc.Name} tier {loc.ScalingTier}");
                }
            }

            if (opt["outenemy"])
            {
                Console.WriteLine(GameData.Serializer.Serialize(enemyLocs));
            }
            if (opt["outyaml"])
            {
                Console.WriteLine(GameData.Serializer.Serialize(locs));
            }
        }

        public void FogDS3(GameData game, LocationData data, AnnotationData ann)
        {
            Dictionary<int, string> names = GetDS3Bonfires(game);
            List<Path> paths = new List<Path>
            {
                new Path
                {
                    Area = "firelink_cemetery",
                    Maps = new List<string> { "firelink" },
                    Conds = new List<int> { 1 },
                    Bonfires = new List<int>
                    {
                        4000110,  // Cemetery Start
                        4001951,  // Cemetery of Ash
                        4001952,  // Iudex Gundyr
                    }
                },
                new Path
                {
                    // probably more is here than in firelink_front, but we'll see
                    Area = "firelink",
                    Maps = new List<string> { "firelink" },
                    Conds = new List<int> { 1 },
                    Bonfires = new List<int>
                    {
                        4001952,  // Iudex Gundyr
                        4001950,  // Firelink Shrine
                    }
                },
                new Path
                {
                    Area = "highwall",
                    Maps = new List<string> { "highwall" },
                    Bonfires = new List<int>
                    {
                        3001950,  // High Wall of Lothric
                        3001955,  // Tower on the Wall
                        3001952,  // Vordt of the Boreal Valley
                    }
                },
                new Path
                {
                    Area = "settlement",
                    Maps = new List<string> { "settlement" },
                    Bonfires = new List<int>
                    {
                        3101954,  // Foot of the High Wall
                        3101950,  // Undead Settlement
                        3101952,  // Cliff Underside
                        3101953,  // Dilapidated Bridge
                        3101951,  // Pit of Hollows
                    }
                },
                new Path
                {
                    Area = "settlement_tower",  // ???
                    Maps = new List<string> { "settlement" },
                    Bonfires = new List<int>
                    {
                        3301956,  // Road of Sacrifices
                        3101951,  // Pit of Hollows
                    }
                },
                new Path
                {
                    Area = "farronkeep_start",
                    Maps = new List<string> { "farronkeep" },
                    Bonfires = new List<int>
                    {
                        3301956,  // Road of Sacrifices
                        3301950,  // Halfway Fortress
                    }
                },
                new Path
                {
                    Area = "farronkeep_road",
                    Maps = new List<string> { "farronkeep" },
                    Bonfires = new List<int>
                    {
                        3301950,  // Halfway Fortress
                        3301957,  // Crucifixion Woods
                        3301952,  // Crystal Sage
                    }
                },
                new Path
                {
                    Area = "farronkeep",
                    Maps = new List<string> { "farronkeep" },
                    Bonfires = new List<int>
                    {
                        3301953,  // Farron Keep
                        3301954,  // Keep Ruins
                        3301958,  // Farron Keep Perimeter
                        3301951,  // Abyss Watchers
                    }
                },
                new Path
                {
                    Area = "cathedral_start",
                    Maps = new List<string> { "farronkeep", "cathedral" },
                    Bonfires = new List<int>
                    {
                        3301952,  // Crystal Sage
                        3501953,  // Cathedral of the Deep
                        3501950,  // Cleansing Chapel
                    }
                },
                new Path
                {
                    Area = "cathedral",
                    Maps = new List<string> { "cathedral" },
                    Bonfires = new List<int>
                    {
                        3501950,  // Cleansing Chapel
                        3501951,  // Deacons of the Deep
                    }
                },
                new Path
                {
                    Area = "catacombs",
                    Maps = new List<string> { "catacombs" },
                    Bonfires = new List<int>
                    {
                        3301951,  // Abyss Watchers
                        3801956,  // Catacombs of Carthus
                        3801950,  // High Lord Wolnir
                        3701957,  // Irithyll of the Boreal Valley
                    }
                },
                new Path
                {
                    Area = "catacombs_prelake",
                    Maps = new List<string> { "catacombs" },
                    Bonfires = new List<int>
                    {
                        3801950,  // High Lord Wolnir
                        3801951,  // Abandoned Tomb
                    }
                },
                new Path
                {
                    Area = "catacombs_lake",
                    Maps = new List<string> { "catacombs" },
                    Bonfires = new List<int>
                    {
                        3801951,  // Abandoned Tomb
                        3801952,  // Old King's Antechamber
                        3801953,  // Demon Ruins
                        3801954,  // Old Demon King
                    }
                },
                new Path
                {
                    Area = "irithyll",
                    Maps = new List<string> { "irithyll" },
                    Bonfires = new List<int>
                    {
                        3701957,  // Irithyll of the Boreal Valley
                        3701954,  // Central Irithyll
                        3701950,  // Church of Yorshka
                        3701951,  // Pontiff Sulyvahn
                    }
                },
                new Path
                {
                    Area = "irithyll_anorlondo",
                    Maps = new List<string> { "irithyll" },
                    Bonfires = new List<int>
                    {
                        3701951,  // Pontiff Sulyvahn
                        3701956,  // Water Reserve
                        3701958,  // Prison Tower
                        3701953,  // Anor Londo
                        3701952,  // Aldrich, Devourer of Gods
                    }
                },
                new Path
                {
                    Area = "irithyll_manor",
                    Maps = new List<string> { "irithyll" },
                    Bonfires = new List<int>
                    {
                        3701955,  // Distant Manor
                        3901950,  // Irithyll Dungeon
                    }
                },
                new Path
                {
                    Area = "dungeon",
                    Maps = new List<string> { "dungeon" },
                    Bonfires = new List<int>
                    {
                        3901950,  // Irithyll Dungeon
                        3901952,  // Profaned Capital
                        3901951,  // Yhorm the Giant
                    }
                },
                new Path
                {
                    Area = "dungeon_profaned",
                    Maps = new List<string> { "dungeon" },
                    Bonfires = new List<int>
                    {
                        3901950,  // Irithyll Dungeon
                        3901952,  // Profaned Capital
                        3901951,  // Yhorm the Giant
                    }
                },
                new Path
                {
                    Area = "highwall_garden",
                    Maps = new List<string> { "highwall" },
                    Bonfires = new List<int>
                    {
                        3001954,  // Dancer of the Boreal Valley
                        3001951,  // Oceiros, the Consumed King
                    }
                },
                new Path
                {
                    Area = "untended",
                    Maps = new List<string> { "firelink" },
                    Conds = new List<int> { 2 },
                    Bonfires = new List<int>
                    {
                        4001953,  // Untended Graves
                        4001954,  // Champion Gundyr
                    }
                },
                new Path
                {
                    Area = "untended_postgundyr",
                    Maps = new List<string> { "firelink" },
                    Conds = new List<int> { 2 },
                    Bonfires = new List<int>
                    {
                        4001954,  // Champion Gundyr
                        4001950,  // Firelink Shrine
                    }
                },
                new Path
                {
                    Area = "lothric",
                    Maps = new List<string> { "lothric" },
                    Bonfires = new List<int>
                    {
                        3011950,  // Lothric Castle
                        3011952,  // Dragon Barracks
                        3011951,  // Dragonslayer Armour
                    }
                },
                new Path
                {
                    Area = "archives",
                    Maps = new List<string> { "archives" },
                    Bonfires = new List<int>
                    {
                        3411951,  // Grand Archives
                        3411950,  // Twin Princes
                    }
                },
                new Path
                {
                    Area = "archdragon_start",
                    Maps = new List<string> { "archdragon" },
                    Bonfires = new List<int>
                    {
                        3202900,  // Archdragon Start
                        3201950,  // Archdragon Peak
                    }
                },
                new Path
                {
                    Area = "archdragon_ancientwyvern",
                    Maps = new List<string> { "archdragon" },
                    Bonfires = new List<int>
                    {
                        3201950,  // Archdragon Peak
                        3201953,  // Dragon-Kin Mausoleum
                    }
                },
                new Path
                {
                    Area = "archdragon",
                    Maps = new List<string> { "archdragon" },
                    Bonfires = new List<int>
                    {
                        3201953,  // Dragon-Kin Mausoleum
                        3201951,  // Nameless King
                        3201952,  // Great Belfry
                    }
                },
                new Path
                {
                    Area = "ariandel",
                    Maps = new List<string> { "ariandel" },
                    Bonfires = new List<int>
                    {
                        4501951,  // Snowfield
                        4501952,  // Rope Bridge Cave
                        4501953,  // Corvian Settlement
                        4501954,  // Snowy Mountain Pass
                    }
                },
                new Path
                {
                    Area = "ariandel",
                    Maps = new List<string> { "ariandel" },
                    Bonfires = new List<int>
                    {
                        4501952,  // Rope Bridge Cave
                        4501957,  // Depths of the Painting
                        4501956,  // Champion's Gravetender
                    }
                },
                new Path
                {
                    Area = "ariandel_vilhelm",
                    Maps = new List<string> { "ariandel" },
                    Bonfires = new List<int>
                    {
                        4501954,  // Snowy Mountain Pass
                        4501955,  // Ariandel Chapel
                        4501950,  // Sister Friede
                    }
                },
                new Path
                {
                    Area = "dregheap_start",
                    Maps = new List<string> { "dregheap" },
                    Bonfires = new List<int>
                    {
                        5001951,  // The Dreg Heap
                        5001952,  // Earthen Peak Ruins
                    }
                },
                new Path
                {
                    Area = "dregheap",
                    Maps = new List<string> { "dregheap" },
                    Bonfires = new List<int>
                    {
                        5001952,  // Earthen Peak Ruins
                        5001953,  // Within the Earthen Peak Ruins
                        5001950,  // The Demon Prince
                    }
                },
                new Path
                {
                    Area = "ringedcity",
                    Maps = new List<string> { "ringedcity" },
                    Bonfires = new List<int>
                    {
                        5101952,  // Mausoleum Lookout
                        5101953,  // Ringed Inner Wall
                        5101954,  // Ringed City Streets
                        5101955,  // Shared Grave
                        5101950,  // Church of Filianore
                        // 5101951,  // Darkeater Midir
                    }
                },
                new Path
                {
                    Area = "filianore",
                    Maps = new List<string> { "filianore" },
                    Bonfires = new List<int>
                    {
                        5111951,  // Filianore's Rest
                        5111950,  // Slave Knight Gael
                    }
                },
                new Path
                {
                    Area = "kiln",
                    Maps = new List<string> { "kiln" },
                    Bonfires = new List<int>
                    {
                        4101951,  // Kiln of the First Flame
                        4101952,  // The First Flame
                    }
                },
            };

            // TODO: May need to switch to map + name
            Dictionary<int, Vector3> points = new Dictionary<int, Vector3>();
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;
                foreach (MSB3.Part e in msb.Parts.GetEntries())
                {
                    if (names.ContainsKey(e.EntityID)) points[e.EntityID] = e.Position;
                }
                foreach (MSB3.Region e in msb.Regions.GetEntries())
                {
                    if (names.ContainsKey(e.EntityID)) points[e.EntityID] = e.Position;
                }
            }
            string pathText(int p)
            {
                int first = paths[p].Bonfires.First();
                int last = paths[p].Bonfires.Last();
                return $"{paths[p].Area} [{names[first]}->{names[last]}]";
            }

            // Overall approach to produce heuristic area classifications for item lots and also enemies
            // ~ seems to be if there are no items in that area in fog rando
            Dictionary<string, string> fogToItemArea = new Dictionary<string, string>
            {
                ["firelink_cemetery"] = "firelink_cemetery",
                ["firelink_iudexgundyr"] = "iudexgundyr",
                ["firelink_front"] = "firelink",
                ["firelink"] = "firelink",
                ["firelink_bellfront"] = "firelink",
                ["firelink_roof"] = "firelink_roof",
                ["firelink_belltower"] = "firelink_belltower",
                ["highwall"] = "highwall",
                ["highwall_emma"] = "highwall",
                ["highwall_darkwraith"] = "highwall_darkwraith",
                ["highwall_greirat"] = "highwall_greirat",
                ["highwall_vordt"] = "vordt",
                ["settlement"] = "settlement",
                ["settlement_greatwood"] = "greatwood",
                ["settlement_tower"] = "settlement",
                ["farronkeep_start"] = "farronkeep_road",
                ["farronkeep_road"] = "farronkeep_road",
                ["farronkeep_crystalsage"] = "crystalsage",
                ["farronkeep"] = "farronkeep",
                ["farronkeep_abysswatchers"] = "abysswatchers",
                ["farronkeep_postwatchers"] = "~farronkeep",
                ["cathedral_start"] = "cathedral",
                ["cathedral"] = "cathedral",
                ["cathedral_prerosaria"] = "cathedral",
                ["cathedral_rosaria"] = "cathedral",
                ["cathedral_deacons"] = "deacons",
                ["catacombs"] = "catacombs",
                ["catacombs_wolnirroom"] = "~catacombs",
                ["catacombs_wolnir"] = "wolnir",
                ["catacombs_prelake"] = "catacombs",
                ["catacombs_lake"] = "catacombs_lake",
                ["catacombs_olddemonking"] = "olddemonking",
                ["irithyll_start"] = "catacombs",
                ["irithyll_bridge"] = "catacombs",
                ["irithyll"] = "irithyll",
                ["irithyll_abovedorhys"] = "irithyll",
                ["irithyll_dorhys"] = "irithyll",
                ["irithyll_manor"] = "irithyll",
                ["irithyll_pontiff"] = "pontiff",
                ["irithyll_yorshka"] = "irithyll",
                ["irithyll_postpontiff"] = "~irithyll",
                ["irithyll_anorlondo"] = "irithyll",
                ["irithyll_abovepontiff"] = "irithyll",
                ["irithyll_aldrich"] = "aldrich",
                ["irithyll_abovealdrich"] = "irithyll",
                ["irithyll_aldrichleft"] = "~irithyll",
                ["irithyll_aldrichright"] = "~irithyll",
                ["dungeon"] = "dungeon",
                ["dungeon_jailbreak"] = "dungeon_jailbreak",
                ["dungeon_jailerscell"] = "dungeon_jailerscell",
                ["dungeon_profaned"] = "dungeon_profaned",
                ["dungeon_oldcell"] = "dungeon_oldcell",
                ["dungeon_yhorm"] = "yhorm",
                ["highwall_dancer"] = "dancer",
                ["highwall_postdancer"] = "dancer",
                ["highwall_abovedancer"] = "dancer",
                ["lothric"] = "lothric",
                ["lothric_dragonslayerarmour"] = "dragonslayerarmour",
                ["lothric_postarmour"] = "lothric",
                ["archives_start"] = "lothric",
                ["lothric_gotthard"] = "lothric_gotthard",
                ["archives"] = "archives",
                ["archives_shortcutbottom"] = "~archives",
                ["archives_shortcuttop"] = "~archives",
                ["archives_preprinces"] = "~archives",
                ["archives_twinprinces"] = "twinprinces",
                ["highwall_garden"] = "highwall_garden",
                ["highwall_oceiros"] = "oceiros",
                ["highwall_postoceiros"] = "highwall_garden",
                ["untended"] = "untended",
                ["untended_championgundyr"] = "championgundyr",
                ["untended_postgundyr"] = "untended",
                ["untended_farron"] = "untended_farron",
                ["archdragon_start"] = "archdragon",
                ["archdragon_shortcuttop"] = "~archdragon",
                ["archdragon_shortcutbottom"] = "~archdragon",
                ["archdragon_prewyvern"] = "~archdragon",
                ["archdragon_ancientwyvern"] = "archdragon",
                ["archdragon_wyverndoor"] = "~archdragon",
                ["archdragon_abovewyvern"] = "~archdragon",
                ["archdragon"] = "archdragon",
                ["archdragon_beforenameless"] = "archdragon",
                ["archdragon_namelessking"] = "namelessking",
                ["archdragon_belownameless"] = "archdragon",
                ["ariandel"] = "ariandel",
                ["ariandel_gravetender"] = "gravetender",
                ["ariandel_vilhelm"] = "ariandel_vilhelm",
                ["ariandel_dunnel"] = "ariandel_vilhelm",
                ["ariandel_friede"] = "friede",
                ["dregheap_start"] = "dregheap",
                ["dregheap"] = "dregheap",
                ["dregheap_predemon"] = "~dregheap",
                ["dregheap_demonprince"] = "demonprince",
                ["dregheap_postdemon"] = "dregheap",
                ["ringedcity"] = "ringedcity",
                ["ringedcity_halflight"] = "halflight",
                ["ringedcity_posthalflight"] = "~ringedcity",
                ["ringedcity_premidir"] = "~ringedcity",
                ["ringedcity_midir"] = "midir",
                ["filianore"] = "filianore",
                ["filianore_gael"] = "gael",
                ["filianore_shira"] = "filianore",
                ["kiln_start"] = "kiln",
                ["kiln"] = "kiln",
                ["kiln_soulofcinder"] = "soulofcinder",
            };
            Dictionary<string, List<string>> itemToFogArea = fogToItemArea.Where(e => !e.Value.StartsWith("~")).GroupBy(e => e.Value).ToDictionary(g => g.Key, g => g.Select(e => e.Key).ToList());
            // Iteration order is not reliable, but anyway, rely on it
            List<string> fogOrder = fogToItemArea.Select(e => e.Key).ToList();

            FogLocations locs = new FogLocations();
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = File.OpenText("locations.txt")) locs = deserializer.Deserialize<FogLocations>(reader);

            bool addItems = locs.Items.Count == 0;
            Dictionary<string, KeyItemLoc> storedItems = locs.Items.ToDictionary(l => l.Key, l => l);
            Dictionary<EntityId, KeyItemLoc> entityFogAreas = new Dictionary<EntityId, KeyItemLoc>();

            // For each item: find its old area in item rando, new proposed area in fog rando based on that, and entity id
            HashSet<string> excludeKeyItem = new HashSet<string> { "missable", "enemy", "norandom", "nokey", "crow", "end" };

            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                if (!ann.Slots.TryGetValue(locScope, out SlotAnnotation slotAnn)) continue;

                // All item lots and shop items
                SortedSet<int> lots = new SortedSet<int>();
                SortedSet<int> shops = new SortedSet<int>();
                List<EntityId> ids = new List<EntityId>();
                List<ItemKey> items = new List<ItemKey>();
                foreach (SlotKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.Location(itemLocKey);
                    ItemScope scope = location.Scope;
                    if (scope.Type != ScopeType.EVENT && scope.Type != ScopeType.ENTITY) continue;
                    items.Add(itemLocKey.Item);
                    foreach (LocationKey locKey in location.Keys)
                    {
                        if (locKey.Type == LocationKey.LocationType.LOT) lots.Add(locKey.ID);
                        else shops.Add(locKey.ID);
                        ids.AddRange(locKey.Entities);
                    }
                }
                foreach (int id in lots.ToList())
                {
                    lots.Remove(id + 1);
                }

                if (storedItems.TryGetValue(slotAnn.Key, out KeyItemLoc keyLoc))
                {
                    if (keyLoc.Area.Contains(' ')) continue;
                    foreach (EntityId id in ids)
                    {
                        entityFogAreas[id] = keyLoc;
                    }
                }

                // We only care about key item eligible lots/shops, but still can care about item locations, since they can be used to find enemy locations.
                // Make sure not to set any norandom options, including setting ngplusrings
                if (addItems && !slotAnn.HasAnyTags(excludeKeyItem))
                {
                    // Console.WriteLine(slotAnn.Area + " " + slotAnn.QuestReqs + " " + slotAnn.Event);
                    // Find the best area
                    List<string> areas = new List<string>();
                    if (slotAnn.Event != null && itemToFogArea.ContainsKey(slotAnn.Event))
                    {
                        areas.AddRange(itemToFogArea[slotAnn.Event]);
                    }
                    else
                    {
                        if (slotAnn.Area == "firelink" && shops.Count > 0)
                        {
                            areas.Add(slotAnn.Area);
                        }
                        else
                        {
                            areas.AddRange(itemToFogArea[slotAnn.Area]);
                        }
                        if (slotAnn.QuestReqs != null)
                        {
                            foreach (string req in slotAnn.QuestReqs.Split(' '))
                            {
                                if (itemToFogArea.TryGetValue(req, out List<string> reqs)) areas.AddRange(reqs);
                                else areas.Add(req);
                            }
                        }
                    }
                    List<string> debug = new List<string>
                    {
                        $"{slotAnn.Text.TrimEnd('.')}. Replaces {string.Join(", ", items.Select(i => game.Name(i)))}",
                    };
                    if (ids.Count > 0)
                    {
                        debug.Add($"{string.Join(", ", ids.Select(i => game.EntityName(i, true) + (string.IsNullOrWhiteSpace(i.MapName) ? "" : " in " + i.MapName)).Distinct())}");
                    }
                    KeyItemLoc loc = new KeyItemLoc
                    {
                        Key = slotAnn.Key,
                        DebugText = debug,
                        Area = string.Join(" ", areas),
                    };
                    if (lots.Count > 0) loc.Lots = string.Join(" ", lots);
                    if (shops.Count > 0) loc.Shops = string.Join(" ", shops);
                    locs.Items.Add(loc);
                }
            }
            locs.Items = locs.Items.OrderBy(l => fogOrder.IndexOf(l.Area.Split(' ')[0])).ToList();

            ISerializer serializer = new SerializerBuilder().DisableAliases().Build();
            // Console.WriteLine(serializer.Serialize(locs));

            // Algorithm for path:
            // For each path: identify entities for each path, and initial fit
            // For each entity: find the best fit path
            // For each entity: output its item-inferred area and path-inferred area

            // But just use item locations instead tbh
            FogLocations enemyLocs = new FogLocations();
            List<FogCoordinate> coords = new List<FogCoordinate>();
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;
                foreach (MSB3.Part e in msb.Parts.GetEntries())
                {
                    EntityId id = new EntityId(map, e.Name);
                    string partMap = map == "firelink" && (e.MapStudioLayer & 1) == 0 ? "untended" : map;
                    if (entityFogAreas.TryGetValue(id, out KeyItemLoc area))
                    {
                        coords.Add(new FogCoordinate
                        {
                            Map = partMap,
                            Pos = e.Position,
                            Loc = area,
                        });
                    }
                }
            }
            Dictionary<string, List<string>> allowedConnections = new Dictionary<string, List<string>>
            {
                ["highwall"] = new List<string> { "lothric", "untended" },
                ["lothric"] = new List<string> { "highwall", "archives" },
                ["archives"] = new List<string> { "lothric" },
                ["settlement"] = new List<string> { "farronkeep" },
                ["archdragon"] = new List<string> { },
                ["farronkeep"] = new List<string> { "settlement", "cathedral", "catacombs" },
                ["cathedral"] = new List<string> { "farronkeep" },
                ["irithyll"] = new List<string> { "catacombs", "dungeon" },
                ["catacombs"] = new List<string> { "irithyll", "dungeon", "farronkeep" },
                ["dungeon"] = new List<string> { "irithyll", },
                ["firelink"] = new List<string> { },
                ["untended"] = new List<string> { "highwall" },
                ["kiln"] = new List<string> { },
                ["ariandel"] = new List<string> { },
                ["dregheap"] = new List<string> { },
                ["ringedcity"] = new List<string> { },
                ["filianore"] = new List<string> { },
            };
            HashSet<string> excludeModel = new HashSet<string>
            {
                "c0100",  // ?
                "c1000",  // Invisible
                "c1480", "c1490",  // Irithyll phantoms
                "c6120", "c6121",  // Painting child
            };
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;
                foreach (MSB3.Part.Enemy e in msb.Parts.Enemies)
                {
                    if (excludeModel.Contains(e.ModelName)) continue;
                    EntityId id = new EntityId(map, e.Name, e.EntityID, e.NPCParamID, e.CharaInitID, e.EntityGroups.Where(g => g > 0).ToList());
                    string partMap = map == "firelink" && (e.MapStudioLayer & 1) == 0 ? "untended" : map;
                    string name = $"{game.EntityName(id, true)} in {partMap} on {e.CollisionName}";
                    string guess;
                    if (entityFogAreas.TryGetValue(id, out KeyItemLoc area))
                    {
                        guess = area.Area;
                    }
                    else
                    {
                        List<FogCoordinate> topCoords = coords
                            .Where(c => c.Map == partMap || allowedConnections[partMap].Contains(c.Map))
                            .OrderBy(f => Vector3.DistanceSquared(f.Pos, e.Position))
                            .ToList();
                        List<string> areas = topCoords.Take(5).Select(a => a.Loc.Area).Distinct().ToList();
                        guess = string.Join(" ", areas);
                        if (false && areas.Count > 1)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                KeyItemLoc loc = topCoords[i].Loc;
                                Console.WriteLine($"- {loc.Area}: {loc.DebugText[0]}");
                            }
                        }
                    }
                    enemyLocs.Enemies.Add(new EnemyLoc
                    {
                        ID = $"{map} {e.Name} {e.CollisionName}",
                        DebugText = name,
                        Area = guess,
                    });
                    // Console.WriteLine($"{name}: {guess}");
                }
            }
            Console.WriteLine(serializer.Serialize(enemyLocs));
        }

        public class FogLocations
        {
            public List<KeyItemLoc> Items = new List<KeyItemLoc>();
            public List<EnemyLocArea> EnemyAreas = new List<EnemyLocArea>();
            public List<EnemyLoc> Enemies = new List<EnemyLoc>();
        }
        public class KeyItemLoc
        {
            public string Key { get; set; }
            public List<string> DebugText { get; set; }
            public string AArea { get; set; }
            public string Area { get; set; }
            public string ReqAreas { get; set; }
            public string Lots { get; set; }
            public string Shops { get; set; }
            [YamlIgnore]
            public string OrderArea => (AArea ?? Area).Split(' ')[0];
            [YamlIgnore]
            public string ActualArea => (Area ?? AArea).Split(' ')[0];
        }

        public class EnemyLocArea
        {
            public string Name { get; set; }
            public string Groups { get; set; }
            public string Cols { get; set; }
            public string MainMap { get; set; }
            public int ScalingTier { get; set; }
        }

        public class EnemyLoc
        {
            // In ER, Map ID Col are used separated. In DS3 they're combined in ID.
            public string Map { get; set; }
            public string ID { get; set; }
            // TODO: Don't do it
            public string Col { get; set; }
            public string DebugText { get; set; }
            public string AArea { get; set; }
            public string Area { get; set; }
            [YamlIgnore]
            public string ActualArea => (Area ?? AArea).Split(' ')[0];
        }

        public class FogCoordinate
        {
            public string Map { get; set; }
            public string Name { get; set; }
            public Vector3 Pos { get; set; }
            public KeyItemLoc Loc { get; set; }
            public string Area { get; set; }
        }
        public class Path
        {
            public int Section { get; set; }
            public string Area { get; set; }
            public List<string> Maps { get; set; }
            public List<int> Conds { get; set; }
            public List<int> Bonfires { get; set; }
        }

        public Dictionary<int, int> InvestigateDS3Scaling(
            GameData game, List<List<EnemyClass>> typeGroups, Dictionary<int, EnemyInfo> infos, List<Path> paths, Dictionary<int, (int, float)> chosenPath)
        {
            Dictionary<int, MSB3.Part.Enemy> enemies = new Dictionary<int, MSB3.Part.Enemy>();
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;
                foreach (MSB3.Part.Enemy e in msb.Parts.Enemies)
                {
                    enemies[e.EntityID] = e;
                }
            }

            // Findings: Hp is not changed. It's done through maxHpRate.
            // getSoul has some weird data (14 >>> 15), just smooth it out I guess
            // Phys attack power scaling is slightly less than other power scaling types.
            // Other categories then, defense and regist change rate
            List<string> scaleSp =
                ("maxHpRate maxStaminaCutRate physAtkPowerRate magicAtkPowerRate fireAtkPowerRate thunderAtkPowerRate staminaAttackRate darkAttackPowerRate "
                // These are all in DS3 and not Sekiro
                + "physDefRate magicDefRate fireDefRate thunderDefRate darkDefRate registPoisonChangeRate registToxicChangeRate registBloodChangeRate registCurseChangeRate registFrostChangeRate").Split(' ').ToList();
            List<string> scaleNpc = "Hp getSoul".Split(' ').ToList();  // speffect3: 7000 vs 7130. 7060/7092/7040/7070, 7040/7140/7135
            List<string> allFields = scaleSp.Concat(scaleNpc).ToList();
            // Disp: ModelDispMask0 -> ModelDispMask31
            // Npc param has GameClearSpEffectID
            // Bases for enemy models which are basically equivalent, like c1105 and c1106
            HashSet<int> reskins = new HashSet<int> { 1070, 1100, 1105, 1200, 1210, 1240, 1280, 1440, 1445, 2130, 2190 };
            Dictionary<(string, int, int), List<float>> allScales = new Dictionary<(string, int, int), List<float>>();
            Dictionary<int, int> allSections = new Dictionary<int, int>();
            foreach (List<EnemyClass> typeGroup in typeGroups)
            {
                // Consider two enemies the same if they have the same think id, or same disp mask
                // Or for minibosses, if they are just the same model, that's probably fine
                Dictionary<string, List<int>> thinks = new Dictionary<string, List<int>>();
                Dictionary<string, List<int>> masks = new Dictionary<string, List<int>>();
                Dictionary<string, List<int>> bosses = new Dictionary<string, List<int>>();
                List<string> order = new List<string>();
                Dictionary<int, int> sections = new Dictionary<int, int>();
                foreach (KeyValuePair<int, (int, float)> entry in chosenPath.OrderBy(e => (e.Value, e.Key)))
                {
                    int id = entry.Key;
                    EnemyInfo info = infos[id];
                    if (!typeGroup.Contains(info.Class)) continue;
                    MSB3.Part.Enemy e = enemies[id];
                    int path = entry.Value.Item1;
                    int section = paths[path].Section;
                    if (info.Class == EnemyClass.Basic && section == 1 && paths[path].Maps.Contains("firelink"))
                    {
                        // Exclude cemetery hollows, they are super underleveled
                        continue;
                    }
                    if (e.ModelName == "c0000") continue;
                    sections[id] = section;
                    allSections[id] = section;
                    string baseModel = e.ModelName;
                    int modelId = int.Parse(baseModel.Substring(1));
                    modelId -= (modelId % 5);
                    if (reskins.Contains(modelId))
                    {
                        baseModel = $"c{modelId}:d4";
                    }
                    string model = game.ModelName(e.ModelName);
                    if (typeGroup.Contains(EnemyClass.Miniboss) || typeGroup.Contains(EnemyClass.Boss))
                    {
                        AddMulti(bosses, model, id);
                        continue;
                    }
                    string think = $"{model} {e.ThinkParamID}";
                    AddMulti(thinks, think, id);
                    PARAM.Row npc = game.Params["NpcParam"][e.NPCParamID];
                    if (e.NPCParamID > 0 && npc != null)
                    {
                        uint mask = 0;
                        for (int i = 0; i < 32; i++)
                        {
                            if ((byte)npc[$"ModelDispMask{i}"].Value == 1)
                            {
                                mask |= ((uint)1 << i);
                            }
                        }
                        string dispMask = $"{model} 0x{mask:X8}";
                        AddMulti(masks, dispMask, id);
                    }
                }
                foreach (KeyValuePair<string, List<int>> entry in thinks.Concat(masks.Concat(bosses)))
                {
                    if (entry.Value.Count == 1) continue;
                    List<int> secs = entry.Value.Select(i => sections[i]).Distinct().ToList();
                    if (secs.Count == 1) continue;

                    Console.WriteLine($"{entry.Key}: {string.Join(",", entry.Value.Select(i => $"{i}[{sections[i]}]"))}");
                    SortedDictionary<string, List<(int, float)>> fieldValues = new SortedDictionary<string, List<(int, float)>>();
                    foreach (int id in entry.Value)
                    {
                        MSB3.Part.Enemy e = enemies[id];
                        PARAM.Row npc = game.Params["NpcParam"][e.NPCParamID];
                        if (e.NPCParamID == 0 || npc == null) continue;
                        Dictionary<string, float> values = new Dictionary<string, float>();
                        foreach (string f in scaleNpc)
                        {
                            values[f] = float.Parse(npc[f].Value.ToString());
                        }
                        int spVal = (int)npc["spEffectId3"].Value;  // GameClearSpEffectID is for NG+ only, or time-of-day only, or something like that
                        PARAM.Row sp = game.Params["SpEffectParam"][spVal];
                        if (spVal > 0 && sp != null)
                        {
                            foreach (string f in scaleSp)
                            {
                                values[f] = float.Parse(sp[f].Value.ToString());
                            }
                        }
                        foreach (KeyValuePair<string, float> val in values)
                        {
                            AddMulti(fieldValues, val.Key, (sections[id], val.Value));
                        }
                    }
                    foreach (KeyValuePair<string, List<(int, float)>> val in fieldValues)
                    {
                        // Console.WriteLine($"  {val.Key}: {string.Join(", ", val.Value.OrderBy(v => v).Select(v => $"[{v.Item1}]{v.Item2}"))}");
                        Dictionary<int, float> bySection = val.Value.GroupBy(v => v.Item1).ToDictionary(g => g.Key, g => g.Select(v => v.Item2).Average());
                        List<string> sorts = new List<string>();
                        foreach (int i in bySection.Keys)
                        {
                            foreach (int j in bySection.Keys)
                            {
                                if (i >= j) continue;
                                float ratio = bySection[j] / bySection[i];
                                if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio == 1 || ratio == 0) continue;
                                sorts.Add($"{i}{j}: {ratio:f3}x");
                                AddMulti(allScales, (val.Key, i, j), ratio);
                                // Can be used for complete table, but easier to leave out for lower diagonal
                                // AddMulti(allScales, (val.Key, j, i), 1 / ratio);
                            }
                        }
                        if (sorts.Count > 0) Console.WriteLine($"  {val.Key}: {string.Join(", ", sorts)}");
                    }
                }
            }
            foreach (string field in allFields)
            {
                Console.WriteLine($"-- {field} ({allScales.Where(k => k.Key.Item1 == field).Sum(e => e.Value.Count)})");
                for (int i = 1; i <= 5; i++)
                {
                    // row: the target class. column: the source class. value: how much to multiply to place the source in the target.
                    Console.WriteLine("  " + string.Join(" ", Enumerable.Range(1, 5).Select(j => allScales.TryGetValue((field, j, i), out List<float> floats) ? $"{floats.Average():f5}," : "        ")));
                }
            }
            return allSections;
        }

        public void WriteList(GameData game, Dictionary<int, EnemyInfo> fullInfos)
        {
            // Generate things
            HashSet<int> allBonfires = new HashSet<int>
            {
                1001950,  // Dragonspring - Hirata Estate
                1001951,  // Estate Path
                1001952,  // Bamboo Thicket Slope
                1001953,  // Hirata Estate - Main Hall
                1001955,  // Hirata Audience Chamber
                1001954,  // Hirata Estate - Hidden Temple
                1101950,  // Dilapidated Temple
                1101956,  // Ashina Outskirts
                1101951,  // Outskirts Wall - Gate Path
                1101952,  // Outskirts Wall - Stairway
                1101953,  // Underbridge Valley
                1101954,  // Ashina Castle Gate Fortress
                1101955,  // Ashina Castle Gate
                1101957,  // Flames of Hatred
                1111950,  // Ashina Castle
                1111951,  // Upper Tower - Antechamber
                1111957,  // Upper Tower - Ashina Dojo
                1111952,  // Castle Tower Lookout
                1111953,  // Upper Tower - Kuro's Room
                1111956,  // Old Grave
                1111954,  // Great Serpent Shrine
                1111955,  // Abandoned Dungeon Entrance
                1121951,  // Ashina Reservoir
                1121950,  // Near Secret Passage
                1301950,  // Underground Waterway
                1301951,  // Bottomless Hole
                1701955,  // Ashina Depths
                1701954,  // Poison Pool
                1701956,  // Guardian Ape's Burrow
                1501950,  // Hidden Forest
                1501951,  // Mibu Village
                1501952,  // Water Mill
                1501953,  // Wedding Cave Door
                1701957,  // Under-Shrine Valley
                1701950,  // Sunken Valley
                1701951,  // Gun Fort
                1701952,  // Riven Cave
                1701958,  // Bodhisattva Valley
                1701953,  // Guardian Ape's Watering Hole
                2001950,  // Senpou Temple,  Mt. Kongo
                2001951,  // Shugendo
                2001952,  // Temple Grounds
                2001953,  // Main Hall
                2001954,  // Inner Sanctum
                2001955,  // Sunken Valley Cavern
                2001956,  // Bell Demon's Temple
                2501950,  // Fountainhead Palace
                2501951,  // Vermilion Bridge
                2501956,  // Mibu Manor
                2501952,  // Flower Viewing Stage
                2501953,  // Great Sakura
                2501954,  // Palace Grounds
                2501957,  // Feeding Grounds
                2501958,  // Near Pot Noble
                2501955,  // Sanctuary
            };

            // Probably shouldn't use tuples, but too late now
            List<Path> paths = new List<Path>
            {
                // Tutorial
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "ashinareservoir", "ashinacastle" },
                    Conds = new List<int> { 8306, 0 },
                    Bonfires = new List<int>
                    {
                        1121951,  // Ashina Reservoir
                        1121950,  // Near Secret Passage
                    }
                },
                // First stretch of Ashina Outskirts
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "ashinaoutskirts" },
                    Conds = new List<int> { 8302, 1, 8302, -1, 1100330, 1 },
                    Bonfires = new List<int>
                    {
                        1101956,  // Ashina Outskirts
                        1101951,  // Outskirts Wall - Gate Path
                        1101952,  // Outskirts Wall - Stairway
                    }
                },
                // Ashina Outskirts up to Blazing Bull
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "ashinaoutskirts", "ashinacastle" },
                    Conds = new List<int> { 8302, 1, 8302, -1, 8301, 1, 8301, -1, 1100330, 1 },
                    Bonfires = new List<int>
                    {
                        1101952,  // Outskirts Wall - Stairway
                        1101953,  // Underbridge Valley
                        1101954,  // Ashina Castle Gate Fortress
                        1101955,  // Ashina Castle Gate
                        // 1111950,  // Ashina Castle
                    }
                },
                // Hirata 1
                new Path
                {
                    Section = 1,
                    Maps = new List<string> { "hirata" },
                    Conds = new List<int> { 1000353, 1, 1005601, 1, 1000301, 1, 1000900, 1 },
                    Bonfires = new List<int>
                    {
                        1001950,  // Dragonspring - Hirata Estate
                        1001951,  // Estate Path
                        1001952,  // Bamboo Thicket Slope
                        1001953,  // Hirata Estate - Main Hall
                        1001955,  // Hirata Audience Chamber
                        1001954,  // Hirata Estate - Hidden Temple
                    }
                },
                // Ashina Castle to Genichiro
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "ashinacastle" },
                    Conds = new List<int> { 8301, 1, 8302, 1, 8302, -1 },
                    Bonfires = new List<int>
                    {
                        1111950,  // Ashina Castle
                        1111951,  // Upper Tower - Antechamber
                        1111957,  // Upper Tower - Ashina Dojo
                        1111952,  // Castle Tower Lookout
                    }
                },
                // Ashina Castle to Reservoir to Dungeon
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "ashinareservoir" },
                    Conds = new List<int> { 8302, 1, 1120300, 0 },
                    Bonfires = new List<int>
                    {
                        1111950,  // Ashina Castle
                        1121951,  // Ashina Reservoir
                        1301951,  // Bottomless Hole
                    }
                },
                // Dungeon
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "dungeon" },
                    Conds = new List<int> { },
                    Bonfires = new List<int>
                    {
                        1111955,  // Abandoned Dungeon Entrance
                        1301950,  // Underground Waterway
                        1301951,  // Bottomless Hole
                    }
                },
                // Senpou temple
                new Path
                {
                    Section = 2,
                    Maps = new List<string> { "senpou" },
                    Conds = new List<int> { },
                    Bonfires = new List<int>
                    {
                        2001950,  // Senpou Temple,  Mt. Kongo
                        2001951,  // Shugendo
                        2001952,  // Temple Grounds
                        2001953,  // Main Hall
                    }
                },
                // Hidden Forest to Water Mill
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "mibuvillage" },
                    Conds = new List<int> { 1700850, 1, 1700520, 1 },
                    Bonfires = new List<int>
                    {
                        1501950,  // Hidden Forest
                        1501951,  // Mibu Village
                        1501952,  // Water Mill
                    }
                },
                // End of Ashina Depths
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "mibuvillage" },
                    Conds = new List<int> { },
                    Bonfires = new List<int>
                    {
                        1501952,  // Water Mill
                        1501953,  // Wedding Cave Door
                    }
                },
                // Most of Sunken Valley
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "ashinacastle", "sunkenvalley" },
                    Conds = new List<int> { 8301, 1, 8301, -1, 8302, 1, 8302, -1 },
                    Bonfires = new List<int>
                    {
                        1111952,  // Castle Tower Lookout
                        1111956,  // Old Grave
                        1111954,  // Great Serpent Shrine
                        1701957,  // Under-Shrine Valley
                        1701950,  // Sunken Valley
                        1701951,  // Gun Fort
                        1701952,  // Riven Cave
                        1701958,  // Bodhisattva Valley
                        1701953,  // Guardian Ape's Watering Hole
                    }
                },
                // Sunken Valley to Poison Pool path
                new Path
                {
                    Section = 3,
                    Maps = new List<string> { "sunkenvalley" },
                    Conds = new List<int> { 1700850, 0, 1700520, 0 },
                    Bonfires = new List<int>
                    {
                        1701958,  // Bodhisattva Valley
                        1701954,  // Poison Pool
                        1701956,  // Guardian Ape's Burrow
                    }
                },
                // Ashina Castle Revisited, also down to Masanaga
                new Path
                {
                    Section = 4,
                    Maps = new List<string> { "ashinacastle" },
                    Conds = new List<int> { 8301, 0, 8302, 1, 8302, -1 },
                    Bonfires = new List<int>
                    {
                        1111955,  // Abandoned Dungeon Entrance
                        1111950,  // Ashina Castle
                        1111951,  // Upper Tower - Antechamber
                        1111957,  // Upper Tower - Ashina Dojo
                        1111952,  // Castle Tower Lookout
                        1111956,  // Old Grave
                        1111954,  // Great Serpent Shrine
                    }
                },
                // Fountainhead
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "fountainhead" },
                    Conds = new List<int> { },
                    Bonfires = new List<int>
                    {
                        2501950,  // Fountainhead Palace
                        2501951,  // Vermilion Bridge
                        2501956,  // Mibu Manor
                        2501952,  // Flower Viewing Stage
                        2501958,  // Near Pot Noble
                        2501953,  // Great Sakura
                        2501954,  // Palace Grounds
                        2501955,  // Sanctuary
                    }
                },
                // Hirata Revisited
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "hirata" },
                    Conds = new List<int> { 1000353, 0, 1005601, 0, 1000301, 0, 1000900, 0 },
                    Bonfires = new List<int>
                    {
                        1001952,  // Bamboo Thicket Slope
                        1001953,  // Hirata Estate - Main Hall
                        1001955,  // Hirata Audience Chamber
                        1001954,  // Hirata Estate - Hidden Temple
                    }
                },
                // Ashina Castle End to Outskirts
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "ashinacastle", "ashinaoutskirts" },
                    Conds = new List<int> { 8302, 0 },
                    Bonfires = new List<int>
                    {
                        1111953,  // Upper Tower - Kuro's Room
                        1111956,  // Old Grave
                        1101952,  // Outskirts Wall - Stairway
                        1101951,  // Outskirts Wall - Gate Path
                    }
                },
                // Ashina Castle End to Reservoir
                new Path
                {
                    Section = 5,
                    Maps = new List<string> { "ashinacastle", "ashinareservoir" },
                    Conds = new List<int> { 8302, 0 },
                    Bonfires = new List<int>
                    {
                        1111953,  // Upper Tower - Kuro's Room
                        1111957,  // Upper Tower - Ashina Dojo
                        1111951,  // Upper Tower - Antechamber
                        1111950,  // Ashina Castle
                        1121951,  // Ashina Reservoir
                        1121950,  // Near Secret Passage
                    }
                },
            };
            FMG bonfires = new GameEditor(GameSpec.FromGame.SDT).LoadBnd(@"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\msg\engus\menu.msgbnd.dcx", (p, n) => FMG.Read(p))["NTC_\u30e1\u30cb\u30e5\u30fc\u30c6\u30ad\u30b9\u30c8"];
            Dictionary<int, string> names = new Dictionary<int, string>();
            foreach (PARAM.Row r in game.Params["BonfireWarpParam"].Rows)
            {
                // break;
                int entity = (int)r["WarpEventId"].Value;
                string bonfire = bonfires[(int)r["BonfireNameId"].Value];
                if (bonfire != null && entity > 0)
                {
                    names[entity] = bonfire;
                    // Console.WriteLine($"{entity},  // {bonfire}");
                }
            }
            Dictionary<int, Vector3> points = new Dictionary<int, Vector3>();

            // Find location of all bonfires
            foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSBS msb = entry.Value;
                foreach (MSBS.Part.Object e in msb.Parts.Objects)
                {
                    if (allBonfires.Contains(e.EntityID))
                    {
                        points[e.EntityID] = e.Position;
                    }
                }
            }
            string pathText(int p)
            {
                int first = paths[p].Bonfires.First();
                int last = paths[p].Bonfires.Last();
                return $"#{paths[p].Section} {names[first]}->{names[last]}";
            }

            bool investigateScaling = false;
            List<List<EnemyClass>> typeGroups = new List<List<EnemyClass>>
            {
                new List<EnemyClass> { EnemyClass.Boss, EnemyClass.TutorialBoss },
                new List<EnemyClass> { EnemyClass.Miniboss },
                new List<EnemyClass> { EnemyClass.Basic, EnemyClass.FoldingMonkey, EnemyClass.OldDragon }
            };
            List<EnemyClass> types = typeGroups.SelectMany(c => c).ToList();
            Dictionary<int, EnemyInfo> infos = fullInfos.Values.Where(e => types.Contains(e.Class)).ToDictionary(e => e.ID, e => e);
            if (!investigateScaling)
            {
                infos.Remove(1110920);
                infos.Remove(1110900);
                infos.Remove(1120800);
            }
            Dictionary<int, List<int>> possiblePaths = new Dictionary<int, List<int>>();
            bool explainCat = false;
            for (int i = 0; i < paths.Count; i++)
            {
                if (explainCat) Console.WriteLine($"--- Processing {pathText(i)}");
                Path p = paths[i];
                List<string> maps = p.Maps;
                List<int> cond = p.Conds;
                List<int> order = p.Bonfires;
                Dictionary<int, List<int>> eventFlags = new Dictionary<int, List<int>>();
                HashSet<int> excludeEntity = new HashSet<int>();
                HashSet<int> expectEntity = new HashSet<int>();
                HashSet<int> getEntity = new HashSet<int>();
                for (int j = 0; j < cond.Count; j += 2)
                {
                    int check = cond[j];
                    int val = cond[j + 1];
                    if (check >= 1000000)
                    {
                        if (val == 0) expectEntity.Add(check);
                        else excludeEntity.Add(check);
                    }
                    else
                    {
                        AddMulti(eventFlags, check, val);
                    }
                }
                foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
                {
                    if (!game.Locations.ContainsKey(entry.Key)) continue;
                    string map = game.Locations[entry.Key];
                    MSBS msb = entry.Value;

                    if (!maps.Contains(map)) continue;
                    foreach (MSBS.Part.Enemy e in msb.Parts.Enemies)
                    {
                        if (!infos.ContainsKey(e.EntityID)) continue;
                        points[e.EntityID] = e.Position;
                        names[e.EntityID] = game.ModelName(e.ModelName);
                        List<int> ids = new List<int> { e.EntityID };
                        ids.AddRange(e.EntityGroupIDs.Where(id => id > 0));
                        if (excludeEntity.Overlaps(ids))
                        {
                            if (explainCat) Console.WriteLine($"excluded: {string.Join(",", ids)} from {string.Join(",", excludeEntity)}");
                            continue;
                        }
                        else if (expectEntity.Overlaps(ids))
                        {
                            getEntity.UnionWith(ids);
                        }
                        else if (eventFlags.Count > 0)
                        {
                            // If not explicitly expected, do a check for game progression
                            Dictionary<int, int> flags = new Dictionary<int, int>();
                            if (e.EventFlagID != -1) flags[e.EventFlagID] = e.EventFlagCompareState;
                            if (e.UnkT48 != -1) flags[e.UnkT48] = e.UnkT4C;
                            if (e.UnkT50 != -1) flags[e.UnkT50] = 1;
                            bool mismatch = false;
                            foreach (KeyValuePair<int, List<int>> flagPair in eventFlags)
                            {
                                int flag = flagPair.Key;
                                List<int> cmps = flagPair.Value;
                                int cmp = flags.TryGetValue(flag, out int tmp) ? tmp : -1;
                                if (explainCat && e.EntityID == 9999999) Console.WriteLine($"for {e.EntityID} expected {flag} = {string.Join(",", cmps)}, found result {cmp}");
                                if (!cmps.Contains(cmp))
                                {
                                    if (explainCat) Console.WriteLine($"excluded: {string.Join(",", ids)} with {flag} = {cmp} (not {string.Join(",", cmps)})");
                                    mismatch = true;
                                }
                            }
                            if (mismatch) continue;
                        }
                        if (explainCat) Console.WriteLine($"added: {string.Join(",", ids)}");
                        AddMulti(possiblePaths, e.EntityID, i);
                    }
                }
                List<int> missing = expectEntity.Except(getEntity).ToList();
                if (missing.Count > 0) throw new Exception($"Missing {string.Join(",", missing)} in {string.Join(",", maps)}");
            }
            // Hardcode headless into Senpou, because it is out of the way and sort of a singleton
            possiblePaths[1100330] = new List<int> { 7 };

            Console.WriteLine("Categories");
            // Mapping from entity id to (chosen path, progress along path)
            Dictionary<int, (int, float)> chosenPath = new Dictionary<int, (int, float)>();
            foreach (EnemyInfo info in infos.Values)
            {
                if (!possiblePaths.TryGetValue(info.ID, out List<int> pathList)) throw new Exception($"{info.ID} has no categorization: {info.DebugText}");
                if (paths[pathList[0]].Maps.Contains("hirata"))
                {
                    // If Hirata, greedily choose pre-revisited Hirata
                    pathList = new List<int> { pathList[0] };
                }
                float score = float.PositiveInfinity;
                Vector3 pos = points[info.ID];
                foreach (int path in pathList)
                {
                    Path p = paths[path];
                    int section = p.Section;
                    List<string> maps = p.Maps;
                    List<int> cond = p.Conds;
                    List<int> order = p.Bonfires;

                    for (int i = 0; i < order.Count - 1; i++)
                    {
                        Vector3 p1 = points[order[i]];
                        Vector3 p2 = points[order[i + 1]];
                        float dist1 = Vector3.Distance(p1, pos);
                        float dist2 = Vector3.Distance(p2, pos);
                        float dist = dist1 + dist2;
                        if (info.ID == 9999999) Console.WriteLine($"Found dist {dist1} to {names[order[i]]}, and {dist2} to {names[order[i + 1]]}. TOTAL {dist}");
                        if (dist < score)
                        {
                            score = dist;
                            chosenPath[info.ID] = (path, i + Vector3.Distance(p1, pos) / dist);
                        }
                    }
                }
                if (float.IsInfinity(score)) throw new Exception($"{info.ID} with paths {string.Join(",", pathList.Select(pathText))} had nothing checked for it");
            }

            // Put bosses in phase order
            foreach (int id in chosenPath.Keys.ToList())
            {
                EnemyInfo info = infos[id];
                if (info.Class == EnemyClass.Boss && info.OwnedBy != 0)
                {
                    chosenPath[id] = chosenPath[info.OwnedBy];
                }
            }

            if (investigateScaling)
            {
                Dictionary<int, int> allSections = InvestigateSekiroScaling(game, typeGroups, infos, paths, chosenPath);
                foreach (EnemyInfo info in fullInfos.Values)
                {
                    if (!allSections.ContainsKey(info.ID) && info.Class == EnemyClass.Helper && allSections.TryGetValue(info.OwnedBy, out int section))
                    {
                        allSections[info.ID] = section;
                    }
                }
                foreach (KeyValuePair<int, int> entry in allSections.OrderBy(e => (e.Value, e.Key)))
                {
                    Console.WriteLine($"  {entry.Key}: {entry.Value}");
                }
            }

            bool debugOutput = false;
            foreach (List<EnemyClass> typeGroup in typeGroups)
            {
                List<string> order = new List<string>();
                foreach (KeyValuePair<int, (int, float)> entry in chosenPath.OrderBy(e => (e.Value, e.Key)))
                {
                    int id = entry.Key;
                    EnemyInfo info = infos[id];
                    if (!typeGroup.Contains(info.Class)) continue;
                    if (debugOutput) Console.WriteLine($"{info.DebugText}\n- {pathText(entry.Value.Item1)}, progress {entry.Value.Item2}\n");
                    order.Add($"{info.ExtraName ?? names[id]} {id}");
                }
                for (int i = 0; i < order.Count; i++)
                {
                    if (!debugOutput) Console.WriteLine($"  {order[i]}: {order[order.Count - 1 - i]}");
                }
            }
        }

        Dictionary<int, int> InvestigateSekiroScaling(
            GameData game, List<List<EnemyClass>> typeGroups, Dictionary<int, EnemyInfo> infos, List<Path> paths, Dictionary<int, (int, float)> chosenPath)
        {
            Dictionary<int, MSBS.Part.Enemy> enemies = new Dictionary<int, MSBS.Part.Enemy>();
            foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSBS msb = entry.Value;
                foreach (MSBS.Part.Enemy e in msb.Parts.Enemies)
                {
                    enemies[e.EntityID] = e;
                }
            }

            // Exclude these from scaling considerations, since they are not really part of the area (meant for when visiting later)
            HashSet<int> phantomGroups = new HashSet<int>
                {
                    // Ashina phantoms
                    1505201, 1505211, 1705200, 1705201, 2005200, 2005201,
                    // Sunken Valley phantoms
                    1505202, 1505212, 2005210, 2005211,
                    // Mibu Village phantoms
                    1705220, 1705221, 2005220, 2005221,
                };

            // haveSoulRate Unk85: NG+ only
            // EventFlagId: used for scaling speffect
            // There are these overall groups: vitality, damage, experience, cash. (is there haveSoulRate for cash/xp? maybe it's Unk85)
            List<string> scaleSp = "maxHpRate maxStaminaCutRate physAtkPowerRate magicAtkPowerRate fireAtkPowerRate thunderAtkPowerRate staminaAttackRate darkAttackPowerRate NewGameBonusUnk".Split(' ').ToList();
            List<string> scaleNpc = "Hp getSoul stamina staminaRecoverBaseVal Experience".Split(' ').ToList();
            List<string> allFields = scaleSp.Concat(scaleNpc).ToList();
            // Disp: ModelDispMask0 -> ModelDispMask31
            // Npc param has GameClearSpEffectID
            Dictionary<(string, int, int), List<float>> allScales = new Dictionary<(string, int, int), List<float>>();
            Dictionary<int, int> allSections = new Dictionary<int, int>();
            foreach (List<EnemyClass> typeGroup in typeGroups)
            {
                // Consider two enemies the same if they have the same think id, or same disp mask
                // Or for minibosses, if they are just the same model, that's probably fine
                Dictionary<string, List<int>> thinks = new Dictionary<string, List<int>>();
                Dictionary<string, List<int>> masks = new Dictionary<string, List<int>>();
                Dictionary<string, List<int>> bosses = new Dictionary<string, List<int>>();
                List<string> order = new List<string>();
                Dictionary<int, int> sections = new Dictionary<int, int>();
                foreach (KeyValuePair<int, (int, float)> entry in chosenPath.OrderBy(e => (e.Value, e.Key)))
                {
                    int id = entry.Key;
                    EnemyInfo info = infos[id];
                    if (!typeGroup.Contains(info.Class)) continue;
                    MSBS.Part.Enemy e = enemies[id];
                    int path = entry.Value.Item1;
                    int section = paths[path].Section;
                    sections[id] = section;
                    allSections[id] = section;
                    if (e.EntityGroupIDs.Any(g => phantomGroups.Contains(g))) continue;
                    string model = game.ModelName(e.ModelName);
                    if (typeGroup.Contains(EnemyClass.Miniboss) || typeGroup.Contains(EnemyClass.Boss))
                    {
                        AddMulti(bosses, model, id);
                        continue;
                    }
                    string think = $"{model} {e.ThinkParamID}";
                    AddMulti(thinks, think, id);
                    PARAM.Row npc = game.Params["NpcParam"][e.NPCParamID];
                    if (e.NPCParamID > 0 && npc != null)
                    {
                        uint mask = 0;
                        for (int i = 0; i < 32; i++)
                        {
                            if ((byte)npc[$"ModelDispMask{i}"].Value == 1)
                            {
                                mask |= ((uint)1 << i);
                            }
                        }
                        string dispMask = $"{model} 0x{mask:X8}";
                        AddMulti(masks, dispMask, id);
                    }
                }
                foreach (KeyValuePair<string, List<int>> entry in thinks.Concat(masks.Concat(bosses)))
                {
                    if (entry.Value.Count == 1) continue;
                    List<int> secs = entry.Value.Select(i => sections[i]).Distinct().ToList();
                    if (secs.Count == 1) continue;

                    Console.WriteLine($"{entry.Key}: {string.Join(",", entry.Value.Select(i => $"{i}[{sections[i]}]"))}");
                    SortedDictionary<string, List<(int, float)>> fieldValues = new SortedDictionary<string, List<(int, float)>>();
                    foreach (int id in entry.Value)
                    {
                        MSBS.Part.Enemy e = enemies[id];
                        PARAM.Row npc = game.Params["NpcParam"][e.NPCParamID];
                        if (e.NPCParamID == 0 || npc == null) continue;
                        Dictionary<string, float> values = new Dictionary<string, float>();
                        foreach (string f in scaleNpc)
                        {
                            values[f] = float.Parse(npc[f].Value.ToString());
                        }
                        int spVal = (int)npc["EventFlagId"].Value;  // GameClearSpEffectID is for NG+ only, or time-of-day only, or something like that
                        PARAM.Row sp = game.Params["SpEffectParam"][spVal];
                        if (spVal > 0 && sp != null)
                        {
                            foreach (string f in scaleSp)
                            {
                                values[f] = float.Parse(sp[f].Value.ToString());
                            }
                        }
                        foreach (KeyValuePair<string, float> val in values)
                        {
                            AddMulti(fieldValues, val.Key, (sections[id], val.Value));
                        }
                    }
                    foreach (KeyValuePair<string, List<(int, float)>> val in fieldValues)
                    {
                        // Console.WriteLine($"  {val.Key}: {string.Join(", ", val.Value.OrderBy(v => v).Select(v => $"[{v.Item1}]{v.Item2}"))}");
                        Dictionary<int, float> bySection = val.Value.GroupBy(v => v.Item1).ToDictionary(g => g.Key, g => g.Select(v => v.Item2).Average());
                        List<string> sorts = new List<string>();
                        foreach (int i in bySection.Keys)
                        {
                            foreach (int j in bySection.Keys)
                            {
                                if (i >= j) continue;
                                float ratio = bySection[j] / bySection[i];
                                if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio == 1 || ratio == 0) continue;
                                sorts.Add($"{i}{j}: {ratio:f3}x");
                                AddMulti(allScales, (val.Key, i, j), ratio);
                                // Can be used for complete table, but easier to leave out for lower diagonal
                                // AddMulti(allScales, (val.Key, j, i), 1 / ratio);
                            }
                        }
                        if (sorts.Count > 0) Console.WriteLine($"  {val.Key}: {string.Join(", ", sorts)}");
                    }
                }
            }
            foreach (string field in allFields)
            {
                Console.WriteLine($"-- {field} ({allScales.Where(k => k.Key.Item1 == field).Sum(e => e.Value.Count)})");
                for (int i = 1; i <= 5; i++)
                {
                    // row: the target class. column: the source class. value: how much to multiply to place the source in the target.
                    Console.WriteLine("  " + string.Join(" ", Enumerable.Range(1, 5).Select(j => allScales.TryGetValue((field, j, i), out List<float> floats) ? $"{floats.Average():f5}," : "        ")));
                }
            }
            return allSections;
        }

        public class EldenAreaClassifier
        {
            private GameData game;
            private EldenCoordinator coord;
            // In m12_01
            private List<Arena> AinselStart { get; set; }
            // In m12_01
            private List<Arena> AinselMain { get; set; }
            // In m60_08_10_02
            private List<Arena> Moonlight { get; set; }
            // In m12_02
            private List<Arena> SacredGround { get; set; }

            private static readonly HashSet<string> moonlight = new HashSet<string> { "moonlight", "liurnia" };
            private static readonly HashSet<string> ainsel = new HashSet<string> { "ainsel", "ainsel_start", "lakeofrot" };
            private static readonly HashSet<string> siofra = new HashSet<string> { "siofra_nokron", "siofra", "siofrabank_nokron", "siofra_limited" };
            private static readonly HashSet<string> siofraStart = new HashSet<string> { "siofrabank_nokron", "siofra" };
            private static readonly HashSet<string> haligtree = new HashSet<string> { "haligtree", "haligtree_elphael" };

            public EldenAreaClassifier(GameData game, EldenCoordinator coord)
            {
                this.game = game;
                this.coord = coord;
                Dictionary<string, MSBE> maps = game.EldenMaps;
                List<Arena> GetArenas(string map, Func<MSBE.Region, bool> pred)
                {
                    return maps[map].Regions.GetEntries().Where(pred).Select(Arena.FromRegion).ToList();
                }
                AinselStart = GetArenas("m12_01_00_00", r => r.Name == "Env_Box204" || r.Name == "Env_Box501");
                AinselMain = GetArenas("m12_01_00_00", r => r.Name == "Env_Box052" || r.Name == "Env_Box053" || r.Name == "Env_Box054");
                Moonlight = GetArenas("m60_08_10_02", r => r is MSBE.Region.PlayArea p && p.UnkT00 == 6202000);
                SacredGround = GetArenas("m12_02_00_00", r => r.Name.StartsWith("サウンド領域_BGM_マリカ遺跡"));
            }

            public string Classify(List<string> areas, List<EntityId> entity)
            {
                if (entity.Count == 1)
                {
                    return Classify(areas, entity[0]);
                }
                else if (entity.Count == 0)
                {
                    return null;
                }
                List<string> results = entity.Select(e => Classify(areas, e)).Distinct().ToList();
                return results.Count == 1 ? results[0] : null;
            }

            public string Classify(List<string> areas, EntityId entity)
            {
                string map = entity.MapName;
                if (entity.Position is not Vector3 pos) return null;
                // Console.WriteLine($"Examining {string.Join(",", areas)} for {entity}");
                // Heuristics for cases with very different area scaling
                // moonlight liurnia: regions with playarea 6202000
                if (moonlight.SetEquals(areas))
                {
                    Vector3 bigTile = pos + coord.RelocationOffset(map, "m60_08_10_02");
                    if (false && entity.EntityName == "AEG099_610_9000" && map.Contains("35_42"))
                    {
                        Arena a = Moonlight[0];
                        // Console.WriteLine($"Found {map} {pos} -> {bigTile} for {entity}");
                        // Console.WriteLine($"  {Vector3.Subtract(bigTile, a.Pos)} -> {a.InverseTransform(bigTile)} in {a} -> {a.Contains(bigTile)}");
                    }
                    return Moonlight.Any(a => a.Contains(bigTile)) ? "moonlight" : "liurnia";
                }

                // m12_01 ainsel ainsel_start lakeofrot
                // ainsel_start is > -260 and < -165, and also Env_Box204 and Env_Box501 (start)
                // ainsel is Env_Box052 Env_Box053 Env_Box054 (nokstella) when > -215 and < -165
                // lakeofrot is < -285 ish
                if (ainsel.SetEquals(areas) && map == "m12_01_00_00")
                {
                    if (pos.Y < -285) return "lakeofrot";
                    if (pos.Y > -260 && pos.Y < -165)
                    {
                        if (pos.Y > -215 && AinselMain.Any(a => a.Contains(pos))) return "ainsel";
                        return "ainsel_start";
                    }
                    if (AinselStart.Any(a => a.Contains(pos))) return "ainsel_start";
                    return "ainsel";
                }

                // m12_02 siofra_nokron siofra siofrabank_nokron siofra_limited (also aqueducts, sacred ground, and dragonkin)
                // < -700 siofra_dragonkin
                // < -765 siofra (AEG099_620_9039 included?)
                // night's ground: "サウンド領域_BGM_マリカ遺跡_01", "サウンド領域_BGM_マリカ遺跡_02", "サウンド領域_BGM_マリカ遺跡_03"
                // aqueducts are nokron, x > 1180, and y < -617
                if (siofra.SetEquals(areas) && map == "m12_02_00_00")
                {
                    return pos.Y < -700 ? "siofra" : "siofra_nokron";
                }

                // m12_07 siofrabank_nokron siofra, < -650 is siofra
                if (siofraStart.SetEquals(areas) && map == "m12_07_00_00")
                {
                    return pos.Y < -650 ? "siofra" : "siofrabank_nokron";
                }

                // m15_00
                if (haligtree.SetEquals(areas) && map == "m15_00_00_00")
                {
                    return pos.Y > 380 ? "haligtree" : "haligtree_elphael";
                }

                return null;
            }

            public List<string> FogClassify(List<string> areas, List<EntityId> entities)
            {
                // Is this really worth it
                if (areas.Count == 1) return areas;
                // This is based on item areas
                if (!areas.Contains("siofra_nokron") && !areas.Contains("siofra") && !areas.Contains("siofrabank_nokron"))
                {
                    return areas;
                }
                // Add this here, after area check
                List<EntityId> entity = entities.Where(e => e.Type != null && (e.Type.Contains("asset") || e.Type.Contains("enemy"))).ToList();
                if (entity.Count == 1)
                {
                    return FogClassify(areas, entity[0]);
                }
                else if (entity.Count == 0)
                {
                    return areas;
                }
                List<List<string>> results = entity.Select(e => FogClassify(areas, e)).DistinctBy(r => string.Join(",", r)).ToList();
                return results.Count == 1 ? results[0] : areas;
            }

            public List<string> FogClassify(List<string> areas, EntityId entity)
            {
                // Prerequisite: entity is in siofra_nokron area
                if (entity.Position is not Vector3 pos) return areas;
                string map = entity.MapName;
                string select = null;
                if (map == "m12_02_00_00")
                {
                    if (pos.Y < -700)
                    {
                        if (pos.Y < -765) select = "siofra";
                        else select = "siofra_dragonkin";
                    }
                    else
                    {
                        if (pos.Z > 1720 && pos.Y < -617 && pos.Y > -635) select = "siofra_nokron_aqueduct";
                        else if (SacredGround.Any(a => a.Contains(pos))) select = "siofra_nokron_grounds";
                        else select = "siofra_nokron";
                    }
                }
                if (map == "m12_07_00_00" && areas.Contains("siofra"))
                {
                    select = "siofra";
                }
                if (map == "m12_07_00_00" && areas.Contains("siofrabank_nokron"))
                {
                    if (pos.Y > -567) select = "siofrabank_prenokron";
                    else select = "siofrabank_nokron";
                }
                if (map == "m34_11_00_00")
                {
                    // Study Hall: enemies in 34115150, assets in 34116150
                    // Inverted: enemies in 34115160, assets in 34116160
                    // However, treasure assets don't have them
                    if (entity.GroupIds.Contains(34115150)) select = "liurnia_studyhall";
                    else if (entity.GroupIds.Contains(34115160)) select = "liurnia_tower";
                }
                // Some redundancy with non-fog calc
                if (moonlight.SetEquals(areas))
                {
                    Vector3 bigTile = pos + coord.RelocationOffset(map, "m60_08_10_02");
                    select = Moonlight.Any(a => a.Contains(bigTile)) ? "moonlight" : "liurnia";
                    // if (map == "m60_33_42_00" && entity.EntityName == "c3350_9000") Console.WriteLine($"Identified with {string.Join(",", areas)}, {moonlight.SetEquals(areas)} -> {select}");
                }
                if (select != null && areas.Contains(select)) return new List<string> { select };
                return areas;
            }
        }

        public void InvestigateSpEffects(GameData game)
        {
            ParamDictionary Params = game.Params;

            Dictionary<string, List<string>> getFieldValues(string p)
            {
                Dictionary<string, List<string>> ret = new Dictionary<string, List<string>>();
                foreach (PARAM.Row row in Params[p].Rows)
                {
                    foreach (PARAM.Cell cell in row.Cells)
                    {
                        string name = cell.Def.InternalName;
                        AddMulti(ret, name, cell.Value.ToString());
                    }
                }
                return ret;
            }
            string histogram(List<string> list)
            {
                return string.Join(", ", list.GroupBy(i => i).Select(g => g.Key + (g.Count() > 1 ? $" ({g.Count()})" : "")));
            }
            HashSet<int> spEffects = new HashSet<int>(Params["SpEffectParam"].Rows.Select(r => (int)r.ID));
            spEffects.Add(-1);
            HashSet<string> spStrs = new HashSet<string>(spEffects.Select(s => s.ToString()));
            Dictionary<int, HashSet<string>> spEffectUsers = new Dictionary<int, HashSet<string>>();
            HashSet<string> scalings = new HashSet<string>(new[] { 7514, 7524, 7534, 7544, 7554, 7564, 7574, 7584, 7594, 7604 }.Select(t => t.ToString()));
            foreach (string p in Params.Keys)
            {
                Dictionary<string, List<string>> fields = getFieldValues(p);
                foreach (KeyValuePair<string, List<string>> fe in fields)
                {
                    HashSet<string> vals = new HashSet<string>(fe.Value);
                    string name = fe.Key;
                    if (name.ToLowerInvariant().Contains("speffectid") || (p == "NpcParam" && (name == "Unk156" || name == "EventFlagId")) || (p == "SkillParam" && (name == "Unk2" || name == "Unk3")))
                    {
                        foreach (string val in vals)
                        {
                            if (int.TryParse(val, out int sp) && sp > 0)
                            {
                                AddMulti(spEffectUsers, sp, $"{p}.{name} ({fe.Value.Count(v => v == val)})");
                            }
                        }
                    }
                    if (false && vals.All(v => spStrs.Contains(v)) && vals.Any(v => int.Parse(v) >= 1000))
                    {
                        Console.WriteLine($"Possible {p}.{fe.Key}: {histogram(fe.Value)}");
                    }
                    if (vals.Any(v => scalings.Contains(v)))
                    {
                        Console.WriteLine($"Possible {p}.{fe.Key}: {histogram(fe.Value)}");
                    }
                }
            }

            List<int> special = new List<int> { 3102050, 300600 }; // sp24 the first
                                                                   // Reference site of speffect, and value of field
            Dictionary<string, HashSet<(string, string)>> fieldValues = new Dictionary<string, HashSet<(string, string)>>();
            for (int i = 0; i <= 1; i++)
            {
                foreach (PARAM.Row row in Params["SpEffectParam"].Rows)
                {
                    int sp = (int)row.ID;
                    if (!spEffectUsers.TryGetValue(sp, out HashSet<string> users))
                    {
                        users = new HashSet<string> { "other" + sp };
                    }
                    if (sp == 7040) users.Add("bonk" + sp);
                    // if (special.Any(s => Math.Abs(s - sp) <= 10)) users.Add("bonk" + sp);
                    foreach (PARAM.Cell cell in row.Cells)
                    {
                        string name = cell.Def.InternalName;
                        foreach (string user in users)
                        {
                            AddMulti(fieldValues, name, (user, cell.Value.ToString()));
                        }
                    }
                }
            }
            foreach (PARAM.Cell cell in Params["SpEffectParam"].Rows[0].Cells)
            {
                string name = cell.Def.InternalName;
                if (fieldValues[name].Select(t => t.Item2).Distinct().Count() <= 1) continue;
                Console.WriteLine($"\n\n{name}:");
                foreach (IGrouping<string, string> e in fieldValues[name].GroupBy(e => e.Item2, e => e.Item1).OrderBy(e => double.Parse(e.Key)))
                {
                    if (e.Count() > 50)
                    {
                        Console.WriteLine($"    {e.Key}: *********************************************");
                    }
                    else
                    {
                        Console.WriteLine($"    {e.Key}: {string.Join(", ", e)}");
                    }
                }
            }
        }

        public void InvestigateEldenCols(MSBE mb, Dictionary<int, EnemyInfo> infos)
        {
            /*
[1 Fountain stairs, 2 Fountain balcony] =
    h005000 (Post-Wolf pots) h005100 (Fountain 1) h005200 (Fountain 2)
    h005400 (1 Fountain stairs) h008400 (Above ball trap) h008500 (Ball trap)
[1 Fountain stairs, 2 Fountain balcony, 3 Rooftop] = h002500 (Graveyard bridge) h008700 (Before elevator)
[2 Fountain balcony, 3 Rooftop] = h002600 (Carian Knight set) h007400 (Cuckoo hallway to sphere)
[2 Fountain balcony, 3 Rooftop, 4 Rooftop bridge] = h002000 (Graveyard start) h003000 (Elevator second level)
    h006000 (3 Rooftop) h006100 (4 Rooftop bridge) h006200 (Rooftop ladder chest 3)
    h006800 (Rooftop floor boards avionette) h007000 (Rooftop outside Cuckoo) h007200 (Rooftop upper Cuckoo)
[3 Rooftop] = h001600 (Cuckoo Church grace)
[3 Rooftop, 4 Rooftop bridge] =
    h004300 (Pre-Wolf mid level) h004500 (Pre-Wolf balcony) h006700 (Rooftop post-bridge) h006900 (Rooftop mid floor boards)
[4 Rooftop bridge] =
    h004400 (Pre-Wolf upper level) h006300 (Rooftop ladder chest 2) h006400 (Rooftop ladder chest 1) h006600 (Rooftop steeple)

[1 Fountain stairs, 2 Fountain balcony, 3 Rooftop, 4 Rooftop bridge] = h005500 (2 Fountain balcony) h005600 (Fountain back left)

Reduce for 2: h002000, h002500, h003000
Reduce for 3: h002000, h002500, h003000
            */
            Dictionary<string, string> cols = new Dictionary<string, string>
            {
                ["h005400"] = "1 Fountain stairs",
                ["h005500"] = "2 Fountain balcony",
                ["h006000"] = "3 Rooftop",
                ["h006100"] = "4 Rooftop bridge",
            };
            Dictionary<string, string> extraCols = new Dictionary<string, string>
            {
                ["h001600"] = "Cuckoo Church grace",  // c1000_9002 14000952
                ["h002000"] = "Graveyard start",  // c3661_9000 14000300
                ["h002500"] = "Graveyard bridge",  // c3661_9027 14000327
                ["h002600"] = "Carian Knight set",  // c3661_9060 14000360
                ["h003000"] = "Elevator second level",  // c3661_9065 14000365
                ["h004300"] = "Pre-Wolf mid level",  // c3702_9032 14000232
                ["h004400"] = "Pre-Wolf upper level",  // c3702_9033 14000233
                ["h004500"] = "Pre-Wolf balcony",  // c3702_9021 14000221
                ["h005000"] = "Post-Wolf pots",  // c0100_9002 14000690
                ["h005100"] = "Fountain 1",  // c2275_9005 2889265
                ["h005200"] = "Fountain 2",  // c0100_9003 2889254
                ["h005600"] = "Fountain back left",  // c2274_9001 14000636
                ["h006200"] = "Rooftop ladder chest 3",  // c3850_9022 14000472
                ["h006300"] = "Rooftop ladder chest 2",  // c3850_9024 14000474
                ["h006400"] = "Rooftop ladder chest 1",  // c3850_9020 14000470
                ["h006600"] = "Rooftop steeple",  // c3860_9000 14000490
                ["h006700"] = "Rooftop post-bridge",  // c3850_9016 2889295
                ["h006800"] = "Rooftop floor boards avionette",  // c3860_9001 14000491
                ["h006900"] = "Rooftop mid floor boards",  // c2275_9031 14000637
                ["h007000"] = "Rooftop outside Cuckoo",  // c3850_9035 2889297
                ["h007200"] = "Rooftop upper Cuckoo",  // c2275_9026 14000633
                ["h007400"] = "Cuckoo hallway to sphere",  // c3703_9000 14000675
                ["h008400"] = "Above ball trap",  // c3702_9075 2889292
                ["h008500"] = "Ball trap",  // c3702_9076 14000276
                ["h008700"] = "Before elevator",  // c0000_9014 14000499
            };
            Dictionary<string, MSBE.Part.Collision> colParts = mb.Parts.Collisions
                .Where(c => cols.ContainsKey(c.Name))
                .ToDictionary(c => c.Name, c => c);
            Dictionary<string, List<string>> visibleCols = new Dictionary<string, List<string>>();
            HashSet<string> enemyCols = new HashSet<string>(mb.Parts.Enemies.Select(c => c.CollisionPartName));
            // Disp groups are a collision's own bits. Draw groups are references to other collisions.
            // In DS3, it is DrawGroups, DispGroups, BackreadGroups, uint[8] default -1
            // TODO: CollisionMask no longer covers these bits, and the order is swapped.
            List<uint> dispGroups(MSBE.Part.Collision c) => c.Unk1.CollisionMask.ToList().GetRange(0, 8);
            List<uint> drawGroups(MSBE.Part.Collision c) => c.Unk1.CollisionMask.ToList().GetRange(8, 8);
            foreach (MSBE.Part.Collision c in mb.Parts.Collisions)
            {
                if (!enemyCols.Contains(c.Name)) continue;
                SortedSet<string> sees = new SortedSet<string>();
                foreach (KeyValuePair<string, string> checkName in cols)
                {
                    MSBE.Part.Collision check = colParts[checkName.Key];
                    List<uint> disp = dispGroups(check);
                    List<uint> draw = drawGroups(c);
                    if (draw.Where((g, i) => (g & disp[i]) != 0).Any())
                    {
                        sees.Add(checkName.Value);
                    }
                }
                if (sees.Count > 0)
                {
                    AddMulti(visibleCols, string.Join(", ", sees), c.Name);
                }
            }
            SortedSet<string> unnamedCols = new SortedSet<string>();
            string formatCol(string s)
            {
                if (cols.TryGetValue(s, out string t)) return $"{s} ({t})";
                if (extraCols.TryGetValue(s, out t) && !string.IsNullOrWhiteSpace(t)) return $"{s} ({t})";
                unnamedCols.Add(s);
                return s;
            };
            foreach (KeyValuePair<string, List<string>> seen in visibleCols)
            {
                Console.WriteLine($"[{seen.Key}] = {string.Join(" ", seen.Value.Select(formatCol))}");
            }
            Dictionary<string, string> examples = new Dictionary<string, string>();
            foreach (string col in unnamedCols)
            {
                Console.WriteLine($"--- {col}");
                List<MSBE.Part.Enemy> enemies = mb.Parts.Enemies.Where(c => c.CollisionPartName == col).ToList();
                foreach (MSBE.Part.Enemy e in enemies)
                {
                    if (infos.TryGetValue((int)e.EntityID, out EnemyInfo info))
                    {
                        Console.WriteLine(info.DebugText);
                        examples[col] = $"{e.Name} {e.EntityID}";
                        break;
                    }
                }
            }
            foreach (string col in unnamedCols)
            {
                examples.TryGetValue(col, out string example);
                example = example == null ? "" : $"  // {example}";
                Console.WriteLine($"[\"{col}\"] = \"\",{example}");
            }
        }

        public void InvestigateDS3Cols(MSB3 mb)
        {
            // DrawGroups just too expansive, crossing from big stairs down into the abyss swamp, or vice versa
            Dictionary<string, string> cols = new Dictionary<string, string>
            {
                ["h004800"] = "Upper stairs",
                ["h005000"] = "Lower stairs",
                // DispGroups same as h006000 coming from lower stairs
                ["h006001"] = "Abyss swamp",
                ["h007000"] = "Outside bonfire",
                ["h008000"] = "Up the swamp ladder",
                ["h508200"] = "Ringed Knight upper level",
                ["h005400"] = "Ringed Knight lower level",
                ["h006104"] = "Further swamp",
                ["h006300"] = "Ledo area",
                // For humanity crash
                // DispGroups same as h007501 going towards monument area
                ["h007500"] = "*Up the humanity ladder",
                ["h007600"] = "+Upper Purging monument",
                ["h007900"] = "Lower Purging monument",
            };
            Dictionary<string, MSB3.Part.Collision> colParts = mb.Parts.Collisions
                .Where(c => cols.ContainsKey(c.Name))
                .ToDictionary(c => c.Name, c => c);
            Dictionary<string, List<string>> visibleCols = new Dictionary<string, List<string>>();
            HashSet<string> enemyCols = new HashSet<string>(mb.Parts.Enemies.Select(c => c.CollisionName));
            foreach (MSB3.Part.Collision c in mb.Parts.Collisions)
            {
                if (!enemyCols.Contains(c.Name)) continue;
                SortedSet<string> sees = new SortedSet<string>();
                foreach (KeyValuePair<string, string> checkName in cols)
                {
                    MSB3.Part.Collision check = colParts[checkName.Key];
                    if (c.DrawGroups.Where((g, i) => (g & check.DispGroups[i]) != 0).Any())
                    {
                        sees.Add(checkName.Value);
                    }
                }
                if (sees.Count > 0)
                {
                    AddMulti(visibleCols, string.Join(", ", sees), c.Name);
                }
            }
            foreach (KeyValuePair<string, List<string>> seen in visibleCols)
            {
                Console.WriteLine($"[{seen.Key}] = {string.Join(" ", seen.Value.Select(s => cols.TryGetValue(s, out string t) ? $"{s} ({t})" : s))}");
            }
        }

        public void DumpSpEffects(GameData game)
        {
            Dictionary<string, List<string>> getFieldValues(string p)
            {
                Dictionary<string, List<string>> ret = new Dictionary<string, List<string>>();
                foreach (PARAM.Row row in game.Params[p].Rows)
                {
                    foreach (PARAM.Cell cell in row.Cells)
                    {
                        string name = cell.Def.InternalName;
                        AddMulti(ret, name, cell.Value.ToString());
                    }
                }
                return ret;
            }
            Dictionary<string, List<string>> spFields = getFieldValues("SpEffectParam");
            Dictionary<string, string> ignoreCells = new Dictionary<string, string>();
            foreach (KeyValuePair<string, List<string>> entry in spFields)
            {
                (int valCount, string value) = entry.Value
                    .OrderBy(x => x).GroupBy(x => x).Select(x => (x.Count(), x.Key)).OrderByDescending(x => x).FirstOrDefault();
                // Console.WriteLine($"{entry.Key}: {value} ({valCount})");
                if (entry.Key == "effectEndurance") value = null;
                ignoreCells[entry.Key] = value;
            }
            Dictionary<int, string> descs = new Dictionary<int, string>();
            SortedDictionary<string, List<int>> valueGroups = new SortedDictionary<string, List<int>>();
            string groupBy = null;
            foreach (PARAM.Row row in game.Params["SpEffectParam"].Rows)
            {
                string formatCell(PARAM.Cell cell)
                {
                    string key = cell.Def.InternalName;
                    string value = cell.Value.ToString();
                    if (ignoreCells[key] != null && ignoreCells[key] == value) return null;
                    if (key.StartsWith("pad")) return null;
                    object val = cell.Value;
                    string disp = val.ToString();
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
                Console.WriteLine($"{row.ID}: {descs[row.ID]}");
            }
            foreach (KeyValuePair<string, List<int>> entry in valueGroups)
            {
                Console.WriteLine($"------- {groupBy} = {entry.Key}");
                foreach (int val in entry.Value) Console.WriteLine($"> {val}: {descs[val]}");
            }
        }
    }
}
