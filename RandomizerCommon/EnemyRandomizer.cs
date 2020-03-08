using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static RandomizerCommon.Events;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsFormats.EMEVD.Instruction;

namespace RandomizerCommon
{
    public class EnemyRandomizer
    {
        private GameData game;
        private Events events;
        public EnemyRandomizer(GameData game, Events events)
        {
            this.game = game;
            this.events = events;
        }
        public EnemyLocations Run(RandomizerOptions opt)
        {
            Dictionary<string, MSBS> maps = game.Smaps;

            // Collect all ids first
            HashSet<int> ids = new HashSet<int>();
            Dictionary<int, string> regionIds = new Dictionary<int, string>();
            Dictionary<int, List<int>> groupIds = new Dictionary<int, List<int>>();
            Dictionary<int, (MSBS.Event.Generator, int)> generators = new Dictionary<int, (MSBS.Event.Generator, int)>();
            foreach (KeyValuePair<string, MSBS> entry in maps)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string map = SekiroLocationDataScraper.locations[entry.Key];
                MSBS msb = entry.Value;

                foreach (MSBS.Part.Enemy e in msb.Parts.Enemies)
                {
                    ids.Add(e.EntityID);
                    foreach (int id in e.EntityGroupIDs)
                    {
                        if (id > 0)
                        {
                            AddMulti(groupIds, id, e.EntityID);
                            ids.Add(id);
                        }
                    }
                }
                foreach (MSBS.Region r in msb.Regions.GetEntries())
                {
                    if (r.EntityID < 1000000) continue;
                    regionIds[r.EntityID] = r.Name;
                    ids.Add(r.EntityID);
                }
                foreach (MSBS.Event.Generator gen in msb.Events.Generators)
                {
                    MSBS.Part.Enemy e = msb.Parts.Enemies.Find(p => p.Name == gen.SpawnPartNames[0]);
                    generators[gen.EntityID] = (gen, e == null ? -1 : e.EntityID);
                }
            }

            IDeserializer deserializer = new DeserializerBuilder().Build();
            EnemyAnnotations ann;
            using (var reader = File.OpenText("dists/Base/enemy.txt"))
            {
                ann = deserializer.Deserialize<EnemyAnnotations>(reader);
            }
            Dictionary<int, EnemyInfo> infos = new Dictionary<int, EnemyInfo>();
            Dictionary<int, List<int>> owners = new Dictionary<int, List<int>>();
            foreach (EnemyInfo info in ann.Enemies)
            {
                infos[info.ID] = info;
                if (info.OwnedBy > 0)
                {
                    AddMulti(owners, info.OwnedBy, info.ID);
                }
            }

            Dictionary<int, EnemyData> defaultData = new Dictionary<int, EnemyData>();
            foreach (KeyValuePair<string, MSBS> entry in maps)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string map = SekiroLocationDataScraper.locations[entry.Key];
                MSBS msb = entry.Value;

                foreach (MSBS.Part.Enemy e in msb.Parts.Enemies)
                {
                    defaultData[e.EntityID] = new EnemyData
                    {
                        Map = entry.Key,
                        Group = e.EntityGroupIDs.Where(g => g > 0).ToList(),
                        Name = e.Name,
                        Model = e.ModelName,
                        NPC = e.NPCParamID,
                        Think = e.ThinkParamID,
                    };
                }
            }


