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
using static RandomizerCommon.GameData;
using static RandomizerCommon.ScalingEffects;
using System.Drawing;

namespace RandomizerCommon
{
    // TODO: Split this class up, for the 4th time. It just keeps growing.
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

        private int tmpBase = 11315000;
        private int tmpBaseMax = 11496000;
        private int tmpJump = 11515000;
        private int tmpMax = 11696000;
        private int writeBase = 11305750;
        private int writeMax = 11306000;

        private void AllocateWriteableIDs(int amount)
        {
            if (game.EldenRing)
            {
                int target = (writeBase + amount - 1) % 10000;
                int newBase = writeBase;
                if (target >= 3000 && target < 5000)
                {
                    newBase = writeBase - target + 5000;
                }
                else if (target < 2000)
                {
                    newBase = writeBase - target + 2000;
                }
                else if (target >= 6000)
                {
                    newBase = writeBase - target + 12000;
                }
                if (newBase < writeBase) throw new Exception($"Internal error: illegal flag allocation {writeBase}->{newBase}");
                writeBase = newBase;
            }
        }

        private int NewID(bool writeable = false)
        {
            int newId = writeable ? writeBase++ : tmpBase++;
            // Jump to the next 5xxxx block
            if (!game.EldenRing)
            {
                if (!IsTemp(tmpBase) && tmpBase % 10000 == 6000)
                {
                    tmpBase -= 1000;
                    tmpBase += 10000;
                }
                if (tmpBase >= tmpBaseMax && tmpBase < tmpJump) tmpBase = tmpJump;
            }
            if (writeable)
            {
                AllocateWriteableIDs(1);
            }
            if (tmpBase > tmpMax || writeBase > writeMax) throw new Exception($"Event {newId} hit event limit.");
            return newId;
        }

