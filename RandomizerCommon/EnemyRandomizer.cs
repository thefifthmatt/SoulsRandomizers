using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Events;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Preset;
using static RandomizerCommon.Util;

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
        public EnemyLocations Run(RandomizerOptions opt, Preset preset)
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
            List<EnemyClass> randomizedTypes = new List<EnemyClass>
            {
                EnemyClass.Basic,
                EnemyClass.Miniboss,
                EnemyClass.Boss,
                EnemyClass.FoldingMonkey,
                EnemyClass.TutorialBoss,
            };
            // Mapping from target entity to map name
            Dictionary<int, string> ownerMap = new Dictionary<int, string>();
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
                    ownerMap[e.EntityID] = entry.Key;
                    // Set all boss/miniboss default teams to be aggressive toward player
                    // TODO: This doesn't work for blazing bull for some reason, or at least it damages other enemies
                    if (e.NPCParamID > 0 && (infos[e.EntityID].Class == EnemyClass.Boss || infos[e.EntityID].Class == EnemyClass.Miniboss))
                    {
                        PARAM.Row npc = game.Params["NpcParam"][e.NPCParamID];
                        npc["teamType"].Value = (byte)3;
                        npc["npcType"].Value = (byte)6;
                    }
                }
            }

            // Process config
            HashSet<string> testNorandom = new HashSet<string> { };
            bool isRandom(EnemyInfo info, string model)
            {
                if (testNorandom.Count == 0) return true;
                if ((info.Class == EnemyClass.Basic || info.ExtraName == null) && testNorandom.Contains(model)) return false;
                if (info.ExtraName != null && testNorandom.Contains(info.ExtraName)) return false;
                if (testNorandom.Contains(info.ID.ToString())) return false;
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
                    info.Class = EnemyClass.SetNone;
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
                        // Add all tags from main, mainly lateness
                        info.Tags = main.Tags + (info.Tags == null ? "" : " " + info.Tags);
                        info.ItemName = main.ItemName;
                    }
                }
                if (info.Arena != null)
                {
                    info.ArenaData = Arena.Parse(info.Arena);
                    if (info.OwlArena != null)
                    {
                        info.OwlArenaData = Arena.Parse(info.OwlArena);
                    }
                }
                else if (info.IsNamedTarget) throw new Exception($"Entity {info.ID} has no arena defined");
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
            if (preset != null)
            {
                preset.ProcessEnemyPreset(game, infos, ann.Categories, defaultData);
            }

            // Generate category config
            if (opt["writecat"])
            {
                EnemyAnnotations outYaml = new EnemyAnnotations();
                SortedDictionary<string, EnemyCategory> cats = new SortedDictionary<string, EnemyCategory>();
                foreach (EnemyInfo info in infos.Values)
                {
                    if (!randomizedTypes.Contains(info.Class)) continue;
                    if (!defaultData.TryGetValue(info.ID, out EnemyData data)) throw new Exception($"Entity {info.ID} does not exist in map; cannot randomize it");
                    string model = game.ModelName(data.Model);
                    if (info.IsBossTarget) model = info.ExtraName ?? model;
                    if (!cats.TryGetValue(model, out EnemyCategory cat))
                    {
                        cat = cats[model] = new EnemyCategory { Name = model };
                    }
                    if (info.Category != null)
                    {
                        cat.Partial = cat.Partial ?? new List<string>();
                        cat.Partial.Add(info.Category);
                    }
                    if (info.Class == EnemyClass.Miniboss)
                    {
                        if (info.ExtraName != null)
                        {
                            cat.Instance = cat.Instance ?? new List<string>();
                            cat.Instance.Add(info.ExtraName);
                        }
                        if (cat.Instance?.Count > 0 && info.ExtraName == null) throw new Exception($"Model {model} has both named and unnamed minibosses");
                    }
                }
                foreach (EnemyCategory cat in cats.Values)
                {
                    if (cat.Partial != null) cat.Partial = cat.Partial.Distinct().OrderBy(a => a).ToList();
                    if (cat.Instance != null) cat.Instance.Sort();
                    outYaml.Categories.Add(cat);
                }
                ISerializer serializer = new SerializerBuilder().DisableAliases().Build();
                using (var writer = File.CreateText("enemycat.txt"))
                {
                    serializer.Serialize(writer, outYaml);
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
                    Dictionary<int, List<int>> additionalEvents = SekiroLocationDataScraper.equivalentEvents.GroupBy(t => t.Value).ToDictionary(t => t.Key, t => t.Select(s => s.Key).ToList());
                    foreach (KeyValuePair<ItemKey, ItemLocations> item in tempData.Data)
                    {
                        foreach (ItemLocation loc in item.Value.Locations.Values)
                        {
                            if (loc.Scope.Type == ItemScope.ScopeType.EVENT)
                            {
                                List<int> flags = new List<int> { loc.Scope.ID };
                                if (additionalEvents.TryGetValue(loc.Scope.ID, out List<int> dupe)) flags.AddRange(dupe);
                                foreach (int eventFlag in flags)
                                {
                                    // Permanent check: eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022
                                    flagItems[eventFlag] = game.Name(item.Key);
                                    ids.Add(eventFlag);
                                    // if (eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022)
                                    {
                                        Console.WriteLine($"{eventFlag}: {game.Name(item.Key)}");
                                    }
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
                // string file = File.ReadAllText(@"dists\Base\enemy.txt");
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
                        // Note: First group may be empty but others may have ids.
                        string id = $"id {e.EntityID}" + (e.EntityGroupIDs[0] > 0 ? $" ({string.Join(",", e.EntityGroupIDs.Where(i => i > 0))})" : "");
                        EnemyInfo info = new EnemyInfo
                        {
                            ID = e.EntityID,
                            DebugText = $"{entry.Key} - {e.Name}"
                                + $" - npc {e.NPCParamID} - think {e.ThinkParamID}"
                                + $" - {id} - {game.ModelName(e.ModelName)}"
                        };
                        // file = file.Replace($"id {e.EntityID}", $"npc {e.NPCParamID} - think {e.ThinkParamID} - id {e.EntityID}");
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
            Dictionary<int, int> forceMap = new Dictionary<int, int>();
            if (preset != null)
            {
                // Specific enemies in specific locations
                Random forcemapRandom = new Random((int)opt.Seed);
                foreach (KeyValuePair<int, List<int>> transfer in preset.EnemyIDs.OrderBy(e => e.Key))
                {
                    forceMap[transfer.Key] = transfer.Value.Count == 1 ? transfer.Value[0] : Choice(forcemapRandom, transfer.Value);
                }
                // Also try to implement norandom as enemies mapping to themselves
                foreach (int norandom in preset.DontRandomizeIDs)
                {
                    forceMap[norandom] = norandom;
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
                // Always place an enemy if target is already selected
                if (forceMap.ContainsKey(target)) return true;
                EnemyInfo sourceInfo = infos[source];
                EnemyInfo targetInfo = infos[target];
                // Custom exclusions
                if (targetInfo.HasTag($"exclude:{source}"))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} since it is specifically excluded from there");
                    return false;
                }
                // Boss falls off
                if (targetInfo.HasTag("island") && sourceInfo.HasTag("noisland"))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} since it doesn't work well on an island");
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
                // Progression options which are hard requirements
                if ((targetInfo.Class == EnemyClass.Miniboss && opt["earlyreq"]) || (targetInfo.Class == EnemyClass.Boss && opt["phasebuff"]))
                {
                    if (targetInfo.Tags != null && targetInfo.HasTag("earlyreq"))
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
                // Phase equalization, run a bunch of heuristics
                if (prevLimits != null && targetInfo.Class == EnemyClass.Boss)
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
                if (randomizeOpt.TryGetValue(siloType, out string optName) && !opt[optName]) continue;

                // If no actual sources added (could happen with some combination of forcemap and source removal), just add all of them to the default pool
                int randomCount = 0;
                Random makeRandom()
                {
                    return new Random(seed + (int)silo.Type + (randomCount++) * 10);
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

                    // Get some random early game bosses, unique by type
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
                if ((custom == null && adds == null) || siloType != EnemyClass.Basic)
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
                    List<EnemyClass> printSilos = new List<EnemyClass> { siloType };
                    if (siloType == EnemyClass.Boss)
                    {
                        printSilos.Add(EnemyClass.TutorialBoss);
                        printSilos.Add(EnemyClass.FoldingMonkey);
                    }
                    foreach (EnemyClass printSilo in printSilos)
                    {
                        foreach (KeyValuePair<int, int> transfer in silos[printSilo].Mapping)
                        {
                            Console.WriteLine($"Replacing {fullName(transfer.Key, true)}: {fullName(transfer.Value, false)}");
                        }
                    }
                }
                Console.WriteLine();
#if !DEBUG
                for (int i = 0; i < 50; i++) Console.WriteLine();
#endif
            }

            // We may need to add a lot of entities (regions and helpers). Previously tried to use Abandoned Dungeon ranges, but that was too small.
            int entityBase = 1400000;
            int entityMax = 1500000;

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
                MSBS.Shape shape = (MSBS.Shape)Activator.CreateInstance(b.Shape.GetType());
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
            Vector3 floorBuffer = new Vector3(0, 3, 0);
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
                else if (scope == "event")
                {
                    // Transplant all regions
                }
                else throw new Exception($"Internal error: unknown scope in {spec}");
                string fromMap = defaultData[fromEntity].Map;
                int toEntity = reloc[fromEntity];
                string toMap = defaultData[toEntity].Map;
                MSBS.Part.Enemy e = maps[toMap].Parts.Enemies.Find(en => en.EntityID == toEntity);
                if (e == null) throw new Exception($"Internal error: can't find {toEntity} in {toMap} for {spec}");
                Dictionary<int, RegionTarget> res = new Dictionary<int, RegionTarget>();
                if (type == "chrpoint")
                {
                    foreach (int region in regions)
                    {
                        (MSBS.Region a, MSBS.Region b) = getRegionCopy(fromMap, toMap, region, replace);
                        b.Position = e.Position;
                        b.Rotation = e.Rotation;
                        res[region] = RegionTarget.ID(b.EntityID);
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
                        (MSBS.Event.Generator newGen, MSBS.Region b) = getGeneratorCopy(fromMap, toMap, region, reloc);
                        if (newGen != null)
                        {
                            b.Position = arena == null ? e.Position : randomPoint(arena);
                            res[region] = RegionTarget.ID(newGen.EntityID);
                        }
                    }
                }
                else if (type.StartsWith("arena"))
                {
                    Arena arena = infos[toEntity].ArenaData;
                    // TODO: Figure out something better to do here
                    if (infos[toEntity].Class == EnemyClass.Basic) return res;
                    if (expectArena && arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    List<Vector3> corners = null;
                    if (arena != null && type == "arenapartition")
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
                        res[region] = RegionTarget.ID(b.EntityID);
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
                            if (arena == null)
                            {
                                box.Width = arena.Box.X;
                                box.Height = arena.Box.Y + 3;
                                box.Depth = arena.Box.Z;
                                b.Position = Vector3.Subtract(arena.Pos, floorBuffer);
                                b.Rotation = arena.Rot;
                            }
                            else
                            {
                                box.Width = box.Height = box.Depth = 10;
                                b.Position = Vector3.Subtract(e.Position, floorBuffer);
                                b.Rotation = e.Rotation;
                            }
                        }
                        else if (type == "arenaowl")
                        {
                            // Note: Currently unused. Tried to use it to safe Owl but to no avail
                            Arena owlArena = infos[toEntity].OwlArenaData;
                            if (owlArena == null)
                            {
                                // Try to make this never activate, by putting a very far away distance from player
                                res[region] = RegionTarget.Dist(100000);
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
                        else if (type == "arenapoint" || type == "arenasfx" || type == "arenabox" || (arena == null && type == "arenaposition"))
                        {
                            // Just move to center for now
                            if (arena == null)
                            {
                                b.Position = e.Position;
                            }
                            else
                            {
                                b.Position = (type == "arenabox" || originalCount > 1) ? randomPoint(arena) : arena.Pos;
                            }
                            // Shapes: point, sphere, box
                            // Only box is used for detection. Points used for spawning/warping enemies, spheres used for Isshin fire
                            if (type == "arenabox" && b.Shape is MSBS.Shape.Box box)
                            {
                                box.Height += 3;
                                b.Position = Vector3.Subtract(b.Position, floorBuffer);
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
                            b.Position = Vector3.Subtract(b.Position, floorBuffer);
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
                        res[region] = RegionTarget.Dist(dist);
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

            // Do NPC param edits

            // Remove butterfly invisibility (3509210)
            Params["NpcParam"][50900001]["spEffectId25"].Value = -1;

            // Start Owl 2 with his sword out
            PARAM.Row owl2Npc = Params["NpcParam"][50601010];
            owl2Npc["ModelDispMask2"].Value = (byte)1;
            owl2Npc["ModelDispMask3"].Value = (byte)0;
            owl2Npc["ModelDispMask4"].Value = (byte)1;

            // Duplicate npc params
            // For bosses, make a copy of them with XP drops (Experience) and money drops (getSoul)
            // For minibosses, prepare to make a copy of them if regular enemies go there, with modified Hp, stamina/staminaRecoverBaseVal?, and HealthbarNum
            // what id disableInitializeDead?
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
            Random reward = new Random((int)opt.Seed + 40);
            foreach (EnemyInfo info in infos.Values.Where(i => i.Class == EnemyClass.Boss || i.Class == EnemyClass.FoldingMonkey || i.Class == EnemyClass.TutorialBoss).OrderBy(i => i.ID))
            {
                int source = info.ID;
                int baseNpc = defaultData[source].NPC;
                PARAM.Row row = duplicateNpcParam(baseNpc);
                // Sen
                row["getSoul"].Value = info.SourcePhases * (info.HasTag("early") ? reward.Next(50, 100) : (info.HasTag("mid") ? reward.Next(100, 150) : reward.Next(100, 200)));
                // XP - more lenient values, closer to minibosses
                // row["Experience"].Value = info.SourcePhases * (info.HasTag("early") ? reward.Next(100, 200) : (info.HasTag("mid") ? reward.Next(150, 250) : reward.Next(200, 300)));
                // XP - more strict values, closer to regular enemies, since they can just fall off
                row["Experience"].Value = info.SourcePhases * (info.HasTag("early") ? reward.Next(25, 50) : (info.HasTag("mid") ? reward.Next(50, 75) : reward.Next(50, 100)));
                row["disableIntiliazeDead"].Value = (byte)0;
                bossAsBasicNpc[source] = (int)row.ID;
            }

            // Lazily make copies of regular enemies as specific bosses/minibosses, will basically only apply in 'oops all' mode.
            // Miniboss targets should only occur once so this does not need to be memoized.
            List<string> basicBuffFields = new List<string> { "Experience", "getSoul", "Hp", "stamina", "staminaRecoverBaseVal", "HealthbarNum" };
            int getBasicAsBoss(int source, int target)
            {
                int baseNpc = defaultData[source].NPC;
                if (preset != null && !preset.BuffBasicEnemiesAsBosses) return baseNpc;

                PARAM.Row targetRow = Params["NpcParam"][defaultData[target].NPC];
                if (targetRow == null) return baseNpc;

                string model = game.ModelName(defaultData[source].Model);
                if (ann.NoDeathblow != null && ann.NoDeathblow.Contains(model)) return baseNpc;

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
                if (infos.TryGetValue(target, out EnemyInfo targetInfo) && targetInfo.IsBossTarget)
                {
                    row["HealthbarNum"].Value = (byte)targetInfo.Phases;
                }
                row["disableIntiliazeDead"].Value = (byte)1;
                return (int)row.ID;
            }

            int helperModelBase = 100;
            // Mapping of enemies which are randomized, from source to target
            Dictionary<int, List<int>> mapping = new Dictionary<int, List<int>>();
            // Another one from target to source
            Dictionary<int, int> revMapping = new Dictionary<int, int>();
            // Mapping of new helpers, from original owner and original helper to target
            Dictionary<(int, int), int> helperMapping = new Dictionary<(int, int), int>();

            Dictionary<int, int> totalTargetCounts = silos.Values.SelectMany(s => s.Mapping).GroupBy(e => e.Value).ToDictionary(e => e.Key, e => e.Count());
            bool withinMaxAllowed(int source, int target, int amount, bool lenient = false)
            {
                // Generally allow all named targets (around 40 of these in the game)
                if (infos[target].IsNamedTarget) return true;
                // If too many of a given type of enemy and all of their helpers, the game will crash.
                if (totalTargetCounts.TryGetValue(source, out int targetCount) && targetCount >= amount)
                {
                    // In some cases, perhaps allow a propotional approach
                    if (lenient) return target.GetHashCode() % targetCount < amount;
                    return false;
                }
                return true;
            }
            bool multichrSwitch = true;
            bool enableMultichr(int source, int target)
            {
                if (!multichrSwitch) return false;
                if (source == 1000353 && !withinMaxAllowed(source, target, 60, true)) return false;
                if (source == 2500850 && !withinMaxAllowed(source, target, 60)) return false;
                if (source == 1000810 && !withinMaxAllowed(source, target, 40)) return false;
                return true;
            }
            int partsNeeded = totalTargetCounts.Where(s => infos[s.Key].HasTag("npcpart")).Sum(s => s.Value);
            // This seems to be a safe global limit. There can be no more than a handful of npc parts loaded at once.
            bool partsRestricted = partsNeeded >= 48;

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
                        if (!infos[target].IsBossTarget && bossAsBasicNpc.TryGetValue(source, out int sourceNpc))
                        {
                            e.NPCParamID = sourceNpc;
                        }
                        else if ((infos[source].Class == EnemyClass.Basic || infos[source].Class == EnemyClass.FoldingMonkey) && infos[target].IsNamedTarget)
                        {
                            e.NPCParamID = getBasicAsBoss(source, target);
                        }

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
                            if (source == 1110900 || source == 1700850 || source == 1110801)
                            {
                                e.Position = new Vector3(-239.089f, -787.188f, 583.271f);
                            }
                        }
                        // Seven Spears can get some big enemies stuck in the Moon-View Tower
                        if (target == 1120530)
                        {
                            if (source == 1700800 || source == 1700850 || source == 1100800 || source == 1100900)
                            {
                                e.Position = new Vector3(-189.066f, -37.287f, 345.113f);
                            }
                        }
                        if (target == 1700200)
                        {
                            // Move Gun Fort Snake Eyes out of the wall
                            if (infos[source].IsBossTarget || infos[source].EnemyType == "chainedogre")
                            {
                                e.Position = new Vector3(-234.235f, -190.218f, 517.457f);
                            }
                        }
                        if (infos[source].HasTag("hidden"))
                        {
                            // Don't allow wooooooo enemies or true monk to walk around, as this interrupts their invisibility
                            e.UnkT20 = -1;
                        }
                        if (infos[source].Regions != null && totalTargetCounts.TryGetValue(source, out int targetCount) && targetCount == 1)
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
                        revMapping[target] = source;
                        ownerMap[target] = entry.Key;
                        // Also add minions
                        if (owners.TryGetValue(source, out List<int> helpers))
                        {
                            Arena arena = infos[target].ArenaData;
                            foreach (int helper in helpers)
                            {
                                if (!enableMultichr(source, target)) continue;
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
                                int target2 = newEntity();
                                e2.EntityID = target2;
                                e2.Name = $"{e2.ModelName}_{helperModelBase++:d4}";
                                // Clear out things that should never apply to helpers? TODO check these
                                for (int i = 0; i < e2.EntityGroupIDs.Length; i++) e2.EntityGroupIDs[i] = -1;
                                e2.UnkT24 = 0;  // Causes main boss to break
                                msb.Parts.Enemies.Add(e2);
                                useModel(e2.ModelName);
                                helperMapping[(target, helper)] = target2;
                                revMapping[target2] = helper;
                                ownerMap[target2] = entry.Key;
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
            HashSet<int> endCond2s = new HashSet<int>();
            // Also startcmds
            Dictionary<int, EnemyTemplate> startEvents = templates.Values
                .Where(ev => ev.Template.Any(t => t.Type.StartsWith("start") && t.StartCmd != null))
                .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("start")));
            Dictionary<int, List<EMEVD.Instruction>> startCmds = new Dictionary<int, List<EMEVD.Instruction>>();
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
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

            // System for transplanting camera ids
            // Map from entity id -> starting camera id
            Dictionary<int, int> startCameras = templates.Values
                .SelectMany(ev => ev.Template.Where(t => t.Type.StartsWith("start") && t.Entity != 0 && t.Camera != null))
                .ToDictionary(t => t.Entity, t => int.Parse(t.Camera));
            // Map from (full source id, target map) -> partial target id
            Dictionary<(int, int), int> movedCameraSets = new Dictionary<(int, int), int>();
            int TransplantCameraParam(int id, int source, int target)
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
                PARAM.Row sourceRow = Params["CameraSetParam"][sourceId];
                if (sourceRow == null) throw new Exception($"Error transplating camera {id} from {source} to {target}: source camera {sourceId} not found");
                if (!movedCameraSets.TryGetValue((sourceId, targetMap), out int targetId))
                {
                    targetId = 0;
                    for (int i = 500; i < 600; i++)
                    {
                        if (Params["CameraSetParam"][targetMap + i] == null)
                        {
                            targetId = i;
                            break;
                        }
                    }
                    if (targetId == 0) throw new Exception("Could not transplant camera??");
                    PARAM.Row targetRow = game.AddRow("CameraSetParam", targetMap + targetId);
                    GameEditor.CopyRow(sourceRow, targetRow);
                    movedCameraSets[(sourceId, targetMap)] = targetId;
                }
                return targetId;
            }

            // Also, name generation stuff
            FMG baseNameFmg = game.BaseItemFMGs["NPC名"];
            FMG nameFmg = game.ItemFMGs["NPC名"];
            int baseNameId = 902000;
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
                    // Use the part name, otherwise keep things simple and use the model name
                    string sourceName = infos[source].PartName ?? game.ModelName(defaultData[source].Model);
                    string fullName = infos[target].FullName != null ? infos[target].FullName.Replace("$1", sourceName) : sourceName;
                    nameId = baseNameId++;
                    nameIds[(target, id)] = nameId;
                    nameFmg[nameId] = fullName;
                    // Console.WriteLine($"Replacement for {id} -> {nameId} - source {ename(source)} -> target {ename(target)}: {fullName}");
                }
                return nameId;
            }

            Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>> newInitializations = new Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>>();
            // Don't redo non-chr event templates. For now, just identity references. (TODO: also include things like target entity or arg list in this set?)
            HashSet<EnemyTemplate> completedTemplates = new HashSet<EnemyTemplate>();
            HashSet<int> removedEvents = new HashSet<int>();
            HashSet<int> usedEvents = new HashSet<int>();
            bool warnBadCommands = false;
            Dictionary<int, EMEVD.Event> commonEvents = emevds["common_func"].Events.ToDictionary(e => (int)e.ID, e => e);
            HashSet<int> removeImmortality = new HashSet<int>(infos.Values
                .Where(info => info.IsMortalBoss)
                .SelectMany(info => mapping.TryGetValue(info.ID, out List<int> targets) ? targets : new List<int> { }));
            // From source event flag and target to target event flag
            Dictionary<(int, int), int> progressFlags = new Dictionary<(int, int), int>();
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
                        Instr originalInit = events.Parse(e.Instructions[i]);
                        if (!originalInit.Init) continue;
                        int callee = originalInit.Callee;
                        if (!templates.TryGetValue(callee, out EventSpec ev)) continue;
                        if (ev.Template.Count == 0) throw new Exception($"event {callee} has no templates");
                        // chr, multichr, loc, start, end, startphase, endphase, remove
                        if (ev.Template[0].Type == "remove")
                        {
                            // If the target comes from somewhere else, remove it
                            if (ev.Template[0].Entity <= 0 || revMapping.ContainsKey(ev.Template[0].Entity))
                            {
                                // Remove action by removing initialization, for now. Can garbage collect later if desired.
                                e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                                removedEvents.Add(callee);
                            }
                            else
                            {
                                usedEvents.Add(callee);
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
                            int argEntity = originalInit.Args.Skip(originalInit.Offset).Where(a => a is int ai && (infos.ContainsKey(ai) || ignoreEnemies.Contains(ai))).FirstOrDefault() is int aj ? aj : 0;
                            if (entity <= 0)
                            {
                                if (argEntity == 0) throw new Exception($"No entity found in {originalInit}) args");
                                entity = argEntity;
                            }
                            // Find targets
                            if (mapping.TryGetValue(entity, out List<int> targets))
                            {
                                targets = targets.ToList();
                            }
                            else
                            {
                                targets = new List<int>();
                            }
                            // If the entity remains the same, don't remove the original... unless it's a deathblow, in which case it must be rewritten
                            if (!revMapping.ContainsKey(entity))
                            {
                                if (t.Deathblow == 0)
                                {
                                    canRemove = false;
                                }
                                else
                                {
                                    targets.Add(entity);
                                }
                            }
                            // # of events should not be a problem, since there is a global multichr limit for some enemies, but we'll see
                            if (t.Type == "multichronly")
                            {
                                targets.RemoveAll(target => !enableMultichr(entity, target));
                            }
                            if (t.Type == "chrpart" && partsRestricted)
                            {
                                targets.RemoveAll(target => !infos[target].IsNamedTarget);
                            }
                            // If no targets left at this point, nothing to do
                            if (targets.Count == 0) continue;

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
                            if (t.Type.Contains("chr")) continue;

                            if (t.Entity > 0)
                            {
                                // Something must be randomized to this target if entity is specified
                                if (!revMapping.TryGetValue(t.Entity, out int source))
                                {
                                    continue;
                                }
                                // Unused feature: source-target pair. This was needed at some point, but keeping it in any case.
                                if (t.Transfer > 0 && t.Transfer != source)
                                {
                                    continue;
                                }
                            }
                            if (t.IsDefault() && t.Type == "loc")
                            {
                                // This is fine, nothing to do if not chr. Except starts may need startcmds
                            }
                            else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                            {
                                if (completedTemplates.Contains(t)) continue;
                                completedTemplates.Add(t);
                                eventCopies.Add((0, 0, theEvent, t));
                            }
                            else if (t.Type.StartsWith("common") && commonEvents.TryGetValue(callee, out EMEVD.Event comEvent))
                            {
                                if (completedTemplates.Contains(t)) continue;
                                if (t.Type == "common") completedTemplates.Add(t);
                                eventCopies.Add((0, 0, comEvent, t));
                            }
                            else throw new Exception($"Can't find event {callee} with {t.Type} template");
                        }
                        foreach (var copy in eventCopies)
                        {
                            (int entity, int target, EMEVD.Event e2, EnemyTemplate t) = copy;
                            // if (target == 1110680) continue;  // Temp way to figure out true monk issues
                            // Main entities to replace
                            Dictionary<int, int> reloc = new Dictionary<int, int>();
                            Dictionary<int, int> distReplace = new Dictionary<int, int>();
                            Instr init = originalInit;
                            if (entity != 0)
                            {
                                reloc[entity] = target;
                                if (t.Type.StartsWith("multichr") && owners.TryGetValue(entity, out List<int> helpers))
                                {
                                    foreach (int helper in helpers)
                                    {
                                        if (infos[helper].Class != EnemyClass.Helper) continue;
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
                                // TODO: Find a better way to get rid of regions in the templates themselves
                                if (t.Regions != null)
                                {
                                    bool expectArena = infos[target].IsNamedTarget;
                                    foreach (string spec in t.Regions)
                                    {
                                        foreach (KeyValuePair<int, RegionTarget> region in copyRegions(spec, entity, init.Args.SelectMany(o => o is int oi ? new[] { oi } : new int[] { }).ToList(), reloc, expectArena: expectArena))
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
                                // Can't allocate enough valid flags for this
                                if (t.ProgressFlag != null && false)
                                {
                                    foreach (string progressFlag in t.ProgressFlag.Split(' '))
                                    {
                                        int flag = int.Parse(progressFlag);
                                        if (!progressFlags.TryGetValue((flag, target), out int targetFlag))
                                        {
                                            targetFlag = progressFlags[(flag, target)] = events.NewID(true);
                                        }
                                        reloc[flag] = targetFlag;
                                    }
                                }
                                // Also we need to make a copy of the instruction at this point, so we don't edit the original
                                init = events.CopyInit(init, e2);
                            }
                            // Renamings in the args
                            if (t.Name != null && t.Name.StartsWith("X"))
                            {
                                string[] nameParts = t.Name.Split(' ');
                                if (!events.ParseArgSpec(nameParts[0], out int entityPos) || !events.ParseArgSpec(nameParts[1], out int namePos)) throw new Exception($"Bad name space {t.Name} for {callee}");
                                int nameTarget = (int)init[entityPos + init.Offset];
                                if (revMapping.TryGetValue(nameTarget, out int nameSource))
                                {
                                    int nameId = (int)init[namePos + init.Offset];
                                    reloc[nameId] = GetCleverName(nameId, nameSource, nameTarget);
                                }
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
                            if (t.RemoveDupe != null && target != 0 && !infos[target].IsBossTarget)
                            {
                                foreach (string remove in Regex.Split(t.RemoveDupe, @"\s*;\s*"))
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
                                    if (targetInfo.DefeatFlag == 0)
                                    {
                                        if (targetInfo.IsNamedTarget) throw new Exception($"{target} has no defeat flag defined, but was randomized to {entity} in {callee}");
                                        // This will probably work for minibosses -> enemies, but will it work for bosses -> enemies? Should a temp flag be set up?
                                        events.RemoveMacro(edits, t.DefeatFlag.ToString());
                                    }
                                    else
                                    {
                                        events.ReplaceMacro(edits, t.DefeatFlag.ToString(), targetInfo.DefeatFlag.ToString());
                                    }
                                }
                                if (t.StartFlag != 0)
                                {
                                    if (targetInfo.StartFlag == 0)
                                    {
                                        // TODO: To what extent is this needed
                                        if (targetInfo.IsBossTarget) throw new Exception($"{target} has no start flag defined, but was randomized to {entity} in {callee}");
                                        events.RemoveMacro(edits, t.StartFlag.ToString());
                                    }
                                    else
                                    {
                                        events.ReplaceMacro(edits, t.StartFlag.ToString(), targetInfo.StartFlag.ToString());
                                    }
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
                            if (t.Camera != null)
                            {
                                if (t.Entity == 0) throw new Exception($"Internal error: Camera defined for {callee} but no owner entity");
                                List<int> cameras = Regex.Split(t.Camera, @"\s*;\s*").Select(c => int.Parse(c)).ToList();
                                if (entity == 0)
                                {
                                    // Moving camera from other entity to this one, a boss, at the start
                                    if (!t.Type.StartsWith("start")) throw new Exception($"Internal error: Non-start in-place camera for {callee}");
                                    int cameraSource = revMapping[t.Entity];
                                    if (startCameras.TryGetValue(cameraSource, out int startCamera))
                                    {
                                        foreach (int camera in cameras)
                                        {
                                            int targetCamera = TransplantCameraParam(startCamera, cameraSource, t.Entity);
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
                                    if (infos[target].IsBossTarget)
                                    {
                                        foreach (int camera in cameras)
                                        {
                                            int targetCamera = TransplantCameraParam(camera, entity, target);
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
                            }
                            if (e2 != null)
                            {
                                OldParams pre = OldParams.Preprocess(e2);
                                if (t.Add != null)
                                {
                                    events.AddMacro(edits, t.Add);
                                }
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
                                        if (instr.Name == "IF Condition Group" && instr[0].ToString() == "0")
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
                                // One more special type of processing, invincibility resets immortality so it should be added for bosses
                                if (t.Invincibility != 0)
                                {
                                    int invTarget = t.Invincibility;
                                    if (revMapping.TryGetValue(invTarget, out int invSource) && infos[invSource].IsImmortal)
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
                                            if (t.Entity == nameEntity)
                                            {
                                                instr[3] = GetCleverName((int)instr[3], nameSource, t.Entity);
                                            }
                                        }
                                        instr.Save();
                                    }
                                }
                                events.ApplyAdds(edits, e2);
                                if (t.Type.StartsWith("start"))
                                {
                                    int source = revMapping[t.Entity];
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
                            removedEvents.Add(callee);
                        }
                        else
                        {
                            usedEvents.Add(callee);
                        }
                    }
                    initOld.Postprocess();
                }
            }
            foreach (KeyValuePair<int, int> transfer in revMapping)
            {
                int target = transfer.Key;
                int source = transfer.Value;
                EnemyInfo info = infos[source];
                // Make all immortal boss targets immortal by default
                if (infos[source].IsImmortal)
                {
                    // Should this be in a dedicate event? This is potentially a lot of events
                    if (infos[target].DefeatFlag != 0)
                    {
                        // AddMulti(newInitializations, ownerMap[target], (events.ParseAdd($"SKIP IF Event Flag (1,1,0,{infos[target].DefeatFlag})"), null));
                    }
                    AddMulti(newInitializations, ownerMap[target], (events.ParseAdd($"Set Character Immortality ({target},1)"), null));
                    // Except when they die in the spot of a non-boss, make sure that goes through, even if it looks hacky
                    if (infos[target].Class != EnemyClass.Boss)
                    {
                        if (endConds.TryGetValue((source, 1), out List<EMEVD.Instruction> after) || endConds.TryGetValue((source, 0), out after))
                        {
                            EMEVD.Event ev = new EMEVD.Event(events.NewID(true), EMEVD.Event.RestBehaviorType.Restart);
                            ev.Instructions.AddRange(events.RewriteCondGroup(after, new Dictionary<int, int> { { source, target } }, 0));
                            // ev.Instructions.Add(events.ParseAdd($"Award Item Lot (60220)"));
                            ev.Instructions.Add(events.ParseAdd($"WAIT Fixed Time (Seconds) (5)"));
                            ev.Instructions.Add(events.ParseAdd($"IF Character Dead/Alive (0,{target},0,0,1)"));
                            ev.Instructions.Add(events.ParseAdd($"Force Character Death ({target},1)"));
                            // Unfortunately, it seems like Fire Isshin can never actually die
                            if (source == 1110920) ev.Instructions.Add(events.ParseAdd($"Change Character Enable State ({target},0)"));
                            // ev.Instructions.Add(events.ParseAdd($"Award Item Lot (60230)"));
                            EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 });
                            AddMulti(newInitializations, ownerMap[target], (init, ev));
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
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
                        foreach (EMEVD.Instruction newEvent in newEvents.Select(n => n.Item1))
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

            // An edit for roberto
            {
                ESD warrior = game.Talk()["m20_00_00_00"]["t200500"];
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
            }

            // Other param edits
            // Most minibosses in earlier spots are manageable, but, Sakura Bull is a bit too tanky. Bring it down to Blazing Bull level
            if (mapping.TryGetValue(2500570, out List<int> sakuraTarget) && infos[sakuraTarget[0]].HasTag("early")) Params["NpcParam"][13800000]["Hp"].Value = 2232;

            if (false && revMapping.TryGetValue(1500800, out int newMonk) && totalTargetCounts.TryGetValue(newMonk, out int monkAmount) && monkAmount == 1)
            {
                // Can look into adding purple speffect/material for corrupted monk, if we can find it
            }

            // Make Sword Saint Isshin deathblow not require Mortal Blade
            Params["ThrowKindParam"].Rows.Add(new PARAM.Row(250001, null, Params["ThrowKindParam"].AppliedParamdef));
            GameEditor.CopyRow(Params["ThrowKindParam"][250000], Params["ThrowKindParam"][250001]);
            Params["ThrowKindParam"][250001]["Mask4"].Value = (byte)0xFF;
            Params["ThrowParam"][15400590]["ThrowKindParamID0"].Value = 250001;

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
        public void CopyAll<T>(T source, T target)
        {
            var type = source.GetType();
            if (source.GetType() != target.GetType()) throw new Exception($"Internal error: trying to copy {source} to {target} but their types differ");
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
                else if (sourceProperty.SetMethod == null)
                {
                    // If a passthrough accessor, that is fine
                }
                else throw new Exception($"Can't move field {sourceProperty.Name} in {type.Name}");
            }
        }
    }
}