            // Process config
            HashSet<string> norandom = new HashSet<string> { };
            bool isRandom(EnemyInfo info, string model)
            {
                if (norandom.Count == 0) return true;
                if ((info.Class == EnemyClass.Basic || info.ExtraName == null) && norandom.Contains(model)) return false;
                if (info.ExtraName != null && norandom.Contains(info.ExtraName)) return false;
                if (norandom.Contains(info.ID.ToString())) return false;
                return true;
            }
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (!defaultData.TryGetValue(info.ID, out EnemyData data)) throw new Exception($"Entity {info.ID} does not exist in map; cannot randomize it");
                if (opt["headlessmove"] && info.Class == EnemyClass.Headless)
                {
                    info.Class = EnemyClass.Miniboss;
                }
                string model = game.ModelName(data.Model);
                if (!isRandom(info, model))
                {
                    // TODO: Verify this works against actual config
                    info.Class = EnemyClass.None;
                }
                if (info.OwnedBy != 0)
                {
                    if (!infos.TryGetValue(info.OwnedBy, out EnemyInfo main)) throw new Exception($"Entity {info.ID} referencing {info.OwnedBy} which does not exist in config");
                    if (info.Class == EnemyClass.Boss)
                    {
                        // Bosses inherit most attributes
                        info.DefeatFlag = main.DefeatFlag;
                        info.AppearFlag = main.AppearFlag;
                        info.Arena = main.Arena;
                        info.Tags = main.Tags;
                        info.ItemName = main.ItemName;
                    }
                }
                if (info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.Boss)
                {
                    if (info.Arena == null) throw new Exception($"Entity {info.ID} has no arena defined");
                    info.ArenaData = Arena.Parse(info.Arena);
                    if (info.OwlArena != null)
                    {
                        info.OwlArenaData = Arena.Parse(info.OwlArena);
                    }
                }
                if (info.Class == EnemyClass.Boss)
                {
                    if (info.Phases == 0) throw new Exception($"No phase count defined for boss {info.ID}");
                    if (!(info.HasTag("early") || info.HasTag("mid") || info.HasTag("late"))) throw new Exception($"No difficulty defined for boss {info.ID}");
                }
                if (info.Class == EnemyClass.Miniboss && info.DefeatFlag == 0)
                {
                    info.DefeatFlag = 10000000 + info.ID;
                }
            }

            // Read some emevds
            Dictionary<string, EMEVD> emevds = game.Emevds;

            // Producing event config
            if (opt["events"])
            {
                SortedDictionary<int, string> flagItems = new SortedDictionary<int, string>();
                Dictionary<int, string> treasureNames = new Dictionary<int, string>();
                if (opt["eventsitem"])
                {
                    LocationData tempData = new SekiroLocationDataScraper().FindItems(game);
                    foreach (KeyValuePair<ItemKey, ItemLocations> item in tempData.Data)
                    {
                        foreach (ItemLocation loc in item.Value.Locations.Values)
                        {
                            if (loc.Scope.Type == ItemScope.ScopeType.EVENT)
                            {
                                int eventFlag = loc.Scope.ID;
                                if (true || eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022)
                                {
                                    flagItems[eventFlag] = game.Name(item.Key);
                                    ids.Add(eventFlag);
                                }
                                if (eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022)
                                {
                                    Console.WriteLine($"{eventFlag}: {game.Name(item.Key)}");
                                }
                            }
                        }
                    }
                    HashSet<string> treasureModels = new HashSet<string> { "o000100", "o000101", "o005300", "o005390", "o005400", "o255300" };
                    foreach (KeyValuePair<string, MSBS> entry in maps)
                    {
                        if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                        string map = SekiroLocationDataScraper.locations[entry.Key];
                        MSBS msb = entry.Value;

                        foreach (MSBS.Part.Object e in msb.Parts.Objects)
                        {
                            if (!treasureModels.Contains(e.ModelName) || e.EntityID <= 0) continue;
                            treasureNames[e.EntityID] = $"{e.Name} - {game.ModelName(e.ModelName)}";
                            ids.Add(e.EntityID);
                        }
                    }
                }
                SortedDictionary<int, EventDebug> eventInfos = events.GetHighlightedEvents(emevds, ids);
                string quickId(int id)
                {
                    if (regionIds.TryGetValue(id, out string region))
                    {
                        return $"region {id} ({region})";
                    }
                    if (groupIds.ContainsKey(id))
                    {
                        return $"group {id} [{string.Join(", ", groupIds[id].Select(i => quickId(i)))}]";
                    }
                    if (flagItems.TryGetValue(id, out string item))
                    {
                        return $"flag {id} ({item})";
                    }
                    if (treasureNames.TryGetValue(id, out string tr))
                    {
                        return $"treasure {id} ({tr})";
                    }
                    if (!defaultData.ContainsKey(id)) return $"{id} unknown";
                    return $"{id} ({defaultData[id].Name} - {game.ModelName(defaultData[id].Model)}"
                        + $"{(infos.TryGetValue(id, out EnemyInfo enemy) && enemy.Class == 0 ? " - not random" : "")})";
                }
                bool isEligible(int entityId)
                {
                    if (opt["eventsitem"])
                    {
                        return flagItems.ContainsKey(entityId) || treasureNames.ContainsKey(entityId);
                    }
                    else
                    {
                        List<int> entityIds = groupIds.TryGetValue(entityId, out List<int> gids) ? gids : new List<int> { entityId };
                        return entityIds.Any(id => !infos.TryGetValue(id, out EnemyInfo enemy) || enemy.Class != 0);
                    }
                }
                events.WriteEventConfig(opt["eventsyaml"] ? "newevents.txt" : null, eventInfos, isEligible, quickId, !opt["eventsitem"]);
                return null;
            }

            Dictionary<string, PARAM> Params = game.Params;

            if (opt["print"])
            {
                EnemyAnnotations outYaml = new EnemyAnnotations();
                foreach (KeyValuePair<string, MSBS> entry in maps)
                {
                    if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                    string map = SekiroLocationDataScraper.locations[entry.Key];
                    MSBS msb = entry.Value;

                    Dictionary<string, MSBS.Event.Talk> talks = new Dictionary<string, MSBS.Event.Talk>();
                    foreach (MSBS.Event.Talk talk in msb.Events.Talks)
                    {
                        foreach (string part in talk.EnemyNames)
                        {
                            if (part != null)
                            {
                                if (talks.ContainsKey(part)) throw new Exception($"{part} appears in multiple talks");
                                talks[part] = talk;
                            }
                        }
                    }
                    foreach (MSBS.Part.Enemy e in msb.Parts.Enemies)
                    {
                        EnemyInfo info = new EnemyInfo
                        {
                            ID = e.EntityID,
                            DebugText = $"{entry.Key} - {e.Name} - id {e.EntityID}"
                                + (e.EntityGroupIDs[0] > 0 ? $" ({string.Join(",", e.EntityGroupIDs.Where(i => i > 0))})" : "")
                                + $" - {game.ModelName(e.ModelName)}"
                        };
                        if (talks.TryGetValue(e.Name, out MSBS.Event.Talk talk))
                        {
                            info.ESDs = string.Join(",", talk.TalkIDs);
                        }
                        // The old system for setting events is removed. Can reimplement this mapping if needed.
                        // if (idCommands.TryGetValue(e.EntityID, out List<InstructionDebug> usages)) info.Events = string.Join("; ", usages.Select(u => u.Name).Distinct());
                        // Can also print out teamType and npcType from Params["NpcParam"][e.NPCParamID]
                        outYaml.Enemies.Add(info);
                    }
                }
                ISerializer serializer = new SerializerBuilder().DisableAliases().Build();
                using (var writer = File.CreateText("enemy.txt"))
                {
                    serializer.Serialize(writer, outYaml);
                }
            }

            // Remove most team affiliations, since they are set for multiple enemies at once.
            // Can revisit this or use emevd like in DS1 enemy rando.
            foreach (PARAM.Row row in Params["NpcParam"].Rows)
            {
                int teamType = (byte)row["npcType"].Value;
                if (teamType == 24 || teamType == 29)
                {
                    row["npcType"].Value = (byte)6;
                }
            }

            // Force mapping from target to source. This does disrupt the seed, but only way to avoid that would be with potentially extensive swapping.
            // TODO: Add a config to load this in, for people with custom preferences, and validate it
            Dictionary<int, int> forceMap = new Dictionary<int, int>();

            HashSet<int> forceMapSources = new HashSet<int>(forceMap.Values);
            Dictionary<EnemyClass, EnemyPermutation> silos = new Dictionary<EnemyClass, EnemyPermutation>();
            // TODO: Make this a bit more formal in the future... the currently randomized silos exclude 0 (no random) and 4 (minion)
            for (int i = 1; i <= 3; i++)
            {
                silos[(EnemyClass)i] = new EnemyPermutation { Type = i };
            }
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
                    if (!forceMapSources.Contains(info.ID))
                    {
                        silo.Sources.Add(info.ID);
                    }
                }
            }

            bool debugPlacement = false;
            string ename(int ent)
            {
                EnemyData data = defaultData[ent];
                return $"{data.Name} #{ent} ({game.ModelName(data.Model)})";
            }
            int seed = (int)opt.Seed;
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
                if (forceMap.ContainsKey(source)) return true;
                EnemyInfo sourceInfo = infos[source];
                EnemyInfo targetInfo = infos[target];
                if (targetInfo.HasTag($"exclude:{source}"))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} since it is specifically excluded from there");
                    return false;
                }
                if (sourceInfo.HasTag("unique") && defaultData[target].Group.Any(g => phantomGroups.Contains(g)))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it is a unique enemy into a transient target");
                    return false;
                }
                if (targetInfo.Class == EnemyClass.Boss || targetInfo.Class == EnemyClass.Miniboss)
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
                if ((targetInfo.Class == EnemyClass.Miniboss && opt["earlyreq"]) || (targetInfo.Class == EnemyClass.Boss && opt["phasebuff"]))
                {
                    if (targetInfo.Tags != null && targetInfo.HasTag("earlyreq"))
                    {
                        if (sourceInfo.Tags == null || !sourceInfo.HasTag("early"))
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's not an early enemy");
                            return false;
                        }
                    }
                }
                if (prevLimits != null && targetInfo.Class == EnemyClass.Boss)
                {
                    // Specific exclusion, for the most accessible settings, due to arena
                    if (opt["phasebuff"] && sourceInfo.ItemName == "headlessape" && new[] { "owl", "isshin", "genichiro" }.Contains(targetInfo.ItemName))
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because of arena restriction due to late-game enemy phases");
                        return false;
                    }
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
                    int targetPhases = targets.Sum(t => t.Phases);
                    if (targetInfo.HasTag("nerfloc")) targetPhases -= 1;
                    float sourcePhases = 0;
                    foreach (EnemyInfo info in sources)
                    {
                        float phases = info.Phases;
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
            foreach (EnemyPermutation silo in silos.Values)
            {
                EnemyClass siloType = (EnemyClass)silo.Type;
                for (int i = 0; i < silo.Targets.Count; i++)
                {
                    silo.Mapping[silo.Targets[i]] = silo.Sources[i];
                }
                if (randomizeOpt.TryGetValue(siloType, out string optName) && !opt[optName]) continue;

                Shuffle(new Random(seed + silo.Type), silo.Sources);
                for (int i = 0; i < silo.Targets.Count; i++)
                {
                    silo.Mapping[silo.Targets[i]] = silo.Sources[i];
                }

                // Fixup pass
                if (siloType == EnemyClass.Boss) printPermutation(silo);
                fixupPass(silo, silo.Targets, false);
                if (siloType == EnemyClass.Boss) printPermutation(silo);
                if (siloType == EnemyClass.Boss && opt["phases"])
                {
                    List<int> targetsBySize = silo.Targets.OrderByDescending(t => infos[silo.Mapping[t]].Phases).ToList();
                    fixupPass(silo, targetsBySize, true);
                }
                if (siloType == EnemyClass.Boss || siloType == EnemyClass.Miniboss) printPermutation(silo);
            }

            // Print everything out
            if (opt["item"])
            {
                Console.WriteLine("Scroll down to see item placements. Or Ctrl+F 'Hints' to jump there.");
                Console.WriteLine();
            }
            foreach (EnemyClass siloType in new[] { EnemyClass.Boss, EnemyClass.Miniboss, EnemyClass.Basic })
            {
                Console.WriteLine($"-- {siloType} enemy assignments");
                if (randomizeOpt.TryGetValue(siloType, out string optName) && !opt[optName])
                {
                    Console.WriteLine("(not randomized)");
                }
                else
                {
                    string fullName(int ent, bool target)
                    {
                        EnemyData data = defaultData[ent];
                        string name = infos[ent].ExtraName ?? game.ModelName(data.Model);
                        return $"{name} (#{ent}) {(target ? "in" : "from")} {SekiroLocationDataScraper.fullMapName[SekiroLocationDataScraper.locations[data.Map]]}";
                    }
                    foreach (KeyValuePair<int, int> transfer in silos[siloType].Mapping)
                    {
                        Console.WriteLine($"Replacing {fullName(transfer.Key, true)}: {fullName(transfer.Value, false)}");
                    }
                }
                Console.WriteLine();
            }

            // There don't need to be too many new entities hopefully, add some in Abandoned Dungeon, between 1301300 and 1301950
            int entityBase = 1301301;  // Previous value: 1300250, but this overlapped with some enemies
            int entityMax = 1301950;
            int newEntity()
            {
                if (entityBase >= entityMax) throw new Exception("Internal error: Can't create more entities: out of ids");
                return entityBase++;
            }
            (MSBS.Event.Generator, MSBS.Region) getGeneratorCopy(string map, string toMap, int id, Dictionary<int, int> reloc)
            {
                if (!generators.ContainsKey(id)) throw new Exception($"Cannot find generator {id}");
                (MSBS.Event.Generator gen, int original) = generators[id];
                if (!reloc.TryGetValue(original, out int target)) throw new Exception($"Can't remap {original} from generator {id}");
                MSBS msb = maps[toMap];
                MSBS.Part.Enemy enemy = msb.Parts.Enemies.Find(e => e.EntityID == target);
                if (enemy == null) return (null, null);
                MSBS.Event.Generator gen2 = new MSBS.Event.Generator();
                CopyAll(gen, gen2);
                MSBS.Region.Event a = maps[map].Regions.Events.Find(r => r.Name == gen.SpawnRegionNames[0]);
                MSBS.Region.Event b = new MSBS.Region.Event();
                CopyAll(a, b);
                msb.Events.Generators.Add(gen2);
                msb.Regions.Events.Add(b);

                int genId = newEntity();
                b.EntityID = -1;
                b.Name = $"Generator Point {genId} {id}";
                gen2.SpawnPartNames[0] = enemy.Name;
                gen2.SpawnRegionNames[0] = b.Name;
                gen2.EntityID = genId;
                gen2.Name = $"Generator {genId} {id}";
                return (gen2, b);
            }
            (MSBS.Region, MSBS.Region) getRegionCopy(string map, string toMap, int id, bool replace)
            {
                MSBS.Region a = maps[map].Regions.GetEntries().Find(r => r.EntityID == id);
                if (a == null) throw new Exception($"Internal error: can't find region {id} in {map}");
                // Shallow copy, but fine if no nested fields are modified
                MSBS.Region b = (MSBS.Region)Activator.CreateInstance(a.GetType());
                CopyAll(a, b);
                MSBS msb = maps[toMap];
                if (b is MSBS.Region.Event et) msb.Regions.Events.Add(et);
                else if (b is MSBS.Region.SFX st) msb.Regions.SFXs.Add(st);
                else if (b is MSBS.Region.WalkRoute wt) msb.Regions.WalkRoutes.Add(wt);
                else if (b is MSBS.Region.ActivationArea at) msb.Regions.ActivationAreas.Add(at);
                else if (b is MSBS.Region.Other ot) msb.Regions.Others.Add(ot);
                else if (b is MSBS.Region.Region0 rt) msb.Regions.Region0s.Add(rt);
                else throw new Exception($"Internal error: unknown region type {b}, entity id {id}");
                // Except shape is a nested field which is modified, so do that
                MSBS.Shape shape = (MSBS.Shape)Activator.CreateInstance(a.GetType());
                CopyAll(b.Shape, shape);
                b.Shape = shape;
                // Hopefully this isn't used much
                b.ActivationPartName = null;
                if (replace)
                {
                    b.EntityID = a.EntityID;
                    a.EntityID = -1;
                }
                else
                {
                    b.EntityID = newEntity();
                }
                b.Name = $"Region {b.EntityID} from {id}";
                return (a, b);
            }

            // Arena points can be independent of seed, keeping things a bit interesting, and also fixes issues on rerandomization
            Random arenaRandom = new Random();
            Vector3 randomPoint(Arena arena)
            {
                Vector3 box = arena.Box;
                Vector3 res = new Vector3((float)arenaRandom.NextDouble() * box.X - (box.X / 2), 0, (float)arenaRandom.NextDouble() * box.Z - (box.Z / 2));
                res = Vector3.Transform(res, arena.RotMatrix);
                res = Vector3.Add(res, arena.Pos);
                return res;
            }
            Dictionary<int, int> copyRegions(string spec, int fromEntity, List<int> args, Dictionary<int, int> reloc, bool replace=false)
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
                else if (scope == "event")
                {
                    // Transplant all regions
                }
                else throw new Exception($"Internal error: unknown scope in {spec}");
                string fromMap = defaultData[fromEntity].Map;
                int toEntity = reloc[fromEntity];
                string toMap = defaultData[toEntity].Map;
                Dictionary<int, int> res = new Dictionary<int, int>();
                if (type == "chrpoint")
                {
                    MSBS.Part.Enemy e = maps[toMap].Parts.Enemies.Find(en => en.EntityID == toEntity);
                    if (e == null) throw new Exception($"Internal error: can't find {toEntity} in {toMap}");
                    foreach (int region in regions)
                    {
                        (MSBS.Region a, MSBS.Region b) = getRegionCopy(fromMap, toMap, region, replace);
                        b.Position = e.Position;
                        b.Rotation = e.Rotation;
                        res[region] = b.EntityID;
                    }
                }
                else if (type == "inside")
                {
                    // Do this later, for slightly better teleports
                }
                else if (type == "arenagen")
                {
                    Arena arena = infos[toEntity].ArenaData;
                    if (arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    foreach (int region in regions)
                    {
                        (MSBS.Event.Generator newGen, MSBS.Region b) = getGeneratorCopy(fromMap, toMap, region, reloc);
                        if (newGen != null)
                        {
                            b.Position = randomPoint(arena);
                            res[region] = newGen.EntityID;
                        }
                    }
                }
                else if (type.StartsWith("arena"))
                {
                    Arena arena = infos[toEntity].ArenaData;
                    if (arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    List<Vector3> corners = null;
                    if (type == "arenapartition")
                    {
                        corners = new List<Vector3>();
                        for (int i = -1; i <= 1; i += 2)
                        {
                            for (int j = -1; j <= 1; j += 2)
                            {
                                Vector3 c = new Vector3(arena.Box.X / 2 * i, 0, arena.Box.Z / 2 * j);
                                c = Vector3.Transform(c, arena.RotMatrix);
                                c = Vector3.Add(c, arena.Pos);
                                corners.Add(c);
                            }
                        }
                    }
                    int partition = 0;
                    foreach (int region in regions)
                    {
                        (MSBS.Region a, MSBS.Region b) = getRegionCopy(fromMap, toMap, region, replace);
                        res[region] = b.EntityID;
                        if (type == "arena")
                        {
                            // Try to add this hack for composite shape
                            if (region == 2502570 || region == 2502571)
                            {
                                b.Shape = new MSBS.Shape.Box();
                            }
                            if (!(b.Shape is MSBS.Shape.Box box)) throw new Exception($"For now, only box regions can encompass entire arenas - {region} in {spec}");
                            // Just ignore x/z rotation for the moment. The x/z bounds will be bigger than when it's laid flat, but hopefully not too much.
                            // Also add a few units since these are often floating above arenas
                            box.Width = arena.Box.X;
                            box.Height = arena.Box.Y + 3;
                            box.Depth = arena.Box.Z;
                            b.Position = new Vector3(arena.Pos.X, arena.Pos.Y - 3, arena.Pos.Z);
                            b.Rotation = arena.Rot;
                        }
                        else if (type == "arenaowl")
                        {
                            Arena owlArena = infos[toEntity].OwlArenaData;
                            if (owlArena == null)
                            {
                                // Try to make this never activate, by putting a very far away distance from player
                                res[region] = -100000;
                            }
                            else
                            {
                                if (!(b.Shape is MSBS.Shape.Box box)) throw new Exception($"For now, only box regions can encompass entire arenas - {region} in {spec} has {b.Shape}");
                                box.Width = owlArena.Box.X;
                                box.Height = owlArena.Box.Y;
                                box.Depth = owlArena.Box.Z;
                                b.Position = owlArena.Pos;
                                b.Rotation = owlArena.Rot;
                            }
                        }
                        else if (type == "arenapoint" || type == "arenasfx" || type == "arenabox")
                        {
                            // Just move to center for now
                            b.Position = (type == "arenabox" || originalCount > 1) ? randomPoint(arena) : arena.Pos;
                            // Shapes: point, sphere, box
                            // Only box is used for detection. Points used for spawning/warping enemies, spheres used for Isshin fire
                            if (type == "arenabox" && b.Shape is MSBS.Shape.Box box)
                            {
                                box.Height += 3;
                                b.Position = new Vector3(b.Position.X, b.Position.Y - 3, b.Position.Z);
                            }
                        }
                        else if (type == "arenapartition")
                        {
                            if (!(b.Shape is MSBS.Shape.Box box))
                            {
                                b.Shape = box = new MSBS.Shape.Box();
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
                            b.Position = new Vector3(b.Position.X, b.Position.Y - 3, b.Position.Z);
                            b.Rotation = arena.Rot;
                        }
                        else throw new Exception($"Unknown region spec: {spec}");
                    }
                }
                else if (type.StartsWith("dist"))
                {
                    int dist = int.Parse(type.Substring(4));
                    foreach (int region in regions)
                    {
                        // Put negative number as convention to replace in/out conditions with distance
                        res[region] = -dist;
                    }
                }
                else throw new Exception($"Unknown region spec: {spec}");
                return res;
            }

            // Small convenience: don't disable boss bonfires when we can't always guarantee that boss helpers die.
            // This should be fine as they don't spawn while the boss is alive... hopefully.
            HashSet<int> noDisableBonfires = new HashSet<int> {
                1101955,  // Ashina Castle Gate
                1111952,  // Castle Tower Lookout
                1501953,  // Wedding Cave Door
                1701956,  // Guardian Ape's Burrow
                1701953,  // Guardian Ape's Watering Hole
                2501951,  // Vermillion Bridge
            };
            foreach (KeyValuePair<string, MSBS> entry in maps)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string map = SekiroLocationDataScraper.locations[entry.Key];
                MSBS msb = entry.Value;

                foreach (MSBS.Part.Collision col in msb.Parts.Collisions)
                {
                    if (noDisableBonfires.Contains(col.DisableBonfireEntityID))
                    {
                        col.DisableBonfireEntityID = -1;
                    }
                }
            }

            bool enableMultichr = true;
            int helperModelBase = 100;
            // Mapping of enemies which are randomized, from source to target
            Dictionary<int, List<int>> mapping = new Dictionary<int, List<int>>();
            // Mapping of new helpers, from original owner and original helper to target
            Dictionary<(int, int), int> helperMapping = new Dictionary<(int, int), int>();
            // Mapping from target entity to map name
            Dictionary<int, string> ownerMap = new Dictionary<int, string>();

            foreach (KeyValuePair<string, MSBS> entry in maps)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string map = SekiroLocationDataScraper.locations[entry.Key];
                MSBS msb = entry.Value;

                // Model declarations
                HashSet<string> mdecl = new HashSet<string>();
                HashSet<string> mused = new HashSet<string>();
                foreach (MSBS.Model.Enemy model in msb.Models.Enemies)
                {
                    mdecl.Add(model.Name);
                }
                foreach (MSBS.Part e in msb.Parts.GetEntries())
                {
                    if (e.Type == MSBS.PartType.DummyEnemy || e.Type == MSBS.PartType.Enemy || e.Type == MSBS.PartType.Player)
                    {
                        mused.Add(e.ModelName);
                    }
                }

                HashSet<string> newEsds = new HashSet<string>();
                // Enemy stuff
                HashSet<string> laterEnemies = new HashSet<string> { "c1361", "c1700", "c1470" };

                void useModel(string name)
                {
                    mused.Add(name);
                    if (!mdecl.Contains(name))
                    {
                        msb.Models.Enemies.Add(new MSBS.Model.Enemy
                        {
                            Name = name,
                            Placeholder = $@"N:\NTC\data\Model\chr\{name}\sib\{name}.sib",
                        });
                        mdecl.Add(name);
                    }
                }
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
                        MSBS.Part.Enemy e = msb.Parts.Enemies.Find(en => en.EntityID == target);
                        EnemyData data = defaultData[source];
                        e.ModelName = data.Model;
                        e.NPCParamID = data.NPC;
                        e.ThinkParamID = data.Think;

                        // Shichimen bad placement? or gravity?
                        if (target == 1300200)
                        {
                            e.Position = new Vector3(e.Position.X, e.Position.Y + 0.5f, e.Position.Z);
                        }
                        if (target == 1000810)
                        {
                            // Emma doesn't aggro from Butterfly 2 spot. Rewrite to ground spot.
                            // TODO: See if anyone else doesn't want to aggro
                            if (source == 1110900)
                            {
                                e.Position = new Vector3(-239.089f, -787.188f, 583.271f);
                            }
                        }
                        if (source == 1110536 || source == 1110549)
                        {
                            // Don't allow wooooooo enemies to walk around, as this interrupts their invisibility
                            e.UnkT20 = -1;
                        }
                        if (infos[source].Regions != null)
                        {
                            foreach (string spec in infos[source].Regions)
                            {
                                copyRegions(spec, source, new List<int>(), new Dictionary<int, int> { { source, target } }, replace: true);
                            }
                        }
                        if (infos[target].ArenaData != null)
                        {
                            // Also always add arena, as a debugging tool
                            Arena arena = infos[target].ArenaData;
                            MSBS.Shape.Box box = new MSBS.Shape.Box();
                            box.Width = arena.Box.X;
                            box.Height = arena.Box.Y;
                            box.Depth = arena.Box.Z;
                            MSBS.Region.Event r = new MSBS.Region.Event();
                            r.Shape = box;
                            r.Position = arena.Pos;
                            r.Rotation = arena.Rot;
                            r.Name = $"Arena for {target}";
                            msb.Regions.Events.Add(r);
                        }
                        useModel(e.ModelName);
                        AddMulti(mapping, source, target);
                        ownerMap[target] = entry.Key;
                        // Also add minions
                        if (owners.TryGetValue(source, out List<int> helpers))
                        {
                            Arena arena = infos[target].ArenaData;
                            foreach (int helper in helpers)
                            {
                                if (!enableMultichr) continue;
                                if (infos[helper].Class != EnemyClass.Helper) continue;
                                MSBS.Part.Enemy e2 = new MSBS.Part.Enemy(e);
                                EnemyData data2 = defaultData[helper];
                                e2.ModelName = data2.Model;
                                e2.NPCParamID = data2.NPC;
                                e2.ThinkParamID = data2.Think;
                                if (arena != null)
                                {
                                    e2.Position = randomPoint(arena);
                                    // Arenas are a bit more of an art than a science, so some extra height for safety
                                    e2.Position = new Vector3(e2.Position.X, e2.Position.Y + 0.5f, e2.Position.Z);
                                }
                                // Also new entity id
                                e2.EntityID = newEntity();
                                e2.Name = $"{e2.ModelName}_{helperModelBase++:d4}";
                                e2.UnkT24 = 0;
                                msb.Parts.Enemies.Add(e2);
                                useModel(e2.ModelName);
                                helperMapping[(source, helper)] = e2.EntityID;
                                ownerMap[e2.EntityID] = entry.Key;
                            }
                        }
                        // And remove them from this current map
                        if (owners.TryGetValue(target, out List<int> helpers2))
                        {
                            msb.Parts.Enemies = msb.Parts.Enemies.Where(p => !(helpers2.Contains(p.EntityID) && infos[p.EntityID].Class == EnemyClass.Helper)).ToList();
                        }
                    }
                }

                List<string> names = msb.Parts.Enemies.Select(e => e.Name).ToList();
                msb.Events.Talks = msb.Events.Talks.Where(t => t.EnemyNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.Event21s = msb.Events.Event21s.Where(t => t.Event21PartNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.GroupTours = msb.Events.GroupTours.Where(t => t.GroupPartNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.Generators = msb.Events.Generators.Where(t => t.SpawnPartNames.All(n => n == null || names.Contains(n))).ToList();

                msb.Models.Enemies = msb.Models.Enemies.Where(e => mused.Contains(e.Name)).OrderBy(e => e.Name).ToList();
            }

            // It's emevd t ime
            Dictionary<int, EventSpec> templates = events.Config.EnemyEvents.ToDictionary(e => e.ID, e => e);

            // Entities which do not exist in game anymore, but passed as arguments to events.
            // These event initializations are ignored, otherwise an error is thrown if no valid entity is found.
            HashSet<int> ignoreEnemies = new HashSet<int> {
                1300231, 1300232, 1300237,
                1500262, 1500263, 1500269, 1500270, 1500272, 1500279, 1500280, 1500281, 1500282, 1500283, 1500284, 1500285, 1500286, 1500287, 1500288, 1500289,
                1500343, 1500362
            };
            // Preprocess boss commands which need to move around
            Dictionary<int, EnemyTemplate> endEvents = templates.Values
                .Where(ev => ev.Template.Any(t => t.Type.StartsWith("end")))
                .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("end")));
            Dictionary<(int, int), List<EMEVD.Instruction>> endConds = new Dictionary<(int, int), List<EMEVD.Instruction>>();
            // Also startcmds
            Dictionary<int, EnemyTemplate> startEvents = templates.Values
                .Where(ev => ev.Template.Any(t => t.Type.StartsWith("start") && t.StartCmd != null))
                .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("start")));
            Dictionary<int, List<EMEVD.Instruction>> startCmds = new Dictionary<int, List<EMEVD.Instruction>>();
            List<int> findCond(EMEVD.Event e, string req)
            {
                List<int> cond = new List<int>();
                bool isGroup = int.TryParse(req, out int _);
                for (int i = 0; i < e.Instructions.Count; i++)
                {
                    Instr instr = events.Parse(e.Instructions[i]);
                    if (isGroup && instr.Name.StartsWith("IF") && instr[0].ToString() == req)
                    {
                        cond.Add(i);
                        continue;
                    }
                    else if (isGroup && instr.Name == "IF Condition Group" && instr[2].ToString() == req)
                    {
                        cond.Add(i);
                        return cond;
                    }
                    else if (!isGroup && instr.Name == req && instr[0].ToString() == "0")
                    {
                        cond.Add(i);
                        return cond;
                    }
                }
                throw new Exception($"Couldn't find ending condition '{req}', group {isGroup}, in event {e.ID}");
            }
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    int id = (int)e.ID;
                    if (endEvents.TryGetValue(id, out EnemyTemplate end))
                    {
                        if (end.EndCond != null)
                        {
                            endConds[(end.Entity, 0)] = findCond(e, end.EndCond).Select(i => events.CopyInstruction(e.Instructions[i])).ToList();
                        }
                        if (end.EndCond2 != null)
                        {
                            endConds[(end.Entity, 1)] = findCond(e, end.EndCond2).Select(i => events.CopyInstruction(e.Instructions[i])).ToList();
                        }
                    }
                    if (startEvents.TryGetValue(id, out EnemyTemplate start))
                    {
                        EventEdits edits = new EventEdits();
                        events.RemoveMacro(edits, start.StartCmd);
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
            // Manually add commands for Owl 2 dispmask. TODO do this with NpcParam instead.
            AddMulti(startCmds, 1000900, new EMEVD.Instruction(2004, 26, new List<object> { 1000900, (byte)2, (byte)1 }));
            AddMulti(startCmds, 1000900, new EMEVD.Instruction(2004, 26, new List<object> { 1000900, (byte)3, (byte)0 }));
            AddMulti(startCmds, 1000900, new EMEVD.Instruction(2004, 26, new List<object> { 1000900, (byte)4, (byte)1 }));

            Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>> newInitializations = new Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>>();
            // Don't redo non-chr event templates. For now, just identity references...
            HashSet<EnemyTemplate> completedTemplates = new HashSet<EnemyTemplate>();
            bool warnBadCommands = false;
            Dictionary<int, EMEVD.Event> commonEvents = emevds["common_func"].Events.ToDictionary(e => (int)e.ID, e => e);
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                if (!newInitializations.ContainsKey(entry.Key)) newInitializations[entry.Key] = new List<(EMEVD.Instruction, EMEVD.Event)>();

                // Note this putting all events in a dictionary precludes duplicates, which technically the game allows, so may conflict with other mods
                Dictionary<int, EMEVD.Event> fileEvents = entry.Value.Events.ToDictionary(e => (int)e.ID, e => e);
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    OldParams initOld = OldParams.Preprocess(e);
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr init = events.Parse(e.Instructions[i]);
                        if (!init.Init) continue;
                        int callee = init.Callee;
                        if (!templates.TryGetValue(callee, out EventSpec ev)) continue;
                        if (ev.Template.Count == 0) throw new Exception($"event {callee} has no templates");
                        // chr, multichr, loc, start, end, startphase, endphase, remove
                        if (ev.Template[0].Type == "remove")
                        {
                            if (ev.Template[0].Entity <= 0 || mapping.ContainsKey(ev.Template[0].Entity))
                            {
                                // Remove action by removing initialization, for now. Can garbage collect later if desired.
                                e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                            }
                            continue;
                        }
                        bool hasChr = ev.Template.Any(t => t.Type.Contains("chr"));
                        bool allChr = ev.Template.All(t => t.Type.Contains("chr"));
                        // Source/target entity and event copy, for copies. At the moment there is only one chr per event initialization, but perhaps group them together in the future.
                        List<(int, int, EMEVD.Event, EnemyTemplate)> eventCopies = new List<(int, int, EMEVD.Event, EnemyTemplate)>();
                        bool canRemove = true;
                        // Do all chrs first
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (!t.Type.Contains("chr"))
                            {
                                canRemove = false;
                                continue;
                            }
                            int entity = t.Entity;
                            // Include dummy enemies for now, otherwise it will detect no entity
                            int argEntity = init.Args.Skip(init.Offset).Where(a => a is int ai && (infos.ContainsKey(ai) || ignoreEnemies.Contains(ai))).FirstOrDefault() is int aj ? aj : 0;
                            if (entity <= 0)
                            {
                                if (argEntity == 0) throw new Exception($"No entity found in {init}) args");
                                entity = argEntity;
                            }
                            if (!mapping.TryGetValue(entity, out List<int> targets))
                            {
                                canRemove = false;
                                continue;
                            }
                            if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                            {
                                foreach (int target in targets) eventCopies.Add((entity, target, events.CopyEvent(theEvent), t));
                            }
                            else if (argEntity != 0)
                            {
                                foreach (int target in targets) eventCopies.Add((entity, target, null, t));
                            }
                            else
                            {
                                throw new Exception($"Initialized event {callee} but absent from this file and not specified in args");
                            }
                        }
                        // Do in-place pass
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (!t.Type.Contains("chr"))
                            {
                                if (t.Entity > 0)
                                {
                                    if (!mapping.ContainsKey(t.Entity))
                                    {
                                        continue;
                                    }
                                }
                                if (t.IsDefault() && !t.Type.StartsWith("start"))
                                {
                                    // This is fine, nothing to do if not chr. Except starts may need startcmds
                                }
                                else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                                {
                                    if (completedTemplates.Contains(t)) continue;
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, 0, theEvent, t));
                                }
                                else if (t.Type == "common" && commonEvents.TryGetValue(callee, out EMEVD.Event comEvent))
                                {
                                    if (completedTemplates.Contains(t)) continue;
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, 0, comEvent, t));
                                }
                                else throw new Exception($"Can't find event {callee} with {t.Type} template");
                            }
                        }
                        foreach (var copy in eventCopies)
                        {
                            (int entity, int target, EMEVD.Event e2, EnemyTemplate t) = copy;
                            // Main entities to replace
                            Dictionary<int, int> reloc = new Dictionary<int, int>();
                            Dictionary<int, int> distReplace = new Dictionary<int, int>();
                            if (entity != 0)
                            {
                                reloc[entity] = target;
                                if (t.Type == "multichr" && owners.TryGetValue(entity, out List<int> helpers))
                                {
                                    foreach (int helper in helpers)
                                    {
                                        if (!enableMultichr)
                                        {
                                            reloc[helper] = helper;
                                            continue;
                                        }
                                        if (infos[helper].Class != EnemyClass.Helper) continue;
                                        // If entity has mapping, its helpers should also
                                        reloc[helper] = helperMapping[(entity, helper)];
                                    }
                                }
                                if (t.Regions != null)
                                {
                                    foreach (string spec in t.Regions)
                                    {
                                        foreach (KeyValuePair<int, int> region in copyRegions(spec, entity, init.Args.SelectMany(o => o is int oi ? new[] { oi } : new int[] { }).ToList(), reloc))
                                        {
                                            if (region.Value > 0)
                                            {
                                                reloc[region.Key] = region.Value;
                                            }
                                            else
                                            {
                                                distReplace[region.Key] = -region.Value;
                                            }
                                        }
                                    }
                                }
                                // Also we need to make a copy of the instruction at this point, so we don't edit the original
                                events.CopyInit(init, e2);
                            }
                            // Add all edits
                            EventEdits edits = new EventEdits();
                            if (t.Remove != null)
                            {
                                foreach (string remove in Regex.Split(t.Remove, @"\s*;\s*"))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.Replace != null)
                            {
                                foreach (string replace in Regex.Split(t.Replace, @"\s*;\s*"))
                                {
                                    events.ReplaceMacro(edits, replace);
                                }
                            }
                            if (t.StartCmd != null)
                            {
                                events.RemoveMacro(edits, t.StartCmd);
                            }
                            // Also edit conditional flags present in both the event and the target
                            if (entity > 0 && infos.ContainsKey(target))
                            {
                                EnemyInfo targetInfo = infos[target];
                                if (t.DefeatFlag != 0)
                                {
                                    if (targetInfo.DefeatFlag == 0) throw new Exception($"{mapping[entity]} has no defeat flag defined, but was randomized to {entity} in {callee}");
                                    events.ReplaceMacro(edits, t.DefeatFlag.ToString(), targetInfo.DefeatFlag.ToString());
                                }
                                if (t.StartFlag != 0)
                                {
                                    if (targetInfo.StartFlag == 0) throw new Exception($"{mapping[entity]} has no start flag defined, but was randomized to {entity} in {callee}");
                                    events.ReplaceMacro(edits, t.StartFlag.ToString(), targetInfo.StartFlag.ToString());
                                }
                                // This won't add appear flag is none was there previously. Do that if it becomes a problem
                                if (t.AppearFlag != 0)
                                {
                                    if (targetInfo.AppearFlag == 0)
                                    {
                                        events.RemoveMacro(edits, t.AppearFlag.ToString());
                                    }
                                    else
                                    {
                                        events.ReplaceMacro(edits, t.AppearFlag.ToString(), targetInfo.AppearFlag.ToString());
                                    }
                                }
                            }
                            if (e2 != null)
                            {
                                OldParams pre = OldParams.Preprocess(e2);
                                if (t.Add != null)
                                {
                                    events.AddMacro(edits, t.Add);
                                }
                                // Prevent events from activating in later fight phases
                                if (t.Type == "chr" && t.DefeatFlag != 0 && entity != 0)
                                {
                                    EnemyInfo targetInfo = infos[target];
                                    if (targetInfo.Class == EnemyClass.Boss && targetInfo.OwnedBy != 0)
                                    {
                                        int startFlag = infos[targetInfo.OwnedBy].StartFlag;
                                        if (startFlag == 0) throw new Exception($"{targetInfo.OwnedBy} has no start flag");
                                        // Skip line if chr event with player animation force, and the first phase of a fight
                                        EMEVD.Instruction toAdd = new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)1, (byte)0, startFlag });
                                        for (int j = 0; j < e2.Instructions.Count; j++)
                                        {
                                            Instr instr = events.Parse(e2.Instructions[j]);
                                            if (instr.Init) continue;
                                            if (instr.Name == "EzState Instruction Request" && instr[0].ToString() == "10000")
                                            {
                                                e2.Instructions.Insert(j, toAdd);
                                                break;
                                            }
                                        }
                                    }
                                }
                                for (int j = 0; j < e2.Instructions.Count; j++)
                                {
                                    Instr instr = events.Parse(e2.Instructions[j]);
                                    // Randomized events shouldn't have initializations, although we could probably also ignore them
                                    if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e2.ID}");
                                    // We are either dealing with a copy of the event or the original one. So all edits are in-place
                                    // Remove/replace cases
                                    edits.ApplyEdits(instr, j);
                                    instr.Save();
                                    e2.Instructions[j] = instr.Val;
                                    // Replace area checks with distance checks
                                    if (entity != 0 && instr.Name == "IF In/Outside Area" && distReplace.TryGetValue((int)instr[3], out int dist))
                                    {
                                        // Note: Doesn't work if we want to reuse parameterized events
                                        EMEVD.Instruction toAdd = new EMEVD.Instruction(3, 3, new List<object> { instr[0], instr[1], 10000, entity, (float)dist, 1 });
                                        e2.Instructions[j] = toAdd;
                                    }
                                }
                                events.ApplyAdds(edits, e2);
                                if (t.Type.StartsWith("start"))
                                {
                                    int source = silos[EnemyClass.Boss].Mapping[t.Entity];
                                    if (startCmds.TryGetValue(source, out List<EMEVD.Instruction> cmds))
                                    {
                                        Dictionary<int, int> bossReplace = new Dictionary<int, int> { { source, t.Entity } };
                                        foreach (EMEVD.Instruction cmd in cmds)
                                        {
                                            Instr instr = events.Parse(cmd);
                                            events.RewriteInts(instr, bossReplace);
                                            instr.Save();
                                            e2.Instructions.Add(instr.Val);
                                        }
                                    }
                                }
                                if (t.Type.StartsWith("end") && (t.EndCond != null || t.EndCond2 != null))
                                {
                                    // Unlike most transformations, this goes from source to target
                                    int source = silos[EnemyClass.Boss].Mapping[t.Entity];
                                    Dictionary<int, int> bossReplace = new Dictionary<int, int> { { source, t.Entity } };
                                    void replaceCond(List<int> before, List<EMEVD.Instruction> after)
                                    {
                                        if (after == null)
                                        {
                                            // If target doesn't use this condition, just remove it probably.
                                            e2.Instructions = e2.Instructions.Where((_, pos) => !before.Contains(pos)).ToList();
                                            return;
                                        }
                                        Instr instr = events.Parse(e2.Instructions[before.Last()]);
                                        sbyte targetCond = instr[0] is sbyte ai ? ai : throw new Exception("Internal error: boss end condition first arg has unexpected type");
                                        // Replace commands starting at first one
                                        e2.Instructions = e2.Instructions.Where((_, pos) => !before.Contains(pos)).ToList();
                                        sbyte sourceCond = 0;
                                        after = after.Select(afterInstr =>
                                        {
                                            instr = events.Parse(events.CopyInstruction(afterInstr));
                                            if (instr.Name == "IF Condition Group")
                                            {
                                                if (sourceCond == 0) throw new Exception("Internal error: can't infer condition group to use for ending boss");
                                                instr[0] = targetCond;
                                                instr[2] = (sbyte)(sourceCond > 0 ? 10 : -10);
                                            }
                                            else
                                            {
                                                if (sourceCond == 0)
                                                {
                                                    sourceCond = (sbyte)instr[0];
                                                }
                                                // This is way too hacky... can add more semantic info if it becomes fragile
                                                instr[0] = after.Count == 1 ? targetCond : (sbyte)(sourceCond > 0 ? 10 : -10);
                                            }
                                            events.RewriteInts(instr, bossReplace);
                                            instr.Save();
                                            return instr.Val;
                                        }).ToList();
                                        e2.Instructions.InsertRange(before[0], after);
                                    }
                                    if (t.EndCond != null)
                                    {
                                        replaceCond(findCond(e2, t.EndCond), endConds.TryGetValue((source, 0), out List<EMEVD.Instruction> after) ? after : throw new Exception($"No primary end cond for {source}"));
                                    }
                                    if (t.EndCond2 != null)
                                    {
                                        replaceCond(findCond(e2, t.EndCond2), t.Type == "end" && endConds.TryGetValue((source, 1), out List<EMEVD.Instruction> after) ? after : null);
                                    }
                                }
                                pre.Postprocess();
                            }
                            if (edits.PendingEdits.Count != 0)
                            {
                                throw new Exception($"{callee} has unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                            }
                            if (reloc.Count > 0)
                            {
                                events.RewriteInts(init, reloc);
                                init.Save();
                                if (e2 != null)
                                {
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j]);
                                        if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e.ID}");
                                        events.RewriteInts(instr, reloc);
                                        instr.Save();
                                    }
                                }
                            }
                            if (entity != 0)
                            {
                                // New event, so add to its target map and also add new initialization
                                if (!ownerMap.TryGetValue(target, out string ownMap)) throw new Exception($"Can't add initialization for {entity}, it has no owner map");
                                AddMulti(newInitializations, ownMap, (init.Val, e2));
                            }
                            // Some utility code for finding obvious bugs
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
                                        if (instr.Name.Contains("Set") && instr.Name.Contains("Event Flag") && instr[0] is int flag && (flag == 0 ? !events.IsTemp(callee) : !events.IsTemp(flag)))
                                        {
                                            warn = true;
                                        }
                                    }
                                    if (warn) Console.WriteLine($"Warning: {callee} has {instr}");
                                }
                            }
                        }
                        if (allChr && canRemove)
                        {
                            e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                        }
                    }
                    initOld.Postprocess();
                }
            }
            List<string> phase1Hack = new[] { 1110800, 1120830, 1110900 }.SelectMany(i => mapping.TryGetValue(i, out List<int> targets) ? targets.Select(j => $"{j}") : new List<string> { }).ToList();
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                List<(EMEVD.Instruction, EMEVD.Event)> newEvents = newInitializations[entry.Key];
                entry.Value.Events.AddRange(newEvents.Select(n => n.Item2).Where(e => e != null));
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    int id = (int)e.ID;
                    if (newEvents.Count > 0 && e.ID == 0)
                    {
                        foreach (EMEVD.Instruction newEvent in newEvents.Select(n => n.Item1))
                        {
                            List<object> args = newEvent.UnpackArgs(Enumerable.Repeat(ArgType.Int32, newEvent.ArgData.Length / 4));
                            e.Instructions.Add(newEvent);
                        }
                    }
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr instr = events.Parse(e.Instructions[i]);
                        if (instr.Init) continue;
                        if (instr.Name == "Set Character Immortality" && phase1Hack.Contains(instr[0].ToString()))
                        {
                            EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                            e.Instructions[i] = newInstr;
                            // Just in case...
                            e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                        }
                    }
                }
            }

            // Remove butterfly invisibility (3509210)
            Params["NpcParam"][50900001]["spEffectId25"].Value = -1;

            // Most minibosses in earlier spots are manageable, but, Sakura Bull is a bit too tanky. Bring it down to Blazing Bull level
            if (mapping.TryGetValue(2500570, out List<int> sakuraTarget) && infos[sakuraTarget[0]].HasTag("early")) Params["NpcParam"][13800000]["Hp"].Value = 2232;

            // Make Sword Saint Isshin deathblow not require Mortal Blade
            Params["ThrowKindParam"].Rows.Add(new PARAM.Row(250001, null, Params["ThrowKindParam"].AppliedParamdef));
            GameEditor.CopyRow(Params["ThrowKindParam"][250000], Params["ThrowKindParam"][250001]);
            Params["ThrowKindParam"][250001]["Mask4"].Value = (byte)0xFF;
            Params["ThrowParam"][15400590]["ThrowKindParamID0"].Value = 250001;

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

        public class EnemyAnnotations
        {
            public List<AllowedConnections> Connections = new List<AllowedConnections>();
            public List<EnemyInfo> Enemies = new List<EnemyInfo>();
        }
        public class AllowedConnections
        {
            public string From { get; set; }
            public string To { get; set; }
            public List<string> ConnectCols { get; set; }
        }
        public class EnemyInfo
        {
            // Unique entity id
            public int ID { get; set; }
            // Name etc
            public string DebugText { get; set; }
            // Info about ESDs
            public string ESDs { get; set; }
            // Info about emevd commands used
            public string Events { get; set; }
            // The category
            public EnemyClass Class { get; set; }
            // For bosses, means a first separate phase. For helpers, always travel with the main enemy.
            public int OwnedBy { get; set; }
            // Info for bosses and minibosses
            public int DefeatFlag { get; set; }
            public int AppearFlag { get; set; }
            public int StartFlag { get; set; }
            public int Phases { get; set; }
            // Tags and metadata
            public string Tags { get; set; }
            // Name of the enemy used in item randomizer, and also to identify some bosses and minibosses
            public string ItemName { get; set; }
            // Full name for bosses and minibosses when there is ambiguity, for spoiler log
            public string ExtraName { get; set; }
            // Category for minibosses, to avoid them replacing each other
            public string EnemyType { get; set; }
            // Same as in event templates, except it will remove the original region. Meant for Lua hardcoded regions (avoid editing the scripts)
            public List<string> Regions { get; set; }
            // The region(s) for bosses and minibosses. Everything should be in-bounds.
            public string Arena { get; set; }
            // Experiment for making Owl 2 owl behave better in certain arenas. Not used.
            public string OwlArena { get; set; }
            [YamlIgnore]
            public Arena ArenaData { get; set; }
            [YamlIgnore]
            public Arena OwlArenaData { get; set; }
            [YamlIgnore]
            public HashSet<string> TagSet { get; set; }
            public bool HasTag(string tag)
            {
                if (Tags == null) return false;
                if (TagSet == null) TagSet = new HashSet<string>(Tags.Split(' '));
                return TagSet.Contains(tag);
            }
        }
        private static readonly float radConvert = (float)Math.PI / 180;
        public class Arena
        {
            public Vector3 Pos { get; set; }
            public Vector3 Rot { get; set; }
            public Vector3 Box { get; set; }
            private bool matrixInit = false;
            private Matrix4x4 matrix;
            public Matrix4x4 RotMatrix {
                get
                {
                    if (!matrixInit)
                    {
                        matrix = Matrix4x4.CreateFromYawPitchRoll(Rot.Y * radConvert, Rot.X * radConvert, Rot.Z * radConvert);
                        matrixInit = true;
                    }
                    return matrix;
                }
            }
            public static Arena Parse(string arena)
            {
                // This format is highly ad hoc, but it is all the needed numbers
                // Example: -18.27 114.3 558.11 -112 45 10 5 0 6.5
                // <x pos> <y pos> <z pos> <y rot> <x width> <y height> <z depth> <opt: x rot> <opt: z rot>
                // following fromsoft convention, x and z pos are center of the box, and y pos is bottom of the box
                List<float> c = arena.Split(' ').Select(w => float.Parse(w, CultureInfo.InvariantCulture)).ToList();
                if (c.Count != 7 && c.Count != 9) throw new Exception($"Badly formatted arena bounds: {arena}");
                return new Arena
                {
                    Pos = new Vector3(c[0], c[1], c[2]),
                    Rot = c.Count == 9 ? new Vector3(c[7], c[3], c[8]) : new Vector3(0, c[3], 0),
                    Box = new Vector3(c[4], c[5], c[6]),
                };
            }
        }
        public enum EnemyClass
        {
            None = 0,
            Basic = 1,
            Miniboss = 2,
            Boss = 3,
            Helper = 4,
            Headless = 5
        }
        public class EnemyData
        {
            // Do not get transplanted
            public string Map { get; set; }
            public string Name { get; set; }
            // Gets transplanted
            public string Model { get; set; }
            public int NPC { get; set; }
            public int Think { get; set; }
            // Info for placement
            public List<int> Group { get; set; }
        }
        public class BossPhaseLimit
        {
            public int Entity { get; set; }
            public float Amount { get; set; }
        }

        public class EnemyPermutation
        {
            public int Type { get; set; }
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
        public void CopyAll<T>(T source, T target)
        {
            var type = typeof(T);
            foreach (System.Reflection.PropertyInfo sourceProperty in type.GetProperties())
            {
                System.Reflection.PropertyInfo targetProperty = type.GetProperty(sourceProperty.Name);
                if (sourceProperty.PropertyType.IsArray)
                {
                    Array arr = (Array)sourceProperty.GetValue(source);
                    Array.Copy(arr, (Array)targetProperty.GetValue(target), arr.Length);
                }
                else if (sourceProperty.CanWrite)
                {
                    targetProperty.SetValue(target, sourceProperty.GetValue(source, null), null);
                }
            }
        }
    }
}
