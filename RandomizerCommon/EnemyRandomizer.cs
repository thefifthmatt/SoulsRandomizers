using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static SoulsIds.Events;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.MapEditors;
using static RandomizerCommon.Preset;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class EnemyRandomizer
    {
        private GameData game;
        private Events events;
        private EventConfig eventConfig;

        public EnemyRandomizer(GameData game, Events events, EventConfig eventConfig)
        {
            this.game = game;
            this.events = events;
            this.eventConfig = eventConfig;
        }

        // Take free event flags from Abandoned Dungeon.
        // These are not usable as flags which can be read/written, even the temporary ones, but they are usable for event initialization.
        // Who knows, these might work for DS3 too, but definitely not writable in that case.

        private int tmpBase = 11315000; // 11305750;  // until 6000, then it's not tmp anymore
        private int tmpBaseMax = 11496000;
        private int tmpJump = 11515000;
        private int tmpMax = 11696000; // 11396000; ?
        private int permBase = 11306000;  // until at least 7000
        private int permMax = 11307000;
        private int writeBase = 11305750;
        private int writeMax = 11306000;

        // Copying
        private int NewID(bool writeable = false)
        {
            int newId = writeable ? writeBase++ : tmpBase++;
            // Jump to the next 5xxxx block
            if (!IsTemp(tmpBase) && tmpBase % 10000 == 6000)
            {
                tmpBase -= 1000;
                tmpBase += 10000;
            }
            if (tmpBase >= tmpBaseMax && tmpBase < tmpJump) tmpBase = tmpJump;
            if (tmpBase > tmpMax || permBase > permMax || writeBase > writeMax) throw new Exception($"Event {newId} hit event limit.");
            return newId;
        }

        public EnemyLocations Run(RandomizerOptions opt, Preset preset)
        {
            if (game.Sekiro)
            {
                return RunGame(opt, preset, game.SekiroMaps, new SekiroEnemyEditor());
            }
            else
            {
                return RunGame(opt, preset, game.DS3Maps, new DS3EnemyEditor());
            }
        }

        public EnemyLocations RunGame<TMap, TEnemy, TRegion, TGenerator>(
            RandomizerOptions opt, Preset preset, Dictionary<string, TMap> maps, EnemyEditor<TMap, TEnemy, TRegion, TGenerator> enemyEditor)
            where TMap : IMsb where TEnemy : IMsbPart where TRegion : IMsbRegion where TGenerator : IMsbEvent
        {
            // We may need to add a lot of entities (regions and helpers).
            // These don't need to exist in a valid map range.
            int entityBase = 1400000;
            int entityMax = 1500000;
            int newEntity()
            {
                if (entityBase >= entityMax) throw new Exception("Internal error: Can't create more entities: out of ids");
                return entityBase++;
            }
            // In DS3, used Filianore map
            int entityGroupBase = 5115000;
            int entityGroupSkip = 5115800; // Skip Gael group
            int entityGroupMax = 5116000;
            int newGroupEntity()
            {
                if (entityGroupBase == entityGroupSkip) entityGroupBase++;
                if (entityGroupBase >= entityGroupMax) throw new Exception("Internal error: Can't create more entity groups: out of ids");
                return entityGroupBase++;
            }

            // Start processing config
            IDeserializer deserializer = new DeserializerBuilder().Build();
            EnemyAnnotations ann;
            using (var reader = File.OpenText($"{game.Dir}/Base/enemy.txt"))
            {
                ann = deserializer.Deserialize<EnemyAnnotations>(reader);
            }

            // Enemy multiplier modifies the config itself
            bool dupeEnabled = preset != null && preset.EnemyMultiplier > 1;
            int dupeCount = 0;
            Dictionary<int, List<int>> dupeMap = new Dictionary<int, List<int>>();
            if (dupeEnabled)
            {
                dupeCount = preset.EnemyMultiplier - 1;
                // Can probably use a global one here
                int partId = 2000;
                foreach (EnemyInfo info in ann.Enemies.ToList())
                {
                    // TODO: Some None enemies maybe be okay to copy, just give them no think and update their enable/disable
                    if (info.DupeEvents != null)
                    {
                        info.Class = EnemyClass.DupeOnly;
                    }
                    // Ashina Castle dupes
                    // if (info.ID >= 1110000 && info.ID < 1120000 && info.Class == EnemyClass.Basic) continue;
                    if (info.Class != EnemyClass.Helper && info.Class != EnemyClass.None)
                    {
                        string modelName = info.Name.Split('_')[0];
                        for (int i = 0; i < dupeCount; i++)
                        {
                            EnemyInfo dupeInfo = new EnemyInfo
                            {
                                ID = newEntity(),
                                Map = info.Map,
                                Name = $"{modelName}_{partId++:d4}",
                                Class = info.Class,
                                SplitFrom = info.ID,
                                // Copy some attributes expected of bosses
                                Tags = info.Tags + " dupe",
                                DefeatFlag = info.DefeatFlag,
                                AppearFlag = info.AppearFlag,
                                StartFlag = info.StartFlag,
                                Phases = info.Phases,
                                Arena = info.Arena,
                                OwnedBy = info.OwnedBy,
                                ExtraName = info.ExtraName,
                            };
                            if (info.Class == EnemyClass.DupeOnly)
                            {
                                dupeInfo.Class = EnemyClass.DupeOnly;
                            }
                            ann.Enemies.Add(dupeInfo);
                            AddMulti(dupeMap, info.ID, dupeInfo.ID);
                        }
                    }
                }
            }
            // Other enemy sources: chest->mimic in DS3
            Dictionary<int, ChestInfo> newMimics = new Dictionary<int, ChestInfo>();
            if (game.DS3 && opt["chests"] && ann.Chests != null)
            {
                int partId = 3000;
                foreach (ChestInfo info in ann.Chests)
                {
                    EnemyInfo chestInfo = new EnemyInfo
                    {
                        ID = newEntity(),
                        Map = info.Map,
                        Name = $"c2120_{partId++:d4}",
                        Class = opt["mimics"] ? EnemyClass.Basic : EnemyClass.DupeOnly,
                        SplitFrom = info.ClosestMimic,
                        SetColName = info.ColName,
                        Tags = info.Tags,
                    };
                    ann.Enemies.Add(chestInfo);
                    newMimics[chestInfo.ID] = info;
                }
            }

            Dictionary<int, EnemyInfo> infos = new Dictionary<int, EnemyInfo>();
            // Entities owned by a given entity.
            // This includes boss arena/flag inheritance (TODO maybe that should be changed, adds a bunch of useless checks)
            Dictionary<int, List<int>> owners = new Dictionary<int, List<int>>();
            // Map from (name, part) to entity id, for enemies without entity ids
            Dictionary<(string, string), int> configEntityIds = new Dictionary<(string, string), int>();
            Dictionary<int, List<EnemyInfo>> copyTo = new Dictionary<int, List<EnemyInfo>>();
            foreach (EnemyInfo info in ann.Enemies)
            {
                infos[info.ID] = info;
                configEntityIds[(info.Map, info.Name)] = info.ID;
                if (info.OwnedBy > 0)
                {
                    AddMulti(owners, info.OwnedBy, info.ID);
                }
                if (info.SplitFrom > 0)
                {
                    AddMulti(copyTo, info.SplitFrom, info);
                }
            }
            foreach (KeyValuePair<int, List<EnemyInfo>> entry in copyTo)
            {
                if (!infos.ContainsKey(entry.Key)) throw new Exception($"Internal error: split entity {entry.Key} does not exist");
            }
            Dictionary<int, List<string>> objectOwners = new Dictionary<int, List<string>>();
            foreach (ObjectInfo info in ann.Objects)
            {
                if (info.OwnedBy > 0)
                {
                    AddMulti(objectOwners, info.OwnedBy, info.ID);
                }
            }

            Dictionary<int, EnemyData> defaultData = new Dictionary<int, EnemyData>();
            List<EnemyClass> randomizedTypes = new List<EnemyClass>
            {
                EnemyClass.Basic,
                EnemyClass.Miniboss,
                EnemyClass.Boss,
                // Sekiro
                EnemyClass.FoldingMonkey,
                EnemyClass.TutorialBoss,
                // DS3
                EnemyClass.HostileNPC,
                // Dupe mode
                EnemyClass.DupeOnly,
            };

            // Ignoring DLC is a bit tricky because of how many systems there are.
            // For the most part, try to randomize DLC enemies to themselves, rather than totally ignoring them.
            HashSet<string> ignoredMaps = new HashSet<string>();
            if (game.DS3)
            {
                if (!opt["dlc1"])
                {
                    ignoredMaps.UnionWith(new[] { "m45_00_00_00" });
                }
                if (!opt["dlc2"])
                {
                    ignoredMaps.UnionWith(new[] { "m50_00_00_00", "m51_00_00_00", "m51_01_00_00" });
                }
                foreach (string map in ignoredMaps.ToList())
                {
                    ignoredMaps.Add(game.Locations[map]);
                }
            }

            // Mapping from target entity to map name
            Dictionary<int, string> ownerMap = new Dictionary<int, string>();
            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                foreach (TEnemy e in enemyEditor.GetEnemies(msb).ToList())
                {
                    EnemyData data = enemyEditor.GetEnemyData(e, entry.Key);
                    if (data.ID == -1)
                    {
                        if (configEntityIds.TryGetValue((entry.Key, e.Name), out int fakeId))
                        {
                            enemyEditor.SetEntityID(e, fakeId);
                            data.ID = fakeId;
                        }
                        else
                        {
                            Console.WriteLine($"Unknown enemy {entry.Key} {e.Name} #-1");
                            continue;
                        }
                    }
                    defaultData[data.ID] = data;
                    ownerMap[data.ID] = entry.Key;

                    if (!infos.TryGetValue(data.ID, out EnemyInfo info))
                    {
                        Console.WriteLine($"Unknown enemy {entry.Key} {e.Name} #{data.ID}");
                    }
                    if (copyTo.TryGetValue(data.ID, out List<EnemyInfo> deriveds))
                    {
                        foreach (EnemyInfo derived in deriveds)
                        {
                            int partId = int.Parse(derived.Name.Split('_')[1].TrimStart('0'));
                            // TODO: Make Map always present. It is not present in Sekiro enemy config.
                            string toMap = derived.Map ?? entry.Key;
                            // This does a concurrent modification to the enemy list for same-map splits,
                            // which is accounted for above.
                            // Also, unlike with helper enemies, don't clear groups
                            TEnemy e2 = enemyEditor.CloneEnemy(maps[toMap], e, data, derived.ID, partId);
                            if (toMap != entry.Key)
                            {
                                if (derived.SetColName == null)
                                {
                                    throw new Exception($"No collision defined for cross-map transplant {entry.Key} {data.ID} -> {toMap} {derived.ID}");
                                }
                                // This is also done when the enemy is randomized, if they are
                                // However, their position still needs to be set
                                enemyEditor.SetEnemyCollision(e2, derived.SetColName);
                            }
                            defaultData[derived.ID] = enemyEditor.GetEnemyData(e2, toMap);
                            ownerMap[derived.ID] = toMap;
                            if (ann.ScalingSections != null && !ann.ScalingSections.ContainsKey(derived.ID) && ann.ScalingSections.ContainsKey(data.ID))
                            {
                                ann.ScalingSections[derived.ID] = ann.ScalingSections[data.ID];
                            }
                        }
                    }
                }
            }

            // Do metadata processing here
            if (opt["configgen"])
            {
                if (game.Sekiro)
                {
                    new EnemyConfigGen(game, events, eventConfig).WriteSekiroEvents(opt, infos, defaultData);
                }
                else
                {
                    new EnemyConfigGen(game, events, eventConfig).WriteDS3Events(opt, infos, defaultData);
                }
                return null;
            }
            // new ReverseEnemyOrder().EnemyDS3(game, infos); return null;
            Dictionary<int, (TGenerator, List<int>)> generators = enemyEditor.GetGeneratorData(maps);

            // Process core enemy config
            List<int> treeDragonOrder = Enumerable.Repeat(0, 5).ToList();
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (!defaultData.TryGetValue(info.ID, out EnemyData data)) throw new Exception($"Entity {info.ID} does not exist in map; cannot randomize it");

                if (info.Name == null) throw new Exception($"Entity {info.ID} has no name");
                string modelName = info.Name.Split('_')[0];
                info.ModelID = modelName;
                info.ModelName = game.ModelName(info.ModelID);

                if (info.Tags != null)
                {
                    info.TagSet = new HashSet<string>(info.Tags.Split(' '));
                }

                // Game-specific class overrides
                if (game.Sekiro)
                {
                    if (opt["headlessmove"] && info.Class == EnemyClass.Headless)
                    {
                        info.Class = EnemyClass.Miniboss;
                    }
                }
                else
                {
                    // TODO add options here
                    if (opt["mimics"] && info.Class == EnemyClass.Mimic)
                    {
                        info.Class = EnemyClass.Basic;
                    }
                    if (opt["lizards"] && info.Class == EnemyClass.CrystalLizard)
                    {
                        info.Class = EnemyClass.Basic;
                    }
                }
                string model = game.ModelName(data.Model);
                if (info.OwnedBy != 0)
                {
                    if (!infos.TryGetValue(info.OwnedBy, out EnemyInfo main)) throw new Exception($"Entity {info.ID} referencing {info.OwnedBy} which does not exist in config");
                    if (info.Class == EnemyClass.Boss)
                    {
                        // Bosses inherit most attributes
                        info.DefeatFlag = main.DefeatFlag;
                        info.AppearFlag = main.AppearFlag;
                        // This can be different when the arena changes between phases, like Friede
                        info.Arena = info.Arena ?? main.Arena;
                        info.ArenaExclude = main.ArenaExclude;
                        info.ArenaRegion = main.ArenaRegion;
                        info.ExtraArenas = main.ExtraArenas;
                        info.ItemName = main.ItemName;
                    }
                }
                if (info.Arena != null)
                {
                    info.ArenaData = Arena.Parse(info.Arena);
                }
                if (info.ArenaExclude != null)
                {
                    info.ArenaExcludeData = phraseRe.Split(info.ArenaExclude).Select(ex => Arena.Parse(ex)).ToList();
                    if (info.ArenaExcludeData.Count > 8) throw new Exception($"Internal error: exclude regions defined for {info.ID} exceeds 8");
                }
                if (info.ExtraArenas != null)
                {
                    info.ExtraArenaData = new Dictionary<string, List<Arena>>();
                    foreach (KeyValuePair<string, string> extra in info.ExtraArenas)
                    {
                        info.ExtraArenaData[extra.Key] = phraseRe.Split(extra.Value).Select(ex => Arena.Parse(ex)).ToList();
                    }
                }
                // High-level game-dependent categories
                if (game.Sekiro)
                {
                    info.IsBossTarget = info.Class == EnemyClass.Boss || info.Class == EnemyClass.TutorialBoss;
                    info.IsImportantTarget = info.Class == EnemyClass.Boss || info.Class == EnemyClass.TutorialBoss
                        || info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.FoldingMonkey;
                    info.IsImmortal = info.Class == EnemyClass.Boss && !info.HasTag("mortal");
                    info.IsMortalSekiroBoss = (info.Class == EnemyClass.Boss && info.HasTag("mortal")) || info.Class == EnemyClass.TutorialBoss;
                }
                else
                {
                    info.IsBossTarget = info.Class == EnemyClass.Boss;
                    info.IsImportantTarget = info.Class == EnemyClass.Boss;
                    info.IsImmortal = info.HasTag("immortal");
                    info.IsMortalSekiroBoss = false;
                }
                // Other game-specific data
                if (game.Sekiro)
                {
                    if (info.IsImportantTarget && info.Arena == null)
                    {
                        throw new Exception($"Entity {info.ID} has no arena defined");
                    }
                    if (info.Arena != null && (info.DragonArena != null || info.DragonTrees != null))
                    {
                        if (info.DragonArena == null || info.DragonTrees == null) throw new Exception($"Entity {info.ID} with dragon placement does not have both [{info.DragonArena}] and [{info.DragonTrees}] defined");
                        info.DragonArenaData = Arena.Parse(info.DragonArena);
                        info.DragonTreeList = info.DragonTrees.Split(' ').Select(t => int.Parse(t)).ToList();
                    }
                    if (info.Class == EnemyClass.Helper && info.DragonTrees != null)
                    {
                        treeDragonOrder[int.Parse(info.DragonTrees)] = info.ID;
                    }
                    if (info.IsBossTarget)
                    {
                        if (info.Phases == 0) throw new Exception($"No phase count defined for boss {info.ID}");
                        if (!(info.HasTag("early") || info.HasTag("mid") || info.HasTag("late"))) throw new Exception($"No difficulty defined for boss {info.ID}");
                    }
                    if (info.Class == EnemyClass.Miniboss && info.DefeatFlag == 0)
                    {
                        info.DefeatFlag = 10000000 + info.ID;
                    }
                }
                if (info.Regions != null)
                {
                    foreach (string region in info.Regions)
                    {
                        if (region.StartsWith("copy"))
                        {
                            string[] parts = region.Split(' ');
                            int source = int.Parse(parts[1]);
                            int target = int.Parse(parts[2]);
                            string map = defaultData[info.ID].Map;
                            enemyEditor.MakeRegionCopy(maps, () => target, map, map, source, false);
                        }
                    }
                }
            }

            foreach (EnemyGroup group in ann.Groups)
            {
                if (group.Models == null) continue;
                Dictionary<string, List<string>> models = new Dictionary<string, List<string>>();
                foreach (string model in group.Models)
                {
                    string[] parts = model.Split('-');
                    AddMulti(models, parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null);
                }
                foreach (EnemyInfo info in infos.Values)
                {
                    if (models.TryGetValue(info.ModelName, out List<string> tags))
                    {
                        if (tags.Contains(null) || (info.Category != null && tags.Contains(info.Category) || tags.Any(t => info.HasTag(t))))
                        {
                            // Console.WriteLine($"Adding to {group.Name}: {info.DebugText}");
                            group.Entities.Add(info.ID);
                            info.TagSet.Add(group.Name);
                        }
                    }
                }
            }

            if (preset != null)
            {
                preset.ProcessEnemyPreset(game, infos, ann.Categories, defaultData);
            }

            // Special pass for Old Dragons, because the # to replace depends on the distribution of enemies
            int randomOldDragons = 0;
            if (game.Sekiro && opt["bosses"] && opt["enemies"])
            {
                SortedSet<int> unpreferred = ann.GetGroup("nodragon");
                List<int> eligibleForDragons = preset?.Basic == null && preset?.Add == null
                    ? infos.Values.Where(info => info.Class == EnemyClass.Basic && !unpreferred.Contains(info.ID)).Select(info => info.ID).ToList()
                    : (preset.Add ?? preset.Basic).SelectMany(pool => pool.PoolGroups.SelectMany(p => p)).ToList();
                // There are 15 old dragons in total, 1 of which is not randomized, and 14 of which can be randomized
                // If eligible enemy count > 200, 3 old dragons total (minimum to make the fight interesting)
                // If eligible enemy count > 100, 7 old dragons total (half and half)
                // If any non-boss enemy count > 10, 12 old dragons total (trio fight, challenge)
                // Otherwise, if below these limits, 14 old dragons total (boss fight in its own right, but with old dragon swoop available)
                if (eligibleForDragons.Count > 200) randomOldDragons = 12;
                else if (eligibleForDragons.Count > 100) randomOldDragons = 8;
                else if (eligibleForDragons.Count(i => !infos[i].IsBossTarget) > 10) randomOldDragons = 3;
                else randomOldDragons = 1;
            }

            Dictionary<string, PARAM> Params = game.Params;

            // Make all scaling speffects, even if we're not using them in this run.
            // Mapping from (source section, target section) to (scaling without xp, scaling with xp)
            Dictionary<(int, int), (int, int)> scalingSpEffects = new ScalingEffects(game).EditScalingSpEffects();

            int seed = opt.Seed2 == 0 ? (int)opt.Seed : (int)opt.Seed2;

            // Finally process Old Dragons
            if (randomOldDragons > 0)
            {
                List<EnemyInfo> oldDragons = infos.Values.Where(i => i.Class == EnemyClass.OldDragon).ToList();
                Shuffle(new Random(seed + 42), oldDragons);
                for (int i = 0; i < Math.Min(oldDragons.Count, randomOldDragons); i++)
                {
                    oldDragons[i].Class = EnemyClass.Basic;
                }
            }

            // Force mapping from target to source. This does disrupt the seed, but only way to avoid that would be with potentially extensive swapping.
            Dictionary<int, int> forceMap = new Dictionary<int, int>();
            HashSet<int> autoForce = new HashSet<int>();
            // Use forcemap to disable DLC randomization
            if (ignoredMaps.Count > 0)
            {
                foreach (EnemyInfo info in infos.Values)
                {
                    if (ignoredMaps.Contains(info.Map))
                    {
                        forceMap[info.ID] = info.ID;
                        autoForce.Add(info.ID);
                    }
                }
            }
            if (preset != null)
            {
                // Specific enemies in specific locations
                Random forcemapRandom = new Random((int)seed);
                foreach (KeyValuePair<int, List<int>> transfer in preset.EnemyIDs.OrderBy(e => e.Key))
                {
                    forceMap[transfer.Key] = transfer.Value.Count == 1 ? transfer.Value[0] : Choice(forcemapRandom, transfer.Value);
                }
                // Also try to implement norandom as enemies mapping to themselves
                foreach (int norandom in preset.DontRandomizeIDs)
                {
                    forceMap[norandom] = norandom;
                }
                // Finally, if there are doubles, randomize them to their originals
                if (preset.EnemyMultiplier > 1)
                {
                    foreach (EnemyInfo info in infos.Values)
                    {
                        if (info.HasTag("dupe") && info.SplitFrom > 0)
                        {
                            forceMap[info.ID] = info.SplitFrom;
                        }
                    }
                }
            }
            foreach (KeyValuePair<int, ChestInfo> mimic in newMimics)
            {
                if (infos[mimic.Key].Class == EnemyClass.DupeOnly)
                {
                    forceMap[mimic.Key] = mimic.Value.ClosestMimic;
                }
            }

            Dictionary<int, List<int>> reverseForceMap = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<int, int> transfer in forceMap)
            {
                AddMulti(reverseForceMap, transfer.Value, transfer.Key);
            }
            Dictionary<EnemyClass, EnemyPermutation> silos = new Dictionary<EnemyClass, EnemyPermutation>();
            foreach (EnemyClass type in randomizedTypes)
            {
                silos[type] = new EnemyPermutation { Type = type };
            }
            Random reducePassiveRandom = new Random(seed);
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (silos.TryGetValue(info.Class, out EnemyPermutation silo))
                {
                    if (forceMap.ContainsKey(info.ID))
                    {
                        silo.Mapping[info.ID] = forceMap[info.ID];
                    }
                    else
                    {
                        silo.Targets.Add(info.ID);
                    }
                    if (reverseForceMap.TryGetValue(info.ID, out List<int> targets) && targets.Any(t => infos[t].Class == info.Class))
                    {
                        // If force mapped somewhere within the same silo, take it out of the source pool
                    }
                    else if (preset != null && preset.RemoveSourceIDs.Contains(info.ID))
                    {
                        // If remove source, don't add the source (the target should still get added if not forced)
                    }
                    else if (opt["reducepassive"] && info.HasTag("passive") && reducePassiveRandom.NextDouble() <= 0.9f)
                    {
                        // Cut 90% of instances of a passive enemy. (Could make this configurable per model)
                    }
                    else
                    {
                        silo.Sources.Add(info.ID);
                    }
                }
            }

            bool debugPlacement = false;
            string ename(int ent)
            {
                EnemyData data = defaultData[ent];
                return $"{data.Name} #{ent} ({game.ModelCharacterName(data.Model, data.Char)})";
            }
            // Don't populate a given arena with the same enemy type. For Sekiro minibosses, it is mainly Shigekichi, Juzou,
            // and Vilehand arenas that are reused.
            // DS3 doesn't use miniboss arenas and there are a lot of them so models are used directly
            Dictionary<string, HashSet<string>> arenaEnemyTypes = new Dictionary<string, HashSet<string>>();
            foreach (EnemyInfo info in infos.Values)
            {
                if (info.Arena != null && info.EnemyType != null) AddMulti(arenaEnemyTypes, info.Arena, info.EnemyType);
            }
            // For the sake of the mod being more interesting, exclude interesting enemies from being temporary
            HashSet<int> phantomGroups = new HashSet<int>
            {
                // Ashina phantoms
                1505201, 1505211, 1705200, 1705201, 2005200, 2005201,
                // Sunken Valley phantoms
                1505202, 1505212, 2005210, 2005211,
                // Mibu Village phantoms
                1705220, 1705221, 2005220, 2005221,
                // Temporary Hidden Forest enemies
                1505400,
            };

            bool canPlace(int source, int target, EnemyPermutation silo, BossPhaseLimit prevLimits, bool explain)
            {
                // Always place an enemy if target is already selected
                if (forceMap.ContainsKey(target)) return true;
                EnemyInfo sourceInfo = infos[source];
                EnemyInfo targetInfo = infos[target];
                // Custom exclusions
                if (targetInfo.TagSet.Any(t => t.StartsWith("exclude:")))
                {
                    foreach (string exclude in targetInfo.TagSet.Where(t => t.StartsWith("exclude:")))
                    {
                        string excludeStr = exclude.Substring(8);
                        if (excludeStr == source.ToString() || sourceInfo.HasTag(excludeStr))
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it is excluded from {excludeStr}");
                            return false;
                        }
                    }
                }
                // Exclusion for Divine Dragon: it is very boring unless the arena supports it
                if (sourceInfo.ItemName == "divinedragon" && targetInfo.DragonArenaData == null)
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because no Divine Dragon arena support");
                    return false;
                }
                // Try not to put interesting enemies in phantom spots. (will this be okay with 'oops all' sorts of modes?)
                if (sourceInfo.HasTag("unique") && defaultData[target].Group.Any(g => phantomGroups.Contains(g)))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it is a unique enemy into a transient target");
                    return false;
                }
                // Bosses in the same spot are boring
                if (targetInfo.Class == EnemyClass.Boss || targetInfo.Class == EnemyClass.Miniboss)
                {
                    if (targetInfo.Arena != null)
                    {
                        if (sourceInfo.Arena == targetInfo.Arena)
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same arena");
                            return false;
                        }
                        if (sourceInfo.EnemyType != null && targetInfo.EnemyType != null
                            && arenaEnemyTypes.TryGetValue(targetInfo.Arena, out HashSet<string> types) && types.Contains(sourceInfo.EnemyType))
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same enemy type");
                            return false;
                        }
                    }
                    // TODO this is probably fine for Sekiro too, but double-check. DS3 doesn't have miniboss arenas (or minibosses, really)
                    if (!game.Sekiro)
                    {
                        if ((sourceInfo.EnemyType != null && sourceInfo.EnemyType == targetInfo.EnemyType)
                            || defaultData[source].Model == defaultData[target].Model)
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same model");
                            return false;
                        }
                    }
                }
                // Hostile NPCs in the same spot are boring
                if (targetInfo.Class == EnemyClass.HostileNPC)
                {
                    if (defaultData[source].Char > 0 && defaultData[source].Char == defaultData[target].Char)
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same character type");
                        return false;
                    }
                }
                // Progression options which are hard requirements
                if ((targetInfo.Class == EnemyClass.Miniboss && opt["earlyreq"]) || (targetInfo.Class == EnemyClass.Boss && opt["phasebuff"]))
                {
                    if (targetInfo.HasTag("earlyreq"))
                    {
                        if (sourceInfo.HasTag("mid") || sourceInfo.HasTag("late"))
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's not an early enemy");
                            return false;
                        }
                    }
                }
                // Specific boss restrictions for most accessible settings, due to arena
                if (opt["phases"] && opt["phasebuff"] && (sourceInfo.ItemName == "headlessape" || sourceInfo.ItemName == "demonofhatred") && new[] { "owl", "isshin", "genichiro" }.Contains(targetInfo.ItemName))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because of arena restriction due to late-game enemy phases");
                    return false;
                }
                // Early boss roadblocks in DS3
                if (targetInfo.Class == EnemyClass.Boss && opt["earlyreq"])
                {
                    if (targetInfo.HasTag("earlyreq"))
                    {
                        if (sourceInfo.HasTag("late") || sourceInfo.HasTag("excludeearly"))
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's not an early boss");
                            return false;
                        }
                    }
                }
                // Try to prevent adjacent boss phases from being the same enemy, when sources are removed.
                // This has some overlap with Sekiro phase logic but I am not touching that rn
                if (game.DS3 && targetInfo.Class == EnemyClass.Boss && silo.Sources.Count > 1)
                {
                    int otherPhaseTarget = targetInfo.OwnedBy;
                    if (otherPhaseTarget <= 0 && owners.TryGetValue(target, out List<int> owns))
                    {
                        otherPhaseTarget = owns.Find(own => infos[own].Class == EnemyClass.Boss);
                    }
                    if (otherPhaseTarget > 0)
                    {
                        int otherPhaseSource = silo.Mapping[otherPhaseTarget];
                        if (source == otherPhaseSource)
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because already present in other phase {ename(otherPhaseTarget)}");
                            return false;
                        }
                    }
                }

                // Phase equalization, run a bunch of heuristics.
                // Currently only in Sekiro, although this might be desirable in DS3 with how long later bosses can take.
                if (game.Sekiro && prevLimits != null && targetInfo.Class == EnemyClass.Boss)
                {
                    List<EnemyInfo> targets = new List<EnemyInfo> { targetInfo };
                    List<EnemyInfo> sources = new List<EnemyInfo> { sourceInfo };
                    if (owners.TryGetValue(target, out List<int> owns) || targetInfo.OwnedBy != 0)
                    {
                        owns = owns ?? new List<int>();
                        if (targetInfo.OwnedBy != 0) owns.Add(targetInfo.OwnedBy);
                        foreach (int other in owns)
                        {
                            if (other == target) continue;
                            EnemyInfo otherInfo = infos[other];
                            if (otherInfo.Class != EnemyClass.Boss) continue;
                            targets.Add(otherInfo);
                            sources.Add(infos[silo.Mapping[other]]);
                        }
                    }
                    int targetPhases = targets.Sum(t => t.TargetPhases);
                    if (explain) Console.WriteLine($"{string.Join(",", targets.Select(t => t.ID))} considering {string.Join(",", sources.Select(t => t.ID))}: {targets.Sum(t => t.TargetPhases)} target vs {sources.Sum(t => t.SourcePhases)} sources");
                    float sourcePhases = 0;
                    foreach (EnemyInfo info in sources)
                    {
                        float phases = info.SourcePhases;
                        if (opt["phasebuff"])
                        {
                            if (targetInfo.HasTag("early"))
                            {
                                if (info.HasTag("mid")) phases *= 1.1f;
                                else if (info.HasTag("late")) phases *= 1.6f;
                            }
                            else if (targetInfo.HasTag("mid"))
                            {
                                // For 2-phase mid locations, just exclude DoH
                                // if (HasTag("late")) phases *= 1.1f;
                                if (info.ItemName == "demonofhatred") phases *= 1.1f;
                            }
                            else if (targetInfo.HasTag("late"))
                            {
                                if (info.HasTag("early")) phases *= 0.5f;
                            }
                        }
                        sourcePhases += phases;
                    }
                    if (explain) Console.WriteLine($"Boss fit {string.Join(", ", sources.Select(e => ename(e.ID)))} ({sourcePhases}) into {string.Join(", ", targets.Select(e => ename(e.ID)))} ({targetPhases}). Lim {prevLimits.Amount}. Conds {prevLimits.Amount > targetPhases + 2.1f} {prevLimits.Amount - sourcePhases > 1.6f}");
                    if (sourcePhases > targetPhases + 1 && Math.Abs(sourcePhases - targetPhases) > 0.001f)
                    {
                        if (prevLimits.Entity == target)
                        {
                            if (prevLimits.Amount == 0)
                            {
                                prevLimits.Amount = sourcePhases;
                            }
                            // If we are saving enough phases, do the swap anyway.
                            else if (prevLimits.Amount > targetPhases + 2.1f && prevLimits.Amount - sourcePhases > 1.6f)
                            {
                                prevLimits.Amount = sourcePhases;
                                return true;
                            }
                        }
                        return false;
                    }
                }
                return true;
            }
            void fixupPass(EnemyPermutation silo, List<int> targets, bool useLimits)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    bool debug = false;
                    int cur = silo.Mapping[targets[i]];
                    BossPhaseLimit limits = useLimits ? new BossPhaseLimit { Entity = targets[i] } : null;
                    if (!canPlace(cur, targets[i], silo, limits, debug))
                    {
                        for (int j = 0; j < targets.Count; j++)
                        {
                            int cand = silo.Mapping[targets[j]];
                            if (debugPlacement) Console.WriteLine($"In the spot of {ename(targets[i])}: For {ename(cur)}, looking at candidate {ename(cand)}");
                            if (canPlace(cur, targets[j], silo, limits, debug) && canPlace(cand, targets[i], silo, limits, debug))
                            {
                                if (debugPlacement) Console.WriteLine($"In the spot of {ename(targets[i])}: replacing {ename(cur)} with {ename(cand)} (now in {ename(targets[j])})");
                                silo.Mapping[targets[i]] = cand;
                                silo.Mapping[targets[j]] = cur;
                                break;
                            }
                        }
                    }
                }
            }
            void printPermutation(EnemyPermutation silo)
            {
                if (!debugPlacement) return;
                Console.WriteLine("----------------");
                foreach (KeyValuePair<int, int> transfer in silo.Mapping) Console.WriteLine($"For {ename(transfer.Key)}: Using {ename(transfer.Value)}");
                Console.WriteLine("----------------");
            }
            Dictionary<EnemyClass, string> randomizeOpt = new Dictionary<EnemyClass, string>
            {
                [EnemyClass.Boss] = "bosses",
                [EnemyClass.Miniboss] = "minibosses",
                [EnemyClass.Basic] = "enemies",
            };
            bool isRandomized(EnemyClass type)
            {
                return !game.Sekiro || (!randomizeOpt.TryGetValue(type, out string optName) || opt[optName]);
            }
            Dictionary<int, bool> singletons = infos.Values.Where(i => i.HasTag("singleton")).ToDictionary(i => i.ID, i => false);

            foreach (EnemyPermutation silo in silos.Values)
            {
                EnemyClass siloType = silo.Type;
#if DEBUG
                Console.WriteLine($"Found {silo.Sources.Count} sources and {silo.Targets.Count} targets for {siloType}");
#endif
                // Maybe there is nothing to place?
                if (silo.Targets.Count == 0) continue;
                // Assign all enemies which are still eligible targets to their default locations, before overwriting it later, just in case.
                for (int i = 0; i < silo.Targets.Count; i++)
                {
                    silo.Mapping[silo.Targets[i]] = silo.Targets[i];
                }
                if (!isRandomized(siloType)) continue;

                // If no actual sources added (could happen with some combination of forcemap and source removal), just add all of them to the default pool
                int randomCount = 0;
                Random makeRandom()
                {
                    int rseed = seed + (int)silo.Type + (randomCount++) * 10;
                    return new Random(rseed);
                }
                List<int> sources = silo.Sources;
                if (sources.Count == 0)
                {
                    sources = infos.Values.Where(i => i.Class == siloType && (preset == null || !preset.RemoveSourceIDs.Contains(i.ID))).Select(i => i.ID).ToList();
                    // If all sources are manually removed for some reason, use default class as a fallback.
                    if (sources.Count == 0) sources = infos.Values.Where(i => i.Class == siloType).Select(i => i.ID).ToList();
                    if (sources.Count == 0) throw new Exception($"Can't find any enemies to use for default pool for {siloType}");
                }
                RandomSources custom = null;
                RandomSources adds = null;
                if (preset != null)
                {
                    List<PoolAssignment> customPools = null;
                    if (silo.Type == EnemyClass.Basic) customPools = preset.Basic;
                    if (silo.Type == EnemyClass.Miniboss) customPools = preset.Miniboss;
                    if (silo.Type == EnemyClass.Boss || silo.Type == EnemyClass.TutorialBoss) customPools = preset.Boss;
                    if (silo.Type == EnemyClass.FoldingMonkey) customPools = preset.FoldingMonkey;
                    if (customPools != null)
                    {
                        custom = RandomSources.Create(makeRandom(), customPools, sources, silo.Targets.Count);
                    }
                    if (silo.Type == EnemyClass.Basic && preset.Add != null)
                    {
                        adds = RandomSources.Create(makeRandom(), preset.Add, sources, silo.Targets.Count);
                    }
                }

                // Some special pools, if not overridden
                if (siloType == EnemyClass.FoldingMonkey && custom == null)
                {
                    Shuffle(makeRandom(), silo.Sources);

                    // Get some random minibosses, unique by type. For balance, make them early game.
                    // This is mostly excluding O'Rin, Headless, Shichimen, and Sakura Bull, but we would probably want to exclude them anyway even with scaling.
                    List<EnemyInfo> minibosses = infos.Values.Where(s => s.Class == EnemyClass.Miniboss && s.HasTag("early")).OrderBy(s => s.ID).ToList();
                    Shuffle(makeRandom(), minibosses);

                    sources = minibosses.GroupBy(e => e.EnemyType ?? e.ID.ToString()).Select(g => g.First().ID).ToList();
                    Shuffle(makeRandom(), sources);
                }
                else if (siloType == EnemyClass.TutorialBoss && custom == null)
                {
                    // There should be just one of these, but cycle through all bosses anyway
                    Shuffle(makeRandom(), sources);

                    sources = infos.Values.Where(s => s.Class == EnemyClass.Boss).Select(s => s.ID).OrderBy(s => s).ToList();
                    sources.Remove(1110800);  // Castle Genichiro too boring here
                    Shuffle(makeRandom(), sources);
                }
                else
                {
                    Shuffle(makeRandom(), sources);
                }

                List<int> targets = silo.Targets;
                Shuffle(makeRandom(), targets);
                int sourceIndex = 0;
                for (int i = 0; i < silo.Targets.Count; i++)
                {
                    int target = silo.Targets[i];
                    int source;
                    if (adds != null && infos[target].Add != 0)
                    {
                        source = adds.Next(singletons);
                    }
                    else if (custom != null)
                    {
                        source = custom.Next(singletons);
                    }
                    else
                    {
                        source = sources[(sourceIndex++) % sources.Count];
                    }
                    silo.Mapping[target] = source;
                }
                if (siloType == EnemyClass.Boss) printPermutation(silo);

                // Fixup pass
                // Although exclude basic group if it's a custom pool, since right now it's only small heuristics that may interfere with challenge modes
                List<int> targetsToFix = silo.Targets.ToList();
                Shuffle(makeRandom(), targetsToFix);
                if ((custom == null && adds == null) || (!game.Sekiro || siloType != EnemyClass.Basic))
                {
                    fixupPass(silo, targetsToFix, false);
                }

                if (siloType == EnemyClass.Boss && opt["phases"])
                {
                    printPermutation(silo);
                    List<int> targetsBySize = targetsToFix.OrderByDescending(t => infos[silo.Mapping[t]].SourcePhases).ToList();
                    fixupPass(silo, targetsBySize, true);
                }
            }

            // Print everything out
            foreach (EnemyClass siloType in new[] { EnemyClass.Boss, EnemyClass.Miniboss, EnemyClass.Basic })
            {
                Console.WriteLine($"-- {siloType} placements");
                if (!isRandomized(siloType))
                {
                    Console.WriteLine("(not randomized)");
                }
                else
                {
                    string fullName(int ent, bool target)
                    {
                        EnemyData data = defaultData[ent];
                        string name = infos[ent].ExtraName ?? game.ModelCharacterName(data.Model, data.Char);
                        string cat = infos[ent].Category; 
                        if (cat != null) name = $"{phraseRe.Split(cat)[0]} {name}";
                        return $"{name} (#{ent}) {(target ? "in" : "from")} {game.LocationNames[game.Locations[data.Map]]}";
                    }
                    List<EnemyClass> printSilos = new List<EnemyClass> { siloType };
                    if (siloType == EnemyClass.Boss)
                    {
                        printSilos.Add(EnemyClass.TutorialBoss);
                        printSilos.Add(EnemyClass.FoldingMonkey);
                    }
                    else if (siloType == EnemyClass.Basic && !game.Sekiro)
                    {
                        printSilos.Add(EnemyClass.HostileNPC);
                    }
                    foreach (EnemyClass printSilo in printSilos)
                    {
                        foreach (KeyValuePair<int, int> transfer in silos[printSilo].Mapping)
                        {
                            if (autoForce.Contains(transfer.Key)) continue;
                            Console.WriteLine($"Replacing {fullName(transfer.Key, true)}: {fullName(transfer.Value, false)}");
                        }
                    }
                }
                Console.WriteLine();
#if !DEBUG
                for (int i = 0; i < 50; i++) Console.WriteLine();
#endif
            }

            // Region transplanets
            // TODO all of this is a good candidate for a different file.
            // Arena points can be independent of seed, keeping things a bit interesting, and also fixes issues on rerandomization
            Random arenaRandom = new Random();
            Vector3 randomPoint(Arena arena)
            {
                Vector3 box = arena.Box;
                Vector3 res = new Vector3((float)arenaRandom.NextDouble() * box.X - (box.X / 2), 0, (float)arenaRandom.NextDouble() * box.Z - (box.Z / 2));
                return arena.Transform(res);
            }
            Vector3 randomSafePoint(Arena arena, List<Arena> excludes, Vector3? defaultPos = null)
            {
                Vector3 box = arena.Box;
                Vector3 res;
                int rep = 0;
                while (rep++ < 20)
                {
                    res = randomPoint(arena);
                    if (excludes != null)
                    {
                        bool valid = true;
                        foreach (Arena exclude in excludes)
                        {
                            Vector3 exclPos = exclude.InverseTransform(res);
                            if (Math.Abs(exclPos.X) < exclude.Box.X && Math.Abs(exclPos.Z) < exclude.Box.Z)
                            {
                                valid = false;
                                break;
                            }
                        }
                        // Console.WriteLine($"Check position {exclPos.X},{exclPos.Y},{exclPos.Z} within box {exclude.Box.X},{exclude.Box.Y},{exclude.Box.Z}");
                        if (!valid)
                        {
                            continue;
                        }
                    }
                    return res;
                }
                res = defaultPos ?? arena.Pos;
#if DEBUG
                Console.WriteLine($"Warning: using default position at {res.X},{res.Y},{res.Z}");
#endif
                return res;
            }
            Vector3 floorBuffer = new Vector3(0, 3, 0);
            int angelHeight = 20;
            // Similar to helperMapping but for regions, (owner target, source region, mapping type) -> target region
            // TODO does sekiro ever depend on duplicating regions? That would be weird
            Dictionary<(int, int, string), int> regionCopyCache = new Dictionary<(int, int, string), int>();
            Dictionary<int, RegionTarget> copyRegions(string spec, int fromEntity, List<int> args, Dictionary<int, int> reloc, bool replace = false, bool expectArena = true)
            {
                string[] words = spec.Split(' ');
                if (words.Length < 3) throw new Exception($"Internal error: malformed region identifier {spec}");
                string scope = words[0];
                string type = words[1];
                List<int> regions = words.Skip(2).Select(w => int.Parse(w)).ToList();
                int originalCount = regions.Count;
                if (scope == "arg")
                {
                    regions = regions.Intersect(args).ToList();
                }
                else if (scope == "event" || scope == "global")
                {
                    // Transplant all regions
                    // TODO: Make "global" imply replace = true, so the method parameters can be simplified
                }
                else if (scope == "copy")
                {
                    // This is handled in config processing stage
                    return new Dictionary<int, RegionTarget>();
                }
                else throw new Exception($"Internal error: unknown scope in {spec}");
                string fromMap = defaultData[fromEntity].Map;
                int toEntity = reloc[fromEntity];
                string toMap = defaultData[toEntity].Map;
                TEnemy e = enemyEditor.GetEnemy(maps[toMap], toEntity);
                TEnemy eSource = enemyEditor.GetEnemy(maps[fromMap], fromEntity);
                if (e == null) throw new Exception($"Internal error: can't find {toEntity} in {toMap} for {spec}");
                if (eSource == null) throw new Exception($"Internal error: can't find {fromEntity} in {fromMap} for {spec}");
                Dictionary<int, RegionTarget> res = new Dictionary<int, RegionTarget>();
                // Note, infos[toEntity] may not exist if fromEntity is a helper
                // However, defaultData[toEntity] is added if it didn't exist previously
                if (infos.TryGetValue(toEntity, out EnemyInfo toInfo) && toInfo.HasTag("dupe")
                    && toInfo.SplitFrom == fromEntity && !type.Contains("gen"))
                {
                    // For basic warp/detection/etc. arenas, preserve them for dupes
                    return res;
                }
                bool useExisting(int region)
                {
                    if (regionCopyCache.TryGetValue((toEntity, region, type), out int existing))
                    {
                        // Console.WriteLine($"Using cached region: {fromEntity} -> {toEntity} and {region} for {spec}");
                        res[region] = RegionTarget.ID(existing);
                        return true;
                    }
                    return false;
                }
                void setCacheableRegion(int region, int newone)
                {
                    res[region] = RegionTarget.ID(newone);
                    regionCopyCache[(toEntity, region, type)] = newone;
                }
                if (type == "chrpoint")
                {
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, TRegion b, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                        b.Position = e.Position;
                        b.Rotation = e.Rotation;
                        setCacheableRegion(region, bID);
                    }
                }
                else if (type == "chrrel")
                {
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, TRegion b, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                        // Get the relative position from chr to region a
                        Vector3 relPos = Vector3.Subtract(a.Position, eSource.Position);
                        relPos = Vector3.Transform(relPos, Matrix4x4.CreateFromYawPitchRoll(-eSource.Rotation.Y * (float)Math.PI / 180, 0, 0));
                        Vector3 offPos = Vector3.Transform(relPos, Matrix4x4.CreateFromYawPitchRoll(e.Rotation.Y * (float)Math.PI / 180, 0, 0));
                        offPos = Vector3.Add(offPos, e.Position);
                        b.Position = offPos;
                        b.Rotation = new Vector3(0, a.Rotation.Y - eSource.Rotation.Y + e.Rotation.Y, 0);
                        setCacheableRegion(region, bID);
                    }
                }
                else if (type == "chrgenangel")
                {
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (List<TRegion> bs, int newGen) = enemyEditor.MakeGeneratorCopy(maps, newEntity, fromMap, toMap, region, generators, reloc);
                        if (bs != null)
                        {
                            foreach (TRegion b in bs)
                            {
                                b.Position = e.Position + new Vector3(0f, angelHeight, 0f);
                            }
                            setCacheableRegion(region, newGen);
                        }
                    }
                }
                else if (type == "inside")
                {
                    // Do this later, for slightly better teleports
                }
                else if (type == "arenagen")
                {
                    Arena arena = infos[toEntity].ArenaData;
                    if (expectArena && arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (List<TRegion> bs, int newGen) = enemyEditor.MakeGeneratorCopy(maps, newEntity, fromMap, toMap, region, generators, reloc);
                        if (bs != null)
                        {
                            foreach (TRegion b in bs)
                            {
                                b.Position = arena == null ? e.Position : randomSafePoint(arena, infos[toEntity].ArenaExcludeData, e.Position);
                            }
                            setCacheableRegion(region, newGen);
                        }
                    }
                }
                else if (type.StartsWith("arena"))
                {
                    Arena arena = infos[toEntity].ArenaData;
                    // This is probably correct, since IsImportantTarget is used as shorthand for arenas elsewhere
                    if (!infos[toEntity].IsImportantTarget) return res;
                    if (expectArena && arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    List<Vector3> corners = null;
                    if (arena != null && type.StartsWith("arenapartition"))
                    {
                        corners = new List<Vector3>();
                        for (int i = -1; i <= 1; i += 2)
                        {
                            for (int j = -1; j <= 1; j += 2)
                            {
                                corners.Add(arena.Transform(new Vector3(arena.Box.X / 2 * i, 0, arena.Box.Z / 2 * j)));
                            }
                        }
                    }
                    if (type == "arenaexclude")
                    {
                        // For princes. Just calculate 4 boxes from main arena bounds, and other excludes
                        int regionIndex = 0;
                        // This does add up to a lot of regions (4 + 8 = 12 out of 18 to support main princes arena),
                        // so an AI edit is used to increase the max from 18 to 24, as well as adding region 3412891
                        List<(int, int)> coords = new List<(int, int)> { (0, 1), (0, -1), (1, 0), (-1, 0) };
                        foreach ((int x, int z) in coords)
                        {
                            int region = regions[regionIndex++];
                            if (useExisting(region)) continue;
                            (TRegion a, TRegion b, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                            setCacheableRegion(region, bID);
                            if (!(b.Shape is MSB.Shape.Box box)) throw new Exception($"Internal error: Twin Princes AI region {region} is not a box");

                            // Add total coverage 3x larger than the original arena, with each exclude region
                            // constituting a 1x3 edge surrounding the central region.
                            Vector3 outsideBox = new Vector3(arena.Box.X * (x == 0 ? 3 : 1), arena.Box.Y, arena.Box.Z * (z == 0 ? 3 : 1));
                            Vector3 outsidePos = arena.Transform(new Vector3(x * arena.Box.X, 0, z * arena.Box.Z));

                            box.Width = outsideBox.X;
                            box.Height = outsideBox.Y + floorBuffer.Y;
                            box.Depth = outsideBox.Z;
                            b.Position = Vector3.Subtract(outsidePos, floorBuffer);
                            b.Rotation = arena.Rot;
                        }
                        if (infos[toEntity].ArenaExcludeData != null)
                        {
                            foreach (Arena exclude in infos[toEntity].ArenaExcludeData)
                            {
                                int region = regions[regionIndex++];
                                if (useExisting(region)) continue;
                                (TRegion a, TRegion b, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                                setCacheableRegion(region, bID);
                                if (!(b.Shape is MSB.Shape.Box box)) throw new Exception($"Internal error: Twin Princes AI region {region} is not a box");
                                box.Width = exclude.Box.X;
                                box.Height = exclude.Box.Y + floorBuffer.Y;
                                box.Depth = exclude.Box.Z;
                                b.Position = Vector3.Subtract(exclude.Pos, floorBuffer);
                                b.Rotation = exclude.Rot;
                            }
                        }
                        // Untouched regions don't matter so hackily exclude them from the main loop
                        regions.Clear();
                    }
                    int partition = 0;
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, TRegion b, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                        setCacheableRegion(region, bID);
                        if ((type == "arena" || type == "arenafull") || (arena == null && type == "arenapartition"))
                        {
                            // Try to add this hack for composite shape
                            if (region == 2502570 || region == 2502571)
                            {
                                b.Shape = new MSB.Shape.Box();
                            }
                            if (!(b.Shape is MSB.Shape.Box box)) throw new Exception($"For now, only box regions can encompass entire arenas - {region} in {spec}");
                            // Just ignore x/z rotation for the moment. The x/z bounds will be bigger than when it's laid flat, but hopefully not too much.
                            // Also add a few units since these are often floating above arenas
                            int sizeMult = type == "arenafull" ? 10 : 1;
                            if (type == "arenafull" && infos[toEntity].ArenaRegion > 0)
                            {
                                TRegion fullArena = enemyEditor.GetRegion(maps[toMap], infos[toEntity].ArenaRegion);
                                if (fullArena == null) throw new Exception($"{toEntity}'s region {infos[toEntity].ArenaRegion} not found");
                                b.Position = fullArena.Position;
                                b.Rotation = fullArena.Rotation;
                                b.Shape = fullArena.Shape.DeepCopy();
                            }
                            else if (arena != null)
                            {
                                box.Width = arena.Box.X * sizeMult;
                                box.Height = arena.Box.Y + floorBuffer.Y * sizeMult;
                                box.Depth = arena.Box.Z * sizeMult;
                                b.Position = Vector3.Subtract(arena.Pos, floorBuffer * sizeMult);
                                b.Rotation = arena.Rot;
                            }
                            else
                            {
                                box.Width = box.Height = box.Depth = 10 * sizeMult;
                                b.Position = Vector3.Subtract(e.Position, floorBuffer * sizeMult);
                                b.Rotation = e.Rotation;
                            }
                        }
                        else if (type == "arenapoint" || type == "arenasfx" || type == "arenabox"
                            || (arena == null && (type == "arenaposition" || type == "arenapartitionpoint")))
                        {
                            // Just move to center for now
                            if (arena == null)
                            {
                                b.Position = e.Position;
                            }
                            else
                            {
                                // TODO: randomPoint should probably have some distance away from edges (artificially reduce box edges by 0.5 or something)
                                if (type == "arenabox" || originalCount == 1)
                                {
                                    b.Position = arena.Pos;
                                }
                                else
                                {
                                    b.Position = randomSafePoint(arena, infos[toEntity].ArenaExcludeData, e.Position);
                                }
                                // b.Position = (type == "arenabox" || originalCount > 1) ? randomPoint(arena) : arena.Pos;  ?????
                            }
                            // Shapes: point, sphere, box
                            // Only box is used for detection. Points used for spawning/warping enemies, spheres used for Isshin fire
                            if (type == "arenabox" && b.Shape is MSB.Shape.Box box)
                            {
                                box.Height += 3;
                                b.Position = Vector3.Subtract(b.Position, floorBuffer);
                            }
                        }
                        else if (type == "arenapartition")
                        {
                            if (!(b.Shape is MSB.Shape.Box box))
                            {
                                b.Shape = box = new MSB.Shape.Box();
                            }
                            box.Width = arena.Box.X;
                            box.Height = arena.Box.Y + 3;
                            box.Depth = arena.Box.Z;
                            if (regions.Count >= 4)
                            {
                                // Cover all 4 corners. Works better for square arenas
                                b.Position = corners[partition];
                                partition = (partition + 1) % 4;
                            }
                            else if (regions.Count >= 2)
                            {
                                // Cover two corners per box. Also dependent on arena size.
                                // These corners will also have the same x coord, so it is okay to expand width to cover them both
                                b.Position = Vector3.Lerp(corners[partition], corners[partition + 1], 0.5f);
                                box.Width *= 2;
                                partition = (partition + 2) % 4;
                            }
                            else if (regions.Count == 1)
                            {
                                // Just make the arena bigger for detection areas
                                box.Width *= 2;
                                box.Height *= 2;
                                box.Depth *= 2;
                            }
                            // In all cases do the usual adjustment
                            b.Position = Vector3.Subtract(b.Position, floorBuffer);
                            b.Rotation = arena.Rot;
                        }
                        else if (type == "arenapartitionpoint")
                        {
                            // For simplicity, always place within 4 quadrants
                            Vector3 quadrantMidpoint = Vector3.Lerp(corners[partition], arena.Pos, 0.5f);
                            partition = (partition + 1) % 4;
                            Arena subArena = new Arena
                            {
                                Box = arena.Box / 2,
                                Pos = quadrantMidpoint,
                                Rot = arena.Rot,
                                RotMatrix = arena.RotMatrix,
                                InvRotMatrix = arena.InvRotMatrix,
                            };
                            b.Position = randomSafePoint(subArena, infos[toEntity].ArenaExcludeData, e.Position);
                        }
                        else throw new Exception($"Unknown region spec: {spec}");
                    }
                }
                else if (type.StartsWith("dist"))
                {
                    int dist;
                    // Small hack to make True Monk not so hard to find in Dragon arena, albeit a pretty bad hack
                    if (type == "distdragon") dist = toEntity == 2500800 ? 30 : 10;
                    else dist = int.Parse(type.Substring(4));
                    foreach (int region in regions)
                    {
                        // Put negative number as convention to replace in/out conditions with distance
                        res[region] = RegionTarget.Dist(dist);
                    }
                }
                else throw new Exception($"Unknown region spec: {spec}");
                return res;
            }

            if (game.Sekiro)
            {
                // Small convenience: don't disable boss bonfires when we can't always guarantee that boss helpers die.
                // DS3 is a bit more thorough here.
                HashSet<int> noDisableBonfires = new HashSet<int> {
                    1101955,  // Ashina Castle Gate
                    1111952,  // Castle Tower Lookout
                    1501953,  // Wedding Cave Door
                    1701956,  // Guardian Ape's Burrow
                    1701953,  // Guardian Ape's Watering Hole
                    2501951,  // Vermillion Bridge
                };
                foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
                {
                    if (!game.Locations.ContainsKey(entry.Key)) continue;
                    string map = game.Locations[entry.Key];
                    MSBS msb = entry.Value;

                    foreach (MSBS.Part.Collision col in msb.Parts.Collisions)
                    {
                        if (noDisableBonfires.Contains(col.DisableBonfireEntityID))
                        {
                            col.DisableBonfireEntityID = -1;
                        }
                    }
                }
            }

            if (opt["printnpc"])
            {
                HashSet<int> npcPrintInfo = new HashSet<int>();
                foreach (EnemyInfo info in infos.Values.Where(i => i.Class != EnemyClass.None).OrderBy(i => i.ID))
                {
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    if (!npcPrintInfo.Add(row.ID)) continue;
                    if ((float)row["hitHeight"].Value > 3 && !info.HasTag("large")) Console.WriteLine($"Radius {row["hitRadius"].Value} Height {row["hitHeight"].Value} - {info.DebugText}");
                }
                npcPrintInfo.Clear();
                foreach (EnemyInfo info in infos.Values.Where(i => i.Class != EnemyClass.None).OrderBy(i => i.ID))
                {
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    if (!npcPrintInfo.Add(row.ID)) continue;
                    Console.WriteLine($"Dead {row["disableIntiliazeDead"].Value} Respawn {row["disableRespawn"].Value} - {info.DebugText}");
                }
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.Class == EnemyClass.None) continue;
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    int itemLot = (int)row["ItemLotId1"].Value;
                    PARAM.Row item = Params["ItemLotParam"][itemLot];
                    if (item == null) continue;
                    bool hasFlag = false;
                    while (item != null)
                    {
                        if ((int)item["getItemFlagId"].Value > 0)
                        {
                            hasFlag = true;
                            break;
                        }
                        itemLot++;
                        item = Params["ItemLotParam"][itemLot];
                    }
                    if (hasFlag)
                    {
                        Console.WriteLine($"Item: {info.DebugText}");
                    }
                }
            }

            // Now altered NPC params
            // NPC -> team id. Default team affiliations. Most are removed and later reapplied (like in DS1 enemy rando)
            Dictionary<int, byte> npcOriginalTeam = new Dictionary<int, byte>();
            // NPC -> speffects. Make sure that swamp-immune enemies remain swamp-immune
            Dictionary<int, List<int>> npcCopySpEffect = new Dictionary<int, List<int>>();
            // NPC -> (item lot, event flag). Changed from NPC drops to scripted drops
            Dictionary<int, (int, int)> npcItemLots = new Dictionary<int, (int, int)>();
            void shrinkNpc(PARAM.Row row, float radius, float height)
            {
                if ((float)row["hitRadius"].Value > radius)
                {
                    row["hitRadius"].Value = row["subHitRadius"].Value = radius;
                }
                if ((float)row["hitHeight"].Value > height)
                {
                    row["hitHeight"].Value = row["subHitHeight"].Value = height;
                }
            }
            // Custom NPC edits before duplicating them
            if (game.Sekiro)
            {
                foreach (PARAM.Row row in Params["NpcParam"].Rows)
                {
                    int teamType = (byte)row["npcType"].Value;
                    if (teamType == 24 || teamType == 29)
                    {
                        npcOriginalTeam[row.ID] = (byte)row["npcType"].Value;
                        row["npcType"].Value = (byte)6;
                    }
                    // TODO try to investigate this further, but it seems like a StrongEnemy type classification
                    // Also maybe 13?
                    teamType = (byte)row["teamType"].Value;
                    if (teamType == 14)
                    {
                        row["teamType"].Value = (byte)3;
                    }
                }
                // Remove butterfly invisibility (3509210)
                Params["NpcParam"][50900001]["spEffectId25"].Value = -1;

                // Start Owl 2 with his sword out
                PARAM.Row owl2Npc = Params["NpcParam"][50601010];
                owl2Npc["ModelDispMask2"].Value = (byte)1;
                owl2Npc["ModelDispMask3"].Value = (byte)0;
                owl2Npc["ModelDispMask4"].Value = (byte)1;

                // Mark Old Dragons so that they can have vanilla behavior in their boss fight
                Params["NpcParam"][53000010]["spEffectId24"].Value = 3530001;
                Params["NpcParam"][53000011]["spEffectId24"].Value = 3530001;
                Params["NpcParam"][53000012]["spEffectId24"].Value = 3530001;

                // Show Divine Dragon always. This will make it appear early in the divine realm as well.
                if (opt["bosses"] || preset != null)
                {
                    Params["NpcParam"][52000000]["ModelDispMask10"].Value = (byte)1;
                }
            }
            else
            {
                // DS3 has a lot more variety in enemy types, and more dependency on this for specific boss fights.
                // So do it only for non-human NPCs (>=100000), for Enemy StrongEnemy Enemy2 ArchEnemyTeam (6 7 24 33), for Basic randomized
                // Poison, Toxic, ignore Blood and Curse (lava seems to be managed by event script)
                int[] resistSps = new[] { 30000, 30010 };
                List<EnemyClass> teamClasses = new List<EnemyClass> { EnemyClass.Basic, EnemyClass.Miniboss, EnemyClass.HostileNPC };
                HashSet<int> transferTeams = new HashSet<int>(
                    infos.Values.Where(i => teamClasses.Contains(i.Class)).Select(i => defaultData[i.ID].NPC));
                foreach (PARAM.Row row in Params["NpcParam"].Rows)
                {
                    byte teamType = (byte)row["teamType"].Value;
                    if (transferTeams.Contains(row.ID) && (teamType == 7 || teamType == 24 || teamType == 33))
                    {
                        npcOriginalTeam[row.ID] = teamType;
                        row["teamType"].Value = (byte)6;
                    }

                    // Resists for Poison Toxic Blood Curse are only in 6 7 8 9 respectively
                    for (int i = 6; i <= 7; i++)
                    {
                        int sp = (int)row[$"spEffectId{i}"].Value;
                        if (resistSps.Contains(sp))
                        {
                            AddMulti(npcCopySpEffect, row.ID, sp);
                        }
                    }

                    // Non-boss enemies shouldn't have radius >2.5. This is mainly the giants, and a few are 2.8.
                    // This must be like 1 for everyone to fit everywhere.
                    // Regular miniboss height is limited by height of barracks basement, which is around 5.5.
                    // The Profaned Dungeon hallways are like height 3-4 though and firelink chest is 4,
                    // accounted for with 'exclude:large' label.
                    shrinkNpc(row, 2.5f, 5.1f);
                }
                // Handle NPC drops which shouldn't travel with the NPC. This should be independent of item randomizer.
                // Always do it for all eligible enemies so that restart isn't required between randomizations
                foreach (EnemyInfo info in infos.Values.Where(i => i.HasTag("npcitem")))
                {
                    int npc = defaultData[info.ID].NPC;
                    if (npcItemLots.ContainsKey(npc)) continue;
                    PARAM.Row row = Params["NpcParam"][npc];
                    int baseItemLot = (int)row["ItemLotId1"].Value;
                    int itemLot = baseItemLot;
                    PARAM.Row item = Params["ItemLotParam"][itemLot];
                    if (item == null) continue;
                    List<int> flags = new List<int>();
                    while (item != null)
                    {
                        // Require a guaranteed drop
                        int flag = (int)item["getItemFlagId"].Value;
                        if (flag > 0)
                        {
                            int totalDrop = Enumerable.Range(1, 8).Sum(i => (short)item[$"LotItemBasePoint0{i}"].Value);
                            if (Enumerable.Range(1, 8).Any(i => totalDrop == (short)item[$"LotItemBasePoint0{i}"].Value))
                            {
                                flags.Add(flag);
                            }
                        }
                        item = Params["ItemLotParam"][++itemLot];
                    }
                    // Console.WriteLine($"[{string.Join(", ", flags)}] = {info.DebugText}");
                    // It just so happens that the last flag is the most unique one. Hopefully this always works
                    if (flags.Count > 0 || (info.HasTag("noitemflag") && info.DefeatFlag > 0))
                    {
                        npcItemLots[npc] = (baseItemLot, flags.Count > 0 ? flags.Max() : info.DefeatFlag);
                        row["ItemLotId1"].Value = -1;
                    }
                }

                // Mimic/lizard initializations. Also, perceptive mimic mode
                List<string> respawnModel = new List<string> { "c2120", "c2150" };
                HashSet<int> editedRespawns = new HashSet<int>();
                foreach (EnemyInfo info in infos.Values.Where(i => respawnModel.Contains(i.ModelID)))
                {
                    int npc = defaultData[info.ID].NPC;
                    if (!editedRespawns.Add(npc)) continue;
                    PARAM.Row row = Params["NpcParam"][npc];
                    row["disableIntiliazeDead"].Value = (byte)0;
                    row["disableRespawn"].Value = (byte)0;
                    if (opt["supermimic"] && info.ModelID == "c2120")
                    {
                        row["spEffectId20"].Value = 6960;
                    }
                }
                if (opt["supermimic"])
                {
                    // While we're here, vastly increase mimic chase dist. They can still be reticent though.
                    Params["NpcThinkParam"][212000]["backhomeBattleDist"].Value = (short)30;
                    Params["NpcThinkParam"][212010]["backhomeBattleDist"].Value = (short)30;
                }

                // Don't skip first phase of multi-boss fight on quitout
                Params["NpcParam"][132000]["disableIntiliazeDead"].Value = (byte)1;  // Crystal Sage
                Params["NpcParam"][209000]["disableIntiliazeDead"].Value = (byte)1;  // Oceiros
                Params["NpcParam"][224000]["disableIntiliazeDead"].Value = (byte)1;  // Vordt
                Params["NpcParam"][526000]["disableIntiliazeDead"].Value = (byte)1;  // Yhorm

                // Make Wyvern suck less (starting HP is 6000 with very high resistance)
                Params["NpcParam"][314100]["Hp"].Value = 300;

                // Put Gael 2 at 65% HP, matching second phase
                Params["NpcParam"][620000]["Hp"].Value = (int)(2776 * 0.65);

                // Make guaranteed DLC invaders not invasion-color. Maybe should depend on randomization, if that's optional
                Params["NpcParam"][21200]["PhantomParamId"].Value = -1;
                Params["NpcParam"][22140]["PhantomParamId"].Value = -1;
                Params["NpcParam"][22240]["PhantomParamId"].Value = -1;
                Params["NpcParam"][22340]["PhantomParamId"].Value = -1;
            }

            // Duplicate npc params
            // For bosses, make a copy of them with XP drops (Experience) and money drops (getSoul)
            // For minibosses, prepare to make a copy of them if regular enemies go there, with modified Hp, stamina/staminaRecoverBaseVal?, and HealthbarNum
            HashSet<int> npcIds = new HashSet<int>(Params["NpcParam"].Rows.Select(e => (int)e.ID));
            PARAM.Row duplicateNpcParam(int baseNpc)
            {
                PARAM.Row baseRow = Params["NpcParam"][baseNpc];
                if (baseRow == null) throw new Exception($"NPC {baseNpc} is referenced but does not exist");
                int copyId = baseNpc;
                while (npcIds.Contains(copyId)) copyId++;
                npcIds.Add(copyId);
                PARAM.Row copyRow = game.AddRow("NpcParam", copyId);
                GameEditor.CopyRow(baseRow, copyRow);
                return copyRow;
            }

            // Preemptively make copies of all bosses as basic enemies and minibosses. Also monkeys, since they have nothing by default.
            Dictionary<int, int> bossAsBasicNpc = new Dictionary<int, int>();
            Random reward = new Random((int)seed + 40);
            foreach (EnemyInfo info in infos.Values.Where(i => i.Class == EnemyClass.Boss || i.Class == EnemyClass.FoldingMonkey || i.Class == EnemyClass.TutorialBoss).OrderBy(i => i.ID))
            {
                int source = info.ID;
                int baseNpc = defaultData[source].NPC;
                PARAM.Row row = duplicateNpcParam(baseNpc);
                // Sen
                if (game.Sekiro)
                {
                    int amt = info.HasTag("early") ? reward.Next(50, 100) : (info.HasTag("mid") ? reward.Next(100, 150) : reward.Next(100, 200));
                    row["getSoul"].Value = info.SourcePhases * amt;
                    // XP - more lenient or strict values, closer to minibosses or to regular enemies. Note that many bosses can just fall off and die pretty easily. So do something in between
                    // amt = info.HasTag("early") ? reward.Next(100, 200) : (info.HasTag("mid") ? reward.Next(150, 250) : reward.Next(200, 300));
                    // amt = info.HasTag("early") ? reward.Next(25, 50) : (info.HasTag("mid") ? reward.Next(50, 75) : reward.Next(50, 100));
                    amt = info.HasTag("early") ? reward.Next(50, 100) : (info.HasTag("mid") ? reward.Next(100, 150) : reward.Next(150, 200));
                    row["Experience"].Value = info.SourcePhases * amt / (opt["splitskills"] ? 5 : 1);
                }
                else
                {
                    // This is probably too low
                    row["getSoul"].Value = info.HasTag("early") ? reward.Next(100, 500) : (info.HasTag("mid") ? reward.Next(500, 1000) : reward.Next(1000, 2000));
                }

                row["disableIntiliazeDead"].Value = (byte)0;
                row["disableRespawn"].Value = (byte)0;
                if (game.DS3)
                {
                    // Marker speffect for non-boss instances, for use in manual AI scripts
                    row["spEffectId20"].Value = 6969;
                }
                bossAsBasicNpc[source] = row.ID;
            }

            // Lazily make copies of regular enemies as specific bosses/minibosses, will basically only apply in 'oops all' mode.
            // Miniboss targets should only occur once so this does not need to be memoized.
            List<string> basicBuffFields = game.Sekiro
                ? new List<string> { "Experience", "getSoul", "Hp", "stamina", "staminaRecoverBaseVal" }
                : new List<string> { "getSoul", "Hp" };
            SortedSet<int> noDeathblow = ann.GetGroup("nodeathblow");
            int getBasicAsBoss(int source, int target)
            {
                int baseNpc = defaultData[source].NPC;
                if (preset != null && !preset.BuffBasicEnemiesAsBosses) return baseNpc;

                PARAM.Row targetRow = Params["NpcParam"][defaultData[target].NPC];
                if (targetRow == null) return baseNpc;

                PARAM.Row row = duplicateNpcParam(baseNpc);
                foreach (string field in basicBuffFields)
                {
                    object newVal = targetRow[field].Value;
                    // Slightly buff enemies as bosses by taking geometric mean of HP values
                    if (row[field].Value is int sourceInt && targetRow[field].Value is int targetInt)
                    {
                        if (targetInt > sourceInt) newVal = (int)Math.Sqrt(sourceInt * targetInt);
                    }
                    if (row[field].Value is short sourceShort && targetRow[field].Value is short targetShort)
                    {
                        if (targetShort > sourceShort) newVal = (short)Math.Sqrt(sourceShort * targetShort);
                    }
                    row[field].Value = newVal;
                }
                if (game.Sekiro && !noDeathblow.Contains(source) && infos.TryGetValue(target, out EnemyInfo targetInfo) && targetInfo.IsImportantTarget)
                {
                    row["HealthbarNum"].Value = targetInfo.IsBossTarget ? (byte)targetInfo.Phases : targetRow["HealthbarNum"].Value;
                }
                row["disableIntiliazeDead"].Value = (byte)1;
                return (int)row.ID;
            }

            int helperModelBase = 100;
            // Mapping of enemies which are randomized, from source to target
            Dictionary<int, List<int>> mapping = new Dictionary<int, List<int>>();
            // Another one from target to source
            Dictionary<int, int> revMapping = new Dictionary<int, int>();
            // Mapping of new helpers, (owner target, source helper) -> target helper
            Dictionary<(int, int), int> helperMapping = new Dictionary<(int, int), int>();

            Dictionary<int, int> totalTargetCounts = silos.Values.SelectMany(s => s.Mapping).GroupBy(e => e.Value).ToDictionary(e => e.Key, e => e.Count());
            bool withinMaxAllowed(int source, int target, int amount, bool lenient = false)
            {
                // Generally allow all named targets (around 40 of these in the game)
                if (infos[target].IsImportantTarget) return true;
                // If too many of a given type of enemy and all of their helpers, the game will crash.
                if (totalTargetCounts.TryGetValue(source, out int targetCount) && targetCount >= amount)
                {
                    // In some cases, allow a proportional approach
                    if (lenient) return target.GetHashCode() % targetCount < amount;
                    return false;
                }
                return true;
            }
            bool multichrSwitch = true;
            bool enableMultichr(int source, int target)
            {
                if (!multichrSwitch) return false;
                // Allow splits, although this may get unwieldy
                if (source == infos[target].SplitFrom) return true;
                if (game.Sekiro)
                {
                    // Hirata Masanaga. Don't forbid replacement from having dogs past the limit, but limit the global number of dogs.
                    if (source == 1000353 && !withinMaxAllowed(source, target, 60, true)) return false;
                    // True Monk does not need helpers
                    if (source == 2500850 && !withinMaxAllowed(source, target, 60)) return false;
                    // Lady Butterfly has a ton of helpers
                    if (source == 1000810 && !withinMaxAllowed(source, target, 40)) return false;
                    // Divine Dragon adds a ridiculous number of objects, so only allow named targets to be dragon
                    if (source == 2500800 && infos[target].DragonArenaData == null) return false;
                }
                else if (game.DS3)
                {
                    if (infos[target].IsImportantTarget) return true;
                    if (infos[source].HasTag("bossonlymulti")) return false;
                    // Crystal Sage (3 helpers)
                    if (source == 3300850 && !withinMaxAllowed(source, target, 50)) return false;
                    // Abyss Watchers (2 helpers and a group)
                    if (source == 3300801 && !withinMaxAllowed(source, target, 50)) return false;
                    // Gravetender (4 helpers, although only Greatwolf may be needed)
                    if (source == 4500860 && !withinMaxAllowed(source, target, 50)) return false;
                    // Pontiff (1 helper but a ton of events)
                    if (source == 3700850 && !withinMaxAllowed(source, target, 500)) return false;
                }
                return true;
            }
            int partsNeeded = totalTargetCounts.Where(s => infos[s.Key].HasTag("npcpart")).Sum(s => s.Value);
            // This seems to be a safe global limit. There can be no more than a handful of npc parts loaded at once locally.
            // Alternatively, could try to track this down per map and use map connections.
            bool partsRestricted = partsNeeded >= 48;
            if (game.DS3)
            {
                // Not tested, but more maps should mean higher limit, right.
                partsRestricted = partsNeeded >= 96;
            }
            HashSet<int> usedGlobalSources = new HashSet<int>();
            HashSet<int> usedHelpers = new HashSet<int>();
            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                ModelEditor models = enemyEditor.GetModelEditor(msb);
                HashSet<string> removedObjects = new HashSet<string>();

                foreach (EnemyPermutation silo in silos.Values)
                {
                    foreach (KeyValuePair<int, int> transfer in silo.Mapping)
                    {
                        int target = transfer.Key;
                        int source = transfer.Value;
                        // Process targets in this map
                        if (defaultData[target].Map != entry.Key) continue;
                        // Treat self-randomizations as none at all. Try to avoid these for bosses/minibosses though, for variety.
                        if (source == target) continue;
                        EnemyData data = defaultData[source];
                        int? overrideNpc = null;
                        if (!infos[target].IsBossTarget && bossAsBasicNpc.TryGetValue(source, out int sourceNpc))
                        {
                            overrideNpc = sourceNpc;
                        }
                        else if (infos[target].IsImportantTarget && (infos[source].Class == EnemyClass.Basic || infos[source].Class == EnemyClass.FoldingMonkey || infos[source].Class == EnemyClass.OldDragon))
                        {
                            overrideNpc = getBasicAsBoss(source, target);
                        }
                        TEnemy e = enemyEditor.TransplantEnemy(msb, data, target, overrideNpc);

                        AddMulti(mapping, source, target);
                        revMapping[target] = source;
                        ownerMap[target] = entry.Key;
                        bool targetMultichr = enableMultichr(source, target);

                        Dictionary<int, int> groupMapping = new Dictionary<int, int>();
                        if (infos[source].Groups != null)
                        {
                            foreach (int group in infos[source].Groups)
                            {
                                if (targetMultichr)
                                {
                                    int group2 = newGroupEntity();
                                    groupMapping[group] = group2;
                                    helperMapping[(target, group)] = group2;
                                    ownerMap[group2] = entry.Key;
                                }
                                else
                                {
                                    // Just use target directly in lieu of groups, if needed
                                    helperMapping[(target, group)] = target;
                                }
                            }
                        }
                        enemyEditor.CopyGroups(msb, data, groupMapping, e, infos[target].RemoveGroup);

                        // TODO: Put all specific locations in config as primary arena
                        // Shichimen bad placement? or gravity?
                        if (target == 1300200)
                        {
                            e.Position = new Vector3(e.Position.X, e.Position.Y + 0.5f, e.Position.Z);
                        }
                        // Okami Leader too close to the edge
                        if (target == 2500406)
                        {
                            e.Position = new Vector3(-3.874f, 129.309f, 401.915f);
                        }
                        if (target == 1000810)
                        {
                            // Some enemies to rewrite away from Butterfly 2 spot to Butterfly 1 spot
                            // Emma: doesn't aggro. Headless Ape: gets stuck on hand. Lightning Genichiro: gets stuck falling and voids out, sometimes.
                            // Actually: just do it for all of them, to be safe.
                            e.Position = new Vector3(-239.089f, -787.188f, 583.271f);
                        }
                        if (source == 2500800)
                        {
                            if (infos[target].DragonArenaData != null && infos[target].DragonTrees != null)
                            {
                                e.Position = infos[target].DragonArenaData.Pos;
                                e.Rotation = infos[target].DragonArenaData.Rot;
                            }
                        }
                        // Seven Spears can get some big enemies stuck in the Moon-View Tower
                        if (target == 1120530)
                        {
                            e.Position = new Vector3(-189.066f, -37.287f, 345.113f);
                        }
                        if (target == 1700200)
                        {
                            // Move Gun Fort Snake Eyes out of the wall
                            if (infos[source].IsBossTarget || infos[source].EnemyType == "chainedogre")
                            {
                                e.Position = new Vector3(-234.235f, -190.218f, 517.457f);
                            }
                        }
                        if (target == 2500800)
                        {
                            e.Position = new Vector3(-22.610f, 388.983f, 318.763f);
                            MSBS.Part.Object treeObj = (msb as MSBS).Parts.Objects.Find(o => o.EntityID == 2501815);
                            if (treeObj != null)
                            {
                                treeObj.Position = e.Position;
                                treeObj.Rotation = new Vector3(0, 90, 0);
                            }
                        }
                        // General solution
                        if (infos[target].TryGetArena("primary", out Arena primary))
                        {
                            e.Position = primary.Pos;
                            e.Rotation = primary.Rot;
                        }
                        if (infos[target].HasTag("dupe") && infos[source].TryGetArena("dupe", out Arena dupe))
                        {
                            e.Position = dupe.Pos;
                            e.Rotation = dupe.Rot;
                        }
                        if (target == 3500800)
                        {
                            // Deacon. Warp in point, 3502810
                            e.Position = new Vector3(-474.323f, -197.240f, -631.515f);
                        }
                        if (target == 3900800)
                        {
                            // Move Yhorm forward
                            e.Position = new Vector3(130.831f, -424.860f, -556.350f);
                        }
                        if (target == 3100800)
                        {
                            // Greatwood
                            e.Position = new Vector3(161.780f, -180.956f, -960.972f);
                            e.Rotation = new Vector3(0, 165, 0);
                        }
                        if (source == 5100201)
                        {
                            // Turn Bridge Midir around
                            e.Rotation -= new Vector3(0, 180, 0);
                        }
                        if (game.DS3 && newMimics.TryGetValue(target, out ChestInfo chestInfo))
                        {
                            // Move chest enemies to chest location, remove chest.
                            // Lookup can be done with IMsb, less efficiently and more circuitously
                            MSB3.Part.Object chest = (msb as MSB3).Parts.Objects.Find(o => o.EntityID == chestInfo.ID);
                            e.Position = chest.Position;
                            e.Rotation = chest.Rotation;
                            (msb as MSB3).Parts.Objects.RemoveAll(o => o.EntityID == chestInfo.ID);
                            removedObjects.Add(chest.Name);
                        }
                        if (infos[target].SetColName != null)
                        {
                            string col = infos[target].SetColName;
                            enemyEditor.SetEnemyCollision(e, col);
                            defaultData[target].Col = col;
                        }

                        if (infos[source].HasTag("hidden"))
                        {
                            // Don't allow wooooooo enemies or true monk to walk around, as this interrupts their invisibility
                            if (e is MSBS.Part.Enemy es)
                            {
                                es.UnkT20 = -1;
                            }
                            else if (e is MSB3.Part.Enemy e3)
                            {
                                e3.WalkRouteName = null;
                            }
                        }
                        // Allow extra region features if the enemy stays unique. Or if not unique and not a singleton
                        // (instances won't get messed up other instances' regions), allow a single transplanted boss.
                        bool uniqueTarget = totalTargetCounts.TryGetValue(source, out int targetCount) && targetCount == 1;
                        if (!uniqueTarget && !infos[source].HasTag("singleton") && revMapping.ContainsKey(target) && infos[target].IsImportantTarget)
                        {
                            uniqueTarget = usedGlobalSources.Add(source);
                        }
                        if (infos[source].Regions != null && uniqueTarget)
                        {
                            foreach (string spec in infos[source].Regions)
                            {
                                copyRegions(spec, source, new List<int>(), new Dictionary<int, int> { { source, target } }, replace: true);
                            }
                        }
                        // Edit large NPCs if they appear in non-large places.
                        // This doesn't need to be especially organized. It's fine if we get more than we need.
                        bool smallDest = infos[target].HasTag("exclude:large");
                        if (smallDest && infos[source].HasTag("large") && !opt["noshrink"])
                        {
                            shrinkNpc(Params["NpcParam"][data.NPC], 1f, 3f);
                        }
                        if (infos[target].ArenaData != null)
                        {
                            // Also always add arena, as a debugging tool
                            Arena arena = infos[target].ArenaData;
                            enemyEditor.MakeArenaRegion(msb, arena, $"Arena for {target}");
                            List<Arena> excludes = infos[target].ArenaExcludeData;
                            if (excludes != null)
                            {
                                for (int i = 0; i < excludes.Count; i++)
                                {
                                    enemyEditor.MakeArenaRegion(msb, excludes[i], $"Arena exclude {i} for {target}");
                                }
                            }
                            // Easy way of setting up arena-aligned locations: make 9 enemies in the grid
                            if (opt["testplacements"] && game.DS3)
                            {
                                float[] xPlaces = new float[] { 1 - arena.Box.X / 2, 0, arena.Box.X / 2 - 1 };
                                float[] zPlaces = new float[] { 1 - arena.Box.Z / 2, 0, arena.Box.Z / 2 - 1 };
                                foreach (float xPlace in xPlaces)
                                {
                                    foreach (float zPlace in zPlaces)
                                    {
                                        TEnemy e2 = enemyEditor.CloneEnemy(msb, e, defaultData[2888002], -1, helperModelBase++);
                                        e2.Position = arena.Transform(new Vector3(xPlace, 0, zPlace));
                                        double viewCenter = Math.Atan2(e2.Position.X - arena.Pos.X, e2.Position.Z - arena.Pos.Z);
                                        e2.Rotation = new Vector3(0, (float)(viewCenter / Math.PI * 180), 0);
                                    }
                                }
                                models.UseModel("c1100");
                            }
                        }
                        models.UseModel(e.ModelName);

                        // Also add minions
                        if (owners.TryGetValue(source, out List<int> helpers))
                        {
                            Arena arena = infos[target].ArenaData;
                            foreach (int helper in helpers)
                            {
                                if (!targetMultichr) continue;
                                if (infos[helper].Class != EnemyClass.Helper) continue;
                                EnemyData data2 = defaultData[helper];
                                int target2 = newEntity();
                                TEnemy e2 = enemyEditor.CloneEnemy(msb, e, data2, target2, helperModelBase++);
                                enemyEditor.ClearGroups(e2);
                                float extraHeight = 0;
                                if (infos[helper].HasTag("primary"))
                                {
                                    // TODO: For secondary/generator/etc., we may need to look those up manually
                                    e2.Position = e.Position;
                                    e2.Rotation = e.Rotation;
                                }
                                else if (arena != null)
                                {
                                    e2.Position = randomSafePoint(arena, infos[target].ArenaExcludeData, e.Position);
                                    if (game.Sekiro)
                                    {
                                        // Arenas are a bit more of an art than a science, so some extra height for safety.
                                        // In theory we're more careful about this in DS3
                                        extraHeight = 0.5f;
                                    }
                                }
                                if (infos[helper].HasTag("angel"))
                                {
                                    extraHeight = angelHeight;
                                }
                                if (extraHeight > 0)
                                {
                                    e2.Position = new Vector3(e2.Position.X, e2.Position.Y + extraHeight, e2.Position.Z);
                                }
                                if (smallDest && infos[helper].HasTag("large"))
                                {
                                    shrinkNpc(Params["NpcParam"][data2.NPC], 1f, 3f);
                                }
                                models.UseModel(e2.ModelName);
                                helperMapping[(target, helper)] = target2;
                                revMapping[target2] = helper;
                                ownerMap[target2] = entry.Key;
                                enemyEditor.CopyGroups(msb, data2, groupMapping, e2);
                                if (infos[helper].Regions != null && uniqueTarget)
                                {
                                    // There is not really any "default" data for helpers, but this is needed just for the purpose of regions
                                    // TODO can we get rid of this hack
                                    defaultData[target2] = enemyEditor.GetEnemyData(e2, entry.Key);
                                    foreach (string spec in infos[helper].Regions)
                                    {
                                        copyRegions(spec, helper, new List<int>(), new Dictionary<int, int> { { helper, target2 } }, replace: true);
                                    }
                                }
                            }
                        }
                        if (objectOwners.TryGetValue(source, out List<string> objHelpers))
                        {
                            // TODO: Add this to MapEditors. It's a pretty isolated routine either way.
                            if (game.Sekiro)
                            {
                                MSBS sourceMsb = game.SekiroMaps[defaultData[source].Map];
                                List<MSBS.Part.Object> objects = sourceMsb.Parts.Objects.Where(o => objHelpers.Contains(o.EntityID.ToString())).ToList();
                                if (objects.Count != objHelpers.Count) throw new Exception($"Not all required objects {string.Join(",", objHelpers)} found in map of {ename(source)}, only {objects.Count}");
                                foreach (MSBS.Part.Object o in objects)
                                {
                                    if (!targetMultichr) continue;
                                    MSBS.Part.Object e2 = (MSBS.Part.Object)o.DeepCopy();
                                    int target2 = newEntity();
                                    e2.EntityID = target2;
                                    e2.Name = $"{e2.ModelName}_{helperModelBase++:d4}";
                                    if (e2.ObjPartName3 != null)
                                    {
                                        e2.ObjPartName3 = defaultData[target].Col;
                                    }
                                    for (int i = 0; i < e2.EntityGroupIDs.Length; i++) e2.EntityGroupIDs[i] = -1;
                                    (msb as MSBS).Parts.Objects.Add(e2);
                                    models.UseObjectModel(e2.ModelName);
                                    helperMapping[(target, o.EntityID)] = target2;
                                }
                            }
                            else
                            {
                                MSB3 sourceMsb = game.DS3Maps[defaultData[source].Map];
                                foreach (string objName in objHelpers)
                                {
                                    if (!targetMultichr) continue;
                                    // TODO: Distinguish multichr or not
                                    MSB3.Part.Object o = sourceMsb.Parts.Objects.Find(obj => obj.Name == objName);
                                    if (o == null) throw new Exception($"Required object {objName} found in map of {ename(source)}");
                                    MSB3.Part.Object e2 = (MSB3.Part.Object)o.DeepCopy();
                                    // TODO: entity id stuff
                                    if (o.EntityID > 0)
                                    {
                                        e2.EntityID = newEntity();
                                    }
                                    e2.Name = $"{e2.ModelName}_{helperModelBase++:d4}";
                                    if (e2.CollisionName != null)
                                    {
                                        e2.CollisionName = defaultData[target].Col;
                                    }
                                    for (int i = 0; i < e2.EntityGroups.Length; i++) e2.EntityGroups[i] = -1;
                                    (msb as MSB3).Parts.Objects.Add(e2);
                                    models.UseObjectModel(e2.ModelName);
                                    if (o.EntityID > 0)
                                    {
                                        helperMapping[(target, o.EntityID)] = e2.EntityID;
                                    }
                                }
                            }
                        }
                        if (source == 3410832 && target == 4500801)
                        {
                            // Easter egg
                            MSB3.Part.Object bowl = (msb as MSB3).Parts.Objects.Find(o => o.EntityID == 4501810);
                            bowl.AnimIDs[0] = 0;
                            bowl.ModelName = "o302596";
                            bowl.Position -= new Vector3(0, 0.8f, 0);
                            models.UseObjectModel("o302596");
                        }
                        // And remove them from this current map, to avoid having to be strict about removing all emevd references
                        if (owners.TryGetValue(target, out List<int> helpers2))
                        {
                            usedHelpers.UnionWith(helpers2);
                        }
                    }
                }
                // Remove copied enemies that weren't randomized after all
                enemyEditor.RemoveEnemies(msb, id => infos.TryGetValue(id, out EnemyInfo info)
                    && info.SplitFrom > 0 && !info.HasTag("dupe")
                    && !revMapping.ContainsKey(info.Class == EnemyClass.Helper && info.OwnedBy > 0 ? info.OwnedBy : id));

                if (msb is MSB3 mb)
                {
                    if (map == "highwall")
                    {
                        // Too many maps loaded at once
                        List<string> badConnects = new List<string>
                        {
                            // Consumed King's Gardens starting crash
                            // Consumed King's Gardens -> Grand Archives
                            "h003000_0001", "h003700_0001",
                            // Above Dancer -> Grand Archives
                            "h004010_0001", "h080090_0001",
                            // Consumed King's Gardens -> Firelink, before the very last hallway
                            "h003300_0000", "h003500_0000", "h003503_0000",
                            // Vordt to Dancer crash. Unclear if this helps.
                            // Lower part of stairs -> Lothric Castle
                            "h002300_0000", "h002301_0000", "h002302_0000", "h002700_0001", "h002702_0001",
                            // Vordt's arena -> Undead Settlement, before the door (h002701 and h002800)
                            // There's also a triangle (h995000)?
                            "h002700_0000", "h002702_0000", "h002701_0000",
                        };
                        mb.Parts.ConnectCollisions.RemoveAll(col => badConnects.Contains(col.Name));
                    }
                    else if (map == "ariandel")
                    {
                        // Ariandel crash collisions: 4100, 4200
                        // But just disable its BackreadGroups feature
                        foreach (MSB3.Part.Collision c in mb.Parts.Collisions)
                        {
                            for (int i = 0; i < c.DrawGroups.Length; i++)
                            {
                                c.BackreadGroups[i] &= c.DrawGroups[i];
                            }
                        }
                    }
                    else if (map == "ringedcity")
                    {
                        // Mapping from collisions which are unnecessarily loaded (DrawGroups) to collisions which see too much (DispGroups)
                        // The main crashes are crossing over to swamp, or dropping down to final judicator
                        Dictionary<string, List<string>> visibilityReductions = new Dictionary<string, List<string>>
                        {
                            // Curse hallway, to lower stairs and Purging Monument area
                            ["h003800"] = new List<string> { "h005000", "h005400", "h008000", "h007600", "h007900" },
                            // After curse hallway, to lower stairs and Purging Monument area
                            ["h003801"] = new List<string> { "h005000", "h005400", "h008000", "h007600", "h007900" },
                            // First ringed knight encounter, to lower stairs and Purging Monument area
                            ["h004300"] = new List<string> { "h005000", "h005400", "h008000", "h007600", "h007900" },
                            // Midir segment, to abyss swamp and Ledo area. h006001 has the same DispGroups as h006000
                            ["h009300"] = new List<string> { "h006000", "h006300" },
                            // Upper Purging Monument area, to lower stairs/abyss swamp/Ledo
                            ["h007600"] = new List<string> { "h005000", "h005400", "h008000", "h006000", "h006300" },
                        };
                        HashSet<string> dispCols = new HashSet<string>(visibilityReductions.Values.SelectMany(cs => cs));
                        Dictionary<string, MSB3.Part.Collision> dispGroups =
                            mb.Parts.Collisions.Where(c => dispCols.Contains(c.Name)).ToDictionary(c => c.Name, c => c);
                        foreach (MSB3.Part.Collision c in mb.Parts.Collisions)
                        {
                            if (!visibilityReductions.TryGetValue(c.Name, out List<string> removes)) continue;
                            foreach (string remove in removes)
                            {
                                MSB3.Part.Collision seer = dispGroups[remove];
                                for (int i = 0; i < c.DrawGroups.Length; i++)
                                {
                                    c.DrawGroups[i] &= ~seer.DispGroups[i];
                                }
                            }
                        }
                    }
                }
                enemyEditor.GarbageCollect(msb, models.EnemyUsed, removedObjects);
            }
            // Finally, remove all randomized helpers
            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                enemyEditor.RemoveEnemies(msb, id => usedHelpers.Contains(id) && infos[id].Class == EnemyClass.Helper);
                enemyEditor.GarbageCollect(msb);
                enemyEditor.GetEnemies(msb).Sort((a, b) => a.Name.CompareTo(b.Name));
            }
            // Update dupe map for helpers, as well
            foreach (KeyValuePair<int, List<int>> entry in dupeMap.ToList())
            {
                if (owners.TryGetValue(entry.Key, out List<int> helpers))
                {
                    foreach (int helper in helpers)
                    {
                        if (infos[helper].Class != EnemyClass.Helper) continue;
                        List<int> dupeHelpers = new List<int>();
                        foreach (int dupe in entry.Value)
                        {
                            if (helperMapping.TryGetValue((dupe, helper), out int dupeHelper))
                            {
                                dupeHelpers.Add(dupeHelper);
                            }
                        }
                        if (dupeHelpers.Count != entry.Value.Count)
                        {
                            throw new Exception($"{entry.Key} helper {helper} missing dupes: [{string.Join(",", entry.Value)}]->[{string.Join(",", dupeHelpers)}]");
                        }
                        dupeMap[helper] = dupeHelpers;
                    }
                }
            }

            // It's emevd time
            Dictionary<int, EventSpec> templates = eventConfig.EnemyEvents.ToDictionary(e => e.ID, e => e);
            if (templates.ContainsKey(0)) throw new Exception($"Internal error: event 0 in config");
            // Preprocess for dupe handling
            if (dupeEnabled)
            {
                // Manufacture event specs for enemy DupeEvents
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.DupeEvents == null) continue;
                    foreach (int ev in info.DupeEvents)
                    {
                        if (templates.ContainsKey(ev)) continue;
                        templates[ev] = new EventSpec
                        {
                            ID = ev,
                            Dupe = info.ID.ToString(),
                            Template = new List<EnemyTemplate> { new EnemyTemplate { Type = "default" } },
                        };
                    }
                }
                // Manufacture templates for event spec Dupes
                foreach (EventSpec ev in templates.Values)
                {
                    if (ev.Dupe == null) continue;
                    EnemyTemplate t = new EnemyTemplate
                    {
                        Dupe = new Dupe(),
                    };
                    if (ev.Dupe == "rewrite")
                    {
                        t.Type = "loc";
                    }
                    else if (ev.Dupe == "copyarg")
                    {
                        t.Type = "chrarg";
                    }
                    else if (int.TryParse(ev.Dupe, out int source))
                    {
                        t.Type = "chr";
                        t.Entity = source;
                    }
                    else throw new Exception($"Unknown {ev.ID} dupe info: {ev.Dupe}");
                    ev.Template.Add(t);
                }
            }

            // Entities which do not exist in game anymore, but passed as arguments to events.
            // These event initializations are ignored, otherwise an error is thrown if no valid entity is found.
            HashSet<int> ignoreEnemies = new HashSet<int> {
                // Sekiro
                1300231, 1300232, 1300237,
                1500262, 1500263, 1500269, 1500270, 1500272, 1500279, 1500280, 1500281, 1500282, 1500283, 1500284, 1500285, 1500286, 1500287, 1500288, 1500289,
                1500343, 1500362,
                1000601, 1105390, 1100231, 1110252, 1110512, 1110325, 1700392, 1300288, 1300213, 1300227, 1300214, 1500303,
                1700330, 1700331, 1700332, 1700335, 1700336, 1500305, 1500307, 1500310, 1500349, 1500358, 1500373, 1500500,
                1500501, 1700475, 1700207, 1700341, 2000463, 2500420, 2500430, 2500431,
                // DS3
                3100358, 3100355,
                3000400,
            };

            // Old boss swap system using StartCmd/EndCond
            // Preprocess boss commands which need to move around
            Dictionary<(int, int), List<EMEVD.Instruction>> endConds = new Dictionary<(int, int), List<EMEVD.Instruction>>();
            HashSet<int> endCond2s = new HashSet<int>();
            Dictionary<int, List<EMEVD.Instruction>> startCmds = new Dictionary<int, List<EMEVD.Instruction>>();
            if (game.Sekiro)
            {
                Dictionary<int, EnemyTemplate> endEvents = templates.Values
                    .Where(ev => ev.Template.Any(t => t.Type.StartsWith("end")))
                    .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("end")));
                // Also startcmds
                Dictionary<int, EnemyTemplate> startEvents = templates.Values
                    .Where(ev => ev.Template.Any(t => t.Type.StartsWith("start") && t.StartCmd != null))
                    .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("start")));
                foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
                {
                    foreach (EMEVD.Event e in entry.Value.Events)
                    {
                        int id = (int)e.ID;
                        if (endEvents.TryGetValue(id, out EnemyTemplate end))
                        {
                            if (end.EndCond != null)
                            {
                                endConds[(end.Entity, 0)] = events.FindCond(e, end.EndCond).Select(i => events.CopyInstruction(e.Instructions[i])).ToList();
                            }
                            if (end.EndCond2 != null)
                            {
                                endConds[(end.Entity, 1)] = events.FindCond(e, end.EndCond2).Select(i => events.CopyInstruction(e.Instructions[i])).ToList();
                                endCond2s.Add(end.Entity);
                            }
                        }
                        if (startEvents.TryGetValue(id, out EnemyTemplate start))
                        {
                            // No actual edits are done, it's used for instruction matching
                            EventEdits edits = new EventEdits();
                            foreach (string remove in phraseRe.Split(start.StartCmd))
                            {
                                events.RemoveMacro(edits, remove);
                            }
                            for (int i = 0; i < e.Instructions.Count; i++)
                            {
                                Instr instr = events.Parse(e.Instructions[i]);
                                List<InstrEdit> results = edits.GetMatches(instr);
                                if (results != null && results.Count > 0)
                                {
                                    AddMulti(startCmds, start.Entity, e.Instructions[i]);
                                }
                            }
                            if (!startCmds.ContainsKey(start.Entity)) throw new Exception($"Internal error: Couldn't find startcmd [{start.StartCmd}] in {id} for boss {start.Entity}");
                        }
                    }
                }
            }

            // Segment info: (entity id, segment type) -> instructions/regions/etc.
            Dictionary<int, Dictionary<string, CommandSegment>> segmentCmds = new Dictionary<int, Dictionary<string, CommandSegment>>();
            HashSet<int> segmentEvents =
                new HashSet<int>(templates.Values.Where(ev => ev.Template.Any(t => t.Type == "segment")).Select(ev => ev.ID));
            // Animation info: (entity id) -> (starting animation, wakeup animation)
            Dictionary<int, (int, int)> startingAnimations = new Dictionary<int, (int, int)>();
            Dictionary<int, List<(int, int)>> npcStartingAnimations = new Dictionary<int, List<(int, int)>>();
            HashSet<int> animationEvents =
                new HashSet<int>(templates.Values.Where(ev => ev.Template.Any(t => t.Animation != null)).Select(ev => ev.ID));
            void addSegments(int eventID, int entity, List<CommandSegment> segments)
            {
                if (!segmentCmds.TryGetValue(entity, out Dictionary<string, CommandSegment> entitySegments))
                {
                    segmentCmds[entity] = entitySegments = new Dictionary<string, CommandSegment>();
                }
                if (segments.Count == 0) throw new Exception($"Internal error: No segments defined in segment template for {eventID} #{entity}");
                // TODO: perhaps verify dead/disable templates here. Otherwise, looking at the event isn't really required.
                foreach (CommandSegment s in segments)
                {
                    if (s.Type == "remove") continue;
                    if (entitySegments.ContainsKey(s.Type))
                    {
                        throw new Exception($"Internal error: {eventID} #{entity} defines duplicate segment {s.Type}");
                    }
                    if (s.Commands == null)
                    {
                        throw new Exception($"Internal error: {eventID} #{entity} {s.Type} segment missing instructions");
                    }
                    List<EMEVD.Instruction> instrs = new List<EMEVD.Instruction>();
                    entitySegments[s.Type] = s;
                }
            }
            int defaultSegmentEnemy = 9999999;
            if (eventConfig.DefaultSegments != null && eventConfig.DefaultSegments.Count > 0)
            {
                addSegments(0, defaultSegmentEnemy, eventConfig.DefaultSegments);
            }
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    int eventID = (int)e.ID;
                    if (segmentEvents.Contains(eventID))
                    {
                        foreach (EnemyTemplate t in templates[eventID].Template)
                        {
                            if (t.Type != "segment") continue;
                            int entity = t.Entity;
                            if (entity == 0) throw new Exception($"Internal error: Segment template for {eventID} missing entity field");
                            addSegments(eventID, entity, t.Segments);
                        }
                    }
                    if (eventID == 0)
                    {
                        // All animation events are initialization-based from constructor
                        for (int i = 0; i < e.Instructions.Count; i++)
                        {
                            Instr init = events.Parse(e.Instructions[i]);
                            if (!init.Init) continue;
                            int callee = init.Callee;
                            if (!animationEvents.Contains(callee)) continue;
                            foreach (EnemyTemplate t in templates[callee].Template)
                            {
                                if (t.Animation == null) continue;
                                if (!t.Type.StartsWith("loc") && t.Type != "common")
                                {
                                    throw new Exception($"Internal error: {callee} anim referenced in {t.Type} event template {callee}");
                                }
                                string[] parts = t.Animation.Split(' ');
                                int parsePart(int index)
                                {
                                    if (index >= parts.Length) throw new Exception($"Internal error: {callee} anim {t.Animation} missing part {index}");
                                    string part = parts[index];
                                    if (int.TryParse(part, out int val)) return val;
                                    if (!events.ParseArgSpec(part, out int pos)) throw new Exception($"Internal error: {callee} anim {t.Animation} has wrong format");
                                    if (init.Offset + pos >= init.Args.Count) throw new Exception($"{callee} anim {t.Animation} init {init} missing required arguments");
                                    return (int)init.Args[init.Offset + pos];
                                }
                                string type = parts[0];
                                if (type == "active" || type == "passive")
                                {
                                    // Anim id
                                    parsePart(1);
                                    continue;
                                }
                                else if (type == "gravity")
                                {
                                    // Other event id
                                    if (parts.Length >= 5) parsePart(4);
                                }
                                int entity = parsePart(1);
                                if (entity == 0) throw new Exception($"{callee} anim {t.Animation} init {init} missing entity");
                                (int, int) anims = (0, 0);
                                if (type == "wakeup")
                                {
                                    anims = (0, parsePart(2));
                                }
                                else 
                                {
                                    anims = (parsePart(2), parsePart(3));
                                }
                                // common_funcs may be called for nonexistent entities
                                if (!defaultData.ContainsKey(entity)) continue;
                                // Only record animations in non-gravity entities
                                if (type != "gravity")
                                {
                                    // Note that these may occur multiple times, though ideally should be the same between duplications
                                    startingAnimations[entity] = anims;
                                    AddMulti(npcStartingAnimations, defaultData[entity].NPC, anims);
                                }
                            }
                        }
                    }
                }
            }
            // Sanity checks about segment data expected by the scripts
            if (!game.Sekiro)
            {
                List<int> segmentlessBosses =
                    infos.Values.Where(info => info.IsBossTarget && !segmentCmds.ContainsKey(info.ID)).Select(info => info.ID).ToList();
                if (segmentlessBosses.Count > 0) throw new Exception($"Internal error: bosses [{string.Join(", ", segmentlessBosses)}] missing segments");
                foreach (KeyValuePair<int, Dictionary<string, CommandSegment>> entry in segmentCmds)
                {
                    List<string> types = entry.Value.Keys.ToList();
                    if (!types.Contains("start")
                        || types.Count(t => t == "dead" || t == "disable") != 1
                        || types.Count(t => t.EndsWith("setup")) != 1
                        || types.Count(t => t.StartsWith("end")) != 1)
                    {
                        throw new Exception($"Internal error: boss {entry.Key} has invalid segment configuration [{string.Join(", ", types)}]");
                    }
                }
            }

            // System for transplanting camera ids
            // This is a system in Sekiro only to add map-specific pointers to LockCamParam etc.
            // Map from entity id -> starting camera id
            Dictionary<int, int> startCameras = templates.Values
                .SelectMany(ev => ev.Template.Where(t => t.Type.StartsWith("start") && t.Entity != 0 && t.Camera != null))
                .ToDictionary(t => t.Entity, t => int.Parse(t.Camera));
            // Map from (full source id, target map) -> partial target id
            Dictionary<(string, int, int), int> movedCameraSets = new Dictionary<(string, int, int), int>();
            int TransplantParamSet(int id, int source, int target, string param = "CameraSetParam")
            {
                // Camera set param is like 1102500. Entity id is like 1120830. At least for now, rely on entity id format to determine map.
                // Get camera id base id for entity
                int getFullCameraSet(int ent)
                {
                    int area = ent / 100000;
                    int block = (ent / 10000) % 10;
                    return (area * 100000) + (block * 1000);
                }
                int sourceMap = getFullCameraSet(source);
                int targetMap = getFullCameraSet(target);
                int sourceId = sourceMap + id;
                PARAM.Row sourceRow = Params[param][sourceId];
                if (sourceRow == null) throw new Exception($"Error transplating {param} {id} from {source} to {target}: source {sourceId} not found");
                if (!movedCameraSets.TryGetValue((param, sourceId, targetMap), out int targetId))
                {
                    targetId = 0;
                    for (int i = 500; i < 600; i++)
                    {
                        if (Params[param][targetMap + i] == null)
                        {
                            targetId = i;
                            break;
                        }
                    }
                    if (targetId == 0) throw new Exception($"Could not transplant {param}??");
                    PARAM.Row targetRow = game.AddRow(param, targetMap + targetId);
                    GameEditor.CopyRow(sourceRow, targetRow);
                    movedCameraSets[(param, sourceId, targetMap)] = targetId;
                }
                return targetId;
            }
            // Make cameras nicer
            if (game.Sekiro && dupeEnabled)
            {
                foreach (PARAM.Row row in Params["LockCamParam"].Rows)
                {
                    // TODO: Limit to certain bosses. may also need y angles
                    row["CamDistTarget"].Value = (float)row["CamDistTarget"].Value * 1.25f;
                }
            }

            // Name changing/generation stuff
            string getName(Dictionary<string, FMG> fmgs, int nameId)
            {
                string name = fmgs["NPC名"][nameId];
                if (game.DS3)
                {
                    if (string.IsNullOrWhiteSpace(name)) name = fmgs["NPC名_dlc1"][nameId];
                    if (string.IsNullOrWhiteSpace(name)) name = fmgs["NPC名_dlc2"][nameId];
                }
                return name;
            }
            void setName(Dictionary<string, FMG> fmgs, int nameId, string name)
            {
                fmgs["NPC名"][nameId] = name;
            }
            int baseNameId = game.Sekiro ? 902000 : 907000;
            // Mapping from (target entity, base target name) = new target name
            Dictionary<(int, int), int> nameIds = new Dictionary<(int, int), int>();
            int GetCleverName(int id, int source, int target)
            {
                if (!opt["edittext"]) return id;
                if (source == target) return id;
                // If they have the same full name, keep it as is to avoid duplication
                if (infos[source].FullName != null && infos[source].FullName == infos[target].FullName) return id;

                if (!nameIds.TryGetValue((target, id), out int nameId))
                {
                    // Use the part name, otherwise keep things simple and use the model name, for English name
                    string sourceName = infos[source].PartName ?? game.ModelCharacterName(defaultData[source].Model, defaultData[source].Char);
                    string fullName = infos[target].FullName != null ? infos[target].FullName.Replace("$1", sourceName) : sourceName;
                    // In-place replacement would be nice, but there are duplicate usages across phases
                    nameId = baseNameId++;
                    nameIds[(target, id)] = nameId;
                    setName(game.ItemFMGs, nameId, fullName);
                    // Console.WriteLine($"Replacement for {id} -> {nameId} - source {ename(source)} -> target {ename(target)}: {fullName}");

                    // For other languages, use the NPC name directly if it exists
                    if (id != nameId)
                    {
                        foreach (KeyValuePair<string, Dictionary<string, FMG>> lang in game.OtherItemFMGs)
                        {
                            string backupName = null;
                            if (infos[source].NpcName > 0)
                            {
                                backupName = getName(lang.Value, infos[source].NpcName);
                            }
                            if (string.IsNullOrWhiteSpace(backupName))
                            {
                                backupName = getName(lang.Value, id);
                            }
                            if (string.IsNullOrWhiteSpace(backupName))
                            {
                                backupName = "???";
                            }
                            setName(lang.Value, nameId, backupName);
                            // Console.WriteLine($"  {lang.Key} replacement for {id} -> {nameId}: {backupName}");
                        }
                    }
                }
                return nameId;
            }

            Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>> newInitializations = new Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>>();
            // Don't redo non-chr event templates. For now, just use references.
            HashSet<EnemyTemplate> completedTemplates = new HashSet<EnemyTemplate>();
            HashSet<int> removedEvents = new HashSet<int>();
            HashSet<int> usedEvents = new HashSet<int>();
            bool warnBadCommands = false;
            Dictionary<int, EMEVD.Event> commonEvents = game.Emevds["common_func"].Events.ToDictionary(e => (int)e.ID, e => e);
            HashSet<int> removeImmortality = new HashSet<int>(infos.Values
                .Where(info => info.IsMortalSekiroBoss)
                .SelectMany(info => mapping.TryGetValue(info.ID, out List<int> targets) ? targets : new List<int> { }));

            // Entity id utilities
            void fillEntityIdMapping(Dictionary<int, int> reloc, int entity, int target, bool includeHelpers)
            {
                reloc[entity] = target;
                if (includeHelpers)
                {
                    List<int> allHelpers = new List<int>();
                    if (owners.TryGetValue(entity, out List<int> helpers)) allHelpers.AddRange(helpers);
                    if (infos[entity].Groups != null) allHelpers.AddRange(infos[entity].Groups);
                    if (objectOwners.TryGetValue(entity, out List<string> objHelpers))
                    {
                        allHelpers.AddRange(objHelpers.Select(h => int.TryParse(h, out int hi) ? hi : -1).Where(h => h > 0));
                    }
                    foreach (int helper in allHelpers)
                    {
                        if (infos.ContainsKey(helper) && infos[helper].Class != EnemyClass.Helper) continue;
                        if (helperMapping.TryGetValue((target, helper), out int helperTarget))
                        {
                            reloc[helper] = helperTarget;
                        }
                        else
                        {
                            reloc[helper] = helper;
                        }
                    }
                }
                foreach (int derived in reloc.Keys.ToList())
                {
                    if (infos.TryGetValue(derived, out EnemyInfo baseInfo) && baseInfo.SplitFrom > 0)
                    {
                        reloc[baseInfo.SplitFrom] = reloc[derived];
                    }
                }
            }
            List<int> getHelperTargets(int target)
            {
                List<int> helperTargets = new List<int>();
                if (revMapping.TryGetValue(target, out int source) && owners.TryGetValue(source, out List<int> helpers))
                {
                    foreach (int helper in helpers)
                    {
                        if (helperMapping.TryGetValue((target, helper), out int helperTarget))
                        {
                            helperTargets.Add(helperTarget);
                        }
                    }
                }
                return helperTargets;
            }
            List<int> getIntArgs<T>(IEnumerable<T> args)
            {
                return args.SelectMany(o => o is int oi ? new[] { oi } : new int[] { }).ToList();
            }
            void transplantRegionSpecs(
                Dictionary<int, int> reloc, Dictionary<int, int> distReplace, List<string> specs,
                int entity, int target, List<int> args)
            {
                bool expectArena = infos[target].IsImportantTarget;
                foreach (string spec in specs)
                {
                    foreach (KeyValuePair<int, RegionTarget> region in copyRegions(spec, entity, args, reloc, expectArena: expectArena))
                    {
                        RegionTarget r = region.Value;
                        if (r.Region != 0)
                        {
                            reloc[region.Key] = r.Region;
                        }
                        else if (r.Distance != 0)
                        {
                            distReplace[region.Key] = r.Distance;
                        }
                    }
                }
            }
            List<Dictionary<int, int>> dupeRelocs = new List<Dictionary<int, int>>();
            for (int i = 0; i < dupeCount; i++)
            {
                Dictionary<int, int> reloc = new Dictionary<int, int>();
                foreach (KeyValuePair<int, List<int>> entry in dupeMap)
                {
                    reloc[entry.Key] = entry.Value[i];
                }
                dupeRelocs.Add(reloc);
            }
            Dictionary<int, int> progressFlagCopies = new Dictionary<int, int>();
            HashSet<int> usedMusicFlagTargets = new HashSet<int>();

            // Main event loop
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                if (!newInitializations.ContainsKey(entry.Key)) newInitializations[entry.Key] = new List<(EMEVD.Instruction, EMEVD.Event)>();

                // Note this putting all events in a dictionary precludes duplicates, which technically the game allows, so may conflict with other mods
                Dictionary<int, EMEVD.Event> fileEvents = entry.Value.Events.ToDictionary(e => (int)e.ID, e => e);
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    OldParams initOld = OldParams.Preprocess(e);
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr originalInit = events.Parse(e.Instructions[i]);
                        if (!originalInit.Init) continue;
                        int callee = originalInit.Callee;
                        if (!templates.TryGetValue(callee, out EventSpec ev)) continue;
                        // ev.Template = ev.Template.Where(t => !t.Type.Contains("xx")).ToList();
                        if (ev.Template.Count == 0) continue;
                        if (ev.Template[0].Type.StartsWith("remove"))
                        {
                            bool simpleRemove = false;
                            foreach (EnemyTemplate t in ev.Template)
                            {
                                // If the target is specified and comes from somewhere else, remove the event
                                if (t.Type == "removearg")
                                {
                                    int removeEntity = getIntArgs(originalInit.Args.Skip(originalInit.Offset))
                                        .Find(a => infos.ContainsKey(a));
                                    simpleRemove |= revMapping.ContainsKey(removeEntity);
                                }
                                else if (t.Entity > 0)
                                {
                                    simpleRemove |= revMapping.ContainsKey(t.Entity);
                                }
                                else if (t.Entities != null)
                                {
                                    simpleRemove |= t.Entities.Split(' ').All(es => !revMapping.ContainsKey(int.Parse(es)));
                                }
                                else
                                {
                                    simpleRemove = true;
                                }
                            }
                            if (simpleRemove)
                            {
                                // Remove action by removing initialization, for now. Can garbage collect later if desired.
                                e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                                removedEvents.Add(callee);
                                continue;
                            }
                            else
                            {
                                usedEvents.Add(callee);
                            }
                        }
                        bool hasChr = ev.Template.Any(t => t.Type.Contains("chr"));
                        bool allChr = ev.Template.All(t => t.Type.Contains("chr"));
                        // Source/target entity and event copy, for copies. At the moment there is only one chr per event initialization, but perhaps group them together in the future.
                        List<(int, int, EMEVD.Event, EnemyTemplate)> eventCopies = new List<(int, int, EMEVD.Event, EnemyTemplate)>();
                        bool canRemove = true;

                        // Set up all chr edits first, before the event itself is edited
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (!t.Type.Contains("chr"))
                            {
                                if (t.Dupe == null || !dupeEnabled)
                                {
                                    canRemove = false;
                                }
                                continue;
                            }
                            int entity = t.Entity;
                            // Include dummy enemies for now, otherwise it will detect no entity
                            int argEntity = 0;
                            if (entity <= 0)
                            {
                                if (!t.Type.Contains("arg"))
                                {
                                    // This needs to be cleaned up in configs
                                    // Console.WriteLine($"{callee}: bad type {t.Type}");
                                }
                                List<int> intArgs = getIntArgs(originalInit.Args.Skip(originalInit.Offset));
                                argEntity = intArgs.Find(a => infos.ContainsKey(a) || ignoreEnemies.Contains(a));
                                // This system is kind of suspicious, not many edits are possible with it.
                                // Require the template type to be explicit in DS3.
                                if (argEntity == 0)
                                {
                                    // There are some events with just blanked out args
                                    if (intArgs.Max() == 0)
                                    {
                                        continue;
                                    }
                                    if (opt["printarg"])
                                    {
                                        Console.WriteLine($"No entity found in {originalInit} args");
                                        continue;
                                    }
                                    throw new Exception($"No entity found in {originalInit} args");
                                }
                                entity = argEntity;
                                if (infos.TryGetValue(entity, out EnemyInfo argHelper) && argHelper.Class == EnemyClass.Helper && argHelper.OwnedBy > 0)
                                {
                                    entity = argHelper.OwnedBy;
                                }
                            }
                            // Find targets for the entity
                            if (mapping.TryGetValue(entity, out List<int> targets))
                            {
                                targets = targets.ToList();
                            }
                            else
                            {
                                targets = new List<int>();
                            }
                            // Hack for mimic events! TODO, generalize this to work for all dupe+random cases
                            // Something like dupeMap, but that's specific to non-randomized in-place stuff.
                            if (newMimics.Count > 0)
                            {
                                foreach (KeyValuePair<int, ChestInfo> chest in newMimics)
                                {
                                    if (chest.Value.ClosestMimic == entity
                                        && mapping.TryGetValue(chest.Key, out List<int> transitiveTargets))
                                    {
                                        targets.AddRange(transitiveTargets);
                                    }
                                }
                            }
                            // If the entity remains the same, don't remove or edit the original event
                            if (!revMapping.ContainsKey(entity))
                            {
                                canRemove = false;
                                // ...unless it's a deathblow, in which case it must be rewritten
                                // This was a fix for Oops All Sword Saint Isshin
                                if (t.Deathblow != 0)
                                {
                                    targets.Add(entity);
                                    canRemove = true;
                                }
                            }
                            // # of events should not be a problem, since there is a global multichr limit for some enemies, but we'll see
                            if (t.Type.StartsWith("multichronly"))
                            {
                                targets.RemoveAll(target => !enableMultichr(entity, target));
                            }
                            if (partsRestricted && t.Type.StartsWith("chrpart"))
                            {
                                targets.RemoveAll(target => !infos[target].IsImportantTarget);
                            }
                            if (t.Type.Contains("boss"))
                            {
                                targets.RemoveAll(target => !infos[target].IsImportantTarget);
                            }
                            if (t.Type.Contains("basic"))
                            {
                                targets.RemoveAll(target => infos[target].IsImportantTarget);
                            }
                            if (t.Type.Contains("move"))
                            {
                                targets.RemoveAll(target =>
                                    dupeEnabled && infos[target].HasTag("dupe") && infos[target].SplitFrom == entity);
                            }
                            if (t.Dupe != null)
                            {
                                targets.RemoveAll(target =>
                                    !(dupeEnabled && infos[target].HasTag("dupe") && infos[target].SplitFrom == entity));
                            }
                            // Console.WriteLine($"Template {callee} {t.Type} mapping {entity} -> {string.Join(",", targets)}");
                            // If no targets left at this point, nothing to do
                            if (targets.Count == 0) continue;

                            if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent) || commonEvents.TryGetValue(callee, out theEvent))
                            {
                                foreach (int target in targets)
                                {
                                    eventCopies.Add((entity, target, events.CopyEvent(theEvent, NewID()), t));
                                }
                            }
                            else if (argEntity != 0)
                            {
                                foreach (int target in targets)
                                {
                                    eventCopies.Add((entity, target, null, t));
                                }
                            }
                            else
                            {
                                throw new Exception($"Initialized event {callee} but absent from this file and not specified in args");
                            }
                        }
                        // Set up in-place edits
                        int lastDeadEntity = -1;
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (t.Type.Contains("chr")) continue;

                            if (t.Dupe != null && !dupeEnabled)
                            {
                                continue;
                            }
                            if (t.Entity > 0)
                            {
                                // Something must be randomized to this target if entity is specified
                                if (!revMapping.TryGetValue(t.Entity, out int source))
                                {
                                    continue;
                                }
                                // Source-target pair
                                if (t.Transfer > 0 && t.Transfer != source)
                                {
                                    continue;
                                }

                                if (t.Type == "segment")
                                {
                                    // This is managing a kind of ugly cross-template interaction, so it's an order-dependent edit either way.
                                    // This is relevant in cases where the same event sets both phases of a multi-boss fight.
                                    if (t.Segments.Any(s => s.Type == "dead"))
                                    {
                                        lastDeadEntity = t.Entity;
                                    }
                                }
                            }
                            if (t.Entities != null)
                            {
                                // Any must be randomized if several entities are specified
                                if (t.Entities.Split(' ').All(es => !revMapping.ContainsKey(int.Parse(es))))
                                {
                                    continue;
                                }
                            }
                            if (t.Type == "default" || (t.Type == "loc" && t.IsDefault()))
                            {
                                // This is fine, nothing to do if not chr
                                // TODO: Make IsDefault a debug check only
                            }
                            else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                            {
                                if (completedTemplates.Contains(t)) continue;
                                if (t.Type.Contains("arg"))
                                {
                                    eventCopies.Add((0, 0, null, t));
                                }
                                else if (t.Type == "copy")
                                {
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, 0, events.CopyEvent(theEvent, NewID()), t));
                                }
                                else
                                {
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, 0, theEvent, t));
                                }
                            }
                            else if ((t.Type.StartsWith("common") || t.Type == "locarg") && commonEvents.TryGetValue(callee, out EMEVD.Event comEvent))
                            {
                                if (completedTemplates.Contains(t)) continue;
                                if (t.Type == "common") completedTemplates.Add(t);
                                eventCopies.Add((0, 0, comEvent, t));
                            }
                            else throw new Exception($"Can't find event {callee} with {t.Type} template");
                        }

                        bool forceRemove = false;
                        foreach (var copy in eventCopies)
                        {
                            (int entity, int target, EMEVD.Event e2, EnemyTemplate t) = copy;

                            // Main entities to replace, for chr events
                            Dictionary<int, int> reloc = new Dictionary<int, int>();
                            Dictionary<int, int> distReplace = new Dictionary<int, int>();
                            Dictionary<int, int> dupeReloc = null;
                            Instr init = originalInit;
                            // Event replacement option
                            if (t.NewEvent != null)
                            {
                                if (e2 == null) throw new Exception($"No original event to ReplaceEvent with for {callee}");
                                OldParams newPre = OldParams.Preprocess(e2);
                                e2.Instructions.Clear();
                                EventEdits newEdits = new EventEdits();
                                foreach (string cmd in t.NewEvent)
                                {
                                    events.AddMacro(newEdits, EditType.AddAfter, cmd);
                                }
                                events.ApplyAdds(newEdits, e2, newPre);
                                newPre.Postprocess();
                            }
                            if (entity != 0)
                            {
                                fillEntityIdMapping(reloc, entity, target, t.Type.StartsWith("multichr"));
                                if (t.Regions != null)
                                {
                                    transplantRegionSpecs(reloc, distReplace, t.Regions, entity, target, getIntArgs(init.Args));
                                }
                                if (t.Dupe != null)
                                {
                                    // Find the dupe index from the mapping
                                    if (dupeMap.TryGetValue(entity, out List<int> dupes))
                                    {
                                        int dupeIndex = dupes.IndexOf(target);
                                        if (dupeIndex != -1)
                                        {
                                            dupeReloc = dupeRelocs[dupeIndex];
                                        }
                                        else Console.WriteLine($"No dupe index found in {entity}->{target}, {originalInit}");
                                    }
                                }
                                // Also we need to make a copy of the instruction at this point, so we don't edit the original
                                init = events.CopyInit(init, e2);
                            }
                            else if (t.Type == "copy")
                            {
                                init = events.CopyInit(init, e2);
                                init.Save();
                            }
                            // Renamings in the args, for common_func mainly
                            if (t.Name != null && t.Name.StartsWith("X"))
                            {
                                string[] nameParts = t.Name.Split(' ');
                                if (!events.ParseArgSpec(nameParts[0], out int entityPos) || !events.ParseArgSpec(nameParts[1], out int namePos)) throw new Exception($"Bad name spec {t.Name} for {callee}");
                                int nameTarget = (int)init[entityPos + init.Offset];
                                if (revMapping.TryGetValue(nameTarget, out int nameSource))
                                {
                                    int nameId = (int)init[namePos + init.Offset];
                                    reloc[nameId] = GetCleverName(nameId, nameSource, nameTarget);
                                }
                            }

                            // Add all edits
                            OldParams pre = e2 == null ? null : OldParams.Preprocess(e2);
                            EventEdits edits = new EventEdits();
                            if (t.Remove != null)
                            {
                                foreach (string remove in phraseRe.Split(t.Remove))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.RemoveDupe != null && target != 0 && !infos[target].IsBossTarget)
                            {
                                foreach (string remove in phraseRe.Split(t.RemoveDupe))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.TreeDragons != null)
                            {
                                // This isn't used exactly anymoe, since ineligible tree dragons can't be removed without affecting the final deathblow thing
                                string[] parts = phraseRe.Split(t.TreeDragons);
                                if (!events.ParseArgSpec(parts[0], out int entityPos)) throw new Exception($"Bad tree spec {parts[0]} in {t.TreeDragons} for {callee}");
                                bool removeTree = true;
                                if (infos[target].DragonTreeList != null)
                                {
                                    int dragon = (int)init[entityPos + init.Offset];
                                    int dragonIndex = treeDragonOrder.IndexOf(dragon);
                                    if (infos[target].DragonTreeList.Contains(dragonIndex))
                                    {
                                        removeTree = false;
                                    }
                                }
                                if (removeTree)
                                {
                                    foreach (string remove in parts.Skip(1))
                                    {
                                        events.RemoveMacro(edits, remove);
                                    }
                                }
                            }
                            if (t.TreeDragonFlags != null)
                            {
                                if (infos[target].DragonTreeList != null)
                                {
                                    List<int> flags = t.TreeDragonFlags.Split(' ').Select(p => int.Parse(p)).ToList();
                                    int oldFlagBase = flags[0];
                                    int newFlagBase = flags[1];
                                    int minDragon = infos[target].DragonTreeList.Min();
                                    int maxDragon = infos[target].DragonTreeList.Max();
                                    events.ReplaceMacro(edits, $"Randomly Set Event Flag In Range ({oldFlagBase} -> {newFlagBase + minDragon},{oldFlagBase + treeDragonOrder.Count() - 1} -> {newFlagBase + maxDragon},1)");
                                }
                                else
                                {
                                    events.RemoveMacro(edits, "Randomly Set Event Flag In Range");
                                }
                            }
                            if (t.Replace != null)
                            {
                                foreach (string replace in phraseRe.Split(t.Replace))
                                {
                                    events.ReplaceMacro(edits, replace);
                                }
                            }
                            if (t.StartCmd != null)
                            {
                                foreach (string remove in phraseRe.Split(t.StartCmd))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.Segments != null)
                            {
                                Dictionary<int, int> segmentReloc = new Dictionary<int, int>();
                                int source = revMapping[t.Entity];
                                if (!segmentCmds.TryGetValue(t.Entity, out Dictionary<string, CommandSegment> targetSegments))
                                {
                                    throw new Exception($"Internal error: No segments collected for {t.Entity} in {callee}");
                                }
                                if (!segmentCmds.TryGetValue(source, out Dictionary<string, CommandSegment> sourceSegments))
                                {
                                    if (!segmentCmds.TryGetValue(defaultSegmentEnemy, out sourceSegments))
                                    {
                                        throw new NotImplementedException($"Unsupported {source}->{t.Entity}");
                                    }
                                    segmentReloc[defaultSegmentEnemy] = t.Entity;
                                }
                                fillEntityIdMapping(segmentReloc, source, t.Entity, true);
                                int removeIndex = 0;
                                foreach (CommandSegment segment in t.Segments)
                                {
                                    if (sourceSegments == null) break;
                                    if (segment.Type == "altsetup")
                                    {
                                        // altsetup is additional data that doesn't replace any originals
                                        continue;
                                    }
                                    string name = $"{segment.Type}_segment";
                                    if (segment.Type == "remove")
                                    {
                                        name += $"{++removeIndex}";
                                    }
                                    events.RegisterSegment(edits, name, segment.Start, segment.End, segment.IgnoreMatch);
                                    // Remove existing segments
                                    foreach (string cmd in segment.Commands)
                                    {
                                        events.RemoveSegmentMacro(edits, name, cmd);
                                    }
                                    // Find corresponding new segment(s)
                                    List<string> duals = new List<string>();
                                    if (segment.Type == "dead" || segment.Type == "disable")
                                    {
                                        duals.Add(sourceSegments.ContainsKey("dead") ? "dead" : "disable");
                                    }
                                    else if (segment.Type == "setup")
                                    {
                                        duals.Add(sourceSegments.ContainsKey("setup") ? "setup" : "altsetup");
                                    }
                                    else if (segment.Type == "start")
                                    {
                                        if (!targetSegments.ContainsKey("setup"))
                                        {
                                            duals.Add(sourceSegments.ContainsKey("setup") ? "setup" : "altsetup");
                                        }
                                        duals.Add("start");
                                    }
                                    else if (segment.Type.StartsWith("end"))
                                    {
                                        duals.Add(sourceSegments.ContainsKey("end") ? "end" : "endphase");
                                    }
                                    List<string> editedCmds = new List<string>();
                                    foreach (string dual in duals)
                                    {
                                        CommandSegment sourceSegment = sourceSegments[dual];
                                        if (sourceSegment.Regions != null)
                                        {
                                            transplantRegionSpecs(
                                                segmentReloc, new Dictionary<int, int>(), sourceSegment.Regions,
                                                source, t.Entity, new List<int>());
                                        }
                                        foreach (string cmd in sourceSegment.Commands)
                                        {
                                            // Remove some commands if end->endphase
                                            // All end->end issues should be handled by segmentReloc
                                            if (dual == "end" && segment.Type == "endphase")
                                            {
                                                (string endCmd, List<string> addArgs) = ParseCommandString(cmd);
                                                if (endCmd == "HandleBossDefeat" || endCmd == "PlaySE"
                                                    || endCmd == "HandleBossDefeatAndDisplayBanner")
                                                {
                                                    continue;
                                                }
                                            }
                                            if (dual == "start")
                                            {
                                                // Find instances of DisplayBossHealthBar and rewrite them
                                                (string startCmd, List<string> addArgs) = ParseCommandString(cmd);
                                                if (startCmd == "DisplayBossHealthBar")
                                                {
                                                    int nameSource = int.Parse(addArgs[1]);
                                                    int targetNameId = int.Parse(addArgs[3]);
                                                    int nameId = targetNameId;
                                                    if (nameSource == defaultSegmentEnemy)
                                                    {
                                                        // GetCleverName looks up source info so we must insert the actual source.
                                                        nameSource = source;
                                                        // This is the only place where a fake id is used, so try to replace it.
                                                        // If we fail, ??? will be used in non-English languages.
                                                        string targetCmd = segment.Commands.Find(c => c.Contains("BossHealthBar"));
                                                        if (targetCmd != null)
                                                        {
                                                            (_, List<string> nameArgs) = ParseCommandString(targetCmd);
                                                            targetNameId = int.Parse(nameArgs[3]);
                                                        }
#if DEBUG
                                                        else if (infos[t.Entity].NpcName <= 0)
                                                        {
                                                            Console.WriteLine($"Unknown name for {t.Entity}");
                                                        }
#endif
                                                    }
                                                    segmentReloc[nameId] = GetCleverName(targetNameId, nameSource, t.Entity);
                                                }
                                            }
                                            editedCmds.Add(events.RewriteInts(cmd, segmentReloc));
                                        }
                                    }
                                    if (segment.Invincibility)
                                    {
                                        bool enable = segment.Type != "start";
                                        List<int> invTargets = getHelperTargets(t.Entity);
                                        invTargets.Add(t.Entity);
                                        foreach (int invTarget in invTargets)
                                        {
                                            int invSource = revMapping[invTarget];
                                            editedCmds.Add($"SetCharacterInvincibility({invTarget}, {(enable ? "Enabled" : "Disabled")})");
                                            if (!enable && infos[invSource].IsImmortal)
                                            {
                                                editedCmds.Add($"SetCharacterImmortality({invTarget}, Enabled)");
                                            }
                                        }

                                    }
                                    if (duals.Contains("endphase") && segment.Type == "end")
                                    {
                                        editedCmds.Add($"PlaySE({t.Entity}, SoundType.s_SFX, 777777777)");
                                        editedCmds.Add($"HandleBossDefeat({t.Entity})");
                                    }
                                    foreach (string cmd in editedCmds)
                                    {
                                        events.AddMacro(edits, EditType.SegmentAdd, cmd, name);
                                    }
                                    // Rewrite 'dead' segments to 'disable' ones, if it's the last template for it
                                    if (segment.Type == "dead" && t.Entity == lastDeadEntity)
                                    {
                                        // e.g. GotoIfEventFlag(Label.LABEL0, OFF, TargetEventFlagType.EventFlag, 13200800)
                                        Instr gotoIf = events.Parse(events.ParseAdd(segment.Start));
                                        if (gotoIf.Name != "GotoIfEventFlag") throw new Exception($"Invalid segment {segment.Start} for {t.Entity}");
                                        string flag = gotoIf[3].ToString();
                                        events.RemoveMacro(edits, segment.Start, applyOnce: true);
                                        events.AddMacro(
                                            edits, EditType.AddBefore, $"SkipIfEventFlag(1, OFF, TargetEventFlagType.EventFlag, {flag})",
                                            segment.End, applyOnce: true);
                                    }
                                }
                            }
                            if (t.Animation != null)
                            {
                                string[] parts = t.Animation.Split(' ');
                                string type = parts[0];
                                int parsePart(int index)
                                {
                                    string part = parts[index];
                                    if (events.ParseArgSpec(part, out int partPos)) return (int)originalInit.Args[originalInit.Offset + partPos];
                                    return int.Parse(part);
                                }
                                // There are two types of animation routines: rewriting 5450-based events, and changing anims
                                if (type == "active" || type == "passive")
                                {
                                    // For 5450-based events, we need to remove the 5450 check before a MAIN IfConditionGroup
                                    // For passive events, we additionally need to disable AI with starting anim and
                                    // enable AI after MAIN check if starting anim is not set.
                                    if (e2 == null) throw new Exception($"Internal error: {callee} anim template type {t.Type}");
                                    List<string> aiCommands(bool enable)
                                    {
                                        if (type == "active") return new List<string>();
                                        return new List<string>
                                        {
                                            $"SkipIfComparison(1, ComparisonType.NotEqual, X4_4, -1)",
                                            $"SetCharacterAIState(X0_4, {(enable ? "Enabled" : "Disabled")})",
                                        };
                                    }
                                    foreach (string cmd in aiCommands(false))
                                    {
                                        events.AddMacro(edits, EditType.AddBefore, cmd);
                                    }
                                    foreach (EMEVD.Instruction ins in e2.Instructions)
                                    {
                                        Instr instr = events.Parse(ins);
                                        if (instr.Name == "IfConditionGroup" && instr.Args[0].ToString() == "0")
                                        {
                                            string toFind = instr.ToString();
                                            foreach (string cmd in aiCommands(true))
                                            {
                                                events.AddMacro(edits, EditType.AddAfter, cmd, instr.ToString());
                                            }
                                            break;
                                        }
                                        if (instr.Name == "IfCharacterHasSpEffect" && instr.Args[2].ToString() == "5450")
                                        {
                                            events.RemoveMacro(edits, instr.ToString());
                                        }
                                    }
                                }
                                else
                                {
                                    int locEntity = parsePart(1);
                                    revMapping.TryGetValue(locEntity, out int source);
                                    if (source > 0 && infos[source].HasTag("hidden"))
                                    {
                                        forceRemove = true;
                                    }
                                    else if (source > 0)
                                    {
                                        List<int> anims = null;
                                        if (startingAnimations.TryGetValue(source, out (int, int) sourceAnims))
                                        {
                                            anims = new List<int> { sourceAnims.Item1, sourceAnims.Item2 };
                                        }
                                        if (type == "gravity")
                                        {
                                            // In this case, the event should ignore animations, unless the enemy's position
                                            // has been edited to support a different event.
                                            if (parts.Length >= 5 && infos[locEntity].TryGetArena("primary", out _))
                                            {
                                                init[init.Offset - 1] = int.Parse(parts[4]);
                                            }
                                            else
                                            {
                                                anims = null;
                                            }
                                        }
                                        List<int> eventIndices = type == "wakeup"
                                            ? new List<int> { -1, 2 }
                                            : new List<int> { 2, 3 };
                                        for (int a = 0; a < 2; a++)
                                        {
                                            if (eventIndices[a] < 0) continue;
                                            string prevAnim = parts[eventIndices[a]];
                                            int newAnim = anims != null && anims[a] > 0 ? anims[a] : -1;
                                            if (events.ParseArgSpec(prevAnim, out int animPos))
                                            {
                                                init[init.Offset + animPos] = newAnim;
                                            }
                                            else if (int.TryParse(prevAnim, out _))
                                            {
                                                events.ReplaceMacro(edits, prevAnim, newAnim.ToString());
                                            }
                                        }
                                        init.Save();
                                        e.Instructions[i] = init.Val;
                                        // Console.WriteLine($"{events.Parse(e.Instructions[i])}");
                                    }
                                }
                            }
                            // Conditional flags present in both the event and the target
                            if (entity > 0 && infos.ContainsKey(target))
                            {
                                void removeOrReplaceFlag(int templateFlag, int targetFlag)
                                {
                                    if (targetFlag == 0)
                                    {
                                        events.RemoveMacro(edits, templateFlag.ToString());
                                    }
                                    else
                                    {
                                        events.ReplaceMacro(edits, templateFlag.ToString(), targetFlag.ToString());
                                    }
                                }
                                EnemyInfo targetInfo = infos[target];
                                if (t.DefeatFlag > 0)
                                {
                                    if (targetInfo.DefeatFlag == 0 && targetInfo.IsImportantTarget)
                                    {
                                        throw new Exception($"{target} has no defeat flag defined, but was randomized to {entity} in {callee}");
                                    }
                                    removeOrReplaceFlag(t.DefeatFlag, targetInfo.DefeatFlag);
                                }
                                if (t.StartFlag == -1 && targetInfo.StartFlag != 0)
                                {
                                    // This is a feature to add a start flag to inference-heavy events
                                    events.AddMacro(edits, EditType.AddBefore, $"IF Event Flag (0,1,0,{targetInfo.StartFlag})");
                                }
                                if (t.StartFlag > 0)
                                {
                                    if (targetInfo.StartFlag == 0 && targetInfo.IsBossTarget)
                                    {
                                        throw new Exception($"{target} has no start flag defined, but was randomized to {entity} in {callee}");
                                    }
                                    removeOrReplaceFlag(t.StartFlag, targetInfo.StartFlag);
                                }
                                // This won't add appear flag is none was there previously. Do that if it becomes a problem
                                if (t.AppearFlag > 0)
                                {
                                    removeOrReplaceFlag(t.AppearFlag, targetInfo.AppearFlag);
                                }
                                if (t.MusicFlag != null)
                                {
                                    string[] parts = Regex.Split(t.MusicFlag, @"\s*->\s*");
                                    if (!int.TryParse(parts.Last(), out int musicFlag))
                                    {
                                        throw new Exception($"Internal error: badly formatted phase change flag {t.MusicFlag} in {callee}");
                                    }
                                    // Music flag is slightly complicated. It is either in the event or not, so it
                                    // can be added, removed, replaced, or left absent.
                                    if (parts.Length == 1)
                                    {
                                        // If a simple flag, it is already in the event.
                                        removeOrReplaceFlag(musicFlag, targetInfo.MusicFlag);
                                    }
                                    else if (targetInfo.MusicFlag > 0)
                                    {
                                        // Otherwise if present in target, we should add it here too.
                                        // The actual value isn't important tbh because it is always just replaced.
                                        events.AddMacro(
                                            edits, EditType.AddAfter, $"SetEventFlag({targetInfo.MusicFlag}, ON)", parts[0]);
                                    }
                                    // Finally, exclude from manual invocation later
                                    if (targetInfo.MusicFlag > 0)
                                    {
                                        usedMusicFlagTargets.Add(target);
                                    }
                                }
                                // Currently, just add progress flags for simultaneous fights
                                if (t.ProgressFlag != null && infos[target].HasTag("dupe"))
                                {
                                    foreach (string flagStr in t.ProgressFlag.Split(' '))
                                    {
                                        int flag = int.Parse(flagStr);
                                        if (!progressFlagCopies.TryGetValue(flag, out int newFlag))
                                        {
                                            progressFlagCopies[flag] = newFlag = NewID(writeable: true);
                                            if (flag == 12505828)
                                            {
                                                // This flag is a 5-wide event value flag
                                                writeBase += 5;
                                            }
                                        }
                                        // These are optional, since they may be arguments. TODO use optional edits for this
                                        reloc[flag] = newFlag;
                                    }
                                }
                            }
                            if (t.Camera != null)
                            {
                                if (t.Entity == 0) throw new Exception($"Internal error: Camera defined for {callee} but no owner entity");
                                List<int> cameras = phraseRe.Split(t.Camera).Select(c => int.Parse(c)).ToList();
                                if (entity == 0)
                                {
                                    // Moving camera from other entity to this one, a boss, at the start
                                    if (!t.Type.StartsWith("start")) throw new Exception($"Internal error: Non-start in-place camera for {callee}");
                                    int cameraSource = revMapping[t.Entity];
                                    if (startCameras.TryGetValue(cameraSource, out int startCamera))
                                    {
                                        foreach (int camera in cameras)
                                        {
                                            int targetCamera = TransplantParamSet(startCamera, cameraSource, t.Entity);
                                            events.ReplaceMacro(edits, $"Set Area CameraSetParam SubID ({camera} -> {targetCamera})");
                                        }
                                    }
                                    else
                                    {
                                        foreach (int camera in cameras)
                                        {
                                            events.ReplaceMacro(edits, $"Set Area CameraSetParam SubID ({camera} -> -1)");
                                        }
                                    }
                                }
                                else
                                {
                                    // Moving camera from this entity to the target
                                    // Also support wire sets here for Divine Dragon only currently
                                    if (infos[target].IsBossTarget)
                                    {
                                        foreach (int camera in cameras)
                                        {
                                            if (camera > 0)
                                            {
                                                int targetCamera = TransplantParamSet(camera, entity, target);
                                                events.ReplaceMacro(edits, $"Set Area CameraSetParam SubID ({camera} -> {targetCamera})");
                                            }
                                            else
                                            {
                                                int targetCamera = TransplantParamSet(-camera, entity, target, "WireSetParam");
                                                events.ReplaceMacro(edits, $"Set Wire Searchability ({-camera} -> {targetCamera})");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (int camera in cameras)
                                        {
                                            if (camera > 0) events.ReplaceMacro(edits, $"Set Area CameraSetParam SubID ({camera} -> -1)");
                                            else events.RemoveMacro(edits, $"Set Wire Searchability ({-camera})");
                                        }
                                    }
                                }
                            }
                            if (t.Add != null)
                            {
                                events.AddMacro(edits, t.Add);
                            }
                            if (t.Invincibility != 0)
                            {
                                // One more special type of processing, invincibility resets immortality so it should be added for immortal enemies
                                // This is only intended for loc events at present
                                int invTarget = t.Invincibility;
                                if (revMapping.TryGetValue(invTarget, out int invSource))
                                {
                                    if (game.Sekiro)
                                    {
                                        if (infos[invSource].IsImmortal)
                                        {
                                            events.AddMacro(edits, new List<EventAddCommand>
                                            {
                                                new EventAddCommand
                                                {
                                                    After = $"Set Character Invincibility ({invTarget},0)",
                                                    Cmd = $"Set Character Immortality ({invTarget},1)",
                                                },
                                            });
                                        }
                                    }
                                    else
                                    {
                                        if (infos[invSource].IsImmortal)
                                        {
                                            events.AddMacro(
                                                edits, EditType.AddAfter,
                                                $"SetCharacterImmortality({invTarget}, Enabled)",
                                                $"SetCharacterInvincibility({invTarget}, Disabled)");
                                        }
                                        foreach (int helperTarget in getHelperTargets(invTarget))
                                        {
                                            events.AddMacro(
                                                edits, EditType.AddAfter,
                                                $"SetCharacterInvincibility({helperTarget}, Enabled)",
                                                $"SetCharacterInvincibility({invTarget}, Enabled)");
                                            events.AddMacro(
                                                edits, EditType.AddAfter,
                                                $"SetCharacterInvincibility({helperTarget}, Disabled)",
                                                $"SetCharacterInvincibility({invTarget}, Disabled)");
                                            if (infos[revMapping[helperTarget]].IsImmortal)
                                            {
                                                events.AddMacro(
                                                    edits, EditType.AddAfter,
                                                    $"SetCharacterImmortality({helperTarget}, Enabled)",
                                                    $"SetCharacterInvincibility({invTarget}, Disabled)");
                                            }
                                        }
                                    }
                                }
                            }
                            if (t.Dupe?.Entity != null && t.Type == "locarg")
                            {
                                string[] parts = t.Dupe.Entity.Split(' ');
                                if (events.ParseArgSpec(parts[0], out int pos))
                                {
                                    int source = (int)init[init.Offset + pos];
                                    if (dupeMap.TryGetValue(source, out List<int> dupes))
                                    {
                                        init.Args.AddRange(dupes.Select(x => (object)x));
                                    }
                                    else
                                    {
                                        init.Args.AddRange(Enumerable.Repeat((object)source, dupeCount));
                                    }
                                    init.Modified = true;
                                    init.Save();
                                }
                            }

                            // Commit edits to e2
                            if (e2 != null)
                            {
                                // Hacky custom edit to e2 directly, as it's rewriting cond groups
                                if (t.Deathblow != 0 && entity != 0)
                                {
                                    List<EMEVD.Instruction> toAdd = new List<EMEVD.Instruction>();
                                    EnemyInfo targetInfo = infos[target];

                                    // The game is way too lenient for when it allows deathblows to happen
                                    // As a result, instead of only activating in the normal circumstance,
                                    // we may also want to end the event when the boss is dead, which we may only be able to detect during its death
                                    // Add this to OR13, and make the overall condition OR14

                                    // If the first phase of a boss fight, prevent it from activating once the phase is over
                                    if (targetInfo.Class == EnemyClass.Boss && targetInfo.OwnedBy != 0 && t.DefeatFlag != 0)
                                    {
                                        int startFlag = infos[targetInfo.OwnedBy].StartFlag;
                                        if (startFlag == 0) throw new Exception($"{targetInfo.OwnedBy} has no start flag");
                                        // For a phase 1 only boss, end if second phase already
                                        toAdd.Add(events.ParseAdd($"IF Event Flag(-13,1,0,{startFlag})"));
                                    }
                                    // Alternatively, if the character is dead. Check using dead/alive command for normal enemies, otherwise using boss command
                                    if (endConds.TryGetValue((t.Deathblow, 1), out List<EMEVD.Instruction> after) || endConds.TryGetValue((t.Deathblow, 0), out after))
                                    {
                                        toAdd.AddRange(events.RewriteCondGroup(after, new Dictionary<int, int>(), -13));
                                    }
                                    else
                                    {
                                        toAdd.Add(events.ParseAdd($"IF Character Dead/Alive (-13,{t.Deathblow},1,0,1)"));
                                    }
                                    toAdd.Add(events.ParseAdd($"IF Condition Group (-14,1,-13)"));
                                    toAdd.Add(events.ParseAdd($"IF Condition Group (0,1,-14)"));
                                    toAdd.Add(events.ParseAdd($"END IF Condition Group State (Compiled) (0,1,-13)"));

                                    bool found = false;
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j]);
                                        if (instr.Init) continue;
                                        // We used to use EzState Instruction Request for 10000, but these commands are now notated with the Deathblow field, so look for the condition group instead
                                        // TODO: check isshin still working.
                                        if ((instr.Name == "IF Condition Group" || instr.Name.StartsWith("IF Character Has Event Message")) && instr[0].ToString() == "0")
                                        {
                                            found = true;
                                            instr[0] = "-14";
                                            instr.Save();
                                            e2.Instructions.InsertRange(j + 1, toAdd);
                                            break;
                                        }
                                    }
                                    if (!found) throw new Exception($"Could not find condition group to apply deathblow rewrite in {callee}");
                                }

                                // Apply standard line-by-line edits, line-by-line. Plus a few misc custom ones
                                for (int j = 0; j < e2.Instructions.Count; j++)
                                {
                                    Instr instr = events.Parse(e2.Instructions[j]);
                                    // Randomized events shouldn't have initializations, although we could probably also ignore them
                                    // if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e2.ID}");
                                    // We are either dealing with a copy of the event or the original one. So all edits are in-place
                                    edits.ApplyEdits(instr, j);
                                    instr.Save();
                                    e2.Instructions[j] = instr.Val;
                                    // A few read-then-write in-place line edits.
                                    // These don't have to be here, but they would either need to scan the entire event
                                    // like Segment does or use a custom EventEdit script.
                                    if (game.Sekiro)
                                    {
                                        // Replace area checks with distance checks
                                        if (entity != 0 && instr.Name == "IF In/Outside Area" && distReplace.TryGetValue((int)instr[3], out int dist))
                                        {
                                            sbyte condGroup = (sbyte)instr[0];
                                            byte isInside = (byte)instr[1];
                                            // EMEVD.Instruction toAdd = new EMEVD.Instruction(3, 3, new List<object> { condGroup, dist > 0 ? instr[1] : 1 - instr[1], 10000, entity, (float)Math.Abs(dist), 1 });
                                            EMEVD.Instruction toAdd = events.ParseAdd($"IF Entity In/Outside Radius Of Entity ({condGroup}, {(dist > 0 ? isInside : 1 - isInside)}, 10000, {entity}, {(float)Math.Abs(dist)}, 1)");
                                            e2.Instructions[j] = toAdd;
                                        }
                                        if (t.Name == "entity" && (instr.Name == "Display Boss Health Bar" || instr.Name == "Display Miniboss Health Bar"))
                                        {
                                            int nameEntity = (int)instr[1];
                                            if (t.Type.Contains("chr"))
                                            {
                                                if (entity == nameEntity)
                                                {
                                                    instr[3] = GetCleverName((int)instr[3], entity, target);
                                                }
                                            }
                                            else if (revMapping.TryGetValue(t.Entity, out int nameSource))
                                            {
                                                // Allow entity to be 0 to substitute an arg. This is needed for Owl 1 (the only parameterized start event),
                                                // may not work in the general case.
                                                if (t.Entity == nameEntity || nameEntity == 0)
                                                {
                                                    instr[3] = GetCleverName((int)instr[3], nameSource, t.Entity);
                                                }
                                            }
                                            instr.Save();
                                            e2.Instructions[j] = instr.Val;
                                        }
                                        // A different healthbar change, for dupe events
                                        if (t.Dupe?.HealthBar != null && entity != 0 && (instr.Name == "Display Boss Health Bar" || instr.Name == "Display Miniboss Health Bar"))
                                        {
                                            instr[2] = (short)1;
                                            instr.Save();
                                            e2.Instructions[j] = instr.Val;
                                        }
                                    }
                                    else
                                    {
                                        // Replace area checks with distance checks
                                        if (entity != 0 && instr.Name == "IfInoutsideArea" && distReplace.TryGetValue((int)instr[3], out int dist))
                                        {
                                            sbyte condGroup = (sbyte)instr[0];
                                            byte isInside = (byte)instr[1];
                                            EMEVD.Instruction toAdd = events.ParseAdd($"IfEntityInoutsideRadiusOfEntity({condGroup}, {(dist > 0 ? isInside : 1 - isInside)}, 10000, {entity}, {(float)Math.Abs(dist)}, 1)");
                                            e2.Instructions[j] = toAdd;
                                        }
                                        if (t.Name != null && instr.Name == "DisplayBossHealthBar" && (sbyte)instr[0] == 1)
                                        {
                                            if (target == 0) throw new Exception($"Internal error: Name is chr-only in DS3");
                                            int nameEntity = (int)instr[1];
                                            int nameSource = int.Parse(t.Name);
                                            if (nameEntity == nameSource)
                                            {
                                                instr[3] = GetCleverName((int)instr[3], nameSource, target);
                                                instr.Save();
                                                e2.Instructions[j] = instr.Val;
                                            }
                                        }
                                    }
                                }
                                // Apply multi-line edits
                                events.ApplyAdds(edits, e2, pre);

                                // Dupe rewrite is handled by postprocessing
                                // TODO handle with start/end
                                if (t.Dupe != null && t.Type != "locarg" && entity == 0)
                                {
                                    // Searches can be scoped to specific entities
                                    bool restrict = false;
                                    Dictionary<object, List<object>> searches = new Dictionary<object, List<object>>();
                                    if (t.Dupe.Entity != null)
                                    {
                                        restrict = true;
                                        string[] parts = t.Dupe.Entity.Split(' ');
                                        if (int.TryParse(parts[0], out int dupeSource))
                                        {
                                            if (dupeMap.TryGetValue(dupeSource, out List<int> dupes))
                                            {
                                                searches[dupeSource] = dupes.Select(x => (object)x).ToList();
                                            }
                                        }
                                        else if (events.ParseArgSpec(parts[0], out int sourcePos) && events.ParseArgSpec(parts[1], out int targetPos))
                                        {
                                            searches[$"X{sourcePos * 4}_4"] = Enumerable.Range(0, dupeCount)
                                                .Select(x => (object)$"X{(targetPos + x) * 4}_4")
                                                .ToList();
                                        }
                                        else throw new Exception($"Badly formatted dupe entity in {callee}: {t.Dupe.Entity}");
                                    }
                                    // Condition replacements for AND/OR customization
                                    // For condition x, turn dupe uses into y, combine x and y into -z, and use -z at the end.
                                    // Map from line number to condition group redefinition
                                    Dictionary<int, int> rewriteDef = new Dictionary<int, int>();
                                    // Map from line number to condition group combination (x + y -> z)
                                    Dictionary<int, (int, int, int)> rewriteUse = new Dictionary<int, (int, int, int)>();
                                    if (t.Dupe.Condition != null)
                                    {
                                        int newCond = 11;
                                        foreach (string condStr in phraseRe.Split(t.Dupe.Condition))
                                        {
                                            // For the moment, condStr is a single value
                                            int cond = int.Parse(condStr);
                                            int condType = Math.Sign(cond);
                                            List<int> lines = events.FindCond(e2, condStr);
                                            // Only handle definition+usage, not single-line definitions, for now
                                            if (lines.Count < 2) throw new Exception($"Unsupported cond in {callee}: {condStr}");
                                            for (int j = 0; j < lines.Count; j++)
                                            {
                                                int line = lines[j];
                                                if (j == lines.Count - 1)
                                                {
                                                    rewriteUse[line] = (cond, newCond * condType, (newCond + 1) * -condType);
                                                }
                                                else
                                                {
                                                    rewriteDef[line] = newCond * condType;
                                                }
                                            }
                                            newCond += 2;
                                        }
                                    }

                                    List<int> addedLines = new List<int>();
                                    for (int j = e2.Instructions.Count - 1; j >= 0; j--)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j]);
                                        events.SetInstrParamArgs(instr, pre);
                                        Dictionary<int, List<object>> replaces = new Dictionary<int, List<object>>();
                                        for (int k = 0; k < instr.Args.Count; k++)
                                        {
                                            object val = instr.Args[k];
                                            if (searches.TryGetValue(val, out List<object> vals))
                                            {
                                                replaces[k] = vals;
                                            }
                                            else if (!restrict && val is int intVal
                                                && dupeMap.TryGetValue(intVal, out List<int> dupes))
                                            {
                                                replaces[k] = dupes.Select(x => (object)x).ToList();
                                            }
                                        }
                                        int instrCount = e2.Instructions.Count;
                                        if (replaces.Count > 0 || rewriteDef.ContainsKey(j))
                                        {
                                            for (int c = 0; c < dupeCount; c++)
                                            {
                                                // Automatic feature in boss fights: add slight delay between activations
                                                if (instr.Name == "Force Animation Playback" && ev.Template.Any(s => s.Type == "start"))
                                                {
                                                    // Non-looping
                                                    if ((byte)instr[2] == 0)
                                                    {
                                                        // e2.Instructions.Insert(j, events.ParseAdd($"WAIT Fixed Time (Seconds) (0.5)"));
                                                    }
                                                }
                                                foreach (KeyValuePair<int, List<object>> replace in replaces)
                                                {
                                                    instr[replace.Key] = replace.Value[c];
                                                }
                                                // We're adding these in reverse order, so... hacky skip-handling.
                                                // This seems to be used in 20005340 for falling off, so AND is appropriate
                                                if (instr.Name.StartsWith("SKIP IF"))
                                                {
                                                    instr[0] = (byte)instr[0] + 1;
                                                }
                                                if (instr.Name == "Display Boss Health Bar" || instr.Name == "Display Miniboss Health Bar")
                                                {
                                                    instr[2] = (short)(c + 1);
                                                    // Invalid name thing, for now
                                                    instr[3] = 100;
                                                    Console.WriteLine($"{instr} from {string.Join(", ", replaces.Select(r => $"{r.Key}={string.Join(",", r.Value)}"))}");
                                                }
                                                if (rewriteDef.TryGetValue(j, out int newCond))
                                                {
                                                    instr[0] = (sbyte)newCond;
                                                }
                                                // Console.WriteLine(instr);
                                                (EMEVD.Instruction ins, List<EMEVD.Parameter> ps) = events.ParseAddArg(instr.ToString());
                                                e2.Instructions.Insert(j, ins);
                                                pre.AddParameters(ins, ps);
                                            }
                                        }
                                        else
                                        {
                                            if (instr.Name.StartsWith("SKIP IF"))
                                            {
                                                int skip = (byte)instr[0];
                                                // Look at last n "original" instructions to see how many were duplicated
                                                int skipExtra = Enumerable.Reverse(addedLines).Take(skip).Sum();
                                                if (skipExtra > 0)
                                                {
                                                    // Okay may we should be making param Instrs editable
                                                    instr[0] = (byte)(skip + skipExtra);
                                                    // Console.WriteLine($"new skip, {skip}+{skipExtra}: {instr}");
                                                    (EMEVD.Instruction ins, List<EMEVD.Parameter> ps) = events.ParseAddArg(instr.ToString());
                                                    pre.AddParameters(ins, ps);
                                                    e2.Instructions[j] = ins;
                                                }
                                            }
                                            else if (rewriteUse.TryGetValue(j, out var val))
                                            {
                                                (int x, int y, int z) = val;
                                                instr[2] = (sbyte)z;
                                                instr.Save();
                                                e2.Instructions[j] = instr.Val;
                                                e2.Instructions.Insert(j, events.ParseAdd($"IF Condition Group ({z},1,{y})"));
                                                e2.Instructions.Insert(j, events.ParseAdd($"IF Condition Group ({z},1,{x})"));
                                            }
                                        }
                                        addedLines.Add(e2.Instructions.Count - instrCount);
                                    }
                                }
                                // start and end are handled by post-processing after edits
                                if (t.Type.StartsWith("start"))
                                {
                                    int source = revMapping[t.Entity];
                                    if (startCmds.TryGetValue(source, out List<EMEVD.Instruction> cmds))
                                    {
                                        // reloc is empty for non-chr events, so make an imitation of it here.
                                        // DS3 uses multichr start sections, which is handled by segments
                                        Dictionary<int, int> bossReplace = new Dictionary<int, int> { { source, t.Entity } };
                                        foreach (EMEVD.Instruction cmd in cmds)
                                        {
                                            Instr instr = events.Parse(events.CopyInstruction(cmd));
                                            events.RewriteInts(instr, bossReplace);
                                            instr.Save();
                                            e2.Instructions.Add(instr.Val);
                                        }
                                    }
                                }
                                if (t.Type.StartsWith("end") && (t.EndCond != null || t.EndCond2 != null))
                                {
                                    // Unlike most transformations, this goes from source to target
                                    int source = revMapping[t.Entity];
                                    Dictionary<int, int> bossReplace = new Dictionary<int, int> { { source, t.Entity } };

                                    void replaceCond(List<int> before, List<EMEVD.Instruction> after, bool addAfter = false)
                                    {
                                        if (after == null)
                                        {
                                            // If target doesn't use this condition, just remove it probably.
                                            if (!addAfter)
                                            {
                                                e2.Instructions = e2.Instructions.Where((_, pos) => !before.Contains(pos)).ToList();
                                            }
                                            return;
                                        }
                                        Instr instr = events.Parse(e2.Instructions[before.Last()]);
                                        sbyte targetCond = instr[0] is sbyte ai ? ai : throw new Exception($"Internal error: boss end condition first arg is not cond {source}->{t.Entity}: {instr}");
                                        // Replace commands starting at first one
                                        if (!addAfter)
                                        {
                                            e2.Instructions = e2.Instructions.Where((_, pos) => !before.Contains(pos)).ToList();
                                        }
                                        after = events.RewriteCondGroup(after, bossReplace, targetCond);
                                        e2.Instructions.InsertRange(addAfter ? before.Last() + 1 : before[0], after);
                                    }
                                    if (t.EndCond2 != null)
                                    {
                                        replaceCond(events.FindCond(e2, t.EndCond2), endConds.TryGetValue((source, 1), out List<EMEVD.Instruction> after) ? after : null);
                                    }
                                    if (t.EndCond != null)
                                    {
                                        if (!endConds.TryGetValue((source, 0), out List<EMEVD.Instruction> after))
                                        {
                                            if (infos[source].IsBossTarget) throw new Exception($"No primary end cond for {source}");
                                            after = new List<EMEVD.Instruction> { new EMEVD.Instruction(4, 0, new List<object> { (sbyte)0, source, (byte)1, (sbyte)0, 1.0f }) };
                                            // This is where we detect using this enemy as a boss, since all bosses have endconds, so that we can not set them to immortal later
                                            removeImmortality.Add(t.Entity);
                                        }
                                        List<int> before = events.FindCond(e2, t.EndCond);
                                        // Can use !endCond2s.Contains(t.Entity) to see if there is not an EndCond2 elsewhere
                                        if (false && !endCond2s.Contains(t.Entity) && endConds.TryGetValue((source, 1), out List<EMEVD.Instruction> after2))
                                        {
                                            // Experimental feature: If there is no spot to put EndCond2, put the two conds in a row. EndCond2 first, so it gets put after
                                            replaceCond(before, after2, addAfter: true);
                                            replaceCond(before, after);
                                        }
                                        else
                                        {
                                            // Otherwise, replace both as normal
                                            replaceCond(before, after);
                                        }
                                    }
                                }
                                pre.Postprocess();
                            }  // If e2 defined
                            if (edits.PendingEdits.Count != 0)
                            {
                                throw new Exception($"{callee} has unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                            }
                            if (reloc.Count > 0)
                            {
                                events.RewriteInts(init, reloc);
                                if (dupeReloc != null) events.RewriteInts(init, dupeReloc);
                                init.Save();
                                if (e2 != null)
                                {
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j]);
                                        // if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e.ID}");
                                        events.RewriteInts(instr, reloc);
                                        if (t.ProgressFlag == "11115900") Console.WriteLine($"made {instr}");
                                        if (dupeReloc != null) events.RewriteInts(instr, dupeReloc);
                                        instr.Save();
                                    }
                                }
                            }
                            if (e2 != null && e2.ID != callee)
                            {
                                // New event, so add to its target map and also add new initialization
                                string ownMap;
                                if (t.Type == "copy")
                                {
                                    ownMap = entry.Key;
                                }
                                else if (!ownerMap.TryGetValue(target, out ownMap))
                                {
                                    throw new Exception($"Can't add initialization for {entity}->{target}, it has no owner map");
                                }
                                AddMulti(newInitializations, ownMap, (init.Val, e2));
                            }
                            // Some utility code for finding obvious bugs
                            // TODO: If unique enemies become non-unique, warn about team types and about health bar display
                            if (warnBadCommands && e2 != null)
                            {
                                for (int j = 0; j < e2.Instructions.Count; j++)
                                {
                                    Instr instr = events.Parse(e2.Instructions[j]);
                                    bool warn = false;
                                    if (entity == 0)
                                    {
                                        if (instr.Name == "Force Animation Playback" || instr.Name == "Request Character AI Command" || instr.Name == "Set Character AI ID" || instr.Name == "IF Character Has Event Message")
                                        {
                                            warn = true;
                                        }
                                    }
                                    else
                                    {
                                        // TODO: Also for reading event flags? But exclude start/defeat/appear flags
                                        if (instr.Name.Contains("Set") && instr.Name.Contains("Event Flag") && instr[0] is int flag && (flag == 0 ? !Events.IsTemp(callee) : !Events.IsTemp(flag)))
                                        {
                                            warn = true;
                                        }
                                    }
                                    if (warn) Console.WriteLine($"Warning: {callee} has {instr}");
                                }
                            }
                        }  // For each template

                        if ((allChr && canRemove) || forceRemove)
                        {
                            e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                            removedEvents.Add(callee);
                        }
                        else
                        {
                            usedEvents.Add(callee);
                        }
                    }  // For each initialization in caller event
                    initOld.Postprocess();
                }
            }

            // Add common functions
            Dictionary<string, NewEvent> customEvents = new Dictionary<string, NewEvent>();
            foreach (NewEvent e in eventConfig.NewEvents)
            {
                if (e.Commands == null)
                {
                    EMEVD.Event common = game.Emevds["common_func"].Events.Find(c => c.ID == c.ID);
                    if (common == null) throw new Exception($"Error: event {e.Name} #{e.ID} missing from common_func");
                    customEvents[e.Name] = e;
                    continue;
                }
                List<EMEVD.Parameter> ps = new List<EMEVD.Parameter>();
                EMEVD.Event ev = new EMEVD.Event(e.ID, EMEVD.Event.RestBehaviorType.Default);
                for (int i = 0; i < e.Commands.Count; i++)
                {
                    (EMEVD.Instruction instr, List<EMEVD.Parameter> newPs) = events.ParseAddArg(e.Commands[i], i);
                    ev.Instructions.Add(instr);
                    ev.Parameters.AddRange(newPs);
                }
                if (e.Name == null)
                {
                    EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 });
                    AddMulti(newInitializations, "common", (init, ev));
                }
                else
                {
                    customEvents[e.Name] = e;
                    AddMulti(newInitializations, "common_func", (null, ev));
                }
            }

            void addCommonFuncInit(string name, int target, List<object> args)
            {
                EMEVD.Instruction init = new EMEVD.Instruction(2000, 6, new List<object> { customEvents[name].ID }.Concat(args));
                AddMulti(newInitializations, ownerMap[target], (init, null));
            }
            Dictionary<int, int> targetSourceNPCs = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> transfer in revMapping)
            {
                int target = transfer.Key;
                int source = transfer.Value;
                EnemyInfo sourceInfo = infos[source];
                // In Sekiro make all immortal boss targets immortal by default (managed by individual event scripts in DS3)
                if (game.Sekiro)
                {
                    if (sourceInfo.IsImmortal)
                    {
                        addCommonFuncInit("immortal", target, new List<object> { target });
                        // Immortal, except when they die in the spot of a non-boss, make sure that goes through
                        // This depends on endConds, so it would require dynamically adding custom common_funcs
                        // in order to use common_func (we'd need to allocate a range of them).
                        if (infos[target].Class != EnemyClass.Boss)
                        {
                            if (endConds.TryGetValue((source, 1), out List<EMEVD.Instruction> after) || endConds.TryGetValue((source, 0), out after))
                            {
                                EMEVD.Event ev = new EMEVD.Event(NewID(), EMEVD.Event.RestBehaviorType.Restart);
                                ev.Instructions.AddRange(events.RewriteCondGroup(after, new Dictionary<int, int> { { source, target } }, 0));
                                // ev.Instructions.Add(events.ParseAdd($"Award Item Lot (60220)"));
                                ev.Instructions.Add(events.ParseAdd($"WAIT Fixed Time (Seconds) (5)"));
                                ev.Instructions.Add(events.ParseAdd($"IF Character Dead/Alive (0,{target},0,0,1)"));
                                ev.Instructions.Add(events.ParseAdd($"Force Character Death ({target},1)"));
                                // Unfortunately, it seems like Fire Isshin can never actually die
                                if (source == 1110920) ev.Instructions.Add(events.ParseAdd($"Change Character Enable State ({target},0)"));
                                EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 });
                                AddMulti(newInitializations, ownerMap[target], (init, ev));
                            }
                        }
                    }
                    // Make non-tree Divine Dragon easily killable, or other one hard to kill
                    if (source == 2500800)
                    {
                        if (enableMultichr(source, target))
                        {
                            addCommonFuncInit("realdivinedragon", target, new List<object> { target });
                        }
                        else
                        {
                            addCommonFuncInit("fakedivinedragon", target, new List<object> { target });
                        }
                    }
                }
                else if (game.DS3)
                {
                    if (target == 3100800)
                    {
                        foreach (int helperTarget in getHelperTargets(target))
                        {
                            addCommonFuncInit("greatwoodfall", target, new List<object> { helperTarget });
                        }
                    }
                    if (infos.TryGetValue(target, out EnemyInfo musicInfo)
                        && musicInfo.MusicFlag > 0 && !usedMusicFlagTargets.Contains(target))
                    {
                        addCommonFuncInit("musicflag", target, new List<object> { target, musicInfo.MusicFlag });
                    }
                }
                int baseTarget = target;
                if (!infos.ContainsKey(target) && sourceInfo.OwnedBy > 0)
                {
                    // Helper mapping is from owner target and source helper to target helper
                    // TODO... can this be faster
                    int ownerTarget = helperMapping.Where(e => e.Key.Item2 == source && e.Value == target).Select(e => e.Key.Item1).FirstOrDefault();
                    if (ownerTarget > 0)
                    {
                        baseTarget = ownerTarget;
                    }
                }
                if (opt["scale"])
                {
                    // Assign scaling speffects on best-effort basis
                    int sourceSection = ann.ScalingSections.TryGetValue(source, out int s) ? s : -1;
                    int targetSection = ann.ScalingSections.TryGetValue(baseTarget, out s) ? s : -1;
                    if (sourceSection > 0 && targetSection > 0 && scalingSpEffects.TryGetValue((sourceSection, targetSection), out (int, int) sp))
                    {
                        bool fixedXp = infos.TryGetValue(target, out EnemyInfo e) && e.IsBossTarget;
                        addCommonFuncInit("scale", target, new List<object> { target, fixedXp ? sp.Item1 : sp.Item2 });
                        // Additional scaling for Gundyr and First Lizard, because no upgrades of any kind
                        if (infos[baseTarget].HasTag("earlyscale") && !sourceInfo.HasTag("early") && scalingSpEffects.TryGetValue((1, 0), out sp))
                        {
                            addCommonFuncInit("scale", target, new List<object> { target, sp.Item1 });
                        }
                    }
                    else if (!(sourceSection > 0 && sourceSection == targetSection))
                    {
#if DEBUG
                        Console.WriteLine($"Warning: scaling speffect not found for {ename(source)} in {target}, sections {sourceSection}->{targetSection}");
#endif
                    }
                }
                if (defaultData.TryGetValue(baseTarget, out EnemyData val))
                {
                    targetSourceNPCs[target] = val.NPC;
                }
            }
            foreach (EnemyData data in defaultData.Values)
            {
                if (!revMapping.ContainsKey(data.ID) && infos.ContainsKey(data.ID))
                {
                    targetSourceNPCs[data.ID] = data.NPC;
                }
            }
            // NPC effects to move around, or re-apply to vanilla enemies
            foreach (KeyValuePair<int, int> entry in targetSourceNPCs)
            {
                int target = entry.Key;
                int npc = entry.Value;
                EnemyInfo sourceInfo = revMapping.TryGetValue(target, out int source) ? infos[source] : infos[target];
                // Scale is just speffect application, so it also works for e.g. poison resistance
                if (npcCopySpEffect.TryGetValue(npc, out List<int> sps))
                {
                    // We could also be precious about this and skip speffects already defined in source NPC
                    foreach (int sp in sps)
                    {
                        addCommonFuncInit("scale", target, new List<object> { target, sp });
                    }
                }
                // Add infighting if the original enemy had it, or the original owner had it, and not overpowered
                if (npcOriginalTeam.TryGetValue(npc, out byte team) && !sourceInfo.HasTag("ultra"))
                {
                    addCommonFuncInit("teamtype", target, new List<object> { target, team });
                }
                // Probably only allow base targets to drop items. lot is (item lot, event flag)
                // This is based on the target's original NPC id, although in the case of chest->mimic, the
                // target's NPC id is a lie and its lot really belongs to a different mimic. So exclude that.
                if (npcItemLots.TryGetValue(npc, out (int, int) lot)
                    && sourceInfo.Class != EnemyClass.Helper
                    && !newMimics.ContainsKey(target))
                {
                    addCommonFuncInit("enemydrop", target, new List<object> { target, lot.Item1, lot.Item2 });
                }
            }
            foreach (KeyValuePair<int, ChestInfo> entry in newMimics)
            {
                int target = entry.Key;
                ChestInfo chest = entry.Value;
                addCommonFuncInit("enemydrop", target, new List<object> { target, chest.ItemLot, chest.EventFlag });
                addCommonFuncInit("showmimic", target, new List<object> { chest.EventFlag, target, chest.EventFlag });
            }
            if (opt["yhormruler"] && mapping.TryGetValue(3900800, out List<int> yhormTargets))
            {
                HashSet<string> yhormMaps = new HashSet<string>();
                foreach (int yhorm in yhormTargets)
                {
                    if (!yhormMaps.Add(ownerMap[yhorm])) continue;
                    // This could also be in common, but we want to conditionally initialize it
                    addCommonFuncInit("yhormruler", yhorm, new List<object> { });
                }
            }

            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                List<(EMEVD.Instruction, EMEVD.Event)> newEvents = newInitializations[entry.Key];
                EMEVD emevd = entry.Value;
                // Remove unused events.
                // For events with remove, these are removed always (based only on template definition)
                // For loc events, these are not removed (canRemove false, added to usedEvents)
                // For chr events with any nonrandomized usages, these are not removed (not in revMapping, canRemove false)
                // For chr events with multiple randomized usages in a file, they should be copied, so it's fine to remove the original (canRemove true)
                // For chr events in common, they are not copied, so should not be removed. So this common_func exclusion is necessary.
                if (entry.Key != "common_func")
                {
                    emevd.Events.RemoveAll(e => removedEvents.Contains((int)e.ID) && !usedEvents.Contains((int)e.ID));
                }
                emevd.Events.AddRange(newEvents.Select(n => n.Item2).Where(e => e != null));
                foreach (EMEVD.Event e in emevd.Events)
                {
                    int id = (int)e.ID;
                    if (newEvents.Count > 0 && e.ID == 0)
                    {
                        foreach (EMEVD.Instruction newEvent in newEvents.Select(n => n.Item1).Where(i => i != null))
                        {
                            e.Instructions.Add(newEvent);
                        }
                    }
                    if (opt["openstart"] && entry.Key == "common" && e.ID == 0)
                    {
                        // Open Senpou door (set objact event flag)
                        emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2003, 2, new List<object> { 62000503, (byte)1 }));
                    }
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr instr = events.Parse(e.Instructions[i]);
                        if (instr.Init) continue;
                        if (instr.Name == "Set Character Immortality" && instr[0] is int imm)
                        {
                            bool shouldRemove = false;
                            // TODO: DS3 probably needs a system like this (and it uses a lot more parameterization). As of now this does nothing, though
                            if (removeImmortality.Contains(imm))
                            {
                                shouldRemove = true;
                            }
                            // For some reason, one of Owl's start events is parameterized.
                            // We could find out what value this parameter has from its initialization, but in this case it works to use start template as a proxy.
                            else if (imm == 0 && templates.TryGetValue(id, out EventSpec spec) && spec.Template.Any(t => t.Type.StartsWith("start") && removeImmortality.Contains(t.Entity)))
                            {
                                shouldRemove = true;
                            }
                            if (shouldRemove)
                            {
                                EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                                e.Instructions[i] = newInstr;
                                // Just in case...
                                e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                            }
                        }
                    }
                }
            }

            // Misc post-randomization edits
            if (game.Sekiro)
            {
                // An edit for roberto
                ESD warrior = game.Talk["m20_00_00_00"]["t200500"];
                List<ESD.State> mainloop = warrior.StateGroups[0x7FFFFFFF - 5].Values.ToList();
                foreach (ESD.State state in mainloop)
                {
                    foreach (ESD.Condition cond in state.Conditions)
                    {
                        byte[] c = cond.Evaluator;
                        // Remove "check self death" condition, replace with false
                        if (c != null && c.Length >= 2 && c[0] == 0x43 && c[1] == 0x84)
                        {
                            c[0] = 0x40;
                            c[1] = 0xa6;
                        }
                    }
                }

                // Other param edits
                // Most minibosses in earlier spots are manageable, but, Sakura Bull is a bit too tanky. Bring it down to Blazing Bull level if scaling is not otherwise enabled
                if (!opt["scale"] && mapping.TryGetValue(2500570, out List<int> sakuraTarget) && infos[sakuraTarget[0]].HasTag("early"))
                {
                    Params["NpcParam"][13800000]["Hp"].Value = 2232;
                }

                if (false && revMapping.TryGetValue(1500800, out int newMonk) && totalTargetCounts.TryGetValue(newMonk, out int monkAmount) && monkAmount == 1)
                {
                    // Can look into adding purple speffect/material for corrupted monk, if we can find it
                }

                // Make Sword Saint Isshin deathblow not require Mortal Blade
                Params["ThrowKindParam"].Rows.Add(new PARAM.Row(250001, null, Params["ThrowKindParam"].AppliedParamdef));
                GameEditor.CopyRow(Params["ThrowKindParam"][250000], Params["ThrowKindParam"][250001]);
                Params["ThrowKindParam"][250001]["Mask4"].Value = (byte)0xFF;
                Params["ThrowParam"][15400590]["ThrowKindParamID0"].Value = 250001;
                // And also Divine Dragon
                Params["ThrowParam"][15200090]["ThrowKindParamID0"].Value = 250001;
                if (mapping.ContainsKey(2500800))
                {
                    Params["ThrowParam"][15200090]["Dist"].Value = (float)16;
                    Params["ThrowParam"][15200090]["UpperYrange"].Value = (float)20;
                    Params["ThrowParam"][15200090]["LowerYrange"].Value = (float)20;
                    // Plus remove Divine Dragon blowback behavior as it easily sends the player straight out of bounds, if dragon is moved anywhere
                    Params["Bullet"].Rows.RemoveAll(r => r.ID == 52000830);
                }
            }
            else
            {
                if (mapping.ContainsKey(3200800))
                {
                    // Nerf Wyvern foot slams, the hitboxes are absolutely horrid
                    HashSet<int> removeAttacks = new HashSet<int>
                    {
                        // 3.5 radius foot blast
                        // 3140131, 3140141, 3140151,
                        // 0.5 second last radius (>2) blast aftershocks from bullets
                        3140750, 3140751, 3140752, 3140755, 3140756, 3140757,
                    };
                    Params["AtkParam_Npc"].Rows.RemoveAll(r => removeAttacks.Contains(r.ID));
                }

                // Add Wolnir light SFX for Wolnir replacements
                PARAM.Row lightVfx = game.AddRow("SpEffectVfxParam", 50089);
                GameEditor.CopyRow(Params["SpEffectVfxParam"][50069], lightVfx);
                // Lockon point: 220. In front of: 184
                lightVfx["midstDmyId"].Value = (short)220;
                PARAM.Row lightSp = game.AddRow("SpEffectParam", 10509);
                GameEditor.CopyRow(Params["SpEffectParam"][10506], lightSp);
                lightSp["EffectVfx0"].Value = lightVfx.ID;

                // In case Giant AI breaks and crawls up, don't do 90% damage cut (12340)
                // Also, don't allow crab to heal (12044) ;_;
                Params["SpEffectParam"].Rows.RemoveAll(row => row.ID == 12340 || row.ID == 12044);

                // Make same Hollow Manservant edit as Fog Gate Randomizer
                PARAM.Row hollow = Params["ActionButtonParam"][3100000];
                hollow["regionType"].Value = (byte)0;
                hollow["Radius"].Value = 5f;
                hollow["Angle"].Value = 180;
                hollow["depth"].Value = 0f;
                hollow["width"].Value = 0f;
                hollow["height"].Value = 15f;
                hollow["baseHeightOffset"].Value = -10f;
                hollow["dummyPoly1"].Value = -1;
                hollow["angleCheckType"].Value = (byte)0;
                hollow["AllowAngle"].Value = 180;

                // Use 5020 (logic speffects) as a base for various set/check statuses, at 6950.
                // This can go until 6980 but cap it until 6969.
                PARAM.Row baseSp = Params["SpEffectParam"][5020];
                for (int i = 0; i < 20; i++)
                {
                    GameEditor.CopyRow(baseSp, game.AddRow("SpEffectParam", 6950 + i));
                }
            }

            // Return item info
            Dictionary<string, List<string>> named = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<int, List<int>> entry in mapping)
            {
                string sourceName = infos[entry.Key].ItemName;
                foreach (int target in entry.Value)
                {
                    // Can happen with duplicated helpers
                    if (!infos.ContainsKey(target)) continue;
                    string targetName = infos[target].ItemName;
                    if (sourceName != null && targetName != null)
                    {
                        AddMulti(named, sourceName, targetName);
                    }
                }
            }
            return new EnemyLocations { Target = named };
        }

        private static readonly Regex phraseRe = new Regex(@"\s*;\s*");

        public class BossPhaseLimit
        {
            public int Entity { get; set; }
            public float Amount { get; set; }
        }

        public class RegionTarget
        {
            public int Distance { get; set; }
            public int Region { get; set; }
            public static RegionTarget ID(int id) => new RegionTarget { Region = id };
            public static RegionTarget Dist(int id) => new RegionTarget { Distance = id };
        }

        public class RandomSources
        {
            public static RandomSources Create(Random random, List<PoolAssignment> pools, List<int> defaultPool, int estimatedTargetCount)
            {
                if (pools == null) return null;
                RandomSources ret = new RandomSources();
                foreach (PoolAssignment original in pools)
                {
                    PoolAssignment pool = original.Copy();
                    if (pool.Weight <= 0) continue;
                    if (pool.Pool.ToLowerInvariant() == "default")
                    {
                        pool.PoolGroups = new List<List<int>> { defaultPool.ToList() };
                    }
                    if (pool.PoolGroups == null || pool.PoolGroups.Count == 0)
                    {
                        continue;
                    }
                    if (!pool.RandomByType)
                    {
                        pool.PoolGroups = new List<List<int>> { pool.PoolGroups.SelectMany(g => g).ToList() };
                    }
                    Shuffle(random, pool.PoolGroups);
                    foreach (List<int> group in pool.PoolGroups)
                    {
                        Shuffle(random, group);
                    }
                    ret.Pools.Add(pool);
                    ret.GroupIndices.Add(0);
                    ret.Indices.Add(pool.PoolGroups.Select(g => 0).ToList());
                    ret.Total += pool.Weight;
                }
                if (ret.Pools.Count == 0) return null;

                // Also create a fixed order to iterate through the pools that can loop.
                // Use the target count to try to make sure it roughly keeps a good proportion.
                if (ret.Pools.Count == 1)
                {
                    ret.PoolOrder.Add(0);
                }
                else
                {
                    for (int i = 0; i < ret.Pools.Count; i++)
                    {
                        PoolAssignment pool = ret.Pools[i];
                        if (pool.Weight > 0)
                        {
                            int size = estimatedTargetCount * pool.Weight / ret.Total;
                            if (size == 0)
                            {
                                // If very small % chance, calculate now whether it will be included
                                double odds = (double)pool.Weight / ret.Total;
                                if (random.NextDouble() >= Math.Pow(1 - odds, estimatedTargetCount)) size = 1;
                            }
                            if (size > 0)
                            {
                                ret.PoolOrder.AddRange(Enumerable.Repeat(i, size));
                            }
                        }
                    }
                    Shuffle(random, ret.PoolOrder);
                }
                return ret;
            }

            public int Next(Dictionary<int, bool> singletons)
            {
                int pool = PoolOrder.Count > 1 ? PoolOrder[PoolIndex] : 0;
                int ret = 0;
                int maxTries = 20;
                int tries;
                for (tries = 0; tries < maxTries; tries++)
                {
                    int group = GroupIndices[pool];
                    int chosen = Indices[pool][group];
                    ret = Pools[pool].PoolGroups[group][chosen];

                    Indices[pool][group] = (Indices[pool][group] + 1) % Pools[pool].PoolGroups[group].Count;
                    GroupIndices[pool] = (GroupIndices[pool] + 1) % Pools[pool].PoolGroups.Count;

                    if (singletons.ContainsKey(ret))
                    {
                        if (singletons[ret])
                        {
                            continue;
                        }
                        else
                        {
                            singletons[ret] = true;
                        }
                    }
                    break;
                }
                if (maxTries == 10) throw new Exception($"Can't select enemy: keep getting enemies like {ret} which can only appear once in the game");

                if (PoolOrder.Count > 1) PoolIndex = (PoolIndex + 1) % PoolOrder.Count;
                return ret;
            }

            public List<PoolAssignment> Pools = new List<PoolAssignment>();
            public List<int> PoolOrder = new List<int>();
            public int PoolIndex = 0;
            public List<int> GroupIndices = new List<int>();
            public List<List<int>> Indices = new List<List<int>>();
            public int Total { get; set; }
        }

        public class EnemyPermutation
        {
            public EnemyClass Type { get; set; }
            // For now, entity IDs are unique and not collapsed (e.g. if two enemies are the same in different positions).
            public List<int> Sources = new List<int>();
            public List<int> Targets = new List<int>();
            // Mapping from target to source
            public Dictionary<int, int> Mapping = new Dictionary<int, int>();
        }

        public Dictionary<string, string> GetFieldMap<T>(T source)
        {
            Dictionary<string, string> m = new Dictionary<string, string>();
            var type = typeof(T);
            foreach (System.Reflection.PropertyInfo sourceProperty in type.GetProperties())
            {
                System.Reflection.PropertyInfo targetProperty = type.GetProperty(sourceProperty.Name);
                if (sourceProperty.PropertyType.IsArray)
                {
                    // Ignore
                }
                else if (sourceProperty.CanWrite)
                {
                    m[sourceProperty.Name] = $"{sourceProperty.GetValue(source, null)}";
                }
            }
            return m;
        }
    }
}