        public EnemyLocations Run(RandomizerOptions opt, Preset preset)
        {
            if (game.Sekiro)
            {
                return RunGame(opt, preset, game.SekiroMaps, new SekiroEnemyEditor());
            }
            else if (game.EldenRing)
            {
                return RunGame(opt, preset, game.EldenMaps, new EldenEnemyEditor());
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
            if (game.EldenRing)
            {
                // These are actually an order of magnitude smaller than all other ids, but it is fine.
                entityBase = 4000000;
                entityMax = 5000000;
                tmpBase = 1700000;
                tmpBaseMax = 1800000;
                // Less than 100 can be used for manual custom flags
                writeBase = EldenRingBase + 2100;
                // Absolute max is 1032650000, but even doing 5x onslaught results in 1032505416 used.
                // So two map tiles should be enough here.
                writeMax = EldenRingBase + 20000;
            }
            int newEntity()
            {
                if (entityBase >= entityMax) throw new Exception("Internal error: Can't create more entities: out of ids");
                int entityId = entityBase++;
                if (game.EldenRing && entityBase % 10000 == 4000)
                {
                    entityBase += 6000;
                }
                return entityId;
            }
            // Filianore/Dungeon range, except for groups, which require a valid range to track
            int entityGroupBase, entityGroupMax;
            if (game.DS3)
            {
                entityGroupBase = 5115000;
                entityGroupMax = 5116000;
            }
            else if (game.Sekiro)
            {
                entityGroupBase = 1305000;
                entityGroupMax = 1306000;
            }
            else if (game.EldenRing)
            {
                entityGroupBase = 19005000;
                entityGroupMax = 19006000;
            }
            else throw new Exception();

            HashSet<int> entityGroupSkip = new HashSet<int>
            {
                5115800, // Gael group
                19005800, // Final boss
                19005801, // Dummy final boss
            };
            int newGroupEntity()
            {
                if (entityGroupSkip.Contains(entityGroupBase)) entityGroupBase++;
                if (entityGroupBase >= entityGroupMax) throw new Exception("Internal error: Can't create more entity groups: out of ids");
                return entityGroupBase++;
            }

            // Start processing config
            IDeserializer deserializer = new DeserializerBuilder().Build();
            EnemyAnnotations ann;
            string enemyConfigPath = $"{game.Dir}/Base/enemy.txt";
#if DEV
            if (game.EldenRing && (opt["full"] || opt["dumpyenemylist"]))
            {
                enemyConfigPath = "configs/diste/enemy.txt";
            }
#endif
            using (var reader = File.OpenText(enemyConfigPath))
            {
                ann = deserializer.Deserialize<EnemyAnnotations>(reader);
            }
#if DEV
            if (opt["dumpenemy"])
            {
                new EnemyConfigGen(game, events, eventConfig).WriteEldenEnemyCategories(ann); return null;
            }
            if (game.EldenRing && (opt["full"] || opt["dumpenemylist"]))
            {
                enemyConfigPath = "configs/diste/enemylist.txt";
                using (var reader = File.OpenText(enemyConfigPath))
                {
                    EnemyAnnotations ann2 = deserializer.Deserialize<EnemyAnnotations>(reader);
                    ann.Enemies = ann2.Enemies;
                    if (!opt["dumpenemylist"] || opt["lite"])
                    {
                        ann.Categories.AddRange(ann2.Categories);
                    }
                }
                if (opt["dumpenemylist"])
                {
                    if (opt["lite"])
                    {
                        new EnemyConfigGen(game, events, eventConfig).WriteEldenEnemyLite(ann, opt); return null;
                    }
                    else
                    {
                        new EnemyConfigGen(game, events, eventConfig).WriteEldenEnemyList(ann, opt); return null;
                    }
                }
            }
#endif

            List<EnemyClass> randomizedTypes = new List<EnemyClass>
            {
                // Boss types in all games
                EnemyClass.Boss,
                EnemyClass.Miniboss,
                // Elden Ring boss types
                EnemyClass.MinorBoss,
                EnemyClass.NightMiniboss,
                EnemyClass.DragonMiniboss,
                EnemyClass.Evergaol,
                // Sekiro boss types
                EnemyClass.FoldingMonkey,
                EnemyClass.TutorialBoss,
                // Basic
                EnemyClass.Basic,
                // DS3 and Elden Ring basic
                EnemyClass.HostileNPC,
                // Elden Ring basic
                EnemyClass.Wildlife,
                EnemyClass.CaravanTroll,
                // Scarab is not currently included here.
                // Dupe mode
                EnemyClass.DupeOnly,
            };

#if DEBUG
            if (opt["configgen"])
            {
                preset = null;
            }
#endif
            if (preset != null)
            {
                preset.ProcessParents(ann);
            }

            bool presetOpt(string name)
            {
                return opt[name] || (preset != null && preset[name]);
            }

            Dictionary<EnemyClass, EnemyClass> parentClass = new Dictionary<EnemyClass, EnemyClass>();
            Dictionary<EnemyClass, int> classDupeCount = new Dictionary<EnemyClass, int>();
            if (ann.Classes != null && preset?.Classes != null)
            {
                foreach (ClassConfig conf in ann.Classes)
                {
                    preset.Classes.TryGetValue(conf.Class, out ClassAssignment assign);
                    if (assign == null) continue;
                    if (!conf.NoMerge && assign.MergeParent && randomizedTypes.Contains(assign.RootParent))
                    {
                        parentClass[conf.Class] = assign.RootParent;
                    }
                    if (assign.EnemyMultiplier > 0 && !assign.MergeParent)
                    {
                        classDupeCount[conf.Class] = assign.EnemyMultiplier - 1;
                    }
                }
            }
            EnemyClass siloClass(EnemyClass cl)
            {
                return parentClass.TryGetValue(cl, out EnemyClass parent) ? parent : cl;
            }

            // Enemy multiplier modifies the config itself
            bool anyDupeEnabled = false;
            int defaultDupeCount = 0;
            int maxDupeCount = 0;
            if (preset != null && preset.EnemyMultiplier > 0)
            {
                anyDupeEnabled = preset.EnemyMultiplier > 1 || classDupeCount.Any(c => c.Value > 0);
                defaultDupeCount = preset.EnemyMultiplier - 1;
                // Some things (like event copies) are not created on demand, so we may allocate a bunch to never use them.
                maxDupeCount = classDupeCount.Select(c => c.Value).Concat(new[] { defaultDupeCount }).Max();
            }
            Dictionary<int, List<int>> dupeEnemyMap = new Dictionary<int, List<int>>();
            if (anyDupeEnabled)
            {
                // Can probably use a global one here
                int partId = 1000;
                foreach (EnemyInfo info in ann.Enemies.ToList())
                {
                    // TODO: Some None enemies maybe be okay to copy, just give them no think and update their enable/disable
                    if (info.DupeEvents != null)
                    {
                        info.Class = EnemyClass.DupeOnly;
                    }
                    // Temporary exclusion of NPCs until multi-invasions can be figured out
                    if (info.Class == EnemyClass.HostileNPC)
                    {
                        continue;
                    }
                    // Use this as the "main" exclusion condition - can maybe use proper categories as well
                    if (info.Class == EnemyClass.Helper || info.Class == EnemyClass.None)
                    {
                        continue;
                    }
                    // This is using the config names, which will have m prefix in Elden Ring. The _ split is also not valid for assets
                    string[] segments = info.Name.Split('-');
                    string modelName = segments.Length == 1 ? info.Name.Split('_')[0] : $"{segments[0]}-{segments[1].Split('_')[0]}";
                    if (!classDupeCount.TryGetValue(siloClass(info.Class), out int enemyDupeCount))
                    {
                        enemyDupeCount = defaultDupeCount;
                    }
                    for (int i = 0; i < enemyDupeCount; i++)
                    {
                        EnemyInfo dupeInfo = new EnemyInfo
                        {
                            ID = newEntity(),
                            Map = info.Map,
                            Name = $"{modelName}_{partId++:d4}",
                            Class = info.Class,
                            SplitFrom = info.ID,
                            DupeFrom = info.ID,
                            DupeIndex = i,
                            // Copy some attributes expected of bosses
                            // For the most part, the only required fields here are those required for entities to be valid targets.
                            // They do not need to be valid sources.
                            Tags = info.Tags + " dupe",
                            DefeatFlag = info.DefeatFlag,
                            AppearFlag = info.AppearFlag,
                            StartFlag = info.StartFlag,
                            MusicFlag = info.MusicFlag,
                            BuddyGroup = info.BuddyGroup,
                            RemoveGroup = info.RemoveGroup,
                            Groups = info.Groups,
                            Phases = info.Phases,
                            Arena = info.Arena,
                            ExtraArenas = info.ExtraArenas,
                            OwnedBy = info.OwnedBy,
                            NextPhase = info.NextPhase,
                            ExtraName = info.ExtraName,
                        };
                        // Reset, otherwise it can run into actual enemies in Elden Ring.
                        // Maps can repeat in the enemy list order, but the first iteration should be much shorter than 8k enemies.
                        if (partId >= 9000) partId = 1000;
                        if (!randomizedTypes.Contains(info.Class))
                        {
                            // Currently, this seems to be done for scarabs in Elden Ring
                            dupeInfo.Class = EnemyClass.DupeOnly;
                        }
                        ann.Enemies.Add(dupeInfo);
                        AddMulti(dupeEnemyMap, info.ID, dupeInfo.ID);
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
            // Map from (name, part) to entity id, for enemies initially without entity ids
            Dictionary<(string, string), int> configEntityIds = new Dictionary<(string, string), int>();
            bool isFakeId(int id)
            {
                // Used by Elden Ring and DS3
                return (id >= 2800000 && id < 2900000) || (id >= 2888000 && id < 2889000);
            }
            // Splits to perform
            Dictionary<int, List<EnemyInfo>> copyTo = new Dictionary<int, List<EnemyInfo>>();
            // Map from owner enemy to total healthbars used. If missing, assume this is 1.
            Dictionary<int, int> totalHealthbars = new Dictionary<int, int>();
            // Map from ESD id to defeat flag
            Dictionary<int, int> deathLineFlags = new Dictionary<int, int>();
            // Map from RemoveGroup to an acceptable substitute group
            Dictionary<int, int> buffGroupEntities = new Dictionary<int, int>();
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (infos.ContainsKey(info.ID)) throw new Exception($"Duplicate config entity {info.ID}");
                infos[info.ID] = info;
                if (isFakeId(info.ID))
                {
                    configEntityIds[(info.Map, info.Name)] = info.ID;
                    if (info.DupeMap != null && info.DupePartName != null)
                    {
                        configEntityIds[(info.DupeMap, info.DupePartName)] = info.ID;
                    }
                }
                if (info.OwnedBy > 0)
                {
                    if (info.OwnedBy == info.ID) throw new Exception($"Internal config error: {info.ID} owns itself");
                    AddMulti(owners, info.OwnedBy, info.ID);
                }
                if (info.HealthbarIndex > 0)
                {
                    int healthKey = info.OwnedBy > 0 ? info.OwnedBy : info.ID;
                    int maxCount = info.HealthbarIndex + 1;
                    if (!totalHealthbars.TryGetValue(healthKey, out int current) || current < maxCount)
                    {
                        totalHealthbars[healthKey] = maxCount;
                    }
                }
                if (info.SplitFrom > 0)
                {
                    AddMulti(copyTo, info.SplitFrom, info);
                }
                if (info.DeathLine > 0 && info.DefeatFlag > 0)
                {
                    // Slight hack - no line for first phase with never-true flag. (Change this if it's relied upon)
                    deathLineFlags[info.DeathLine] = info.NextPhase > 0 ? 6000 : info.DefeatFlag;
                }
                if (info.RemoveGroup > 0 && info.BuddyGroup > 0 && info.OwnedBy <= 0 && info.NextPhase <= 0)
                {
                    // In Elden Ring, can use BuddyGroup, which should be reliably present.
                    // In DS3, need to figure out a different system (reuse BuddyGroup, despite no buddies?)
                    buffGroupEntities[info.RemoveGroup] = info.BuddyGroup;
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

            bool dupeEnabled(int target) => dupeEnemyMap.ContainsKey(target) || (infos.TryGetValue(target, out EnemyInfo ti) && ti.DupeFrom > 0);
            int dupeCount(int target) => dupeEnemyMap.TryGetValue(target, out List<int> dupes) ? dupes.Count : 0;
            int combinedDupeCount(IEnumerable<int> targets) => targets.Select(dupeCount).Concat(new[] { 0 }).Max();

            // The "main" map data for an entity
            Dictionary<int, EnemyData> defaultData = new Dictionary<int, EnemyData>();
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
            // Bad hack to separate out main group for Rykard and a new buddy group, since we want to keep only the latter
            if (game.EldenRing && maps.TryGetValue("m16_00_00_00", out TMap tvolcano) && tvolcano is MSBE volcano)
            {
                game.Params["BuddyStoneParam"][16000114]["eliminateTargetEntityId"].Value = (uint)16005802;
                foreach (MSBE.Part.Enemy e in volcano.Parts.Enemies)
                {
                    // TODO: Put this stuff in MapEditor, when overhauling the awful groups API
                    if (e.EntityID == 16000800 || e.EntityID == 16000801)
                    {
                        int addIndex = Array.IndexOf(e.EntityGroupIDs, (uint)0);
                        if (addIndex >= 0)
                        {
                            e.EntityGroupIDs[addIndex] = 16005802;
                        }
                    }
                }
            }
            Vector3 getDupeOffset(Vector3 rotation, int target, int index)
            {
                if (!dupeEnabled(target)) return Vector3.Zero;
                // Start from center if any, then alternate right and left
                // Flank indices are: dupeCount 1 [1 2], dupeCount 2 [0 1 2], dupeCount 3 [1 2 3 4].
                bool hasCenter = dupeCount(target) % 2 == 0;
                int flankIndex = (index + 2) - (hasCenter ? 1 : 0);
                if (flankIndex == 0) return Vector3.Zero;
                // Go right (positive direction), which usually appears to player as left
                bool rightSide = flankIndex % 2 != 0;
                // Ideally, separate each by 0.5 units. This may get messy at higher dupe counts
                float initialDist = hasCenter ? 0.5f : 0.25f;
                // 1 2 get 0th flank, 3 4 get 1st flank, etc.
                // This is amount to shift right
                float amt = (initialDist + ((flankIndex - 1) / 2) * 0.5f) * (rightSide ? 1 : -1);
                // Lightweight side-to-side moving, which hopefully won't cause floor issues
                // part.Rotation.Y is facing forwards, so +90 should mean moving on the perpendicular axis
                float forward = (rotation.Y - 90) * (float)Math.PI / 180;
                return new Vector3((float)Math.Sin(forward) * amt, 0, (float)Math.Cos(forward) * amt);
            }
            Vector3 getQuadrantOffset(Vector3 rotation, float amt, int quadrant)
            {
                // Similar to strafe logic, but radial, to be more space-efficient
                float angle = 45 + 90 * quadrant;
                float forward = (rotation.Y - 90 + angle) * (float)Math.PI / 180;
                return new Vector3((float)Math.Sin(forward) * amt, 0, (float)Math.Cos(forward) * amt);
            }
            Vector3 moveInDirection(Vector3 v, Vector3 r, float dist)
            {
                float angle = r.Y * (float)Math.PI / 180;
                return new Vector3(v.X + (float)Math.Sin(angle) * dist, v.Y, v.Z + (float)Math.Cos(angle) * dist);
            }

            // Mapping from target entity to event map name.
            // In the case of Elden Ring, this should not be a 01 or 02 tile.
            Dictionary<int, string> ownerMap = new Dictionary<int, string>();
            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                if (!game.Locations.ContainsKey(entry.Key))
                {
                    continue;
                }
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                foreach (TEnemy e in enemyEditor.GetEnemies(msb).ToList())
                {
                    EnemyData data = enemyEditor.GetEnemyData(e, entry.Key);
                    if (data.ID <= 0)
                    {
                        if (configEntityIds.TryGetValue((entry.Key, e.Name), out int fakeId))
                        {
                            enemyEditor.SetEntityID(e, fakeId);
                            data.ID = fakeId;
                        }
                        else
                        {
                            Console.WriteLine($"Unknown enemy {entry.Key} {e.Name} #{data.ID}");
                            continue;
                        }
                    }
                    if (!infos.TryGetValue(data.ID, out EnemyInfo info))
                    {
                        // Allow EntityID-less enemies to be given an id by other mods, and try to recognize it.
                        // Hopefully, the enemies are compatible and there are no notable caches indexed by fakeId.
                        if (game.HasMods
                            && configEntityIds.TryGetValue((entry.Key, e.Name), out int fakeId)
                            && infos.TryGetValue(fakeId, out info))
                        {
                            infos.Remove(fakeId);
                            infos[data.ID] = info;
                            info.ID = data.ID;
                        }
                        else
                        {
                            Console.WriteLine($"Unknown enemy {entry.Key} {e.Name} #{data.ID}");
                        }
                    }
                    string eventMap = entry.Key;
                    string eventPart = data.Name;
                    if (game.EldenRing && eventMap.StartsWith("m60") && !eventMap.EndsWith("0"))
                    {
                        if (!eventPart.StartsWith("m") || !eventPart.Contains("-"))
                        {
                            throw new Exception($"Entity {eventMap} {eventPart} missing map-specific name");
                        }
                        eventMap = eventPart.Split('-')[0];
                        // Console.WriteLine($"{eventPart} in {data.Map} -> {eventMap}");
                    }
                    if (info != null)
                    {
                        if (!game.Emevds.ContainsKey(eventMap))
                        {
                            if (info.NeighborMap != null)
                            {
                                eventMap = info.NeighborMap;
                            }
                        }
                        if (info.DupeMap != null)
                        {
                            if (info.DupeMap == entry.Key)
                            {
                                // In this case, the other instance of this data will be used as the main one
                                // If no unique emevd, the entire emevd will also get copied later
                                continue;
                            }
                            data.DupeMap = info.DupeMap;
                        }
                    }
#if DEBUG
                    if (!game.Emevds.ContainsKey(eventMap)) Console.WriteLine($"Nonexistent eventMap {eventMap} for {data.Name} #{data.ID}");
#endif
                    defaultData[data.ID] = data;
                    ownerMap[data.ID] = eventMap;

                    if (copyTo.TryGetValue(data.ID, out List<EnemyInfo> deriveds))
                    {
                        if (dupeEnemyMap.ContainsKey(data.ID))
                        {
                            e.Position += getDupeOffset(e.Rotation, data.ID, -1);
                        }
                        foreach (EnemyInfo derived in deriveds)
                        {
                            int partId = int.Parse(derived.Name.Split('_')[1].TrimStart('0'));
                            // TODO: Make Map always present. It is not present in Sekiro enemy config.
                            string toMap = derived.Map ?? entry.Key;
                            // This does a concurrent modification to the enemy list for same-map splits,
                            // which is accounted for above.
                            // Also, unlike with helper enemies, don't clear groups
                            TEnemy e2 = enemyEditor.CloneEnemy(maps[toMap], e, data, derived.ID, partId);
                            if (derived.DupeFrom == data.ID)
                            {
                                // Split positions slightly in this case!
                                e2.Position += getDupeOffset(e2.Rotation, data.ID, derived.DupeIndex);
                            }
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
                            // TODO: Needs handling of DupeMap
                            defaultData[derived.ID] = enemyEditor.GetEnemyData(e2, toMap);
                            ownerMap[derived.ID] = toMap == entry.Key ? eventMap : toMap;
                            if (ann.ScalingSections != null && !ann.ScalingSections.ContainsKey(derived.ID) && ann.ScalingSections.ContainsKey(data.ID))
                            {
                                ann.ScalingSections[derived.ID] = ann.ScalingSections[data.ID];
                            }
                        }
                    }
                }
            }
            // Make the helper invisible so they can be in the primary position
            if (game.EldenRing && defaultData.TryGetValue(12030814, out EnemyData fiaHelper))
            {
                fiaHelper.NPC = fiaHelper.Think = fiaHelper.Char = 0;
            }

            // Do metadata processing here
#if DEV
            if (opt["configgen"])
            {
                if (game.Sekiro)
                {
                    new EnemyConfigGen(game, events, eventConfig).WriteSekiroEvents(opt, infos, defaultData);
                }
                else if (game.EldenRing)
                {
                    if (opt["cols"])
                    {
                        new ReverseEnemyOrder().InvestigateEldenCols(game.EldenMaps["m14_00_00_00"], infos);
                    }
                    else if (opt["rewrite"])
                    {
                        new EnemyConfigGen(game, events, eventConfig).WriteEldenLite(opt, opt["lite"]);
                    }
                    else
                    {
                        new EnemyConfigGen(game, events, eventConfig).WriteEldenEvents(opt, infos, defaultData);
                    }
                }
                else
                {
                    new EnemyConfigGen(game, events, eventConfig).WriteDS3Events(opt, infos, defaultData);
                }
                return null;
            }
#endif
            // new ReverseEnemyOrder().EnemyDS3(game, infos); return null;
            Dictionary<int, (TGenerator, List<int>)> generators = enemyEditor.GetGeneratorData(maps);

            // Process core enemy config
            List<int> treeDragonOrder = Enumerable.Repeat(0, 5).ToList();
            // Unused feature which compensated for prior lack of BuddyGroup
            HashSet<int> defeatIds = new HashSet<int>(game.Params["GameAreaParam"].Rows.Select(r => r.ID));
            // Used feature for altering GameAreaParam amounts aesthetically
            int roundBonusSoul(int val)
            {
                if (val > 100000) val = (int)Math.Ceiling(val / 10000.0) * 10000;
                else if (val > 10000) val = (int)Math.Ceiling(val / 1000.0) * 1000;
                else val = (int)Math.Ceiling(val / 100.0) * 100;
                val = Math.Max(val, 200);
                return val;
            }
            // Entities for whom to use multcolor
            List<int> importantDupes = new List<int>();
            // Report on these and don't randomize them by default
            SortedSet<int> missingInfos = new SortedSet<int>();
            // Mapping from defeat flag to main boss entity id, for use in bossbgm which is keyed by defeat flag
            Dictionary<int, int> defeatFlagIds = new Dictionary<int, int>();
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (!defaultData.TryGetValue(info.ID, out EnemyData data))
                {
                    // These can potentially be fine. Handle them later.
                    missingInfos.Add(info.ID);
                    Console.WriteLine($"Missing enemy {info.Map} {info.Name} #{info.ID}");
                }
                if (info.Name == null) throw new Exception($"Entity {info.ID} has no name");
                string modelId = info.Name;
                if (modelId.StartsWith("m") && modelId.Contains('-'))
                {
                    modelId = modelId.Split('-')[1];
                }
                modelId = modelId.Split('_')[0];
                info.ModelID = modelId;
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
                if (info.NextPhase != 0)
                {
                    // Much less is copied here, since it's specified individually in both phases
                    if (!infos.TryGetValue(info.NextPhase, out EnemyInfo next)) throw new Exception($"Entity {info.ID} referencing {info.NextPhase} which does not exist in config");
                    if (info.BuddyGroup <= 0)
                    {
                        info.BuddyGroup = next.BuddyGroup;
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
                    info.IsBuffSource = info.IsBossTarget || info.Class == EnemyClass.Miniboss;
                    info.IsFixedSource = info.IsBossTarget || info.Class == EnemyClass.FoldingMonkey;
                    info.IsImportantTarget = info.Class == EnemyClass.Boss || info.Class == EnemyClass.TutorialBoss
                        || info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.FoldingMonkey;
                    info.HasPerceptiveNose = info.IsBossTarget;
                    info.IsArenaTarget = info.IsImportantTarget;
                    info.IsImmortal = info.Class == EnemyClass.Boss && !info.HasTag("mortal");
                    info.IsMortalSekiroBoss = (info.Class == EnemyClass.Boss && info.HasTag("mortal")) || info.Class == EnemyClass.TutorialBoss;
                }
                else if (game.DS3)
                {
                    info.IsBossTarget = info.Class == EnemyClass.Boss;
                    info.IsBuffSource = info.Class == EnemyClass.Boss;
                    info.IsFixedSource = info.Class == EnemyClass.Boss;
                    info.IsImportantTarget = info.Class == EnemyClass.Boss;
                    info.HasPerceptiveNose = info.Class == EnemyClass.Boss;
                    info.IsArenaTarget = info.Class == EnemyClass.Boss;
                    info.IsImmortal = info.HasTag("immortal");
                    info.IsMortalSekiroBoss = false;
                }
                else if (game.EldenRing)
                {
                    // TODO: split this up into, is boss encounter/start flag? and, has souls on drop/defeat flag?
                    info.IsBossTarget = info.Class == EnemyClass.Boss || info.Class == EnemyClass.MinorBoss;
                    info.IsImportantTarget = info.IsBossTarget
                        || info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.DragonMiniboss
                        || info.Class == EnemyClass.NightMiniboss || info.Class == EnemyClass.Evergaol;
                    info.HasPerceptiveNose = info.IsBossTarget || info.Class == EnemyClass.Evergaol;
                    info.IsBuffSource = info.IsImportantTarget;
                    info.IsFixedSource = info.IsImportantTarget;
                    info.IsArenaTarget = false;
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
                            // This may fail in some cases, when entity does not exist or multiple maps
                            string map = defaultData[info.ID].MainMap;
                            enemyEditor.MakeRegionCopy(maps, () => target, map, map, source, false);
                        }
                    }
                }
                bool isDuped = dupeEnabled(info.ID);
                if (isDuped && dupeEnemyMap.ContainsKey(info.ID) && info.IsImportantTarget)
                {
                    // Add to list for way later, specifically for originals of important targets
                    importantDupes.Add(info.ID);
                }
                if (info.DefeatEntity > 0 || (isDuped && defeatIds.Contains(info.ID)))
                {
                    PARAM.Row selfRow = game.Params["GameAreaParam"][info.ID];
                    // Cases where main entity is different from HandleBossDefeat entity.
                    // TODO this should be possible to eliminate, was only a hack for missing BuddyGroup.
                    if (info.DefeatEntity > 0)
                    {
                        PARAM.Row defeatRow = game.Params["GameAreaParam"][info.DefeatEntity];
                        if (selfRow == null && defeatRow != null)
                        {
                            selfRow = game.AddRow("GameAreaParam", info.ID);
                            GameEditor.CopyRow(defeatRow, selfRow);
                        }
                    }
                    // Default soul multiplier for dupe status. TODO backport to previous games
                    if (isDuped && selfRow != null && game.EldenRing && !opt["swaprewards"])
                    {
                        double mult = GetXpRate(dupeCount(info.ID), presetOpt("multhp"));
                        uint val = (uint)selfRow["bonusSoul_single"].Value;
                        val = (uint)roundBonusSoul((int)(val * mult));
                        selfRow["bonusSoul_single"].Value = val;
                        selfRow["bonusSoul_multi"].Value = val;
                    }
                }
                if (info.BuddyGroup > 0 && info.OwnedBy <= 0 && info.NextPhase <= 0 && data != null)
                {
                    foreach (int group in data.Group)
                    {
                        // Just do this rewrite whenever possible
                        if (buffGroupEntities.ContainsKey(group)) continue;
                        buffGroupEntities[group] = info.BuddyGroup;
                    }
                }
                if (info.IsImportantTarget && info.OwnedBy <= 0 && info.DefeatFlag > 0)
                {
                    defeatFlagIds[info.DefeatFlag] = info.ID;
                }
            }
            // If enemies are not important, allow them to be missing
            // Other entities must be added to a preset as explicitly removed.
            foreach (int id in missingInfos)
            {
                infos[id].Class = EnemyClass.None;
                /*if (isFakeId(id))
                {
                }*/
            }
            /*
            if (missingInfos.Count > 0)
            {
                List<int> mainIds = missingInfos
                    .Where(i => infos[i].Class != EnemyClass.None)
                    .Select(i => infos[i].Class == EnemyClass.Helper && infos[i].OwnedBy > 0 ? infos[i].OwnedBy : i)
                    .Distinct()
                    .ToList();
                if (mainIds.Count > 0)
                {
                    Console.WriteLine($"To disable randomization for missing enemies:");
                    Console.WriteLine($"DontRandomize: {string.Join("; ", mainIds)}");
                }
            }*/

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
                preset.ProcessEnemyPreset(game, infos, ann, defaultData);
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

            // Dupe color speffects, even if not using them here either
            List<int> dupeBossSpEffects = new List<int>();
            if (game.EldenRing)
            {
                // Start just before ScalingEffects NewScalingBase
                int colorBase = 77700 - 10;
                List<Color> dupeColors = new List<Color>
                {
                    Color.FromArgb(50, 100, 255), // Blue
                    Color.FromArgb(255, 0, 50), // Red
                    Color.FromArgb(200, 255, 220), // White (but slightly green)
                    Color.FromArgb(255, 128, 0), // Orange
                    Color.FromArgb(160, 32, 240), // Purple
                };
                for (int i = 0; i < dupeColors.Count; i++)
                {
                    // Base this off Siofra followers: phantom param 260, vfx param 51508, speffect 13177, (speffectset 33600020)
                    PARAM.Row phantom = game.AddRow("PhantomParam", colorBase + i, 260);
                    phantom["alpha"].Value = 1f;
                    phantom["edgePower"].Value = 0.5f;
                    // Potentially too expensive
                    phantom["glowScale"].Value = 0.0f;
                    // Byte fields
                    phantom["edgeColorR"].Value = dupeColors[i].R;
                    phantom["edgeColorG"].Value = dupeColors[i].G;
                    phantom["edgeColorB"].Value = dupeColors[i].B;

                    PARAM.Row vfx = game.AddRow("SpEffectVfxParam", phantom.ID, 51508);
                    vfx["phantomParamOverwriteId"].Value = phantom.ID;

                    PARAM.Row sp = game.AddRow("SpEffectParam", vfx.ID, 13177);
                    sp["vfxId"].Value = vfx.ID;

                    dupeBossSpEffects.Add(sp.ID);
                }
            }


            // Make all scaling speffects, even if we're not using them in this run.
            // Mapping from (source section, target section) to (scaling without xp, scaling with xp)
            SpEffectValues scalingSpEffects;
            if (game.EldenRing)
            {
                ScalingEffects scaling = new ScalingEffects(game);
                ann.ScalingSections = scaling.InitializeEldenScaling(defaultData, dupeEnemyMap);
                scalingSpEffects = scaling.EditScalingSpEffects();
            }
            else
            {
                scalingSpEffects = new ScalingEffects(game).EditScalingSpEffects();
            }
            int seed = opt.Seed2 == 0 ? (int)opt.Seed : (int)opt.Seed2;

            Dictionary<int, int> targetScalingSections = ann.ScalingSections;
            if (ann.ScalingSections != null && opt["scale"] && (presetOpt("scalerandom") || (preset != null && preset.RandomScaling)))
            {
                Random scalingRandom = new Random(seed);
                targetScalingSections = new Dictionary<int, int>(ann.ScalingSections);
                foreach (KeyValuePair<int, int> entry in ann.ScalingSections)
                {
                    int target = entry.Key;
                    int targetSection = entry.Value;
                    if (targetSection > 0)
                    {
                        targetScalingSections[target] = scalingRandom.Next(scalingSpEffects.MaxTier) + 1;
                    }
                }
            }
            bool getScalingSections(int source, int target, out int sourceSection, out int targetSection, bool ignoreCustom = false)
            {
                sourceSection = ann.ScalingSections.TryGetValue(source, out int s) ? s : -1;
                targetSection = targetScalingSections.TryGetValue(target, out s) ? s : -1;
                if (sourceSection == -1 || targetSection == -1)
                {
                    return false;
                }
                if (ignoreCustom)
                {
                    return true;
                }
                if (infos.TryGetValue(source, out EnemyInfo sourceInfo))
                {
                    if (sourceInfo.HasTag("noscale") || (sourceInfo.HasTag("noscaleup") && targetSection > sourceSection))
                    {
                        targetSection = sourceSection;
                    }
                }
                if (presetOpt("scaleup") && targetSection < sourceSection)
                {
                    targetSection = sourceSection;
                }
                return true;
            }

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
                if (anyDupeEnabled)
                {
                    // Should this logic be somewhere more sensible?
                    // TODO: Fix double invasions, though for now don't randomize them, as that is less messy
                    // These aren't in the config
                    preset.DontRandomizeIDs.UnionWith(
                        infos.Values.Where(i => i.Class == EnemyClass.DupeOnly || i.Class == EnemyClass.CaravanTroll).Select(i => i.ID));
                }
                // Also try to implement norandom as enemies mapping to themselves
                foreach (int norandom in preset.DontRandomizeIDs)
                {
                    // This allows Enemies mapping to override norandom, but keep current behavior for now
                    // if (!forceMap.ContainsKey(norandom)) 
                    forceMap[norandom] = norandom;
                }
                // Finally, if there are doubles, randomize them to their originals
                if (preset.EnemyMultiplier > 1)
                {
                    foreach (EnemyInfo info in infos.Values)
                    {
                        if (info.DupeFrom > 0 && preset.DontRandomizeIDs.Contains(info.DupeFrom))
                        {
                            forceMap[info.ID] = info.DupeFrom;
                            // Do this for swap mapping later, for simplicity's sake
                            preset.DontRandomizeIDs.Add(info.ID);
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

            // Map from owner to swappable helpers
            SortedDictionary<int, List<int>> swapHelperMapping = new SortedDictionary<int, List<int>>();
            // Total list of swappable helpers
            SortedSet<int> allSwapHelpers = new SortedSet<int>();
            if (opt["swapboss"])
            {
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.HasTag("swappable") && info.OwnedBy > 0)
                    {
                        AddMulti(swapHelperMapping, info.OwnedBy, info.ID);
                        allSwapHelpers.Add(info.ID);
                    }
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
            Random forceSwapRandom = new Random(seed);
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (silos.TryGetValue(siloClass(info.Class), out EnemyPermutation silo))
                {
                    if (forceMap.ContainsKey(info.ID))
                    {
                        int target = info.ID;
                        int source = forceMap[info.ID];
                        silo.Mapping[target] = source;
                        // Try to exclude non-random enemies from being swapped here, or if they are non-random dupes
                        if (swapHelperMapping.TryGetValue(source, out List<int> helperSources)
                            && (preset == null || !preset.DontRandomizeIDs.Contains(target)))
                        {
                            foreach (int helper in helperSources)
                            {
                                silo.SwapMapping[(target, helper)] = Choice(forceSwapRandom, allSwapHelpers.ToList());
                            }
                        }
                    }
                    else if (info.Class == EnemyClass.DupeOnly)
                    {
                        // Dupe-only enemies should only be forceMap'd
                    }
                    else
                    {
                        if (missingInfos.Contains(info.ID)) throw new Exception($"Can't randomize #{info.ID} since it was removed from the game. See spoiler log for a full list.");
                        silo.Targets.Add(info.ID);
                    }

                    if (reverseForceMap.TryGetValue(info.ID, out List<int> targets)
                        && targets.Any(t => siloClass(infos[t].Class) == siloClass(info.Class)))
                    {
                        // If force mapped somewhere within the same silo, take it out of the source pool
                    }
                    else if (preset != null && preset.RemoveSourceIDs.Contains(info.ID))
                    {
                        // If remove source, don't add the source (the target should still get added if not forced)
                        // Per-class RemoveSource comes during per-class randomization, because it's target-specific
                    }
                    else if (opt["reducepassive"] && info.HasTag("passive") && reducePassiveRandom.NextDouble() <= 0.9f)
                    {
                        // Cut 90% of instances of a passive enemy. (Could make this configurable per model)
                    }
                    else if (preset != null
                        && preset.AdjustSourceIDs.TryGetValue(info.ID, out float remain)
                        && reducePassiveRandom.NextDouble() > remain)
                    {
                        // Cut out instances of an enemy per preset.
                        // If remain is 10%, 90% will be allowed.
                    }
                    else if (info.DupeFrom > 0)
                    {
                        // Dupes cannot be sources - this will generally involve duplicating other eligible sources
                    }
                    else
                    {
                        if (missingInfos.Contains(info.ID)) throw new Exception($"Can't randomize #{info.ID} since it was removed from the game. See spoiler log for a full list.");
                        silo.Sources.Add(info.ID);
                    }
                }
            }

            string ename(int ent)
            {
                if (defaultData.TryGetValue(ent, out EnemyData data))
                {
                    return $"{data.Name} #{ent} ({game.ModelCharacterName(data.Model, data.Char)})";
                }
                return $"#{ent}";
            }
            string enameClass(int ent)
            {
                string modelName = null;
                if (defaultData.TryGetValue(ent, out EnemyData data))
                {
                    modelName = game.ModelCharacterName(data.Model, data.Char);
                }
                string className = "";
                if (infos.TryGetValue(ent, out EnemyInfo info))
                {
                    if (info.ExtraName != null && info.ExtraName != modelName)
                    {
                        modelName = info.ExtraName;
                    }
                    className = $"{siloClass(info.Class)} ";
                }
                return $"{className}#{ent} ({modelName ?? "unknown"})";
            }
            // Don't populate a given arena with the same enemy type. For Sekiro minibosses, it is mainly Shigekichi, Juzou,
            // and Vilehand arenas that are reused.
            // DS3 doesn't use miniboss arenas and there are a lot of them so models are used directly
            Dictionary<string, HashSet<string>> arenaEnemyTypes = new Dictionary<string, HashSet<string>>();
            // Map from multi-phase bosses (either phase) to the second phase
            Dictionary<int, int> multiPhase = new Dictionary<int, int>();
            foreach (EnemyInfo info in infos.Values)
            {
                if (info.Arena != null && info.EnemyType != null)
                {
                    AddMulti(arenaEnemyTypes, info.Arena, info.EnemyType);
                }
                if (info.NextPhase > 0)
                {
                    multiPhase[info.ID] = info.NextPhase;
                    multiPhase[info.NextPhase] = info.NextPhase;
                }
                // Rely on dupes coming after main cases
                if (info.DupeFrom > 0 && multiPhase.TryGetValue(info.DupeFrom, out int originalNext))
                {
                    multiPhase[info.ID] = originalNext;
                }
            }
            // For the sake of the mod being more interesting, exclude interesting enemies from being temporary
            HashSet<int> sekiroPhantomGroups = new HashSet<int>
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
            HashSet<string> eldenFrameMaps = game.GetEldenFrameMaps();
            // If in frame map and location is not important target and not allowframes, exclude it

            bool debugPlacement = false;
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
                // Custom case of perceptive/sensitive
                if (game.EldenRing && targetInfo.HasTag("sensitive") && sourceInfo.HasTag("perceptive"))
                {
                    // Doesn't apply with fixed source and non-perceptive target, which is manually nerfed
                    if (!sourceInfo.IsFixedSource || targetInfo.HasPerceptiveNose)
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's too perceptive");
                        return false;
                    }
                }
                // If not important target, do frames/allowframes
                if (game.EldenRing && sourceInfo.HasTag("frames")
                    && eldenFrameMaps.Contains(targetInfo.Map)
                    && !targetInfo.HasTag("allowframes") && !targetInfo.IsImportantTarget)
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because frames in {targetInfo.Map}");
                    return false;
                }

                // Exclusion for Divine Dragon: it is very boring unless the arena supports it
                if (sourceInfo.ItemName == "divinedragon" && targetInfo.DragonArenaData == null)
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because no Divine Dragon arena support");
                    return false;
                }
                // Try not to put interesting enemies in phantom spots. (will this be okay with 'oops all' sorts of modes?)
                if (sourceInfo.HasTag("unique") && defaultData[target].Group.Any(g => sekiroPhantomGroups.Contains(g)))
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it is a unique enemy into a transient target");
                    return false;
                }
                // Bosses in the same spot are boring
                if (targetInfo.IsImportantTarget)
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
                    // TODO this is probably fine for Sekiro too, but double-check
                    // Night minibosses are only three types of enemies in Elden Ring
                    if (game.DS3 || (game.EldenRing && targetInfo.Class != EnemyClass.NightMiniboss))
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
                // Elden Ring has a slightly different way of keeping track of this
                if (game.EldenRing && targetInfo.Class == EnemyClass.Boss && silo.Sources.Count > 1)
                {
                    multiPhase.TryGetValue(source, out int sourcePhase);
                    multiPhase.TryGetValue(target, out int targetPhase);
                    if (targetPhase > 0 && sourcePhase > 0 && sourcePhase == targetPhase)
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same final phase");
                        return false;
                    }
                    if (targetPhase > 0 && sourceInfo.HasTag("excludemultiphase"))
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because source has excludemultiphase");
                        return false;
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
            Dictionary<EnemyClass, string> sekiroRandomizeOpts = new Dictionary<EnemyClass, string>
            {
                [EnemyClass.Boss] = "bosses",
                [EnemyClass.Miniboss] = "minibosses",
                [EnemyClass.Basic] = "enemies",
            };
            bool isRandomized(EnemyClass type)
            {
                if (game.Sekiro)
                {
                    return !sekiroRandomizeOpts.TryGetValue(type, out string optName) || opt[optName];
                }
                else if (game.EldenRing)
                {
                    if (preset?.Classes != null && preset.Classes.TryGetValue(type, out ClassAssignment assign) && assign != null)
                    {
                        return !assign.NoRandom;
                    }
                }
                return true;
            }
            Dictionary<int, bool> singletons = infos.Values.Where(i => i.HasTag("singleton")).ToDictionary(i => i.ID, i => false);
            bool anyRandomized = false;

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

                anyRandomized = true;

                // If no actual sources added (could happen with some combination of forcemap and source removal), just add all of them to the default pool
                int randomCount = 0;
                int secondaryCount = 0;
                Random makeRandom(bool secondary = false)
                {
                    int rseed = seed + (int)silo.Type;
                    // Previously times 10, for DS3 and Sekiro, and no secondary
                    if (secondary)
                    {
                        // Keep stuff relatively the same which is not fixed within the silo
                        rseed += (secondaryCount++) * 100 + 50;
                    }
                    else
                    {
                        rseed += (randomCount++) * 100;
                    }
                    return new Random(rseed);
                }
                ClassAssignment classAssign = null;
                if (preset?.Classes != null)
                {
                    preset.Classes.TryGetValue(siloType, out classAssign);
                }
                List<int> sources = silo.Sources;
                if (sources.Count == 0)
                {
                    sources = infos.Values.Where(i => siloClass(i.Class) == siloType).Select(i => i.ID).ToList();
                    if (sources.Count == 0) throw new Exception($"Can't find any enemies to use for default pool for {siloType}");
                }
                if (preset != null)
                {
                    List<int> removedSources = sources
                        .Where(id =>
                            !preset.RemoveSourceIDs.Contains(id)
                                && (classAssign == null || !classAssign.RemoveSourceIDs.Contains(id)))
                        .ToList();
                    // If all sources are manually removed for some reason, use default class as a fallback.
                    if (removedSources.Count > 0)
                    {
                        sources = removedSources;
                    }
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
                    if (customPools == null && classAssign != null)
                    {
                        customPools = classAssign.Pools;
                    }
                    if (customPools != null)
                    {
                        custom = RandomSources.Create(makeRandom(true), customPools, sources, silo.Targets.Count);
                        // Adds are used for any custom pool class in Elden Ring, if defined
                        // nvm, disable this for now
                        if (false
                            && preset.Classes.TryGetValue(EnemyClass.Spectator, out ClassAssignment addAssign)
                            && addAssign != null
                            && addAssign.Pools != null)
                        {
                            adds = RandomSources.Create(makeRandom(true), addAssign.Pools, sources, silo.Targets.Count);
                        }
                    }
                    if (!game.EldenRing && silo.Type == EnemyClass.Basic && preset.Add != null)
                    {
                        adds = RandomSources.Create(makeRandom(true), preset.Add, sources, silo.Targets.Count);
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
                List<(int, int)> swapTargets = new List<(int, int)>();
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
                    if (swapHelperMapping.TryGetValue(source, out List<int> helperSources))
                    {
                        foreach (int helper in helperSources)
                        {
                            swapTargets.Add((target, helper));
                        }
                    }
                }
                if (siloType == EnemyClass.Boss) printPermutation(silo);

                if (swapTargets.Count > 0 && allSwapHelpers.Count > 0)
                {
                    List<int> swapSources = allSwapHelpers.ToList();
                    Shuffle(makeRandom(true), swapSources);
                    Shuffle(makeRandom(true), swapTargets);

                    int swapSourceIndex = 0;
                    for (int i = 0; i < swapTargets.Count; i++)
                    {
                        int swapSource = swapSources[(swapSourceIndex++) % swapSources.Count];
                        silo.SwapMapping[swapTargets[i]] = swapSource;
                        // Console.WriteLine($"Swapping {ename(swapTargets[i].Item2)}: source {ename(swapSource)} in {siloType}");
                    }
                }

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
                if (opt["silent"]) break;
                // if (siloType != EnemyClass.Boss) return null;
                Console.WriteLine($"-- {siloType} placements");
                string fullName(int ent, bool target)
                {
                    if (missingInfos.Contains(ent)) return $"missing #{ent}";
                    EnemyData data = defaultData[ent];
                    string name = infos[ent].ExtraName ?? game.ModelCharacterName(data.Model, data.Char);
                    string cat = infos[ent].Category;
                    if (cat != null) name = $"{phraseRe.Split(cat)[0]} {name}";
                    return $"{name} (#{ent}) {(target ? "in" : "from")} {game.LocationNames[game.Locations[data.MainMap]]}";
                }
                List<EnemyClass> printSilos = new List<EnemyClass> { siloType };
                if (siloType == EnemyClass.Boss)
                {
                    printSilos.Add(EnemyClass.TutorialBoss);
                    printSilos.Add(EnemyClass.FoldingMonkey);
                }
                else if (siloType == EnemyClass.Basic)
                {
                    // TODO: This shouldn't print out the silos one after another
                    if (game.DS3)
                    {
                        printSilos.Add(EnemyClass.HostileNPC);
                    }
                    else if (game.EldenRing)
                    {
                        printSilos.Add(EnemyClass.HostileNPC);
                        printSilos.Add(EnemyClass.Wildlife);
                    }
                }
                else if (siloType == EnemyClass.Miniboss)
                {
                    if (game.EldenRing)
                    {
                        printSilos.AddRange(new[]
                        {
                            EnemyClass.MinorBoss,
                            EnemyClass.NightMiniboss,
                            EnemyClass.DragonMiniboss,
                            EnemyClass.Evergaol,
                        });
                    }
                }
                SortedDictionary<int, int> printMapping = new SortedDictionary<int, int>();
                foreach (EnemyClass printSilo in printSilos)
                {
                    if (!isRandomized(printSilo))
                    {
                        continue;
                    }
                    foreach (KeyValuePair<int, int> transfer in silos[printSilo].Mapping)
                    {
                        if (autoForce.Contains(transfer.Key)) continue;
                        printMapping[transfer.Key] = transfer.Value;
                    }
                }
                bool printScale = opt["scale"] && ann.ScalingSections != null
                   ; // && (siloType == EnemyClass.Boss || siloType == EnemyClass.Miniboss);
                foreach (KeyValuePair<int, int> transfer in printMapping)
                {
                    string scale = "";
                    if (printScale
                        && getScalingSections(transfer.Value, transfer.Key, out int sourceSection, out int targetSection)
                        // && targetScalingSections.TryGetValue(transfer.Key, out int targetSection)
                        // && ann.ScalingSections.TryGetValue(transfer.Value, out int sourceSection)
                        && targetSection != sourceSection)
                    {
                        // -1 ideally should not happen
                        // TODO: This logic is duplicated from below; maybe make determinations about this independently
                        // if (!infos[transfer.Value].HasTag("noscale")
                        //    && !(infos[transfer.Value].HasTag("noscaleup") && targetSection > sourceSection))
                        {
                            scale = $" (scaling {sourceSection}->{targetSection})";
                        }
                    }
                    Console.WriteLine($"Replacing {fullName(transfer.Key, true)}: {fullName(transfer.Value, false)}{scale}");
                }
                if (printMapping.Count == 0)
                {
                    Console.WriteLine("(not randomized)");
                }
                Console.WriteLine();
#if !DEBUG
                for (int i = 0; i < 50; i++) Console.WriteLine();
#endif
            }

            // Region transplants
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
                            if (exclude.ContainsXZ(res))
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
            Dictionary<int, RegionTarget> copyRegions(
                string spec,
                int fromEntity,
                List<int> args,
                Dictionary<EventValue, EventValue> reloc,
                bool replace = false)
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
                string fromMap = defaultData[fromEntity].MainMap;
                if (defaultData[fromEntity].DupeMap != null)
                {
                    throw new Exception($"Error: {fromEntity} regions can't be copied as it exists in {string.Join(", ", defaultData[fromEntity].Maps)}");
                }
                int toEntity = reloc[EventValue.Enemy(fromEntity)].IntID;
                string toMap = defaultData[toEntity].MainMap;
                TEnemy e = enemyEditor.GetEnemy(maps[toMap], toEntity);
                TEnemy eSource = enemyEditor.GetEnemy(maps[fromMap], fromEntity);
                if (e == null) throw new Exception($"Internal error: can't find {toEntity} in {toMap} for {spec}");
                if (eSource == null) throw new Exception($"Internal error: can't find {fromEntity} in {fromMap} for {spec}");
                Dictionary<int, RegionTarget> res = new Dictionary<int, RegionTarget>();
                // Note, infos[toEntity] may not exist if fromEntity is a helper
                // However, defaultData[toEntity] is added if it didn't exist previously
                bool expectArena = false;
                bool isDupe = false;
                int dupeIndex = -1;
                if (infos.TryGetValue(toEntity, out EnemyInfo toInfo))
                {
                    if (toInfo.DupeFrom == fromEntity)
                    {
                        isDupe = true;
                        // For basic warp/detection/etc. arenas, preserve them for dupes
                        // (except in Elden Ring, try to shift some slightly, and in Sekiro, just randomize them)
                        // Otherwise, have some special placement logic below
                        if (type.Contains("gen") || (game.Sekiro && type.Contains("chr")) || (game.EldenRing && type == "chrpoint"))
                        {
                            // Rewrite randomly or with strafe offset
                            dupeIndex = toInfo.DupeIndex;
                        }
                        else
                        {
                            return res;
                        }
                    }
                    expectArena = toInfo.IsArenaTarget;
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
                if (type.StartsWith("chrpoint"))
                {
                    Vector3 diff = new Vector3();
                    string remainder = type.Replace("chrpoint", "");
                    if (remainder.Length > 0)
                    {
                        if (!int.TryParse(remainder, out int heightDiff)) throw new Exception($"Invalid chrpoint {spec}");
                        diff = new Vector3(0, heightDiff, 0);
                    }
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, List<TRegion> bs, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                        foreach (TRegion b in bs)
                        {
                            if (dupeIndex == -1)
                            {
                                b.Position = e.Position + diff;
                                b.Rotation = e.Rotation;
                            }
                            else
                            {
                                // getDupeOffset, as it is, can rewrite the base offset, which we don't want here.
                                // Just strafe very slightly instead
                                b.Position += getQuadrantOffset(b.Rotation, 0.3f, dupeIndex);
                            }
                        }
                        setCacheableRegion(region, bID);
                    }
                }
                else if (type == "chrrel")
                {
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, List<TRegion> bs, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                        foreach (TRegion b in bs)
                        {
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
                }
                else if (type.StartsWith("chrgen"))
                {
                    float height = 0;
                    string remainder = type.Replace("chrgen", "");
                    if (remainder == "angel")
                    {
                        height = angelHeight;
                    }
                    else if (remainder == "student")
                    {
                        height = 5;
                    }
                    else if (remainder.Length > 0)
                    {
                        if (!int.TryParse(remainder, out int heightDiff)) throw new Exception($"Invalid chrpoint {spec}");
                        height = heightDiff;
                    }
                    foreach (int region in regions)
                    {
                        if (useExisting(region)) continue;
                        (List<TRegion> bs, int newGen) = enemyEditor.MakeGeneratorCopy(maps, newEntity, fromMap, toMap, region, generators, reloc);
                        if (bs != null)
                        {
                            foreach (TRegion b in bs)
                            {
                                // Generator positions can be the same for dupes
                                if (isDupe) break;
                                b.Position = e.Position + new Vector3(0, height, 0);
                                if (remainder == "student")
                                {
                                    // 4 in each direction is probably fine. Becomes an issue with Makar
                                    float spread() => ((float)arenaRandom.NextDouble() - 0.5f) * 8;
                                    b.Position += new Vector3(spread(), 0, spread());
                                }
                                b.Rotation = e.Rotation;
                            }
                            setCacheableRegion(region, newGen);
                        }
                    }
                }
                else if (type == "inside")
                {
                    // Do this later, for slightly better teleports
                }
                // TODO: Make arenas work alongside isDupe
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
                            (TRegion a, List<TRegion> bs, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                            TRegion b = bs[0];
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
                                (TRegion a, List<TRegion> bs, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                                TRegion b = bs[0];
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
                        (TRegion a, List<TRegion> bs, int bID) = enemyEditor.MakeRegionCopy(maps, newEntity, fromMap, toMap, region, replace);
                        TRegion b = bs[0];
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

            ParamDictionary Params = game.Params;
            string noDead = game.EldenRing ? "disableInitializeDead" : "disableIntiliazeDead";
            if (opt["dumpnpc"])
            {
                HashSet<int> npcPrintInfo = new HashSet<int>();
                foreach (EnemyInfo info in infos.Values.Where(i => i.Class != EnemyClass.None).OrderBy(i => i.ID))
                {
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    if (row == null || !npcPrintInfo.Add(row.ID)) continue;
                    if ((float)row["hitHeight"].Value > 3 && !info.HasTag("large"))
                    {
                        Console.WriteLine($"Radius {row["hitRadius"].Value} Height {row["hitHeight"].Value} - {info.DebugText}");
                        if (game.EldenRing && !row["hitHeight"].Value.Equals(row["chrHitHeight"].Value))
                        {
                            Console.WriteLine($"    Separate - Radius {row["chrHitRadius"].Value} Height {row["chrHitHeight"].Value}");
                        }
                    }
                }
                Console.WriteLine();
                npcPrintInfo.Clear();
                foreach (EnemyInfo info in infos.Values.Where(i => i.Class != EnemyClass.None).OrderBy(i => i.ID))
                {
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    if (row == null || !npcPrintInfo.Add(row.ID)) continue;
                    Console.WriteLine($"Dead {row[noDead].Value} Respawn {row["disableRespawn"].Value} - {info.DebugText}");
                }
                foreach (EnemyInfo info in infos.Values)
                {
                    // In Elden Ring, these mostly apply to friendly NPCs
                    // if (info.Class == EnemyClass.None) continue;
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    if (row == null) continue;
                    string npcLot = game.EldenRing ? "itemLotId_map" : "ItemLotId1";
                    string lotParam = game.EldenRing ? "ItemLotParam_map" : "ItemLotParam";
                    int itemLot = (int)row[npcLot].Value;
                    PARAM.Row item = Params[lotParam][itemLot];
                    if (item == null)
                    {
                        if (itemLot > 0) Console.WriteLine($"Item bad reference: {itemLot} in {info.DebugText}");
                        continue;
                    }
                    bool hasFlag = false;
                    while (item != null)
                    {
                        object flag = item["getItemFlagId"].Value;
                        if ((flag is int val && val > 0) || (flag is uint uval && uval > 0))
                        {
                            hasFlag = true;
                            break;
                        }
                        itemLot++;
                        item = Params[lotParam][itemLot];
                    }
                    if (game.EldenRing || hasFlag)
                    {
                        Console.WriteLine($"Item {hasFlag}: {info.DebugText}");
                    }
                }
                // u16, u8, f32
                // Also, Tanith Knight, Black Blade Kindred.
                Dictionary<string, int> thinkFields = new Dictionary<string, int>
                {
                    ["searchEye_dist"] = 21, // 50
                    // ["searchEye_angY"] = 60,
                    ["SightTargetForgetTime"] = 100, // 10
                    ["eye_dist"] = 10,
                    ["nose_dist"] = 1,
                    ["maxBackhomeDist"] = 100,
                    ["backhomeDist"] = 100,
                    ["backhomeBattleDist"] = 100,
                    ["nonBattleActLife"] = 10
                };

                Console.WriteLine();
                npcPrintInfo.Clear();
                foreach (EnemyInfo info in infos.Values.Where(i => i.Class != EnemyClass.None))
                {
                    PARAM.Row row = Params["NpcThinkParam"][defaultData[info.ID].Think];
                    if (row == null || !npcPrintInfo.Add(row.ID)) continue;
                    List<string> fields = new List<string>();
                    foreach (KeyValuePair<string, int> field in thinkFields)
                    {
                        object obj = row[field.Key].Value;
                        int val;
                        if (obj is ushort uval) val = uval;
                        else if (obj is byte bval) val = bval;
                        else if (obj is float fval) val = (int)fval;
                        else throw new Exception(field.Key);
                        if (val >= field.Value)
                        {
                            fields.Add($"{field.Key}={val}");
                        }
                    }
                    if (fields.Count > 0)
                    {
                        Console.WriteLine($"Think {string.Join(", ", fields)} - {info.DebugText}");
                    }
                }

                return null;
            }
            if (opt["dumpscaling"])
            {
                Dictionary<int, string> sps = File.ReadLines("er_speffects.txt")
                    .Select(l => l.Split(": "))
                    .ToDictionary(p => int.Parse(p[0]), p => p[1]);

                SortedDictionary<string, SortedDictionary<int, List<string>>> mapSps =
                    new SortedDictionary<string, SortedDictionary<int, List<string>>>();
                SortedDictionary<int, List<string>> spMaps =
                    new SortedDictionary<int, List<string>>();
                HashSet<(string, int)> usedNpcs = new HashSet<(string, int)>();
                Dictionary<int, uint> bossSouls = game.Params["GameAreaParam"].Rows.ToDictionary(r => r.ID, r => (uint)r["bonusSoul_single"].Value);
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.Class == EnemyClass.None) continue;
                    PARAM.Row row = Params["NpcParam"][defaultData[info.ID].NPC];
                    if (row == null) continue;
                    if (!usedNpcs.Add((info.Map, row.ID))) continue;
                    int scalingSp = (int)row["spEffectID3"].Value;
                    string bossSoul = bossSouls.TryGetValue(info.ID, out uint soul) ? $" <{soul}>" : "";
                    string text = $"{info.DebugText} - {row["getSoul"].Value}{bossSoul} runes";
                    AddMultiNest(mapSps, info.Map, scalingSp, text);
                    AddMulti(spMaps, scalingSp, text);
                    // Console.WriteLine($"{scalingSp}: {info.DebugText}");
                }
                bool humanSps = opt["humanonly"];
                foreach (var entry in spMaps)
                {
                    Console.WriteLine("--- " + entry.Key);
                    foreach (string info in entry.Value)
                    {
                        Console.WriteLine($"{entry.Key}: {info}");
                    }
                    Console.WriteLine();
                }
                foreach (var entry in mapSps)
                {
                    SortedDictionary<int, List<string>> mapSp = entry.Value;
                    if (humanSps && entry.Value.All(v => v.Key < 10000)) continue;
                    Console.WriteLine("--- " + game.MapLocationName(entry.Key));
                    Console.WriteLine($"Regular sps: {string.Join(", ", mapSp.Select(e => $"{e.Key} ({e.Value.Count} count)"))}");
                    foreach (int sp in mapSp.Keys)
                    {
                        if (sp <= 0) continue;
                        Console.WriteLine($"{sp}: {sps[sp]}");
                    }
                    foreach (KeyValuePair<int, List<string>> entry2 in mapSp)
                    {
                        if (humanSps && entry2.Key < 10000) continue;
                        foreach (string info in entry2.Value)
                        {
                            Console.WriteLine($"{entry2.Key}: {info}");
                        }
                    }
                    Console.WriteLine();
                }
                return null;
            }

            // Now altered NPC params
            // NPC -> team id. Default team affiliations. Most are removed and later reapplied (like in DS1 enemy rando)
            Dictionary<int, byte> npcOriginalTeam = new Dictionary<int, byte>();
            // NPC -> speffects. Make sure that swamp-immune enemies remain swamp-immune
            Dictionary<int, List<int>> npcCopySpEffect = new Dictionary<int, List<int>>();
            // NPC -> (item lot, event flag). Changed from NPC drops to scripted drops
            Dictionary<int, (int, int)> npcItemLots = new Dictionary<int, (int, int)>();
            string subSizeName = game.EldenRing ? "chr" : "sub";
            void shrinkNpc(PARAM.Row row, float radius, float height)
            {
                if ((float)row["hitRadius"].Value > radius)
                {
                    row["hitRadius"].Value = row[$"{subSizeName}HitRadius"].Value = radius;
                }
                if ((float)row["hitHeight"].Value > height)
                {
                    row["hitHeight"].Value = row[$"{subSizeName}HitHeight"].Value = height;
                }
            }

            HashSet<int> noBackHome = new HashSet<int> { 47600900, 47700200, 47701200 };
            void shrinkRange(PARAM.Row row, int range)
            {
                if (row == null || !anyRandomized) return;
                if ((ushort)row["eye_dist"].Value > range)
                {
                    row["eye_dist"].Value = (ushort)range;
                }
                if ((ushort)row["nose_dist"].Value > range)
                {
                    row["nose_dist"].Value = (ushort)range;
                }
                // This causes issues in non-random dupe fights, though just disable in dupe mode entirely for now
                // This is mainly for bosses tracking you super long distances in the overworld, but disrupts vanilla Fire Giant/Gargoyles.
                // Temp hack: hardcode specific rows and let them be nightmares for the time being.
                if ((ushort)row["maxBackhomeDist"].Value > range && !noBackHome.Contains(row.ID))
                {
                    row["maxBackhomeDist"].Value = (ushort)range;
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
            else if (game.DS3)
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
            else if (game.EldenRing)
            {
                // Poison and Rot
                int[] resistSps = new[] { 90000, 90010 };
                List<EnemyClass> teamClasses = new List<EnemyClass>
                {
                    EnemyClass.Basic, EnemyClass.Wildlife, EnemyClass.HostileNPC,
                    EnemyClass.Miniboss, EnemyClass.NightMiniboss, EnemyClass.DragonMiniboss, EnemyClass.Evergaol,
                };
                if (anyDupeEnabled)
                {
                    // Be a bit more invasive if dupes present
                    teamClasses.AddRange(new[] { EnemyClass.Boss, EnemyClass.MinorBoss, EnemyClass.CaravanTroll });
                }
                HashSet<int> transferTeams = new HashSet<int>(
                    infos.Values.Where(i => teamClasses.Contains(i.Class)).Select(i => defaultData[i.ID].NPC));
                // Enemies which don't get getBasicAsBoss
                // Rennala seems to crash without helpers being nodead
                HashSet<int> noDeadNpcs = new HashSet<int>(
                    infos.Values.Where(i => i.Class != EnemyClass.None && (i.IsBuffSource || (i.OwnedBy > 0 && infos[i.OwnedBy].IsBuffSource)))
                        .Select(i => defaultData[i.ID].NPC));
                HashSet<int> hostileNpcs = new HashSet<int>(
                    infos.Values.Where(i => i.Class == EnemyClass.HostileNPC).Select(i => defaultData[i.ID].NPC));
                foreach (PARAM.Row row in Params["NpcParam"].Rows)
                {
                    byte teamType = (byte)row["teamType"].Value;
                    // Transfer teams, for Enemy StrongEnemy Enemy2 ArchEnemyTeam (6 7 24 33), for Basic randomized
                    // TODO: get a condition for Elden Ring? DS3 is (teamType == 7 || teamType == 24 || teamType == 33)
                    if (transferTeams.Contains(row.ID))
                    {
                        npcOriginalTeam[row.ID] = teamType;
                        row["teamType"].Value = (byte)6;
                    }
                    if (noDeadNpcs.Contains(row.ID))
                    {
                        // if ((byte)row[noDead].Value == 0) Console.WriteLine($"making nodead: {infos.Values.Where(i => defaultData[i.ID].NPC == row.ID).Select(i => i.DebugText).FirstOrDefault()}");
                        // Make more enemies feasible for phase 1s, e.g. Godskin Duo
                        row[noDead].Value = (byte)1;
                    }
                    if (hostileNpcs.Contains(row.ID))
                    {
                        // The only place these NPCs can go is invasions or unique encounters, and
                        // this is required to prevent invaders from respawning as regular enemies.
                        // TODO revisit this if merging with other categories is implemented.
                        // Could also make an event to ForceCharacterDeath defeated invaders.
                        row["disableRespawn"].Value = (byte)1;
                    }

                    // Resists for Poison Rot are only in 6 7 respectively
                    for (int i = 6; i <= 7; i++)
                    {
                        int sp = (int)row[$"spEffectID{i}"].Value;
                        if (resistSps.Contains(sp))
                        {
                            AddMulti(npcCopySpEffect, row.ID, sp);
                        }
                    }
                    // Hack for lava dwellers, use debug (?) speffect
                    // 99100: fireDamageCutRate = 0, effectTargetOpposeTarget = 1, effectTargetFriendlyTarget = 1
                    if ((float)row["fireDamageCutRate"].Value < 0.01)
                    {
                        AddMulti(npcCopySpEffect, row.ID, 99100);
                    }

                    // This seems like an okay radius/height for bosses
                    // 1f, 2.1f prevents slowdowns...
                    shrinkNpc(row, 2.5f, 5.1f);
                }

                if (opt["bossbgm"])
                {
                    // Swap around >1 threatLvs if randomizing boss BGMs.
                    // At present, having infinite nose range is equivalent to having manual BGM. A new bool can be introduced if this changes.
                    // threatLv may be read directly later (but event checks are generally preprocessed out).
                    HashSet<int> worldBossNpcs = new HashSet<int>(
                        infos.Values.Where(i => i.IsImportantTarget && !i.HasPerceptiveNose).Select(i => defaultData[i.ID].NPC));

                    List<uint> threatLvs = new List<uint>();
                    foreach (PARAM.Row row in Params["NpcParam"].Rows)
                    {
                        if (!worldBossNpcs.Contains(row.ID)) continue;
                        uint threatLv = (uint)row["threatLv"].Value;
                        if (threatLv > 1)
                        {
                            threatLvs.Add(threatLv);
                        }
                    }
                    Shuffle(new Random(seed + 400), threatLvs);
                    // Console.WriteLine($"threat levels: {string.Join(",", threatLvs)}");
                    int index = 0;
                    foreach (PARAM.Row row in Params["NpcParam"].Rows)
                    {
                        if (!worldBossNpcs.Contains(row.ID)) continue;
                        uint threatLv = (uint)row["threatLv"].Value;
                        if (threatLv > 1)
                        {
                            uint newThreatLv = threatLvs[index++ % threatLvs.Count];
                            row["threatLv"].Value = newThreatLv;
                        }
                    }
                }

                // Elden Ring version of DS3 item stuff, with a few more constraints here too
                // Always do it for all eligible enemies so that restart isn't required between randomizations
                foreach (EnemyInfo info in infos.Values.Where(i => i.HasTag("npcitem")))
                {
                    // Don't need it for vanilla enemy onslaught, even though it leads to duplicates
                    if (!anyRandomized) break;
                    int npc = defaultData[info.ID].NPC;
                    if (npcItemLots.ContainsKey(npc)) continue;
                    PARAM.Row row = Params["NpcParam"][npc];
                    void getFlagRows(string lotType, out int baseItemLot, out List<PARAM.Row> rows)
                    {
                        baseItemLot = -1;
                        rows = new List<PARAM.Row>();
                        string lotField = $"itemLotId_{lotType}";
                        string lotParam = $"ItemLotParam_{lotType}";
                        baseItemLot = (int)row[lotField].Value;
                        if (baseItemLot <= 0) return;
                        int itemLot = baseItemLot;
                        PARAM.Row item = game.Params[lotParam][itemLot];
                        // In the case of an invalid reference, don't fill in rows, but leave
                        // base lot as-is to avoid dealing with removing this reference.
                        if (item == null) return;
                        while (item != null)
                        {
                            // Require a guaranteed drop
                            uint flag = (uint)item["getItemFlagId"].Value;
                            if (flag > 0)
                            {
                                int totalDrop = Enumerable.Range(1, 8).Sum(i => (ushort)item[$"lotItemBasePoint0{i}"].Value);
                                if (totalDrop > 0 && Enumerable.Range(1, 8).Any(i => totalDrop == (ushort)item[$"lotItemBasePoint0{i}"].Value))
                                {
                                    rows.Add(item);
                                }
                            }
                            item = game.Params[lotParam][++itemLot];
                        }
                    }
                    getFlagRows("map", out int mapLot, out List<PARAM.Row> mapItems);
                    getFlagRows("enemy", out int enemyLot, out List<PARAM.Row> enemyItems);
                    // "enemy" requires rewriting to map. "map" is a no-op for currently randomized enemies,
                    // has things like Bell Bearing otherwise. "enemy" is also mixed between flag lots and non-flag ones.
                    // So for "map", treat it like DS3 and just change it over entirely.
                    // For "enemy", make a duplicate "map" lot with just the flag-based items.
                    if (mapItems.Count > 0)
                    {
                        List<uint> flags = mapItems.Select(r => (uint)r["getItemFlagId"].Value).Distinct().ToList();
                        // if (flags.Count > 0) Console.WriteLine($"map flags {npc} = {string.Join(", ", flags)}");
                        // Flag uniqueness is checked when adding npcitem
                        if (flags.Count == 1)
                        {
                            npcItemLots[npc] = (mapLot, (int)flags.First());
                            row[$"itemLotId_map"].Value = -1;
                        }
                    }
                    else if (enemyItems.Count > 0 && mapLot <= 0)
                    {
                        List<uint> flags = enemyItems.Select(r => (uint)r["getItemFlagId"].Value).Distinct().ToList();
                        // if (flags.Count > 0) Console.WriteLine($"enemy flags {npc} = {string.Join(", ", flags)}");
                        // Flag uniqueness is checked when adding npcitem
                        if (flags.Count == 1)
                        {
                            // Copy over rows, and blank out originals
                            int newBaseMapLot = enemyLot;
                            if (game.HasMods)
                            {
                                // The lot is free real estate in vanilla, but some mods use it. Unfortunately expensive to look up.
                                while (Params["ItemLotParam_map"][newBaseMapLot] != null)
                                {
                                    newBaseMapLot += 10;
                                }
                            }
                            int newMapLot = newBaseMapLot;
                            foreach (PARAM.Row item in enemyItems)
                            {
                                PARAM.Row newItem = game.AddRow("ItemLotParam_map", newMapLot++);
                                GameEditor.CopyRow(item, newItem);
                                item["getItemFlagId"].Value = (uint)0;
                                for (int i = 1; i <= 8; i++)
                                {
                                    // This should be enough
                                    item[$"lotItemBasePoint0{i}"].Value = (ushort)0;
                                    item[$"lotItemId0{i}"].Value = 0;
                                    item[$"lotItemCategory0{i}"].Value = 0;
                                }
                            }
                            npcItemLots[npc] = (newBaseMapLot, (int)flags.First());
                        }
                    }
                }

                HashSet<int> humanThinks = new HashSet<int>(
                    infos.Values.Where(i => i.Class == EnemyClass.HostileNPC).Select(i => defaultData[i.ID].Think));
                foreach (PARAM.Row row in Params["NpcThinkParam"].Rows)
                {
                    // max out nose_dist and eye_dist at 100. (basic gets further manual edits)
                    // Especially fix Tanith's Knight, which has 9999 nose_dist
                    // Fire Giant is 100 eye_dist, 999 nose_dist
                    shrinkRange(row, 100);
                    // Further shrink Tanith's Knight to be close to other Crucible Knights (20)
                    if (row.ID == 25009000)
                    {
                        shrinkRange(row, 30);
                    }
                    // Moongrum, Miriam, and Flame Guardians exist out in the world, so prevent the latter from attacking
                    // They generally have eye_dist around 20, but invaders have 9999 nose_dist
                    if (humanThinks.Contains(row.ID))
                    {
                        shrinkRange(row, 50);
                    }
                    // Enemy Onslaught traditional mode
                    if (opt["impolite"])
                    {
                        row["TeamAttackEffectivity"].Value = (byte)0;
                    }
                }

                // Undo CreatedReferredDamagePair
                uint hp;
                // Hoarah Loux p2 (47210070, HP 3186 scaling 7160) <- p1 (47200070, HP 1721 scaling 7160) dead
                PARAM.Row hoarah1 = Params["NpcParam"][defaultData[11050801].NPC];
                PARAM.Row hoarah2 = Params["NpcParam"][defaultData[11050800].NPC];
                hp = (uint)hoarah2["hp"].Value - (uint)hoarah1["hp"].Value;
                if (hp > 500 && hp < 5000)
                {
                    // Console.WriteLine($"hoarah: {hoarah2["hp"].Value}->{hp}");
                    hoarah2["hp"].Value = hp;
                }
                // Maliketh p2 (21101072, HP 1588 scaling 7150) <- p1 (21100072, HP 1588 scaling 7150) at 55% health
                // TODO: When not randomized, the 55% health condition is used
                PARAM.Row maliketh1 = Params["NpcParam"][defaultData[13000801].NPC];
                PARAM.Row maliketh2 = Params["NpcParam"][defaultData[13000800].NPC];
                hp = (uint)maliketh1["hp"].Value * 45 / 100;
                // Console.WriteLine($"maliketh: {maliketh1["hp"].Value}->{hp}");
                maliketh1["hp"].Value = hp;
                hp = (uint)maliketh2["hp"].Value - hp;
                if (hp > 500 && hp < 5000)
                {
                    // Console.WriteLine($"maliketh: {maliketh2["hp"].Value}->{hp}");
                    maliketh2["hp"].Value = hp;
                }
                // Fire Giant p2 (47601050, HP 6592 scaling 7140) <- p1 (47600050, HP 3489 scaling 7140) dead, also takes 10% immediate damage
                PARAM.Row giant1 = Params["NpcParam"][defaultData[1052520801].NPC];
                PARAM.Row giant2 = Params["NpcParam"][defaultData[1052520800].NPC];
                hp = ((uint)giant2["hp"].Value - (uint)giant1["hp"].Value) * 90 / 100;
                if (hp > 500 && hp < 5000)
                {
                    // Console.WriteLine($"firegiant: {giant2["hp"].Value}->{hp}");
                    giant2["hp"].Value = hp;
                }
            }

            // Duplicate npc params
            // For bosses, make a copy of them with XP drops (Experience) and money drops (getSoul)
            // For minibosses, prepare to make a copy of them if regular enemies go there, with modified Hp, stamina/staminaRecoverBaseVal?, and HealthbarNum
            Dictionary<string, HashSet<int>> existingParamIds = new Dictionary<string, HashSet<int>>();
            PARAM.Row duplicateAdjacentRow(string paramName, int baseId)
            {
                if (!existingParamIds.TryGetValue(paramName, out HashSet<int> existingIds))
                {
                    existingParamIds[paramName] = existingIds =
                        new HashSet<int>(Params[paramName].Rows.Select(e => e.ID));
                }
                PARAM.Row baseRow = Params[paramName][baseId];
                if (baseRow == null) throw new Exception($"{paramName} {baseId} is referenced but does not exist");
                int copyId = baseId;
                while (existingIds.Contains(copyId)) copyId++;
                existingIds.Add(copyId);
                PARAM.Row copyRow = game.AddRow(paramName, copyId);
                GameEditor.CopyRow(baseRow, copyRow);
                return copyRow;
            }

            // Preemptively make copies of all bosses as basic enemies and minibosses. Also monkeys, since they have nothing by default.
            Dictionary<int, int> bossAsBasicNpc = new Dictionary<int, int>();
            Dictionary<int, int> bossAsBasicThink = new Dictionary<int, int>();
            Random reward = new Random(seed + 40);
            List<EnemyInfo> infosToBasic = infos.Values.Where(i => i.IsFixedSource).OrderBy(i => i.ID).ToList();
            if (game.EldenRing)
            {
                infosToBasic.AddRange(infos.Values
                    .Where(i => i.OwnedBy > 0 && infos.TryGetValue(i.OwnedBy, out EnemyInfo own) && own.IsFixedSource)
                    .OrderBy(i => i.ID)
                    .ToList());
            }
            foreach (EnemyInfo info in infosToBasic)
            {
                // Handle missing infos
                if (info.Class == EnemyClass.None) continue;
                int source = info.ID;
                int baseNpc = defaultData[source].NPC;
                PARAM.Row row = duplicateAdjacentRow("NpcParam", baseNpc);
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
                else if (game.DS3)
                {
                    // This is probably too low
                    row["getSoul"].Value = info.HasTag("early") ? reward.Next(100, 500) : (info.HasTag("mid") ? reward.Next(500, 1000) : reward.Next(1000, 2000));
                }
                else if (game.EldenRing && !info.HasTag("nosouls"))
                {
                    // Note this should also be scaled up/down
                    double amt = reward.Next(800, 1200);
                    if (info.Class == EnemyClass.Boss) amt *= 1.5;
                    if (ann.ScalingSections.TryGetValue(source, out int section)
                        && section > 0 && section <= ScalingEffects.EldenSoulScaling.Count)
                    {
                        amt *= ScalingEffects.EldenSoulScaling[section - 1];
                    }
                    if (info.Class == EnemyClass.Helper && !info.HasTag("mainhelper"))
                    {
                        // For helper, minimum of 50 and max of 250
                        amt /= 5;
                    }
                    // Console.WriteLine($"Souls {(int)amt} for {info.DebugText}");
                    // This will get wacky with scaling anyway
                    // if (amt > 5000) amt = amt / 500 * 500; else amt = amt / 100 * 100;
                    row["getSoul"].Value = (int)amt;
                }
                row[noDead].Value = (byte)0;
                row["disableRespawn"].Value = (byte)0;
                if (game.DS3)
                {
                    // Marker speffect for non-boss instances, for use in manual AI scripts
                    // There are no easy slots for doing this in Elden Ring
                    row["spEffectId20"].Value = 6969;
                }
                if (game.EldenRing)
                {
                    if ((uint)row["threatLv"].Value == 0)
                    {
                        row["threatLv"].Value = 1;
                    }
                    row["isSoulGetByBoss"].Value = (byte)0;
                }
                bossAsBasicNpc[source] = row.ID;

                if (game.EldenRing)
                {
                    int baseThink = defaultData[source].Think;
                    PARAM.Row think = Params["NpcThinkParam"][baseThink];
                    // Make overworld instances of bosses less able to track you down
                    // Try to not to nerf nose too much, in case their sight is not great
                    if (think != null)
                    {
                        think = duplicateAdjacentRow("NpcThinkParam", baseThink);
                        think["nose_dist"].Value = Math.Min((ushort)think["nose_dist"].Value, (ushort)5);
                        think["eye_dist"].Value = Math.Min((ushort)think["eye_dist"].Value, (ushort)30);
                        bossAsBasicThink[source] = think.ID;
                    }
                }
            }

            // Lazily make copies of regular enemies as specific bosses/minibosses, will basically only apply in 'oops all' mode.
            // Miniboss targets should only occur once so this does not need to be memoized.
            List<string> basicBuffFields = game.Sekiro
                ? new List<string> { "Experience", "getSoul", "Hp", "stamina", "staminaRecoverBaseVal" }
                : new List<string> { "getSoul", game.EldenRing ? "hp" : "Hp" };
            SortedSet<int> noDeathblow = ann.GetGroup("nodeathblow");
            int getBasicAsBoss(int source, int target)
            {
                int baseNpc = defaultData[source].NPC;

                PARAM.Row targetRow = Params["NpcParam"][defaultData[target].NPC];
                if (targetRow == null) return baseNpc;

                PARAM.Row row = duplicateAdjacentRow("NpcParam", baseNpc);
                // Just make a new row every time, and also do speffect-based warn
                infos.TryGetValue(target, out EnemyInfo targetInfo);
                if (game.EldenRing
                    ? presetOpt("bosshp")
                    : (true || (preset != null && preset.BuffBasicEnemiesAsBosses)))
                {
                    foreach (string field in basicBuffFields)
                    {
                        object newVal = targetRow[field].Value;
                        // Slightly buff enemies as bosses by taking geometric mean of HP values
                        // TODO: Boilerplate
                        if (row[field].Value is int sourceInt && targetRow[field].Value is int targetInt)
                        {
                            if (targetInt > sourceInt) newVal = (int)Math.Sqrt(sourceInt * targetInt);
                        }
                        else if (row[field].Value is uint sourceUint && targetRow[field].Value is uint targetUint)
                        {
                            if (targetUint > sourceUint) newVal = (uint)Math.Sqrt(sourceUint * targetUint);
                        }
                        else if (row[field].Value is short sourceShort && targetRow[field].Value is short targetShort)
                        {
                            if (targetShort > sourceShort) newVal = (short)Math.Sqrt(sourceShort * targetShort);
                        }
                        else throw new Exception($"Unknown NpcParam field type {field} {newVal.GetType()}");
                        row[field].Value = newVal;
                    }
                    if (game.Sekiro && !noDeathblow.Contains(source) && targetInfo != null && targetInfo.IsImportantTarget)
                    {
                        row["HealthbarNum"].Value = targetInfo.IsBossTarget ? (byte)targetInfo.Phases : targetRow["HealthbarNum"].Value;
                    }
                }
                if (targetInfo != null && targetInfo.IsFixedSource)
                {
                    row["getSoul"].Value = 0;
                }
                row[noDead].Value = (byte)1;
                return row.ID;
            }

            int helperModelBase = 100;
            bool isAnythingRandomized = false;
            // Mapping of enemies which are randomized, from source to target
            Dictionary<int, List<int>> mapping = new Dictionary<int, List<int>>();
            // Another one from target to source
            Dictionary<int, int> revMapping = new Dictionary<int, int>();
            // Mapping of new helpers, (owner target, source helper) -> target helper
            // More of a reverse mapping
            Dictionary<(int, int), int> helperMapping = new Dictionary<(int, int), int>();
            // To support swapboss mode, mapping from (owner target, source helper) -> other source helper
            // This is mainly for scaling.
            Dictionary<(int, int), int> swapMapping = new Dictionary<(int, int), int>();

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
            // This can be >10k for Elden Ring
            int requiredGroups = totalTargetCounts.Select(s => (infos[s.Key].Groups?.Count ?? 0) * s.Value).Sum();
            bool multichrSwitch = true;
            bool enableMultichr(int source, int target)
            {
                if (!multichrSwitch) return false;
                // Allow splits, although this may get unwieldy
                if (source == infos[target].SplitFrom)
                {
                    // It's too much for Gideon, who comes with many c0000s
                    if (source == 11050850) return false;
                    return true;
                }
                if (game.Sekiro)
                {
                    // Hirata Masanaga. Don't forbid having dogs past the limit, but limit the global number of dogs.
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
                else if (game.EldenRing)
                {
                    if (infos[target].IsImportantTarget) return true;
                    if (infos[source].HasTag("bossonlymulti")) return false;
                    // TODO: Any way to automate these? Helper count?
                    // Rennala 2
                    if (source == 14000800 && !withinMaxAllowed(source, target, 50)) return false;
                    // Malenia
                    if (source == 15000800 && !withinMaxAllowed(source, target, 50)) return false;
                    // Yelough Astel
                    if (source == 32110800 && !withinMaxAllowed(source, target, 50)) return false;
                    // Mariners, many helpers
                    if (infos[source].HasTag("mariner") && !withinMaxAllowed(source, target, 20)) return false;
                    // Human bosses are an issue. Oops All Mimic Tear, Stray wouldn't work
                    // Group limit - can probably get more ids though
                    if (infos[source].Groups != null && infos[source].Groups.Count > 0 && requiredGroups > 500)
                    {
                        return (target.GetHashCode() % requiredGroups) < 500;
                    }
                }
                return true;
            }
            int partsNeeded = totalTargetCounts.Where(s => infos[s.Key].HasTag("npcpart")).Sum(s => s.Value);
            // There can be no more than a handful of npc parts loaded at once locally.
            // Try using a safe global limit. (Tested in Sekiro, ballparked in DS3, no idea in Elden Ring)
            // Alternatively, could try to track this down per map and use map connections.
            bool partsRestricted = partsNeeded >= 48;
            if (game.DS3)
            {
                partsRestricted = partsNeeded >= 96;
            }
            if (game.EldenRing)
            {
                partsRestricted = partsNeeded >= 200;
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
                Dictionary<int, List<TEnemy>> dupedHelpers = new Dictionary<int, List<TEnemy>>();

                foreach (EnemyPermutation silo in silos.Values)
                {
                    foreach (KeyValuePair<int, int> transfer in silo.Mapping)
                    {
                        int target = transfer.Key;
                        int source = transfer.Value;
                        // Process targets in this map
                        if (!defaultData[target].Maps.Contains(entry.Key)) continue;
                        // Treat self-randomizations as none at all. Try to avoid these for bosses/minibosses though, for variety.
                        if (source == target) continue;
                        // TODO: Put all specific locations in config as primary arena
                        bool dupeTarget = infos[target].HasTag("dupe") && infos[target].DupeFrom == source;

                        EnemyData data = defaultData[source];
                        if (!dupeTarget)
                        {
                            isAnythingRandomized = true;
                            // Maybe have a different pass for this - but it's a reasonable way to either use anim data or not
                            data.Anim = -1;
                        }
                        TEnemy e = enemyEditor.TransplantEnemy(msb, data, target);
                        if (!infos[target].IsFixedSource && bossAsBasicNpc.TryGetValue(source, out int sourceNpc))
                        {
                            enemyEditor.SetNpcParam(e, npcParam: sourceNpc);
                        }
                        else if (infos[target].IsImportantTarget && !infos[source].IsBuffSource)
                        {
                            enemyEditor.SetNpcParam(e, npcParam: getBasicAsBoss(source, target));
                        }
                        if (!infos[target].HasPerceptiveNose && bossAsBasicThink.TryGetValue(source, out int sourceThink))
                        {
                            enemyEditor.SetNpcParam(e, npcThinkParam: sourceThink);
                        }
                        if (game.EldenRing)
                        {
                            game.WriteMSBs.Add(entry.Key);
                            // TODO: Make generators a property of the enemy so they can be reliably removed.
                            if (target == 31220800)
                            {
                                (msb as MSBE).Events.Generators.RemoveAll(g => g.EntityID == 31223308);
                            }
                        }

                        AddMulti(mapping, source, target);
                        revMapping[target] = source;
                        if (!ownerMap.TryGetValue(target, out string eventMap)) throw new Exception($"Internal error: no event map for {target}");
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
                                    ownerMap[group2] = eventMap;
                                }
                                else
                                {
                                    // Just use target directly in lieu of groups, if needed
                                    helperMapping[(target, group)] = target;
                                }
                            }
                        }
                        if (infos[source].BuddyGroup > 0 && infos[target].BuddyGroup > 0)
                        {
                            // Make sure helpers with the same BuddyGroup as the source automatically get the target BuddyGroup
                            // This is redundant for the main entity
                            groupMapping[infos[source].BuddyGroup] = infos[target].BuddyGroup;
                        }
                        Dictionary<int, int> mainGroupMapping = groupMapping;
                        if (infos[target].BuddyGroup > 0 && infos[target].NextPhase > 0)
                        {
                            // We really do need to make sure this happens, even for basic-to-boss without a source BuddyGroup
                            // However, only do this transformation for the main enemy.
                            // TODO: This is pretty bad, make a more coherent set of MapEditor group utility functions.
                            mainGroupMapping = groupMapping.ToDictionary(en => en.Key, en => en.Value);
                            mainGroupMapping[-1] = infos[target].BuddyGroup;
                        }
                        enemyEditor.CopyGroups(data, mainGroupMapping, e, infos[target].RemoveGroup);
                        HashSet<int> preserveGroups = null;
                        if (game.EldenRing && defaultData[target].Group != null)
                        {
                            // Extend group mapping to include Carian Study Hall groups, so helpers get added to it to
                            // This case is exceptional since it is used for a broad enable/disable
                            if (defaultData[target].Group.Contains(34115150))
                            {
                                preserveGroups = new HashSet<int> { 34115150 };
                            }
                            if (defaultData[target].Group.Contains(34115160))
                            {
                                preserveGroups = new HashSet<int> { 34115160 };
                            }
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
                        if (!dupeTarget && infos[target].TryGetArena("primary", out Arena primary))
                        {
                            e.Position = primary.Pos;
                            e.Rotation = primary.Rot;
                        }
                        if (dupeTarget && infos[source].TryGetArena("dupe", out Arena dupe))
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
                        if ((source == 5100201 || source == 35000800) && !dupeTarget)
                        {
                            // Turn Bridge Midir around
                            // Also Mohg 1
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
                                es.PatrolIndex = -1;
                            }
                            else if (e is MSB3.Part.Enemy e3)
                            {
                                e3.WalkRouteName = null;
                            }
                        }
                        if (infos[target].HasTag("ignoresp"))
                        {
                            // This is required to get Night's Cavalries working on Mountaintops
                            // TODO: Should this be carried with those enemies? Scripting seems to mention it
                            if (e is MSBE.Part.Enemy ee)
                            {
                                ee.SpEffectSetParamID[0] = 0;
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
                                copyRegions(
                                    spec, source, new List<int>(),
                                    new Dictionary<EventValue, EventValue> { { EventValue.Enemy(source), EventValue.Enemy(target) } },
                                    replace: true);
                            }
                        }
                        // Edit large NPCs if they appear in non-large places.
                        // This doesn't need to be especially organized. It's fine if we get more than we need.
                        // TODO: Applies to Elden Ring? It just has frames/allowframes for now
                        bool smallDest = infos[target].HasTag("exclude:large");
                        if (smallDest && infos[source].HasTag("large") && !opt["noshrink"])
                        {
                            shrinkNpc(Params["NpcParam"][data.NPC], 1f, 3f);
                        }
                        // Edit perceptive NPCs if they appear in sensitive places
                        // perceptive threshold is 20 and veryperceptive is 100, so don't shrink too much
                        if (false && infos[target].HasTag("exclude:perceptive") && infos[source].HasTag("perceptive"))
                        {
                            shrinkRange(Params["NpcThinkParam"][data.Think], 30);
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
                        else if (opt["testplacements"])
                        {
                            // TODO: Auto-add arenas centered around the boss, to make it easier to position them
                        }
                        models.UseModel(e.ModelName);

                        if (owners.TryGetValue(source, out List<int> helpers))
                        {
                            Arena arena = infos[target].ArenaData;
                            foreach (int helper in helpers)
                            {
                                // if (game.EldenRing) break;
                                if (!targetMultichr) continue;
                                if (infos[helper].Class != EnemyClass.Helper) continue;
                                int helperSource = helper;
                                (int, int) helperKey = (target, helper);
                                if (silo.SwapMapping.TryGetValue(helperKey, out int swapHelper))
                                {
                                    helperSource = swapHelper;
                                    swapMapping[helperKey] = swapHelper;
                                }
                                EnemyData data2 = defaultData[helperSource];
                                if (!helperMapping.TryGetValue(helperKey, out int target2))
                                {
                                    // In the case of DupeMap, this mapping must remain stable
                                    target2 = newEntity();
                                }
                                TEnemy e2 = enemyEditor.CloneEnemy(msb, e, data2, target2, helperModelBase++);
                                enemyEditor.ClearGroups(e2, preserveGroups);
                                if (!infos[target].IsFixedSource
                                    && bossAsBasicNpc.TryGetValue(helperSource, out int helperNpc))
                                {
                                    enemyEditor.SetNpcParam(e2, npcParam: helperNpc);
                                }
                                if (!infos[target].HasPerceptiveNose
                                    && bossAsBasicThink.TryGetValue(helperSource, out int helperThink))
                                {
                                    enemyEditor.SetNpcParam(e2, npcThinkParam: helperThink);
                                }
                                float extraHeight = 0;
                                if (infos[helper].HasTag("primary"))
                                {
                                    // TODO: For secondary/generator/etc., we may need to look those up manually
                                    // This is currently only used for Painting Guardians, who spawn in Halflight's spot
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
                                if (source == 12090800)
                                {
                                    double angle = new Random().NextDouble() * Math.PI * 2;
                                    // e2.Position += new Vector3((float)Math.Cos(angle) * 5, 0, (float)Math.Sin(angle) * 5);
                                }
                                if (dupeTarget)
                                {
                                    // If duplicating in-place, use that position if possible
                                    TEnemy sourceEnemy = enemyEditor.GetEnemy(msb, helperSource);
                                    if (sourceEnemy != null)
                                    {
                                        // Add to set to change the original position later
                                        AddMulti(dupedHelpers, target, sourceEnemy);
                                        e2.Position = sourceEnemy.Position + getDupeOffset(sourceEnemy.Rotation, target, infos[target].DupeIndex);
                                        e2.Rotation = sourceEnemy.Rotation;
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
                                if (infos[helper].HasTag("ballista"))
                                {
                                    Vector3 pos = e2.Position;
                                    e2.Position = moveInDirection(e2.Position, e2.Rotation, -1.5f);
                                }
                                if (smallDest && infos[helper].HasTag("large"))
                                {
                                    shrinkNpc(Params["NpcParam"][data2.NPC], 1f, 3f);
                                }
                                models.UseModel(e2.ModelName);
                                helperMapping[helperKey] = target2;
                                revMapping[target2] = helper;
                                ownerMap[target2] = eventMap;
                                enemyEditor.CopyGroups(data2, groupMapping, e2);
                                // The part names come from MainMap, so use the original MSB here
                                enemyEditor.CopyAssociations(maps[data.MainMap], data, data2, msb, e, e2);
                                if (infos[helper].Regions != null && uniqueTarget)
                                {
                                    // There is not really any "default" data for helpers, but this is needed just for the purpose of regions
                                    // TODO can we get rid of this hack
                                    defaultData[target2] = enemyEditor.GetEnemyData(e2, entry.Key);
                                    foreach (string spec in infos[helper].Regions)
                                    {
                                        copyRegions(
                                            spec, helper, new List<int>(),
                                            new Dictionary<EventValue, EventValue> { { EventValue.Enemy(helper), EventValue.Enemy(target2) } },
                                            replace: true);
                                    }
                                }
                            }
                        }
                        if (objectOwners.TryGetValue(source, out List<string> objHelpers))
                        {
                            // TODO: Add this to MapEditors. It's a pretty isolated routine either way.
                            if (game.Sekiro)
                            {
                                MSBS sourceMsb = game.SekiroMaps[defaultData[source].MainMap];
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
                                MSB3 sourceMsb = game.DS3Maps[defaultData[source].MainMap];
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
                            // Temporary Elden Ring hack
                            if (game.EldenRing) models.UseModel("c1000");
                            usedHelpers.UnionWith(helpers2);
                        }
                    }
                }
                foreach (KeyValuePair<int, List<TEnemy>> helperEntry in dupedHelpers)
                {
                    foreach (TEnemy helperEnemy in helperEntry.Value)
                    {
                        helperEnemy.Position += getDupeOffset(helperEnemy.Rotation, helperEntry.Key, -1);
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
                else if (msb is MSBE me)
                {
                    if (map == "m14_00_00_00")
                    {
                        // Mapping from collisions which are unnecessarily loaded (DrawGroups) to collisions which see too much (DispGroups)
                        Dictionary<string, List<string>> visibilityReductions = new Dictionary<string, List<string>>
                        {
                            // From [Graveyard start, Graveyard bridge, Big elevator mid-level]
                            // To [Pre-rooftop balcony, Rooftop start]
                            ["h002000"] = new List<string> { "h005500", "h006000" },
                            ["h002500"] = new List<string> { "h005500", "h006000" },
                            ["h003000"] = new List<string> { "h005500", "h006000" },
                        };
                        HashSet<string> dispCols = new HashSet<string>(visibilityReductions.Values.SelectMany(cs => cs));
                        Dictionary<string, MSBE.Part.Collision> dispGroups =
                            me.Parts.Collisions.Where(c => dispCols.Contains(c.Name)).ToDictionary(c => c.Name, c => c);
                        foreach (MSBE.Part.Collision c in me.Parts.Collisions)
                        {
                            if (!visibilityReductions.TryGetValue(c.Name, out List<string> removes)) continue;
                            foreach (string remove in removes)
                            {
                                MSBE.Part.Collision seer = dispGroups[remove];
                                for (int i = 0; i < 8; i++)
                                {
                                    // CollisionMask starts out as 8 dispgroups then 8 drawgroups
                                    // c.Unk1.CollisionMask[i + 8] &= ~seer.Unk1.CollisionMask[i];
                                    c.Unk1.DrawGroups[i] &= ~seer.Unk1.DisplayGroups[i];
                                    // if (start != end) Console.WriteLine($"Edited {c.Name} against {seer.Name}: {start:x8}->{end:x8}");
                                }
                            }
                        }
                    }
                }
                if (removedObjects.Count > 0)
                {
                    // Is this needed given second pass? Only keep in DS3 for now.
                    enemyEditor.GarbageCollect(msb, models.EnemyUsed, removedObjects);
                }
            }
            // Finally, remove all randomized helpers
            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                // if (game.EldenRing) break;
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                enemyEditor.RemoveEnemies(msb, id => usedHelpers.Contains(id) && infos[id].Class == EnemyClass.Helper);
                enemyEditor.GarbageCollect(msb);
                enemyEditor.GetEnemies(msb).Sort((a, b) => a.Name.CompareTo(b.Name));
            }

            // Update dupe map for helpers, as well
            // This is not heavily relied upon - only in loc (rewrite/copyinit) cases where logic applies to helpers.
            // It's not an error for helpers to be absent in those cases, in case something else got randomized there
            foreach (KeyValuePair<int, List<int>> entry in dupeEnemyMap.ToList())
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
                            else
                            {
                                // It's usually safe to add the entity itself
                                // In the worst case, this may result in duplicate wakeup events, seemingly
                                dupeHelpers.Add(dupe);
                            }
                        }
                        dupeEnemyMap[helper] = dupeHelpers;
                    }
                }
            }
            bool getEntityFromInit(Instr init, EnemyTemplate t, out int entity)
            {
                entity = t.Entity;
                if (entity > 0) return true;
                if (t.ArgEntities == null || t.Entities == null) return false;
                List<string> entities = t.Entities.Split(' ').ToList();
                foreach (string argSpec in t.ArgEntities.Split(' '))
                {
                    if (!events.ParseArgSpec(argSpec, out int argPos)) throw new Exception($"{init} {t.ArgEntities}");
                    if (init.Offset + argPos > init.Count) throw new Exception($"{init} missing argument {argSpec}");
                    object arg = init[init.Offset + argPos];
                    if (entities.Contains(arg.ToString()))
                    {
                        entity = (int)arg;
                        return true;
                    }
                }
                return false;
            }

            // It's emevd time
            Dictionary<EventKey, EventSpec> templates = eventConfig.EnemyEvents.ToDictionary(e => e.Key, e => e);
            if (!game.EldenRing && templates.Any(e => e.Key.ID == 0)) throw new Exception($"Internal error: event 0 in config");

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
                            OldParams pre = OldParams.Preprocess(e);
                            foreach (string remove in phraseRe.Split(start.StartCmd))
                            {
                                events.RemoveMacro(edits, remove);
                            }
                            for (int i = 0; i < e.Instructions.Count; i++)
                            {
                                Instr instr = events.Parse(e.Instructions[i], pre);
                                List<InstrEdit> results = events.GetMatches(edits, instr);
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
            HashSet<EventKey> segmentEvents =
                new HashSet<EventKey>(templates.Values.Where(
                    ev => ev.Template.Any(t => t.Type.StartsWith("segment"))).Select(ev => ev.Key));
            List<string> encounterSegmentTypes = new List<string>
            {
                // All of these must be defined together if they are defined
                // They are mapped 1:1 when both exist, otherwise use a custom mapping
                "presetup", "firstsetup", "firststart", "secondsetup", "secondstart",
            };
            List<string> minibossSegmentTypes = new List<string>
            {
                "quickstart", "healthbar", "unhealthbar", "healthbarcheck", "unhealthbarcheck",
            };

            HashSet<(EventKey, int, List<CommandSegment>)> segmentEntities = new HashSet<(EventKey, int, List<CommandSegment>)>();
            void addSegments(EventKey eventID, int entity, List<CommandSegment> segments, Instr init = null)
            {
                if (!segmentEntities.Add((eventID, entity, segments))) return;
                if (!segmentCmds.TryGetValue(entity, out Dictionary<string, CommandSegment> entitySegments))
                {
                    segmentCmds[entity] = entitySegments = new Dictionary<string, CommandSegment>();
                }
                if (segments.Count == 0) throw new Exception($"Internal error: No segments defined in segment template for {eventID} #{entity}");
                // TODO: perhaps verify dead/disable templates here. Otherwise, looking at the event isn't really required.
                foreach (CommandSegment original in segments)
                {
                    if (original.Commands == null)
                    {
                        throw new Exception($"Internal error: {eventID} #{entity} {original.Type} segment missing instructions");
                    }
                    // Post-process commands to remove // comments, as inline comments are quite useful
                    original.Commands = events.Decomment(original.Commands);
                    original.EncounterOnly = events.Decomment(original.EncounterOnly);
                    original.NonEncounterOnly = events.Decomment(original.NonEncounterOnly);
                    original.SpecificHelperOnly = events.Decomment(original.SpecificHelperOnly);
                    original.MoveOnly = events.Decomment(original.MoveOnly);
                    original.NonMoveOnly = events.Decomment(original.NonMoveOnly);
                    CommandSegment s = original.DeepCopy();
                    if (s.Type == "remove") continue;
                    if (entitySegments.ContainsKey(s.Type))
                    {
                        throw new Exception($"Internal error: {eventID} #{entity} defines duplicate segment {s.Type}");
                    }
                    s.NewCommands = s.Commands;
                    if (s.Params != null)
                    {
                        if (init == null) throw new Exception($"Internal error: no init provided for parameterized {eventID} #{entity} {s.Type}");
                        Dictionary<string, string> paramReplaces = new Dictionary<string, string>();
                        foreach (string param in s.Params.Split(' '))
                        {
                            if (!events.ParseArgSpec(param, out int pos))
                            {
                                throw new Exception($"Internal error: badly formatted params in {eventID}: {s.Params}");
                            }
                            int argPos = pos + init.Offset;
                            if (argPos >= init.Count)
                            {
                                throw new Exception($"Error: boss segment {init} missing argument in position {argPos}");
                            }
                            paramReplaces[$"X{pos * 4}_4"] = init[argPos].ToString();
                        }
                        s.NewCommands = s.Commands.Select(c =>
                        {
                            // It is possible to use ParseAddCommand here, though string replace should be good enough
                            foreach (KeyValuePair<string, string> replace in paramReplaces)
                            {
                                c = c.Replace(replace.Key, replace.Value);
                            }
                            return c;
                        }).ToList();
                    }
#if DEBUG
                    foreach (string c in s.NewCommands)
                    {
                        if (c.Contains("X") && !c.Contains("SFX")) throw new Exception($"Unreplaced {c} in {eventID} from [{s.Params}]");
                    }
#endif
                    entitySegments[s.Type] = s;
                }
            }
            int defaultSegmentEnemy = 9999999;
            if (eventConfig.DefaultSegments != null && eventConfig.DefaultSegments.Count > 0)
            {
                addSegments(null, defaultSegmentEnemy, eventConfig.DefaultSegments);
            }
            // For Elden Ring initially
            bool disableEvents = false;
            bool partialEventConfig = false;

            // Animation info: (entity id) -> (starting animation, wakeup animation)
            Dictionary<int, (int, int)> startingAnimations = new Dictionary<int, (int, int)>();
            Dictionary<int, List<(int, int)>> npcStartingAnimations = new Dictionary<int, List<(int, int)>>();
            HashSet<EventKey> animationEvents =
                new HashSet<EventKey>(templates.Values.Where(ev => ev.Template.Any(t => t.Animation != null)).Select(ev => ev.Key));
            // All BGM ids which are later shuffled around, keyed by defeat flag
            SortedDictionary<int, int> defeatFlagBgmIds = new SortedDictionary<int, int>();
            HashSet<EventKey> bossBgmEvents =
                new HashSet<EventKey>(templates.Values.Where(ev => ev.Template.Any(t => t.BossBgmArg != null)).Select(ev => ev.Key));

            // Preprocess emevds
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                if (disableEvents) break;
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    // All animation events are initialization-based from constructor
                    // Segment events are a mix of constructor 0, and 200 in a few cases in Elden Ring
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr init = events.Parse(e.Instructions[i]);
                        if (!init.Init) continue;
                        if (init.TryCalleeKey(segmentEvents, entry.Key, out EventKey callee))
                        {
                            foreach (EnemyTemplate t in templates[callee].Template)
                            {
                                if (!t.Type.StartsWith("segment")) continue;
                                int entity = t.Entity;
                                if (entity == 0 && t.Entities != null && t.ArgEntities != null)
                                {
                                    if (!getEntityFromInit(init, t, out entity))
                                    {
                                        // In this case, it's okay to skip nonexistent entities,
                                        // as real ones will be checked later
                                        continue;
                                    }
                                }
                                if (entity == 0) throw new Exception($"Internal error: Segment template missing entity: {init}");
                                addSegments(callee, entity, t.Segments, init);
                            }
                        }
                        if (init.TryCalleeKey(bossBgmEvents, entry.Key, out callee))
                        {
                            EnemyTemplate t = templates[callee].Template.Find(t => t.BossBgmArg != null);
                            string[] parts = t.BossBgmArg.Split(' ');
                            if (!events.ParseArgSpec(parts[0], out int defeatFlagPos) || !events.ParseArgSpec(parts[1], out int bgmIdPos))
                            {
                                throw new Exception($"Internal error: badly formatted BossBgmArg {t.BossBgmArg} for {init}");
                            }
                            int defeatFlag = (int)init[init.Offset + defeatFlagPos];
                            int bgmId = (int)init[init.Offset + bgmIdPos];
                            if (!defeatFlagIds.ContainsKey(defeatFlag)) continue;
                            // Note that if there are multiple, the second one will be used, generally.
                            // For instance, Caelid Godskin has 930000 (generic) and then 356000 (also used in duo)
                            // if (defeatFlagBgmIds.ContainsKey(defeatFlag)) Console.WriteLine($"Multiple entries for flag: {defeatFlag}->{bgmId} (exist: {defeatFlagBgmIds[defeatFlag]})");
                            defeatFlagBgmIds[defeatFlag] = bgmId;
                        }
                        if (init.TryCalleeKey(animationEvents, entry.Key, out callee))
                        {
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
                                    if (init.Offset + pos >= init.Count) throw new Exception($"{callee} anim {t.Animation} init {init} missing required arguments");
                                    return (int)init[init.Offset + pos];
                                }
                                string type = parts[0];
                                if (type == "active" || type == "passive")
                                {
                                    // Anim id. Just validate it's a good int/argument
                                    parsePart(1);
                                    continue;
                                }
                                else if (type == "suspend")
                                {
                                    // Entry id and animation id, also validate them
                                    parsePart(1);
                                    parsePart(2);
                                    continue;
                                }
                                else if (type == "gravity")
                                {
                                    // Other event id
                                    if (parts.Length >= 5) parsePart(4);
                                }
                                int entity = parsePart(1);
                                if (entity == 0)
                                {
                                    // All-0 initialization of 90005261 in m12_02_00_00
                                    if (parts[1].StartsWith("X")) continue;
                                    throw new Exception($"{callee} anim {t.Animation} init {init} missing entity");
                                }
                                (int, int) anims = (0, 0);
                                if (type == "wakeup")
                                {
                                    anims = (0, parsePart(2));
                                }
                                else
                                {
                                    anims = (parsePart(2), parsePart(3));
                                }
                                // These are fine to coexist, probably, unless it explicitly disables them
                                if (false && defaultData.TryGetValue(entity, out EnemyData data) && data.Anim > 0)
                                {
                                    // Console.WriteLine($"{entity} in event {callee}: MSB animation {data.Anim} vs evented ({anims.Item1}, {anims.Item2})");
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
                List<int> segmentlessBosses = infos.Values
                    .Where(info => info.IsImportantTarget && !segmentCmds.ContainsKey(info.ID) && !segmentCmds.ContainsKey(info.DupeFrom))
                    .Select(info => info.ID)
                    .ToList();
                if (!partialEventConfig && segmentlessBosses.Count > 0) throw new Exception($"Internal error: bosses [{string.Join(", ", segmentlessBosses)}] missing segments");
                foreach (KeyValuePair<int, Dictionary<string, CommandSegment>> entry in segmentCmds)
                {
                    List<string> types = entry.Value.Keys.ToList();
                    int encounterCount = types.Count(t => encounterSegmentTypes.Contains(t));
                    int minibossCount = types.Count(t => minibossSegmentTypes.Contains(t));
                    bool valid = true;
                    if (types.Count(t => t == "start" || t == "quickstart") != 1)
                    {
                        valid = false;
                    }
                    else if (types.Count(t => t == "dead" || t == "disable") != 1)
                    {
                        valid = false;
                    }
                    else if (types.Count(t => t == "setup" || t == "altsetup" || t == "presetup") != 1)
                    {
                        valid = false;
                    }
                    else if (types.Count(t => t.StartsWith("end")) != 1)
                    {
                        valid = false;
                    }
                    else if (encounterCount > 0 && encounterCount != encounterSegmentTypes.Count)
                    {
                        valid = false;
                    }
                    else if (minibossCount > 0 && minibossCount != minibossSegmentTypes.Count)
                    {
                        valid = false;
                    }
                    if (!valid)
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
            if (game.Sekiro && anyDupeEnabled)
            {
                foreach (PARAM.Row row in Params["LockCamParam"].Rows)
                {
                    // TODO: Limit to certain bosses. may also need y angles
                    row["CamDistTarget"].Value = (float)row["CamDistTarget"].Value * 1.25f;
                }
            }

            // Name changing/generation stuff
            string getName(FMGDictionary fmgs, int nameId)
            {
                if (game.EldenRing)
                {
                    return fmgs["NpcName"][nameId];
                }
                string name = fmgs["NPC名"][nameId];
                if (game.DS3)
                {
                    if (string.IsNullOrWhiteSpace(name)) name = fmgs["NPC名_dlc1"][nameId];
                    if (string.IsNullOrWhiteSpace(name)) name = fmgs["NPC名_dlc2"][nameId];
                }
                return name;
            }
            void setName(FMGDictionary fmgs, int nameId, string name)
            {
                if (game.EldenRing)
                {
                    game.WriteFMGs = true;
                    fmgs["NpcName"][nameId] = name;
                }
                else
                {
                    fmgs["NPC名"][nameId] = name;
                }
            }
            int baseNameId = game.Sekiro ? 902000 : (game.DS3 ? 907000 : 907770000);
            Dictionary<int, int> allocatedTargetNameIds = new Dictionary<int, int>();
            // TODO: Make a "healthbar boss" category
            foreach (EnemyInfo info in infos.Values
                .Where(i => i.ExtraName != null || i.FullName != null || i.PartName != null)
                .OrderBy(i => i.ID))
            {
                allocatedTargetNameIds[info.ID] = baseNameId;
                baseNameId += 10;
            }
            // Mapping from (target entity, base target name) = new target name
            Dictionary<(int, int), int> nameIds = new Dictionary<(int, int), int>();
            int GetCleverName(int id, int source, int target, bool inplaceTarget = false)
            {
                // TODO: Add this option to UI
                if (!opt["edittext"] && !opt["editnames"]) return id;
                if (source == target) return id;
                // Odd case which can come up in dedupe, where helper->main healthbar may occur.
                // TODO be careful about this in Sekiro (and DS3?) where OwnedBy is overloaded
                if (game.EldenRing && infos[source].OwnedBy == target) return id;
                // If they have the same full name, keep it as is to avoid duplication
                if (infos[source].FullName != null && infos[source].FullName == infos[target].FullName) return id;
                // If there is no configuration in the target, do not bother
                if (inplaceTarget && infos[target].FullName == null) return id;

                if (!nameIds.TryGetValue((target, id), out int nameId))
                {
                    // Use the part name, otherwise keep things simple and use the model name, for English name
                    // Previously tried to use ExtraName in Elden Ring, but this is still used for config purposes.
                    string sourceModelName = game.ModelCharacterName(defaultData[source].Model, defaultData[source].Char);
                    if (game.EldenRing)
                    {
                        sourceModelName = Regex.Replace(sourceModelName, @"[0-9 ]*$", "");
                        // This seemed to be a good initial heuristic if any PartName/FullName/NpcName are missing,
                        // but it needs a better applicable condition now, as this is wrong for e.g. boss
                        // segment starts with basic enemy sources.
                        if (false && inplaceTarget)
                        {
                            string existing = getName(game.ItemFMGs, id);
                            if (!string.IsNullOrWhiteSpace(existing))
                            {
                                sourceModelName = existing;
                            }
                        }
                    }
                    string sourcePart = infos[source].PartName;
                    string targetFull = infos[target].FullName;
                    // To-be-filled-in
                    if (sourcePart != null && sourcePart.Contains("^")) sourcePart = null;
                    if (targetFull != null && targetFull.Contains("^")) targetFull = null;
                    if (infos[target].CustomName != null && sourcePart != null && infos[target].CustomName.TryGetValue(sourcePart, out string customName))
                    {
                        targetFull = customName;
                    }
                    string fullName;
                    if (targetFull == null)
                    {
                        fullName = sourceModelName;
                    }
                    else
                    {
                        fullName = targetFull.Replace("$1", sourcePart ?? sourceModelName);
                    }
                    if (infos[target].DupeFrom == source)
                    {
                        fullName = infos[source].DupeName ?? infos[source].ExtraName ?? sourceModelName;
                    }
                    // In-place replacement would be nice, but there are duplicate usages across phases
                    // So select a new id
                    if (allocatedTargetNameIds.TryGetValue(target, out int allocId))
                    {
                        nameId = allocId;
                        if (allocId + 1 % 10 == 0)
                        {
                            allocatedTargetNameIds.Remove(target);
                        }
                        else
                        {
                            allocatedTargetNameIds[target] = allocId + 1;
                        }
                    }
                    else
                    {
                        nameId = baseNameId++;
                    }
                    nameIds[(target, id)] = nameId;
                    setName(game.ItemFMGs, nameId, fullName);
                    if (opt["debugnames"])
                    {
                        Console.WriteLine($"Replacement for {id} -> {nameId} - source {ename(source)} -> target {ename(target)}: {fullName}");
                    }

                    // For other languages, use the NPC name directly if it exists
                    if (id != nameId)
                    {
                        foreach (KeyValuePair<string, FMGDictionary> lang in game.AllItemFMGs)
                        {
                            if (lang.Key == "engus") continue;
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
                            if (opt["debugnames"])
                            {
                                Console.WriteLine($"  {lang.Key} replacement for {id} -> {nameId}: {backupName}");
                            }
                        }
                    }
                }
                return nameId;
            }

            // After all of the above metadata has been collected, preprocess for dupe handling
            // Mapping from original copyphase music flag to new one
            Dictionary<int, int> copyphaseNewMusicFlags = new Dictionary<int, int>();
            if (anyDupeEnabled)
            {
                // Dictionary from event key to (copy event or not), for preprocessing entire event 
                Dictionary<EventKey, bool> preDupeEvents = new Dictionary<EventKey, bool>();

                // Hacky dictionary just by id. Used for DupeEvents.
                Dictionary<int, EventSpec> idTemplates = new Dictionary<int, EventSpec>();
                // Don't support this for Elden Ring, mainly because it requires making EventKeys with null maps.
                // Need to either change DupeEvents format, infer a unique event id, or infer from ownerMap
                bool mapRequired = !(game.Sekiro || game.DS3);
                if (!mapRequired)
                {
                    foreach (KeyValuePair<EventKey, EventSpec> e in templates)
                    {
                        idTemplates[e.Key.ID] = e.Value;
                    }
                }

                // Manufacture event specs for enemy DupeEvents
                foreach (EnemyInfo info in infos.Values)
                {
                    if (mapRequired) break;
                    if (info.DupeEvents == null) continue;
                    foreach (int ev in info.DupeEvents)
                    {
                        if (idTemplates.ContainsKey(ev)) continue;
                        idTemplates[ev] = templates[new EventKey(ev, null)] = new EventSpec
                        {
                            ID = ev,
                            Dupe = game.Sekiro ? info.ID.ToString() : "copy",
                            Entities = info.ID.ToString(),
                            Template = new List<EnemyTemplate> { new EnemyTemplate { Type = "default" } },
                        };
                    }
                }
                // Manufacture templates for event spec Dupes
                // Handle segments later. The main reason for not rewriting the templates/events now is that segment detection
                // is already pretty fragile, so processing a segment should mean "do it for all instances of the fight at once"
                foreach (EventSpec ev in templates.Values)
                {
                    // Preprocessing of Dupe templates to make sure default types are present
                    if (ev.Template != null)
                    {
                        foreach (EnemyTemplate td in ev.Template)
                        {
                            if (td.Dupe == null || td.Dupe.Type != null) continue;
                            td.Dupe.Type = "none";
                            if (game.Sekiro && (td.Type == "loc" || td.Type == "common") && !td.Dupe.NoRewrite)
                            {
                                // Default is rewrite in Sekiro, for loc/common events
                                td.Dupe.Type = "rewrite";
                            }
                        }
                    }
                    if (ev.Dupe == null || ev.Dupe == "none" || ev.Dupe == "xx" || ev.Dupe == "manual") continue;
                    if (ev.Dupe == "copy" || ev.Dupe == "copyphase" || ev.Dupe == "copyinit")
                    {
                        if (string.IsNullOrEmpty(ev.Entities)) throw new Exception($"Internal error: {ev.Key} with dupe behavior {ev.Dupe} has no entities defined");
                        // This makes a copy of the event, its initializations, and its non-chr templates.
                        // common_func is tricky, but relies on arg-only (copyinit) and detection in e.g. animation handling
                        bool copyEvent = ev.Dupe != "copyinit";
                        if (ev.Map == "common_func")
                        {
                            if (copyEvent) throw new Exception($"Internal error: {ev.Key} has dupe type {ev.Dupe}");
                            // The main modification here is to prevent edits if nothing is randomized
                            foreach (EnemyTemplate ct in ev.Template)
                            {
                                // fatcat
                                if (ct.Entity > 0) throw new Exception($"Internal error: Entity field not implemented for copyinit");
                                // If not specific entity list applied, continue to apply it to everything
                                if (ct.Entities == null) continue;
                                // Edits can occur if any of orginal entities *or* dupes are randomized
                                List<int> entities = ct.Entities.Split(' ').Select(int.Parse).ToList();
                                entities = entities
                                    .SelectMany(id => new[] { id }.Concat(dupeEnemyMap.TryGetValue(id, out List<int> ds) ? ds : new List<int>()))
                                    .ToList();
                                ct.Entities = string.Join(" ", entities);
                            }
                        }
                        if (ev.Dupe == "copyphase")
                        {
                            // Avoid starting fights too soon by splitting up phase 2 start flags
                            // This caused issues with e.g. Fire Giant, where an enable event is dependent on start flag
                            foreach (EnemyTemplate ct in ev.Template)
                            {
                                if (ct.Type == "segment" && ct.StartFlag > 0)
                                {
                                    int startFlag = infos[ct.Entity].StartFlag;
                                    if (startFlag > 0 && dupeEnemyMap.TryGetValue(ct.Entity, out List<int> segDupes))
                                    {
                                        foreach (int segDupe in segDupes)
                                        {
                                            infos[segDupe].StartFlag = NewID(true);
                                        }
                                    }
                                }
                                if (int.TryParse(ct.MusicFlag, out int flag))
                                {
                                    // If there is also a music flag here, we need to make sure to change it
                                    // and also explicitly add an off/on setting everywhere it's used.
                                    copyphaseNewMusicFlags[flag] = NewID(true);
                                }
                            }
                        }
                        preDupeEvents[ev.Key] = copyEvent;
                        continue;
                    }
                    EnemyTemplate t = new EnemyTemplate
                    {
                        Dupe = new Dupe
                        {
                            Type = "none",
                        },
                    };
                    if (ev.Dupe == "rewrite")
                    {
                        t.Type = "loc";
                        t.Dupe.Type = "rewrite";
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
                // Stable copy ids for a given event id
                Dictionary<EventKey, List<int>> eventCopies = new Dictionary<EventKey, List<int>>();
                List<int> getEventCopies(EventKey original)
                {
                    if (!eventCopies.TryGetValue(original, out List<int> copies))
                    {
                        eventCopies[original] = copies = Enumerable.Range(0, maxDupeCount).Select(_ => NewID()).ToList();
                    }
                    return copies;
                }
                // This might be good to add to EventSpec directly and cache there
                // All of this string parsing everywhere is suboptimal, but preprocessing is also pretty messy
                (List<int>, List<int>) parseEntitiesAndArgs(string entStr)
                {
                    List<int> entityIds = new List<int>();
                    List<int> entityArgs = new List<int>();
                    if (entStr != null)
                    {
                        foreach (string ent in entStr.Split(' '))
                        {
                            if (int.TryParse(ent, out int id))
                            {
                                entityIds.Add(id);
                            }
                            else if (events.ParseArgSpec(ent, out int pos))
                            {
                                entityArgs.Add(pos);
                            }
                        }
                    }
                    return (entityIds, entityArgs);
                }
                foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
                {
                    List<EMEVD.Event> newDupeEvents = new List<EMEVD.Event>();
                    foreach (EMEVD.Event e in entry.Value.Events)
                    {
                        // Copy event itself if it's directly in the config
                        // Don't touch initializations in this case
                        EventKey originalKey = new EventKey((int)e.ID, entry.Key);
                        if (preDupeEvents.TryGetValue(originalKey, out bool copyEvent) && copyEvent)
                        {
                            if (entry.Key == "common_func") throw new Exception($"Internal error: can't duplicate event {originalKey}");
                            EventSpec ev = templates[originalKey];
                            List<int> copies = getEventCopies(originalKey);
                            (List<int> entityIds, List<int> entityArgs) = parseEntitiesAndArgs(ev.Entities);
                            for (int c = 0; c < copies.Count; c++)
                            {
                                int copyId = copies[c];
                                EventKey copyKey = new EventKey(copyId, entry.Key);
                                newDupeEvents.Add(events.CopyEvent(e, copyId));
                                EventSpec evCopy = ev.DeepCopy();
                                templates[copyKey] = evCopy;
                                evCopy.ID = copyId;
                                evCopy.DupeIndex = c;
                                evCopy.Template.RemoveAll(t => t.Type != null && t.Type.Contains("chr"));
                                // Replace in the event itself, can just always just do this (copyphase handled by segments, though)
                                // TODO: See if dupe helpful for copyphase. segment logic is just custom anyway
                                if (evCopy.Dupe == "copyphase") continue;
                                evCopy.Template.Add(new EnemyTemplate
                                {
                                    Type = "loc",
                                    Dupe = new Dupe
                                    {
                                        Type = "replace",
                                    },
                                });
                                if (entityArgs.Count > 0)
                                {
                                    // Also if there are args, replace them individually.
                                    // This is a separate template so it can apply repeatedly.
                                    evCopy.Template.Add(new EnemyTemplate
                                    {
                                        Type = "locarg",
                                        ArgEntities = string.Join(" ", entityArgs.Select(a => $"X{a * 4}")),
                                        Dupe = new Dupe
                                        {
                                            Type = "replace",
                                        },
                                    });
                                }
                            }
                            continue;
                        }
                        // Copy initializations
                        OldParams initOld = OldParams.Preprocess(e);
                        List<EMEVD.Instruction> newDupeInits = new List<EMEVD.Instruction>();
                        for (int i = 0; i < e.Instructions.Count; i++)
                        {
                            Instr originalInit = events.Parse(e.Instructions[i], initOld);
                            if (!originalInit.Init) continue;
                            if (!originalInit.TryCalleeValue(templates, entry.Key, out EventKey callee, out EventSpec ev)) continue;
                            if (!preDupeEvents.TryGetValue(callee, out copyEvent)) continue;
                            (List<int> entityIds, List<int> entityArgs) = parseEntitiesAndArgs(ev.Entities);
                            // Assume indicates are valid for the moment, and events.Parse uses ints
                            List<int> argEntities = entityArgs.Select(k => (int)originalInit[originalInit.Offset + k]).ToList();
                            int maxCopies = combinedDupeCount(entityIds.Concat(argEntities));
                            if (copyEvent)
                            {
                                List<int> copies = getEventCopies(callee);
                                for (int c = 0; c < maxCopies; c++)
                                {
                                    // Copy the init itself, only changing event id
                                    int copyId = copies[c];
                                    Instr copyInit = events.CopyInit(originalInit, copyId, initOld);
                                    copyInit.Save();
                                    newDupeInits.Add(copyInit.Val);
                                }
                            }
                            else
                            {
                                for (int c = 0; c < maxCopies; c++)
                                {
                                    // Copy the init itself, rewriting with dupe entities
                                    Instr copyInit = events.CopyInit(originalInit, originalInit.Callee, initOld);
                                    foreach (int k in entityArgs)
                                    {
                                        // Assume indices are valid for the moment
                                        object val = (int)copyInit[copyInit.Offset + k];
                                        if (val is int ival && dupeEnemyMap.TryGetValue(ival, out List<int> dupeVals))
                                        {
                                            copyInit[copyInit.Offset + k] = dupeVals[c];
                                        }
                                    }
                                    copyInit.Save();
                                    newDupeInits.Add(copyInit.Val);
                                }
                            }
                        }
                        if (newDupeInits.Count > 0)
                        {
                            game.WriteEmevds.Add(entry.Key);
                            e.Instructions.AddRange(newDupeInits);
                        }
                    }
                    if (newDupeEvents.Count > 0)
                    {
                        game.WriteEmevds.Add(entry.Key);
                        entry.Value.Events.AddRange(newDupeEvents);
                    }
                }
            }

            Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>> newInitializations = new Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>>();
            // Don't redo non-chr event templates. For now, just use references.
            HashSet<EnemyTemplate> completedTemplates = new HashSet<EnemyTemplate>();
            HashSet<EventKey> removedEvents = new HashSet<EventKey>();
            HashSet<EventKey> usedEvents = new HashSet<EventKey>();
            Dictionary<EventKey, EMEVD.Event> commonEvents = game.Emevds["common_func"].Events
                .ToDictionary(e => new EventKey((int)e.ID, "common_func"), e => e);
            HashSet<int> removeImmortality = new HashSet<int>(infos.Values
                .Where(info => info.IsMortalSekiroBoss)
                .SelectMany(info => mapping.TryGetValue(info.ID, out List<int> targets) ? targets : new List<int> { }));

            // Entity id utilities
            void fillEntityIdMapping(Dictionary<EventValue, EventValue> reloc, int entity, int target, bool includeHelpers)
            {
                reloc[EventValue.Enemy(entity)] = EventValue.Enemy(target);
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
                            reloc[EventValue.Enemy(helper)] = infos.ContainsKey(helper)
                                ? EventValue.Enemy(helperTarget) : EventValue.Asset(helperTarget);
                        }
                        else if (!game.EldenRing)
                        {
                            // This is needed when entity == target, so that e.g. copyRegion can work
                            // Try to avoid calling it in that case, however
                            // reloc[EventValue.Unknown(helper)] = EventValue.Unknown(helper);
                        }
                    }
                }
                foreach (EventValue derived in reloc.Keys.ToList())
                {
                    // Handle normal SplitFrom rewrites (not meant to handle dupes)
                    if (derived.ID is int id
                        && infos.TryGetValue(id, out EnemyInfo baseInfo)
                        && baseInfo.SplitFrom > 0)
                    {
                        reloc[EventValue.Enemy(baseInfo.SplitFrom)] = reloc[derived];
                    }
                }
            }
            List<int> getHelperTargets(int target)
            {
                List<int> helperTargets = new List<int>();
                if (revMapping.TryGetValue(target, out int source))
                {
                    if (owners.TryGetValue(source, out List<int> helpers))
                    {
                        foreach (int helper in helpers)
                        {
                            if (helperMapping.TryGetValue((target, helper), out int helperTarget))
                            {
                                helperTargets.Add(helperTarget);
                            }
                        }
                    }
                }
                else
                {
                    // Not randomized
                    if (owners.TryGetValue(target, out List<int> helpers))
                    {
                        helperTargets.AddRange(helpers);
                    }
                }
                return helperTargets;
            }
            List<int> getIntArgs<T>(IEnumerable<T> args)
            {
                return args.SelectMany(o => o is int oi ? new[] { oi } : new int[] { }).ToList();
            }
            (int, List<int>) getDupeEntityArgPositions(string spec)
            {
                string[] parts = spec.Split(' ');
                if (!events.ParseArgSpec(parts.Last(), out int targetPos)) throw new Exception(spec);
                List<int> sourcePoses = new List<int>();
                for (int p = 0; p < parts.Length - 1; p++)
                {
                    if (!events.ParseArgSpec(parts[p], out int sourcePos)) throw new Exception(spec);
                    sourcePoses.Add(sourcePos);
                }
                return (targetPos, sourcePoses);
            }
            void transplantRegionSpecs(
                Dictionary<EventValue, EventValue> reloc, Dictionary<int, int> distReplace, List<string> specs,
                int entity, int target, List<int> args)
            {
                foreach (string spec in specs)
                {
                    foreach (KeyValuePair<int, RegionTarget> region in copyRegions(spec, entity, args, reloc))
                    {
                        RegionTarget r = region.Value;
                        if (r.Region != 0)
                        {
                            reloc[EventValue.Region(region.Key)] = EventValue.Region(r.Region);
                        }
                        if (r.Generator != 0)
                        {
                            reloc[EventValue.Generator(region.Key)] = EventValue.Generator(r.Generator);
                        }
                        else if (r.Distance != 0)
                        {
                            distReplace[region.Key] = r.Distance;
                        }
                    }
                }
            }

            // These have duplicate data from dupeEnemyMap, but are used for all event purposes
            List<Dictionary<EventValue, EventValue>> dupeRelocs = new List<Dictionary<EventValue, EventValue>>();
            for (int i = 0; i < maxDupeCount; i++)
            {
                Dictionary<EventValue, EventValue> reloc = new Dictionary<EventValue, EventValue>();
                foreach (KeyValuePair<int, List<int>> entry in dupeEnemyMap)
                {
                    if (i < entry.Value.Count)
                    {
                        reloc[EventValue.Enemy(entry.Key)] = EventValue.Enemy(entry.Value[i]);
                    }
                }
                dupeRelocs.Add(reloc);
            }
            // Various cached state
            // Map from progress flags to second dupe flags
            Dictionary<int, List<int>> progressFlagCopies = new Dictionary<int, List<int>>();
            // Owner entities which have music flags set by events
            HashSet<int> usedMusicFlagTargets = new HashSet<int>();
            Dictionary<string, int> flagToSpeffectControl = new Dictionary<string, int>
            {
                ["SkipIfEventFlag"] = 0,
                ["EndIfEventFlag"] = 2,
                ["GotoIfEventFlag"] = 1,
            };
            List<int> allocateProgressFlags(string flagList)
            {
                // Need loops nested like this so that contiguous flags remain contiguous
                // Either way, it depends on the actual event configuration list being contiguous
                // List of (base flag, count)
                List<(int, int)> flags = flagList.Split(' ').Select(f =>
                {
                    string[] parts = f.Split('-');
                    int start = int.Parse(parts[0]);
                    if (parts.Length == 1) return (start, 1);
                    int end = int.Parse(parts[1]);
                    int count = end - start + 1;
                    if (count < 1 || count > 50) throw new Exception($"Internal error: illegal flag range in {flagList}");
                    return (start, count);
                }).ToList();
                List<int> addedFlags = new List<int>();
                // Can limit this to target dupe count, but global is simpler
                for (int d = 0; d < maxDupeCount; d++)
                {
                    foreach ((int start, int count) in flags)
                    {
                        AllocateWriteableIDs(count);
                        for (int i = 0; i < count; i++)
                        {
                            int flag = start + i;
                            addedFlags.Add(flag);
                            if (progressFlagCopies.TryGetValue(flag, out List<int> newFlags) && newFlags.Count > d)
                            {
                                // Simpler condition: newFlags[d] is valid
                            }
                            else
                            {
                                AddMulti(progressFlagCopies, flag, NewID(writeable: true));
                            }
                        }
                    }
                }
                return addedFlags;
            }
            Dictionary<int, int> assignedDefeatFlagBgmIds = new Dictionary<int, int>();
            if (opt["bossbgm"])
            {
                Random bossBgmRandom = new Random(seed + 420);
                // A few weird cases use an invalid id but still work in-game (900000 in side tomb -> soldier of godrick)
                HashSet<int> paramIds = new HashSet<int>(Params["WwiseValueToStrParam_BgmBossChrIdConv"].Rows.Select(r => r.ID));
                if (opt["bossbgmexplain"])
                {
                    foreach ((int flag, int bgm) in defeatFlagBgmIds)
                    {
                        if (!paramIds.Contains(bgm)) Console.WriteLine($"Bad {flag} -> {bgm}");
                    }
                }
                // Lots of dict accesses but these should exist. Also, the order is deterministic (sorted) here.
                List<int> majorBgms = defeatFlagBgmIds
                    .Where(e => infos[defeatFlagIds[e.Key]].Class == EnemyClass.Boss && paramIds.Contains(e.Value))
                    .Select(e => e.Value)
                    .Distinct()
                    .ToList();
                List<int> allBgms = defeatFlagBgmIds.Where(e => paramIds.Contains(e.Value)).Select(e => e.Value).Distinct().ToList();
                Shuffle(bossBgmRandom, majorBgms);
                List<int> bgmDefeatIds = defeatFlagBgmIds.Select(e => e.Key).ToList();
                Shuffle(bossBgmRandom, bgmDefeatIds);
                foreach (int defeatFlag in bgmDefeatIds)
                {
                    int originalBgm = defeatFlagBgmIds[defeatFlag];
                    int newBgm;
                    if (majorBgms.Count > 0 && infos[defeatFlagIds[defeatFlag]].Class == EnemyClass.Boss)
                    {
                        newBgm = majorBgms[majorBgms.Count - 1];
                        // Only sorta try to guarantee uniqueness for these important placements, otherwise don't care
                        if (originalBgm == newBgm && majorBgms.Count > 1)
                        {
                            newBgm = majorBgms[majorBgms.Count - 2];
                            majorBgms.RemoveAt(majorBgms.Count - 2);
                        }
                        else
                        {
                            majorBgms.RemoveAt(majorBgms.Count - 1);
                        }
                    }
                    else
                    {
                        newBgm = Choice(bossBgmRandom, allBgms);
                    }
                    assignedDefeatFlagBgmIds[defeatFlag] = newBgm;
                }
            }

            // Utilites to avoid using mapping/revMapping directly
            bool isRandomizedTarget(int target)
            {
                // If not present in revMapping, source == target
                // Otherwise, if source comes from target, this counts as not being randomized as well - this means that some templates
                // won't run, but rewriting should still happen as part of DupeIndex.
                return revMapping.TryGetValue(target, out int source)
                    && !(anyDupeEnabled && infos.TryGetValue(target, out EnemyInfo targetInfo) && targetInfo.DupeFrom == source);
            }
            bool isRandomizedSource(int source)
            {
                // Likewise, if source ever moves outside of its own spot
                return mapping.TryGetValue(source, out List<int> targets)
                    && !(anyDupeEnabled && targets.All(target => infos.TryGetValue(target, out EnemyInfo targetInfo) && targetInfo.DupeFrom == source));
            }
            // Can pare this interface down, but currently returns true if randomized for a real (non-dupe) target.
            // and outputs the source (present in infos map) and real target (may not be - also can be ignored if using replace/rewrite logic)
            bool getRandomizedSource(int baseTarget, int dupeIndex, out int source, out int realTarget)
            {
                source = baseTarget;
                realTarget = baseTarget;
                if (dupeIndex >= 0)
                {
                    if (!dupeEnemyMap.TryGetValue(baseTarget, out List<int> dupes)) return false;
                    // This used to be an error, and kind of is, but it's on the caller to check dupeCount themselves
                    // It matches how we use the main entity for params if not all can be filled in.
                    if (dupeIndex >= dupes.Count) return false;
                    realTarget = dupes[dupeIndex];
                }
                if (!revMapping.TryGetValue(realTarget, out int realSource)) return false;
                source = realSource;
                return true;
            }
            // Undoes dupe stuff, for non-helpers only!
            int getBaseTarget(int realTarget, out int dupeIndex)
            {
                dupeIndex = -1;
                // We're using DupeFrom on a target here, meaning the source does not matter
                if (!infos.TryGetValue(realTarget, out EnemyInfo info) || info.DupeFrom <= 0) return realTarget;
                if (!dupeEnemyMap.TryGetValue(info.DupeFrom, out List<int> dupes)) throw new Exception($"Internal error: {info.DupeFrom} ({realTarget} base) has no dupes");
                dupeIndex = dupes.IndexOf(realTarget);
                if (realTarget == -1) throw new Exception($"Internal error: {realTarget}'s DupeFrom {info.DupeFrom} missing it: [{string.Join(",", dupes)}]");
                return info.DupeFrom;
            }
            int maxHealthbars = 3;
            int getHealthbarIndex(int source, int target, int dupeIndex = -1)
            {
                EnemyInfo info = infos[source];
                int index = info.HealthbarIndex;
                // If no dupes exist for target, use normal index
                if (!dupeEnemyMap.TryGetValue(target, out List<int> dupes)) return index;
                // Otherwise, look at all dupes, and see if they can all fit
                // Could cache this but it's not super expensive
                int targetIndex = -1;
                int totalCount = 0;
                // If there are doubles, dupeCount is 1, so this goes -1 0.
                for (int i = -1; i < dupeCount(target); i++)
                {
                    int dupeTarget = i == -1 ? target : dupes[i];
                    int dupeSource = revMapping.TryGetValue(dupeTarget, out int id) ? id : dupeTarget;
                    if (i == dupeIndex)
                    {
                        // Record exact sub-index if we're being asked about it
                        targetIndex = totalCount + index;
                    }
                    int targetTotal = totalHealthbars.TryGetValue(dupeSource, out int amt) ? amt : 1;
                    totalCount += targetTotal;
                }
                if (totalCount > maxHealthbars)
                {
                    // If can't show all targets, only show primary target, assuming it'll fit
                    int shiftedIndex = dupeIndex + 1;
                    return index == 0 && shiftedIndex < maxHealthbars ? shiftedIndex : -1;
                }
                // Use full offsets!
                return targetIndex;
            }

            // Main event loop
            List<string> eventEditErrors = new List<string>();
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                if (disableEvents) break;
                if (!newInitializations.ContainsKey(entry.Key))
                {
                    newInitializations[entry.Key] = new List<(EMEVD.Instruction, EMEVD.Event)>();
                }

                // Note this putting all events in a dictionary precludes duplicates, which technically the game allows, so may conflict with other mods
                Dictionary<EventKey, EMEVD.Event> fileEvents = entry.Value.Events
                    .ToDictionary(e => new EventKey((int)e.ID, entry.Key), e => e);
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    OldParams initOld = OldParams.Preprocess(e);
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr originalInit = events.Parse(e.Instructions[i], initOld);
                        if (!originalInit.Init) continue;
                        if (!originalInit.TryCalleeValue(templates, entry.Key, out EventKey callee, out EventSpec ev)) continue;
                        ev.Template = ev.Template.Where(t => !t.Type.Contains("xx")).ToList();
                        if (ev.Template.Count == 0) continue;
                        game.WriteEmevds.Add(entry.Key);
                        game.WriteEmevds.Add(callee.Map);
                        if (ev.Template[0].Type.StartsWith("remove"))
                        {
                            bool simpleRemove = false;
                            foreach (EnemyTemplate t in ev.Template)
                            {
                                if (!t.Type.StartsWith("remove")) continue;
                                // If the target is specified and comes from somewhere else, remove the event
                                if (t.Type == "removearg")
                                {
                                    if (game.EldenRing)
                                    {
                                        // ArgEntities aware
                                        if (getEntityFromInit(originalInit, t, out int removeEntity))
                                        {
                                            simpleRemove |= isRandomizedTarget(removeEntity);
                                        }
                                    }
                                    else
                                    {
                                        int removeEntity = getIntArgs(originalInit.Args.Skip(originalInit.Offset))
                                            .Find(a => infos.ContainsKey(a));
                                        simpleRemove |= isRandomizedTarget(removeEntity);
                                    }
                                }
                                else if (t.Entity > 0)
                                {
                                    simpleRemove |= isRandomizedTarget(t.Entity);
                                }
                                else if (t.Entities != null)
                                {
                                    // This was changed from All to Any, TODO check this is fine in DS3?
                                    simpleRemove |= t.Entities.Split(' ').Any(es => isRandomizedTarget(int.Parse(es)));
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
                        // Source/target entity and event copy, for copies. At the moment there is only one chr per event initialization, but perhaps group them together in the future.
                        List<(int, int, EMEVD.Event, EnemyTemplate)> eventCopies = new List<(int, int, EMEVD.Event, EnemyTemplate)>();
                        bool canRemove = true;

                        // Set up all chr edits first, before the event itself is edited
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (!t.Type.Contains("chr")) continue;
                            int entity = t.Entity;
                            // Include dummy enemies for now, otherwise it will detect no entity
                            int argEntity = 0;
                            if (entity <= 0)
                            {
                                if (!t.Type.Contains("arg"))
                                {
                                    throw new Exception($"Internal error for {t.Type} {callee}: no entity defined");
                                }
                                List<int> intArgs = getIntArgs(originalInit.Args.Skip(originalInit.Offset));
                                if (t.Entities != null)
                                {
                                    List<int> selectArgs = intArgs.Intersect(t.Entities.Split(' ').Select(int.Parse)).ToList();
                                    if (selectArgs.Count == 0)
                                    {
                                        continue;
                                    }
                                    argEntity = selectArgs[0];
                                }
                                else
                                {
                                    argEntity = intArgs.Find(a => infos.ContainsKey(a) || ignoreEnemies.Contains(a));
                                }
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
                            if (!isRandomizedTarget(entity))
                            {
                                canRemove = false;
                                // ...unless it's a deathblow, in which case it must be rewritten
                                // This was a fix for Oops All Sword Saint Isshin
                                // Only works when this template is the only template for this event! Otherwise canRemove may be reset to false.
                                if (t.Deathblow != 0)
                                {
                                    targets.Add(entity);
                                    canRemove = true;
                                }
                            }
                            // if (callee.ID == 15002811) Console.WriteLine($"Template {callee} {t.Type} mapping {entity} -> {string.Join(",", targets)}");
                            // # of events should not be a problem, since there is a global multichr limit for some enemies, but we'll see
                            if (t.Type.StartsWith("multichronly"))
                            {
                                targets.RemoveAll(target => !enableMultichr(entity, target));
                            }
                            if (partsRestricted && t.Type.StartsWith("chrpart"))
                            {
                                targets.RemoveAll(target => !infos[target].IsImportantTarget);
                            }
                            if (t.Type.Contains("nonboss"))
                            {
                                targets.RemoveAll(target => infos[target].IsBossTarget);
                            }
                            else if (t.Type.Contains("boss"))
                            {
                                targets.RemoveAll(target => !infos[target].IsBossTarget);
                            }
                            if (t.Type.Contains("basic"))
                            {
                                targets.RemoveAll(target => infos[target].IsImportantTarget);
                            }
                            else if (t.Type.Contains("important"))
                            {
                                targets.RemoveAll(target => !infos[target].IsImportantTarget);
                            }
                            if (t.Type.Contains("move"))
                            {
                                targets.RemoveAll(target => anyDupeEnabled && infos[target].DupeFrom == entity);
                            }
                            if (t.Dupe != null)
                            {
                                // Unlike loc events, we specifically require this to be an in-place dupe to apply
                                targets.RemoveAll(target => !(anyDupeEnabled && infos[target].DupeFrom == entity));
                            }

                            // If no targets left at this point, nothing to do
                            if (targets.Count == 0) continue;
                            // Console.WriteLine($"Copying {entity} -> {string.Join(", ", targets)}");

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
                        string lastDeadType = null;
                        int lastDeadEntity = 0;
                        bool segmentCopied = false;
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (t.Type.Contains("chr")) continue;

                            if (t.Dupe != null && !anyDupeEnabled)
                            {
                                // If a dupe event and dupe is disabled, ignore it.
                                // Do this before counting the template against canRemove.
                                continue;
                            }

                            bool shouldRewriteEntity(int id)
                            {
                                // if (id == 1034500800) Console.WriteLine($"{entry.Key} -> {callee} {t.Type} determination: {isRandomizedTarget(id)} {dupeEnabled(id)}");
                                return isRandomizedTarget(id) || (t.Type.StartsWith("segment") && dupeEnabled(id));
                            }
                            if (t.Entity > 0)
                            {
                                // Something must be randomized to this target if entity is specified.
                                // Alternatively, always handle segments when bosses are duplicated, or "stay" keyword is used.
                                // Also the inverse.
                                bool isRandom = shouldRewriteEntity(t.Entity);
                                bool requireNonRandom = t.Type.Contains("stay");
                                if (isRandom == requireNonRandom)
                                {
                                    if (!isRandom)
                                    {
                                        // In the normal case of non-random entity, the event stays.
                                        // randomized locstay templates are ignored for removal determination.
                                        canRemove = false;
                                    }
                                    continue;
                                }
                                // Source-target pair. Not really needed for anything essential
                                if (t.Transfer > 0 && revMapping.TryGetValue(t.Entity, out int transferSource) && t.Transfer != transferSource)
                                {
                                    canRemove = false;
                                    continue;
                                }
                                if (t.Type.StartsWith("segment"))
                                {
                                    // This is managing a kind of ugly cross-template interaction, so it's an order-dependent edit either way.
                                    // This is relevant in cases where the same event sets both phases of a multi-boss fight.
                                    if (t.Segments.Any(s => s.Type == "dead"))
                                    {
                                        lastDeadEntity = t.Entity;
                                        if (lastDeadType != null && lastDeadType != t.Type)
                                        {
                                            throw new Exception($"Internal error: unsupported mix of boss disable segments in {callee}");
                                        }
                                        lastDeadType = t.Type;
                                    }
                                }
                            }
                            if (t.Entities != null)
                            {
                                // Any must be randomized if several entities are specified
                                if (!t.Entities.Split(' ').Any(es => shouldRewriteEntity(int.Parse(es))))
                                {
                                    canRemove = false;
                                    continue;
                                }
                            }
                            if (t.Type == "default" || (!game.EldenRing && t.Type == "loc" && t.IsDefault()))
                            {
                                // This is fine, nothing to do if not chr
                                // TODO: Make IsDefault a debug check only
                            }
                            else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                            {
                                if (completedTemplates.Contains(t))
                                {
                                    canRemove = false;
                                    continue;
                                }
                                if (t.Type.Contains("arg"))
                                {
                                    eventCopies.Add((0, 0, null, t));
                                }
                                else if (t.Type == "copy")
                                {
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, 0, events.CopyEvent(theEvent, NewID()), t));
                                }
                                else if (t.Type == "segmentcopy")
                                {
                                    // Similar to chrarg, segmentcopy is skipped for nonexistent entities,
                                    // and skipped for nonrandomized enemies otherwise.
                                    // eventCopies Item1 is not used because there is no rewrite here.
                                    if (getEntityFromInit(originalInit, t, out int seg) && shouldRewriteEntity(seg))
                                    {
                                        eventCopies.Add((0, 0, events.CopyEvent(theEvent, NewID()), t));
                                        segmentCopied = true;
                                    }
                                    // Figure out canRemove later
                                    continue;
                                }
                                else
                                {
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, 0, theEvent, t));
                                }
                            }
                            else if ((t.Type.StartsWith("common") || t.Type == "locarg" || t.Type == "removearg")
                                && commonEvents.TryGetValue(callee, out EMEVD.Event comEvent))
                            {
                                if (completedTemplates.Contains(t))
                                {
                                    canRemove = false;
                                    continue;
                                }
                                if (t.Type == "common") completedTemplates.Add(t);
                                eventCopies.Add((0, 0, comEvent, t));
                            }
                            else if (t.Type == "segmentcopy" && commonEvents.TryGetValue(callee, out comEvent))
                            {
                                if (getEntityFromInit(originalInit, t, out int seg) && shouldRewriteEntity(seg))
                                {
                                    eventCopies.Add((0, 0, events.CopyEvent(comEvent, NewID()), t));
                                    segmentCopied = true;
                                }
                                // Figure out canRemove later
                                continue;
                            }
                            else throw new Exception($"Can't find event {callee} with {t.Type} template");
                            // At this point, loc handling was added, so the event should not be removed.
                            canRemove = false;
                        }
                        // Hack for cases where there are multiple segmentcopy templates.
                        // canRemove should be true when any of them are copied elsewhere, since each initialization should apply to only one entity.
                        // This is not something any individual segmentcopy can determine, since it can only change canRemove from true->false.
                        // So instead do the contrapositive, if no segmentcopy resulted in a copy, canRemove should be false.
                        if (canRemove && ev.Template.Any(t => t.Type == "segmentcopy") && !segmentCopied)
                        {
                            canRemove = false;
                        }

                        bool forceRemove = false;
                        foreach (var copy in eventCopies)
                        {
                            (int entity, int target, EMEVD.Event e2, EnemyTemplate t) = copy;
                            // Main entities to replace, for chr events
                            Dictionary<EventValue, EventValue> reloc = new Dictionary<EventValue, EventValue>();
                            Dictionary<EventValue, EventValue> dupeReloc = null;
                            Dictionary<int, int> distReplace = new Dictionary<int, int>();
                            Instr init = originalInit;
                            // Event replacement option
                            if (t.NewEvent != null)
                            {
                                if (e2 == null) throw new Exception($"No original event to ReplaceEvent with for {callee}");
                                OldParams newPre = OldParams.Preprocess(e2);
                                e2.Instructions.Clear();
                                EventEdits newEdits = new EventEdits();
                                foreach (string cmd in events.Decomment(t.NewEvent))
                                {
                                    events.AddMacro(newEdits, EditType.AddAfter, cmd);
                                }
                                events.ApplyAdds(newEdits, e2, newPre);
                                newPre.Postprocess();
                            }

                            OldParams pre = e2 == null ? null : OldParams.Preprocess(e2);
                            int dupeIndex = -1;
                            if (entity != 0)
                            {
                                fillEntityIdMapping(reloc, entity, target, t.Type.StartsWith("multichr"));
                                // Fill in dupeIndex where possible, but only do dupeReloc for Dupe-only chr events? Mainly used by Sekiro.
                                // This used to look up entity in dupeEnemyMap, but this is not valid with randomized dupes.
                                if (dupeEnabled(target))
                                {
                                    getBaseTarget(target, out dupeIndex);
                                    if (t.Dupe != null)
                                    {
                                        if (dupeIndex != -1)
                                        {
                                            // Console.WriteLine($"dupe-only {t.Type}: {callee}");
                                            dupeReloc = dupeRelocs[dupeIndex];
                                        }
                                        else Console.WriteLine($"No dupe index found in {entity}->{target}, {originalInit}");
                                    }
                                }
                                if (t.Regions != null)
                                {
                                    transplantRegionSpecs(dupeReloc ?? reloc, distReplace, t.Regions, entity, target, getIntArgs(init.Args));
                                }
                                // Also we need to make a copy of the instruction at this point, so we don't edit the original
                                init = events.CopyInit(init, e2, initOld);
                            }
                            else if (t.Type == "copy" || t.Type == "segmentcopy")
                            {
                                init = events.CopyInit(init, e2, initOld);
                                init.Save(initOld);
                            }
                            // Renamings in the args, for common_func mainly
                            if (t.Name != null && t.Name.StartsWith("X"))
                            {
                                string[] nameParts = t.Name.Split(' ');
                                if (!events.ParseArgSpec(nameParts[0], out int entityPos) || !events.ParseArgSpec(nameParts[1], out int namePos)) throw new Exception($"Bad name spec {t.Name} for {callee}");
                                int nameTarget = (int)init[init.Offset + entityPos];
                                if (getRandomizedSource(nameTarget, ev.DupeIndex, out int nameSource, out _))
                                {
                                    int nameId = (int)init[init.Offset + namePos];
                                    // This is currently not necessary in Elden Ring, since this is only used in segment events,
                                    // where rewrites are manual (fill in parameters) and threat levels are ignored.
                                    reloc[EventValue.NpcName(nameId)] = EventValue.NpcName(GetCleverName(nameId, nameSource, nameTarget));
                                    if (nameParts.Length > 2)
                                    {
                                        if (!events.ParseArgSpec(nameParts[2], out int threatPos)) throw new Exception($"Bad name spec {t.Name} for {callee}");
                                        int threat = 0;
                                        if (nameSource > 0 && defaultData.TryGetValue(nameSource, out EnemyData nameData))
                                        {
                                            PARAM.Row npc = game.Params["NpcParam"][nameData.NPC];
                                            if (npc != null)
                                            {
                                                threat = (int)(uint)npc["threatLv"].Value;
                                            }
                                        }
                                        init[init.Offset + threatPos] = threat == 0 ? 1 : threat;
                                        init.Save(initOld);
                                        e.Instructions[i] = init.Val;
                                    }
                                }
                            }

                            // Add all edits
                            EventEdits edits = new EventEdits();
                            void addDefeatFlagLabel(string labelSpec, int flag)
                            {
                                string[] parts = labelSpec.Split(' ');
                                bool on = parts[0] == "on";
                                int label = int.Parse(parts[1]);
                                if (flag > 0)
                                {
                                    string cmd = $"GotoIfEventFlag(Label.Label{label}, {(on ? "ON" : "OFF")}, TargetEventFlagType.EventFlag, {flag})";
                                    events.AddMacro(edits, EditType.AddBefore, cmd);
                                }
                                else
                                {
                                    // If there is no defeat flag, assume the defeat flag is never set.
                                    // This means that "on 19" (end with nonexistent label) should do nothing,
                                    // "off 9" (existing label) should always jump.
                                    if (!on)
                                    {
                                        string cmd = $"GotoUnconditionally(Label.Label{label})";
                                        events.AddMacro(edits, EditType.AddBefore, cmd);
                                    }
                                }
                            }

                            if (t.CondIdentity != null)
                            {
                                // No-op, but used to infer condition group rewrites in other instructions
                                foreach (string check in events.Decomment(t.CondIdentity))
                                {
                                    events.IdentityMacro(edits, check);
                                }
                            }
                            // TODO: Make partial Remove and Replace work for Elden Ring (add types)
                            if (t.Remove != null)
                            {
                                foreach (string remove in phraseRe.Split(t.Remove))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.Removes != null)
                            {
                                foreach (string remove in events.Decomment(t.Removes))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            // This is mainly only meant to be used in Sekiro
                            if (t.RemoveDupe != null && target != 0 && !infos[target].IsBossTarget)
                            {
                                foreach (string remove in phraseRe.Split(t.RemoveDupe))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.TreeDragons != null)
                            {
                                // This isn't used exactly anymore, since ineligible tree dragons can't be removed without affecting the final deathblow thing
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
                            if (t.MultiplayerBuff != null)
                            {
                                // TODO: Support event replacements
                                int buffEntity;
                                if (events.ParseArgSpec(t.MultiplayerBuff, out int buffPos))
                                {
                                    buffEntity = (int)init[init.Offset + buffPos];
                                }
                                else
                                {
                                    buffEntity = int.Parse(t.MultiplayerBuff);
                                    buffPos = -1;
                                }
                                int replaceEntity = -1;
                                if (buffGroupEntities.TryGetValue(buffEntity, out int altGroup))
                                {
                                    // Replace removed groups with new ones
                                    replaceEntity = altGroup;
                                    // Console.WriteLine($"group {buffEntity} -> {replaceEntity}");
                                }
                                else if (infos.TryGetValue(buffEntity, out EnemyInfo buffInfo) && buffInfo.BuddyGroup > 0)
                                {
                                    // Replace single entities with groups too
                                    replaceEntity = buffInfo.BuddyGroup;
                                    // Console.WriteLine($"single {buffEntity} -> {replaceEntity}");
                                }
                                if (replaceEntity > 0)
                                {
                                    if (buffPos >= 0)
                                    {
                                        init[init.Offset + buffPos] = replaceEntity;
                                        init.Save(initOld);
                                        e.Instructions[i] = init.Val;
                                    }
                                    else
                                    {
                                        // TODO: This should probably be optional
                                        events.ReplaceMacro(
                                            edits,
                                            $"ActivateMultiplayerdependantBuffs({buffEntity})",
                                            $"ActivateMultiplayerdependantBuffs({replaceEntity})");
                                    }
                                }
                            }
                            if (t.MusicFlagArg != null)
                            {
                                if (!events.ParseArgSpec(t.MusicFlagArg, out int pos)) throw new Exception($"Invalid spec {t.MusicFlagArg}");
                                int musicFlag = (int)init[init.Offset + pos];
                                if (copyphaseNewMusicFlags.TryGetValue(musicFlag, out int newMusicFlag))
                                {
                                    init[init.Offset + pos] = newMusicFlag;
                                    init.Save(initOld);
                                    e.Instructions[i] = init.Val;
                                }

                            }
                            if (t.Replace != null)
                            {
                                foreach (string replace in phraseRe.Split(t.Replace))
                                {
                                    events.ReplaceMacro(edits, replace);
                                }
                            }
                            if (t.Replaces != null)
                            {
                                events.ReplaceMacro(edits, t.Replaces);
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
                                if (!getEntityFromInit(originalInit, t, out int segTarget))
                                {
                                    throw new Exception($"Internal error: No boss entity found for {originalInit}");
                                }
                                if (!segmentCmds.TryGetValue(segTarget, out Dictionary<string, CommandSegment> targetSegments))
                                {
                                    throw new Exception($"Internal error: No segments collected for {segTarget} in {callee}");
                                }
                                SourceSegmentData getSourceSegmentData(int index)
                                {
                                    bool randomized = getRandomizedSource(segTarget, index, out int sourceId, out int realTarget);
                                    SourceSegmentData data = new SourceSegmentData
                                    {
                                        DupeIndex = index,
                                        Source = sourceId,
                                        Target = realTarget,
                                        IsRandom = randomized && (index == -1 || infos[realTarget].DupeFrom != sourceId),
                                    };
                                    // Console.WriteLine($"IsRandom {sourceId}->{realTarget}: {data.IsRandom} ({randomized})");
                                    data.Reloc = new Dictionary<EventValue, EventValue>();
                                    if (!segmentCmds.TryGetValue(data.Source, out Dictionary<string, CommandSegment> sourceSegments))
                                    {
                                        if (!segmentCmds.TryGetValue(defaultSegmentEnemy, out sourceSegments))
                                        {
                                            // Note: This is not supported anymore (find a different way to exit out early)
                                            if (!partialEventConfig) throw new NotImplementedException($"Unsupported {data.Source}->{segTarget}");
                                        }
                                        data.Reloc[EventValue.Enemy(defaultSegmentEnemy)] = EventValue.Enemy(data.Target);
                                    }
                                    data.Segments = sourceSegments;
                                    if (data.IsRandom || index >= 0)
                                    {
                                        fillEntityIdMapping(data.Reloc, data.Source, data.Target, true);
                                        data.IsSwapped = owners.TryGetValue(data.Source, out List<int> swapHelpers)
                                            && swapHelpers.Any(helper => swapMapping.ContainsKey((data.Target, helper)));
                                    }
                                    else
                                    {
                                        // Can call fillEntityIdMapping if absolutely necessary (e.g. bc copyRegion), but try to avoid this
                                    }
                                    data.IsEncounter = data.Segments.ContainsKey("presetup");
                                    return data;
                                }
                                // Fill these in so that the main event editing is last
                                List<SourceSegmentData> sources = new List<SourceSegmentData>();
                                if (ev.Dupe == "copyphase")
                                {
                                    // In some cases, phase transitions are independent, and DupeIndex is set to reflect that
                                    SourceSegmentData copyData = getSourceSegmentData(ev.DupeIndex);
                                    sources.Add(copyData);
                                    if (t.StartFlag > 0
                                        && infos.TryGetValue(copyData.Target, out EnemyInfo targetInfo)
                                        && t.StartFlag != targetInfo.StartFlag)
                                    {
                                        // Console.WriteLine($"start flag: {t.StartFlag} -> {targetInfo.StartFlag}");
                                        events.ReplaceMacro(edits, t.StartFlag.ToString(), targetInfo.StartFlag.ToString(), EventValueType.Flag);
                                    }
                                    if (int.TryParse(t.MusicFlag, out int musicFlag)
                                        && copyphaseNewMusicFlags.TryGetValue(musicFlag, out int newMusicFlag))
                                    {
                                        events.AddMacro(edits, new List<EventAddCommand>
                                        {
                                            new EventAddCommand
                                            {
                                                Before = $"SetEventFlag(TargetEventFlagType.EventFlag, {musicFlag}, ON)",
                                                Cmds = new List<string>
                                                {
                                                    $"SetEventFlag(TargetEventFlagType.EventFlag, {newMusicFlag}, OFF)",
                                                    $"WaitFixedTimeFrames(1)",
                                                    $"SetEventFlag(TargetEventFlagType.EventFlag, {newMusicFlag}, ON)",
                                                },
                                            },
                                        });
                                    }
                                }
                                else
                                {
                                    for (int c = 0; c < dupeCount(segTarget); c++)
                                    {
                                        sources.Add(getSourceSegmentData(c));
                                    }
                                    sources.Add(getSourceSegmentData(-1));
                                }
                                // Hacky special edit, used by healthbar events to avoid showing healthbar after defeat
                                if (t.DefeatFlagLabel != null && entity == 0)
                                {
                                    addDefeatFlagLabel(t.DefeatFlagLabel, infos[segTarget].DefeatFlag);
                                }

                                bool encounterTarget = targetSegments.ContainsKey("presetup");
                                bool minibossTarget = targetSegments.ContainsKey("quickstart");
                                int removeIndex = 0;
                                Dictionary<string, string> segmentNames = new Dictionary<string, string>();
                                string previous = null;
                                foreach (CommandSegment segment in t.Segments)
                                {
                                    if (segment.Type == "altsetup" || segment.Type == "althealthbar" || segment.Type == "altunhealthbar")
                                    {
                                        // altsetup is additional data that doesn't replace any originals
                                        continue;
                                    }
                                    string name = $"{segment.Type}_segment";
                                    if (segment.Type == "remove")
                                    {
                                        // if (!randomizedTarget) continue;
                                        name += $"{++removeIndex}";
                                    }
                                    string preName = null;
                                    if (segment.PreSegment != null
                                        && !segmentNames.TryGetValue(segment.PreSegment, out preName))
                                    {
                                        throw new Exception($"Internal error: Bad ref {segment.Type}->{segment.PreSegment} {segTarget} in {callee}");
                                    }
                                    segmentNames[segment.Type] = name;
                                    events.RegisterSegment(
                                        edits, name, segment.Start, segment.End,
                                        segment.IgnoreMatch, preName);
                                    if (segment.Type != "remove")
                                    {
                                        events.CheckSegment(edits, name, previous);
                                        previous = name;
                                    }
                                    // Remove existing segments, or otherwise register them for matching
                                    List<string> alwaysRewrite = new List<string>
                                    {
                                        // To create one combined healthbar condition
                                        "healthbarcheck", "unhealthbarcheck",
                                        // To edit in other conditions
                                        "end", "endphase",
                                    };
                                    // TODO dupe: also have to deal with speffect flags
                                    // Just rewrite everything for the moment
                                    bool removeCommands = true;
                                    // This "remove remove" logic is specific to MoveOnly for now, mostly for location-editing
                                    // (or lack thereof) and reflecting chr events being left alone
                                    foreach (string cmd in segment.Commands)
                                    {
                                        if (removeCommands)
                                        {
                                            events.RemoveSegmentMacro(edits, name, cmd);
                                        }
                                        else
                                        {
                                            events.MatchSegmentMacro(edits, name, cmd);
                                        }
                                    }
                                    // if (segTarget == 1042360800) Console.WriteLine($"Segment in {e2.ID}: {name}, rm {segment.Commands.Count}");

                                    // For each source, build up a list of commands to use and modify the event
                                    // Exceptional behaviors:
                                    // 1. (un)healthbarcheck - constructed from scratch if non-miniboss source/multiple sources
                                    // 2. start - sets flags at the start (special exception)
                                    // 3. end - checks flags after the last MAIN condition in the end block

                                    // (TODO dupe: make sure ForceCharacterDeath is appropriately removed)

                                    List<int> dupeFighters = new List<int>();
                                    List<int> dupeEndFlags = new List<int>();
                                    foreach (SourceSegmentData data in sources)
                                    {
                                        // Don't need to add anything if the commands remain the same
                                        // (However, currently, we just rewrite everything)
                                        if (data.DupeIndex == -1 && !removeCommands) continue;

                                        // Find corresponding new segment(s)
                                        List<string> duals = new List<string>();
                                        if (segment.Type == "remove")
                                        {
                                            // Nothing to add
                                        }
                                        else if (segment.Type == "dead" || segment.Type == "disable")
                                        {
                                            duals.Add(data.Segments.ContainsKey("dead") ? "dead" : "disable");
                                        }
                                        else if (segment.Type == "setup" || segment.Type == "start" || segment.Type == "quickstart")
                                        {
                                            // Combined handling for these cases as they may both add setup blocks
                                            // Either this is a setup block or there is no handling for setup, so do it in start
                                            if (segment.Type == "setup"
                                                || (segment.Type.Contains("start") && targetSegments.ContainsKey("altsetup")))
                                            {
                                                if (data.IsEncounter)
                                                {
                                                    // Normal case where a fog gate is always present
                                                    duals.Add("presetup");
                                                    duals.Add("secondsetup");
                                                }
                                                else
                                                {
                                                    duals.Add(data.Segments.ContainsKey("setup") ? "setup" : "altsetup");
                                                }
                                            }
                                            if (segment.Type == "start" || segment.Type == "quickstart")
                                            {
                                                if (data.IsEncounter && !encounterTarget)
                                                {
                                                    // If secondstart is present in source and won't be mapped normally,
                                                    // add it on here.
                                                    duals.Add("secondstart");
                                                }
                                                if (data.Segments.ContainsKey("quickstart"))
                                                {
                                                    duals.Add("quickstart");
                                                    if (segment.Type == "start")
                                                    {
                                                        duals.Add("healthbar");
                                                    }
                                                }
                                                else
                                                {
                                                    duals.Add("start");
                                                }
                                            }
                                        }
                                        else if (segment.Type.StartsWith("end"))
                                        {
                                            duals.Add(data.Segments.ContainsKey("end") ? "end" : "endphase");
                                        }
                                        else if (encounterSegmentTypes.Contains(segment.Type))
                                        {
                                            if (data.IsEncounter)
                                            {
                                                // If this is an encounter type and source also has them, they can all be mapped
                                                duals.Add(segment.Type);
                                            }
                                            else
                                            {
                                                // Otherwise, the source is a regular fog gate entry encounter.
                                                // For first encounter, if there's a gateless cutscene, firstsetup will be after it
                                                // For second encounter, the boss should be enabled but unactivated from the start.
                                                if (segment.Type == "firstsetup" || segment.Type == "secondsetup")
                                                {
                                                    duals.Add(data.Segments.ContainsKey("setup") ? "setup" : "altsetup");
                                                }
                                            }
                                        }
                                        else if (segment.Type == "healthbar")
                                        {
                                            if (data.Segments.ContainsKey("healthbar"))
                                            {
                                                duals.Add("healthbar");
                                            }
                                            else if (data.Segments.ContainsKey("althealthbar"))
                                            {
                                                duals.Add("althealthbar");
                                            }
                                            else
                                            {
                                                duals.Add("start");
                                            }
                                        }
                                        else if (segment.Type == "unhealthbar")
                                        {
                                            if (data.Segments.ContainsKey("unhealthbar"))
                                            {
                                                duals.Add("unhealthbar");
                                            }
                                            else if (data.Segments.ContainsKey("altunhealthbar"))
                                            {
                                                duals.Add("altunhealthbar");
                                            }
                                            else
                                            {
                                                duals.Add("start");
                                            }
                                        }
                                        else if (segment.Type == "healthbarcheck" || segment.Type == "unhealthbarcheck")
                                        {
                                            // Copy exactly if present in source, and only one source
                                            // Otherwise, this is created dynamically
                                            if (data.Segments.ContainsKey(segment.Type) && sources.Count == 1)
                                            {
                                                duals.Add(segment.Type);
                                            }
                                        }
                                        // TODO: This may fail in DS3
                                        else throw new Exception($"Internal error: unknown target segment {segment.Type} in {callee}");

                                        // Entry commands
                                        List<string> editedCmds = new List<string>();
                                        int dupeFlag = -1;
                                        // Dupe starts use a standalone event, since they may have complicated setup
                                        if (segment.Type == "start" && data.DupeIndex >= 0 && ev.Dupe != "copyphase")
                                        {
                                            dupeFlag = NewID(true);
                                            editedCmds.Add($"EndIfEventFlag(0, 1, 0, {dupeFlag})");
                                            editedCmds.Add($"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {dupeFlag})");
                                        }
                                        void setInvincibility(bool enable)
                                        {
                                            if (minibossTarget) return;
                                            if (infos[data.Source].HasTag("noinvincible")) return;
                                            List<int> invTargets = getHelperTargets(data.Target);
                                            invTargets.Add(data.Target);
                                            foreach (int invTarget in invTargets)
                                            {
                                                // No Gideon invincibility
                                                // TODO: Figure out how to safely get this data. It's only this one entity id
                                                if (invTarget == 11050850) continue;
                                                // TODO dupe: base on dupe indices
                                                revMapping.TryGetValue(invTarget, out int x);
                                                // Don't bother if the enemy is not moved (should skip this section entirely?)
                                                if (!revMapping.TryGetValue(invTarget, out int invSource)) continue;
                                                // Console.WriteLine($"inv target {invTarget} to {x} in {segment.Type}");
                                                editedCmds.Add($"SetCharacterInvincibility({invTarget}, {(enable ? "Enabled" : "Disabled")})");
                                                // Invincibility/immortality issue in Sekiro.
                                                // Not sure if it's present in DS3/Elden Ring, but can't hurt to reenable
                                                if (!enable && infos[invSource].IsImmortal)
                                                {
                                                    editedCmds.Add($"SetCharacterImmortality({invTarget}, Enabled)");
                                                }
                                            }
                                        }
                                        // These are more complicated in Elden Ring so eliminate interference (e.g. Rennala 1 in Elden Beast)
                                        if (game.EldenRing && segment.Type.StartsWith("end"))
                                        {
                                            editedCmds.Add("IfElapsedSeconds(MAIN, 0)");
                                        }
                                        if (game.EldenRing && segment.Type == "start")
                                        {
                                            setInvincibility(false);
                                        }

                                        // Main commands, based on dual list earlier
                                        string lastMainCond = null;
                                        foreach (string dual in duals)
                                        {
                                            CommandSegment sourceSegment = data.Segments[dual];
                                            // Regions will mostly not move when non-move dupe, but e.g. needed for generators
                                            if (sourceSegment.Regions != null && (data.IsRandom || data.DupeIndex >= 0))
                                            {
                                                transplantRegionSpecs(
                                                    data.Reloc, new Dictionary<int, int>(), sourceSegment.Regions,
                                                    data.Source, data.Target, new List<int>());
                                            }
                                            if (sourceSegment.ProgressFlag != null && data.DupeIndex >= 0)
                                            {
                                                List<int> flags = allocateProgressFlags(sourceSegment.ProgressFlag);
                                                foreach (int flag in flags)
                                                {
                                                    reloc[EventValue.Flag(flag)] = EventValue.Flag(progressFlagCopies[flag][data.DupeIndex]);
                                                }
                                            }
                                            List<string> ignoreCommands = new List<string>();
                                            if (minibossTarget && sourceSegment.EncounterOnly != null)
                                            {
                                                ignoreCommands = sourceSegment.EncounterOnly;
                                            }
                                            else if (!minibossTarget && sourceSegment.NonEncounterOnly != null)
                                            {
                                                ignoreCommands = sourceSegment.NonEncounterOnly;
                                            }
                                            if (data.IsSwapped && sourceSegment.SpecificHelperOnly != null)
                                            {
                                                ignoreCommands.AddRange(sourceSegment.SpecificHelperOnly);
                                            }
                                            if (!data.IsRandom && sourceSegment.MoveOnly != null)
                                            {
                                                ignoreCommands.AddRange(sourceSegment.MoveOnly);
                                            }
                                            else if (data.IsRandom && sourceSegment.NonMoveOnly != null)
                                            {
                                                ignoreCommands.AddRange(sourceSegment.NonMoveOnly);
                                            }
                                            if ((data.IsRandom || data.DupeIndex >= 0) && sourceSegment.OriginalOnly != null)
                                            {
                                                ignoreCommands.AddRange(sourceSegment.OriginalOnly);
                                            }
                                            foreach (string newCmdStr in sourceSegment.NewCommands)
                                            {
                                                string cmdStr = newCmdStr;
                                                (string cmd, List<string> cmdArgs) = ParseCommandString(cmdStr);
                                                // Generic miniboss command removal
                                                if (ignoreCommands.Contains(cmdStr))
                                                {
                                                    continue;
                                                }
                                                // Remove some commands if end->endphase, and also if setting up a side-event
                                                // All end->end issues should be handled by segmentReloc
                                                if (dual == "end" && (segment.Type == "endphase" || data.DupeIndex >= 0))
                                                {
                                                    if (cmd == "HandleBossDefeat" || cmd == "PlaySE"
                                                        || cmd == "HandleBossDefeatAndDisplayBanner"
                                                        || cmd == "WaitFixedTimeSeconds"
                                                        || cmd == "WaitFixedTimeFrames")
                                                    {
                                                        continue;
                                                    }
#if DEBUG
                                                    if (cmd == "DisplayBossHealthBar"
                                                        && cmdArgs[0] != "Disabled" && cmdArgs[0] != "0")
                                                    {
                                                        throw new Exception($"Healthbar state set to enabled rather than disabled in {segTarget}");
                                                    }
#endif
                                                }
                                                // Remove commands if dead/disable->anything
                                                if (dual == "dead" || dual == "disable")
                                                {
                                                    if (cmd == "ForceCharacterDeath")
                                                    {
                                                        continue;
                                                    }
                                                }
                                                // Healthbar segments only keep healthbar events
                                                if ((segment.Type == "healthbar" || segment.Type == "unhealthbar") && dual == "start")
                                                {
                                                    if (!cmd.Contains("BossHealthBar"))
                                                    {
                                                        continue;
                                                    }
                                                }
                                                // Conversely, minibosses exclude healthbar handling, except for healthbar event
                                                // Also most things other than the boss staying still
                                                else if (minibossTarget && !dual.Contains("healthbar"))
                                                {
                                                    if (cmd.Contains("BossHealthBar"))
                                                    {
                                                        continue;
                                                    }
                                                }
                                                if (dual == "start"
                                                    || dual == "healthbar" || dual == "althealthbar"
                                                    || segment.Type == "healthbar" || segment.Type == "unhealthbar")
                                                {
                                                    // Find instances of DisplayBossHealthBar and rewrite them
                                                    // segTarget is used for naming purposes - data.Target may not exist for dupes
                                                    if (cmd.Contains("BossHealthBar"))
                                                    {
                                                        int nameSource = int.Parse(cmdArgs[1]);
                                                        int nameSlot = int.Parse(cmdArgs[2]);
                                                        int targetNameId = int.Parse(cmdArgs[3]);
                                                        int nameId = targetNameId;
                                                        if (nameSource == defaultSegmentEnemy)
                                                        {
                                                            // GetCleverName looks up source info so we must insert the actual source.
                                                            nameSource = data.Source;
                                                            // This is the only place where a fake id is used, so try to replace it.
                                                            // If we fail, ??? will be used in non-English languages.
                                                            string targetCmd = (segment.NewCommands ?? segment.Commands)
                                                                .Find(c => c.Contains("BossHealthBar"));
                                                            if (targetCmd != null)
                                                            {
                                                                (_, List<string> nameArgs) = ParseCommandString(targetCmd);
                                                                int.TryParse(nameArgs[3], out targetNameId);
                                                            }
#if DEBUG
                                                            else if (infos[segTarget].NpcName <= 0)
                                                            {
                                                                Console.WriteLine($"Unknown name for {segTarget}");
                                                            }
#endif
                                                        }
                                                        if (segment.Type == "unhealthbar")
                                                        {
                                                            cmdArgs[0] = "Disabled";
                                                            cmdStr = $"{cmd}({string.Join(", ", cmdArgs)})";
                                                        }
                                                        if (dupeEnabled(segTarget))
                                                        {
                                                            // We'll want to either skip the healthbar or offset it here
                                                            int newNameSlot = getHealthbarIndex(nameSource, segTarget, data.DupeIndex);
                                                            // if (nameSlot != newNameSlot) Console.WriteLine($"Changing {t.Type} {nameSource}->{segTarget}[{data.DupeIndex}]: {nameSlot}->{newNameSlot}");
                                                            if (newNameSlot == -1) continue;
                                                            cmdArgs[2] = newNameSlot.ToString();
                                                            cmdStr = $"{cmd}({string.Join(", ", cmdArgs)})";
                                                        }
                                                        if (swapMapping.TryGetValue((data.Target, nameSource), out int swapSource))
                                                        {
                                                            nameSource = swapSource;
                                                        }
                                                        data.Reloc[EventValue.NpcName(nameId)] =
                                                            EventValue.NpcName(GetCleverName(targetNameId, nameSource, segTarget, true));
                                                    }
                                                }
                                                if (dual == "end" && dupeEnabled(segTarget))
                                                {
                                                    // Edge case: we use disabling healthbars in "end" to make it work cleanly
                                                    // as "endphase", but "end" also executes whenever a dupe is defeated, so
                                                    // avoid clearing a different healthbar.
                                                    // See also: "Healthbar state" check above
                                                    if (cmd.Contains("BossHealthBar"))
                                                    {
                                                        int nameSource = int.Parse(cmdArgs[1]);
                                                        // We'll want to either skip the healthbar or offset it here
                                                        int newNameSlot = getHealthbarIndex(nameSource, segTarget, data.DupeIndex);
                                                        if (newNameSlot == -1) continue;
                                                        cmdArgs[2] = newNameSlot.ToString();
                                                        cmdStr = $"{cmd}({string.Join(", ", cmdArgs)})";
                                                    }
                                                }
                                                string newCmd = events.RewriteInts(cmdStr, data.Reloc);
                                                // if (segTarget == 1042360800) Console.WriteLine($"{cmdStr} -> {newCmd}");
                                                editedCmds.Add(newCmd);
                                                // Detection for final end condition, while we have parse data
                                                if ((cmd.StartsWith("If") || condCmdRe.IsMatch(cmd))
                                                    && (cmdArgs[0] == "MAIN" || (int.TryParse(cmdArgs[0], out int group) && group == 0)))
                                                {
                                                    // This doesn't save the index, so make sure this is unique if it has to be
                                                    lastMainCond = newCmd;
                                                }
                                            }
                                        }
                                        // Main commands when no duals exist
                                        if ((segment.Type == "healthbarcheck" || segment.Type == "unhealthbarcheck")
                                            && !duals.Contains(segment.Type))
                                        {
                                            List<int> fighters = new List<int> { data.Target };
                                            if (owners.TryGetValue(data.Source, out List<int> helpers))
                                            {
                                                foreach (int helper in helpers)
                                                {
                                                    if (infos.TryGetValue(helper, out EnemyInfo inf)
                                                        && (inf.HasTag("mainhelper") || inf.HasTag("combathelper"))
                                                        && helperMapping.TryGetValue((data.Target, helper), out int helperTarget))
                                                    {
                                                        fighters.Add(helperTarget);
                                                    }
                                                }
                                            }
                                            // Assumption: events containing these checks are not copyphase,
                                            // so they cannot be split up like this.
                                            if (data.DupeIndex >= 0)
                                            {
                                                // Dupes come first in the iteration order, and just apply later on
                                                dupeFighters.AddRange(fighters);
                                                fighters = null;
                                            }
                                            else
                                            {
                                                // Apply any dupes in last iteration
                                                fighters.AddRange(dupeFighters);
                                            }
                                            if (fighters != null && segment.Type == "healthbarcheck")
                                            {
                                                foreach (int id in fighters)
                                                {
                                                    editedCmds.Add($"IfCharacterAIState(OR_11, {id}, AIStateType.Combat, ComparisonType.Equal, 1)");
                                                }
                                                editedCmds.AddRange(new List<string>
                                                {
                                                    "IfConditionGroup(AND_11, PASS, OR_11)",
                                                    "IfEventFlag(AND_11, OFF, TargetEventFlagType.EventFlag, 9000)",
                                                    "IfConditionGroup(MAIN, PASS, AND_11)",
                                                });
                                            }
                                            else if (fighters != null && segment.Type == "unhealthbarcheck")
                                            {
                                                foreach (int id in fighters)
                                                {
                                                    editedCmds.Add($"IfCharacterAIState(OR_13, {id}, AIStateType.Combat, ComparisonType.Equal, 1)");
                                                }
                                                editedCmds.Add("IfConditionGroup(OR_12, FAIL, OR_13)");
                                                foreach (int id in fighters)
                                                {
                                                    editedCmds.Add($"IfCharacterDeadalive(AND_13, {id}, DeathState.Dead, ComparisonType.Equal, 1)");
                                                }
                                                editedCmds.AddRange(new List<string>
                                                {
                                                    "IfConditionGroup(OR_12, PASS, AND_13)",
                                                    "IfEventFlag(OR_12, ON, TargetEventFlagType.EventFlag, 9000)",
                                                    "IfConditionGroup(MAIN, PASS, OR_12)",
                                                });
                                            }
                                        }

                                        // Exit commands
                                        if (game.EldenRing && (segment.Type == "setup" || segment.Type == "presetup"))
                                        {
                                            setInvincibility(true);
                                        }
                                        if (!game.EldenRing && segment.Invincibility)
                                        {
                                            setInvincibility(segment.Type.Contains("start"));
                                        }
                                        if (duals.Contains("endphase") && segment.Type == "end" && data.DupeIndex == -1)
                                        {
                                            // In all cases, there should be one of these per fight, but exactly one
                                            if (game.DS3)
                                            {
                                                editedCmds.Add($"PlaySE({segTarget}, SoundType.s_SFX, 777777777)");
                                                editedCmds.Add($"HandleBossDefeat({segTarget})");
                                            }
                                            else
                                            {
                                                editedCmds.Add($"PlaySE({segTarget}, SoundType.SFX, 888880000)");
                                                // Use "Great Enemy Felled", I guess?
                                                editedCmds.Add($"HandleBossDefeatAndDisplayBanner({segTarget}, 17)");
                                            }
                                        }
                                        if ((segment.Type == "end" || segment.Type == "endphase") && ev.Dupe != "copyphase")
                                        {
                                            if (data.DupeIndex == -1 && dupeEndFlags.Count > 0)
                                            {
                                                // Special edit for adding flags to end condition
                                                int mainIndex = editedCmds.IndexOf(lastMainCond);
                                                if (mainIndex == -1) throw new Exception($"No MAIN condition found in {segment.Type} segment for {data.Source} [{lastMainCond}]");
                                                string waitForFlag(int f) => $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {f})";
                                                editedCmds.InsertRange(mainIndex + 1, dupeEndFlags.Select(waitForFlag));
                                            }
                                            else if (data.DupeIndex >= 0)
                                            {
                                                // Add the flag here
                                                dupeFlag = NewID(true);
                                                // TODO: Needs different args in DS3/Sekiro
                                                editedCmds.Add($"SetEventFlag(TargetEventFlagType.EventFlag, {dupeFlag}, ON)");
                                                dupeEndFlags.Add(dupeFlag);
                                            }
                                        }
                                        if (game.EldenRing && segment.Type.StartsWith("end"))
                                        {
                                            editedCmds.Add("IfElapsedSeconds(MAIN, 0)");
                                        }

                                        // Finally add everything together.
                                        if (dupeFlag == -1)
                                        {
                                            foreach (string cmd in editedCmds)
                                            {
                                                events.AddMacro(edits, EditType.SegmentAdd, cmd, name);
                                            }
                                        }
                                        else
                                        {
                                            // dupeFlag being set means flag communication, so use a separate event there
                                            if (segment.Type == "start")
                                            {
                                                // Edit main event to add trigger in start case
                                                string triggerCmd = $"SetEventFlag(TargetEventFlagType.EventFlag, {dupeFlag}, ON)";
                                                events.AddMacro(edits, EditType.SegmentAdd, triggerCmd, name);
                                            }
                                            editedCmds.Insert(0, $"EndIfEventFlag(0, 1, 0, {infos[segTarget].DefeatFlag})");
                                            int customEventId = NewID();
                                            EMEVD.Event customEvent = new EMEVD.Event(customEventId, EMEVD.Event.RestBehaviorType.Restart);
                                            EventEdits newEdits = new EventEdits();
                                            foreach (string cmd in editedCmds)
                                            {
                                                events.AddMacro(newEdits, EditType.AddAfter, cmd);
                                            }
                                            events.ApplyAdds(newEdits, customEvent);
                                            EMEVD.Instruction customInit = new EMEVD.Instruction(2000, 0, new List<object> { 0, customEventId, 0 });
                                            // TODO dupe: is this the correct map to use? probably, since this is loc?
                                            AddMulti(newInitializations, entry.Key, (customInit, customEvent));
                                        }
                                    }

                                    // Rewrite 'dead' segments to 'disable' ones, if it's the last template for it
                                    if (segment.Type == "dead" && t.Entity == lastDeadEntity)
                                    {
                                        events.RemoveMacro(edits, segment.Start, applyOnce: true);
                                        if (game.DS3)
                                        {
                                            // e.g. GotoIfEventFlag(Label.LABEL0, OFF, TargetEventFlagType.EventFlag, 13200800)
                                            Instr gotoIf = events.Parse(events.ParseAdd(segment.Start), pre);
                                            if (gotoIf.Name != "GotoIfEventFlag") throw new Exception($"Invalid segment {segment.Start}->{gotoIf} for {segTarget}");
                                            string flag = gotoIf[3].ToString();
                                            events.AddMacro(
                                                edits, EditType.AddBefore, $"SkipIfEventFlag(1, OFF, TargetEventFlagType.EventFlag, {flag})",
                                                segment.End, applyOnce: true);
                                        }
                                        else
                                        {
                                            // Try to be less fragile in Elden Ring, just jump to the label always
                                            // In some cases, this may mean to use a End other than EndUnconditionally
                                            // Basically, the first non-disable/deactivate/death command
                                            // TODO: Output different End in config?
                                            bool started = false;
                                            string endCmd = null;
                                            for (int j = 0; j < e2.Instructions.Count; j++)
                                            {
                                                EMEVD.Instruction ins = e2.Instructions[j];
                                                Instr instr = events.Parse(ins, pre);
                                                string cmd = instr.ToString();
                                                // TODO: Need to do smarter command matching than string equality
                                                // The space after instr.Name will be inconsistent between game versions
                                                if (cmd.Equals(segment.Start))
                                                {
                                                    started = true;
                                                    continue;
                                                }
                                                if (!started) continue;
                                                if (cmd.Equals(segment.End)) break;
                                                if (instr.Name != null && instr.Name != "Label69"
                                                    && instr.Name != "ChangeCharacterEnableState"
                                                    && instr.Name != "ChangeCharacterCollisionState"
                                                    && instr.Name != "SetCharacterAnimationState"
                                                    && instr.Name != "ForceCharacterDeath"
                                                    && instr.Name != "DeactivateGenerator"
                                                    && instr.Name != "SetGeneratorState"
                                                    && !segment.Commands.Contains(cmd))
                                                {
                                                    endCmd = cmd;
                                                    break;
                                                }
                                            }
                                            if (!started) throw new Exception($"Could not find disable segment for {segTarget}");
                                            // Console.WriteLine($"Entity {lastDeadEntity}: goto {name} before {endCmd};");
                                            events.AddMacro(
                                                edits, EditType.AddBefore, segment.Start,
                                                endCmd ?? segment.End, applyOnce: true);
                                        }
                                    }
                                }
                            }
                            // Replacing Lesser Fingercreeper (#1035500225) in Liurnia of the Lakes - Caria Manor: Giant Ant (#12010204) from Ainsel River
                            if (t.Animation != null)
                            {
                                string[] parts = t.Animation.Split(' ');
                                string type = parts[0];
                                int parsePart(int index)
                                {
                                    string part = parts[index];
                                    if (events.ParseArgSpec(part, out int partPos)) return (int)originalInit[originalInit.Offset + partPos];
                                    return int.Parse(part);
                                }
                                string parseArgPart(int index)
                                {
                                    string part = parts[index];
                                    if (events.ParseArgSpec(part, out int partPos)) return $"X{partPos * 4}_4";
                                    return part;
                                }
                                // There are two types of animation routines: rewriting 5450-based events, and changing anims
                                if (type == "active" || type == "passive" || type == "suspend")
                                {
                                    // For 5450-based events, we need to remove the 5450 check before a MAIN IfConditionGroup
                                    // For passive events, we additionally need to disable AI with starting anim and
                                    // enable AI after MAIN check if starting anim is not set.
                                    if (e2 == null) throw new Exception($"Internal error: {callee} anim template type {t.Type}");
                                    bool alwaysActive = type == "active";
                                    Instr ifActive = null;
                                    List<string> aiCommands(bool enable)
                                    {
                                        if (alwaysActive) return new List<string>();
                                        if (type == "passive")
                                        {
                                            // Passive if an animation is not present
                                            return new List<string>
                                            {
                                                $"SkipIfComparison(1, ComparisonType.NotEqual, X4_4, -1)",
                                                $"SetCharacterAIState(X0_4, {(enable ? "Enabled" : "Disabled")})",
                                            };
                                        }
                                        if (ifActive == null)
                                        {
                                            // Passive if an animation is not present
                                            return new List<string>
                                            {
                                                $"SkipIfComparison(1, ComparisonType.NotEqual, {parseArgPart(2)}, -1)",
                                                $"SetCharacterAIState({parseArgPart(1)}, {(enable ? "Enabled" : "Disabled")})",
                                            };
                                        }
                                        else
                                        {
                                            // Passive if an animation is not present *and* the "if active" condition is false
                                            return new List<string>
                                            {
                                                $"SkipIfComparison(3, ComparisonType.NotEqual, {parseArgPart(2)}, -1)",
                                                ifActive.ToString(),
                                                $"SkipUnconditionally(1)",
                                                $"SetCharacterAIState({parseArgPart(1)}, {(enable ? "Enabled" : "Disabled")})",
                                            };
                                        }
                                    }
                                    Instr prevInstr = null;
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        EMEVD.Instruction ins = e2.Instructions[j];
                                        Instr instr = events.Parse(ins, pre);
                                        if (type == "suspend" && instr.Name == "IfCharacterAIState")
                                        {
                                            // Before the main IfConditionGroup, see if there is a check for IfCharacterAIState.
                                            // First check this applies to the entity
                                            string entityStr = parseArgPart(1);
                                            bool isCorrect = false;
                                            if (events.ParseArgSpec(entityStr, out int entityPos))
                                            {
                                                // We are using param-aware mode in Elden Ring
                                                isCorrect = instr[1] is string arg && events.ParseArgSpec(arg, out int argPos) && argPos == entityPos;
                                            }
                                            else if (int.TryParse(entityStr, out int animEntity) && animEntity > 0)
                                            {
                                                isCorrect = entityStr == instr[1].ToString();
                                            }
                                            // If no statement, keep alwaysActive = false (unable to use AI, should be disabled)
                                            // If it is unconditional, alwaysActive = true (able to use AI to wake up)
                                            // If it is conditional, keep alwaysActive = false, unless condition is true.
                                            if (isCorrect && instr[2].ToString() == "3")
                                            {
                                                if (prevInstr != null
                                                    && (prevInstr.Name == "SkipIfUnsignedComparison" || prevInstr.Name == "SkipIfComparison2"))
                                                {
                                                    if (ifActive == null) ifActive = prevInstr;
                                                }
                                                else
                                                {
                                                    alwaysActive = true;
                                                }
                                            }
                                        }
                                        if (instr.Name == "IfConditionGroup" && instr[0].ToString() == "0")
                                        {
                                            string toFind = instr.ToString();
                                            foreach (string cmd in aiCommands(true))
                                            {
                                                events.AddMacro(edits, EditType.AddAfter, cmd, instr.ToString());
                                            }
                                            break;
                                        }
                                        if (instr.Name == "IfCharacterHasSpEffect"
                                            && (instr[2].ToString() == "5450" || instr[2].ToString() == "5080"))
                                        {
                                            events.RemoveMacro(edits, instr.ToString());
                                        }
                                        prevInstr = instr;
                                    }
                                    // Initial disable at the start
                                    foreach (string cmd in aiCommands(false))
                                    {
                                        events.AddMacro(edits, EditType.AddBefore, cmd);
                                    }
                                }
                                else
                                {
                                    int locEntity = parsePart(1);
                                    // Previous bug: we may have several copies of this, if the number of dupes doesn't
                                    // depend on entity (since entity is encoded in Animation), but the entity itself
                                    // may not have that many copies.
                                    bool changed = getRandomizedSource(locEntity, ev.DupeIndex, out int source, out _);
                                    if (ev.DupeIndex == -1 && infos.TryGetValue(locEntity, out EnemyInfo locsInfo) && locsInfo.DupeFrom == source)
                                    {
                                        // The "changed" applies to whether a transfer is required, but in the specific case of
                                        // animations, we can allow an in-spot duplicate to keep the same animation, and not deal with
                                        // startingAnimations being available or gravity primary positions.
                                        changed = false;
                                    }
                                    if (changed && source > 0 && infos[source].HasTag("hidden"))
                                    {
                                        forceRemove = true;
                                    }
                                    else if (changed && source > 0)
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
                                            else if (int.TryParse(prevAnim, out int prevAnimId) && prevAnimId > 0)
                                            {
                                                events.ReplaceMacro(edits, prevAnim, newAnim.ToString(), type: EventValueType.Animation);
                                            }
                                        }
                                        init.Save(initOld);
                                        e.Instructions[i] = init.Val;
                                        // Console.WriteLine($"{events.Parse(e.Instructions[i])}");
                                    }
                                }
                            }
                            if (t.BossBgmArg != null && opt["bossbgm"])
                            {
                                string[] parts = t.BossBgmArg.Split(' ');
                                if (!events.ParseArgSpec(parts[0], out int defeatFlagPos) || !events.ParseArgSpec(parts[1], out int bgmIdPos))
                                {
                                    throw new Exception($"Internal error: badly formatted BossBgmArg {t.BossBgmArg} for {init}");
                                }
                                int defeatFlag = (int)originalInit[originalInit.Offset + defeatFlagPos];
                                int bgmId = (int)originalInit[originalInit.Offset + bgmIdPos];
                                if (assignedDefeatFlagBgmIds.TryGetValue(defeatFlag, out int newBgmId))
                                {
                                    init[init.Offset + bgmIdPos] = newBgmId;
                                    if (opt["explainbossbgm"])
                                    {
                                        Console.WriteLine($"For {defeatFlag}: {bgmId} -> {newBgmId}");
                                    }
                                    init.Save(initOld);
                                    e.Instructions[i] = init.Val;
                                }
                            }
                            // Conditional flags present in both the event and the target
                            if (entity > 0 && infos.ContainsKey(target))
                            {
                                void removeOrReplaceFlag(int templateFlag, int targetFlag)
                                {
                                    if (targetFlag == 0)
                                    {
                                        events.RemoveMacro(edits, templateFlag.ToString(), type: EventValueType.Flag);
                                    }
                                    else
                                    {
                                        events.ReplaceMacro(edits, templateFlag.ToString(), targetFlag.ToString(), type: EventValueType.Flag);
                                    }
                                }
                                EnemyInfo targetInfo = infos[target];
                                if (t.DefeatFlag == -1 && targetInfo.DefeatFlag != 0)
                                {
                                    // Add a defeat check when the existing one cannot be easily edited.
                                    // Mainly Commander Niall
                                }
                                if (t.DefeatFlag > 0)
                                {
                                    if (targetInfo.DefeatFlag == 0 && targetInfo.IsImportantTarget)
                                    {
                                        throw new Exception($"{target} has no defeat flag defined, but was randomized to {entity} in {callee}");
                                    }
                                    removeOrReplaceFlag(t.DefeatFlag, targetInfo.DefeatFlag);
                                }
                                if (t.DefeatFlagLabel != null)
                                {
                                    addDefeatFlagLabel(t.DefeatFlagLabel, targetInfo.DefeatFlag);
                                }
                                if (t.StartFlag == -1 && targetInfo.StartFlag != 0)
                                {
                                    // This is a feature to add a start flag to inference-heavy events
                                    events.AddMacro(edits, EditType.AddBefore, $"IF Event Flag (0,1,0,{targetInfo.StartFlag})");
                                }
                                if (t.StartFlag > 0)
                                {
                                    if (targetInfo.StartFlag <= 0 && targetInfo.IsBossTarget)
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
                                    else if (targetInfo.MusicFlag > 0 && !game.EldenRing)
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
                                // Currently, add progress flags for simultaneous fights in chr events
                                // Also done separately in segments, to support loc-type situations
                                if (t.ProgressFlag != null && infos[target].HasTag("dupe") && dupeIndex >= 0)
                                {
                                    List<int> flags = allocateProgressFlags(t.ProgressFlag);
                                    foreach (int flag in flags)
                                    {
                                        reloc[EventValue.Flag(flag)] = EventValue.Flag(progressFlagCopies[flag][dupeIndex]);
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
                            if (t.Dupe != null)
                            {
                                if (t.Type == "locarg" && t.Dupe.Entity != null)
                                {
                                    // Rewrite initialization to add extra arguments
                                    // This is <source pos 1> <source pos 2> <target pos>, with helpers unedited
                                    // The resulting args look like <dupe 1a> <dupe 1b> <dupe 2a> <dupe 2b>,
                                    // with <dupe 1> <healthbar 1> <dupe 2> <dupe 3> etc. added when healthbars exist
                                    (int targetPos, List<int> sourcePoses) = getDupeEntityArgPositions(t.Dupe.Entity);
                                    if (init.Count != init.Offset + targetPos)
                                    {
                                        throw new Exception($"Expected {targetPos} arguments, found {init.Count} (offset {init.Offset}) in {init}, trying to add dupe entities");
                                    }
                                    for (int p = 0; p < sourcePoses.Count; p++)
                                    {
                                        int sourcePos = sourcePoses[p];
                                        int source = (int)init[init.Offset + sourcePos];
                                        if (!dupeEnemyMap.TryGetValue(source, out List<int> dupeArgs))
                                        {
                                            dupeArgs = new List<int>();
                                        }
                                        while (dupeArgs.Count < maxDupeCount)
                                        {
                                            dupeArgs.Add(source);
                                        }
                                        // Healthbar is currently only supported for first, for simplicity
                                        if (p == 0 && events.ParseArgSpec(t.Dupe.HealthBarArg, out int namePos))
                                        {
                                            int nameBase = (int)init[init.Offset + namePos];
                                            foreach (int dupe in dupeArgs.ToList())
                                            {
                                                dupeArgs.Add(GetCleverName(nameBase, source, dupe));
                                            }
                                        }
                                        init.AddArgs(dupeArgs.Select(x => (object)x));
                                    }
                                    init.Save(initOld);
                                }
                                if (t.Dupe.Generator != null)
                                {
                                    // Expand both dupeMap and dupeReloc with the given generators
                                    // These are used for rewrite-based and replace-based edits later on
                                    foreach (string genPart in t.Dupe.Generator.Split(' '))
                                    {
                                        int gen = int.Parse(genPart);
                                        if (dupeEnemyMap.ContainsKey(gen))
                                        {
                                            continue;
                                        }
                                        EventValue genValue = EventValue.Generator(gen);
                                        int dupeGen(int index)
                                        {
                                            (_, int result) = enemyEditor.MakeGeneratorCopy(
                                                maps, newEntity, entry.Key, entry.Key, gen, generators, dupeRelocs[index]);
                                            return result;
                                        }
                                        dupeEnemyMap[gen] = Enumerable.Range(0, maxDupeCount).Select(dupeGen).ToList();
                                        for (int c = 0; c < maxDupeCount; c++)
                                        {
                                            dupeRelocs[c][genValue] = EventValue.Generator(dupeEnemyMap[gen][c]);
                                        }
                                    }
                                }
                            }

                            // Commit edits to e2
                            int totalEditCount = edits.PendingEdits.Count;
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
                                        if (startFlag <= 0) throw new Exception($"{targetInfo.OwnedBy} has no start flag");
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
                                        Instr instr = events.Parse(e2.Instructions[j], pre);
                                        if (instr.Init) continue;
                                        // We used to use EzState Instruction Request for 10000, but these commands are now notated with the Deathblow field, so look for the condition group instead
                                        // TODO: check isshin still working.
                                        if ((instr.Name == "IF Condition Group" || instr.Name.StartsWith("IF Character Has Event Message")) && instr[0].ToString() == "0")
                                        {
                                            found = true;
                                            instr[0] = "-14";
                                            instr.Save(pre);
                                            e2.Instructions.InsertRange(j + 1, toAdd);
                                            break;
                                        }
                                    }
                                    if (!found) throw new Exception($"Could not find condition group to apply deathblow rewrite in {callee}");
                                }

                                // Rewrite condition groups for non-vanilla. Blanket exclude animation events since they dynamically edit conditions.
                                Dictionary<int, int> condRewrite = null;
                                if (t.Animation == null)
                                {
                                    bool adjustGroups = game.HasMods;
#if DEBUG
                                    adjustGroups = true;
#endif
                                    if (adjustGroups)
                                    {
                                        // This may set PreprocessError which will be shown later if edits fail.
                                        condRewrite = events.AdjustConditionGroups(edits, e2, pre, t.CondOrder, !game.HasMods, callee.ID);
                                    }
                                }
                                // Apply standard line-by-line edits, line-by-line. Plus a few misc custom ones
                                for (int j = 0; j < e2.Instructions.Count; j++)
                                {
                                    Instr instr = events.Parse(e2.Instructions[j], pre);
                                    // Randomized events shouldn't have initializations, although we could probably also ignore them
                                    // if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e2.ID}");
                                    // We are either dealing with a copy of the event or the original one. So all edits are in-place
                                    events.ApplyEdits(edits, instr, j);
                                    instr.Save(pre);
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
                                            instr.Save(pre);
                                            e2.Instructions[j] = instr.Val;
                                        }
                                        // A different healthbar change, for dupe events. These are assumed to be the chr entity.
                                        if (t.Dupe?.HealthBar != null && entity != 0 && (instr.Name == "Display Boss Health Bar" || instr.Name == "Display Miniboss Health Bar"))
                                        {
                                            instr[2] = (short)1;
                                            instr[3] = GetCleverName((int)instr[3], entity, target);
                                            instr.Save(pre);
                                            e2.Instructions[j] = instr.Val;
                                        }
                                    }
                                    else if (game.DS3)
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
                                                instr.Save(pre);
                                                e2.Instructions[j] = instr.Val;
                                            }
                                        }
                                    }
                                    else if (game.EldenRing)
                                    {
                                        // Note: Unlike past games, args can be param strings, so be careful
                                        // Replace area checks with distance checks
                                        if (entity != 0 && instr.Name == "IfInoutsideArea"
                                            && instr[3] is uint areaId
                                            && distReplace.TryGetValue((int)areaId, out int dist))
                                        {
                                            sbyte condGroup = (sbyte)instr[0];
                                            byte isInside = (byte)instr[1];
                                            EMEVD.Instruction toAdd = events.ParseAdd($"IfEntityInoutsideRadiusOfEntity({condGroup}, {(dist > 0 ? isInside : 1 - isInside)}, 10000, {entity}, {(float)Math.Abs(dist)}, 1)");
                                            e2.Instructions[j] = toAdd;
                                        }
                                        // For dupe, we don't care about if it's enable or disable, and both loc/chr are fine
                                        if (t.Name != null && instr.Name == "DisplayBossHealthBar" && !t.Name.StartsWith("X"))
                                        {
                                            // Console.WriteLine($"{instr} {instr[1]} {instr[1].GetType()}");
                                            foreach (string nameStr in t.Name.Split(' '))
                                            {
                                                int nameEntity = (int)(uint)instr[1];
                                                int nameSource = int.Parse(nameStr);
                                                if (nameEntity == nameSource)
                                                {
                                                    int nameSlot = (short)instr[2];
                                                    // This is allowed in chr event or locstay event
                                                    int nameTarget;
                                                    int nameDupeIndex = -1;
                                                    if (target > 0)
                                                    {
                                                        nameTarget = getBaseTarget(target, out nameDupeIndex);
                                                    }
                                                    else if (t.Type.Contains("stay"))
                                                    {
                                                        // Resolve helpers here
                                                        nameTarget = infos.TryGetValue(nameSource, out EnemyInfo nameInfo) && nameInfo.OwnedBy > 0
                                                            ? nameInfo.OwnedBy : nameSource;
                                                    }
                                                    else throw new Exception($"{callee} {t.Type} can't be used for names");
                                                    int newNameSlot = getHealthbarIndex(nameSource, nameTarget, nameDupeIndex);
                                                    if (dupeEnabled(nameTarget) && (sbyte)instr[0] == 0)
                                                    {
                                                        // Attempt to automatically fix incorrect healthbar offsets from getting disabled
                                                        newNameSlot = -1;
                                                    }
                                                    // if (nameSlot != newNameSlot) Console.WriteLine($"Changing {callee} {t.Type} {nameSource}->{nameTarget}[{nameDupeIndex}]: {nameSlot}->{newNameSlot}");
                                                    if (infos[nameTarget].IsImportantTarget && newNameSlot != -1)
                                                    {
                                                        instr[2] = (short)newNameSlot;
                                                        instr[3] = GetCleverName((int)instr[3], nameSource, nameTarget, true);
                                                        instr.Save(pre);
                                                        e2.Instructions[j] = instr.Val;
                                                    }
                                                    else
                                                    {
                                                        e2.Instructions[j] = new EMEVD.Instruction(1014, 69);
                                                    }
                                                }
                                            }
                                        }
                                        if (t.EffectFlag != null)
                                        {
                                            bool getSp(object flagArg, out int spEntity, out int sp)
                                            {
                                                spEntity = 0;
                                                sp = 0;
                                                int pos = -1;
                                                int flag;
                                                if (flagArg is string arg && events.ParseArgSpec(arg, out pos))
                                                {
                                                    flag = (int)originalInit[originalInit.Offset + pos];
                                                }
                                                else if (flagArg is uint uval)
                                                {
                                                    flag = (int)uval;
                                                }
                                                else if (flagArg is int val)
                                                {
                                                    flag = val;
                                                }
                                                else throw new Exception($"Unrecognized flag type in {instr} arg {flagArg}");
                                                // This should maybe be preprocessed earlier than this
                                                foreach (string spec in phraseRe.Split(t.EffectFlag))
                                                {
                                                    // <index> <entity> <flag> [<arg>]
                                                    // arg can be ignored for now, since a flag's sp should ideally
                                                    // not depend on where it shows up.
                                                    string[] parts = spec.Split(' ');
                                                    int specFlag = int.Parse(parts[2]);
                                                    if (flag != specFlag) continue;
                                                    sp = 6950 + int.Parse(parts[0]);
                                                    spEntity = int.Parse(parts[1]);
                                                    return true;
                                                }
                                                return false;
                                            }
                                            // Keep this emedf-lite for now. No parameters need to be output, at least.
                                            // Commands:
                                            // IfEventFlag 3[00]:
                                            // sbyte control, byte onoff, byte 0, uint flag
                                            // IfCharacterHasSpEffect 4[05]:
                                            // sbyte control, uint entity, int sp, byte onoff, byte 0, float 1
                                            // SkipIfEventFlag 1003[01], EndIfEventFlag 1003[02], GotoIfEventFlag 1003[101]:
                                            // byte control, byte onoff, byte 0, uint flag
                                            // SkipEndGotoIfCharacterHasSpeffect 1004[00] 1004[02] 1004[01]
                                            // byte control, uint entity, int sp, byte onoff, byte 0, float 1
                                            // SetEventFlag 2003[66], SetNetworkconnectedEventFlag 2003[69]:
                                            // byte 0, uint flag, byte onoff
                                            // SetSpEffect 2004[08]: uint entity, int sp
                                            // ClearSpEffect 2004[21]: uint entity, int sp
                                            if (instr.Name == "IfEventFlag")
                                            {
                                                // Ignore EventTargetType for now
                                                if (getSp(instr[3], out int spEntity, out int sp))
                                                {
                                                    e2.Instructions[j] = new EMEVD.Instruction(4, 5,
                                                        new List<object> { instr[0], spEntity, sp, instr[1], (byte)0, 1f });
                                                }
                                            }
                                            else if (instr.Name != null && flagToSpeffectControl.TryGetValue(instr.Name, out int spIndex))
                                            {
                                                if (getSp(instr[3], out int spEntity, out int sp))
                                                {
                                                    e2.Instructions[j] = new EMEVD.Instruction(1004, spIndex,
                                                        new List<object> { instr[0], spEntity, sp, instr[1], (byte)0, 1 });
                                                }
                                            }
                                            else if (instr.Val.Bank == 2003 && (instr.Val.ID == 66 || instr.Val.ID == 69))
                                            {
                                                if (getSp(instr[1], out int spEntity, out int sp))
                                                {
                                                    string onoff = instr[2].ToString();
                                                    spIndex = (onoff == "0" || onoff == "OFF") ? 21 : 8;
                                                    e2.Instructions[j] = new EMEVD.Instruction(2004, spIndex,
                                                        new List<object> { spEntity, sp });
                                                }
                                            }
                                        }
                                    }
                                }
                                // Apply multi-line edits
                                events.ApplyAdds(edits, e2, pre);

                                // Dupe rewrite of event itself is handled by postprocessing
                                if (entity == 0 && t.Dupe != null && t.Dupe.Type == "rewrite")
                                {
                                    // Searches can be scoped to specific entities
                                    bool restrict = false;
                                    // Note that we actually *don't* want to use general dupeReloc here, which can be
                                    // populated with duplicated regions/etc.
                                    // We only care about enemies and generators.
                                    Dictionary<object, List<object>> searches = new Dictionary<object, List<object>>();
                                    if (t.Dupe.Entity != null)
                                    {
                                        restrict = true;
                                        if (int.TryParse(t.Dupe.Entity, out int dupeSource))
                                        {
                                            if (dupeEnemyMap.TryGetValue(dupeSource, out List<int> dupes))
                                            {
                                                searches[dupeSource] = dupes.Select(x => (object)x).ToList();
                                            }
                                        }
                                        else
                                        {
                                            (int targetPos, List<int> sourcePoses) = getDupeEntityArgPositions(t.Dupe.Entity);
                                            int offset = targetPos;
                                            for (int p = 0; p < sourcePoses.Count; p++)
                                            {
                                                int sourcePos = sourcePoses[p];
                                                searches[$"X{sourcePos * 4}_4"] = Enumerable.Range(0, maxDupeCount)
                                                    .Select(x => (object)$"X{(offset + x) * 4}_4")
                                                    .ToList();
                                                offset += maxDupeCount;
                                                if (t.Dupe.HealthBar != null && events.ParseArgSpec(t.Dupe.HealthBar.Split(' ')[1], out int healthPos))
                                                {
                                                    searches[$"X{healthPos * 4}_4"] = Enumerable.Range(0, maxDupeCount)
                                                        .Select(x => (object)$"X{(offset + x) * 4}_4")
                                                        .ToList();
                                                    offset += maxDupeCount;
                                                }
                                            }
                                        }
                                    }
                                    // Condition replacements for AND/OR customization
                                    // For condition x, turn dupe uses into y, combine x and y into -z, and use -z at the end.
                                    // Map from line number to condition group redefinition
                                    Dictionary<int, int> rewriteDef = new Dictionary<int, int>();
                                    // Map from line number to condition group combination (x + y -> z)
                                    Dictionary<int, (int, int, int)> rewriteUse = new Dictionary<int, (int, int, int)>();
                                    // Map from line number to MAIN group evaluation, where a condition group must be added
                                    Dictionary<int, int> rewriteMain = new Dictionary<int, int>();
                                    if (t.Dupe.Condition != null)
                                    {
                                        int newCond = 11;
                                        foreach (string condStr in phraseRe.Split(t.Dupe.Condition))
                                        {
                                            // For the moment, condStr is a single value, not the other condition groups
                                            // (if needed, newCond can be manually defined)
                                            if (int.TryParse(condStr, out int cond))
                                            {
                                                if (condRewrite != null && condRewrite.TryGetValue(cond, out int actualCond))
                                                {
                                                    cond = actualCond;
                                                }
                                                int condType = Math.Sign(cond);
                                                List<int> lines = events.FindCond(e2, cond.ToString(), pre);
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
                                            else
                                            {
                                                // Otherwise, a main group evaluation, with name followed by sign
                                                string[] parts = condStr.Split(' ');
                                                int condType = parts[1] == "and" ? 1 : -1;
                                                List<int> lines = events.FindCond(e2, parts[0], pre);
                                                foreach (int line in lines)
                                                {
                                                    rewriteMain[line] = newCond * condType;
                                                }
                                                newCond++;
                                            }
                                        }
                                    }

                                    bool printSkips = false;
                                    List<int> addedLines = new List<int>();
                                    string condCmd = game.Sekiro ? "IF Condition Group" : "IfConditionGroup";
                                    for (int j = e2.Instructions.Count - 1; j >= 0; j--)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j], pre);
                                        // Already done in Elden Ring, but should be fine to do again?
                                        events.SetInstrParamArgs(instr, pre);
                                        // Map from arg index to different copies to use
                                        Dictionary<int, List<object>> replaces = new Dictionary<int, List<object>>();
                                        for (int k = 0; k < instr.Count; k++)
                                        {
                                            if (!events.IsArgCompatible(instr.Doc, k, EventValueType.Entity)) continue;
                                            object val = instr[k];
                                            if (searches.TryGetValue(val, out List<object> vals))
                                            {
                                                replaces[k] = vals;
                                            }
                                            else if (!restrict)
                                            {
                                                int entityVal = 0;
                                                if (val is int ival) entityVal = ival;
                                                else if (val is uint uval) entityVal = (int)uval;
                                                if (entityVal > 0 && dupeEnemyMap.TryGetValue(entityVal, out List<int> dupes))
                                                {
                                                    replaces[k] = dupes.Select(x => (object)x).ToList();
                                                }
                                            }
                                        }
                                        int instrCount = e2.Instructions.Count;
                                        // rewriteDef.ContainsKey(j) - has no effect if no dupes, though (the main cond is left alone)
                                        if (replaces.Count > 0 || rewriteMain.ContainsKey(j))
                                        {
                                            if (rewriteMain.TryGetValue(j, out int mainCond))
                                            {
                                                instr[0] = (sbyte)mainCond;
                                                instr.Save(pre);
                                                e2.Instructions[j] = instr.Val;
                                                // Add this after the original instruction
                                                e2.Instructions.Insert(j + 1, events.ParseAdd($"{condCmd} (0,1,{mainCond})"));
                                            }
                                            int biggestReplace = replaces.Count == 0 ? 0 : replaces.Select(r => r.Value.Count).Max();
                                            for (int c = 0; c < biggestReplace; c++)
                                            {
                                                // Automatic feature in boss fights: add slight delay between activations
                                                if (t.Dupe.DelayAnimation > 0 && instr.Name == "Force Animation Playback")
                                                {
                                                    // Non-looping for entity in question
                                                    if ((int)instr[0] == t.Dupe.DelayAnimation && (byte)instr[2] == 0)
                                                    {
                                                        e2.Instructions.Insert(j, events.ParseAdd($"WAIT Fixed Time (Seconds) (0.5)"));
                                                    }
                                                }
                                                foreach (KeyValuePair<int, List<object>> replace in replaces)
                                                {
                                                    // Doubling these is hopefully fine
                                                    object val = c < replace.Value.Count ? replace.Value[c] : replace.Value[0];
                                                    instr[replace.Key] = val;
                                                }
                                                // We're adding these in reverse order, so... hacky skip-handling.
                                                // This seems to be used in 20005340 for falling off, so AND is appropriate
                                                // TODO dupe: define all skipifs
                                                if (instr.Name.StartsWith("SKIP IF") || instr.Name.StartsWith("SkipIf"))
                                                {
                                                    instr[0] = (byte)instr[0] + 1;
                                                    if (printSkips) Console.WriteLine(instr.Name);
                                                }
                                                // This is only used for Sekiro, as segments handle this otherwise
                                                if (instr.Name == "Display Boss Health Bar" || instr.Name == "Display Miniboss Health Bar")
                                                {
                                                    // TODO: Watch out for bars being above some limit
                                                    instr[2] = (short)(c + 1);
                                                    // Console.WriteLine($"{instr} from {string.Join(", ", replaces.Select(r => $"{r.Key}={string.Join(",", r.Value)}"))}");
                                                    if (instr[1] is int nameEntity)
                                                    {
                                                        // In-place name editing with fixed entity
                                                    }
                                                    else if (!(instr[3] is int))
                                                    {
                                                        // This is also fine, HealthBarArg should handle variable names
                                                        // Elden Ring bad argument (but also we need emedf anyway)
                                                        nameEntity = -1;
                                                    }
                                                    else if (t.Dupe.HealthBar != null && int.TryParse(t.Dupe.HealthBar.Split(' ')[0], out int sourceNameEntity))
                                                    {
                                                        // With fixed name, entity should be provided by the config
                                                        nameEntity = dupeEnemyMap[sourceNameEntity][c];
                                                        // Console.WriteLine($"--------- your name is {nameEntity} for {t.Dupe.HealthBar}, with {revMapping[nameEntity]}");
                                                    }
                                                    else throw new Exception($"Not enough information to add health bar in {callee}: {instr}");
                                                    // Can also use getRandomizedSource here, but we've effectively already done the map lookup
                                                    if (nameEntity > 0 && revMapping.TryGetValue(nameEntity, out int sourceEntity))
                                                    {
                                                        // Console.WriteLine($"entity name {sourceEntity}->{nameEntity}");
                                                        instr[3] = GetCleverName((int)instr[3], sourceEntity, nameEntity);
                                                    }
                                                }
                                                if (rewriteDef.TryGetValue(j, out int newCond))
                                                {
                                                    instr[0] = (sbyte)newCond;
                                                }
                                                else if (rewriteMain.TryGetValue(j, out int mainCond2))
                                                {
                                                    instr[0] = (sbyte)mainCond2;
                                                }
                                                // Console.WriteLine(instr);
                                                (EMEVD.Instruction ins, List<EMEVD.Parameter> ps) = events.ParseAddArg(instr.ToString());
                                                e2.Instructions.Insert(j, ins);
                                                pre.AddParameters(ins, ps);
                                            }
                                        }
                                        else
                                        {
                                            if (instr.Name != null && (instr.Name.StartsWith("SKIP IF") || instr.Name.StartsWith("SkipIf")))
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
                                                    if (printSkips) Console.WriteLine(instr.Name);
                                                }
                                            }
                                            else if (rewriteUse.TryGetValue(j, out var val))
                                            {
                                                (int x, int y, int z) = val;
                                                instr[2] = (sbyte)z;
                                                instr.Save(pre);
                                                e2.Instructions[j] = instr.Val;
                                                e2.Instructions.Insert(j, events.ParseAdd($"{condCmd} ({z},1,{y})"));
                                                e2.Instructions.Insert(j, events.ParseAdd($"{condCmd} ({z},1,{x})"));
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
                                            Instr instr = events.Parse(events.CopyInstruction(cmd), pre);
                                            events.RewriteInts(instr, bossReplace);
                                            instr.Save(pre);
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
                                        Instr instr = events.Parse(e2.Instructions[before.Last()], pre);
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
                            // if (totalEditCount > 0) Console.WriteLine($"{callee}: applied edits {totalEditCount} -> {edits.PendingEdits.Count}");
                            if (edits.PendingEdits.Count != 0)
                            {
                                eventEditErrors.Add($"-- Unapplied edits in {originalInit} in {entry.Key}");
                                if (t.Entity > 0)
                                {
                                    eventEditErrors.Add($"It was processed because {enameClass(t.Entity)} was randomized");
                                }
                                else if (t.Entities != null)
                                {
                                    eventEditErrors.Add($"It was processed because one of these enemies was randomized: {string.Join(", ", t.Entities.Split(' ').Select(int.Parse).Select(enameClass))}");
                                }
                                if (edits.PreprocessError != null)
                                {
                                    eventEditErrors.Add(edits.PreprocessError);
                                }
                                eventEditErrors.Add($"Unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                                eventEditErrors.Add("");
                            }
                            bool useReplaceDupe = t.Dupe?.Type == "replace" && ev.DupeIndex >= 0;
                            if (reloc.Count > 0 || useReplaceDupe)
                            {
                                Dictionary<EventValue, EventValue> autoDupeReloc = dupeReloc;
                                if (autoDupeReloc == null && useReplaceDupe)
                                {
                                    autoDupeReloc = dupeRelocs[ev.DupeIndex];
                                }
                                // if (callee.ID == 1051572822) Console.WriteLine($"{callee} init1: {init}");
                                if (t.ArgEntities != null || t.ArgFlags != null || !game.EldenRing)
                                {
                                    Dictionary<int, EventValueType> initTypes = null;
                                    if (game.EldenRing && (t.ArgEntities != null || t.ArgFlags != null))
                                    {
                                        initTypes = new Dictionary<int, EventValueType>();
                                        void addArgTypes(string spec, EventValueType type)
                                        {
                                            if (spec == null) return;
                                            foreach (string arg in spec.Split(' '))
                                            {
                                                if (!events.ParseArgSpec(arg, out int argPos)) throw new Exception($"{callee} {spec}");
                                                initTypes[argPos] = type;
                                            }
                                        }
                                        addArgTypes(t.ArgEntities, EventValueType.Entity);
                                        addArgTypes(t.ArgFlags, EventValueType.Flag);
                                    }
                                    if (reloc.Count > 0) events.RewriteInitInts(init, reloc, initTypes);
                                    if (autoDupeReloc != null) events.RewriteInitInts(init, autoDupeReloc, initTypes);
                                }
                                init.Save(initOld);
                                if (e2 != null)
                                {
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j], pre);
                                        // if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e.ID}");
                                        if (reloc.Count > 0) events.RewriteInts(instr, reloc);
                                        if (autoDupeReloc != null) events.RewriteInts(instr, autoDupeReloc);
                                        instr.Save(pre);
                                        // if (callee == 12505926) Console.WriteLine($"rewrite: {instr}");
                                    }
                                }
                            }
                            if (e2 != null && e2.ID != callee.ID)
                            {
                                // New event, so add to its target map and also add new initialization
                                string ownMap;
                                if (t.Type == "copy" || t.Type == "segmentcopy")
                                {
                                    ownMap = entry.Key;
                                }
                                else if (!ownerMap.TryGetValue(target, out ownMap))
                                {
                                    throw new Exception($"Can't add initialization for {entity}->{target}, it has no owner map");
                                }
                                // if (e2.ID == 1700548) Console.WriteLine($"Processing {entity}->{target} {t.Type}. e2 {e2?.ID}. adding to {ownMap}");
                                AddMulti(newInitializations, ownMap, (init.Val, e2));
                            }
                        }  // For each template

                        if (canRemove || forceRemove)
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
            if (eventEditErrors.Count > 0)
            {
                foreach (string err in eventEditErrors)
                {
                    Console.WriteLine(err);
                }
                throw new Exception($"Failed to edit event scripts in enemy randomizer, likely because merged mod is incompatible. See spoiler logs for details.");
            }

            // Add common functions
            Dictionary<string, NewEvent> newEvents = new Dictionary<string, NewEvent>();
            foreach (NewEvent e in eventConfig.NewEvents ?? new List<NewEvent>())
            {
                if (e.Commands == null)
                {
                    EMEVD.Event common = game.Emevds["common_func"].Events.Find(c => c.ID == c.ID);
                    if (common == null) throw new Exception($"Error: event {e.Name} #{e.ID} missing from common_func");
                    newEvents[e.Name] = e;
                    continue;
                }
                List<EMEVD.Parameter> ps = new List<EMEVD.Parameter>();
                EMEVD.Event ev = new EMEVD.Event(e.ID, EMEVD.Event.RestBehaviorType.Default);
                List<string> commands = events.Decomment(e.Commands);
                for (int i = 0; i < commands.Count; i++)
                {
                    (EMEVD.Instruction instr, List<EMEVD.Parameter> newPs) = events.ParseAddArg(commands[i], i);
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
                    newEvents[e.Name] = e;
                    AddMulti(newInitializations, "common_func", ((EMEVD.Instruction)null, ev));
                }
            }

            void addCommonFuncInit(string name, int target, List<object> args)
            {
                List<object> startArgs = new List<object>();
                if (game.EldenRing) startArgs.Add(0);
                startArgs.Add(newEvents[name].ID);
                EMEVD.Instruction init = new EMEVD.Instruction(2000, 6, startArgs.Concat(args));
                AddMulti(newInitializations, ownerMap[target], (init, (EMEVD.Event)null));
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
                }
                if (game.DS3 || game.EldenRing)
                {
                    if (infos.TryGetValue(target, out EnemyInfo musicInfo)
                        && musicInfo.MusicFlag > 0 && !usedMusicFlagTargets.Contains(target))
                    {
                        addCommonFuncInit("musicflag", target, new List<object> { target, musicInfo.MusicFlag });
                    }
                }
                if (game.EldenRing)
                {
                    // Currently, it's assumed this won't apply to swappable encounters
                    // (and other various cases sourceInfo is used)
                    // All swappable are also expected to be multi and mainhelper
                    if (sourceInfo.HasTag("sentry"))
                    {
                        addCommonFuncInit("sentry", target, new List<object> { target });
                    }
                }
                int baseTarget = target;
                int enemySource = source;
                if (!infos.ContainsKey(target) && sourceInfo.OwnedBy > 0)
                {
                    // Helper mapping is from owner target and source helper to target helper
                    // TODO... can this be faster
                    int ownerTarget = helperMapping.Where(e => e.Key.Item2 == source && e.Value == target).Select(e => e.Key.Item1).FirstOrDefault();
                    if (ownerTarget > 0)
                    {
                        baseTarget = ownerTarget;
                        if (swapMapping.TryGetValue((baseTarget, source), out int swapSource))
                        {
                            // Console.WriteLine($"{source} has {enemySource} original source, {swapSource} swap source");
                            enemySource = swapSource;
                        }
                    }
                }
                EnemyInfo targetInfo = infos[baseTarget];
                if (opt["scale"])
                {
                    // Assign scaling speffects on best-effort basis
                    // Console.WriteLine($"{source} -> {target}. base {baseTarget}, owner {sourceInfo.OwnedBy}");
                    // if (sourceInfo.OwnedBy > 0) Console.WriteLine($"  entries: {string.Join(" ", helperMapping.Where(e => e.Key.Item2 == source && e.Value == target))}");
                    string scaleEvent = "scale";
                    if (game.EldenRing &&
                        (sourceInfo.Class == EnemyClass.Helper || sourceInfo.IsBossTarget || targetInfo.HasTag("generated")))
                    {
                        scaleEvent = "scale2";
                    }
                    getScalingSections(enemySource, baseTarget, out int sourceSection, out int targetSection);
                    // int sourceSection = ann.ScalingSections.TryGetValue(enemySource, out int s) ? s : -1;
                    // int targetSection = targetScalingSections.TryGetValue(baseTarget, out s) ? s : -1;
                    bool fixedXp = targetInfo.IsFixedSource;
                    if (sourceSection > 0 && targetSection > 0
                        && scalingSpEffects.Areas.TryGetValue((sourceSection, targetSection), out AreaScalingValue sp))
                    {
                        // Console.WriteLine($"{ename(source)}->{target}: fixed {fixedXp}");
                        // if (!sourceInfo.HasTag("noscale")
                        //    && !(sourceInfo.HasTag("noscaleup") && targetSection > sourceSection))
                        {
                            int scaleSp = fixedXp ? (sourceInfo.HasTag("nonunique") ? sp.FixedScaling : sp.UniqueFixedScaling) : sp.RegularScaling;
                            addCommonFuncInit(scaleEvent, target, new List<object> { target, scaleSp });
                        }
                        // Additional scaling for Gundyr and First Lizard, because no upgrades of any kind
                        if (targetInfo.HasTag("earlyscale") && !sourceInfo.HasTag("early"))
                        {
                            addCommonFuncInit(scaleEvent, target, new List<object> { target, scalingSpEffects.TutorialScaling });
                        }
                    }
                    else if (sourceSection > 0 && sourceSection == targetSection)
                    {
                        // This is fine: we don't expect to find the speffect when the sections are the same,
                        // or if the source explicitly has section 99 (do not scale).
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine($"Warning: scaling speffect not found for {ename(source)} in {target}, sections {sourceSection}->{targetSection}");
#endif
                    }
                    // Additional downscaling for boss-as-basic, if the option is enabled
                    // Don't scale down simple enemy helpers, so only allow non-helper (boss) or boss-equivalent helpers
                    if (presetOpt("regularhp") && !fixedXp && bossAsBasicNpc.ContainsKey(source)
                        && (sourceInfo.Class != EnemyClass.Helper || sourceInfo.HasTag("mainhelper")))
                    {
                        addCommonFuncInit(scaleEvent, target, new List<object> { target, scalingSpEffects.BossAsBasicScaling });
                    }
                    // More scaling for boss phases, anytime any phase enemy is rewritten
                    // Exclude non-main helpers; allow main helpers and everyone else.
                    // This is mainly when non-phase -> phase, e.g. Niall helpers to Rennala, don't scale
                    if (opt["phasehp"] && (sourceInfo.Class != EnemyClass.Helper || sourceInfo.HasTag("mainhelper")))
                    {
                        bool phaseSource = sourceInfo.HasTag("scalephase");
                        bool phaseTarget = targetInfo.HasTag("scalephase");
                        // Scale up phase source, like Fire Giant 1 -> Gideon.
                        // Just exclude e.g. Rykard -> Gideon, who shouldn't be scaled up ever.
                        if (phaseSource && !phaseTarget && !sourceInfo.HasTag("noscaleup"))
                        {
                            // Console.WriteLine($"Scale up {ename(source)}->{ename(baseTarget)}");
                            addCommonFuncInit(scaleEvent, target, new List<object> { target, scalingSpEffects.PhaseUpScaling });
                        }
                        // Scale down phase target, like Placidusax -> Hoarah Loux.
                        // Exclude Mimic Tear etc., who should never be scaled
                        else if (!phaseSource && phaseTarget && !sourceInfo.HasTag("noscale"))
                        {
                            // Console.WriteLine($"Scale down {ename(source)}->{ename(baseTarget)}");
                            addCommonFuncInit(scaleEvent, target, new List<object> { target, scalingSpEffects.PhaseDownScaling });
                        }
                    }
                }
                if (defaultData.TryGetValue(baseTarget, out EnemyData val))
                {
                    targetSourceNPCs[target] = val.NPC;
                }
