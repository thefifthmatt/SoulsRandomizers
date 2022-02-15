using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;
using static SoulsIds.Events;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Preset;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class EnemyConfigGen
    {
        private GameData game;
        private Events events;
        private EventConfig eventConfig;

        public EnemyConfigGen(GameData game, Events events, EventConfig eventConfig)
        {
            this.game = game;
            this.events = events;
            this.eventConfig = eventConfig;
        }

        private static readonly Dictionary<string, List<string>> fieldOrder = new Dictionary<string, List<string>>
        {
            ["EventSpec"] = new List<string> { "ID", "Comment", "Dupe", "Template", "ItemTemplate", "DebugInfo", "DebugInit", "DebugCommands" },
        };

        private class CustomOrderInspector : TypeInspectorSkeleton
        {
            private readonly ITypeInspector inner;

            public CustomOrderInspector(ITypeInspector inner)
            {
                this.inner = inner;
            }

            public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
            {
                return inner.GetProperties(type, container).OrderBy(x =>
                {
                    if (fieldOrder.TryGetValue(type.Name, out List<string> fields))
                    {
                        int index = fields.IndexOf(x.Name);
                        if (index != -1) return index;
                    }
                    return 999;
                });
            }
        }

        private static ISerializer MakeSerializer()
        {
            return new SerializerBuilder().DisableAliases().WithTypeInspector(x => new CustomOrderInspector(x)).Build();
        }

        public void WriteDS3Enemies()
        {
            EnemyAnnotations outYaml = new EnemyAnnotations();

            EMEDF doc = EMEDF.ReadFile(@"dist\Base\ds3-common.emedf.json");
            EMEDF.EnumDoc teamEnum = doc.Enums.First(e => e.Name == "Team Type");
            HashSet<string> noneTeams = new HashSet<string> { "WhitePhantom", "FriendlyNPC", "Ally", "Object", "RedBerserker", "HostileNPC", "Indiscriminate" };

            HashSet<int> entityIds = new HashSet<int>();
            Dictionary<int, List<int>> groupIds = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                MSB3 msb = entry.Value;
                foreach (MSB3.Part.Enemy e in msb.Parts.Enemies)
                {
                    entityIds.Add(e.EntityID);
                    entityIds.UnionWith(e.EntityGroups.Where(i => i > 0));
                }
            }
            SortedDictionary<int, EventDebug> enemyEvents = events.GetHighlightedEvents(game.Emevds, entityIds);

            int newIdBase = 2888000;
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;

                foreach (MSB3.Part.Enemy e in msb.Parts.Enemies)
                {
                    // Note: First group may be empty but others may have ids.
                    List<int> groups = e.EntityGroups.Where(i => i > 0).ToList();
                    string idStr = $"id {e.EntityID}" + (groups.Count > 0 ? $" ({string.Join(",", groups)})" : "");
                    int teamNum = (byte)game.Params["NpcParam"][e.NPCParamID]["teamType"].Value;
                    string team = teamEnum.Values.TryGetValue(teamNum.ToString(), out string val) ? Regex.Replace(val, @"[^\w]", "") : teamNum.ToString();
                    string name = game.ModelCharacterName(e.ModelName, e.CharaInitID);
                    int entityId = e.EntityID;
                    if (entityId == -1)
                    {
                        entityId = newIdBase++;
                    }
                    EnemyInfo info = new EnemyInfo
                    {
                        ID = entityId,
                        Map = entry.Key,
                        Name = e.Name,
                        DebugText = $"{map} - {e.Name}"
                            + $" - npc {e.NPCParamID} - think {e.ThinkParamID}"
                            + $" - team {team}"
                            + (e.CharaInitID > 0 ? $" - chr {e.CharaInitID}" : "")
                            + $" - {idStr} - {name}",
                        // Class = EnemyClass.Other,
                    };
                    // Some attempted heuristics...
                    if (noneTeams.Contains(team) || e.ModelName == "c0000")
                    {
                        // Maybe not gravetender/halflight though
                        // info.Class = EnemyClass.Non;
                    }
                    else if (team == "Enemy")
                    {
                        info.Class = EnemyClass.Basic;
                    }
                    if (e.TalkID > 0)
                    {
                        info.ESDs = $"{e.TalkID}";
                    }
                    SortedSet<string> eventRefs = new SortedSet<string>();
                    if (e.EntityID > 0 || groups.Count > 0)
                    {
                        if (e.EntityID > 0)
                        {
                            groups.Insert(0, e.EntityID);
                        }
                        foreach (int id in groups)
                        {
                            foreach (EventDebug ev in enemyEvents.Values)
                            {
                                if (!ev.IDs.Contains(id)) continue;
                                foreach (InstructionDebug instr in ev.Instructions)
                                {
                                    if (instr.IDs.Contains(id))
                                    {
                                        eventRefs.Add(instr.Name);
                                    }
                                }
                            }
                        }
                        if (eventRefs.Count > 0)
                        {
                            info.Events = string.Join("; ", eventRefs);
                        }
                    }
                    // Can also print out teamType and npcType from 
                    outYaml.Enemies.Add(info);
                }
            }
            // For mass updates to the enemy file, a bit hacky, but it is at least a possible migration path
            // using (var writer = File.CreateText("newenemy.txt")) writer.Write(file);
            ISerializer serializer = MakeSerializer();
            using (var writer = File.CreateText("enemy.txt"))
            {
                serializer.Serialize(writer, outYaml);
            }
        }

        public void WriteSekiroEnemies()
        {
            EnemyAnnotations outYaml = new EnemyAnnotations();
            foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
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
                    if (talks.TryGetValue(e.Name, out MSBS.Event.Talk talk))
                    {
                        info.ESDs = string.Join(",", talk.TalkIDs);
                    }
                    // The old system for setting events is removed. Can reimplement this mapping if needed.
                    // if (idCommands.TryGetValue(e.EntityID, out List<InstructionDebug> usages)) info.Events = string.Join("; ", usages.Select(u => u.Name).Distinct());
                    // Can also print out teamType and npcType from 
                    outYaml.Enemies.Add(info);
                }
            }
            // For mass updates to the enemy file, a bit hacky, but it is at least a possible migration path
            // using (var writer = File.CreateText("newenemy.txt")) writer.Write(file);
            ISerializer serializer = MakeSerializer();
            using (var writer = File.CreateText("enemy.txt"))
            {
                serializer.Serialize(writer, outYaml);
            }
        }

        public void WriteDS3Chests(LocationData data)
        {
            Dictionary<int, ChestInfo> chests = new Dictionary<int, ChestInfo>();
            Dictionary<int, List<SlotKey>> chestItems = new Dictionary<int, List<SlotKey>>();

            // This should probably iterate over data.Locations instead, but the sunk cost is already too great
            foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
            {
                foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                {
                    foreach (LocationKey loc in itemLoc.Keys.Where(k => k.Type == LocationKey.LocationType.LOT))
                    {
                        foreach (EntityId chest in loc.Entities.Where(l => l.ModelName == "o000200"))
                        {
                            int id = chest.EntityID;
                            if (id <= 0) throw new Exception($"Uneventful chest {chest}");
                            if (!chests.TryGetValue(id, out ChestInfo info))
                            {
                                string map = game.RevLocations[chest.MapName];
                                chests[id] = info = new ChestInfo
                                {
                                    ID = id,
                                    Map = map,
                                    Name = chest.EntityName,
                                    EventFlag = -1,
                                    ClosestEnemy = -1,
                                    ClosestMimic = -1,
                                };
                                MSB3.Event.ObjAct act = game.DS3Maps[map].Events.ObjActs.Find(oa => oa.ObjActPartName == info.Name);
                                if (act == null) throw new Exception($"Unopenable (?) chest {chest}");
                                info.DebugInfo = $"{chest.MapName} - objact {act.ObjActEntityID} - ";
                            }
                            AddMulti(chestItems, id, new SlotKey(entry.Key, itemLoc.Scope));
                        }
                    }
                }
            }
            foreach (KeyValuePair<int, List<SlotKey>> entry in chestItems)
            {
                SortedDictionary<int, string> lots = new SortedDictionary<int, string>();
                foreach (SlotKey slotKey in entry.Value)
                {
                    ItemLocation itemLoc = data.Location(slotKey);
                    LocationKey lot = itemLoc.Keys[0];
                    lots[lot.ID] = $"{lot.ID}[flag {itemLoc.LocScope.UniqueId}] {game.Name(slotKey.Item)}";
                    if (itemLoc.Quantity > 1) lots[lot.ID] += $" {itemLoc.Quantity}x";
                }
                ChestInfo info = chests[entry.Key];
                info.ItemLot = lots.Keys.Min();
                info.EventFlag = (int)game.Params["ItemLotParam"][info.ItemLot]["getItemFlagId"].Value;
                info.DebugInfo += string.Join(", ", lots.Values);
            }
            ISerializer serializer = MakeSerializer();
            serializer.Serialize(Console.Out, chests.Values.OrderBy(c => (c.Map, c.Name)));
        }

        public void WriteDS3Events(RandomizerOptions opt, Dictionary<int, EnemyInfo> infos, Dictionary<int, EnemyData> defaultData)
        {
            // Collect all ids first
            HashSet<int> entityIds = new HashSet<int>();
            Dictionary<int, string> regionIds = new Dictionary<int, string>();
            Dictionary<int, List<int>> groupIds = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> generatorIds = new Dictionary<int, List<int>>();
            Dictionary<int, string> basicPartNames = new Dictionary<int, string>();
            foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSB3 msb = entry.Value;

                foreach (MSB3.Part e in msb.Parts.GetEntries())
                {
                    if (e.EntityID >= 3000000)
                    {
                        entityIds.Add(e.EntityID);
                        basicPartNames[e.EntityID] = $"{e.GetType().Name.ToLowerInvariant()} {e.EntityID} ({map} {e.Name})";
                        foreach (int id in e.EntityGroups)
                        {
                            if (id > 0)
                            {
                                AddMulti(groupIds, id, e.EntityID);
                                entityIds.Add(id);
                            }
                        }
                    }
                }
                foreach (MSB3.Region r in msb.Regions.GetEntries())
                {
                    if (r.EntityID < 3000000) continue;
                    regionIds[r.EntityID] = r.Name;
                    entityIds.Add(r.EntityID);
                }
                foreach (MSB3.Event.Generator gen in msb.Events.Generators)
                {
                    foreach (string partName in gen.SpawnPartNames)
                    {
                        if (partName == null) continue;
                        MSB3.Part.Enemy e = msb.Parts.Enemies.Find(p => p.Name == partName);
                        // There are a handful of dummyenemy entries, ignore those
                        if (e == null) continue;
                        AddMulti(generatorIds, gen.EntityID, e.EntityID);
                        entityIds.Add(gen.EntityID);
                    }
                }
            }

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
                                entityIds.Add(eventFlag);
                                // if (eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022)
                                {
                                    Console.WriteLine($"{eventFlag}: {game.Name(item.Key)}");
                                }
                            }
                        }
                    }
                }
                foreach (KeyValuePair<string, MSB3> entry in game.DS3Maps)
                {
                    if (!game.Locations.ContainsKey(entry.Key)) continue;
                    string map = game.Locations[entry.Key];
                    MSB3 msb = entry.Value;

                    foreach (MSB3.Part.Object e in msb.Parts.Objects)
                    {
                        if (e.EntityID <= 0) continue;
                        string modelName = game.ModelName(e.ModelName);
                        if (modelName.Contains("?")) continue;
                        treasureNames[e.EntityID] = $"{e.Name} - {modelName}";
                        entityIds.Add(e.EntityID);
                    }
                }
            }

            string quickId(int id) => quickRec(id, null);
            string quickRec(int id, int? parent)
            {
                if (regionIds.TryGetValue(id, out string region))
                {
                    return $"region {id} ({region})";
                }
                if (groupIds.ContainsKey(id))
                {
                    if (parent is int id2 && id == id2)
                    {
                        return "SELF";
                    }
                    return $"group {id} [{string.Join(", ", groupIds[id].Select(i => quickRec(i, id)))}]";
                }
                if (generatorIds.ContainsKey(id))
                {
                    if (parent is int id2 && id == id2)
                    {
                        return "SELF";
                    }
                    return $"generator {id} [{string.Join(", ", generatorIds[id].Select(i => quickRec(i, id)))}]";
                }
                if (flagItems.TryGetValue(id, out string item))
                {
                    return $"flag {id} ({item})";
                }
                if (treasureNames.TryGetValue(id, out string tr))
                {
                    return $"treasure {id} ({tr})";
                }
                if (!defaultData.TryGetValue(id, out EnemyData data))
                {
                    return basicPartNames.TryGetValue(id, out string basic) ? basic : $"{id} unknown";
                }
                string name = game.ModelCharacterName(data.Model, data.Char);
                return $"{id} ({game.Locations[data.Map]} {data.Name} - {name}"
                    + $"{(infos.TryGetValue(id, out EnemyInfo enemy) && enemy.Class == 0 ? "" : "")})";  // - not random
            }
            bool isEligible(int entityId)
            {
                if (opt["eventsitem"])
                {
                    return flagItems.ContainsKey(entityId) || treasureNames.ContainsKey(entityId);
                }
                else
                {
                    // return generatorIds.ContainsKey(entityId));
                    List<int> groupEntityIds = groupIds.TryGetValue(entityId, out List<int> gids) ? gids : new List<int> { entityId };
                    return groupEntityIds.Any(id => infos.TryGetValue(id, out EnemyInfo enemy) && enemy.Class != 0);
                }
            }
            EventSpec produceSpec()
            {
                if (!opt["eventsitem"])
                {
                    return new EventSpec
                    {
                        Template = new List<EnemyTemplate>
                            {
                                new EnemyTemplate
                                {
                                    Type = "chr loc start end remove xx",
                                    Entity = -1,
                                }
                            }
                    };
                }
                else
                {
                    return new EventSpec
                    {
                        ItemTemplate = new List<ItemTemplate>
                            {
                                new ItemTemplate
                                {
                                    Type = "item loc",
                                    EventFlag = "X0",
                                }
                            }
                    };
                }
            }

            HashSet<int> processEventsOverride = new HashSet<int>
            {
                13305209, // Crab speffect
                // 20005837, 20005838, 20005839, 13005817, 13015815, 13205864, 13415845, // ChangeCamera events
                // 13205360, // Summoner
                // 15105350, // Judicator region/speffect event
                // 15105200, 15105230, 15105231, 15105232, 15105234, 15105235, 15105236, 15105237 // Bridge Midir vanilla modifications
            };
            HashSet<int> processEntitiesOverride = new HashSet<int>
            {
                // 5110850, 3800499, // Shira, Sandworm
                // 5000200, 5000201, 5000202, 5000210, 5000211, 5000212,  // Angels
            };
            bool alwaysHighlight(Instr instr) => instr.Init && processEventsOverride.Contains(instr.Callee);

            SortedDictionary<int, EventDebug> eventInfos = events.GetHighlightedEvents(game.Emevds, entityIds, alwaysHighlight);

            if (opt["ignoreexisting"])
            {
                HashSet<int> current = new HashSet<int>(eventConfig.EnemyEvents.Select(e => e.ID));
                foreach (int key in eventInfos.Keys.ToList())
                {
                    if (current.Contains(key)) eventInfos.Remove(key);
                }
            }
            List<EventSpec> specs = events.CreateEventConfig(eventInfos, isEligible, produceSpec, quickId, processEventsOverride, processEntitiesOverride);

            ISerializer serializer = MakeSerializer();
            if (opt["eventsyaml"])
            {
                using (var writer = File.CreateText("newevents.txt"))
                {
                    serializer.Serialize(writer, specs);
                }
            }
            else
            {
                serializer.Serialize(Console.Out, specs);
            }
        }

        public void WriteSekiroEvents(RandomizerOptions opt, Dictionary<int, EnemyInfo> infos, Dictionary<int, EnemyData> defaultData)
        {
            // Collect all ids first
            HashSet<int> entityIds = new HashSet<int>();
            Dictionary<int, string> regionIds = new Dictionary<int, string>();
            Dictionary<int, List<int>> groupIds = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                MSBS msb = entry.Value;

                foreach (MSBS.Part.Enemy e in msb.Parts.Enemies)
                {
                    entityIds.Add(e.EntityID);
                    foreach (int id in e.EntityGroupIDs)
                    {
                        if (id > 0)
                        {
                            AddMulti(groupIds, id, e.EntityID);
                            entityIds.Add(id);
                        }
                    }
                }
                foreach (MSBS.Region r in msb.Regions.GetEntries())
                {
                    if (r.EntityID < 1000000) continue;
                    regionIds[r.EntityID] = r.Name;
                    entityIds.Add(r.EntityID);
                }
            }

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
                                entityIds.Add(eventFlag);
                                // if (eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022)
                                {
                                    Console.WriteLine($"{eventFlag}: {game.Name(item.Key)}");
                                }
                            }
                        }
                    }
                }
                HashSet<string> treasureModels = new HashSet<string> { "o000100", "o000101", "o005300", "o005390", "o005400", "o255300" };
                foreach (KeyValuePair<string, MSBS> entry in game.SekiroMaps)
                {
                    if (!game.Locations.ContainsKey(entry.Key)) continue;
                    string map = game.Locations[entry.Key];
                    MSBS msb = entry.Value;

                    foreach (MSBS.Part.Object e in msb.Parts.Objects)
                    {
                        if (!treasureModels.Contains(e.ModelName) || e.EntityID <= 0) continue;
                        treasureNames[e.EntityID] = $"{e.Name} - {game.ModelName(e.ModelName)}";
                        entityIds.Add(e.EntityID);
                    }
                }
            }
            SortedDictionary<int, EventDebug> eventInfos = events.GetHighlightedEvents(game.Emevds, entityIds);
            if (!opt["eventsitem"])
            {
                HashSet<int> current = new HashSet<int>(eventConfig.EnemyEvents.Select(e => e.ID));
                foreach (int key in eventInfos.Keys.ToList())
                {
                    if (current.Contains(key)) eventInfos.Remove(key);
                }
            }
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
                    + $"{(infos.TryGetValue(id, out EnemyInfo enemy) && enemy.Class == 0 ? "" : "")})";  // - not random
            }
            bool isEligible(int entityId)
            {
                if (opt["eventsitem"])
                {
                    return flagItems.ContainsKey(entityId) || treasureNames.ContainsKey(entityId);
                }
                else
                {
                    List<int> groupEntityIds = groupIds.TryGetValue(entityId, out List<int> gids) ? gids : new List<int> { entityId };
                    return groupEntityIds.Any(id => !infos.TryGetValue(id, out EnemyInfo enemy) || enemy.Class != 0);
                }
            }
            EventSpec produceSpec()
            {
                if (!opt["eventsitem"])
                {
                    return new EventSpec
                    {
                        Template = new List<EnemyTemplate>
                            {
                                new EnemyTemplate
                                {
                                    Type = "default",
                                    // Type = "chr loc start end remove xx",
                                    // Entity = -1,
                                    // DefeatFlag = -1,
                                }
                            }
                    };
                }
                else
                {
                    return new EventSpec
                    {
                        ItemTemplate = new List<ItemTemplate>
                            {
                                new ItemTemplate
                                {
                                    Type = "item loc",
                                    EventFlag = "X0",
                                }
                            }
                    };
                }
            }

            HashSet<int> processEventsOverride = new HashSet<int> { };
            // Old Dragons: 2500810, 2500811, 2500812, 2500813, 2500814, 2500815, 2500816, 2500817, 2500818, 2500819, 2500820, 2500821, 2500822, 2500823, 2500824, 2500825. Group 2505830
            // Tree Dragons: 2500930, 2500933, 2500934, 2500884, 2500880, 2500881, 2500882, 2500883
            // Divine Dragon: 2500800
            // Monkeys: 2000800, 2000801, 2000802, 2000803, 2000804 
            // Serpents: 1100850, 1700600, 1700610, 1700620, 1700640
            // Big Carps: 2500310, 2500312, 2500313. but only 2500311 active
            HashSet<int> processEntitiesOverride = new HashSet<int>
            {
                // 2500613, 2500614,
            };

            List<EventSpec> specs = events.CreateEventConfig(eventInfos, isEligible, produceSpec, quickId, processEventsOverride, processEntitiesOverride);

            if (processEntitiesOverride.Count > 0)
            {
                Dictionary<int, List<int>> relevantEvents = new Dictionary<int, List<int>>();
                foreach (KeyValuePair<int, EventDebug> entry in eventInfos.OrderBy(e => e.Key))
                {
                    List<int> highlighted = processEntitiesOverride.Intersect(entry.Value.IDs).ToList();
                    foreach (int id in highlighted)
                    {
                        AddMulti(relevantEvents, id, entry.Key);
                    }
                }
                foreach (KeyValuePair<int, List<int>> entry in relevantEvents)
                {
                    Console.WriteLine($"{entry.Key}: [{string.Join(", ", entry.Value)}]");
                }
            }

            ISerializer serializer = MakeSerializer();
            if (opt["eventsyaml"])
            {
                using (var writer = File.CreateText("newevents.txt"))
                {
                    serializer.Serialize(writer, specs);
                }
            }
            else
            {
                serializer.Serialize(Console.Out, specs);
            }
        }

        public void WriteCategories(Dictionary<int, EnemyInfo> infos, Dictionary<int, EnemyData> defaultData, List<EnemyClass> randomizedTypes)
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
    }
}
