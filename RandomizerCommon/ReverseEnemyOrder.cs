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
            public List<EnemyLoc> Enemies = new List<EnemyLoc>();
        }
        public class KeyItemLoc
        {
            public string Key { get; set; }
            public List<string> DebugText { get; set; }
            public string Area { get; set; }
            public string Lots { get; set; }
            public string Shops { get; set; }
        }
        public class EnemyLoc
        {
            public string ID { get; set; }
            public string DebugText { get; set; }
            public string Area { get; set; }
        }

        public class FogCoordinate
        {
            public string Map { get; set; }
            public Vector3 Pos { get; set; }
            public KeyItemLoc Loc { get; set; }
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

        public void InvestigateSpEffects(GameData game)
        {
            GameData.ParamDictionary Params = game.Params;

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
    }
}