#if DEBUG
                if (opt["idtest"])
                {
                    if (infos.TryGetValue(target, out EnemyInfo info) &&
                        (info.Class == EnemyClass.Basic || info.Class == EnemyClass.Wildlife || info.Class == EnemyClass.Scarab))
                    {
                        addCommonFuncInit("test", target, new List<object> { target });
                    }
                }
#endif
            }
            if (importantDupes.Count > 0 && presetOpt("multcolor"))
            {
                // For any important target, see if there are dupe sources which may conflict.
                // If so, color-code all of the possibly conflicting ones, and any of their helpers.
                foreach (int baseTarget in importantDupes)
                {
                    int count = dupeCount(baseTarget);
                    if (count == 0) continue;
                    List<string> models = new List<string>();
                    List<int> targets = new List<int>();
                    for (int dupeIndex = -1; dupeIndex < count; dupeIndex++)
                    {
                        getRandomizedSource(baseTarget, dupeIndex, out int source, out int realTarget);
                        string model = defaultData.TryGetValue(source, out EnemyData data) ? data.Model : null;
                        models.Add(model);
                        targets.Add(realTarget);
                    }
                    for (int i = 0; i < Math.Min(targets.Count, dupeBossSpEffects.Count); i++)
                    {
                        if (models[i] == null || models.Count(m => m == models[i]) <= 1) continue;
                        int target = targets[i];
                        addCommonFuncInit("scale2", target, new List<object> { target, dupeBossSpEffects[i] });
                        foreach (int helperTarget in getHelperTargets(target))
                        {
                            addCommonFuncInit("scale2", helperTarget, new List<object> { helperTarget, dupeBossSpEffects[i] });
                        }
                    }
                }
            }
            foreach (EnemyData data in defaultData.Values)
            {
                // Also add self-mapping here, so that NPC attributes can be added back for enemies where it was removed
                // TODO dupe: also add dupes here? mainly for teamtype
                if (!revMapping.ContainsKey(data.ID) && infos.ContainsKey(data.ID))
                {
                    targetSourceNPCs[data.ID] = data.NPC;
                }
            }
            // NPC effects to move around, or re-apply to vanilla enemies
            // Infighting will generally use Enemy2
            byte[] acceptableDupeTeams = new byte[] { 24 };
            foreach (KeyValuePair<int, int> entry in targetSourceNPCs)
            {
                int target = entry.Key;
                int npc = entry.Value;
                EnemyInfo sourceInfo = revMapping.TryGetValue(target, out int source) ? infos[source] : infos[target];
                infos.TryGetValue(target, out EnemyInfo targetInfo);
                bool generated = targetInfo?.HasTag("generated") ?? false;
                // The target info here comes from baseTarget for randomized enemies, so it should work for randomized helpers
                bool noSwamp = targetInfo?.HasTag("noswamp") ?? false;
                bool manualTeam = targetInfo?.HasTag("manualteam") ?? false;
                // Scale is just speffect application, so it also works for e.g. poison resistance
                if (npcCopySpEffect.TryGetValue(npc, out List<int> sps) && !noSwamp)
                {
                    string scaleEvent = "scale";
                    if (game.EldenRing && (sourceInfo.Class == EnemyClass.Helper || sourceInfo.IsBossTarget || generated))
                    {
                        scaleEvent = "scale2";
                    }
                    // We could also be precious about this and skip speffects already defined in source NPC
                    foreach (int sp in sps)
                    {
                        addCommonFuncInit(scaleEvent, target, new List<object> { target, sp });
                    }
                }
                // Add infighting if the original enemy had it, or the original owner had it, and not overpowered
                // TODO: Dupe is a bit tricky, as dupeEnabled does not catch all cases (e.g. helpers? should use getBaseTarget?)
                if (npcOriginalTeam.TryGetValue(npc, out byte team) && !sourceInfo.HasTag("ultra") && !sourceInfo.HasTag("multi") && !manualTeam
                    && (!anyDupeEnabled || acceptableDupeTeams.Contains(team)))
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
                // Finally, easier scaling for enemy onslaught
                // Use a similar condition as boss-as-basic scaling, but applied to both source and target being fixed
                // Alternatively, we could change the conditions to apply to all boss targets, regardless of source classification
                if (opt["scale"] && presetOpt("multhp") && dupeEnabled(target)
                    && targetInfo != null && targetInfo.IsFixedSource
                    && bossAsBasicNpc.ContainsKey(sourceInfo.ID) && (sourceInfo.Class != EnemyClass.Helper || sourceInfo.HasTag("mainhelper")))
                {
                    int baseTarget = getBaseTarget(target, out _);
                    int baseCount = dupeCount(baseTarget);
                    addCommonFuncInit("scale2", target, new List<object> { target, scalingSpEffects.GetDupeScaling(baseCount) });
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

            // Reverse mapping for Elden Ring, where we may need to create new emevds
            foreach (string map in newInitializations.Keys)
            {
                if (game.Emevds.ContainsKey(map)) continue;
                Console.WriteLine($"Unknown event map target {map}, enemy scripting and scaling will not apply there");
            }
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                if (disableEvents) break;

                List<(EMEVD.Instruction, EMEVD.Event)> addEvents = newInitializations[entry.Key];
                EMEVD emevd = entry.Value;
                // Remove unused events.
                // For events with remove, these are removed always (based only on template definition)
                // For loc events, these are not removed (canRemove false, added to usedEvents)
                // For chr events with any nonrandomized usages, these are not removed (not in revMapping, canRemove false)
                // For chr events with multiple randomized usages in a file, they should be copied, so it's fine to remove the original (canRemove true)
                // For chr events in common, they are not copied, so should not be removed. So this common_func exclusion is necessary.
                if (entry.Key != "common_func")
                {
                    int eventCount = emevd.Events.Count;
                    emevd.Events.RemoveAll(e =>
                    {
                        EventKey key = new EventKey((int)e.ID, entry.Key);
                        return removedEvents.Contains(key) && !usedEvents.Contains(key);
                    });
                    if (emevd.Events.Count != eventCount) game.WriteEmevds.Add(entry.Key);
                }
                if (addEvents.Count > 0) game.WriteEmevds.Add(entry.Key);
                emevd.Events.AddRange(addEvents.Select(n => n.Item2).Where(e => e != null));
                // Always add inits to the first event. Some maps don't have primary constructors like m60_52_52_00,
                // others like m60_45_35_00 don't have constructors of any type whatsoever. Just add one in that case.
                if (addEvents.Count > 0)
                {
                    if (emevd.Events.Count == 0 || emevd.Events[0].ID != 0)
                    {
                        emevd.Events.Insert(0, new EMEVD.Event(0, EMEVD.Event.RestBehaviorType.Default));
                    }
                    foreach (EMEVD.Instruction newEvent in addEvents.Select(n => n.Item1).Where(i => i != null))
                    {
                        emevd.Events[0].Instructions.Add(newEvent);
                    }
                }

                // Some custom edits
                foreach (EMEVD.Event e in emevd.Events)
                {
                    EventKey key = new EventKey((int)e.ID, entry.Key);
                    if (!game.Sekiro) continue;
                    if (opt["openstart"] && entry.Key == "common" && e.ID == 0)
                    {
                        // Open Senpou door (set objact event flag)
                        emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2003, 2, new List<object> { 62000503, (byte)1 }));
                    }
                    bool foundDefeat = false;
                    bool foundSave = false;
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        foundDefeat |= ins.Bank == 2003 && ins.ID == 12;
                        foundSave |= ins.Bank == 2000 && ins.ID == 5;
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
                            else if (imm == 0 && templates.TryGetValue(key, out EventSpec spec) && spec.Template.Any(t => t.Type.StartsWith("start") && removeImmortality.Contains(t.Entity)))
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
                    if (!entry.Key.StartsWith("common") && foundDefeat && !foundSave)
                    {
                        // Quick hack for sitting-at-grace crashes in Elden Ring. Doesn't work for minibosses
                        e.Instructions.Add(new EMEVD.Instruction(2000, 5, new List<object> { 0 }));
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
                if (mapping.TryGetValue(2500800, out List<int> targets)
                    && targets.Any(t => !(infos.TryGetValue(t, out EnemyInfo info) && info.DupeFrom == 2500800)))
                {
                    Params["ThrowParam"][15200090]["Dist"].Value = (float)16;
                    Params["ThrowParam"][15200090]["UpperYrange"].Value = (float)20;
                    Params["ThrowParam"][15200090]["LowerYrange"].Value = (float)20;
                    // Plus remove Divine Dragon blowback behavior as it easily sends the player straight out of bounds, if dragon is moved anywhere
                    Params["Bullet"].Rows.RemoveAll(r => r.ID == 52000830);
                }
            }
            else if (game.DS3)
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
            else if (game.EldenRing)
            {
                if (isAnythingRandomized)
                {
                    // Weaken glowing statues
                    game.Params["AssetEnvironmentGeometryParam"][99635]["defense"].Value = (ushort)1;
                }
                // Strengthen caravans and caravan chests
                game.Params["AssetEnvironmentGeometryParam"][100100]["defense"].Value = (ushort)990;
                game.Params["AssetEnvironmentGeometryParam"][100100]["hp"].Value = (short)-1;
                game.Params["AssetEnvironmentGeometryParam"][100120]["defense"].Value = (ushort)990;
                game.Params["AssetEnvironmentGeometryParam"][100120]["hp"].Value = (short)-1;

                // Assume nerfrykard is true, if Rykard is randomized to anywhere else
                // Can make this an option
                if (isRandomizedSource(16000800) || isRandomizedSource(16000801))
                {
                    // Removing either 4710600 or 4710601 seems to work. The former has fire damage
                    Params["AtkParam_Npc"].Rows.RemoveAll(r => r.ID == 4710600);
                }
                // Progress speffects are added in MiscSetup instead

                // Look at deathline cases, probably just always
                // TODO: This doesn't work
                foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
                {
                    bool modified = false;
                    foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                    {
                        ESD esd = esdEntry.Value;
                        int esdId = int.Parse(esdEntry.Key.Substring(1));
                        if (!deathLineFlags.TryGetValue(esdId, out int defeatFlag)) continue;
                        if (!esd.StateGroups.TryGetValue(1103, out Dictionary<long, ESD.State> machine)) continue;
                        if (!machine.TryGetValue(0, out ESD.State state) || state.Conditions.Count != 1) continue;
                        ESD.Condition cond = state.Conditions[0];
                        if (cond.Evaluator == null || !(AST.DisassembleExpression(cond.Evaluator).TryAsInt(out int val) && val == 1)) continue;
                        // GetEventFlag f15
                        cond.Evaluator = AST.AssembleExpression(AST.MakeFunction("f15", defeatFlag));
                        modified = true;
                    }
                    if (modified)
                    {
                        game.WriteESDs.Add(entry.Key);
                    }
                }

                if (opt["swaprewards"])
                {
                    bool debugBonus = opt["debugbonus"];
                    Dictionary<int, uint> bossSouls = new Dictionary<int, uint>();
                    foreach (PARAM.Row row in Params["GameAreaParam"].Rows)
                    {
                        uint amt = (uint)row["bonusSoul_single"].Value;
                        if (amt > 0)
                        {
                            bossSouls[row.ID] = amt;
                        }
                    }
                    foreach (PARAM.Row row in Params["GameAreaParam"].Rows)
                    {
                        if (!infos.ContainsKey(row.ID) || !bossSouls.ContainsKey(row.ID)) continue;
                        List<int> targets = new List<int> { row.ID };
                        List<(int, int)> sourceMapping = new List<(int, int)>();
                        if (multiPhase.ContainsKey(row.ID))
                        {
                            targets.AddRange(multiPhase.Where(e => e.Key != row.ID && e.Value == row.ID).Select(e => e.Key));
                        }
                        // Just add up the bosses
                        foreach (int target in targets)
                        {
                            if (infos[target].DupeFrom > 0) continue;
                            int count = dupeCount(target);
                            for (int dupeIndex = -1; dupeIndex < count; dupeIndex++)
                            {
                                getRandomizedSource(target, dupeIndex, out int source, out int realTarget);
                                sourceMapping.Add((target, source));
                            }
                        }
                        int total = 0;
                        foreach ((int target, int source) in sourceMapping)
                        {
                            // Start with base soul amount.
                            // Modifiers to souls: scaling tiers, dupe amount (GetXpRate), phase HP
                            double baseAmt = 0;
                            int mainSource = multiPhase.TryGetValue(source, out int nextSource) ? nextSource : source;
                            // Some hardcoding for double-horse bosses. Should this try some kind of averaging...? bleh
                            if (mainSource == 1041510801)
                            {
                                mainSource = 1041510800;
                            }
                            else if (mainSource == 1248550801)
                            {
                                mainSource = 1248550800;
                            }
                            if (bossSouls.TryGetValue(mainSource, out uint bossAmt))
                            {
                                baseAmt = bossAmt;
                                // Could divvy up based on phase health etc, but start with this for now
                                if (multiPhase.ContainsKey(source)) baseAmt /= 2;
                            }
                            if (defaultData.TryGetValue(source, out EnemyData data) && baseAmt == 0)
                            {
                                // TODO: Find actual boss-as-basic HP scaling used here. For now, just use their soul amount directly.
                                PARAM.Row npc = Params["NpcParam"][data.NPC];
                                if (npc != null)
                                {
                                    baseAmt = (uint)npc["getSoul"].Value;
                                }
                            }
                            if (baseAmt < 1)
                            {
                                Console.WriteLine($"Note: no runes to move {ename(source)}->{ename(target)} (GameAreaParam {mainSource}, NpcParam {data?.NPC})");
                                continue;
                            }
                            double dupeMult = GetXpModifier(dupeCount(target), presetOpt("multhp"));
                            double dupeAmt = baseAmt * dupeMult;
                            double scaleAmt, scalePhaseAmt;
                            // As with all things scaling, much logic is duplicated in different places
                            if (opt["scale"]
                                && infos.TryGetValue(source, out EnemyInfo sourceInfo)
                                && infos.TryGetValue(target, out EnemyInfo targetInfo))
                            {
                                getScalingSections(source, target, out int sourceSection, out int targetSection, true);
                                double scaleMult = 1;
                                if (sourceSection > 0 && targetSection > 0)
                                {
                                    scaleMult = EldenSoulScaling[targetSection - 1] / EldenSoulScaling[sourceSection - 1];
                                }
                                scaleAmt = dupeAmt * scaleMult;
                                double phaseMult = 1;
                                if (opt["phasehp"])
                                {
                                    bool phaseSource = sourceInfo.HasTag("scalephase");
                                    bool phaseTarget = targetInfo.HasTag("scalephase");
                                    // Scale up phase source, like Fire Giant 1 -> Gideon. Use magic constants from ScalingEffects
                                    if (phaseSource && !phaseTarget)
                                    {
                                        phaseMult = 1.4;
                                    }
                                    // Scale down phase target, like Placidusax -> Hoarah Loux.
                                    else if (!phaseSource && phaseTarget)
                                    {
                                        phaseMult = 0.7;
                                    }
                                }
                                scalePhaseAmt = scaleAmt * phaseMult;
                            }
                            else
                            {
                                scaleAmt = scalePhaseAmt = dupeAmt;
                            }
                            if (debugBonus)
                            {
                                Console.WriteLine($"Target {ename(target)} <- {ename(source)}: {baseAmt} -> dupe {dupeAmt:0} -> scale {scaleAmt:0} -> final {roundBonusSoul((int)scalePhaseAmt)}, vs {bossSouls[row.ID]}");
                            }
                            total += (int)scalePhaseAmt;
                        }
                        uint val = (uint)roundBonusSoul(total);
                        row["bonusSoul_single"].Value = val;
                        row["bonusSoul_multi"].Value = val;
                        if (debugBonus && sourceMapping.Count > 1)
                        {
                            Console.WriteLine($"  Total: {val}");
                        }
                    }
                }

                // Invisible enemies hack. Take this from invisible enemies mod installer
                {
                    int newSp = 1450601;
                    PARAM.Row phRow = GameEditor.AddRow(Params["PhantomParam"], newSp, 260);
                    foreach (PARAM.Cell cell in phRow.Cells)
                    {
                        if (cell.Value is byte)
                        {
                            cell.Value = (byte)0;
                        }
                        if (cell.Value is float)
                        {
                            cell.Value = (float)0;
                        }
                    }
                    // 10101 completely empty
                    PARAM.Row spRow = GameEditor.AddRow(Params["SpEffectParam"], newSp, 13177);
                    spRow["vfxId"].Value = newSp;
                    PARAM.Row vfxRow = GameEditor.AddRow(Params["SpEffectVfxParam"], newSp, 0);
                    vfxRow["playCategory"].Value = (byte)3;
                    vfxRow["playPriority"].Value = (byte)255;
                    vfxRow["useCamouflage"].Value = (byte)1;
                    vfxRow["invisibleAtFriendCamouflage"].Value = (byte)1;
                    vfxRow["effectInvisibleAtCamouflage"].Value = (byte)1;
                    vfxRow["isHideFootEffect_forCamouflage"].Value = (byte)1;
                    vfxRow["phantomParamOverwriteId"].Value = newSp;
                    vfxRow["phantomParamOverwriteType"].Value = (byte)2;
                }
            }

#if DEBUG
            Console.WriteLine($"entity max {entityBase} event max {tmpBase} write max {writeBase} any {anyRandomized}");
#endif

            // Return item info
            EnemyLocations itemLocs = new EnemyLocations();

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
                        AddMulti(itemLocs.Target, sourceName, targetName);
                    }
                }
            }

            if (game.EldenRing && mapping.TryGetValue(11050850, out List<int> gideonTarget) && gideonTarget.Count == 1)
            {
                itemLocs.Outfit = infos[gideonTarget[0]].Outfit;
                if (opt["testoutfit"]) itemLocs.Outfit = string.Join("|", infos.Values.Where(i => i.Outfit != null).OrderBy(i => i.ID).Select(i => i.Outfit));
            }

            return itemLocs;
        }

        private static readonly Regex phraseRe = new Regex(@"\s*;\s*");
        private static readonly Regex condCmdRe = new Regex(@"^c\d_");

        public class BossPhaseLimit
        {
            public int Entity { get; set; }
            public float Amount { get; set; }
        }

        public class RegionTarget
        {
            public int Distance { get; set; }
            public int Region { get; set; }
            public int Generator { get; set; }
            public static RegionTarget ID(int id) => new RegionTarget { Region = id };
            public static RegionTarget Gen(int id) => new RegionTarget { Generator = id };
            public static RegionTarget Dist(int id) => new RegionTarget { Distance = id };
        }

        public class RandomSources
        {
            public static RandomSources Create(Random random, List<PoolAssignment> pools, List<int> defaultPool, int estimatedTargetCount)
            {
                if (pools == null || pools.Count == 0) return null;
                RandomSources ret = new RandomSources();
                foreach (PoolAssignment original in pools)
                {
                    PoolAssignment pool = original.Copy();
                    if (pool.Weight <= 0) continue;
                    if (pool.Pool == null || pool.Pool.ToLowerInvariant() == "default")
                    {
                        pool.PoolGroups = new List<List<int>> { defaultPool.ToList() };
                    }
                    else if (pool.DefaultCount > 0)
                    {
                        if (pool.PoolGroups == null)
                        {
                            pool.PoolGroups = new List<List<int>>();
                        }
                        for (int i = 0; i < pool.DefaultCount; i++)
                        {
                            pool.PoolGroups.Add(defaultPool.ToList());
                        }
                    }
                    if (pool.PoolGroups == null || pool.PoolGroups.Count == 0)
                    {
                        Console.WriteLine($"Ignoring pool group {pool.PoolGroups} {pool.PoolGroups?.Count}");
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
                        // Console.WriteLine($"Adding pool group of size {group.Count}");
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
                if (tries == maxTries) throw new Exception($"Can't select enemy: keep getting enemies like {ret} which can only appear once in the game");

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
            // Mapping from (target owner, source helper) to other source helper
            public Dictionary<(int, int), int> SwapMapping = new Dictionary<(int, int), int>();
        }
    }
}
