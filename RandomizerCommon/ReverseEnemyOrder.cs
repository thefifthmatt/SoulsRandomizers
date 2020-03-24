using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    // Messy code to generate reverse enemy order run
    // Honestly this is probably more work than just listing every enemy in the game
    public class ReverseEnemyOrder
    {
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
            List<(int, List<string>, List<int>, List<int>)> paths = new List<(int, List<string>, List<int>, List<int>)>
            {
                // Tutorial
                (0, new List<string> { "ashinareservoir", "ashinacastle" },
                new List<int> { 8306, 0 }, new List<int>
                {
                    1121951,  // Ashina Reservoir
                    1121950,  // Near Secret Passage
                }),
                // First stretch of Ashina Outskirts
                (1, new List<string> { "ashinaoutskirts" },
                new List<int> { 8302, 1, 8302, -1, 1100330, 1 }, new List<int>
                {
                    1101956,  // Ashina Outskirts
                    1101951,  // Outskirts Wall - Gate Path
                    1101952,  // Outskirts Wall - Stairway
                }),
                // Ashina Outskirts up to Blazing Bull
                (2, new List<string> { "ashinaoutskirts", "ashinacastle" },
                new List<int> { 8302, 1, 8302, -1, 8301, 1, 8301, -1, 1100330, 1 }, new List<int>
                {
                    1101952,  // Outskirts Wall - Stairway
                    1101953,  // Underbridge Valley
                    1101954,  // Ashina Castle Gate Fortress
                    1101955,  // Ashina Castle Gate
                    // 1111950,  // Ashina Castle
                }),
                // Hirata 1
                (2, new List<string> { "hirata" },
                new List<int> { 1000353, 1, 1005601, 1, 1000301, 1, 1000900, 1 }, new List<int>
                {
                    1001950,  // Dragonspring - Hirata Estate
                    1001951,  // Estate Path
                    1001952,  // Bamboo Thicket Slope
                    1001953,  // Hirata Estate - Main Hall
                    1001955,  // Hirata Audience Chamber
                    1001954,  // Hirata Estate - Hidden Temple
                }),
                // Ashina Castle to Genichiro
                (3, new List<string> { "ashinacastle" },
                new List<int> { 8301, 1, 8302, 1, 8302, -1 }, new List<int>
                {
                    1111950,  // Ashina Castle
                    1111951,  // Upper Tower - Antechamber
                    1111957,  // Upper Tower - Ashina Dojo
                    1111952,  // Castle Tower Lookout
                }),
                // Ashina Castle to Reservoir to Dungeon
                (3, new List<string> { "ashinareservoir" },
                new List<int> { 8302, 1, 1120300, 0 }, new List<int>
                {
                    1111950,  // Ashina Castle
                    1121951,  // Ashina Reservoir
                    1301951,  // Bottomless Hole
                }),
                // Dungeon
                (3, new List<string> { "dungeon" },
                new List<int> { }, new List<int>
                {
                    1111955,  // Abandoned Dungeon Entrance
                    1301950,  // Underground Waterway
                    1301951,  // Bottomless Hole
                }),
                // Senpou temple start
                (4, new List<string> { "senpou" },
                new List<int> { }, new List<int>
                {
                    2001950,  // Senpou Temple,  Mt. Kongo
                    2001951,  // Shugendo
                    2001952,  // Temple Grounds
                    2001953,  // Main Hall
                }),
                // Hidden Forest to Water Mill
                (4, new List<string> { "mibuvillage" },
                new List<int> { 1700850, 1, 1700520, 1 }, new List<int>
                {
                    1501950,  // Hidden Forest
                    1501951,  // Mibu Village
                    1501952,  // Water Mill
                }),
                // End of Ashina Depths
                (5, new List<string> { "mibuvillage" },
                new List<int> { }, new List<int>
                {
                    1501952,  // Water Mill
                    1501953,  // Wedding Cave Door
                }),
                // Most of Sunken Valley
                (4, new List<string> { "ashinacastle", "sunkenvalley" },
                new List<int> { 8301, 1, 8301, -1, 8302, 1, 8302, -1 }, new List<int>
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
                }),
                // Sunken Valley to Poison Pool path
                (5, new List<string> { "sunkenvalley" },
                new List<int> { 1700850, 0, 1700520, 0 }, new List<int>
                {
                    1701958,  // Bodhisattva Valley
                    1701954,  // Poison Pool
                    1701956,  // Guardian Ape's Burrow
                }),
                // Ashina Castle Revisited, also down to Masanaga
                (6, new List<string> { "ashinacastle" },
                new List<int> { 8301, 0, 8302, 1, 8302, -1 }, new List<int>
                {
                    1111955,  // Abandoned Dungeon Entrance
                    1111950,  // Ashina Castle
                    1111951,  // Upper Tower - Antechamber
                    1111957,  // Upper Tower - Ashina Dojo
                    1111952,  // Castle Tower Lookout
                    1111956,  // Old Grave
                    1111954,  // Great Serpent Shrine
                }),
                // Fountainhead
                (7, new List<string> { "fountainhead" },
                new List<int> { }, new List<int>
                {
                    2501950,  // Fountainhead Palace
                    2501951,  // Vermilion Bridge
                    2501956,  // Mibu Manor
                    2501952,  // Flower Viewing Stage
                    2501958,  // Near Pot Noble
                    2501953,  // Great Sakura
                    2501954,  // Palace Grounds
                    2501955,  // Sanctuary
                }),
                // Hirata Revisited
                (8, new List<string> { "hirata" },
                new List<int> { 1000353, 0, 1005601, 0, 1000301, 0, 1000900, 0 }, new List<int>
                {
                    1001952,  // Bamboo Thicket Slope
                    1001953,  // Hirata Estate - Main Hall
                    1001955,  // Hirata Audience Chamber
                    1001954,  // Hirata Estate - Hidden Temple
                }),
                // Ashina Castle End to Outskirts
                (9, new List<string> { "ashinacastle", "ashinaoutskirts" },
                new List<int> { 8302, 0 }, new List<int>
                {
                    1111953,  // Upper Tower - Kuro's Room
                    1111956,  // Old Grave
                    1101952,  // Outskirts Wall - Stairway
                    1101951,  // Outskirts Wall - Gate Path
                }),
                // Ashina Castle End to Reservoir
                (10, new List<string> { "ashinacastle", "ashinareservoir" },
                new List<int> { 8302, 0 }, new List<int>
                {
                    1111953,  // Upper Tower - Kuro's Room
                    1111957,  // Upper Tower - Ashina Dojo
                    1111951,  // Upper Tower - Antechamber
                    1111950,  // Ashina Castle
                    1121951,  // Ashina Reservoir
                    1121950,  // Near Secret Passage
                }),
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
            foreach (KeyValuePair<string, MSBS> entry in game.Smaps)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string map = SekiroLocationDataScraper.locations[entry.Key];
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
                int first = paths[p].Item4.First();
                int last = paths[p].Item4.Last();
                return $"{names[first]}->{names[last]}";
            }

            List<List<EnemyClass>> typeGroups = new List<List<EnemyClass>>
            {
                new List<EnemyClass> { EnemyClass.Boss },
                new List<EnemyClass> { EnemyClass.Miniboss },
                new List<EnemyClass> { EnemyClass.Basic, EnemyClass.FoldingMonkey }
            };
            List<EnemyClass> types = typeGroups.SelectMany(c => c).ToList();
            Dictionary<int, EnemyInfo> infos = fullInfos.Values.Where(e => types.Contains(e.Class)).ToDictionary(e => e.ID, e => e);
            infos.Remove(1110920);
            infos.Remove(1110900);
            Dictionary<int, List<int>> possiblePaths = new Dictionary<int, List<int>>();
            bool explainCat = false;
            for (int i = 0; i < paths.Count; i++)
            {
                if (explainCat) Console.WriteLine($"--- Processing {pathText(i)}");
                (int section, List<string> maps, List<int> cond, List<int> order) = paths[i];
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
                foreach (KeyValuePair<string, MSBS> entry in game.Smaps)
                {
                    if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                    string map = SekiroLocationDataScraper.locations[entry.Key];
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
                                if (!cmps.Contains(cmp))
                                {
                                    if (explainCat) Console.WriteLine($"excluded: {string.Join(",", ids)} with {flag} = {cmp} (not {string.Join(",", cmps)})");
                                    mismatch = true;
                                }
                            }
                            if (mismatch) continue;
                        }
                        if (explainCat)Console.WriteLine($"added: {string.Join(",", ids)}");
                        AddMulti(possiblePaths, e.EntityID, i);
                    }
                }
                List<int> missing = expectEntity.Except(getEntity).ToList();
                if (missing.Count > 0) throw new Exception($"Missing {string.Join(",", missing)} in {string.Join(",", maps)}");
            }

            Console.WriteLine("Categories");
            Dictionary<int, (int, float)> chosenPath = new Dictionary<int, (int, float)>();
            foreach (EnemyInfo info in infos.Values)
            {
                if (!possiblePaths.TryGetValue(info.ID, out List<int> pathList)) throw new Exception($"{info.ID} has no categorization: {info.DebugText}");
                if (paths[pathList[0]].Item2.Contains("hirata"))
                {
                    // If Hirata, greedily choose pre-revisited Hirata
                    pathList = new List<int> { pathList[0] };
                }
                float score = float.PositiveInfinity;
                Vector3 pos = points[info.ID];
                foreach (int path in pathList)
                {
                    (int section, List<string> maps, List<int> cond, List<int> order) = paths[path];
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
                if (float.IsInfinity(score)) throw new Exception($"{info.ID} with paths {string.Join(",", pathList)} had nothing checked for it");
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
                    Console.WriteLine($"  {order[i]}: {order[order.Count - 1 - i]}");
                }
            }
        }
    }
}
