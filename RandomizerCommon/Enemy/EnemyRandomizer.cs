using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.GameData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.EnemyPermutation;
using static RandomizerCommon.EnemyPreset;
using static RandomizerCommon.Util;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class EnemyRandomizer
    {
        private GameData game;
        private Events events;
        private EventConfig eventConfig;
        private MergeModManifest merge;

        public EnemyRandomizer(GameData game, Events events, EventConfig eventConfig, MergeModManifest merge = null)
        {
            this.game = game;
            this.events = events;
            this.eventConfig = eventConfig;
            this.merge = merge;
        }

        private int tmpBase = 11315000;
        private int tmpBaseMax = 11496000;
        private int tmpJump = 11515000;
        private int tmpMax = 11696000;
        private int writeBase = 11305750;
        private int writeMax = 11306000;

        private void AllocateWritableEventIDs(int amount)
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

        // These values are currently both int and uint range, but they are only used in uint context in Elden Ring
        private int NewEventID(bool writeable = false)
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
                AllocateWritableEventIDs(1);
            }
            if (tmpBase > tmpMax || writeBase > writeMax) throw new Exception($"Event {newId} hit event limit.");
            return newId;
        }

        public Result Run(RandomizerOptions opt, EnemyPreset preset)
        {
#if SDT
            if (game.Sekiro)
            {
                return RunGame(opt, preset, game.SekiroMaps, new SekiroEnemyEditor { Game = game });
            }
#endif
#if ER
            if (game.EldenRing)
            {
                return RunGame(opt, preset, game.EldenMaps, new EldenEnemyEditor { Game = game });
            }
#endif
#if DS1
            if (game.DS1)
            {
                return RunGame(opt, preset, game.DS1Maps, new DS1EnemyEditor { Game = game });
            }
#endif
#if DS3
            if (game.DS3)
            {
                return RunGame(opt, preset, game.DS3Maps, new DS3EnemyEditor { Game = game });
            }
#endif
            throw new Exception($"Internal error: {game.Type}");
        }

        // TODO: Probably degenericize EnemyEditor as it's not great for breaking up state and it doesn't provide much safety in practice on top of MSB interfaces
        public Result RunGame<TMap, TEnemy, TObject, TRegion, TGenerator>(
            RandomizerOptions opt, EnemyPreset preset, IReadOnlyDictionary<string, TMap> maps, EnemyEditor<TMap, TEnemy, TObject, TRegion, TGenerator> enemyEditor)
            where TMap : IMsb where TEnemy : IMsbPart where TObject : IMsbPart where TRegion : IMsbRegion where TGenerator : IMsbEvent
        {
            int seed = opt.Seed2 == 0 ? (int)opt.Seed : (int)opt.Seed2;

            // ---
            // --- Set up misc constants
            // ---

            // We may need to add a lot of entities (regions and helpers).
            // These don't need to exist in a valid map range.
            uint entityBase = 1400000;
            uint entityMax = 1500000;
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
            else if (game.DS1)
            {
                // Free flag ranges in final map (may conflict with other mods):
                // Temp flags: 11805400 to 11805900
                // 11805400, 20 flags: for Vagrant
                // 11805420, 20 flags: for O&S phase 2
                // 11805430, 20 flags: for selecting gravelord map
                // 11805890: param alert flag
                // 11805891: gravelord active flag, instead of 732/735
                // Perm flags: 11800600 to 11800900
                // 11800600, 10 flags: for counting up gravelord timer
                // 11800610, 20 flags: current gravelord map.
                // For two flags, first is selected 1/3 of the time
                // 5010 5020
                entityBase = 1900000;
                entityMax = 2000000;
                // Covenant items: 109 374 375
                // game.Params["EquipParamGoods"][109]["maxNum"].Value = (short)9999;
            }
            uint newEntity()
            {
                if (entityBase >= entityMax) throw new Exception("Internal error: Can't create more entities: out of ids");
                uint entityId = entityBase++;
                if (game.EldenRing && entityBase % 10000 == 4000)
                {
                    entityBase += 6000;
                }
                return entityId;
            }
            // Filianore/Dungeon range, except for groups, which require a valid range to track
            uint entityGroupBase, entityGroupMax;
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
            else if (game.DS1)
            {
                // No groups
                entityGroupBase = entityGroupMax = 0;
            }
            else throw new Exception();

            HashSet<uint> entityGroupSkip = new HashSet<uint>
            {
                5115800, // Gael group
                19005800, // Final boss
                19005801, // Dummy final boss
            };
            uint newGroupEntity()
            {
                if (entityGroupSkip.Contains(entityGroupBase)) entityGroupBase++;
                if (entityGroupBase >= entityGroupMax) throw new Exception("Internal error: Can't create more entity groups: out of ids");
                return entityGroupBase++;
            }

            bool eldenExcludeDlc = game.EldenRing && !opt["dlc"];
            Dictionary<string, EMEVD> emevds = game.Emevds;
            if (eldenExcludeDlc)
            {
                maps = new Dictionary<string, TMap>(maps.Where(e => !game.IsEldenDlcMap(e.Key)));
                emevds = new(emevds.Where(e => !game.IsEldenDlcMap(e.Key)));
            }

            // ---
            // --- Load enemy config
            // ---

            IDeserializer deserializer = new DeserializerBuilder().Build();
            string enemyConfigPath = $"{game.Dir}/Base/enemy.txt";
            string enemyListPath = null;
            string customNamesPath = null;
#if DEV
            if (game.EldenRing && opt["full"])
            {
                // More verbose enemy config is 3x bigger so don't load it for normal randomization
                // All of this file management may not be worth it
                enemyConfigPath = "configs/diste/enemy.txt";
                enemyListPath = "configs/diste/enemylist.txt";
                customNamesPath = "configs/diste/customnames.txt";
            }
#endif
            EnemyAnnotations ann = ReadYamlFile<EnemyAnnotations>(enemyConfigPath);
#if DEV
            if (enemyListPath != null)
            {
                EnemyAnnotations ann2 = ReadYamlFile<EnemyAnnotations>(enemyListPath);
                ann.Enemies = ann2.Enemies;
                ann.CategoryChanges = ann2.CategoryChanges;
                if (!opt["dumpenemylist"])
                {
                    // TODO: Make these two different lists
                    if (ann.BaseCategories.Count > 0)
                    {
                        ann.BaseCategories = ann2.BaseCategories;
                    }
                    else
                    {
                        ann.Categories.AddRange(ann2.Categories);
                    }
                }
            }
            if (customNamesPath != null)
            {
                ann.CustomNames = ReadYamlFile<Dictionary<string, string>>(customNamesPath);
            }
            if (EnemyDump.WriteEnemyInfo(game, events, eventConfig, ann, opt))
            {
                return null;
            }
#endif

            // ---
            // --- Load preset, but do not fully process
            // ---

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

#if DEV
            if (opt["configgen"])
            {
                preset = null;
            }
#endif
            if (preset != null)
            {
                preset.ProcessParents(ann);
            }
            bool presetOpt(string name, bool defaultTrue = false)
            {
                if (opt[name]) return true;
                return preset == null ? defaultTrue : preset[name];
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

            // ---
            // --- Modify the enemy config directly before any processing (for enemy onslaught and chest->mimic)
            // ---

            bool dlcDupe = true;
            bool baseDupe = true;
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
            Dictionary<uint, List<uint>> dupeEnemyMap = new();
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
                        // Actually, try to only limit it for invaders or pseudomultiplayer encounters
                        // This is before SetTags (why is that even a thing?)
                        if (info.Tags != null && info.Tags.Contains("invader") && info.Map != "m20_01_00_00")
                        {
                            continue;
                        }
                        // Hardcode skipping non-default NPCs for Leda fight at higher dupe amounts
                        if (defaultDupeCount > 5 && (info.ID == 20010721 || info.ID == 20010723))
                        {
                            continue;
                        }
                    }
                    // Use this as the "main" exclusion condition - can maybe use proper categories as well
                    if (info.Class == EnemyClass.Helper || info.Class == EnemyClass.None)
                    {
                        continue;
                    }
                    // Note info.DLC is not set yet.
                    if (!dlcDupe && game.IsEldenDlcMap(info.Map))
                    {
                        continue;
                    }
                    if (!baseDupe && !game.IsEldenDlcMap(info.Map))
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
            Dictionary<uint, ChestInfo> newMimics = new();
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
                        SplitFrom = (uint)info.ClosestMimic,
                        SetColName = info.ColName,
                        Tags = info.Tags,
                    };
                    ann.Enemies.Add(chestInfo);
                    newMimics[chestInfo.ID] = info;
                }
            }

            // Currently silo depends on enemy onslaught as not all event scripts are migrated, but this can be moved up when that is implemented
            AreaSiloType overallSilo = opt["dlc"] && (opt["dlcsilo"] || (!dlcDupe && anyDupeEnabled)) ? AreaSiloType.DLC : AreaSiloType.None;
            if (opt["dlc"] && overallSilo == AreaSiloType.None && preset != null)
            {
                overallSilo = preset.OverallSilo;
            }

            // ---
            // --- Pre-pass on enemy config, for use in map data pass + main enemy config pass
            // ---

            Dictionary<uint, EnemyInfo> infos = new Dictionary<uint, EnemyInfo>();
            // Entities owned by a given entity.
            // This includes boss arena/flag inheritance (TODO maybe that should be changed, adds a bunch of useless checks)
            Dictionary<uint, List<uint>> owners = new();
            // Map from (map, part) to entity id, for enemies initially without entity ids
            Dictionary<(string, string), uint> configEntityIds = new();
            bool isFakeId(uint id)
            {
                // Used by Elden Ring, DS1, and DS3. DS1 also has Chained Prisoner
                return (id >= 2800000 && id < 2900000) || (id >= 2888000 && id < 2889000) || id == 1210700;
            }
            // Splits to perform from source id
            Dictionary<uint, List<EnemyInfo>> copyTo = new();
            // Map from owner enemy to total healthbars used. If missing, assume this is 1.
            Dictionary<uint, int> totalHealthbars = new();
            // Map from ESD id to defeat flag
            Dictionary<int, int> deathLineFlags = new();
            // For later convenience, enemies to edit talk lines for
            List<uint> talkLineEdits = new();
            // Map from RemoveGroup to an acceptable substitute group
            Dictionary<uint, uint> buffGroupEntities = new();
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (game.EldenRing)
                {
                    info.DLC = game.IsEldenDlcMap(info.Map);
                    if (eldenExcludeDlc && info.DLC) continue;
                }
                if (infos.ContainsKey(info.ID)) throw new Exception($"Duplicate config entity {info.ID}");
                infos[info.ID] = info;
                if (isFakeId(info.ID))
                {
                    // Map is present in games with fake ids, which excludes Sekiro
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
                    uint healthKey = info.OwnedBy > 0 ? info.OwnedBy : info.ID;
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
                if (info.TalkLine != null)
                {
                    talkLineEdits.Add(info.ID);
                }
                if (info.RemoveGroup > 0 && info.BuddyGroup > 0 && info.OwnedBy <= 0 && info.NextPhase <= 0)
                {
                    // In Elden Ring, can use BuddyGroup, which should be reliably present.
                    // In DS3, need to figure out a different system (reuse BuddyGroup, despite no buddies?)
                    buffGroupEntities[info.RemoveGroup] = info.BuddyGroup;
                }
                if (info.Tags != null)
                {
                    info.SetTags(info.Tags);
                }
            }
            foreach (KeyValuePair<uint, List<EnemyInfo>> entry in copyTo)
            {
                if (!infos.ContainsKey(entry.Key)) throw new Exception($"Internal error: split entity {entry.Key} does not exist");
            }
            Dictionary<int, List<string>> objectOwners = new();
            if (ann.Objects != null)
            {
                foreach (ObjectInfo info in ann.Objects)
                {
                    if (info.OwnedBy > 0)
                    {
                        AddMulti(objectOwners, info.OwnedBy, info.ID);
                    }
                }
            }

            bool dupeEnabled(uint target) => dupeEnemyMap.ContainsKey(target) || (infos.TryGetValue(target, out EnemyInfo ti) && ti.DupeFrom > 0);
            int dupeCount(uint target) => dupeEnemyMap.TryGetValue(target, out List<uint> dupes) ? dupes.Count : 0;
            int combinedDupeCount(IEnumerable<uint> targets) => targets.Select(dupeCount).Concat(new[] { 0 }).Max();

            // ---
            // --- Preprocess actual map data, including some modifications to make it match the config, before constructing defaultData
            // ---

            enemyEditor.InitialMapPass(opt);

            // Ignoring DLC is a bit tricky because of how many systems there are.
            // For the most part, try to randomize DLC enemies to themselves, rather than totally ignoring them, in DS3.
            // In Elden Ring, try to leave out both maps and infos.
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

            Vector3 getDupeOffset(Vector3 rotation, uint target, int index)
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

            // The "main" map data for an entity
            Dictionary<uint, EnemyData> defaultData = new();
            // Mapping from target entity to event map name.
            // In the case of Elden Ring, this should not be a 01 or 02 tile.
            Dictionary<uint, string> ownerMap = new();
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
                    // TODO: DS3 handling for -1
                    if (data.ID == 0 || (int)data.ID == -1)
                    {
                        if (configEntityIds.TryGetValue((entry.Key, e.Name), out uint fakeId))
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
                            && configEntityIds.TryGetValue((entry.Key, e.Name), out uint fakeId)
                            && infos.TryGetValue(fakeId, out info))
                        {
                            infos.Remove(fakeId);
                            infos[data.ID] = info;
                            info.ID = data.ID;
                        }
                        else
                        {
                            bool dlcStartGrace = data.ID / 10 == 1001095;
                            if (!dlcStartGrace) Console.WriteLine($"Unknown enemy {entry.Key} {e.Name} #{data.ID}");
                        }
                    }
                    string eventMap = entry.Key;
                    string eventPart = data.Name;
                    if (game.EldenRing && eventMap.StartsWith("m6") && !eventMap.EndsWith("0"))
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
                        if (!emevds.ContainsKey(eventMap))
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
                    if (!emevds.ContainsKey(eventMap)) Console.WriteLine($"Nonexistent eventMap {eventMap} for {data.Name} #{data.ID}");
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
                            // Note, different format for 02 maps
                            int partId = int.Parse(derived.Name.Split('_').Last().TrimStart('0'));
                            // TODO: Make Map always present. It is not present in Sekiro enemy config.
                            string toMap = derived.Map ?? entry.Key;
                            // This does a concurrent modification to the enemy list for same-map splits,
                            // which is accounted for above.
                            // Also, unlike with helper enemies, don't clear groups
                            TMap actualMap = maps[toMap];
                            TEnemy e2 = enemyEditor.CloneEnemy(actualMap, e, data, derived.ID, partId);
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

#if DEV
            if (opt["configgen"])
            {
                EnemyConfigGen.WriteEvents(game, events, eventConfig, opt, infos, ann, defaultData);
                return null;
            }
#endif

            // ---
            // --- Main pass on enemy config, reconciling with map data and filling in the rest of the EnemyInfo metadata
            // ---

            EnemyPlacement places = null;
            if (game.EldenRing)
            {
                places = EnemyPlacement.ReadLazy("diste/Base/places.bin");
            }
            else if (game.DS1)
            {
                places = EnemyPlacement.ReadLazy("dist1/Base/places.bin");
            }

            // Map from fake boss to actual occurrence of it
            Dictionary<uint, uint> tempBosses = new();
            if (game.EldenRing)
            {
                tempBosses = new()
                {
                    // Adula
                    [1034500800] = 1034420800,
                    // Lansseax
                    [1037510800] = 1041520800,
                };
            }

            // Opaque GeneratorData to collect before removing any enemies from any maps
            var generators = enemyEditor.GetCrossMapGeneratorData();

            // Process core enemy config
            List<uint> treeDragonOrder = Enumerable.Repeat((uint)0, 5).ToList();
            // Unused feature which compensated for prior lack of BuddyGroup
            HashSet<uint> defeatIds = new(game.Params["GameAreaParam"].Rows.Select(r => (uint)r.ID));
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
            List<uint> importantDupes = new();
            // Report on these and don't randomize them by default
            SortedSet<uint> missingInfos = new();
            // Mapping from defeat flag to main boss entity id, for use in bossbgm which is keyed by defeat flag
            Dictionary<int, uint> defeatFlagIds = new();
            // Entities which have custom placement supported, currently only multi-enemy bosses
            HashSet<uint> placeEnemies = new();
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (!infos.ContainsKey(info.ID)) continue;
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
                // Migration
                if (info.Important != null)
                {
                    info.ExtraName ??= info.Important.ExtraName;
                    if (info.NpcName == 0)
                    {
                        info.NpcName = info.Important.NpcName;
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
                    info.ArenaExcludeData = PhraseRe.Split(info.ArenaExclude).Select(ex => Arena.Parse(ex)).ToList();
                    if (info.ArenaExcludeData.Count > 8) throw new Exception($"Internal error: exclude regions defined for {info.ID} exceeds 8");
                }
                if (info.ExtraArenas != null)
                {
                    info.ExtraArenaData = new Dictionary<string, List<Arena>>();
                    foreach (KeyValuePair<string, string> extra in info.ExtraArenas)
                    {
                        info.ExtraArenaData[extra.Key] = PhraseRe.Split(extra.Value).Select(ex => Arena.Parse(ex)).ToList();
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
                else if (game.DS1)
                {
                    info.IsBossTarget = info.Class == EnemyClass.Boss;
                    info.IsBuffSource = info.Class == EnemyClass.Boss;
                    info.IsFixedSource = info.Class == EnemyClass.Boss;
                    info.IsImportantTarget = info.Class == EnemyClass.Boss;
                    info.HasPerceptiveNose = info.Class == EnemyClass.Boss;
                    // Miniboss mainly for Hydra placement, may not be used
                    info.IsArenaTarget = info.Class == EnemyClass.Boss || info.Class == EnemyClass.Miniboss;
                    info.IsImmortal = info.HasTag("immortal");
                    info.IsMortalSekiroBoss = false;
                }
                else if (game.EldenRing)
                {
                    // TODO: split this up into, is boss encounter/start flag? and, has souls on drop/defeat flag?
                    info.IsBossTarget = info.Class == EnemyClass.Boss || info.Class == EnemyClass.MinorBoss || info.Class == EnemyClass.Evergaol;
                    info.IsImportantTarget = info.IsBossTarget
                        || info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.DragonMiniboss
                        || info.Class == EnemyClass.NightMiniboss;
                    info.HasPerceptiveNose = info.IsBossTarget;
                    info.IsBuffSource = info.IsImportantTarget;
                    info.IsFixedSource = info.IsImportantTarget;
                    // Elden Ring uses a different system which is entity-specific, at least for current time-space tradeoff
                    // Still, reuse this boolean for now.
                    info.IsArenaTarget = info.IsImportantTarget;
                    info.IsImmortal = info.HasTag("immortal");
                    info.IsMortalSekiroBoss = false;
                }
                if (places != null && info.IsArenaTarget)
                {
                    // Arena sources is a subset of arena targets
                    if (game.EldenRing)
                    {
                        // Should this just be a tag?
                        if (info.Regions != null)
                        {
                            placeEnemies.Add(info.ID);
                        }
                        else
                        {
                            int placeCount = info.HasTag("noplace") ? 0 : 1;
                            if (owners.TryGetValue(info.ID, out List<uint> entities))
                            {
                                placeCount += entities.Count(id => !infos[id].HasTag("noplace"));
                            }
                            if (placeCount > 1)
                            {
                                placeEnemies.Add(info.ID);
                            }
                        }
                    }
                    // Don't use IsImportantTarget for this, it implies segments
                    else if (game.DS1)
                    {
                        placeEnemies.Add(info.ID);
                    }
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
                        info.DefeatFlag = 10000000 + (int)info.ID;
                    }
                }
                if (info.Regions != null)
                {
                    foreach (string region in info.Regions)
                    {
                        if (region.StartsWith("copy"))
                        {
                            string[] parts = region.Split(' ');
                            uint source = uint.Parse(parts[1]);
                            uint target = uint.Parse(parts[2]);
                            // This may fail in some cases, when entity does not exist or multiple maps
                            string map = defaultData[info.ID].MainMap;
                            enemyEditor.MakeCrossMapRegionCopy(() => target, map, map, source, false);
                        }
                    }
                }
                bool isDuped = dupeEnabled(info.ID);
                if (isDuped && dupeEnemyMap.TryGetValue(info.ID, out List<uint> dupes) && info.IsImportantTarget)
                {
                    // Add to list for way later, specifically for originals of important targets
                    importantDupes.Add(info.ID);
                    // Also fill out tempBoss for dupes of temp bosses
                    if (tempBosses.TryGetValue(info.ID, out uint realBoss))
                    {
                        List<uint> realBosses = new() { realBoss };
                        if (dupeEnemyMap.TryGetValue(realBoss, out List<uint> realBossDupes))
                        {
                            realBosses.AddRange(realBossDupes);
                        }
                        for (int i = 0; i < dupes.Count; i++)
                        {
                            // First temp dupe lines up with first dupe of first real dupe, ideally
                            tempBosses[dupes[i]] = realBossDupes[(i + 1) % realBossDupes.Count];
                        }
                    }
                }
                if (info.DefeatEntity > 0 || (isDuped && defeatIds.Contains(info.ID)) || tempBosses.ContainsKey(info.ID))
                {
                    PARAM.Row selfRow = game.Params["GameAreaParam"][(int)info.ID];
                    // Cases where main entity is different from HandleBossDefeat entity.
                    // TODO this should be possible to eliminate, was only a hack for missing BuddyGroup.
                    if (info.DefeatEntity > 0)
                    {
                        PARAM.Row defeatRow = game.Params["GameAreaParam"][(int)info.DefeatEntity];
                        if (selfRow == null && defeatRow != null)
                        {
                            selfRow = game.AddRow("GameAreaParam", (int)info.ID);
                            GameEditor.CopyRow(defeatRow, selfRow);
                        }
                    }
                    // Hardcode this for now
                    if (tempBosses.ContainsKey(info.ID) && selfRow != null)
                    {
                        selfRow["bonusSoul_single"].Value = 0;
                        selfRow["bonusSoul_multi"].Value = 0;
                    }
                    // Default soul multiplier for dupe status. TODO backport to previous games
                    if (isDuped && selfRow != null && game.EldenRing && !opt["swaprewards"])
                    {
                        double mult = ScalingEffects.GetXpRate(dupeCount(info.ID), presetOpt("multhp"));
                        uint val = (uint)selfRow["bonusSoul_single"].Value;
                        val = (uint)roundBonusSoul((int)(val * mult));
                        selfRow["bonusSoul_single"].Value = val;
                        selfRow["bonusSoul_multi"].Value = val;
                    }
                }
                if (info.BuddyGroup > 0 && info.OwnedBy <= 0 && info.NextPhase <= 0 && data != null)
                {
                    foreach (uint group in data.Group)
                    {
                        // Just do this rewrite whenever possible
                        if (buffGroupEntities.ContainsKey(group)) continue;
                        buffGroupEntities[group] = info.BuddyGroup;
                    }
                }
                if (info.IsImportantTarget && info.OwnedBy <= 0 && info.DefeatFlag > 0)
                {
                    // Hardcode Malenia, who uses her defeat event rather than defeat flag
                    int bgmDefeatFlag = info.ID == 15000800 ? 15002800 : info.DefeatFlag;
                    defeatFlagIds[bgmDefeatFlag] = info.ID;
                }
            }
            // If enemies are not important, allow them to be missing
            // Other entities must be added to a preset/merge config as explicitly removed.
            foreach (uint id in missingInfos)
            {
                infos[id].Class = EnemyClass.None;
            }

            if (merge != null && merge.NorandomEntities.Any())
            {
                if (merge.NorandomEntities.GetAllValues(out IEnumerable<int> entities))
                {
                    foreach (int id in entities)
                    {
                        if (infos.TryGetValue((uint)id, out EnemyInfo info))
                        {
                            infos[(uint)id].Class = EnemyClass.None;
                        }
                    }
                }
                else
                {
                    foreach (EnemyInfo info in infos.Values)
                    {
                        if (merge.NorandomEntities.Includes((int)info.ID))
                        {
                            info.Class = EnemyClass.None;
                        }
                    }
                }
            }

            // At this point, set up EnemyState. As more fields are moved into Enemies, all of this can be set up at the start.
            EnemyState state = new();
            Enemies enemies = new Enemies(game)
            {
                Infos = infos,
                Owners = owners,
                DefaultData = defaultData,
                DupeEnemyMap = dupeEnemyMap,
                AnyDupeEnabled = anyDupeEnabled,
                DeathLineFlags = deathLineFlags,
                TalkLineEdits = talkLineEdits,
            };
            state.Enemies = enemies;

            // ---
            // --- Process preset
            // ---

            foreach (EnemyGroup group in ann.Groups)
            {
                if (group.Models == null) continue;
                Dictionary<string, List<string>> models = new Dictionary<string, List<string>>();
                foreach (string model in events.Decomment(group.Models))
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
                            info.AddTag(group.Name);
                        }
                    }
                }
            }

            if (preset != null)
            {
                preset.ProcessEnemyPreset(game, infos, ann, defaultData);
            }

            state.Preset = preset;

            // Special pass for Old Dragons, and the final mutation to EnemyInfo.
            // This is here because the # to replace currently depends on the distribution of enemies, especially for more aggressive presets.
            // This is the first time the enemy seed is used.
            int randomOldDragons = 0;
            if (game.Sekiro && opt["bosses"] && opt["enemies"])
            {
                SortedSet<uint> unpreferred = ann.GetGroup("nodragon");
                List<uint> eligibleForDragons = preset?.Basic == null && preset?.Add == null
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

                List<EnemyInfo> oldDragons = infos.Values.Where(i => i.Class == EnemyClass.OldDragon).ToList();
                Shuffle(new Random(seed + 42), oldDragons);
                for (int i = 0; i < Math.Min(oldDragons.Count, randomOldDragons); i++)
                {
                    oldDragons[i].Class = EnemyClass.Basic;
                }
            }

            // ---
            // --- Set up scaling. This is done early mainly to print it out in spoiler logs.
            // ---

            // Make all scaling speffects, even if we're not using them in this run.
            // The scaling sections and effects should be combined and put in EnemyState, with some of the below things encapsulated there.
            // Mapping from (source section, target section) to various speffects
            ScalingEffects.SpEffectValues scalingSpEffects;
            if (game.EldenRing)
            {
                ScalingEffects scaling = new ScalingEffects(game);
                ann.ScalingSections = scaling.InitializeEldenScaling(defaultData, dupeEnemyMap);
                scalingSpEffects = scaling.EditScalingSpEffects();
            }
            else if (game.DS1 || game.DS3)
            {
                ScalingEffects scaling = new ScalingEffects(game);
                ann.ScalingSections = scaling.InitializeScaling(defaultData, dupeEnemyMap);
                scalingSpEffects = scaling.EditScalingSpEffects();
            }
            else
            {
                scalingSpEffects = new ScalingEffects(game).EditScalingSpEffects();
            }

            Dictionary<uint, int> targetScalingSections = ann.ScalingSections;
            if (ann.ScalingSections != null && opt["scale"] && (presetOpt("scalerandom") || (preset != null && preset.RandomScaling)))
            {
                Random scalingRandom = new Random(seed);
                targetScalingSections = new(ann.ScalingSections);
                bool onlyScaleUp = presetOpt("scaleup");
                foreach ((uint target, int targetSection) in ann.ScalingSections)
                {
                    if (targetSection > 0)
                    {
                        // MaxTier is something like 20 (tier 0 ignored in Elden Ring), so normally this goes from 0 to 19 -> 1 to 20.
                        int newScale = scalingRandom.Next(scalingSpEffects.MaxTier) + 1;
                        if (onlyScaleUp)
                        {
                            // Random scaling has some chance of not applying alongside onlyScaleUp, with higher chance for higher scalings,
                            // compared to e.g. choosing a number between targetSection and MaxTier. This approach seems better balanced.
                            newScale = Math.Max(newScale, targetSection);
                        }
                        targetScalingSections[target] = newScale;
                    }
                }
            }
            bool getScalingSections(uint source, uint target, out int sourceSection, out int targetSection, bool ignoreCustom = false)
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
                if (game.DS1 && opt["earlyscale"] && targetSection == 1)
                {
                    // Always scale to 1 with this mode, doesn't matter who the source is
                    return true;
                }
                if (infos.TryGetValue(source, out EnemyInfo sourceInfo))
                {
                    if (sourceInfo.HasTag("noscale") || (sourceInfo.HasTag("noscaleup") && targetSection > sourceSection))
                    {
                        if (game.EldenRing && targetSection >= 21)
                        {
                            // Since this only applies to base game enemies, try using relative scaling here, just to add resistance to player blessing upgrades.
                            // If this works well enough, DLC-aware unscaled could be used across the board.
                            sourceSection = 21;
                        }
                        else
                        {
                            targetSection = sourceSection;
                        }
                    }
                }
                // Exclude most Asylum 1 enemies from scaling (does not include evil vagrants)
                if ((presetOpt("scaleup") && targetSection < sourceSection) || opt["noscale"])
                {
                    targetSection = sourceSection;
                }
                if (targetSection >= 21 && opt["flatdlc"])
                {
                    targetSection = 21;
                }
                return true;
            }

            // This is not currently used. Backport scadu shop from fog gate rando maybe?
            if (opt["flatdlc"] || opt["veryflatdlc"])
            {
                PARAM.Row baseScadu = game.Params["SpEffectParam"][20000100];
                PARAM.Row baseRevered = game.Params["SpEffectParam"][20000200];
                PARAM.Row baseDlc = null;
                PARAM.Row baseDlcNpc = null;
                if (opt["veryflatdlc"])
                {
                    // Just for testing that special scaling modes still work
                    baseDlc = game.Params["SpEffectParam"][20007000];
                    baseDlcNpc = game.Params["SpEffectParam"][20007200];
                }
                foreach (PARAM.Row row in game.Params["SpEffectParam"].Rows)
                {
                    if (row.ID > 20000100 && row.ID <= 20000120)
                    {
                        GameEditor.CopyRow(baseScadu, row);
                    }
                    else if (row.ID > 20000200 && row.ID <= 20000210)
                    {
                        GameEditor.CopyRow(baseRevered, row);
                    }
                    else if (baseDlc != null && row.ID > 20007000 && row.ID <= 20007130)
                    {
                        GameEditor.CopyRow(baseDlc, row);
                    }
                    else if (baseDlc != null && row.ID > 20007200 && row.ID <= 20007330)
                    {
                        GameEditor.CopyRow(baseDlcNpc, row);
                    }
                }
            }

            // ---
            // --- Set up all silos of source/target enemies
            // ---

            EnemyPermutation perm = new();

            // Force mapping from target to source. This does disrupt the seed, but only way to avoid that would be with potentially extensive swapping.
            Dictionary<uint, uint> forceMap = new();
            HashSet<uint> autoForce = new();

            // Use forcemap to disable DLC randomization in DS3. In Elden Ring, map filters happen at the very start.
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
            bool ignoreTarnished = !opt["tarnished"];
            if (game.EldenRing && (preset == null || ignoreTarnished))
            {
                foreach (EnemyInfo info in infos.Values)
                {
                    // I guess custom system for default norandom
                    if (preset == null && info.HasTag("norandom"))
                    {
                        forceMap[info.ID] = info.ID;
                    }
                    else if (ignoreTarnished && info.HasTag("tarnished"))
                    {
                        forceMap[info.ID] = info.ID;
                    }
                }
            }
            if (preset != null)
            {
                // Specific enemies in specific locations
                Random forcemapRandom = new Random((int)seed);
                foreach (KeyValuePair<uint, List<uint>> transfer in preset.EnemyIDs.OrderBy(e => e.Key))
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
                foreach (uint norandom in preset.DontRandomizeIDs)
                {
                    // Previously, norandom overwrite Enemies, but this makes phobia presets etc hard
                    if (forceMap.ContainsKey(norandom)) continue;
                    forceMap[norandom] = norandom;
                }
                // Finally, if there are doubles, randomize them to their originals
                // TODO: This was change from preset.EnemyMultiplier > 1, is this right?
                if (anyDupeEnabled)
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
            foreach (KeyValuePair<uint, ChestInfo> mimic in newMimics)
            {
                if (infos[mimic.Key].Class == EnemyClass.DupeOnly)
                {
                    forceMap[mimic.Key] = (uint)mimic.Value.ClosestMimic;
                }
            }
            // Hack system until DS1 presets work
            if (opt["limitvagrant"])
            {
                Random limitVagrantRandom = new Random(seed);
                List<EnemyInfo> vagrants = infos.Values.Where(i => i.ModelID == "c3491").ToList();
                Shuffle(limitVagrantRandom, vagrants);
                bool limit = false;
                foreach (EnemyInfo info in vagrants)
                {
                    if (limit)
                    {
                        forceMap[info.ID] = info.ID;
                    }
                    limit = !limit;
                }
            }

            // Map from owner to swappable helpers
            SortedDictionary<uint, List<uint>> swapHelperMapping = new();
            // Total list of swappable helpers
            SortedSet<uint> allSwapHelpers = new();
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

            Dictionary<uint, List<uint>> reverseForceMap = new();
            foreach (KeyValuePair<uint, uint> transfer in forceMap)
            {
                AddMulti(reverseForceMap, transfer.Value, transfer.Key);
            }
            /*SortedDictionary<EnemySiloType, EnemySilo> silos = new();
            IEnumerable<KeyValuePair<EnemySiloType, EnemySilo>> getSilos(EnemyClass type)
            {
                return silos.Where(e => e.Key.Type == type);
            }
            IEnumerable<KeyValuePair<uint, uint>> getSiloMappings(EnemyClass type)
            {
                return silos.Where(e => e.Key.Type == type).SelectMany(e => e.Value.Mapping);
            }*/

            Random reducePassiveRandom = new Random(seed);
            Random forceSwapRandom = new Random(seed);
            bool allowSubsilo = overallSilo == AreaSiloType.None && opt["dlc"] && preset != null;
            foreach (EnemyInfo info in ann.Enemies)
            {
                if (!infos.ContainsKey(info.ID)) continue;
                EnemyClass infoSilo = siloClass(info.Class);
                if (!randomizedTypes.Contains(infoSilo)) continue;
                EnemySiloType siloType = EnemySiloType.Of(infoSilo);
                AreaSiloType areaSiloType = overallSilo;
                if (allowSubsilo && preset.Classes.TryGetValue(infoSilo, out ClassAssignment assign) && assign != null)
                {
                    areaSiloType = assign.ClassSilo;
                }
                if (areaSiloType == AreaSiloType.DLC)
                {
                    siloType = siloType.WithArea(AreaSilo.ForDlc(info.DLC));
                }
                EnemySilo silo = perm.GetOrCreateSilo(siloType);
                if (forceMap.ContainsKey(info.ID))
                {
                    uint target = info.ID;
                    uint source = forceMap[info.ID];
                    silo.Mapping[target] = source;
                    // Try to exclude non-random enemies from being swapped here, or if they are non-random dupes
                    if (swapHelperMapping.TryGetValue(source, out List<uint> helperSources)
                        && (preset == null || !preset.DontRandomizeIDs.Contains(target)))
                    {
                        foreach (uint helper in helperSources)
                        {
                            silo.SwapMapping[(target, helper)] = Choice(forceSwapRandom, allSwapHelpers.ToList());
                        }
                    }
                }
                else if (info.Class == EnemyClass.DupeOnly)
                {
                    // Dupe-only enemies should only be forceMap'd
                }
                else if (tempBosses.ContainsKey(info.ID))
                {
                    // These get assigned automatically if not forceMap'd
                    silo.TempTargets.Add(info.ID);
                }
                else
                {
                    if (missingInfos.Contains(info.ID)) throw new Exception($"Can't randomize #{info.ID} since it was removed from the game. See spoiler log for a full list.");
                    silo.Targets.Add(info.ID);
                }

                if (reverseForceMap.TryGetValue(info.ID, out List<uint> targets)
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
                else if (info.DupeFrom > 0 || tempBosses.ContainsKey(info.ID))
                {
                    // Dupes cannot be sources - this will generally involve duplicating other eligible sources
                    // Also, by default, temp bosses are not their own sources (TODO should be their own EnemyClass?)
                }
                else
                {
                    if (missingInfos.Contains(info.ID)) throw new Exception($"Can't randomize #{info.ID} since it was removed from the game. See spoiler log for a full list.");
                    silo.Sources.Add(info.ID);
                }
            }

            // Quick internal name for entity id (also in Enemies.Name)
            string ename(uint ent)
            {
                if (defaultData.TryGetValue(ent, out EnemyData data))
                {
                    return $"{data.Name} #{ent} ({game.ModelCharacterName(data.Model, data.Char)})";
                }
                return $"#{ent}";
            }

            // ---
            // --- Actually assign enemy locations within silos, taking into account placement constraints
            // ---

            // Avoid populating a given arena with the same enemy type. For Sekiro minibosses, it is mainly Shigekichi, Juzou,
            // and Vilehand arenas that are reused.
            // DS3 doesn't use miniboss arenas and there are a lot of them so models are used directly
            Dictionary<string, HashSet<string>> arenaEnemyTypes = new Dictionary<string, HashSet<string>>();
            // Map from multi-phase bosses (either phase) to the second phase
            Dictionary<uint, uint> multiPhase = new();
            // Map from entity id / 10 to all entity ids in an npc encounter
            Dictionary<uint, HashSet<uint>> encounterNpcs = new();
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
                if (info.DupeFrom > 0 && multiPhase.TryGetValue(info.DupeFrom, out uint originalNext))
                {
                    multiPhase[info.ID] = originalNext;
                }
                if (info.Class == EnemyClass.HostileNPC)
                {
                    // In all current encounters, this entity always exists. Otherwise can just track it in a separate set.
                    // Or use a non-numerical way to group together NPCs.
                    uint baseId = info.ID / 10 * 10;
                    if (infos.TryGetValue(baseId, out EnemyInfo baseInfo) && baseInfo.HasTag("encounter"))
                    {
                        AddMulti(encounterNpcs, info.ID / 10, info.ID);
                    }
                }
            }
            if (game.DS1)
            {
                // NextPhase can't support multiple next phases, so add this for placement purposes
                multiPhase[1510811] = 1510801;
            }

            // For the sake of the mod being more interesting, exclude interesting enemies from being temporary
            HashSet<uint> sekiroPhantomGroups = new()
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
            bool canPlace(uint source, uint target, EnemySilo silo, BossPhaseLimit prevLimits, bool explain)
            {
                // Always place an enemy if target is already selected
                if (forceMap.ContainsKey(target)) return true;
                EnemyInfo sourceInfo = infos[source];
                EnemyInfo targetInfo = infos[target];
                // Custom exclusions
                if (targetInfo.ExcludeTags.Any())
                {
                    foreach (string exclude in targetInfo.ExcludeTags)
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
                // TODO fix Godskin Snail in dupe, for now prevent placement
                if (source == 31220800 && targetInfo.IsImportantTarget && targetInfo.DupeIndex == -1 && dupeCount(target) > 0)
                {
                    if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because a bug prevents it from working as the main enemy in an onslaught boss");
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
                // Bosses in the same spot are disappointing
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
                    if (game.DS3 || game.EldenRing)
                    {
                        // Night minibosses are only three types of enemies in Elden Ring, DLC dragons are also three types if siloed
                        if (targetInfo.Class == EnemyClass.NightMiniboss || targetInfo.Class == EnemyClass.DragonMiniboss)
                        {
                            if (source == target)
                            {
                                if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they're identical");
                                return false;
                            }
                        }
                        else if ((sourceInfo.EnemyType != null && sourceInfo.EnemyType == targetInfo.EnemyType)
                            || defaultData[source].Model == defaultData[target].Model)
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same model");
                            return false;
                        }
                    }
                    else if (game.DS1)
                    {
                        if (source == target)
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they're identical");
                            return false;
                        }
                    }
                }
                // Minibosses not important targets in DS1, mainly prevent Hydra-to-Hydra (can add custom positioning too)
                if (game.DS1 && targetInfo.Class == EnemyClass.Miniboss)
                {
                    if (defaultData[source].Model == defaultData[target].Model)
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because they have the same model");
                        return false;
                    }
                }
                if (targetInfo.IsImportantTarget && sourceInfo.IsImportantTarget && !opt["ignoresize"])
                {
                    int arena = targetInfo.GetNamedTag("arena", out string arenaStr) && int.TryParse(arenaStr, out int arenaVal) ? arenaVal : (sourceInfo.IsBossTarget ? 2 : 3);
                    int size = sourceInfo.GetNamedTag("size", out string sizeStr) && int.TryParse(sizeStr, out int sizeVal) ? sizeVal : 0;
                    if (size > arena)
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} in safe mode because size {size} > arena {arena}");
                        return false;
                    }
                    // Yet more rykard exception, but only in omit since its boss distribution requires it
                    else if (Randomizer.Omit && (source == 16000800 || source == 16000801) && (arena < 3 || !targetInfo.IsBossTarget))
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} in safe mode because arena size {arena} or boss status {targetInfo.IsBossTarget}");
                        return false;
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
                    // Also avoid multiple instances of the same NPC in the same multi-NPC encounter
                    if (game.EldenRing && silo.Sources.Count > 1 && encounterNpcs.TryGetValue(target / 10, out HashSet<uint> encounterTargets) && encounterTargets.Count > 1)
                    {
                        // Try to avoid getting stuck by focusing only on this placement.
                        // Ignore the current contents of target, and don't check if other placements are valid.
                        foreach (uint encounterTarget in encounterTargets)
                        {
                            if (target == encounterTarget) continue;
                            uint encounterSource = silo.Mapping[encounterTarget];
                            if (defaultData[source].Char > 0 && defaultData[source].Char / 10 == defaultData[encounterSource].Char / 10)
                            {
                                if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because character already exists in the same encounter, {ename(encounterSource)} to {ename(encounterTarget)}");
                                return false;
                            }
                        }
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
                // Early boss roadblocks in DS1 and DS3
                if (targetInfo.Class == EnemyClass.Boss && opt["earlyreq"])
                {
                    if (targetInfo.HasTag("earlyreq"))
                    {
                        // Previously used sourceInfo.HasTag("late") in DS3, but scaling mostly been fixed
                        if (sourceInfo.HasTag("excludeearly"))
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's not an early boss");
                            return false;
                        }
                    }
                }
                if (game.DS1 && targetInfo.Class == EnemyClass.Boss)
                {
                    // Distinct options for Asylum Demon and Taurus Demon in DS1
                    if (opt["simpleasylum"] && targetInfo.HasTag("simpleasylum") && sourceInfo.HasTag("excludeearly"))
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's not a simple boss");
                        return false;
                    }
                    if (opt["simpletaurus"] && targetInfo.HasTag("simpletaurus") && sourceInfo.HasTag("excludeearly"))
                    {
                        if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because it's not a simple boss");
                        return false;
                    }
                }
                // Try to prevent adjacent boss phases from being the same enemy, when sources are removed.
                // This has some overlap with Sekiro phase logic but I am not touching that rn
                if (game.DS3 && targetInfo.Class == EnemyClass.Boss && silo.Sources.Count > 1)
                {
                    uint otherPhaseTarget = targetInfo.OwnedBy;
                    if (otherPhaseTarget <= 0 && owners.TryGetValue(target, out List<uint> owns))
                    {
                        otherPhaseTarget = owns.Find(own => infos[own].Class == EnemyClass.Boss);
                    }
                    if (otherPhaseTarget > 0)
                    {
                        uint otherPhaseSource = silo.Mapping[otherPhaseTarget];
                        if (source == otherPhaseSource)
                        {
                            if (explain) Console.WriteLine($"Not adding {ename(source)} to {ename(target)} because already present in other phase {ename(otherPhaseTarget)}");
                            return false;
                        }
                    }
                }
                // Elden Ring has a slightly different way of keeping track of this
                if ((game.EldenRing || game.DS1) && targetInfo.Class == EnemyClass.Boss && silo.Sources.Count > 1)
                {
                    multiPhase.TryGetValue(source, out uint sourcePhase);
                    multiPhase.TryGetValue(target, out uint targetPhase);
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
                    if (owners.TryGetValue(target, out List<uint> owns) || targetInfo.OwnedBy != 0)
                    {
                        owns ??= new();
                        if (targetInfo.OwnedBy != 0) owns.Add(targetInfo.OwnedBy);
                        foreach (uint other in owns)
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
            void fixupPass(EnemySilo silo, List<uint> targets, bool useLimits)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    bool debug = false;
                    uint cur = silo.Mapping[targets[i]];
                    BossPhaseLimit limits = useLimits ? new BossPhaseLimit { Entity = targets[i] } : null;
                    if (!canPlace(cur, targets[i], silo, limits, debug))
                    {
                        for (int j = 0; j < targets.Count; j++)
                        {
                            uint cand = silo.Mapping[targets[j]];
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
            void printPermutation(EnemySilo silo)
            {
                if (!debugPlacement) return;
                Console.WriteLine("----------------");
                foreach (KeyValuePair<uint, uint> transfer in silo.Mapping) Console.WriteLine($"For {ename(transfer.Key)}: Using {ename(transfer.Value)}");
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
            // Assume forceMap enemies are already placed and shouldn't be placed again.
            Dictionary<uint, bool> singletons = infos.Values.Where(i => i.HasTag("singleton")).ToDictionary(i => i.ID, i => reverseForceMap.ContainsKey(i.ID));

            // TODO: Order this from most important to least important classes, mainly for the remaining singleton cases
            foreach ((EnemySiloType siloInfo, EnemySilo silo) in perm.Silos)
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

                perm.AnyRandomized = true;

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
                List<uint> sources = silo.Sources;
                if (sources.Count == 0)
                {
                    // TODO: Fix silo
                    sources = infos.Values.Where(i => siloClass(i.Class) == siloType).Select(i => i.ID).ToList();
                    if (sources.Count == 0) throw new Exception($"Can't find any enemies to use for default pool for {siloType}");
                }
                if (preset != null)
                {
                    List<uint> removedSources = sources
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
                    if (customPools != null && silo.SiloType.Area.Type == AreaSiloType.DLC)
                    {
                        // TODO: Add logic to Preset itself?
                        bool dlcSilo = silo.SiloType.Area.IsDlc();
                        PoolAssignment rewrite(PoolAssignment assign)
                        {
                            assign = assign.Copy();
                            assign.PoolGroups = assign.PoolGroups.Select(ids => ids.Where(id => infos[id].DLC == dlcSilo).ToList()).ToList();
                            return assign;
                        }
                        customPools = customPools.Select(rewrite).ToList();
                        if (customPools.All(RandomSources.IsPoolEmpty))
                        {
                            // TODO: Maybe handle this differently based on preset contents, e.g. Oops All. For now avoid having an empty RandomSources and add warning.
                            Console.WriteLine($"Ignoring configuration for {silo.SiloType} enemies because it's empty after filtering enemies based on DLC silo");
                            customPools = null;
                        }
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


                List<uint> targets = silo.Targets;
                List<(uint, uint)> swapTargets = new();
                HashSet<uint> poolNorandom = new();
                Shuffle(makeRandom(), targets);
                int sourceIndex = 0;
                for (int i = 0; i < silo.Targets.Count; i++)
                {
                    uint target = silo.Targets[i];
                    uint source;
                    if (adds != null && infos[target].Add != 0)
                    {
                        source = adds.Next(singletons, target);
                    }
                    else if (custom != null)
                    {
                        source = custom.Next(singletons, target);
                    }
                    else
                    {
                        // Should use common routine for handling singletons
                        source = RandomSources.Next(singletons, sources, ref sourceIndex);
                    }
                    if (source <= 0)
                    {
                        source = target;
                        poolNorandom.Add(source);
                    }
                    silo.Mapping[target] = source;
                    if (swapHelperMapping.TryGetValue(source, out List<uint> helperSources))
                    {
                        foreach (uint helper in helperSources)
                        {
                            swapTargets.Add((target, helper));
                        }
                    }
                }
                if (siloType == EnemyClass.Boss) printPermutation(silo);

                if (swapTargets.Count > 0 && allSwapHelpers.Count > 0)
                {
                    List<uint> swapSources = allSwapHelpers.ToList();
#if DEBUG
                    Console.WriteLine($"Found {swapSources.Count} sources and {swapTargets.Count} targets for {siloType} swapboss");
#endif
                    Shuffle(makeRandom(true), swapSources);
                    Shuffle(makeRandom(true), swapTargets);

                    int swapSourceIndex = 0;
                    for (int i = 0; i < swapTargets.Count; i++)
                    {
                        uint swapSource = swapSources[(swapSourceIndex++) % swapSources.Count];
                        silo.SwapMapping[swapTargets[i]] = swapSource;
                        // Console.WriteLine($"Swapping {ename(swapTargets[i].Item2)}: source {ename(swapSource)} in {siloType}");
                    }
                }

                // Fixup pass
                // Although exclude basic group if it's a custom pool, since right now it's only small heuristics that may interfere with challenge modes
                List<uint> targetsToFix = poolNorandom.Count == 0 ? silo.Targets.ToList() : silo.Targets.Except(poolNorandom).ToList();
                Shuffle(makeRandom(), targetsToFix);
                bool bigClass = siloType == EnemyClass.Basic || siloType == EnemyClass.Wildlife;
                if ((custom == null && adds == null) || (!game.Sekiro || siloType != EnemyClass.Basic))
                {
                    fixupPass(silo, targetsToFix, false);
                }

                if (siloType == EnemyClass.Boss && opt["phases"])
                {
                    printPermutation(silo);
                    List<uint> targetsBySize = targetsToFix.OrderByDescending(t => infos[silo.Mapping[t]].SourcePhases).ToList();
                    fixupPass(silo, targetsBySize, true);
                }

                // No logic for tempBosses, just copy
                foreach (uint tempTarget in silo.TempTargets)
                {
                    if (silo.Mapping.ContainsKey(tempTarget)
                        || !tempBosses.TryGetValue(tempTarget, out uint target)
                        || !silo.Mapping.TryGetValue(target, out uint source))
                    {
                        continue;
                    }
                    silo.Mapping[tempTarget] = source;
                    // Try to infer presence of helpers to swap
                    if (swapTargets.Count > 0 && owners.TryGetValue(source, out List<uint> helpers))
                    {
                        foreach (uint helper in helpers)
                        {
                            if (silo.SwapMapping.TryGetValue((target, helper), out uint swapHelper))
                            {
                                silo.SwapMapping[(tempTarget, helper)] = swapHelper;
                            }
                        }
                    }
                }
            }

#if DEV
            if (opt["crashmap"])
            {
                string crashmap = "m16_00_00_00";
                Dictionary<uint, string> models = new();
                EnemySilo silo = perm.Silos[EnemySiloType.Of(EnemyClass.Basic)];
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.Map != crashmap || info.Class != EnemyClass.Basic) continue;
                    if (!silo.Mapping.TryGetValue(info.ID, out uint sourceId))
                    {
                        Console.WriteLine($"Vanilla: {info.DebugText}");
                        sourceId = info.ID;
                    }
                    EnemyData data = defaultData[sourceId];
                    models[info.ID] = data.Model;
                }
                Console.WriteLine($"{models.Values.Distinct().Count()} models, {models.Count} targets");
                int count = 0;
                MSB1 msb = game.DS1Maps[crashmap];
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.Map != crashmap || info.Class != EnemyClass.Basic) continue;
                    if (!silo.Mapping.TryGetValue(info.ID, out uint sourceId)) sourceId = info.ID;
                    EnemyData data = defaultData[sourceId];
                    // models.Add(data.Model);
                    EnemyData tdata = defaultData[info.ID];
                    MSB1.Part.Enemy e = msb.Parts.Enemies.Find(e => e.EntityID == info.ID);
                    // Vector3 near = new Vector3(84.000f, -162.500f, 0.000f);
                    Vector3 near = msb.Parts.Enemies.Find(e => e.EntityID == 2800601).Position;
                    // if (tdata.Model == "c2390" || tdata.Model == "c2670")
                    // if (count++ % 5 == 0)
                    // For drake 2800601, 110 not fine, 112 not fine, 113 fine, 115 fine
                    // if (Vector3.Distance(e.Position, near) < 112f)
                    // if (info.ID == 1600301 || info.ID == 1600361 || info.ID == 1600401)
                    if (info.ID == 1600401)
                    // if (false)
                    {
                        Console.WriteLine(info.ID);
                        silo.Mapping[info.ID] = info.ID;
                        models[info.ID] = tdata.Model;
                    }
                }
                Console.WriteLine($"{models.Values.Distinct().Count()} models, {models.Count} targets");
            }
#endif

            // ---
            // --- Write to spoiler logs
            // ---

            foreach (EnemyClass siloType in new[] { EnemyClass.Boss, EnemyClass.Miniboss, EnemyClass.Basic })
            {
                if (opt["silent"]) break;
                // if (siloType != EnemyClass.Boss) return null;
                Console.WriteLine($"-- {siloType} placements");
                string fullName(uint ent, bool target)
                {
                    if (missingInfos.Contains(ent)) return $"missing #{ent}";
                    EnemyData data = defaultData[ent];
                    EnemyInfo info = infos[ent];
                    string name = info.ExtraName ?? game.ModelCharacterName(data.Model, data.Char);
                    string cat = info.Category;
                    if (cat != null) name = $"{PhraseRe.Split(cat)[0]} {name}";
                    else if (target && game.DS1 && info.HasTag("gravelord")) name = $"Black Phantom {name}";
                    string mapId = game.Locations[data.MainMap];
                    // TODO: Low-level map id, but then it can't use location stuff without transformation
                    if (!game.LocationName(mapId, out string loc, pos: data.Pos)) loc = mapId;
                    return $"{name} (#{ent}) {(target ? "in" : "from")} {loc}";
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
                    if (!game.Sekiro) 
                    {
                        printSilos.Add(EnemyClass.HostileNPC);
                    }
                    if (game.EldenRing)
                    {
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
                // Originally was from entity id, but for bosses, we'd like (scaling level, -entity id),
                // and for basic, (map, entity id). Use a helper object to communicate this.
                // Boss ids usually decrease from side to major bosses, although e.g. Fortissax is an exception.
                // TODO, just use a struct...?
                SortedDictionary<(int, long, uint), uint> printMapping = new();
                Dictionary<uint, int> sortScalingSections = ann.ScalingSections ?? new();
                // Hopefully constructing this should be worth the cost of avoiding thousands of string keys (did not benchmark)
                Dictionary<string, int> mapIndices = maps.Keys.OrderBy(x => x).Select((x, i) => (x, i)).ToDictionary(e => e.Item1, e => e.Item2);
                foreach (EnemyClass printSilo in printSilos)
                {
                    if (!isRandomized(printSilo))
                    {
                        continue;
                    }
                    foreach (KeyValuePair<uint, uint> transfer in perm.GetSiloMappings(printSilo))
                    {
                        uint targetId = transfer.Key;
                        long keyId = targetId;
                        if (autoForce.Contains(targetId)) continue;
                        uint dupeId = 0;
                        if (anyDupeEnabled)
                        {
                            if (infos.TryGetValue(targetId, out EnemyInfo ti) && ti.DupeFrom > 0)
                            {
                                dupeId = targetId;
                                targetId = ti.DupeFrom;
                                keyId = targetId;
                            }
                        }
                        int grouping;
                        if (siloType == EnemyClass.Basic)
                        {
                            grouping = mapIndices.TryGetValue(defaultData[targetId].MainMap, out int index) ? index : int.MaxValue;
                        }
                        else
                        {
                            grouping = sortScalingSections.TryGetValue(targetId, out int section) ? section : 99;
                            // TODO: Which heuristics are better for which games? ER decreasing, DS1 increasing
                            if (game.EldenRing) keyId = -targetId;
                        }
                        printMapping[(grouping, keyId, dupeId)] = transfer.Value;
                    }
                }
                bool printScale = opt["scale"] && ann.ScalingSections != null;
                foreach ((var target, uint sourceId) in printMapping)
                {
                    uint targetId = (uint)Math.Abs(target.Item2);
                    if (anyDupeEnabled && target.Item3 > 0) targetId = target.Item3;
                    string scale = "";
                    if (printScale
                        && getScalingSections(sourceId, targetId, out int sourceSection, out int targetSection))
                    {
                        if (targetSection != sourceSection)
                        {
                            scale = $" (scaling {sourceSection}->{targetSection})";
                        }
                        else
                        {
                            scale = $" (scaling {sourceSection})";
                        }
                    }
                    Console.WriteLine($"Replacing {fullName(targetId, true)}: {fullName(sourceId, false)}{scale}");
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

            state.Permutation = perm;

            // ---
            // --- Enemy position logic, used in the main enemy randomizer pass and in event scripts which need to transplant regions/generators/etc
            // ---

            uint getPlaceTarget(uint target)
            {
                EnemyInfo info = infos[target];
                if (info.DupeFrom > 0)
                {
                    info = infos[info.DupeFrom];
                }
                if (game.DS1)
                {
                    // Use original O&S for all O&S
                    if (info.ID == 1510801 || info.ID == 1510811)
                    {
                        return 1510800;
                    }
                    return info.ID;
                }
                if (info.NextPhase > 0 && !info.HasTag("uniquearena"))
                {
                    return info.NextPhase;
                }
                return info.ID;
            }
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
            Dictionary<(uint, uint, string), uint> regionCopyCache = new();
            Dictionary<uint, RegionTarget> copyRegions(
                string spec,
                uint fromEntity,
                List<uint> args,
                RelocMap reloc,
                bool replace = false)
            {
                string[] words = spec.Split(' ');
                if (words.Length < 2) throw new Exception($"Internal error: malformed region identifier {spec}");
                string scope = words[0];
                string type = words[1];
                List<uint> regions = words.Skip(2).Select(w => uint.Parse(w)).ToList();
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
                else if (scope == "copy" || scope == "place")
                {
                    // This is handled in config processing stage
                    return new();
                }
                else throw new Exception($"Internal error: unknown scope in {spec}");
                string fromMap = defaultData[fromEntity].MainMap;
                if (defaultData[fromEntity].DupeMap != null)
                {
                    throw new Exception($"Error: {fromEntity} regions can't be copied as it exists in {string.Join(", ", defaultData[fromEntity].Maps)}");
                }
                uint toEntity = reloc[EventValue.Enemy(fromEntity)].UIntID;
                string toMap = defaultData[toEntity].MainMap;
                TEnemy e = enemyEditor.GetEnemy(maps[toMap], toEntity);
                TEnemy eSource = enemyEditor.GetEnemy(maps[fromMap], fromEntity);
                if (e == null) throw new Exception($"Internal error: can't find {toEntity} in {toMap} for {spec}");
                if (eSource == null) throw new Exception($"Internal error: can't find {fromEntity} in {fromMap} for {spec}");
                // Create mapping
                Dictionary<uint, RegionTarget> res = new();
                // Note, toInfo may not exist if fromEntity is a helper
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
                        if (type.Contains("gen") || (game.Sekiro && type.Contains("chr")) || (game.EldenRing && (type == "chrpoint" || type == "chrrel")))
                        {
                            // Rewrite randomly or with strafe offset
                            dupeIndex = toInfo.DupeIndex;
                        }
                        else
                        {
                            return res;
                        }
                    }
                    expectArena = places == null && toInfo.IsArenaTarget;
                }
                bool useExisting(uint region)
                {
                    if (regionCopyCache.TryGetValue((toEntity, region, type), out uint existing))
                    {
                        // Console.WriteLine($"Using cached region: {fromEntity} -> {toEntity} and {region} for {spec}");
                        res[region] = RegionTarget.ID(existing);
                        return true;
                    }
                    return false;
                }
                void setCacheableRegion(uint region, uint newone)
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
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, List<TRegion> bs, uint bID) = enemyEditor.MakeCrossMapRegionCopy(newEntity, fromMap, toMap, region, replace);
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
                else if (type.StartsWith("chrrel"))
                {
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        Vector3 sourcePos = eSource.Position;
                        Vector3 sourceRot = eSource.Rotation;
                        if (infos[fromEntity].TryGetArena("relbase", out Arena relbase))
                        {
                            // Just for Moonlight Butterfly
                            sourcePos = relbase.Pos;
                            sourceRot = relbase.Rot;
                        }
                        (TRegion a, List<TRegion> bs, uint bID) = enemyEditor.MakeCrossMapRegionCopy(newEntity, fromMap, toMap, region, replace);
                        foreach (TRegion b in bs)
                        {
                            // Get the relative position from chr to region a
                            Vector3 relPos = Vector3.Subtract(a.Position, sourcePos);
                            relPos = Vector3.Transform(relPos, Matrix4x4.CreateFromYawPitchRoll(-sourceRot.Y * (float)Math.PI / 180, 0, 0));
                            Vector3 offPos = Vector3.Transform(relPos, Matrix4x4.CreateFromYawPitchRoll(e.Rotation.Y * (float)Math.PI / 180, 0, 0));
                            offPos = Vector3.Add(offPos, e.Position);
                            b.Position = offPos;
                            b.Rotation = new Vector3(0, a.Rotation.Y - sourceRot.Y + e.Rotation.Y, 0);
                            if (type == "chrrelbuffer" && b.Shape is MSB.Shape.Box relbox)
                            {
                                b.Position -= floorBuffer;
                                relbox.Height += floorBuffer.Y;
                            }
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
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        (List<TRegion> bs, uint newGen) = enemyEditor.MakeCrossMapGeneratorCopy(newEntity, fromMap, toMap, region, generators, reloc);
                        if (bs != null)
                        {
                            foreach (TRegion b in bs)
                            {
                                // Generator positions can be the same for dupes
                                if (isDupe) break;
                                if (b == null) continue;
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
                else if (type.StartsWith("placegen"))
                {
                    if (places == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}: no placement data");
                    float height = 0;
                    string remainder = type.Replace("placegen", "");
                    if (remainder == "student")
                    {
                        height = 5;
                    }
                    uint placeTarget = getPlaceTarget(toEntity);
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        (List<TRegion> bs, uint newGen) = enemyEditor.MakeCrossMapGeneratorCopy(newEntity, fromMap, toMap, region, generators, reloc);
                        if (bs != null)
                        {
                            for (int i = 0; i < bs.Count; i++)
                            {
                                TRegion b = bs[i];
                                // These are kept null so placement indices can line up
                                if (b == null) continue;
                                EnemyPlacement.Entry placement = places.GetEntry(placeTarget, region, i);
                                if (dupeIndex != -1)
                                {
                                    // Ignore placement
                                    b.Position += getQuadrantOffset(b.Rotation, 0.3f, dupeIndex);
                                }
                                else if (placement != null)
                                {
                                    b.Position = placement.Position;
                                    b.Rotation = placement.Rotation;
                                }
                                else
                                {
                                    b.Position = e.Position;
                                    b.Rotation = e.Rotation;
                                    // Still Needed for DLC. TODO deduplicate, eventually remove
                                    if (remainder == "student")
                                    {
                                        // 4 in each direction is probably fine. Becomes an issue with Makar
                                        float spread() => ((float)arenaRandom.NextDouble() - 0.5f) * 8;
                                        b.Position += new Vector3(spread(), 0, spread());
                                    }
                                }
                                b.Position += new Vector3(0, height, 0);
                            }
                            setCacheableRegion(region, newGen);
                        }
                    }
                }
                else if (type == "placepoint")
                {
                    if (places == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}: no placement data");
                    uint placeTarget = getPlaceTarget(toEntity);
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, List<TRegion> bs, uint bID) = enemyEditor.MakeCrossMapRegionCopy(newEntity, fromMap, toMap, region, replace);
                        TRegion b = bs[0];
                        setCacheableRegion(region, bID);
                        EnemyPlacement.Entry placement = places.GetEntry(placeTarget, region);
                        if (dupeIndex >= 0)
                        {
                            b.Position += getQuadrantOffset(b.Rotation, 0.3f, dupeIndex);
                        }
                        else if (placement != null)
                        {
                            b.Position = placement.Position;
                            b.Rotation = placement.Rotation;
                        }
                        else
                        {
                            b.Position = e.Position;
                            b.Rotation = e.Rotation;
                        }
                    }
                }
                // TODO: Make arenas work alongside isDupe
                else if (type == "arenagen")
                {
                    if (toInfo == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity}, no data found for target {toEntity}");
                    Arena arena = toInfo.ArenaData;
                    if (expectArena && arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        (List<TRegion> bs, uint newGen) = enemyEditor.MakeCrossMapGeneratorCopy(newEntity, fromMap, toMap, region, generators, reloc);
                        if (bs != null)
                        {
                            foreach (TRegion b in bs)
                            {
                                if (b == null) continue;
                                b.Position = arena == null ? e.Position : randomSafePoint(arena, toInfo.ArenaExcludeData, e.Position);
                            }
                            setCacheableRegion(region, newGen);
                        }
                    }
                }
                else if (type.StartsWith("arena"))
                {
                    if (toInfo == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity}, no data found for target {toEntity}");
                    Arena arena = toInfo.ArenaData;
                    if (expectArena && arena == null) throw new Exception($"Can't relocate '{spec}' from {fromEntity} to {toEntity}; it has no arena bounds defined");
                    if (toInfo.ExtraArenaData != null && toInfo.ExtraArenaData.TryGetValue("check", out List<Arena> checkArenas))
                    {
                        // For specific types, for Wolnir, partition checks will not work at all
                        if (type == "arenapartition")
                        {
                            arena = checkArenas.First();
                        }
                    }
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
                            uint region = regions[regionIndex++];
                            if (useExisting(region)) continue;
                            (TRegion a, List<TRegion> bs, uint bID) = enemyEditor.MakeCrossMapRegionCopy(newEntity, fromMap, toMap, region, replace);
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
                        if (toInfo.ArenaExcludeData != null)
                        {
                            foreach (Arena exclude in toInfo.ArenaExcludeData)
                            {
                                uint region = regions[regionIndex++];
                                if (useExisting(region)) continue;
                                (TRegion a, List<TRegion> bs, uint bID) = enemyEditor.MakeCrossMapRegionCopy(newEntity, fromMap, toMap, region, replace);
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
                    foreach (uint region in regions)
                    {
                        if (useExisting(region)) continue;
                        (TRegion a, List<TRegion> bs, uint bID) = enemyEditor.MakeCrossMapRegionCopy(newEntity, fromMap, toMap, region, replace);
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
                            if (type == "arenafull" && toInfo.ArenaRegion > 0)
                            {
                                TRegion fullArena = enemyEditor.GetRegion(maps[toMap], toInfo.ArenaRegion);
                                if (fullArena == null) throw new Exception($"{toEntity}'s region {toInfo.ArenaRegion} not found");
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
                                    b.Position = randomSafePoint(arena, toInfo.ArenaExcludeData, e.Position);
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
                            b.Position = randomSafePoint(subArena, toInfo.ArenaExcludeData, e.Position);
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
                    foreach (uint region in regions)
                    {
                        res[region] = RegionTarget.Dist(dist);
                    }
                }
                else throw new Exception($"Unknown region spec: {spec}");
                return res;
            }

            // ---
            // --- Fix up and standardize NPC params. Do this before duplicating them based on variations or specific enemy placement.
            // ---

            ParamDictionary Params = game.Params;

#if DEV
            if (EnemyDump.WriteNpcInfo(game, opt, infos, defaultData))
            {
                return null;
            }
#endif

            NpcEdits npcs = state.NpcEdits = enemyEditor.InitialNpcPass(state, opt);

            // ---
            // --- Preemptively make copies of all bosses as basic enemies and minibosses
            // ---

            // Probably should live in NpcEdits in some form
            Dictionary<uint, int> bossAsBasicNpc = new();
            Dictionary<uint, int> bossAsBasicThink = new();
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
                uint source = info.ID;
                int baseNpc = defaultData[source].NPC;
                PARAM.Row row = npcs.DuplicateAdjacentNpc(baseNpc);
                if (game.Sekiro)
                {
                    int amt = info.HasTag("early") ? reward.Next(50, 100) : (info.HasTag("mid") ? reward.Next(100, 150) : reward.Next(100, 200));
                    row["getSoul"].Value = info.SourcePhases * amt;
                    // XP - more lenient or strict values, closer to minibosses or to regular enemies. Note that many bosses can just fall off and die pretty easily. So do something in between
                    // amt = info.HasTag("early") ? reward.Next(100, 200) : (info.HasTag("mid") ? reward.Next(150, 250) : reward.Next(200, 300));
                    // amt = info.HasTag("early") ? reward.Next(25, 50) : (info.HasTag("mid") ? reward.Next(50, 75) : reward.Next(50, 100));
                    amt = info.HasTag("early") ? reward.Next(50, 100) : (info.HasTag("mid") ? reward.Next(100, 150) : reward.Next(150, 200));
                    row["skillPoint"].Value = (uint)(info.SourcePhases * amt / (opt["splitskills"] ? 5 : 1));
                }
                else if (game.DS3)
                {
                    // TODO: This is too low
                    row["getSoul"].Value = info.HasTag("early") ? reward.Next(100, 500) : (info.HasTag("mid") ? reward.Next(500, 1000) : reward.Next(1000, 2000));
                }
                else if (game.EldenRing && !info.HasTag("nosouls"))
                {
                    // Note this should also be scaled up/down.
                    // This used to be 800-1200, but the default bosshp option means 25% reduction.
                    // This could be 400-500 after that (1600-2000), or 800-1000 for major bosses.
                    // Try to fix falling enemies before that (e.g. mountaintops hand, faroth enemy)
                    double amt = reward.Next(1200, 1600);
                    if (info.Class == EnemyClass.Boss) amt *= 2;
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
                else if (game.DS1)
                {
                    double amt = reward.Next(800, 1500);
                    row["getSoul"].Value = (int)amt;
                }
                row["disableInitializeDead"].Value = (byte)0;
                row["disableRespawn"].Value = (byte)0;
                if (game.DS3)
                {
                    // Marker speffect for non-boss instances, for use in manual AI scripts
                    // There are no easy slots for doing this in Elden Ring
                    row["spEffectId20"].Value = 6969;
                }
                if (game.EldenRing)
                {
                    // Could alternatively set if != 1, to avoid non-bosses making boss music
                    if ((uint)row["threatLv"].Value == 0)
                    {
                        row["threatLv"].Value = 1;
                    }
                    row["isSoulGetByBoss"].Value = (byte)0;
                }
                bossAsBasicNpc[source] = row.ID;

                if (game.EldenRing || game.DS3 || game.DS1)
                {
                    int baseThink = defaultData[source].Think;
                    PARAM.Row think = Params["NpcThinkParam"][baseThink];
                    // Make overworld instances of bosses less able to track you down
                    // Try to not to nerf nose too much, in case their sight is not great
                    if (think != null)
                    {
                        think = npcs.DuplicateAdjacentThink(baseThink);
                        think["nose_dist"].Value = Math.Min((ushort)think["nose_dist"].Value, (ushort)5);
                        think["eye_dist"].Value = Math.Min((ushort)think["eye_dist"].Value, (ushort)30);
                        bossAsBasicThink[source] = think.ID;
                    }
                }
            }

            // Lazily make copies of regular enemies as specific bosses/minibosses, will basically only apply in 'oops all' mode.
            // These are not cached currently, but there are a finite number of bosses in the game.
            List<string> basicBuffFields = game.Sekiro
                ? new List<string> { "skillPoint", "getSoul", "hp", "stamina", "staminaRecoverBaseVel" }
                : new List<string> { "getSoul", "hp" };
            SortedSet<uint> noDeathblow = ann.GetGroup("nodeathblow");
            int getBasicAsBoss(uint source, uint target)
            {
                int baseNpc = defaultData[source].NPC;

                if (!npcs.TryGetNpc(defaultData[target].NPC, out PARAM.Row targetRow))
                {
                    return baseNpc;
                }

                PARAM.Row row = npcs.DuplicateAdjacentNpc(baseNpc);
                // Just make a new row every time, and also do speffect-based warn
                infos.TryGetValue(target, out EnemyInfo targetInfo);
                if (game.EldenRing
                    ? presetOpt("bosshp", true)
                    : (preset != null && preset.BuffBasicEnemiesAsBosses))
                {
                    foreach (string field in basicBuffFields)
                    {
                        object newVal = targetRow[field].Value;
                        // Slightly buff enemies as bosses by taking geometric mean of HP values
                        // Can this boilerplate be improved in any way
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
                        else if (row[field].Value is ushort sourceUshort && targetRow[field].Value is ushort targetUshort)
                        {
                            if (targetUshort > sourceUshort) newVal = (ushort)Math.Sqrt(sourceUshort * targetUshort);
                        }
                        else throw new Exception($"Unknown NpcParam field type {field} {newVal.GetType()}");
                        row[field].Value = newVal;
                    }
                    if (game.Sekiro && !noDeathblow.Contains(source) && targetInfo != null && targetInfo.IsImportantTarget)
                    {
                        row["ninsatuNum"].Value = targetInfo.IsBossTarget ? (byte)targetInfo.Phases : targetRow["ninsatuNum"].Value;
                    }
                }
                if (targetInfo != null && targetInfo.IsFixedSource)
                {
                    row["getSoul"].Value = 0;
                }
                row["disableInitializeDead"].Value = (byte)1;
                return row.ID;
            }

            state.NpcEdits = npcs;

            // ---
            // --- Main enemy randomizer loop for mutating map data
            // ---

            EnemyMapping mapping = new(enemies);
            int helperModelBase = 100;
            // Mapping from boss to speffect holder if dupe enabled
            // Currently unused as the main boss enemy is used for this.
            // TODO: Needed for Godskin Snail
            Dictionary<uint, uint> spEffectHolders = new();

            Dictionary<uint, int> totalTargetCounts = perm.Silos.Values.SelectMany(s => s.Mapping).GroupBy(e => e.Value).ToDictionary(e => e.Key, e => e.Count());
            bool withinMaxAllowed(uint source, uint target, int amount, bool lenient = false)
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
            HashSet<uint> eldenHelperBosses = new()
            {
                // TODO: Any way to automate these? Helper count? Amounts are made up.
                // Also based on map density, maybe
                // Rennala 2
                1400080,
                // Malenia
                15000800,
                // Yelough Astel
                32110800,
                // Messmer 2. TODO should be partially enabled
                21010800,
                // Metyr
                25000800,
                // Lamenter
                41020800,
                // Cerulean Ghostflame Dragon
                2048380850,
                // Count Ymir
                2051450720,
                // Jori
                2052430800,
                // PCR I guess
                20010800,
                // Base game Radahn, NPC is unnecessary
                1052385800,
                // Scadutree Avatar 2050480800, currently difficult to make partial
            };
            // Probably fine?
            int eldenMulti = opt["dlc"] ? 100 : 50;
            bool enableMultichr(uint source, uint target)
            {
                if (!multichrSwitch) return false;
                // Allow splits, although this may get unwieldy
                if (source == infos[target].SplitFrom)
                {
                    // It's too much for Gideon, who comes with many c0000s
                    if (source == 11050850) return false;
                    // 10 Jori is too much, but 5 is mostly functional
                    if (source == 2052430800 && infos[target].DupeIndex >= 4) return false;
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
                else if (game.DS1)
                {
                    // Hardcode this for now (Nito -> Asylum Demon) as giant skeletons in back are brutal.
                    if (source == 1310800 && target == 1810800) return false;
                }
                else if (game.EldenRing)
                {
                    if (infos[target].IsImportantTarget) return true;
                    if (infos[source].HasTag("bossonlymulti")) return false;
                    // Mariners, many helpers
                    if (infos[source].HasTag("mariner") && !withinMaxAllowed(source, target, 20)) return false;
                    // Random amount basically
                    if (eldenHelperBosses.Contains(source) && !withinMaxAllowed(source, target, eldenMulti)) return false;

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
            HashSet<uint> usedGlobalSources = new();
            HashSet<uint> usedHelpers = new();
            // NPC -> threat lvs
            SortedDictionary<int, List<uint>> npcThreatTargets = new SortedDictionary<int, List<uint>>();

            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                ModelEditor models = enemyEditor.GetModelEditor(msb);
                HashSet<string> removedObjects = new();
                Dictionary<uint, List<TEnemy>> dupedHelpers = new();

                foreach (EnemySilo silo in perm.Silos.Values)
                {
                    foreach ((uint target, uint source) in silo.Mapping)
                    {
                        // Process targets in this map
                        // This filtering is not very fast, but all maps x all silos is potentially pretty big. Maybe make it anyway
                        if (!defaultData[target].Maps.Contains(entry.Key)) continue;
                        // Treat self-randomizations as none at all. Try to avoid these for bosses/minibosses though, for variety.
                        // Except when dupeEnabled, because normal events are left alone, but segments are still processed
                        if (source == target)
                        {
                            if (dupeEnabled(target))
                            {
                                // TODO: Do nothing if not exist
                                TEnemy eDupe = enemyEditor.GetEnemy(msb, target);
                                EnemyData dataDupe = defaultData[source];
                                enemyEditor.CopyGroups(dataDupe, new(), eDupe, infos[target].RemoveGroup);
                                if (owners.TryGetValue(source, out List<uint> dupeHelpers))
                                {
                                    foreach (uint helper in dupeHelpers)
                                    {
                                        TEnemy e2Dupe = enemyEditor.GetEnemy(msb, helper);
                                        // This happened for 1046360230, a Ballista, when the fake id OwnedBy changed
                                        if (e2Dupe == null) throw new Exception($"Missing {helper} of {target} processing onslaught for {map}");
                                        enemyEditor.CopyGroups(dataDupe, new(), e2Dupe, infos[target].RemoveGroup);
                                    }
                                }
                            }
                            // Could maybe do edits like Putrescent Knight here. Currently done above unconditionally.
                            continue;
                        }
                        EnemyInfo sourceInfo = infos[source];
                        EnemyInfo targetInfo = infos[target];

                        // TODO: Put all specific locations in config as primary arena
                        bool dupeTarget = targetInfo.HasTag("dupe") && targetInfo.DupeFrom == source;

                        EnemyData data = defaultData[source];
                        TEnemy e = enemyEditor.TransplantEnemy(msb, data, target);
                        // TODO: Change TransplantEnemy API for this?
                        if (dupeTarget && e is MSBE.Part.Enemy eldenEnemy)
                        {
                            eldenEnemy.BackupEventAnimID = data.Anim;
                        }
                        if (targetInfo.HasTag("gravelord")
                            ? npcs.ToGravelordNpc.TryGetValue(data.NPC, out int graveNpc)
                            : npcs.FromGravelordNpc.TryGetValue(data.NPC, out graveNpc))
                        {
                            enemyEditor.SetNpcParam(e, npcParam: graveNpc);
                        }
                        if (!targetInfo.IsFixedSource && bossAsBasicNpc.TryGetValue(source, out int sourceNpc))
                        {
                            enemyEditor.SetNpcParam(e, npcParam: sourceNpc);
                        }
                        else if ((targetInfo.IsImportantTarget || target == 28000801) && !sourceInfo.IsBuffSource)
                        {
                            enemyEditor.SetNpcParam(e, npcParam: getBasicAsBoss(source, target));
                        }
                        if (!targetInfo.HasPerceptiveNose && bossAsBasicThink.TryGetValue(source, out int sourceThink))
                        {
                            enemyEditor.SetNpcParam(e, npcThinkParam: sourceThink);
                        }
                        else if (targetInfo.HasTag("invader")
                            ? npcs.NpcAsInvaderThink.TryGetValue(data.Think, out sourceThink)
                            : npcs.NpcAsWorldThink.TryGetValue(data.Think, out sourceThink))
                        {
                            enemyEditor.SetNpcParam(e, npcThinkParam: sourceThink);
                        }
                        // Reuse HasPerceptiveNose for this, since we only want main bosses to get these rewrites, and only if they're relatively unique.
                        // Maybe do this outside this loop. In any case, definitely don't make a new row from this
                        if (targetInfo.IsImportantTarget && sourceInfo.HasPerceptiveNose
                            && npcs.NpcThreatLvs.TryGetValue(defaultData[target].NPC, out uint targetLv))
                        {
                            if (npcs.TryGetNpc(data.NPC, out PARAM.Row npcRow) && (uint)npcRow["threatLv"].Value <= 1)
                            {
                                AddMulti(npcThreatTargets, data.NPC, targetLv);
                            }
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

                        AddMulti(mapping.Mapping, source, target);
                        mapping.RevMapping[target] = source;
                        if (!ownerMap.TryGetValue(target, out string eventMap)) throw new Exception($"Internal error: no event map for {target}");
                        bool targetMultichr = enableMultichr(source, target);

                        Dictionary<uint, uint> groupMapping = new();
                        if (sourceInfo.Groups != null)
                        {
                            foreach (uint group in sourceInfo.Groups)
                            {
                                if (targetMultichr)
                                {
                                    uint group2 = newGroupEntity();
                                    groupMapping[group] = group2;
                                    mapping.HelperMapping[(target, group)] = group2;
                                    ownerMap[group2] = eventMap;
                                }
                                else
                                {
                                    // Just use target directly in lieu of groups, if needed
                                    mapping.HelperMapping[(target, group)] = target;
                                }
                            }
                        }
                        if (sourceInfo.BuddyGroup > 0 && targetInfo.BuddyGroup > 0)
                        {
                            // Make sure helpers with the same BuddyGroup as the source automatically get the target BuddyGroup
                            // This is redundant for the main entities
                            groupMapping[sourceInfo.BuddyGroup] = targetInfo.BuddyGroup;
                        }
                        Dictionary<uint, uint> mainGroupMapping = groupMapping;
                        if (targetInfo.BuddyGroup > 0 && targetInfo.NextPhase > 0)
                        {
                            // We really do need to make sure this happens, even for basic-to-boss without a source BuddyGroup
                            // However, only do this transformation for the main enemy.
                            // TODO: This is pretty bad, make a more coherent set of MapEditor group utility functions.
                            mainGroupMapping = groupMapping.ToDictionary(en => en.Key, en => en.Value);
                            mainGroupMapping[0] = targetInfo.BuddyGroup;
                        }
                        enemyEditor.CopyGroups(data, mainGroupMapping, e, targetInfo.RemoveGroup);
                        HashSet<uint> preserveGroups = null;
                        if (game.EldenRing && defaultData[target].Group != null)
                        {
                            // Extend group mapping to include Carian Study Hall groups, so helpers get added to it to
                            // This case is exceptional since it is used for a broad enable/disable
                            if (defaultData[target].Group.Contains(34115150))
                            {
                                preserveGroups = new() { 34115150 };
                            }
                            if (defaultData[target].Group.Contains(34115160))
                            {
                                preserveGroups = new() { 34115160 };
                            }
                        }

                        // TODO: Gradually migrate positions to enemy config or placement data

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
                            if (targetInfo.DragonArenaData != null && targetInfo.DragonTrees != null)
                            {
                                e.Position = targetInfo.DragonArenaData.Pos;
                                e.Rotation = targetInfo.DragonArenaData.Rot;
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
                            if (sourceInfo.IsBossTarget || sourceInfo.EnemyType == "chainedogre")
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
                        if (!dupeTarget && targetInfo.ExtraArenaData != null
                            && (targetInfo.TryGetArena("center", out Arena primary) || targetInfo.TryGetArena("primary", out primary)))
                        {
                            e.Position = primary.Pos;
                            e.Rotation = primary.Rot;
                            // Hack for avoiding patrol point inside restricted area in this one case (Garrew). Can be done at end as well
                            if (target == 2047450800 && msb is MSBE msbe)
                            {
                                MSBE.Event.PatrolInfo patrol = msbe.Events.PatrolInfo.Find(e => e.Name == "walk_route_c5840_9002_0");
                                if (patrol != null)
                                {
                                    patrol.WalkRegionNames[0] = patrol.WalkRegionNames[1];
                                }
                            }
                        }
                        // More height for floating enemies which would otherwise be stuck in the ground.
                        // May be overridden by boss-specific placement pairs.
                        if (sourceInfo.HasTag("floating"))
                        {
                            e.Position += new Vector3(0, 1.5f, 0);
                            // e.Position += new Vector3(0, 1.5f, 0);
                        }
                        if (dupeTarget && sourceInfo.TryGetArena("dupe", out Arena dupe))
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
                        if ((source == 5100201 || (source == 35000800 && !targetInfo.HasTag("back"))) && !dupeTarget)
                        {
                            // Turn Bridge Midir around
                            // Also Mohg 1. This doesn't edit warps, though, so Godrick always turns around twice
                            e.Rotation -= new Vector3(0, 180, 0);
                        }
                        // Finally, precalculated placements
                        uint placeTarget = target;
                        EnemyPlacement.Entry mainPlacement = null;
                        if (!dupeTarget && places != null && placeEnemies.Contains(source) && targetInfo.IsArenaTarget)
                        {
                            placeTarget = getPlaceTarget(target);
                            mainPlacement = places.GetEntry(placeTarget, source);
                            if (mainPlacement != null)
                            {
                                e.Position = mainPlacement.Position;
                                e.Rotation = mainPlacement.Rotation;
                            }
                            else if (game.EldenRing)
                            {
                                // This results in issues for hidden guys like Godskin Duo, so trust in placeEnemies here
                                // Consider just adding default placements for them, not many cases to worry about
                                mainPlacement = new();
                            }
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
                        if (targetInfo.SetColName != null)
                        {
                            string col = targetInfo.SetColName;
                            enemyEditor.SetEnemyCollision(e, col);
                            defaultData[target].Col = col;
                        }

                        // TODO: Evaluate if this should check dupeTarget too
                        if (sourceInfo.HasTag("hidden"))
                        {
                            // Don't allow wooooooo enemies or true monk to walk around, as this interrupts their invisibility
                            if (e is MSBS.Part.Enemy es)
                            {
                                // es.PatrolIndex = -1;
                                es.UnkT20 = -1;
                            }
                            else if (e is MSB3.Part.Enemy e3)
                            {
                                e3.WalkRouteName = null;
                            }
                        }
                        if (!dupeTarget && (targetInfo.HasTag("ignoresp") || (source == 19000800 && targetInfo.Class == EnemyClass.Evergaol)))
                        {
                            // This is required to get Night's Cavalries working on Mountaintops
                            // TODO: Should this be carried with those enemies? Scripting seems to mention it
                            // Also for now, use it to make Elden Beast visible when moved to Evergaols, though the real issue is PhantomParam
                            if (e is MSBE.Part.Enemy ee)
                            {
                                ee.SpEffectSetParamID[0] = 0;
                            }
                        }
                        // Allow extra region features if the enemy stays unique. Or if not unique and not a singleton
                        // (instances won't get messed up other instances' regions), allow a single transplanted boss.
                        bool uniqueTarget = totalTargetCounts.TryGetValue(source, out int uniqueCount) && uniqueCount == 1;
                        if (!uniqueTarget && !sourceInfo.HasTag("singleton") && mapping.RevMapping.ContainsKey(target) && targetInfo.IsImportantTarget)
                        {
                            uniqueTarget = usedGlobalSources.Add(source);
                        }
                        if (sourceInfo.Regions != null && uniqueTarget)
                        {
                            foreach (string spec in sourceInfo.Regions)
                            {
                                copyRegions(
                                    spec, source, new List<uint>(),
                                    RelocMap.Of(EventValue.Enemy(source), EventValue.Enemy(target)),
                                    replace: true);
                            }
                        }
                        // Handling for Moonlight Butterfly
                        if (source == 1200801)
                        {
                            // Require regions present and eligible arena
                            if (uniqueTarget && mainPlacement != null)
                            {
                                EnemyPlacement.Entry spawnPlacement = places.GetEntry(placeTarget, 1202182);
                                if (spawnPlacement == null) throw new Exception("Internal error in place config");
                                e.Position = spawnPlacement.Position;
                                e.Rotation = spawnPlacement.Rotation;
                            }
                            else
                            {
                                // At this late point, turn the boss into a basic enemy
                                enemyEditor.SetNpcParam(e, npcThinkParam: 323001);
                            }
                            // e.Position = new Vector3(457.230f, 36.730f, 262.668f);
                            // e.Rotation = new Vector3();
                        }
                        // Edit large NPCs if they appear in non-large places.
                        // This doesn't need to be especially organized. It's fine if we get more than we need.
                        // TODO: Applies to Elden Ring? It just has frames/allowframes for now
                        bool smallDest = targetInfo.HasTag("exclude:large");
                        if (smallDest && sourceInfo.HasTag("large") && !opt["noshrink"])
                        {
                            npcs.ShrinkNpc(Params["NpcParam"][data.NPC], 1f, 3f);
                        }
                        // Edit perceptive NPCs if they appear in sensitive places
                        // perceptive threshold is 20 and veryperceptive is 100, so don't shrink too much
                        // Update: exclude:perceptive is no longer used, only boss targets are shrunk.
                        if (false && targetInfo.HasTag("exclude:perceptive") && sourceInfo.HasTag("perceptive"))
                        {
                            npcs.ShrinkRange(Params["NpcThinkParam"][data.Think], 30);
                        }

                        if (targetInfo.ArenaData != null)
                        {
                            // Also always add arena, as a debugging tool
                            Arena arena = targetInfo.ArenaData;
                            enemyEditor.MakeArenaRegion(msb, arena, $"Arena for {target}");
                            List<Arena> excludes = targetInfo.ArenaExcludeData;
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
                                        // Note this makes EntityID 0 but it's fine, it's not meant to be used in-game
                                        TEnemy e2 = enemyEditor.CloneEnemy(msb, e, defaultData[2888002], 0, helperModelBase++);
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

                        if (owners.TryGetValue(source, out List<uint> helpers))
                        {
                            Arena arena = targetInfo.ArenaData;
                            foreach (uint helper in helpers)
                            {
                                if (!targetMultichr && !infos[helper].HasTag("alwayschr")) continue;
                                if (infos[helper].Class != EnemyClass.Helper) continue;
                                uint helperSource = helper;
                                (uint, uint) helperKey = (target, helper);
                                if (silo.SwapMapping.TryGetValue(helperKey, out uint swapHelper))
                                {
                                    helperSource = swapHelper;
                                    mapping.SwapMapping[helperKey] = swapHelper;
                                }
                                EnemyData data2 = defaultData[helperSource];
                                if (!mapping.HelperMapping.TryGetValue(helperKey, out uint target2))
                                {
                                    // In the case of DupeMap, this mapping must remain stable
                                    target2 = newEntity();
                                }
                                TEnemy e2 = enemyEditor.CloneEnemy(msb, e, data2, target2, helperModelBase++);
                                enemyEditor.ClearGroups(e2, preserveGroups);
                                if (!targetInfo.IsFixedSource
                                    && bossAsBasicNpc.TryGetValue(helperSource, out int helperNpc))
                                {
                                    enemyEditor.SetNpcParam(e2, npcParam: helperNpc);
                                }
                                if (!targetInfo.HasPerceptiveNose
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
                                    e2.Position = randomSafePoint(arena, targetInfo.ArenaExcludeData, e.Position);
                                    if (game.Sekiro)
                                    {
                                        // Arenas are a bit more of an art than a science, so some extra height for safety.
                                        // In theory we're more careful about this in DS3
                                        extraHeight = 0.5f;
                                    }
                                }
                                else if (mainPlacement != null)
                                {
                                    EnemyPlacement.Entry helperPlacement = places.GetEntry(placeTarget, helper);
                                    if (helperPlacement != null)
                                    {
                                        e2.Position = helperPlacement.Position;
                                        e2.Rotation = helperPlacement.Rotation;
                                    }
                                }
                                else if (source == 12090800)
                                {
                                    // Regal Ancestor Spirit
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
                                        e2.Position = sourceEnemy.Position + getDupeOffset(sourceEnemy.Rotation, target, targetInfo.DupeIndex);
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
                                    npcs.ShrinkNpc(Params["NpcParam"][data2.NPC], 1f, 3f);
                                }
                                models.UseModel(e2.ModelName);
                                mapping.HelperMapping[helperKey] = target2;
                                mapping.RevMapping[target2] = helper;
                                ownerMap[target2] = eventMap;
                                enemyEditor.CopyGroups(data2, groupMapping, e2);
                                EnemyData getOriginalData(EnemyData data)
                                {
                                    // Copy helper associations from original enemy pair, whether randomized or not
                                    // Assume that SplitFrom enemies keep their associations. If not, don't call CopyAssociations at all
                                    // This is mainly for Night's Cav in-place dupe
                                    return infos.TryGetValue(data.ID, out EnemyInfo dataInfo)
                                        && defaultData.TryGetValue(dataInfo.SplitFrom, out EnemyData fromData)
                                        ? fromData : data;
                                }
                                EnemyData original = getOriginalData(data);
                                EnemyData original2 = getOriginalData(data2);
                                // The part names come from the original map, not the current target map
                                enemyEditor.CopyAssociations(maps[original.MainMap], original, original2, msb, e, e2);
                                if (infos[helper].Regions != null && uniqueTarget)
                                {
                                    // There is not really any "default" data for helpers, but this is needed just for the purpose of regions
                                    // TODO can we get rid of this hack
                                    defaultData[target2] = enemyEditor.GetEnemyData(e2, entry.Key);
                                    foreach (string spec in infos[helper].Regions)
                                    {
                                        copyRegions(
                                            spec, helper, new List<uint>(),
                                            RelocMap.Of(EventValue.Enemy(helper), EventValue.Enemy(target2)),
                                            replace: true);
                                    }
                                }
                            }
                        }
                        if (objectOwners.TryGetValue((int)source, out List<string> objHelpers))
                        {
                            string sourceMap = defaultData[source].MainMap;
                            foreach (string objId in objHelpers)
                            {
                                if (!targetMultichr) continue;
                                (TObject e2, uint source2, uint target2) = enemyEditor.MakeCrossMapObjectCopy(
                                    newEntity, defaultData[source].MainMap, entry.Key, objId, defaultData[target], helperModelBase++);
                                if (e2 == null) throw new Exception($"Required object {objId} not found by {ename(source)}");
                                // Position is not set in DS3/Sekiro, TODO see if boss position objects creates issues
                                // (if so, it might create issues at map-transplanted coordinates)
                                if (game.DS1)
                                {
                                    e2.Position = e.Position;
                                    e2.Rotation = e.Rotation;
                                    if (mainPlacement != null)
                                    {
                                        EnemyPlacement.Entry helperPlacement = places.GetEntry(placeTarget, source2);
                                        if (helperPlacement != null)
                                        {
                                            e2.Position = helperPlacement.Position;
                                            e2.Rotation = helperPlacement.Rotation;
                                            // Special case for Bed of Chaos left and right branch as format doesn't store non-Y rotation
                                            if (source2 == 1411122)
                                            {
                                                e2.Rotation = new Vector3(11.659762f, e2.Rotation.Y, 29.42011f);
                                            }
                                            else if (source2 == 1411121)
                                            {
                                                e2.Rotation = new Vector3(-20.350224f, e2.Rotation.Y, 7.604623f);
                                            }
                                        }
                                    }
                                }
                                models.UseObjectModel(e2.ModelName);
                                if (source2 > 0 && target2 > 0)
                                {
                                    mapping.HelperMapping[(target, source2)] = target2;
                                }
                                // Probably fine to not remove original objects or clear ids, but especially in DS1 they will change from flags set elsewhere
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
                        if (owners.TryGetValue(target, out List<uint> helpers2))
                        {
                            // Temporary Elden Ring hack
                            if (game.EldenRing) models.UseModel("c1000");
                            usedHelpers.UnionWith(helpers2);
                        }
                    }
                }
                foreach (KeyValuePair<uint, List<TEnemy>> helperEntry in dupedHelpers)
                {
                    foreach (TEnemy helperEnemy in helperEntry.Value)
                    {
                        helperEnemy.Position += getDupeOffset(helperEnemy.Rotation, helperEntry.Key, -1);
                    }
                }

                // Remove copied enemies that weren't randomized after all
                enemyEditor.RemoveEnemies(msb, id => infos.TryGetValue(id, out EnemyInfo info)
                    && info.SplitFrom > 0 && !info.HasTag("dupe")
                    && !mapping.RevMapping.ContainsKey(info.Class == EnemyClass.Helper && info.OwnedBy > 0 ? info.OwnedBy : id));

                if (msb is MSB3 mb)
                {
                    if (map == "highwall" && opt["colfix"])
                    {
                        // Too many maps loaded at once
                        // However, this breaks some AIs in Vordt's arena
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
                        // Even with crash fix dll, still has issues
                        foreach (MSB3.Part.Collision c in mb.Parts.Collisions)
                        {
                            for (int i = 0; i < c.DrawGroups.Length; i++)
                            {
                                c.BackreadGroups[i] &= c.DrawGroups[i];
                            }
                        }
                    }
                    // TODO: Do this for stairs up to Dragon Barracks. Alternatively, debug crash directly.
                    else if (map == "ringedcity" && opt["colfix"])
                    {
                        // Mapping from collisions which are unnecessarily loaded (DrawGroups) to collisions which see too much (DispGroups)
                        // The main crashes are crossing over to swamp, or dropping down to final judicator
                        // TODO: Restrict this even further
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
                else if (msb is MSB1 m1)
                {
                    // Unlike other games, a few collisions are too strict and cause invisible enemies
                    // Dispgroups use unique bits so ORing them into drawgroups is sensible
                    // Do this mutually for now, unless issues arise
                    void mergeCollisions(string colName1, string colName2)
                    {
                        MSB1.Part.Collision col1 = m1.Parts.Collisions.Find(c => c.Name == colName1);
                        MSB1.Part.Collision col2 = m1.Parts.Collisions.Find(c => c.Name == colName2);
                        if (col1 != null && col2 != null)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                col1.DrawGroups[i] |= col2.DispGroups[i];
                                col2.DrawGroups[i] |= col1.DispGroups[i];
                            }
                            return;
                        }
#if DEBUG
                        throw new Exception($"Missing {colName1} ({col1}) or {colName2} ({col2}) in {map}");
#endif
                    }
                    // Viewing from colName1, make colName2 not load
                    void hideCollision(string colName1, string colName2)
                    {
                        MSB1.Part.Collision col1 = m1.Parts.Collisions.Find(c => c.Name == colName1);
                        MSB1.Part.Collision col2 = m1.Parts.Collisions.Find(c => c.Name == colName2);
                        if (col1 != null && col2 != null)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                col1.DrawGroups[i] &= ~col2.DispGroups[i];
                            }
                            return;
                        }
#if DEBUG
                        throw new Exception($"Missing {colName1} ({col1}) or {colName2} ({col2}) in {map}");
#endif
                    }
                    if (map == "m10_01_00_00")
                    {
                        // Sunlight Altar and room below
                        mergeCollisions("h1000B1", "h1061B1");
                    }
                    else if (map == "m10_00_00_00")
                    {
                        // Depths Kirk hallway
                        mergeCollisions("h0056B0", "h0027B0");
                    }
                    else if (map == "m16_00_00_00")
                    {
                        // Valley of Drakes to inside, but doesn't help invisible enemy issue
                        // hideCollision("h0052B0_0000", "h0031B0_0000");
                    }
                }
                else if (msb is MSBE me)
                {
                    if (map == "m14_00_00_00")
                    {
                        // Mapping from collisions which are unnecessarily loaded (DrawGroups) to collisions which see too much (DispGroups)
                        // This may be unnecessary with RandomizerCrashFix but doesn't hurt
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
                    // Used to be here: Removing ConnectCollisions for Regal Ancestor Spirit crash. It didn't work
                }
                if (removedObjects.Count > 0)
                {
                    // Is this needed given second pass? Only keep in DS3 for now.
                    enemyEditor.GarbageCollect(msb, models.EnemyUsed, removedObjects);
                }
            }

            state.Mapping = mapping;

            // ---
            // --- Cleanup pass to update cross-map metadata and remove obsolete cross-map references
            // ---

            // Finally, remove all randomized helpers
            foreach (KeyValuePair<string, TMap> entry in maps)
            {
                if (!game.Locations.ContainsKey(entry.Key)) continue;
                string map = game.Locations[entry.Key];
                TMap msb = entry.Value;

                enemyEditor.RemoveEnemies(msb, id => usedHelpers.Contains(id) && infos[id].Class == EnemyClass.Helper);
                enemyEditor.GarbageCollect(msb);
                if (!game.DS1)
                {
                    // There's probably some kind of index in DS1 which causes invisible enemies if this is enabled
                    enemyEditor.GetEnemies(msb).Sort((a, b) => a.Name.CompareTo(b.Name));
                }
            }

            // Update dupe map for helpers, as well
            // This is not heavily relied upon - only in loc (rewrite/copyinit) cases where logic applies to helpers.
            // It's not an error for helpers to be absent in those cases, in case something else got randomized there
            foreach (KeyValuePair<uint, List<uint>> entry in dupeEnemyMap.ToList())
            {
                if (owners.TryGetValue(entry.Key, out List<uint> helpers))
                {
                    foreach (uint helper in helpers)
                    {
                        if (infos[helper].Class != EnemyClass.Helper) continue;
                        List<uint> dupeHelpers = new();
                        foreach (uint dupe in entry.Value)
                        {
                            if (mapping.HelperMapping.TryGetValue((dupe, helper), out uint dupeHelper))
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
            // Post-process on assigning threat lvs for bosses, try to do it based on target count (or randomly)
            if (npcThreatTargets.Count > 0)
            {
                Random threatLvRandom = new Random(seed + 400);
                foreach ((int npcId, List<uint> lvs) in npcThreatTargets)
                {
                    if (npcs.TryGetNpc(npcId, out PARAM.Row npcRow) && (uint)npcRow["threatLv"].Value <= 1)
                    {
                        npcRow["threatLv"].Value = opt["bossbgm"] && npcs.ThreatLvs.Count > 0
                            ? Choice(threatLvRandom, npcs.ThreatLvs)
                            : lvs.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
                    }
                }
            }
            // Final pass to apply threatLv to main/combat helpers to avoid e.g. sudden Godskin Snail music
            // Do it in cases threatLv was edited, which is mainly npcThreatTargets and bossbgm
            if (npcThreatTargets.Count > 0 || opt["bossbgm"])
            {
                // Do a dumb thing for now, it can't be worse than the processing above
                // This is explicitly based on sources, and direct usages of source NPCs.
                // NPCs are only duplicated for boss<->basic, and basic has no main helpers, and doesn't need threat levels.
                // but if that changes, important NPCs need to be better tracked.
                foreach (EnemyInfo sourceInfo in infos.Values.Where(i => i.IsImportantTarget))
                {
                    uint source = sourceInfo.ID;
                    if (defaultData.TryGetValue(source, out EnemyData data) && npcs.TryGetNpc(data.NPC, out PARAM.Row row))
                    {
                        uint threatLv = (uint)row["threatLv"].Value;
                        // Similar logic to getHelperTargets, and healthbarcheck fights as well
                        if (threatLv > 1 && owners.TryGetValue(source, out List<uint> helpers))
                        {
                            foreach (uint helper in helpers)
                            {
                                if (infos.TryGetValue(helper, out EnemyInfo inf)
                                    && (inf.HasTag("mainhelper") || inf.HasTag("combathelper"))
                                    && defaultData.TryGetValue(helper, out EnemyData helperData)
                                    && npcs.TryGetNpc(helperData.NPC, out PARAM.Row helperRow))
                                {
                                    uint helperThreatLv = (uint)helperRow["threatLv"].Value;
                                    // Console.WriteLine($"Can rewrite {ename(helper)} {helperData.NPC} in {ename(source)} ({helperThreatLv} -> {threatLv})");
                                    helperRow["threatLv"].Value = threatLv;
                                }
                            }
                        }
                    }
                }
            }

            // ---
            // --- It's emevd time
            // ---

            Dictionary<EventKey, EventSpec> templates = eventConfig.EnemyEvents.ToDictionary(e => e.Key, e => e);
            if (!game.EldenRing && templates.Any(e => e.Key.ID == 0)) throw new Exception($"Internal error: event 0 in config");

            bool disableEvents = false;
            bool partialEventConfig = false;
            bool argEntityAware = game.EldenRing || game.DS1;

            // ---
            // --- Preprocess event config for boss scripts
            // ---

            // Old boss swap system using StartCmd/EndCond
            // Preprocess boss commands which need to move around in Sekiro
            Dictionary<(uint, int), List<EMEVD.Instruction>> endConds = new();
            HashSet<uint> endCond2s = new();
            Dictionary<uint, List<EMEVD.Instruction>> startCmds = new();
            HashSet<uint> removeImmortality = new(infos.Values
                .Where(info => info.IsMortalSekiroBoss)
                .SelectMany(info => mapping.Mapping.TryGetValue(info.ID, out List<uint> targets) ? targets : new()));
            if (game.Sekiro)
            {
                Dictionary<uint, EnemyTemplate> endEvents = templates.Values
                    .Where(ev => ev.Template.Any(t => t.Type.StartsWith("end")))
                    .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("end")));
                // Also startcmds
                Dictionary<uint, EnemyTemplate> startEvents = templates.Values
                    .Where(ev => ev.Template.Any(t => t.Type.StartsWith("start") && t.StartCmd != null))
                    .ToDictionary(ev => ev.ID, ev => ev.Template.Find(t => t.Type.StartsWith("start")));
                foreach (KeyValuePair<string, EMEVD> entry in emevds)
                {
                    foreach (EMEVD.Event e in entry.Value.Events)
                    {
                        uint id = (uint)e.ID;
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
                            foreach (string remove in PhraseRe.Split(start.StartCmd))
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
            Dictionary<uint, Dictionary<string, CommandSegment>> segmentCmds = new();
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

            // Surely this isn't a valid key
            HashSet<(EventKey, uint, List<CommandSegment>)> segmentEntities = new();
            uint defaultSegmentEnemy = 9999999;
            void addSegments(EventKey eventID, uint entity, List<CommandSegment> segments, Instr init = null)
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
                        // Romina hack, TODO verify emevd output
                        if (eventID.Map.EndsWith("_10")) continue;
                        // Allowed for bosses with different first/second encounters
                        if (s.Type == "buff") continue;
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
                        // if (c.Contains("IfConditionGroup")) Console.WriteLine($"Condition segment: {entity} {original.Type}");
                        // placi 13000830, stray mimic uses IfConditionGroup(MAIN, PASS, OR_03). chrnonboss
                    }
#endif
                    entitySegments[s.Type] = s;
                }
            }
            if (eventConfig.DefaultSegments != null && eventConfig.DefaultSegments.Count > 0)
            {
                addSegments(null, defaultSegmentEnemy, eventConfig.DefaultSegments);
            }

            // ---
            // --- Preprocess emevds for other configs/scripts which may be transplanted elsewhere
            // ---

            // Animation info: (entity id) -> (starting animation, wakeup animation)
            Dictionary<uint, (int, int)> startingAnimations = new();
            // Same map from NPC. TODO maybe use this
            Dictionary<int, List<(int, int)>> npcStartingAnimations = new();
            // All BGM ids which are later shuffled around, keyed by defeat flag
            SortedDictionary<int, int> defeatFlagBgmIds = new SortedDictionary<int, int>();
            // DS1: Map from entity id to anim info.
            Dictionary<uint, SpecialAnim> specialAnimations = new();
            // DS1: (caller, callee) nested chr inits. This is for DS1 so map specification isn't especially needed
            HashSet<(int, int)> subInitPairs = new();
            // DS1: Start and end flags for gravelording. Do this based on config even if events don't exist.
            SortedDictionary<string, (int, int)> gravelordFlags = new();
            // Used to collect init data
            HashSet<EventKey> animationEvents = new();
            HashSet<EventKey> bossBgmEvents = new();
            foreach (EventSpec ev in templates.Values)
            {
                foreach (EnemyTemplate t in ev.Template)
                {
                    if (t.Animation != null)
                    {
                        animationEvents.Add(ev.Key);
                    }
                    if (t.BossBgmArg != null)
                    {
                        bossBgmEvents.Add(ev.Key);
                    }
                    if (t.SubInit != null)
                    {
                        foreach (string subStr in t.SubInit.Split(' '))
                        {
                            int sub = int.Parse(subStr);
                            subInitPairs.Add(((int)ev.ID, sub));
                        }
                    }
                    if (t.Gravelord != null)
                    {
                        string[] parts = t.Gravelord.Split(' ');
                        if (!gravelordFlags.TryGetValue(ev.Map, out (int, int) flags))
                        {
                            flags = default;
                        }
                        (int startFlag, int endFlag) = flags;
                        if (parts[0] == "start")
                        {
                            startFlag = int.Parse(parts[1]);
                        }
                        else if (parts[0] == "end")
                        {
                            endFlag = int.Parse(parts[1]);
                        }
                        else continue;
                        gravelordFlags[ev.Map] = (startFlag, endFlag);
                    }
                }
            }

            bool getEntityFromInit(Instr init, EnemyTemplate t, out uint entity)
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
                        entity = (uint)(int)arg;
                        return true;
                    }
                }
                return false;
            }

            // Entities which do not exist in game anymore, but passed as arguments to events.
            // These event initializations are ignored, otherwise an error is thrown if no valid entity is found.
            HashSet<uint> ignoreEnemies = new()
            {
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

            // Gather usage data from real initializations of events
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
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
                                uint entity = t.Entity;
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
                            if (!defeatFlagIds.ContainsKey(defeatFlag) || bgmId <= 0) continue;
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
                                if (!t.Type.StartsWith("loc") && t.Type != "common" && t.Type != "copyloc")
                                {
                                    throw new Exception($"Internal error: {callee} anim referenced in {t.Type} event template {callee}");
                                }
                                string[] parts = t.Animation.Split(' ');
                                // "Default" unset animation values are 0, so uint should be fine, outside of DS1
                                uint parsePart(int index)
                                {
                                    if (index >= parts.Length) throw new Exception($"Internal error: {callee} anim {t.Animation} missing part {index}");
                                    string part = parts[index];
                                    if (uint.TryParse(part, out uint val)) return val;
                                    if (!events.ParseArgSpec(part, out int pos)) throw new Exception($"Internal error: {callee} anim {t.Animation} has wrong format");
                                    if (init.Offset + pos >= init.Count) throw new Exception($"{callee} anim {t.Animation} init {init} missing required arguments");
                                    return (uint)(int)init[init.Offset + pos];
                                }
                                int parseIntPart(int index)
                                {
                                    if (index >= parts.Length) throw new Exception($"Internal error: {callee} anim {t.Animation} missing part {index}");
                                    string part = parts[index];
                                    if (int.TryParse(part, out int val)) return val;
                                    if (!events.ParseArgSpec(part, out int pos)) throw new Exception($"Internal error: {callee} anim {t.Animation} has wrong format");
                                    if (init.Offset + pos >= init.Count) throw new Exception($"{callee} anim {t.Animation} init {init} missing required arguments");
                                    return (int)init[init.Offset + pos];
                                }
                                if (game.DS1)
                                {
                                    if (!getEntityFromInit(init, t, out uint initEntity))
                                    {
                                        // 11205180 has one instance of split copyloc initialization
                                        continue;
                                    }
                                    if (!defaultData.TryGetValue(initEntity, out EnemyData initData))
                                    {
                                        throw new Exception($"Enemy {initEntity} with wakeup animation missing from map data");
                                    }
                                    SpecialAnim anim = new() { Type = parts[0] };
                                    for (int p = 1; p < parts.Length; p += 2)
                                    {
                                        string spec = parts[p];
                                        if (spec == "force" || spec == "cancel")
                                        {
                                            anim.CancelType = spec;
                                            anim.CancelAnim = parseIntPart(p + 1);
                                            anim.CancelStr = parts[p + 1];
                                        }
                                        else
                                        {
                                            anim.InitType = spec;
                                            anim.InitStr = parts[p + 1];
                                            if (anim.InitType == "map")
                                            {
                                                anim.InitAnim = initData.Anim;
                                            }
                                            else if (anim.InitType == "init")
                                            {
                                                anim.InitAnim = parseIntPart(p + 1);
                                            }
                                            else if (anim.InitType != "disabled") throw new Exception($"Unknown anim type {t.Animation} in {callee}");
                                        }
                                    }
                                    specialAnimations[initEntity] = anim;
                                    continue;
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
                                uint entity = parsePart(1);
                                if (entity == 0)
                                {
                                    // All-0 initialization of 90005261 in m12_02_00_00
                                    if (parts[1].StartsWith("X")) continue;
                                    throw new Exception($"{callee} anim {t.Animation} init {init} missing entity");
                                }
                                (int, int) anims = (0, 0);
                                if (type == "wakeup")
                                {
                                    anims = (0, (int)parsePart(2));
                                }
                                else
                                {
                                    anims = ((int)parsePart(2), (int)parsePart(3));
                                }
                                // These are fine to coexist, probably, unless it explicitly disables them
                                if (false && defaultData.TryGetValue(entity, out EnemyData data) && data.Anim > 0)
                                {
                                    // Console.WriteLine($"{entity} in event {callee}: MSB animation {data.Anim} vs evented ({anims.Item1}, {anims.Item2})");
                                }
                                // common_funcs may be called for nonexistent entities
                                if (!defaultData.ContainsKey(entity)) continue;
                                // Only record animations in non-gravity entities
                                if (type != "gravity" && type != "gravityarg")
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

            if (game.DS1 && specialAnimations.Count > 0)
            {
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.Class != EnemyClass.Basic) continue;
                    EnemyData data = defaultData[info.ID];
#if DEBUG
                    if (data.Anim > 0 && !specialAnimations.ContainsKey(data.ID))
                    {
                        // This applies to egg carriers and a few hollows in Sunlight Altar, New Londo Ruins, and Asylum
                        // One engorged in Painted World, wheel dog in Blighttown, transport batwing in Anor Londo, maiden Pisaca
                        // This won't work well for new enemies so tag in config explicitly
                        // Console.WriteLine($"{info.Class} {info.Map} {ename(data.ID)} has {data.Anim}");
                    }
#endif
                    if (info.HasTag("docile") && !specialAnimations.ContainsKey(data.ID))
                    {
                        specialAnimations[info.ID] = new() { Type = "passive", InitType = "map", InitAnim = data.Anim };
                    }
                }
            }

            // After emevd pre-pass, verify all required segment data is collected from scripts
            if (!game.Sekiro)
            {
                // Errors are ignored for ignored merge enemies. TODO just don't add them to segmentCmds in the first place?
                List<uint> segmentlessBosses = infos.Values
                    .Where(info => info.IsImportantTarget && !segmentCmds.ContainsKey(info.ID) && !segmentCmds.ContainsKey(info.DupeFrom) && info.Class != EnemyClass.None)
                    .Select(info => info.ID)
                    .ToList();
                if (!partialEventConfig && segmentlessBosses.Count > 0) throw new Exception($"Internal error: bosses [{string.Join(", ", segmentlessBosses)}] missing segments");
                foreach (KeyValuePair<uint, Dictionary<string, CommandSegment>> entry in segmentCmds)
                {
                    if (infos.TryGetValue(entry.Key, out EnemyInfo info) && info.Class == EnemyClass.None) continue;
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
                    else if (game.EldenRing && minibossCount > 0 && minibossCount != minibossSegmentTypes.Count)
                    {
                        valid = false;
                    }
                    if (!valid)
                    {
                        throw new Exception($"Internal error: boss {entry.Key} has invalid segment configuration [{string.Join(", ", types)}]");
                    }
                }
            }

            // System for transplanting camera ids in Sekiro
            // This is a system in Sekiro only to add map-specific pointers to LockCamParam etc.
            // Map from entity id -> starting camera id
            Dictionary<uint, int> startCameras = templates.Values
                .SelectMany(ev => ev.Template.Where(t => t.Type.StartsWith("start") && t.Entity != 0 && t.Camera != null))
                .ToDictionary(t => t.Entity, t => int.Parse(t.Camera));
            // Map from (full source id, target map) -> partial target id
            Dictionary<(string, int, int), int> movedCameraSets = new Dictionary<(string, int, int), int>();
            int TransplantParamSet(int id, uint source, uint target, string param = "CameraSetParam")
            {
                // Camera set param is like 1102500. Entity id is like 1120830. At least for now, rely on entity id format to determine map.
                // Get camera id base id for entity
                int getFullCameraSet(int ent)
                {
                    int area = ent / 100000;
                    int block = (ent / 10000) % 10;
                    return (area * 100000) + (block * 1000);
                }
                int sourceMap = getFullCameraSet((int)source);
                int targetMap = getFullCameraSet((int)target);
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
            // Make cameras nicer in onslaught
            if (game.Sekiro && anyDupeEnabled)
            {
                foreach (PARAM.Row row in Params["LockCamParam"].Rows)
                {
                    // TODO: Limit to certain bosses. may also need y angles
                    row["CamDistTarget"].Value = (float)row["CamDistTarget"].Value * 1.25f;
                }
            }

            // ---
            // --- Boss names
            // ---

            string getName(FMGDictionary fmgs, int nameId)
            {
                string name;
                if (game.EldenRing)
                {
                    name = fmgs["NpcName"][nameId];
                }
                else if (game.DS1)
                {
                    name = fmgs["NPC_name_"][nameId];
                }
                else
                {
                    name = fmgs["NPC名"][nameId];
                    if (game.DS3)
                    {
                        if (string.IsNullOrWhiteSpace(name)) name = fmgs["NPC名_dlc1"][nameId];
                        if (string.IsNullOrWhiteSpace(name)) name = fmgs["NPC名_dlc2"][nameId];
                    }
                }
                return string.IsNullOrEmpty(name) ? null : name;
            }
            void setName(FMGDictionary fmgs, int nameId, string name)
            {
                if (game.EldenRing)
                {
                    game.WriteFMGs = true;
                    fmgs["NpcName"][nameId] = name;
                }
                else if (game.DS1)
                {
                    game.WriteFMGs = true;
                    fmgs["NPC_name_"][nameId] = name;
                }
                else
                {
                    fmgs["NPC名"][nameId] = name;
                }
            }
            int baseNameId = game.Sekiro ? 902000 : (game.DS3 ? 907000 : 907770000);
            if (game.DS1)
            {
                // Limit of 32767. Bosses in-game are from 2230 to 5400
                baseNameId = 19000;
            }

            Dictionary<uint, int> allocatedTargetNameIds = new Dictionary<uint, int>();
            // TODO: Make a "healthbar boss" category
            foreach (EnemyInfo info in infos.Values
                .Where(i => i.ExtraName != null || i.FullName != null || i.PartName != null)
                .OrderBy(i => i.ID))
            {
                allocatedTargetNameIds[info.ID] = baseNameId;
                baseNameId += 10;
            }
            // Mapping from (target entity, base target name) = new target name
            Dictionary<(uint, int), int> nameIds = new();
            int GetCleverName(int id, uint source, uint target, bool inplaceTarget = false)
            {
                if (!opt["edittext"] && !opt["editnames"]) return id;
                if (source == target) return id;
                EnemyInfo sourceInfo = infos[source];
                EnemyInfo targetInfo = infos[target];
                // Odd case which can come up in dedupe, where helper->main healthbar may occur.
                // TODO be careful about this in Sekiro (and DS3?) where OwnedBy is overloaded
                if (game.EldenRing && sourceInfo.OwnedBy == target) return id;
                // If they have the same full name, keep it as is to avoid duplication
                if (sourceInfo.FullName != null && sourceInfo.FullName == targetInfo.FullName) return id;
                if (sourceInfo.Important?.Names?.UniqueTemplate != null && sourceInfo.Important?.Names?.UniqueTemplate == targetInfo.Important?.Names?.UniqueTemplate) return id;
                // If there is no configuration in the target, do not bother
                if (inplaceTarget && targetInfo.FullName == null && targetInfo?.Important.Names?.MainTemplate == null) return id;

                if (!nameIds.TryGetValue((target, id), out int nameId))
                {
                    // Use the part name, otherwise keep things simple and use the model name, for English name
                    // Previously tried to use ExtraName in Elden Ring, but this is still used for config purposes.
                    string sourceModelName = game.ModelCharacterName(defaultData[source].Model, defaultData[source].Char);
                    if (game.EldenRing)
                    {
                        sourceModelName = Regex.Replace(sourceModelName, @"[0-9 ]*$", "");
                    }
                    string sourcePart = sourceInfo.PartName;
                    string targetFull = targetInfo.FullName;
                    // To-be-filled-in
                    if (sourcePart != null && sourcePart.Contains("^")) sourcePart = null;
                    if (targetFull != null && targetFull.Contains("^")) targetFull = null;
                    string fullName;
                    if (targetInfo.Important?.Names?.MainTemplate != null)
                    {
                        fullName = CalculateCleverName(sourceInfo, targetInfo, sourceModelName);
                    }
                    else if (targetFull == null)
                    {
                        fullName = sourceModelName;
                    }
                    else
                    {
                        // Effectively there are 6 types of names.
                        // Adjective part enemies have full form and adj form: (Night's) Cavalry, (Erdtree Burial) Watchdog, (Fire) Giant
                        //   -> Night's Watchdog, Fire Morgott (partial from PartName, full name from NpcName with partial tag)
                        // X of Y enemies: Ancient Hero, Red Wolf, Beastman -> Bell Bearing Hunter of Zamor, Morgott of the Champion
                        // Full names always: Kindred of Rot, Ancestor Spirit -> Ancient Hero of Rot, Margit Spirit
                        // Knight names have two forms: Crucible Watchdog, Magma Wyrm Morgott (semicolon in FullName)
                        // Proper title enemies have proper names: Ordovis, Morgott (PartName plus title tag)
                        // Proper adjective enemies have names but can accept partial names: Magma Wyrm Radahn, Starscourge Cavalry
                        // Special tags: proper, adjective, partial, sourcefull
                        // fullName = targetFull.Replace("$1", sourcePart ?? sourceModelName);
                        string npcName = null;
                        if (sourceInfo.HasTag("partial") && sourceInfo.NpcName > 0)
                        {
                            npcName = getName(game.ItemFMGs, sourceInfo.NpcName);
                        }
                        fullName = CalculateCleverName(sourceInfo, targetInfo, sourceModelName, npcName);
                    }
                    if (targetInfo.DupeFrom == source)
                    {
                        // TODO: Is ExtraName ever used? It should likely be NpcName. When does it activate?
                        fullName = sourceInfo.DupeName ?? sourceInfo.ExtraName ?? sourceModelName;
                    }
                    if (ann.CustomNames != null && ann.CustomNames.TryGetValue(fullName, out string customName) && fullName != customName)
                    {
                        if (opt["debugnames"]) Console.Write(">");
                        fullName = customName;
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
                            if (lang.Key == game.EnglishName) continue;
                            string backupName = null;
                            if (sourceInfo.NpcName > 0)
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
                            if (opt["debugnames"] && opt["allnames"])
                            {
                                Console.WriteLine($"  {lang.Key} replacement for {id} -> {nameId}: {backupName}");
                            }
                        }
                    }
                }
                return nameId;
            }

            // ---
            // --- Preprocess dupe events. This modifies the event scripts and the event config in-place. It is confusing.
            // ---

            // After all of the above metadata has been collected, preprocess for dupe handling
            // Mapping from original copyphase music flag to new one
            Dictionary<int, int> copyphaseNewMusicFlags = new();
            if (anyDupeEnabled)
            {
                // Dictionary from event key to (copy event or not), for preprocessing entire event 
                Dictionary<EventKey, bool> preDupeEvents = new();

                // Hacky dictionary just by id. Used for DupeEvents.
                Dictionary<uint, EventSpec> idTemplates = new();
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
                    foreach (uint ev in info.DupeEvents)
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
                                List<uint> entities = ct.Entities.Split(' ').Select(uint.Parse).ToList();
                                entities = entities
                                    .SelectMany(id => new[] { id }.Concat(dupeEnemyMap.TryGetValue(id, out List<uint> ds) ? ds : new List<uint>()))
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
                                // Note: copyInfo may not be present if DLC is disabled
                                if (ct.Type == "segment" && ct.StartFlag > 0 && infos.TryGetValue(ct.Entity, out EnemyInfo copyInfo))
                                {
                                    int startFlag = copyInfo.StartFlag;
                                    if (startFlag > 0 && dupeEnemyMap.TryGetValue(ct.Entity, out List<uint> segDupes))
                                    {
                                        foreach (uint segDupe in segDupes)
                                        {
                                            infos[segDupe].StartFlag = NewEventID(true);
                                        }
                                    }
                                }
                                if (int.TryParse(ct.MusicFlag, out int flag))
                                {
                                    // If there is also a music flag here, we need to make sure to change it
                                    // and also explicitly add an off/on setting everywhere it's used.
                                    copyphaseNewMusicFlags[flag] = NewEventID(true);
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
                    else if (uint.TryParse(ev.Dupe, out uint source))
                    {
                        t.Type = "chr";
                        t.Entity = source;
                    }
                    else throw new Exception($"Unknown {ev.ID} dupe info: {ev.Dupe}");
                    ev.Template.Add(t);
                }
                // Stable copy ids for a given event id
                Dictionary<EventKey, List<int>> eventCopies = new();
                List<int> getEventCopies(EventKey original)
                {
                    if (!eventCopies.TryGetValue(original, out List<int> copies))
                    {
                        eventCopies[original] = copies = Enumerable.Range(0, maxDupeCount).Select(_ => NewEventID()).ToList();
                    }
                    return copies;
                }
                // This might be good to add to EventSpec directly and cache there
                // All of this string parsing everywhere is suboptimal, but preprocessing is also pretty messy
                (List<uint>, List<int>) parseEntitiesAndArgs(string entStr)
                {
                    List<uint> entityIds = new();
                    List<int> entityArgs = new();
                    if (entStr != null)
                    {
                        foreach (string ent in entStr.Split(' '))
                        {
                            if (uint.TryParse(ent, out uint id))
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
                foreach (KeyValuePair<string, EMEVD> entry in emevds)
                {
                    List<EMEVD.Event> newDupeEvents = new List<EMEVD.Event>();
                    foreach (EMEVD.Event e in entry.Value.Events)
                    {
                        // Copy event itself if it's directly in the config
                        // Don't touch initializations in this case
                        EventKey originalKey = new EventKey(e.ID, entry.Key);
                        if (preDupeEvents.TryGetValue(originalKey, out bool copyEvent) && copyEvent)
                        {
                            if (entry.Key == "common_func") throw new Exception($"Internal error: can't duplicate event {originalKey}");
                            EventSpec ev = templates[originalKey];
                            List<int> copies = getEventCopies(originalKey);
                            (List<uint> entityIds, List<int> entityArgs) = parseEntitiesAndArgs(ev.Entities);
                            for (int c = 0; c < copies.Count; c++)
                            {
                                int copyId = copies[c];
                                EventKey copyKey = new EventKey((uint)copyId, entry.Key);
                                newDupeEvents.Add(events.CopyEvent(e, copyId));
                                EventSpec evCopy = ev.DeepCopy();
                                templates[copyKey] = evCopy;
                                evCopy.ID = (uint)copyId;
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
                            (List<uint> entityIds, List<int> entityArgs) = parseEntitiesAndArgs(ev.Entities);
                            // Assume indicates are valid for the moment, and events.Parse uses ints
                            List<uint> argEntities = entityArgs.Select(k => (uint)(int)originalInit[originalInit.Offset + k]).ToList();
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
                                // TODO: Make copyinit track slot ids for non-common_func
                                for (int c = 0; c < maxCopies; c++)
                                {
                                    // Copy the init itself, rewriting with dupe entities
                                    Instr copyInit = events.CopyInit(originalInit, originalInit.Callee, initOld);
                                    foreach (int k in entityArgs)
                                    {
                                        // Assume indices are valid for the moment
                                        // TODO: This previously assigned it to object val -> val is int ival, does that do anything?
                                        int ival = (int)copyInit[copyInit.Offset + k];
                                        if (dupeEnemyMap.TryGetValue((uint)ival, out List<uint> dupeVals))
                                        {
                                            copyInit[copyInit.Offset + k] = (int)dupeVals[c];
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

            // ---
            // --- Main event state (avoid editing emevd files directly in main pass)
            // ---

            // Dictionary from map to (new init instruction, new event). Either may be null
            Dictionary<string, List<(EMEVD.Instruction, EMEVD.Event)>> newInitializations = new();
            // Dictionary from sub-init caller-callee info to init instruction, where it is left out of new inits.
            Dictionary<SubInitKey, EMEVD.Instruction> subInits = new();
            // Dictionary from sub-init caller info to init instruction, to rewrite later. Callee should be null in the key.
            Dictionary<SubInitKey, EMEVD.Event> subInitEvents = new();
            // Don't redo non-chr event templates. For now, just use references.
            HashSet<EnemyTemplate> completedTemplates = new();
            HashSet<EventKey> removedEvents = new();
            HashSet<EventKey> usedEvents = new();
            Dictionary<EventKey, EMEVD.Event> commonEvents = emevds["common_func"].Events
                .ToDictionary(e => new EventKey(e.ID, "common_func"), e => e);
            // For events with parameters, avoid updating it with loc templates immediately
            Dictionary<EventKey, EMEVD.Event> modifiedEvents = new();

            void writeModifiedEvents(string map, EMEVD emevd)
            {
                for (int i = 0; i < emevd.Events.Count; i++)
                {
                    EventKey key = new EventKey(emevd.Events[i].ID, map);
                    if (modifiedEvents.Remove(key, out EMEVD.Event replace))
                    {
                        emevd.Events[i] = replace;
                    }
                }
            }
            EMEVD.Event modifyEvent(EventKey callee, EMEVD.Event ev)
            {
                if (modifiedEvents.TryGetValue(callee, out EMEVD.Event modified))
                {
                    return modified;
                }
                modifiedEvents[callee] = modified = events.CopyEvent(ev, ev.ID);
                return modified;
            }

            // ---
            // --- Various cached state and output state for later passes
            // ---

            // These have duplicate data from dupeEnemyMap, but are used for all event purposes
            List<RelocMap> dupeRelocs = new();
            for (int i = 0; i < maxDupeCount; i++)
            {
                RelocMap reloc = new();
                foreach (KeyValuePair<uint, List<uint>> entry in dupeEnemyMap)
                {
                    if (i < entry.Value.Count)
                    {
                        reloc[EventValue.Enemy(entry.Key)] = EventValue.Enemy(entry.Value[i]);
                    }
                }
                dupeRelocs.Add(reloc);
            }
            // Map from progress flags to second dupe flags
            Dictionary<int, List<int>> progressFlagCopies = new Dictionary<int, List<int>>();
            // Owner entities which have music flags set by events
            HashSet<uint> usedMusicFlagTargets = new();
            Dictionary<string, int> flagToSpeffectControl = new Dictionary<string, int>
            {
                ["SkipIfEventFlag"] = 0,
                ["EndIfEventFlag"] = 2,
                ["GotoIfEventFlag"] = 1,
            };
            // MSB output of event editing, setting initial animations
            Dictionary<uint, int> eventInitialAnimations = new();
            // Dictionary from target entity to cancel anim used on them in new location
            Dictionary<uint, int> usedCancelAnims = new();
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
                        AllocateWritableEventIDs(count);
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
                                AddMulti(progressFlagCopies, flag, NewEventID(writeable: true));
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
                // Only silo if there's a global silo in place (TODO could also check boss silo)
                bool siloBgms = overallSilo == AreaSiloType.DLC;
                foreach (bool dlc in new[] { false, true })
                {
                    SortedDictionary<int, int> flagBgmIds = defeatFlagBgmIds;
                    if (siloBgms)
                    {
                        flagBgmIds = new(flagBgmIds.Where(e => dlc == game.IsEldenDlcMap(infos[defeatFlagIds[e.Key]].Map)).ToDictionary(e => e.Key, e => e.Value));
                        if (flagBgmIds.Count == 0) continue;
                    }
                    List<int> majorBgms = flagBgmIds
                        .Where(e => infos[defeatFlagIds[e.Key]].Class == EnemyClass.Boss && paramIds.Contains(e.Value))
                        .Select(e => e.Value)
                        .Distinct()
                        .ToList();
                    List<int> allBgms = flagBgmIds.Where(e => paramIds.Contains(e.Value)).Select(e => e.Value).Distinct().ToList();
                    Shuffle(bossBgmRandom, majorBgms);
                    List<int> bgmDefeatIds = flagBgmIds.Select(e => e.Key).ToList();
                    Shuffle(bossBgmRandom, bgmDefeatIds);
                    foreach (int defeatFlag in bgmDefeatIds)
                    {
                        int originalBgm = flagBgmIds[defeatFlag];
                        int newBgm;
                        if (majorBgms.Count > 0 && infos[defeatFlagIds[defeatFlag]].Class == EnemyClass.Boss)
                        {
                            newBgm = PopDeranged(majorBgms, originalBgm);
                        }
                        else
                        {
                            newBgm = Choice(bossBgmRandom, allBgms);
                        }
                        assignedDefeatFlagBgmIds[defeatFlag] = newBgm;
                    }
                    if (!siloBgms || !opt["dlc"])
                    {
                        break;
                    }
                }
            }

            // ---
            // --- Event-related entity id and mapping utilities
            // ---

            void fillEntityIdMapping(RelocMap reloc, uint entity, uint target, bool includeHelpers)
            {
                reloc[EventValue.Enemy(entity)] = EventValue.Enemy(target);
                if (includeHelpers)
                {
                    List<uint> allHelpers = new();
                    if (owners.TryGetValue(entity, out List<uint> helpers)) allHelpers.AddRange(helpers);
                    if (infos[entity].Groups != null) allHelpers.AddRange(infos[entity].Groups);
                    if (objectOwners.TryGetValue((int)entity, out List<string> objHelpers))
                    {
                        allHelpers.AddRange(objHelpers.Select(h => int.TryParse(h, out int hi) ? (uint)hi : 0).Where(h => h > 0));
                    }
                    foreach (uint helper in allHelpers)
                    {
                        if (infos.ContainsKey(helper) && infos[helper].Class != EnemyClass.Helper) continue;
                        if (mapping.HelperMapping.TryGetValue((target, helper), out uint helperTarget))
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
                foreach (EventValue derived in reloc.Keys())
                {
                    // Handle normal SplitFrom rewrites (not meant to handle dupes)
                    if (infos.TryGetValue(derived.UIntID, out EnemyInfo baseInfo) && baseInfo.SplitFrom > 0)
                    {
                        reloc[EventValue.Enemy(baseInfo.SplitFrom)] = reloc[derived];
                    }
                }
            }
            List<int> getIntEventArgs(IEnumerable<object> args)
            {
                return args.SelectMany(o => o is int oi ? new[] { oi } : Array.Empty<int>()).ToList();
            }
            List<uint> getUintEventArgs(IEnumerable<object> args)
            {
                // The args should still be ints if it comes from Instr, but cast it
                return args.SelectMany(o => o is int oi ? new[] { (uint)oi } : Array.Empty<uint>()).ToList();
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
                RelocMap reloc, Dictionary<uint, int> distReplace, List<string> specs,
                uint entity, uint target, List<uint> args)
            {
                foreach (string spec in specs)
                {
                    foreach (KeyValuePair<uint, RegionTarget> region in copyRegions(spec, entity, args, reloc))
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

            int maxHealthbars = 3;
            int getHealthbarIndex(uint source, uint target, int dupeIndex = -1)
            {
                EnemyInfo info = infos[source];
                int index = info.HealthbarIndex;
                // If no dupes exist for target, use normal index
                if (!dupeEnemyMap.TryGetValue(target, out List<uint> dupes)) return index;
                // Otherwise, look at all dupes, and see if they can all fit
                // Could cache this but it's not super expensive
                int targetIndex = -1;
                int totalCount = 0;
                // If there are doubles, dupeCount is 1, so this goes -1 0.
                for (int i = -1; i < dupeCount(target); i++)
                {
                    uint dupeTarget = i == -1 ? target : dupes[i];
                    uint dupeSource = mapping.RevMapping.TryGetValue(dupeTarget, out uint id) ? id : dupeTarget;
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
            int dupeSpEffectBase = 6095000;
            int getDupeSpEffect(int dupeIndex, bool start)
            {
                if (dupeIndex == -1) throw new Exception($"Internal error: boss start/end speffect ({start}) only available for copies");
                return dupeSpEffectBase + dupeIndex * 2 + (start ? 0 : 1);
            }

            // --
            // ----
            // ----- Main event loop
            // ----
            // --

            List<string> eventEditErrors = new List<string>();
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                if (disableEvents) break;
                if (!newInitializations.ContainsKey(entry.Key))
                {
                    newInitializations[entry.Key] = new List<(EMEVD.Instruction, EMEVD.Event)>();
                }

                // Note this putting all events in a dictionary precludes duplicates, which technically the game allows, so may conflict with other mods
                Dictionary<EventKey, EMEVD.Event> fileEvents = entry.Value.Events
                    .ToDictionary(e => new EventKey(e.ID, entry.Key), e => e);
                // Lookup order doesn't matter as the map is part of EventKey
                bool lookupEvent(EventKey key, out EMEVD.Event ev) => fileEvents.TryGetValue(key, out ev) || commonEvents.TryGetValue(key, out ev);
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    OldParams initOld = OldParams.Preprocess(e);
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr originalInit = events.Parse(e.Instructions[i], initOld);
                        if (!originalInit.Init) continue;
                        if (!originalInit.TryCalleeValue(templates, entry.Key, out EventKey callee, out EventSpec ev)) continue;
#if DEBUG
                        ev.Template = ev.Template.Where(t => !t.Type.Contains("xx")).ToList();
#endif
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
                                    if (argEntityAware)
                                    {
                                        if (getEntityFromInit(originalInit, t, out uint removeEntity))
                                        {
                                            simpleRemove |= mapping.IsRandomizedTarget(removeEntity);
                                        }
                                    }
                                    else
                                    {
                                        uint removeEntity = getUintEventArgs(originalInit.Args.Skip(originalInit.Offset))
                                            .Find(a => infos.ContainsKey(a));
                                        simpleRemove |= mapping.IsRandomizedTarget(removeEntity);
                                    }
                                }
                                else if (t.Entity > 0)
                                {
                                    simpleRemove |= mapping.IsRandomizedTarget(t.Entity);
                                }
                                else if (t.Entities != null)
                                {
                                    // This was changed from All to Any, TODO check this is fine in DS3?
                                    simpleRemove |= t.Entities.Split(' ').Any(es => mapping.IsRandomizedTarget(uint.Parse(es)));
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
                        List<(uint, uint, EMEVD.Event, EnemyTemplate)> eventCopies = new();
                        bool canRemove = true;

                        // Set up all chr edits first, before the event itself is edited
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (!t.Type.Contains("chr")) continue;
                            uint entity = t.Entity;
                            // Include dummy enemies for now, otherwise it will detect no entity
                            uint argEntity = 0;
                            if (entity <= 0)
                            {
                                if (!t.Type.Contains("arg"))
                                {
                                    throw new Exception($"Internal error for {t.Type} {callee}: no entity defined");
                                }
                                // TODO: Some overlap in logic with getEntityFromInit - this requires it to come from args, and errors out otherwise
                                List<uint> idArgs = getUintEventArgs(originalInit.Args.Skip(originalInit.Offset));
                                if (t.Entities != null)
                                {
                                    List<uint> selectArgs = idArgs.Intersect(t.Entities.Split(' ').Select(uint.Parse)).ToList();
                                    if (selectArgs.Count == 0)
                                    {
                                        continue;
                                    }
                                    argEntity = selectArgs[0];
                                }
                                else
                                {
                                    argEntity = idArgs.Find(a => infos.ContainsKey(a) || ignoreEnemies.Contains(a));
                                }
                                if (argEntity == 0)
                                {
                                    // There are some events with just blanked out args
                                    if (idArgs.Max() == 0)
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
                            if (mapping.GetTargets(entity, out List<uint> targets))
                            {
                                targets = targets.ToList();
                            }
                            else
                            {
                                targets = new();
                            }
                            // Hack for mimic events! TODO, generalize this to work for all dupe+random cases
                            // Something like dupeMap, but that's specific to non-randomized in-place stuff.
                            if (newMimics.Count > 0)
                            {
                                foreach (KeyValuePair<uint, ChestInfo> chest in newMimics)
                                {
                                    if (chest.Value.ClosestMimic == entity
                                        && mapping.GetTargets(chest.Key, out List<uint> transitiveTargets))
                                    {
                                        targets.AddRange(transitiveTargets);
                                    }
                                }
                            }
                            // If the entity remains the same, don't remove or edit the original event
                            if (!mapping.IsRandomizedTarget(entity))
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
                            else if (t.Type.Contains("singleonly"))
                            {
                                targets.RemoveAll(target => enableMultichr(entity, target));
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

                            if (lookupEvent(callee, out EMEVD.Event theEvent))
                            {
                                foreach (uint target in targets)
                                {
                                    eventCopies.Add((entity, target, events.CopyEvent(theEvent, NewEventID()), t));
                                }
                            }
                            else if (argEntity != 0)
                            {
                                foreach (uint target in targets)
                                {
                                    eventCopies.Add((entity, target, null, t));
                                }
                            }
                            else
                            {
                                throw new Exception($"Initialized event {callee} but absent from this file and not specified in args");
                            }
                        }

                        // Shared routines for segment handling
                        bool shouldRewriteEntity(EnemyTemplate t, uint id)
                        {
                            // if (id == 1034500800) Console.WriteLine($"{entry.Key} -> {callee} {t.Type} determination: {mapping.IsRandomizedTarget(id)} {dupeEnabled(id)}");
                            return mapping.IsRandomizedTarget(id) || (t.Type.StartsWith("segment") && dupeEnabled(id));
                        }
                        // Should only be called if segments will be processed below
                        string lastDeadType = null;
                        uint lastDeadEntity = 0;
                        void trackDeadSegment(EnemyTemplate t)
                        {
                            if (t.Entity > 0 && t.Type.StartsWith("segment"))
                            {
                                // This is managing a kind of ugly cross-template interaction, so it's an order-dependent edit either way.
                                // This is relevant in cases where the same event handles both phases of a multi-boss fight.
                                if (t.Segments.Any(s => s.Type == "dead"))
                                {
                                    // if (lastDeadEntity > 0) Console.WriteLine($"lastDeadEntity {callee} {lastDeadEntity}->{t.Entity}");
                                    lastDeadEntity = t.Entity;
                                    if (lastDeadType != null && lastDeadType != t.Type)
                                    {
                                        throw new Exception($"Internal error: unsupported mix of boss disable segments in {callee}");
                                    }
                                    lastDeadType = t.Type;
                                }
                            }
                        }
                        bool shouldProcessLocEntity(EnemyTemplate t, uint entity)
                        {
                            // Something must be randomized to this target if entity is specified.
                            // Alternatively, always handle segments when bosses are duplicated, or "stay" keyword is used.
                            // Also the inverse.
                            bool isRandom = shouldRewriteEntity(t, entity);
                            bool requireNonRandom = t.Type.Contains("stay");
                            if (isRandom == requireNonRandom)
                            {
                                if (!isRandom)
                                {
                                    // In the normal case of non-random entity, the event stays.
                                    // randomized locstay templates are ignored for removal determination.
                                    canRemove = false;
                                }
                                return false;
                            }
                            // Source-target pair. Not really needed for anything essential
                            if (t.Transfer > 0 && mapping.GetSource(t.Entity, out uint transferSource) && t.Transfer != transferSource)
                            {
                                canRemove = false;
                                return false;
                            }
                            // Also do this processing while we're here (TODO inline?)
                            trackDeadSegment(t);
                            return true;
                        }

                        // Process copyloc edits, which behave like chr but copy the event for each init
                        // This is not as fully-featured as other types but is most flexible for loc edits with multiple inits
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (!t.Type.Contains("copyloc")) continue;
                            // if (t.Dupe != null) throw new Exception($"Internal error: dupe not supported for {t.Type} in {callee}");
                            if (t.Entity < 0 && t.Entities == null) throw new Exception($"Internal error: entities required for {t.Type}");

                            if (!getEntityFromInit(originalInit, t, out uint entity))
                            {
                                // This can happen when there are multiple segmentcopyloc for a given event, and only one applies.
                                // If it applies and the entity is randomized, remove the init
                                // If it applies and the entity is not randomized, keep the init
                                // If nothing applies, keep the init (unknown or new boss)
                                continue;
                            }
                            if (!shouldProcessLocEntity(t, entity))
                            {
                                continue;
                            }
                            if (lookupEvent(callee, out EMEVD.Event theEvent))
                            {
                                eventCopies.Add((0, 0, events.CopyEvent(theEvent, NewEventID()), t));
                            }
                            else
                            {
                                throw new Exception($"Initialized event {callee} but absent from {entry.Key}");
                            }
                        }

                        // Set up in-place edits
                        foreach (EnemyTemplate t in ev.Template)
                        {
                            if (t.Type.Contains("chr") || t.Type.Contains("copyloc")) continue;

                            if (t.Dupe != null && !anyDupeEnabled)
                            {
                                // If a dupe event and dupe is disabled, ignore it.
                                // Do this before counting the template against canRemove.
                                continue;
                            }
                            if (t.Type == "default" || (!game.EldenRing && t.Type == "loc" && t.IsDefault()))
                            {
                                // This is fine, nothing to do if not chr
                                // TODO: Make IsDefault a debug check only
                                canRemove = false;
                                continue;
                            }

                            // Entity acts as a filter for whether the template applies
                            if (t.Entity > 0)
                            {
                                if (!shouldProcessLocEntity(t, t.Entity))
                                {
                                    continue;
                                }
                            }
                            if (t.Entities != null)
                            {
                                // Process if any are randomized
                                // This is a very coarse filter (not accurate if ArgEntities is missing the entity). Use copyloc for more specific.
                                if (!t.Entities.Split(' ').Any(es => shouldRewriteEntity(t, uint.Parse(es))))
                                {
                                    canRemove = false;
                                    continue;
                                }
                            }

                            if (!lookupEvent(callee, out EMEVD.Event locEvent))
                            {
                                throw new Exception($"Can't find event {callee} with {t.Type} template");
                            }
                            if (callee.Map == "common_func" &&
                                !(t.Type == "common" || t.Type == "commonstay" || t.Type == "locarg" || t.Type == "removearg" || t.Type == "segmentcopy"))
                            {
                                throw new Exception($"Internal error: common event {callee} cannot use {t.Type} template");
                            }

                            if (completedTemplates.Contains(t))
                            {
                                // When stay and copyloc/chr exist together, and there are multiple inits, don't impact canRemove, similar to above
                                if (!t.Type.Contains("stay"))
                                {
                                    canRemove = false;
                                }
                                continue;
                            }

                            canRemove = false;
                            if (t.Type.Contains("arg"))
                            {
                                eventCopies.Add((0, 0, null, t));
                            }
                            else if (t.Type == "segmentcopy")
                            {
                                eventCopies.Add((0, 0, events.CopyEvent(locEvent, NewEventID()), t));
                            }
                            else if (t.Type == "copy")
                            {
                                completedTemplates.Add(t);
                                eventCopies.Add((0, 0, events.CopyEvent(locEvent, NewEventID()), t));
                            }
                            else
                            {
                                completedTemplates.Add(t);
                                eventCopies.Add((0, 0, locEvent, t));
                            }
                        }

                        bool hasCopyTemplates = ev.Template.Any(t => t.Type.Contains("chr") || t.Type.Contains("copy"));

                        bool forceRemove = false;
                        foreach (var copy in eventCopies)
                        {
                            (uint entity, uint target, EMEVD.Event e2, EnemyTemplate t) = copy;
                            if (hasCopyTemplates && e2.ID == callee.ID)
                            {
                                // Make copy to write back later. This should probably be done in the above pass.
                                e2 = modifyEvent(callee, e2);
                            }
                            // Main entities to replace, for chr events
                            RelocMap reloc = new();
                            RelocMap dupeReloc = null;
                            Dictionary<uint, int> distReplace = new();
                            Instr init = originalInit;
                            // Event replacement option. Note this keeps args in init
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
                                    mapping.GetBaseTarget(target, out dupeIndex);
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
                                    transplantRegionSpecs(dupeReloc ?? reloc, distReplace, t.Regions, entity, target, getUintEventArgs(init.Args));
                                }
                                // Also we need to make a copy of the instruction at this point, so we don't edit the original
                                init = events.CopyInit(init, e2, initOld);
                            }
                            else if (t.Type.Contains("copy"))
                            {
                                init = events.CopyInit(init, e2, initOld);
                                init.Save(initOld);
                            }
                            // Renamings in the args, for common_func mainly
                            if (t.Name != null && t.Name.StartsWith("X"))
                            {
                                string[] nameParts = t.Name.Split(' ');
                                if (!events.ParseArgSpec(nameParts[0], out int entityPos) || !events.ParseArgSpec(nameParts[1], out int namePos)) throw new Exception($"Bad name spec {t.Name} for {callee}");
                                uint nameTarget = (uint)(int)init[init.Offset + entityPos];
                                if (mapping.GetRandomizedSource(nameTarget, ev.DupeIndex, out uint nameSource, out _))
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
                                            if (npcs.TryGetNpc(nameData.NPC, out PARAM.Row npc))
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
                                if (game.DS1)
                                {
                                    // In this case, use on/off to refer to whether to end
                                    if (flag > 0)
                                    {
                                        string cmd = $"EndIfEventFlag(EventEndType.End, {(on ? "ON" : "OFF")}, TargetEventFlagType.EventFlag, {flag})";
                                        events.AddMacro(edits, EditType.AddBefore, cmd);
                                    }
                                    else if (!on)
                                    {
                                        // End if defeat flag off, but it can never turn on
                                        string cmd = $"EndUnconditionally(EventEndType.End)";
                                        events.AddMacro(edits, EditType.AddBefore, cmd);
                                    }
                                }
                                else
                                {
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
                                foreach (string remove in PhraseRe.Split(t.Remove))
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
                                foreach (string remove in PhraseRe.Split(t.RemoveDupe))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.TreeDragons != null)
                            {
                                // This isn't used exactly anymore, since ineligible tree dragons can't be removed without affecting the final deathblow thing
                                string[] parts = PhraseRe.Split(t.TreeDragons);
                                if (!events.ParseArgSpec(parts[0], out int entityPos)) throw new Exception($"Bad tree spec {parts[0]} in {t.TreeDragons} for {callee}");
                                bool removeTree = true;
                                if (infos[target].DragonTreeList != null)
                                {
                                    uint dragon = (uint)(int)init[entityPos + init.Offset];
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
                                uint buffEntity;
                                if (events.ParseArgSpec(t.MultiplayerBuff, out int buffPos))
                                {
                                    buffEntity = (uint)(int)init[init.Offset + buffPos];
                                }
                                else
                                {
                                    buffEntity = uint.Parse(t.MultiplayerBuff);
                                    buffPos = -1;
                                }
                                uint replaceEntity = 0;
                                if (buffGroupEntities.TryGetValue(buffEntity, out uint altGroup))
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
                                        init[init.Offset + buffPos] = (int)replaceEntity;
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
                                foreach (string replace in PhraseRe.Split(t.Replace))
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
                                foreach (string remove in PhraseRe.Split(t.StartCmd))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.Segments != null)
                            {
                                if (!getEntityFromInit(originalInit, t, out uint segTarget))
                                {
                                    throw new Exception($"Internal error: No boss entity found for {originalInit}");
                                }
                                if (!segmentCmds.TryGetValue(segTarget, out Dictionary<string, CommandSegment> targetSegments))
                                {
                                    throw new Exception($"Internal error: No segments collected for {segTarget} in {callee}");
                                }
                                SourceSegmentData getSourceSegmentData(int index)
                                {
                                    bool randomized = mapping.GetRandomizedSource(segTarget, index, out uint sourceId, out uint realTarget);
                                    SourceSegmentData data = new SourceSegmentData
                                    {
                                        DupeIndex = index,
                                        Source = sourceId,
                                        Target = realTarget,
                                        IsRandom = randomized && (index == -1 || infos[realTarget].DupeFrom != sourceId),
                                    };
                                    // Console.WriteLine($"IsRandom {sourceId}->{realTarget}: {data.IsRandom} ({randomized})");
                                    data.Reloc = new();
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
                                        data.IsSwapped = owners.TryGetValue(data.Source, out List<uint> swapHelpers)
                                            && swapHelpers.Any(helper => mapping.SwapMapping.ContainsKey((data.Target, helper)));
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
                                    if (segment.Type.StartsWith("alt"))
                                    {
                                        // altsetup is additional data that doesn't replace any originals
                                        // There's althealthbar and altunhealthbar for minibosses and DS1 bosses,
                                        // and probably altenable/altdisable for DS1
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
                                    // Just rewrite everything for the moment
                                    // This "remove remove" logic is specific to MoveOnly for now, mostly for location-editing
                                    // (or lack thereof) and reflecting chr events being left alone
                                    foreach (string cmd in segment.Commands)
                                    {
                                        events.RemoveSegmentMacro(edits, name, cmd);
                                    }
                                    // if (segTarget == 1042360800) Console.WriteLine($"Segment in {e2.ID}: {name}, rm {segment.Commands.Count}");

                                    // For each source, build up a list of commands to use and modify the event
                                    // Exceptional behaviors:
                                    // 1. (un)healthbarcheck - constructed from scratch if non-miniboss source/multiple sources
                                    // 2. start - sets flags at the start (special exception)
                                    // 3. end - checks flags after the last MAIN condition in the end block

                                    bool guardrailConditions = segment.Type.StartsWith("end") || segment.Type.Contains("healthbarcheck");
                                    bool keepDead = game.DS1;

                                    // TODO: Clean up if it works
                                    bool useDupeFlag = false;
                                    List<uint> dupeFighters = new();
                                    List<int> dupeEndIds = new();
                                    foreach (SourceSegmentData data in sources)
                                    {
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
                                        // Only support dead segment for this for now. These commands are removed from endphase in ER
                                        else if (segment.Type == "phasedisable" && data.Segments.ContainsKey("dead"))
                                        {
                                            duals.Add("dead");
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
                                        else if (segment.Type == "buff")
                                        {
                                            if (data.Segments.ContainsKey(segment.Type))
                                            {
                                                duals.Add(segment.Type);
                                            }
                                        }
                                        else if (segment.Type == "tempdisable" || segment.Type == "tempenable")
                                        {
                                            if (data.Segments.ContainsKey(segment.Type))
                                            {
                                                duals.Add(segment.Type);
                                            }
                                            else if (data.Segments.ContainsKey("alt" + segment.Type))
                                            {
                                                duals.Add("alt" + segment.Type);
                                            }
                                            else
                                            {
                                                duals.Add("dead");
                                            }
                                        }
                                        // TODO: This may fail in DS3
                                        else throw new Exception($"Internal error: unknown target segment {segment.Type} in {callee}");

                                        // Entry commands
                                        List<string> editedCmds = new List<string>();
                                        // Indicates commands should go in a new event
                                        // Start triggers block dupe events, and setters are added to the main event before other start segments
                                        // End triggers are set at the end of dupe events, and all together block the main end
                                        int dupeTrigger = -1;
                                        if (!spEffectHolders.TryGetValue(segTarget, out uint dupeHolder))
                                        {
                                            // This may not be reliable if enemy can get regenerated or unloaded
                                            dupeHolder = segTarget;
#if DEBUG
                                            if (dupeCount(segTarget) > 0)
                                            {
                                                // throw new Exception($"Enemy {segTarget} has dupes but no speffect holder");
                                            }
#endif
                                        }
                                        // Dupe starts use a standalone event, since they may have complicated setup
                                        if (segment.Type == "start" && data.DupeIndex >= 0 && ev.Dupe != "copyphase")
                                        {
                                            dupeTrigger = NewEventID(true);
                                            if (useDupeFlag)
                                            {
                                                editedCmds.Add($"EndIfEventFlag(0, 1, 0, {dupeTrigger})");
                                                editedCmds.Add($"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {dupeTrigger})");
                                            }
                                            else
                                            {
                                                dupeTrigger = getDupeSpEffect(data.DupeIndex, true);
                                                editedCmds.Add($"EndIfCharacterHasSpEffect(EventEndType.End, {dupeHolder}, {dupeTrigger}, true, ComparisonType.Equal, 1)");
                                                editedCmds.Add($"IfCharacterHasSpEffect(MAIN, {dupeHolder}, {dupeTrigger}, true, ComparisonType.Equal, 1)");
                                            }
                                        }
                                        void setInvincibility(bool enable)
                                        {
                                            if (minibossTarget) return;
                                            // Used for Regal Ancestor Spirit, should probably use vulnsource for each animal
                                            if (infos[data.Source].HasTag("noinvincible")) return;
                                            List<uint> invTargets = mapping.GetHelperTargets(data.Target);
                                            invTargets.Add(data.Target);
                                            foreach (uint invTarget in invTargets)
                                            {
                                                // Don't bother if the enemy is not moved (should skip this section entirely?)
                                                if (!mapping.GetSource(invTarget, out uint invSource)) continue;
                                                // No Gideon/Priscilla invincibility
                                                if (infos[invSource].HasTag("vulnsource")
                                                    || (infos.ContainsKey(invTarget) && infos[invTarget].HasTag("vulntarget"))) continue;
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
                                        if (game.EldenRing && guardrailConditions)
                                        {
                                            editedCmds.Add("IfElapsedSeconds(MAIN, 0)");
                                        }
                                        if (!game.DS3 && segment.Type == "start")
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
                                                    data.Reloc, new(), sourceSegment.Regions,
                                                    data.Source, data.Target, new());
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
                                            RelocMap cmdHelpers = new();
                                            if (sourceSegment.AddHelpers)
                                            {
                                                // TODO: Helper for this too?
                                                if (owners.TryGetValue(data.Source, out List<uint> helpers))
                                                {
                                                    foreach (uint helper in helpers)
                                                    {
                                                        if (infos.TryGetValue(helper, out EnemyInfo inf)
                                                            && inf.HasTag("cmdhelper")
                                                            && mapping.HelperMapping.TryGetValue((data.Target, helper), out uint helperTarget))
                                                        {
                                                            cmdHelpers[EventValue.Enemy(data.Source)] = EventValue.Enemy(helperTarget);
                                                        }
                                                    }
                                                }
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
                                                    if (!keepDead && cmd == "ForceCharacterDeath")
                                                    {
                                                        continue;
                                                    }
                                                }
                                                // Custom case for end as endphase, take these from dead segment for now
                                                if (dual == "dead" && segment.Type == "phasedisable")
                                                {
                                                    if (cmd != "ChangeCharacterEnableState")
                                                    {
                                                        continue;
                                                    }
                                                }
                                                // Except for rewriting tempdisable/tempenable
                                                if ((segment.Type == "tempdisable" || segment.Type == "tempenable") && dual == "dead")
                                                {
                                                    if (cmd != "ChangeCharacterEnableState")
                                                    {
                                                        continue;
                                                    }
                                                    // TODO: Use helper tags to determine which are handled by independent chr events?
                                                    // Probably easier to add these commands without using a dual
                                                    uint enableSource = uint.Parse(cmdArgs[0]);
                                                    if (enableSource != defaultSegmentEnemy && enableSource != data.Source)
                                                    {
                                                        continue;
                                                    }
                                                    if (segment.Type == "tempenable")
                                                    {
                                                        cmdArgs[1] = "Enabled";
                                                        cmdStr = $"{cmd}({string.Join(", ", cmdArgs)})";
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
                                                        uint nameSource = uint.Parse(cmdArgs[1]);
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
                                                        if (mapping.SwapMapping.TryGetValue((data.Target, nameSource), out uint swapSource))
                                                        {
                                                            nameSource = swapSource;
                                                            // Recalculate name id just for the case that editnames is disabled
                                                            // Even better would be to swap the healthbar command itself, but this should match that probably
                                                            int swapNpcId = infos[swapSource].NpcName;
                                                            if (swapNpcId > 0)
                                                            {
                                                                targetNameId = swapNpcId;
                                                            }
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
                                                        uint nameSource = uint.Parse(cmdArgs[1]);
                                                        // We'll want to either skip the healthbar or offset it here
                                                        int newNameSlot = getHealthbarIndex(nameSource, segTarget, data.DupeIndex);
                                                        if (newNameSlot == -1) continue;
                                                        cmdArgs[2] = newNameSlot.ToString();
                                                        cmdStr = $"{cmd}({string.Join(", ", cmdArgs)})";
                                                    }
                                                }
                                                if (tempBosses.ContainsKey(segTarget) && cmd == "HandleBossDefeatAndDisplayBanner")
                                                {
                                                    // Don't show banner for temp bosses
                                                    cmdArgs[1] = "0";
                                                    cmdStr = $"{cmd}({string.Join(", ", cmdArgs)})";
                                                }
                                                string newCmd = events.RewriteInts(cmdStr, data.Reloc);
                                                editedCmds.Add(newCmd);
                                                if (sourceSegment.AddHelpers && !cmdHelpers.IsEmpty() && (cmd == "ChangeCharacterEnableState" || cmd == "ChangeCharacterCollisionState"))
                                                {
                                                    string helperCmd = events.RewriteInts(cmdStr, cmdHelpers);
                                                    if (helperCmd != newCmd)
                                                    {
                                                        editedCmds.Add(helperCmd);
                                                    }
                                                }
                                                // Detection for final end condition, while we have parse data
                                                if ((cmd.StartsWith("If") || condCmdRe.IsMatch(cmd))
                                                    && (cmdArgs[0] == "MAIN" || (int.TryParse(cmdArgs[0], out int group) && group == 0)))
                                                {
                                                    // This doesn't save the index, so make sure this is unique if it has to be
                                                    lastMainCond = newCmd;
                                                }
                                            }
                                        }
                                        // Done adding all commands from existing duals

                                        // Main commands when no duals exist
                                        if ((segment.Type == "healthbarcheck" || segment.Type == "unhealthbarcheck")
                                            && !duals.Contains(segment.Type))
                                        {
                                            List<uint> fighters = new() { data.Target };
                                            if (owners.TryGetValue(data.Source, out List<uint> helpers))
                                            {
                                                foreach (uint helper in helpers)
                                                {
                                                    if (infos.TryGetValue(helper, out EnemyInfo inf)
                                                        && (inf.HasTag("mainhelper") || inf.HasTag("combathelper"))
                                                        && mapping.HelperMapping.TryGetValue((data.Target, helper), out uint helperTarget))
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
                                                foreach (uint id in fighters)
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
                                                foreach (uint id in fighters)
                                                {
                                                    editedCmds.Add($"IfCharacterAIState(OR_13, {id}, AIStateType.Combat, ComparisonType.Equal, 1)");
                                                }
                                                editedCmds.Add("IfConditionGroup(OR_12, FAIL, OR_13)");
                                                foreach (uint id in fighters)
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
                                        if (!game.DS3 && (segment.Type == "setup" || segment.Type == "presetup"))
                                        {
                                            setInvincibility(true);
                                        }
                                        if (segment.Invincibility)
                                        {
                                            setInvincibility(!segment.Type.Contains("start"));
                                        }
                                        if (duals.Contains("endphase") && segment.Type == "end" && data.DupeIndex == -1)
                                        {
                                            // In all cases, there should be one of these per fight, but exactly one
                                            if (game.DS3)
                                            {
                                                editedCmds.Add($"PlaySE({segTarget}, SoundType.s_SFX, 777777777)");
                                                editedCmds.Add($"HandleBossDefeat({segTarget})");
                                            }
                                            else if (game.DS1)
                                            {
                                                editedCmds.Add($"PlaySE({segTarget}, SoundType.sSFX, 777777777)");
                                                editedCmds.Add($"HandleBossDefeat({segTarget})");
                                            }
                                            else
                                            {
                                                editedCmds.Add($"PlaySE({segTarget}, SoundType.SFX, 888880000)");
                                                // Can use "Great Enemy Felled" default of 17 by default, but try to take it from next phase. Could also calculate this in advance
                                                string banner = "17";
                                                uint nextSource = infos[data.Source].NextPhase;
                                                if (nextSource > 0 && segmentCmds.TryGetValue(nextSource, out var nextCmds)
                                                    && nextCmds.TryGetValue("end", out CommandSegment nextEnd) && nextEnd.NewCommands != null)
                                                {
                                                    string endCmd = nextEnd.NewCommands.Find(c => c.StartsWith("HandleBossDefeatAndDisplayBanner("));
                                                    if (endCmd != null)
                                                    {
                                                        (_, List<string> endArgs) = ParseCommandString(endCmd);
                                                        banner = endArgs[1];
                                                    }
                                                }
                                                editedCmds.Add($"HandleBossDefeatAndDisplayBanner({segTarget}, {banner})");
                                            }
                                        }
                                        if ((segment.Type == "end" || segment.Type == "endphase") && ev.Dupe != "copyphase")
                                        {
                                            if (data.DupeIndex >= 0)
                                            {
                                                // Add the flag here
                                                dupeTrigger = NewEventID(true);
                                                // TODO: Needs different args in DS3/Sekiro
                                                if (useDupeFlag)
                                                {
                                                    editedCmds.Add($"SetEventFlag(TargetEventFlagType.EventFlag, {dupeTrigger}, ON)");
                                                    dupeEndIds.Add(dupeTrigger);
                                                }
                                                else
                                                {
                                                    dupeTrigger = getDupeSpEffect(data.DupeIndex, false);
                                                    editedCmds.Add($"SetSpEffect({dupeHolder}, {dupeTrigger})");
                                                    dupeEndIds.Add(dupeTrigger);
                                                }
                                            }
                                            else if (dupeEndIds.Count > 0)
                                            {
                                                // Special edit for adding flags to end condition
                                                int mainIndex = editedCmds.IndexOf(lastMainCond);
                                                if (mainIndex == -1) throw new Exception($"No MAIN condition found in {segment.Type} segment for {data.Source} [{lastMainCond}]");

                                                string waitForTrigger(int f) => useDupeFlag
                                                    ? $"IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, {f})"
                                                    : $"IfCharacterHasSpEffect(MAIN, {dupeHolder}, {f}, true, ComparisonType.Equal, 1)";
                                                editedCmds.InsertRange(mainIndex + 1, dupeEndIds.Select(waitForTrigger));
                                            }
                                        }
                                        if (game.EldenRing && guardrailConditions)
                                        {
                                            editedCmds.Add("IfElapsedSeconds(MAIN, 0)");
                                        }

                                        // Finally add everything together.
                                        if (dupeTrigger == -1)
                                        {
                                            foreach (string cmd in editedCmds)
                                            {
                                                events.AddMacro(edits, EditType.SegmentAdd, cmd, name);
                                            }
                                        }
                                        else
                                        {
                                            // dupeFlag being set means flag communication with a separate start/event event
                                            if (segment.Type == "start")
                                            {
                                                // Edit main event to add trigger in start case
                                                string triggerCmd = useDupeFlag
                                                    ? $"SetEventFlag(TargetEventFlagType.EventFlag, {dupeTrigger}, ON)"
                                                    : $"SetSpEffect({dupeHolder}, {dupeTrigger})";
                                                events.AddMacro(edits, EditType.SegmentAdd, triggerCmd, name);
                                            }
                                            editedCmds.Insert(0, $"EndIfEventFlag(0, 1, 0, {infos[segTarget].DefeatFlag})");
                                            int customEventId = NewEventID();
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
                                    if (segment.Type == "dead" && t.Entity == lastDeadEntity && !keepDead)
                                    {
                                        if (segment.Start == null) throw new Exception($"Internal error: dead section missing Start for {segTarget}");
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
                                                // TODO: No hardcode (latter commands for PCR), or just fix PCR init
                                                if (instr.Name != null && instr.Name != "Label69"
                                                    && instr.Name != "ChangeCharacterEnableState"
                                                    && instr.Name != "ChangeCharacterCollisionState"
                                                    && instr.Name != "SetCharacterAnimationState"
                                                    && instr.Name != "ForceCharacterDeath"
                                                    && instr.Name != "DeactivateGenerator"
                                                    && instr.Name != "SetGeneratorState"
                                                    && instr.Name != "SetCharacterDefaultBackreadState"
                                                    && instr.Name != "SetCharacterTeamType"
                                                    && !segment.Commands.Contains(cmd))
                                                {
                                                    endCmd = cmd;
                                                    break;
                                                }
                                            }
                                            // Console.WriteLine($"edited for {callee} {segTarget}: {segment.Start}");
                                            if (!started)
                                            {
                                                throw new Exception($"Could not find disable segment for {segTarget}");
                                            }
                                            // Console.WriteLine($"Entity {lastDeadEntity}: goto {name} before {endCmd};");
                                            events.AddMacro(
                                                edits, EditType.AddBefore, segment.Start,
                                                endCmd ?? segment.End, applyOnce: true);
                                        }
                                    }
                                }
                                if (tempBosses.ContainsKey(segTarget))
                                {
                                    // There isn't a great place for this, just eliminate item drops if any exist
                                    // This is specifically for Lansseax 1037510800, lot 30300 in event 90005860
                                    events.RemoveMacro(edits, "AwardItemsIncludingClients", optional: true);
                                }
                                if (t.Segments.Any(s => s.Type == "dead"))
                                {
                                    // Another special case, Manus and Kalameet both have dead segment with Default
                                    // Causes issues with bosses which don't have disableRespawn, because generally the event should just do that.
                                    if (e2.RestBehavior == EMEVD.Event.RestBehaviorType.Default)
                                    {
                                        // Console.WriteLine($"!! {segTarget}");
                                        e2.RestBehavior = EMEVD.Event.RestBehaviorType.Restart;
                                    }
                                }
                            }
                            if (t.Gravelord != null)
                            {
                                if (opt["permagravelord"])
                                {
                                    forceRemove = true;
                                }
                                else
                                {
                                    string type = t.Gravelord.Split(' ')[0];
                                    // Remove all automatic aspects if possible
                                    if (type == "start")
                                    {
                                        events.RemoveMacro(edits, "5000", type: EventValueType.Flag);
                                        events.RemoveMacro(edits, "735", type: EventValueType.Flag);
                                    }
                                    else if (type == "end")
                                    {
                                        // Disasters message
                                        events.RemoveMacro(edits, "5001", type: EventValueType.Flag);
                                        events.RemoveMacro(edits, "735", type: EventValueType.Flag);
                                    }
                                    else if (type == "manage")
                                    {
                                        forceRemove = true;
                                    }
                                    else throw new Exception($"Internal config error: unknown {t.Gravelord}");
                                }
                            }
                            if (t.Animation != null)
                            {
                                string[] parts = t.Animation.Split(' ');
                                string type = parts[0];
                                uint parsePart(int index)
                                {
                                    string part = parts[index];
                                    if (events.ParseArgSpec(part, out int partPos)) return (uint)(int)originalInit[originalInit.Offset + partPos];
                                    return uint.Parse(part);
                                }
                                string parseArgPart(int index)
                                {
                                    string part = parts[index];
                                    if (events.ParseArgSpec(part, out int partPos)) return $"X{partPos * 4}_4";
                                    return part;
                                }
                                if (game.DS1)
                                {
#if DS1
                                    uint getUintArg(object obj)
                                    {
                                        // Surely this utility exists somewhere already?
                                        if (obj is string arg && events.ParseArgSpec(arg, out int partPos))
                                        {
                                            return (uint)(int)originalInit[originalInit.Offset + partPos];
                                        }
                                        else if (obj is uint uval) return uval;
                                        else if (obj is int val) return (uint)val;
                                        else return 0;
                                    }
                                    // Possible edits to make:
                                    // - Changing the contents of SetSpecialAnimations/ForceAnimationPlayback and the enemy's InitAnimID
                                    // - Adding damage check to MAIN condition
                                    // - Adding disable/enable AI at start and after MAIN condition
                                    // TODO: Make dupe-compatible
                                    if (getEntityFromInit(originalInit, t, out uint animTarget)
                                        && mapping.GetSource(animTarget, out uint animSource))
                                    {
                                        if (!specialAnimations.TryGetValue(animTarget, out SpecialAnim targetAnim))
                                        {
                                            // Try to avoid reparsing. May need to refer to spec for what to replace
                                            throw new Exception($"Internal config error: {animTarget} animation data not processed for {callee}");
                                        }
                                        if (!specialAnimations.TryGetValue(animSource, out SpecialAnim sourceAnim))
                                        {
                                            sourceAnim = SpecialAnim.Default;
                                        }
                                        // TODO: Check source anim for when this isn't necessary
                                        bool manageAI = type == "wakeup" || type == "dropdown" || type == "invincible" || type == "float";
                                        bool checkDamage = type == "climb" || type == "invincible" || type == "float";
                                        bool sourceUsable = false;
                                        if (type == "lunge")
                                        {
                                            // TODO: Figure out usable animations for each model, lunge cases are not great without it
                                            sourceUsable = sourceAnim.Type == "lunge";
                                        }
                                        // Event types shouldn't be passive so this is basically the non-lunge case
                                        else if (type != "passive")
                                        {
                                            sourceUsable = sourceAnim.Type == "wakeup" || sourceAnim.Type == "invincible";
                                        }
                                        int targetInit = sourceUsable ? sourceAnim.InitAnim : -1;
                                        int targetCancel = sourceUsable ? sourceAnim.CancelAnim : -1;
                                        if (targetInit > 0)
                                        {
                                            // Always set this when applicable (done after event loop to avoid simultaneous map editing)
                                            eventInitialAnimations[animTarget] = targetInit;
                                        }
                                        // SetSpecialAnimations has too many animation ids to do typed replace, so search for exact commands instead.
                                        // These must be replaced for scripts to function. InitAnimID is also set whenever applicable.
                                        string replaceSetInit = null, replaceSetCancel = null, replaceForceCancel = null;
                                        if (targetAnim.InitType == "init")
                                        {
                                            replaceSetInit = targetAnim.InitStr;
                                        }
                                        if (targetAnim.CancelType == "cancel")
                                        {
                                            replaceSetCancel = targetAnim.CancelStr;
                                        }
                                        else if (targetAnim.CancelType == "force")
                                        {
                                            replaceForceCancel = targetAnim.CancelStr;
                                        }
                                        string firstTimeCmd = null, firstMainCmd = null, replaceMainCmd = null;
                                        bool prevEndCmd = false;
                                        for (int j = 0; j < e2.Instructions.Count; j++)
                                        {
                                            EMEVD.Instruction ins = e2.Instructions[j];
                                            Instr instr = events.Parse(ins, pre);
                                            if (prevEndCmd)
                                            {
                                                firstTimeCmd = instr.ToString();
                                                prevEndCmd = false;
                                            }
                                            // It should be fine to edit Instr here as it's never saved back into the event and can't interfere with other edits
                                            if (instr.Name == "SetSpecialAnimations" && (replaceSetInit != null || replaceSetCancel != null)
                                                && getUintArg(instr[0]) == animTarget)
                                            {
                                                if (replaceSetInit != null && instr[1].ToString() == replaceSetInit)
                                                {
                                                    // In some cases, may be better to remove the command entirely
                                                    string prev = instr.ToString();
                                                    instr[1] = targetInit;
                                                    events.ReplaceMacro(edits, prev, instr.ToString());
                                                    replaceSetInit = null;
                                                }
                                                else if (replaceSetCancel != null && instr[1].ToString() == "-1" && instr[3].ToString() == replaceSetCancel)
                                                {
                                                    string prev = instr.ToString();
                                                    instr[3] = targetCancel;
                                                    usedCancelAnims[animTarget] = targetCancel;
                                                    events.ReplaceMacro(edits, prev, instr.ToString());
                                                    replaceSetCancel = null;
                                                }
                                            }
                                            else if (instr.Name == "ForceAnimationPlayback" && replaceForceCancel != null
                                                && getUintArg(instr[0]) == animTarget && instr[1].ToString() == replaceForceCancel)
                                            {
                                                string prev = instr.ToString();
                                                if (targetCancel > 0)
                                                {
                                                    instr[1] = targetCancel;
                                                    usedCancelAnims[animTarget] = targetCancel;
                                                    events.ReplaceMacro(edits, prev, instr.ToString());
                                                }
                                                else
                                                {
                                                    events.RemoveMacro(edits, prev);
                                                }
                                                replaceForceCancel = null;
                                            }
                                            // This makes some major assumptions about command naming, but is generally true in enrolled events.
                                            else if (firstTimeCmd == null && instr.Name != null && instr.Name.StartsWith("End"))
                                            {
                                                // Need to delay this, as otherwise it would be seen as extending a skip statement
                                                // Is it worth adding an EditType variant for that?
                                                prevEndCmd = true;
                                            }
                                            else if (firstMainCmd == null && instr.Name != null && instr.Name.StartsWith("If") && instr[0].ToString() == "0")
                                            {
                                                firstMainCmd = instr.ToString();
                                                if (checkDamage)
                                                {
                                                    instr[0] = (sbyte)-6;
                                                    replaceMainCmd = instr.ToString();
                                                }
                                            }
                                        }
                                        if (manageAI && firstMainCmd != null)
                                        {
                                            events.AddMacro(edits, EditType.AddBefore, $"SetCharacterAIState({animTarget}, Disabled)", firstTimeCmd);
                                            events.AddMacro(edits, EditType.AddAfter, $"SetCharacterAIState({animTarget}, Enabled)", firstMainCmd);
                                        }
                                        // This has to come after the AI edit in case the two commands are the same
                                        if (replaceMainCmd != null)
                                        {
                                            events.RemoveMacro(edits, firstMainCmd);
                                            events.AddMacro(edits, new EventAddCommand
                                            {
                                                Before = firstMainCmd,
                                                Cmds = new()
                                                {
                                                    replaceMainCmd,
                                                    $"IfCharacterDamagedBy(OR_06, {animTarget}, 10000)",
                                                    "IfConditionGroup(MAIN, PASS, OR_06)",
                                                },
                                            });
                                        }
                                    }
#endif
                                }
                                // There are two types of animation routines: rewriting 5450-based events, and changing anims in initializations
                                // Only suspend is ever used in Elden Ring
                                else if (type == "active" || type == "passive" || type == "suspend" || type == "suspendgravity")
                                {
                                    // For 5450-based events, we need to remove the 5450 check before a MAIN IfConditionGroup
                                    // For passive events, we additionally need to disable AI with starting anim and
                                    // enable AI after MAIN check if starting anim is not set.
                                    if (e2 == null) throw new Exception($"Internal error: {callee} anim template type {t.Type}");
                                    bool alwaysActive = type == "active";
                                    Instr ifActive = null;
                                    IEnumerable<string> aiCommands(bool enable)
                                    {
                                        if (alwaysActive) return new List<string>();
                                        if (type == "passive")
                                        {
                                            // Passive if an animation is not present
                                            // TODO: Why does this hardcode X0 and X4?
                                            return new List<string>
                                            {
                                                $"SkipIfComparison(1, ComparisonType.NotEqual, X4_4, -1)",
                                                $"SetCharacterAIState(X0_4, {(enable ? "Enabled" : "Disabled")})",
                                            };
                                        }
                                        string entityArg = parseArgPart(1);
                                        List<string> enableBlock = new List<string>
                                        {
                                            $"SetCharacterAIState({entityArg}, {(enable ? "Enabled" : "Disabled")})",
                                        };
                                        if (type == "suspendgravity")
                                        {
                                            // Elden Ring only
                                            // This seems to not work for mounted characters, but most others do
                                            enableBlock.AddRange(new[]
                                            {
                                                // Egh boolean not supported with lite emedf
                                                $"SetCharacterMaphit({entityArg}, {(enable ? "1" : "0")})",
                                                $"SetCharacterGravity({entityArg}, {(enable ? "Enabled" : "Disabled")})",
                                            });
                                            if (enable)
                                            {
                                                // Fall damage/death prevention. These have durations of 180 and 60 seconds, but are
                                                // needed in Mountaintops (could be controlled more precisely if needed)
                                                enableBlock.AddRange(new[]
                                                {
                                                    $"SetSpEffect({entityArg}, 4080)",
                                                    $"SetSpEffect({entityArg}, 4085)",
                                                });
                                            }
                                        }
                                        // Suspend mode, if not unconditionally active
                                        if (ifActive == null)
                                        {
                                            // Passive if an animation is not present, and there's no "if active" condition
                                            // So enemy without an animation and without an AI check is managed only by emevd.
                                            return new[]
                                            {
                                                $"SkipIfComparison({enableBlock.Count}, ComparisonType.NotEqual, {parseArgPart(2)}, -1)",
                                            }.Concat(enableBlock);
                                        }
                                        else
                                        {
                                            // Passive if an animation is not present *and* the "if active" condition is false
                                            // This means every enemy without an animation and without a conditional (parameter-based) AI
                                            // check is managed only by emevd. This parameter logic is in many Elden Ring events, but rarely used.
                                            return new[]
                                            {
                                                $"SkipIfComparison({enableBlock.Count + 2}, ComparisonType.NotEqual, {parseArgPart(2)}, -1)",
                                                ifActive.ToString(),
                                                $"SkipUnconditionally({enableBlock.Count})",
                                            }.Concat(enableBlock);
                                        }
                                    }
                                    Instr prevInstr = null;
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        EMEVD.Instruction ins = e2.Instructions[j];
                                        Instr instr = events.Parse(ins, pre);
                                        if ((type == "suspend" || type == "suspendgravity") && instr.Name == "IfCharacterAIState")
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
                                                    // This is a construct in Elden Ring like:
                                                    // SkipIfUnsignedComparison(1, ComparisonType.Equal, X24_4, 0)
                                                    // IfCharacterAIState(OR_09, X0_4, AIStateType.Combat, ComparisonType.Equal, 1)
                                                    // Which is rarely used, but we should allow think ranges to work in cases where it is used.
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
                                                // TODO: Make this a one-time edit?
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
                                // pose, wakeup, gravity - edit animation ids in-place in event, or in args, or remove event entirely
                                else
                                {
                                    uint locEntity = parsePart(1);
                                    // Previous bug: we may have several copies of this, if the number of dupes doesn't
                                    // depend on entity (since entity is encoded in Animation), but the entity itself
                                    // may not have that many copies.
                                    bool changed = mapping.GetRandomizedSource(locEntity, ev.DupeIndex, out uint source, out uint realTarget);
                                    infos.TryGetValue(realTarget, out EnemyInfo locsInfo);
                                    // Console.WriteLine($"Anim ids for {locEntity} #{ev.DupeIndex} (from {source} to {realTarget}, {locsInfo?.DupeFrom}): {changed}");
                                    // This used to check ev.DupeIndex != -1, but DupeIndex is not set for arg events like Weeping DRB
                                    if (locsInfo != null && locsInfo.DupeFrom == source)
                                    {
                                        // The "changed" applies to whether a transfer is required, but in the specific case of
                                        // animations, we can allow an in-spot duplicate to keep the same animation, and not deal with
                                        // startingAnimations being available or gravity primary positions.
                                        changed = false;
                                    }
                                    // hidden enemies must not appear early, and in particular, not have AI enabled early.
                                    // In Elden Ring applies to Scadutree Avatar (AI not enabled until third phase)
                                    // In Sekiro applies to woo guy, True Monk
                                    // In DS3 applies to mimics
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
                                            if (parts.Length >= 5 && locsInfo != null && locsInfo.TryGetArena("primary", out _))
                                            {
                                                init[init.CalleeOffset] = int.Parse(parts[4]);
                                            }
                                            else
                                            {
                                                anims = null;
                                            }
                                        }
                                        else if (type == "gravityarg")
                                        {
                                            if (locsInfo != null && locsInfo.HasTag("drop") && parts.Length >= 5 && events.ParseArgSpec(parts[4], out int dropPos))
                                            {
                                                init[init.Offset + dropPos] = 1;
                                            }
                                            // Dane Rite Bird issue, TODO debug event to see what condition is causing issues
                                            if (changed && locEntity == 2047390800)
                                            {
                                                forceRemove = true;
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
                                    int defeatFlag = targetInfo.DefeatFlag;
                                    if (game.DS1 && targetInfo.NextPhase > 0)
                                    {
                                        // Get true defeat flag in DS1, especially for Gargs in O&S phase 1.
                                        // Is this safe to do in all games? (do any events like this restart?)
                                        defeatFlag = infos[targetInfo.NextPhase].DefeatFlag;
                                    }
                                    addDefeatFlagLabel(t.DefeatFlagLabel, defeatFlag);
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
                                List<int> cameras = PhraseRe.Split(t.Camera).Select(c => int.Parse(c)).ToList();
                                if (entity == 0)
                                {
                                    // Moving camera from other entity to this one, a boss, at the start
                                    if (!t.Type.StartsWith("start")) throw new Exception($"Internal error: Non-start in-place camera for {callee}");
                                    if (mapping.GetSource(t.Entity, out uint cameraSource) && startCameras.TryGetValue(cameraSource, out int startCamera))
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
                                uint invTarget = t.Invincibility;
                                if (mapping.GetSource(invTarget, out uint invSource))
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
                                        foreach (uint helperTarget in mapping.GetHelperTargets(invTarget))
                                        {
                                            events.AddMacro(
                                                edits, EditType.AddAfter,
                                                $"SetCharacterInvincibility({helperTarget}, Enabled)",
                                                $"SetCharacterInvincibility({invTarget}, Enabled)");
                                            events.AddMacro(
                                                edits, EditType.AddAfter,
                                                $"SetCharacterInvincibility({helperTarget}, Disabled)",
                                                $"SetCharacterInvincibility({invTarget}, Disabled)");
                                            if (mapping.GetSource(helperTarget, out uint helperSource) && infos[helperSource].IsImmortal)
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
                            if (t.ShowNight && opt["nightday"])
                            {
                                // All of these checks are night checks
                                events.RemoveMacro(edits, "IfTimeOfDayInRange");
                            }
                            // Erg. This needs a custom edit routine, or enum replacement in parameters, or something
                            if (callee.ID == 20012200 && game.EldenRing && entity > 0
                                && init.Offset + 3 < init.Count && (int)init[init.Offset + 3] == 17)
                            {
                                init[init.Offset + 3] = 3;
                                init.Save();
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
                                        uint source = (uint)(int)init[init.Offset + sourcePos];
                                        List<uint> dupeArgs = dupeEnemyMap.TryGetValue(source, out List<uint> dupes) ? dupes.ToList() : new();
                                        while (dupeArgs.Count < maxDupeCount)
                                        {
                                            dupeArgs.Add(source);
                                        }
                                        // Healthbar is currently only supported for first, for simplicity
                                        if (p == 0 && events.ParseArgSpec(t.Dupe.HealthBarArg, out int namePos))
                                        {
                                            int nameBase = (int)init[init.Offset + namePos];
                                            foreach (uint dupe in dupeArgs.ToList())
                                            {
                                                dupeArgs.Add((uint)GetCleverName(nameBase, source, dupe));
                                            }
                                        }
                                        // These args mix uints (entities) and ints (names), but try to keep them ints here
                                        init.AddArgs(dupeArgs.Select(x => (object)(int)x));
                                    }
                                    init.Save(initOld);
                                }
                                if (t.Dupe.Generator != null)
                                {
                                    // Expand both dupeMap and dupeReloc with the given generators
                                    // These are used for rewrite-based and replace-based edits later on
                                    foreach (string genPart in t.Dupe.Generator.Split(' '))
                                    {
                                        uint gen = uint.Parse(genPart);
                                        if (dupeEnemyMap.ContainsKey(gen))
                                        {
                                            continue;
                                        }
                                        EventValue genValue = EventValue.Generator(gen);
                                        uint dupeGen(int index)
                                        {
                                            (_, uint result) = enemyEditor.MakeCrossMapGeneratorCopy(
                                                newEntity, entry.Key, entry.Key, gen, generators, dupeRelocs[index]);
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
                                // TODO: Make it work for other games. Requires smarter chaining logic unfortunately
                                if (t.Animation == null && game.EldenRing)
                                {
                                    bool adjustGroups = game.HasMods;
#if DEBUG
                                    // TODO: Fill out the cond fields for DS3 if needed
                                    if (!opt["ignorecond"])
                                    {
                                        adjustGroups = game.EldenRing;
                                    }
#endif
                                    // TODO: Make it work here
                                    if (adjustGroups) // && e2.ID != 2049430830)
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
                                        if (entity != 0 && instr.Name == "IF In/Outside Area" && distReplace.TryGetValue((uint)(int)instr[3], out int dist))
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
                                            else if (mapping.GetSource(t.Entity, out uint nameSource))
                                            {
                                                // Allow entity to be 0 to substitute an arg. This is needed for Owl 1 (the only parameterized start event),
                                                // may not work in the general case.
                                                if (t.Entity == nameEntity || nameEntity == 0)
                                                {
                                                    instr[3] = GetCleverName((int)instr[3], nameSource, t.Entity);
                                                }
                                            }
                                            mapping.GetSource(t.Entity, out uint ns);
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
                                        if (entity != 0 && instr.Name == "IfInoutsideArea" && distReplace.TryGetValue((uint)(int)instr[3], out int dist))
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
                                            uint nameSource = uint.Parse(t.Name);
                                            if (nameEntity == nameSource)
                                            {
                                                instr[3] = GetCleverName((int)instr[3], nameSource, target);
                                                instr.Save(pre);
                                                e2.Instructions[j] = instr.Val;
                                            }
                                        }
                                    }
                                    else if (game.DS1)
                                    {
                                        // Ignore the dupe logic for now, just do in-place rename and removal
                                        // For dupe, we don't care about if it's enable or disable, and both loc/chr are fine
                                        if (t.Name != null && instr.Name == "DisplayBossHealthBar" && !t.Name.StartsWith("X"))
                                        {
                                            // Console.WriteLine($"{instr} {instr[1]} {instr[1].GetType()}");
                                            foreach (string nameStr in t.Name.Split(' '))
                                            {
                                                uint nameEntity = (uint)(int)instr[1];
                                                uint nameSource = uint.Parse(nameStr);
                                                if (nameEntity == nameSource)
                                                {
                                                    // This is allowed in chr event or locstay event
                                                    uint nameTarget;
                                                    if (target > 0)
                                                    {
                                                        nameTarget = mapping.GetBaseTarget(target, out _);
                                                    }
                                                    else if (t.Entity > 0)
                                                    {
                                                        // Miniboss Seath has a regular loc event, but no segments
                                                        nameTarget = t.Entity;
                                                        // Rewrite source in loc terms in this case. It's a bit messy
                                                        if (mapping.GetRandomizedSource(nameTarget, -1, out uint locSource, out _))
                                                        {
                                                            nameSource = locSource;
                                                        }
                                                    }
                                                    else throw new Exception($"{callee} {t.Type} can't be used for names from {target}");
                                                    // TODO: Should miniboss be important target? Segment requirement is main issue
                                                    if (infos[nameTarget].IsImportantTarget || infos[nameTarget].Class == EnemyClass.Miniboss)
                                                    {
                                                        instr[3] = (short)GetCleverName((short)instr[3], nameSource, nameTarget, true);
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
                                    }
                                    else if (game.EldenRing)
                                    {
                                        // Note: Unlike past games, args can be param strings, so be careful
                                        // Replace area checks with distance checks
                                        if (entity != 0 && instr.Name == "IfInoutsideArea"
                                            && instr[3] is uint areaId
                                            && distReplace.TryGetValue(areaId, out int dist))
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
                                                uint nameEntity = (uint)instr[1];
                                                uint nameSource = uint.Parse(nameStr);
                                                if (nameEntity == nameSource)
                                                {
                                                    int nameSlot = (short)instr[2];
                                                    // This is allowed in chr event or locstay event
                                                    uint nameTarget;
                                                    int nameDupeIndex = -1;
                                                    if (target > 0)
                                                    {
                                                        nameTarget = mapping.GetBaseTarget(target, out nameDupeIndex);
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
                                                foreach (string spec in PhraseRe.Split(t.EffectFlag))
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
                                // This was originally written for Sekiro and barely works
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
                                        if (uint.TryParse(t.Dupe.Entity, out uint dupeSource))
                                        {
                                            if (dupeEnemyMap.TryGetValue(dupeSource, out List<uint> dupes))
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

                                    // Condition replacements for AND/OR customization. This breaks if a line is both a definition a usage.
                                    // TODO: This works differently now since it only supports individual lines, and only condition is needed.
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
                                        // TODO: Optimize condition usage, also square this with number of conditions
                                        if (callee.ID == 20010775) newCond = 6;
                                        foreach (string condStr in PhraseRe.Split(t.Dupe.Condition))
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
                                                        rewriteUse[line] = (cond, newCond * -condType, (newCond + 1) * condType);
                                                    }
                                                    else
                                                    {
                                                        rewriteDef[line] = newCond * -condType;
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
                                                uint entityVal = 0;
                                                if (val is int ival) entityVal = (uint)ival;
                                                else if (val is uint uval) entityVal = uval;
                                                if (entityVal > 0 && dupeEnemyMap.TryGetValue(entityVal, out List<uint> dupes))
                                                {
                                                    replaces[k] = dupes.Select(x => (object)(int)x).ToList();
                                                }
                                            }
                                        }
                                        bool isSkip = instr.Name != null && (instr.Name.StartsWith("SKIP IF") || instr.Name.StartsWith("SkipIf") || instr.Name.StartsWith("SKIP Uncond") || instr.Name.StartsWith("SkipUncond"));
                                        // if (callee.ID == 28002810) Console.WriteLine($"{instr} - has {string.Join(" ", replaces)}");
                                        int instrCount = e2.Instructions.Count;
                                        if (replaces.Count > 0)
                                        {
                                            // This is intentionally guarded behind having replaces, so that non-entity conds are left alone
                                            // TODO: This is quite broken in Sekiro now since it used Condition for duplicating compound conditions, which only works for 2x
                                            if (rewriteDef.TryGetValue(j, out int newCond))
                                            {
                                                instr[0] = (sbyte)newCond;
                                                instr.Save(pre);
                                                e2.Instructions[j] = instr.Val;
                                            }
                                            else if (rewriteMain.TryGetValue(j, out int mainCond))
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
                                                if (isSkip)
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
                                                    // In-place name editing with fixed entity
                                                    uint nameEntity;
                                                    if (instr[1] is int entityArg)
                                                    {
                                                        nameEntity = (uint)entityArg;
                                                    }
                                                    else if (!(instr[3] is int))
                                                    {
                                                        // This is also fine, HealthBarArg should handle variable names
                                                        // Elden Ring bad argument (but also we need emedf anyway)
                                                        nameEntity = 0;
                                                    }
                                                    else if (t.Dupe.HealthBar != null && uint.TryParse(t.Dupe.HealthBar.Split(' ')[0], out uint sourceNameEntity))
                                                    {
                                                        // With fixed name, entity should be provided by the config
                                                        nameEntity = dupeEnemyMap[sourceNameEntity][c];
                                                        // Console.WriteLine($"--------- your name is {nameEntity} for {t.Dupe.HealthBar}, with {revMapping[nameEntity]}");
                                                    }
                                                    else throw new Exception($"Not enough information to add health bar in {callee}: {instr}");
                                                    // Can also use getRandomizedSource here, but we've effectively already done the map lookup
                                                    if (nameEntity > 0 && mapping.GetSource(nameEntity, out uint sourceEntity))
                                                    {
                                                        // Console.WriteLine($"entity name {sourceEntity}->{nameEntity}");
                                                        instr[3] = GetCleverName((int)instr[3], sourceEntity, nameEntity);
                                                    }
                                                }
                                                // This should be redundant now?
                                                if (rewriteDef.TryGetValue(j, out int newCond2))
                                                {
                                                    instr[0] = (sbyte)newCond2;
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
                                            if (isSkip)
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
                                    uint source = mapping.RevMapping[t.Entity];
                                    if (startCmds.TryGetValue(source, out List<EMEVD.Instruction> cmds))
                                    {
                                        // reloc is empty for non-chr events, so make an imitation of it here.
                                        // DS3 uses multichr start sections, which is handled by segments
                                        Dictionary<int, int> bossReplace = new() { { (int)source, (int)t.Entity } };
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
                                    // This is awkward but segments will replace it eventually anyway
                                    uint source = mapping.RevMapping[t.Entity];
                                    Dictionary<int, int> bossReplace = new() { { (int)source, (int)t.Entity } };

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
                                    eventEditErrors.Add($"It was processed because {enemies.Name(t.Entity)} was randomized");
                                }
                                else if (t.Entities != null)
                                {
                                    eventEditErrors.Add($"It was processed because one of these enemies was randomized: {string.Join(", ", t.Entities.Split(' ').Select(uint.Parse).Select(enemies.Name))}");
                                }
                                if (edits.PreprocessError != null)
                                {
                                    eventEditErrors.Add(edits.PreprocessError);
                                }
                                eventEditErrors.Add($"Unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                                eventEditErrors.Add("");
                            }
                            bool useReplaceDupe = t.Dupe?.Type == "replace" && ev.DupeIndex >= 0;
                            if (!reloc.IsEmpty() || useReplaceDupe)
                            {
                                RelocMap autoDupeReloc = dupeReloc;
                                if (autoDupeReloc == null && useReplaceDupe)
                                {
                                    autoDupeReloc = dupeRelocs[ev.DupeIndex];
                                }
                                // if (callee.ID == 1051572822) Console.WriteLine($"{callee} init1: {init}");
                                // TODO: This doesn't work for region-only rewrites (e.g. DS1 11515110)
                                if (t.ArgEntities != null || t.ArgFlags != null || !argEntityAware)
                                {
                                    Dictionary<int, EventValueType> initTypes = null;
                                    if (argEntityAware && (t.ArgEntities != null || t.ArgFlags != null))
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
                                    if (!reloc.IsEmpty()) events.RewriteInitInts(init, reloc, initTypes);
                                    if (autoDupeReloc != null) events.RewriteInitInts(init, autoDupeReloc, initTypes);
                                }
                                init.Save(initOld);
                                if (e2 != null)
                                {
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j], pre);
                                        // if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e.ID}");
                                        if (!reloc.IsEmpty()) events.RewriteInts(instr, reloc);
                                        if (autoDupeReloc != null) events.RewriteInts(instr, autoDupeReloc);
                                        instr.Save(pre);
                                        // if (callee == 12505926) Console.WriteLine($"rewrite: {instr}");
                                    }
                                }
                            }
                            // TODO: Infrastructure for this. Asylum patrol behavior
                            if (e2 != null && e2.ID == 11810320)
                            {
                                e2.RestBehavior = EMEVD.Event.RestBehaviorType.Restart;
                            }
                            if (e2 != null && e2.ID != callee.ID && !forceRemove)
                            {
                                EMEVD.Instruction initInstr = init.Val;
                                // Look at sub-init, which results in different handling for inits
                                if (t.SubInit != null)
                                {
                                    SubInitKey subInitKey = new(entity, target, callee, null, 0);
                                    // Console.WriteLine($"From caller: {subInitKey}");
                                    subInitEvents[subInitKey] = e2;
                                }
                                if (subInitPairs.Contains(((int)e.ID, (int)callee.ID)))
                                {
                                    // This does not work at all with common_func (incorrect slot, callee lookup is based on caller map)
                                    SubInitKey subInitKey = new(entity, target, new EventKey(e.ID, entry.Key), callee, (int)originalInit[0]);
                                    // Console.WriteLine($"From callee: {subInitKey}");
                                    subInits[subInitKey] = initInstr;
                                    initInstr = null;
                                }
                                else if (e2.RestBehavior == EMEVD.Event.RestBehaviorType.End)
                                {
                                    // Some nested events have End behavior in DS1 because the outside event conditionally inits them at times.
                                    // But as new inits, make sure they don't get erased at bonfire sits.
                                    e2.RestBehavior = EMEVD.Event.RestBehaviorType.Restart;
                                }
                                // New event, so add to its target map and also add new initialization
                                string ownMap;
                                if (t.Type.Contains("copy"))
                                {
                                    ownMap = entry.Key;
                                }
                                else if (!ownerMap.TryGetValue(target, out ownMap))
                                {
                                    throw new Exception($"Can't add initialization for {entity}->{target}, it has no owner map");
                                }
                                // if (e2.ID == 1700548) Console.WriteLine($"Processing {entity}->{target} {t.Type}. e2 {e2?.ID}. adding to {ownMap}");
                                AddMulti(newInitializations, ownMap, (initInstr, e2));
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
                writeModifiedEvents(entry.Key, entry.Value);
            }
            if (emevds.ContainsKey("common_func"))
            {
                writeModifiedEvents("common_func", emevds["common_func"]);
            }
            if (modifiedEvents.Count > 0)
            {
                throw new Exception($"Internal error: modified events were not written to game files: {string.Join(", ", modifiedEvents.Keys)}");
            }

            if (eventEditErrors.Count > 0)
            {
                foreach (string err in eventEditErrors)
                {
                    Console.WriteLine(err);
                }
                throw new Exception($"Failed to edit event scripts in enemy randomizer, likely because merged mod is incompatible. See spoiler logs for details.");
            }

            // Deal with recursive DS1 inits here
            foreach ((SubInitKey callerKey, EMEVD.Event e) in subInitEvents)
            {
                // callerKey is partial, only missing callee and slot
                // SubInitKey subInitKey = new(entity, target, callee, null, 0);
                // SubInitKey subInitKey = new(entity, target, new EventKey((int)e.ID, entry.Key), callee, (int)originalInit[0]);
                OldParams initOld = OldParams.Preprocess(e);
                for (int i = 0; i < e.Instructions.Count; i++)
                {
                    Instr originalInit = events.Parse(e.Instructions[i], initOld);
                    if (!originalInit.Init) continue;
                    // Don't find actual callee, just match what's declared in the event
                    EventKey callee = new EventKey(originalInit.Callee, callerKey.Caller.Map);
                    int slot = (int)originalInit[0];
                    SubInitKey fullKey = new(callerKey.Source, callerKey.Target, callerKey.Caller, callee, slot);
                    if (subInits.TryGetValue(fullKey, out EMEVD.Instruction initInstr))
                    {
                        e.Instructions[i] = initInstr;
                    }
#if DEBUG
                    else
                    {
                        throw new Exception($"Missing expected sub-init {fullKey}");
                    }
#endif
                }
            }

            // --
            // -- Initialize predefined events
            // --

            EMEVD.Event parseEvent(IReadOnlyList<string> commands, int id, EMEVD.Event.RestBehaviorType restType)
            {
                // This could be in Events. Unlike NewEvent, it doesn't reuse any existing Event object.
                EMEVD.Event ev = new EMEVD.Event(id, restType);
                for (int i = 0; i < commands.Count; i++)
                {
                    (EMEVD.Instruction instr, List<EMEVD.Parameter> newPs) = events.ParseAddArg(commands[i], i);
                    ev.Instructions.Add(instr);
                    ev.Parameters.AddRange(newPs);
                }
                return ev;
            }
            Dictionary<string, NewEvent> newEvents = new();
            foreach (NewEvent e in eventConfig.NewEvents ?? new List<NewEvent>())
            {
                if (e.Commands == null)
                {
                    EMEVD.Event common = emevds["common_func"].Events.Find(c => c.ID == c.ID);
                    if (common == null) throw new Exception($"Error: event {e.Name} #{e.ID} missing from common_func");
                    newEvents[e.Name] = e;
                    e.Event = common;
                    continue;
                }
                // TODO verify for DS3/Sekiro? that this is the correct restart behavior for custom events
                // In DS1, restart is necessary for skeleton, passive, novagrant, somevagrant. In ER, especially necessary for teamtype
                EMEVD.Event ev = parseEvent(events.Decomment(e.Commands), e.ID, EMEVD.Event.RestBehaviorType.Restart);
                e.Event = ev;
                if (e.Name == null)
                {
                    EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 });
                    AddMulti(newInitializations, "common", (init, ev));
                }
                else if (game.DS1)
                {
                    // No common_func in DS1, use per-map cached copy instead
                    newEvents[e.Name] = e;
                    continue;
                }
                else
                {
                    newEvents[e.Name] = e;
                    AddMulti(newInitializations, "common_func", ((EMEVD.Instruction)null, ev));
                }
            }

            // Map from (event, map) -> event id
            Dictionary<(string, string), int> mapEventIds = new();
            void addCommonFuncInit(string name, uint target, List<object> args)
            {
                if (game.DS1)
                {
                    (string, string) key = (name, ownerMap[target]);
                    EMEVD.Event ev = null;
                    if (!mapEventIds.TryGetValue(key, out int eventId))
                    {
                        eventId = NewEventID(false);
                        NewEvent newEv = newEvents[name];
                        ev = new EMEVD.Event(eventId, newEv.Event.RestBehavior);
                        ev.Instructions = newEv.Event.Instructions;
                        ev.Parameters = newEv.Event.Parameters;
                        mapEventIds[key] = eventId;
                    }
                    // Maybe can combine with bottom section
                    List<object> startArgs = new List<object>() { -1, eventId };
                    EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, startArgs.Concat(args));
                    AddMulti(newInitializations, ownerMap[target], (init, ev));
                }
                else
                {
                    List<object> startArgs = new List<object>();
                    if (game.EldenRing) startArgs.Add(0);
                    startArgs.Add(newEvents[name].ID);
                    EMEVD.Instruction init = new EMEVD.Instruction(2000, 6, startArgs.Concat(args));
                    AddMulti(newInitializations, ownerMap[target], (init, (EMEVD.Event)null));
                }
            }
            void addEnemyDrop(uint target, int itemLot, int itemFlag)
            {
                if (game.EldenRing)
                {
                    // Handling of dupe drops, currently for Elden Ring only
                    if (infos.TryGetValue(target, out EnemyInfo targetInfo) && targetInfo.DupeFrom > 0)
                    {
                        return;
                    }
                    if (dupeEnemyMap.TryGetValue(target, out List<uint> dupes))
                    {
                        List<string> cmds = new()
                        {
                            "EndIfPlayerIsInWorldType(EventEndType.End, WorldType.OtherWorld)",
                            $"IfEventFlag(AND_01, ON, TargetEventFlagType.EventFlag, {itemFlag})",
                            "EndIfConditionGroupStateUncompiled(EventEndType.End, PASS, AND_01)",
                            $"IfCharacterDeadalive(AND_02, {target}, DeathState.Dead, ComparisonType.Equal, 1)",
                        };
                        foreach (uint dupe in dupes)
                        {
                            cmds.Add($"IfCharacterDeadalive(AND_02, {dupe}, DeathState.Dead, ComparisonType.Equal, 1)");
                        }
                        cmds.AddRange(new[]
                        {
                            "IfConditionGroup(MAIN, PASS, AND_02)",
                            $"AwardItemsIncludingClients({itemLot})",
                        });
                        EMEVD.Event ev = parseEvent(cmds, NewEventID(), EMEVD.Event.RestBehaviorType.Restart);
                        EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 });
                        AddMulti(newInitializations, ownerMap[target], (init, ev));
                        return;
                    }
                }
                addCommonFuncInit("enemydrop", target, new List<object> { target, itemLot, itemFlag });
            }
            Dictionary<uint, int> targetSourceNPCs = new();
            foreach ((uint target, uint source) in mapping.RevMapping)
            {
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
                                EMEVD.Event ev = new EMEVD.Event(NewEventID(), EMEVD.Event.RestBehaviorType.Restart);
                                ev.Instructions.AddRange(events.RewriteCondGroup(after, new Dictionary<int, int> { { (int)source, (int)target } }, 0));
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
                        foreach (uint helperTarget in mapping.GetHelperTargets(target))
                        {
                            addCommonFuncInit("greatwoodfall", target, new List<object> { helperTarget });
                        }
                    }
                }
                else if (game.DS1)
                {
#if DS1
                    if (target == 1200801)
                    {
                        int darkrootFallId = DS1EnemyEditor.DarkrootFallID;
                        // Currently only main bosses are allowed outside arena, helpers are not
                        if (!infos[source].HasTag("outside"))
                        {
                            addCommonFuncInit("butterflyfall", target, new List<object> { target, darkrootFallId });
                        }
                        foreach (uint helperTarget in mapping.GetHelperTargets(target))
                        {
                            addCommonFuncInit("butterflyfall", target, new List<object> { helperTarget, darkrootFallId });
                        }
                    }
#endif
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
                uint baseTarget = target;
                uint enemySource = source;
                if (!infos.ContainsKey(target) && sourceInfo.OwnedBy > 0)
                {
                    // Helper mapping is from owner target and source helper to target helper
                    // TODO... can this be faster
                    uint ownerTarget = mapping.HelperMapping
                        .Where(e => e.Key.Item2 == source && e.Value == target)
                        .Select(e => e.Key.Item1)
                        .FirstOrDefault();
                    if (ownerTarget > 0)
                    {
                        baseTarget = ownerTarget;
                        if (mapping.SwapMapping.TryGetValue((baseTarget, source), out uint swapSource))
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
                    string scaleEvent = game.DS1 ? "speffect" : "scale";
                    if (game.EldenRing &&
                        (sourceInfo.Class == EnemyClass.Helper || sourceInfo.IsImportantTarget || targetInfo.HasTag("generated")))
                    {
                        scaleEvent = "scale2";
                    }
                    else if (game.DS1 && (sourceInfo.HasTag("generated") || targetInfo.HasTag("generated")))
                    {
                        // Not sure if needed, but better be safe
                        scaleEvent = "speffect2";
                    }
                    getScalingSections(enemySource, baseTarget, out int sourceSection, out int targetSection);
                    bool fixedXp = targetInfo.IsFixedSource;
                    if (sourceSection > 0 && targetSection > 0
                        && scalingSpEffects.Areas.TryGetValue((sourceSection, targetSection), out ScalingEffects.AreaScalingValue sp))
                    {
                        int scaleSp = fixedXp ? (sourceInfo.HasTag("nonunique_") ? sp.FixedScaling : sp.UniqueFixedScaling) : sp.RegularScaling;
                        // if (target == 20010801) Console.WriteLine($"For {source}->{target}, picked {scaleSp} ({fixedXp}, {sourceInfo.HasTag("nonunique")})");
                        addCommonFuncInit(scaleEvent, target, new List<object> { target, scaleSp });
                        // Additional scaling for Gundyr and First Lizard, because no upgrades of any kind
                        if (targetInfo.HasTag("earlyscale") && !sourceInfo.HasTag("early"))
                        {
                            addCommonFuncInit(scaleEvent, target, new List<object> { target, scalingSpEffects.TutorialScaling });
                        }
                        if (targetInfo.IsImportantTarget && (sourceInfo.IsImportantTarget || sourceInfo.Class == EnemyClass.Helper)
                            && targetInfo.HasTag("tutscale") != sourceInfo.HasTag("tutscale"))
                        {
                            int tutSp = targetInfo.HasTag("tutscale") ? scalingSpEffects.TutorialDownScaling : scalingSpEffects.TutorialUpScaling;
                            addCommonFuncInit(scaleEvent, target, new List<object> { target, tutSp });
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
                    // Same condition used for bosschr type events, including Serpent Hunter activation
                    if (targetInfo.IsImportantTarget && !targetInfo.IsBossTarget && scalingSpEffects.RykardScaling > 0
                        && (source == 16000800 || source == 16000801))
                    {
                        addCommonFuncInit(scaleEvent, target, new List<object> { target, scalingSpEffects.RykardScaling });
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
                if (game.DS1 && targetInfo.HasTag("edge"))
                {
                    // This applies to Kalameet and Sanctuary Guardian currently, so just bosses/minibosses. (scale2 probably not needed)
                    addCommonFuncInit("speffect", target, new List<object> { target, 88020 });
                }
#if DEV
                if (game.EldenRing && opt["idtest"])
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
                foreach (uint baseTarget in importantDupes)
                {
                    int count = dupeCount(baseTarget);
                    if (count == 0) continue;
                    List<string> models = new();
                    List<uint> targets = new();
                    for (int dupeIndex = -1; dupeIndex < count; dupeIndex++)
                    {
                        mapping.GetRandomizedSource(baseTarget, dupeIndex, out uint source, out uint realTarget);
                        string model = defaultData.TryGetValue(source, out EnemyData data) ? data.Model : null;
                        models.Add(model);
                        targets.Add(realTarget);
                    }
                    for (int i = 0; i < Math.Min(targets.Count, npcs.DupeBossSpEffects.Count); i++)
                    {
                        if (models[i] == null || models.Count(m => m == models[i]) <= 1) continue;
                        uint target = targets[i];
                        addCommonFuncInit("scale2", target, new List<object> { target, npcs.DupeBossSpEffects[i] });
                        foreach (uint helperTarget in mapping.GetHelperTargets(target))
                        {
                            addCommonFuncInit("scale2", helperTarget, new List<object> { helperTarget, npcs.DupeBossSpEffects[i] });
                        }
                    }
                }
            }
            foreach (EnemyData data in defaultData.Values)
            {
                // Also add self-mapping here, so that NPC attributes can be added back for enemies where it was removed
                // TODO dupe: also add dupes here? mainly for teamtype
                if (!mapping.GetSource(data.ID, out _) && infos.ContainsKey(data.ID))
                {
                    targetSourceNPCs[data.ID] = data.NPC;
                }
            }
            // NPC effects to move around, or re-apply to vanilla enemies
            // Infighting maybe uses Enemy2 (24) before Elden Ring, but Liurnia uses these other ones.
            // However, this was still not enough to make them fight in onslaught.
            byte[] acceptableDupeTeams = game.EldenRing ? new byte[] { 24, 48, 54, 55 } : Array.Empty<byte>();
            foreach ((uint target, int npc) in targetSourceNPCs)
            {
                EnemyInfo sourceInfo = mapping.GetSource(target, out uint source) ? infos[source] : infos[target];
                infos.TryGetValue(target, out EnemyInfo targetInfo);
                bool generated = targetInfo?.HasTag("generated") ?? false;
                // The target info here comes from baseTarget for randomized enemies, so it should work for randomized helpers
                bool noSwamp = targetInfo?.HasTag("noswamp") ?? false;
                bool manualTeam = targetInfo?.HasTag("manualteam") ?? false;
                // Scale is just speffect application, so it also works for e.g. poison resistance
                // For dragon: 2049430800 -> 58600090 -> 20011666
                if (npcs.NpcCopySpEffect.TryGetValue(npc, out List<int> sps))
                {
                    string scaleEvent = game.DS1 ? "speffect" : "scale";
                    if (game.EldenRing && (sourceInfo.Class == EnemyClass.Helper || sourceInfo.IsImportantTarget || generated))
                    {
                        scaleEvent = "scale2";
                    }
                    // We could also be precious about this and skip speffects already defined in source NPC
                    foreach (int sp in sps)
                    {
                        if (noSwamp && npcs.ResistSps.Contains(sp)) continue;
                        addCommonFuncInit(scaleEvent, target, new List<object> { target, sp });
                    }
                }
                // Bosses which are better balanced as StrongEnemy (7), mainly Tibia Mariner killing its helper skeletons.
                // Make this take priority over the other case for now, as some targets like Windmill Apostle have 6 hardcoded.
                if (sourceInfo.HasTag("strong") && targetInfo.IsImportantTarget && !dupeEnabled(target))
                {
                    addCommonFuncInit("teamtype", target, new List<object> { target, 7 });
                }
                // Otherwise, add infighting if the original enemy had it, or the original owner had it, and not overpowered
                // TODO: Dupe is a bit tricky, as dupeEnabled does not catch all cases (e.g. helpers? should use getBaseTarget?)
                else if (npcs.NpcOriginalTeam.TryGetValue(npc, out byte team) && !sourceInfo.HasTag("ultra") && !sourceInfo.HasTag("multi") && !manualTeam
                    && (!anyDupeEnabled || acceptableDupeTeams.Contains(team)))
                {
                    addCommonFuncInit("teamtype", target, new List<object> { target, team });
                }
                // Probably only allow base targets to drop items. lot is (item lot, event flag)
                // This is based on the target's original NPC id, although in the case of chest->mimic, the
                // target's NPC id is a lie and its lot really belongs to a different mimic. So exclude that.
                if (npcs.NpcItemLots.TryGetValue(npc, out (int, int) lot)
                    && sourceInfo.Class != EnemyClass.Helper
                    && !newMimics.ContainsKey(target))
                {
                    addEnemyDrop(target, lot.Item1, lot.Item2);
                }
                // Finally, easier scaling for enemy onslaught
                // Use a similar condition as boss-as-basic scaling, but applied to both source and target being fixed
                // Alternatively, we could change the conditions to apply to all boss targets, regardless of source classification
                if (opt["scale"] && presetOpt("multhp") && dupeEnabled(target)
                    && targetInfo != null && targetInfo.IsFixedSource
                    && bossAsBasicNpc.ContainsKey(sourceInfo.ID) && (sourceInfo.Class != EnemyClass.Helper || sourceInfo.HasTag("mainhelper")))
                {
                    uint baseTarget = mapping.GetBaseTarget(target, out _);
                    int baseCount = dupeCount(baseTarget);
                    addCommonFuncInit("scale2", target, new List<object> { target, scalingSpEffects.GetDupeScaling(baseCount) });
                }
            }
            foreach ((uint target, ChestInfo chest) in newMimics)
            {
                addEnemyDrop(target, chest.ItemLot, chest.EventFlag);
                addCommonFuncInit("showmimic", target, new List<object> { chest.EventFlag, target, chest.EventFlag });
            }
            if (opt["yhormruler"] && mapping.GetTargets(3900800, out List<uint> yhormTargets))
            {
                HashSet<string> yhormMaps = new HashSet<string>();
                foreach (uint yhorm in yhormTargets)
                {
                    if (!yhormMaps.Add(ownerMap[yhorm])) continue;
                    // This could also be in common, but we want to conditionally initialize it
                    addCommonFuncInit("yhormruler", yhorm, new List<object> { });
                }
            }

#if DS1
            // Misc map edits for DS1. Could combine into one pass
            // This needs to be encapsulated somewhere reasonable
            if (game.DS1)
            {
                // Make all dead end bonfires in boss rooms always usable. This could be what backread management is for.
                HashSet<int> alwaysBonfires = new() { 1211950, 1311950, 1411950, 1601950, 1701950 };
                foreach ((string map, IMsb imsb) in game.DS1Maps)
                {
                    if (imsb is not MSB1 msb) continue;
                    HashSet<int> vagrants = new();
                    foreach (MSB1.Part.Collision col in msb.Parts.Collisions)
                    {
                        for (int i = 0; i < col.VagrantEntityIDs.Length; i++)
                        {
                            int id = col.VagrantEntityIDs[i];
                            if (id > 0 && infos.TryGetValue((uint)id, out EnemyInfo info) && info.Class != EnemyClass.None)
                            {
                                // Evil vagrant locations should not have network condition to spawn and should be controlled by scripting
                                col.VagrantEntityIDs[i] = -1;
                                if (!opt["permavagrant"] && vagrants.Add(id))
                                {
                                    addCommonFuncInit("somevagrant", info.ID, new List<object> { info.ID });
                                }
                            }
                        }
                        if (alwaysBonfires.Contains(col.DisableBonfireEntityID))
                        {
                            col.DisableBonfireEntityID = -1;
                        }
                    }
                    // This takes some time to crunch but very likely worth it
                    HashSet<int> usedRegions = new();
                    foreach (MSB1.Region r in msb.Regions.Regions)
                    {
                        if (r.EntityID <= 0) continue;
                        if (!usedRegions.Add(r.EntityID))
                        {
                            throw new Exception($"Duplicate region {r.EntityID} in {map}");
                        }
                    }
                    // Make skeletons not completely trivial sometimes
                    List<MSB1.Part.Enemy> necromancers = new();
                    foreach (MSB1.Part.Enemy e in msb.Parts.Enemies)
                    {
                        if (e.ModelName == "c2650" && e.EntityID > 0
                            && infos.TryGetValue((uint)e.EntityID, out EnemyInfo info) && info.Class == EnemyClass.Basic
                            && !info.HasTag("gravelord"))
                        {
                            necromancers.Add(e);
                        }
                    }
                    if (necromancers.Count > 0)
                    {
                        foreach (MSB1.Part.Enemy e in msb.Parts.Enemies)
                        {
                            if (e.ModelName == "c2900" && e.EntityID > 0
                                && infos.TryGetValue((uint)e.EntityID, out EnemyInfo info)
                                // Just in case, like if the enemies are in different asylum partitions. TODO maybe require backread
                                && info.Class == EnemyClass.Basic && !info.HasTag("exclude:unkillable")
                                && mapping.GetSource(info.ID, out uint source)
                                && npcs.SkeletonNpcs.Contains(defaultData[source].NPC))
                            {
                                MSB1.Part.Enemy e2 = necromancers.MinBy(e2 => Vector3.DistanceSquared(e.Position, e2.Position));
                                float dist = Vector3.DistanceSquared(e.Position, e2.Position);
                                float yDist = Math.Abs(e.Position.Y - e2.Position.Y);
                                if (dist > 30 * 30) continue;
                                bool sameCol = e.CollisionName == e2.CollisionName;
                                if (!sameCol)
                                {
                                    MSB1.Part.Collision col1 = msb.Parts.Collisions.Find(c => c.Name == e.CollisionName);
                                    MSB1.Part.Collision col2 = msb.Parts.Collisions.Find(c => c.Name == e2.CollisionName);
                                    if (col1.DrawGroups.Where((d, i) => (d & col2.DispGroups[i]) != 0).Any())
                                    {
                                        sameCol = true;
                                    }
                                }
                                // Console.WriteLine($"{map} #{e.EntityID} has #{e2.EntityID} at {Math.Sqrt(dist)}, {yDist}{(dist < 10 * 10 ? "**" : "")}{(sameCol ? " <<<" : "")}");
                                if (sameCol)
                                {
                                    addCommonFuncInit("skeleton", info.ID, new List<object> { e2.EntityID, info.ID });
                                }
                            }
                        }
                    }
                }
                // Finally add scripted gravelording. For now, always in operation (flags are just ignored with peramgravelord).
                // 11805430, 20 flags: for selecting gravelord map
                // 11805891: gravelord active flag, instead of 732/735 (not needed)
                // 11800600, 10 flags: for counting up gravelord timer
                // 11800610, 20 flags: current gravelord map.
                int timerFlag = 11800600;
                int currentFlag = 11800610;
                int selectFlag = 11805430;
                int selectRange = 15;
                int selectMax = selectFlag + selectRange - 1;
                List<string> gravelordMaps = new();
                foreach ((string map, (int startFlag, int endFlag)) in gravelordFlags)
                {
                    if (startFlag <= 0 || endFlag <= 0) throw new Exception($"Config error for {map}: missing gravelord flag");
                    gravelordMaps.Add(map);
                }
                if (gravelordMaps.Count < 10 || gravelordMaps.Count > selectRange)
                {
                    throw new Exception($"Config error: unexpected gravelord map count [{string.Join(", ", gravelordMaps)}]");
                }
                // gravelordMaps.RemoveRange(2, gravelordMaps.Count - 2);
                int currentMax = currentFlag + gravelordMaps.Count - 1;
                // Specialized event routines for common gravelord events
                EMEVD.Event addGravelordEvent(IReadOnlyList<string> cmds)
                {
                    // All events can be default (don't affect enemies directly)
                    EMEVD.Event ev = parseEvent(cmds, NewEventID(false), EMEVD.Event.RestBehaviorType.Default);
                    AddMulti(newInitializations, "common", ((EMEVD.Instruction)null, ev));
                    return ev;
                }
                void addGravelordInit(EMEVD.Event ev, params object[] args)
                {
                    EMEVD.Instruction init = new EMEVD.Instruction(2000, 0, new List<object> { -1, (uint)ev.ID }.Concat(args));
                    AddMulti(newInitializations, "common", (init, (EMEVD.Event)null));
                }
                // Timer event, counts up while active (using batch flag range)
                EMEVD.Event timerEvent = addGravelordEvent(new[]
                {
                    $"IfBatchEventFlags(MAIN, LogicalOperationType.NotAllOFF, TargetEventFlagType.EventFlag, {currentFlag}, {currentMax})",
                    "WaitFixedTimeSeconds(5)",
                    // "AwardItemLot(5010)",
                    $"IncrementEventValue({timerFlag}, 10, 1000)",
                    "EndUnconditionally(EventEndType.Restart)",
                });
                addGravelordInit(timerEvent, 0);
                // Activate event, if none active or timer maxed out
                List<string> changeCmds = new()
                {
                    // This could be brutal at the start, but it's needed for the timer to count (could make it independent of flags altogether)
                    $"IfBatchEventFlags(OR_01, LogicalOperationType.AllOFF, TargetEventFlagType.EventFlag, {currentFlag}, {currentMax})",
                    // 600 seconds is 120 count. In binary 1111000. Make this 300 seconds for 60 count
                    $"IfEventValue(OR_01, {timerFlag}, 10, ComparisonType.GreaterOrEqual, 60)",
                    $"IfConditionGroup(MAIN, PASS, OR_01)",
                    // Stopgap to prevent issues, surely won't cause issues of its own
                    $"SkipIfBatchEventFlags(1, LogicalOperationType.NotAllON, TargetEventFlagType.EventFlag, {currentFlag}, {currentMax})",
                    $"BatchSetEventFlags({currentFlag}, {currentMax}, OFF)",
                    // Use non-even number for good distribution
                    $"BatchSetEventFlags({selectFlag}, {selectMax}, OFF)",
                    $"RandomlySetEventFlagInRange({selectFlag}, {selectMax}, ON)",
                };
                // Avoid setting same map. This is the most fragile part of this by far. Also exclude stat-only maps.
                for (int i = 0; i < selectRange; i++)
                {
                    int selectMap = selectFlag + i;
                    int currentMap = currentFlag + i;
                    if (i < gravelordMaps.Count)
                    {
                        changeCmds.Add($"SkipIfEventFlag(1, OFF, TargetEventFlagType.EventFlag, {selectMap})");
                        changeCmds.Add($"EndIfEventFlag(EventEndType.Restart, ON, TargetEventFlagType.EventFlag, {currentMap})");
                    }
                    else
                    {
                        changeCmds.Add($"SkipIfEventFlag(1, OFF, TargetEventFlagType.EventFlag, {selectMap})");
                        changeCmds.Add("EndUnconditionally(EventEndType.Restart)");
                    }
                }
                // Then just copy over and restart. Select flag is temporary and cleared before random anyway.
                changeCmds.Add($"BatchSetEventFlags({currentFlag}, {currentMax}, OFF)");
                for (int i = 0; i < selectRange; i++)
                {
                    int selectMap = selectFlag + i;
                    int currentMap = currentFlag + i;
                    if (i < gravelordMaps.Count)
                    {
                        changeCmds.Add($"SkipIfEventFlag(1, OFF, TargetEventFlagType.EventFlag, {selectMap})");
                        changeCmds.Add($"SetEventFlag({currentMap}, ON)");
                    }
                }
                changeCmds.Add($"ClearEventValue({timerFlag}, 10)");
                changeCmds.Add("EndUnconditionally(EventEndType.Restart)");
                EMEVD.Event changeEvent = addGravelordEvent(changeCmds);
                addGravelordInit(changeEvent, 0);
                // Common event with many inits to sync flag states in any map. X0_4 our flag, X4_4 start, X8_4 end
                // This could probably be in event config, but init is tricky
                EMEVD.Event syncEvent = addGravelordEvent(new[] {
                    $"IfEventFlag(AND_01, ON, TargetEventFlagType.EventFlag, X0_4)",
                    "IfEventFlag(AND_01, OFF, TargetEventFlagType.EventFlag, X4_4)",
                    "IfEventFlag(AND_02, ON, TargetEventFlagType.EventFlag, X4_4)",
                    "IfEventFlag(AND_02, OFF, TargetEventFlagType.EventFlag, X0_4)",
                    "IfConditionGroup(OR_01, PASS, AND_01)",
                    "IfConditionGroup(OR_01, PASS, AND_02)",
                    "IfConditionGroup(MAIN, PASS, OR_01)",
                    // In this case, our flag is on, map flag is not. Enable gravelording
                    "SkipIfConditionGroupStateCompiled(2, FAIL, AND_01)",
                    "SetEventFlag(X4_4, ON)",
                    "EndUnconditionally(EventEndType.Restart)",
                    // In the other case, map flag is on, our flag is not. Disable gravelording
                    // Do this with end flag, and in case the map event is not loaded, disable it right after
                    "SetEventFlag(X4_4, OFF)",
                    "SetEventFlag(X8_4, ON)",
                    "WaitFixedTimeSeconds(1)",
                    "SetEventFlag(X8_4, OFF)",
                    "EndUnconditionally(EventEndType.Restart)",
                });
                for (int i = 0; i < gravelordMaps.Count; i++)
                {
                    if (opt["nogravelord"]) break;
                    int currentMap = currentFlag + i;
                    (int startFlag, int endFlag) = gravelordFlags[gravelordMaps[i]];
                    addGravelordInit(syncEvent, currentMap, startFlag, endFlag);
                }
            }
            // Last pass for animations
            if (game.DS1 && specialAnimations.Count > 0)
            {
                foreach ((uint animTarget, SpecialAnim anim) in specialAnimations)
                {
                    if (anim.Type != "passive") continue;
                    // TODO: Again dupes and stuff
                    if (mapping.GetSource(animTarget, out uint animSource))
                    {
                        int initAnim = -1;
                        if (specialAnimations.TryGetValue(animSource, out SpecialAnim sourceAnim) && sourceAnim.Type == "passive")
                        {
                            initAnim = sourceAnim.InitAnim;
                        }
                        if (initAnim > 0)
                        {
                            eventInitialAnimations[animTarget] = initAnim;
                        }
                        else
                        {
                            // No anim in map data, so enforce passivity with event script (incompatible with invincible anims!)
                            addCommonFuncInit("passive", animTarget, new List<object> { animTarget });
                        }
                    }
                }
                // Go through all map enemies and assign anims. This is probably better than id lookup
                foreach (MSB1 msb in game.DS1Maps.Values)
                {
                    foreach (MSB1.Part.Enemy e in msb.Parts.Enemies)
                    {
                        if (e.EntityID <= 0) continue;
                        uint target = (uint)e.EntityID;
                        if (eventInitialAnimations.TryGetValue(target, out int animId))
                        {
                            e.InitAnimID = animId;
                        }
                        // Very custom pass for rockworms. Set initial position based on orientation and animation used (usedCancelAnims)
                        if (e.ModelName == "c3390" && mapping.GetSource(target, out uint source))
                        {
                            int npc = defaultData[source].NPC;
                            // 339001 upright, offset 10000
                            // 339002 on wall, offset 20000
                            // 339003 on ceiling, offset 30000
                            // Apparently none of them use root motion after all in 9060
                            // But whatever, the code is here. Can also use offset between actual position and primary arena in main pass
                            if (npc == 339003)
                            {
                                // Hopefully this doesn't interfere with anything above it
                                e.Position += new Vector3(0, 6.5f, 0);
                            }
                        }
                    }
                }
            }
#endif

            // Reverse mapping for Elden Ring, where we may need to create new emevds
            foreach (string map in newInitializations.Keys)
            {
                if (emevds.ContainsKey(map)) continue;
                Console.WriteLine($"Unknown event map target {map}, enemy scripting and scaling will not apply there");
            }
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                if (!newInitializations.ContainsKey(entry.Key)) continue;

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
                        EventKey key = new EventKey(e.ID, entry.Key);
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

                // Some custom edits. This does not change WriteEmevds
                foreach (EMEVD.Event e in emevd.Events)
                {
                    EventKey key = new EventKey(e.ID, entry.Key);
                    if (game.Sekiro && opt["openstart"] && entry.Key == "common" && e.ID == 0)
                    {
                        // Open Senpou door (set objact event flag)
                        emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2003, 2, new List<object> { 62000503, (byte)1 }));
                    }
                    bool foundDefeat = false;
                    bool foundSave = false;
                    // TODO: Do this properly, may need event infrastructure, or just segment rewrite fix
                    bool leyndellConstructor = game.EldenRing && e.ID == 0 && entry.Key == "m11_00_00_00";
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        foundDefeat |= ins.Bank == 2003 && ins.ID == 12;
                        foundSave |= ins.Bank == 2000 && ins.ID == 5;
                        bool shouldRemove = false;
                        // Try to make this fast to avoid unnecessary parse
                        // SetCharacterImmortality
                        // TODO: Test this still works after restructure
                        if (game.Sekiro && ins.Bank == 2004 && ins.ID == 12)
                        {
                            Instr instr = events.Parse(e.Instructions[i]);
                            if (instr.Name == "Set Character Immortality" && instr[0] is int imm)
                            {
                                // TODO: DS3 probably needs a system like this (and it uses a lot more parameterization). As of now this does nothing, though
                                if (removeImmortality.Contains((uint)imm))
                                {
                                    shouldRemove = true;
                                }
                                // For some reason, one of Owl's start events is parameterized.
                                // We could find out what value this parameter has from its initialization, but in this case it works to use start template as a proxy.
                                else if (imm == 0 && templates.TryGetValue(key, out EventSpec spec) && spec.Template.Any(t => t.Type.StartsWith("start") && removeImmortality.Contains(t.Entity)))
                                {
                                    shouldRemove = true;
                                }
                            }
                        }
                        // Difficult to remove normally because it's in preconstructor
                        else if (game.DS1 && e.ID == 50 && ins.Bank == 2004 && ins.ID == 2)
                        {
                            Instr instr = events.Parse(e.Instructions[i]);
                            if (instr.Name == "SetCharacterTeamType")
                            {
                                int id = (int)instr[0];
                                if (id == 1100160 || id == 1510650)
                                {
                                    // TODO: EnemyInfo tag
                                    shouldRemove = true;
                                }
                            }
                        }
                        // Another constructor edit, EndIfEventFlag 11810000 in Asylum
                        else if (game.DS1 && e.ID == 0 && ins.Bank == 1003 && ins.ID == 2 && entry.Key == "m18_01_00_00")
                        {
                            shouldRemove = true;
                        }
                        else if (leyndellConstructor && ins.Bank == 2000 && ins.ID == 6)
                        {
                            Instr instr = events.Parse(ins, OldParams.Preprocess(e));
                            if (instr.Init && instr.Callee == 9005810 && (int)instr[instr.Offset + 3] == 11001950)
                            {
                                instr[instr.Offset + 4] = 0.0f;
                                instr.Save();
                                e.Instructions[i] = instr.Val;
                            }
                        }
                        if (shouldRemove)
                        {
                            EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                            e.Instructions[i] = newInstr;
                            // Just in case...
                            e.Parameters.RemoveAll(p => p.InstructionIndex == i);
                        }
                    }
                    if (game.EldenRing && !entry.Key.StartsWith("common") && foundDefeat && !foundSave)
                    {
                        // Quick hack for sitting-at-grace crashes in Elden Ring. Doesn't work for minibosses
                        e.Instructions.Add(new EMEVD.Instruction(2000, 5, new List<object> { 0 }));
                    }
                }
            }

            // Misc post-randomization edits
            enemyEditor.FinalNpcPass(state, opt);

#if ER
            // Currently specific to Elden Ring, but the same routine should be shared if used elsewhere
            if (opt["swaprewards"])
            {
                // TODO: Oof
                // This should be rewritten to not use scaling factors naively, but adjust the location based on how well the original boss fits the scaling factors
                bool debugBonus = opt["debugbonus"];
                Dictionary<uint, int> bossSouls = new();
                foreach (PARAM.Row row in Params["GameAreaParam"].Rows)
                {
                    uint amt = (uint)row["bonusSoul_single"].Value;
                    if (amt > 0)
                    {
                        bossSouls[(uint)row.ID] = (int)amt;
                    }
                }
                int originalTotal = 0;
                Dictionary<int, int> results = new Dictionary<int, int>();
                foreach (PARAM.Row row in Params["GameAreaParam"].Rows)
                {
                    uint mainTarget = (uint)row.ID;
                    if (!infos.ContainsKey(mainTarget) || !bossSouls.ContainsKey(mainTarget)) continue;
                    int originalAmount = bossSouls[mainTarget];
                    List<uint> targets = new List<uint> { mainTarget };
                    List<(uint, uint)> sourceMapping = new();
                    if (multiPhase.ContainsKey(mainTarget))
                    {
                        targets.AddRange(multiPhase.Where(e => e.Key != mainTarget && e.Value == mainTarget).Select(e => e.Key));
                    }
                    // Just add up the bosses
                    foreach (uint target in targets)
                    {
                        if (infos[target].DupeFrom > 0) continue;
                        int count = dupeCount(target);
                        for (int dupeIndex = -1; dupeIndex < count; dupeIndex++)
                        {
                            mapping.GetRandomizedSource(target, dupeIndex, out uint source, out uint realTarget);
                            sourceMapping.Add((target, source));
                        }
                    }
                    int total = 0;
                    foreach ((uint target, uint source) in sourceMapping)
                    {
                        // Start with base soul amount.
                        // Modifiers to souls: scaling tiers, dupe amount (GetXpRate), phase HP
                        double baseAmt = 0;
                        uint mainSource = multiPhase.TryGetValue(source, out uint nextSource) ? nextSource : source;
                        // Some hardcoding for double-horse bosses. Should this try some kind of averaging...? bleh
                        if (mainSource == 1041510801)
                        {
                            mainSource = 1041510800;
                        }
                        else if (mainSource == 1248550801)
                        {
                            mainSource = 1248550800;
                        }
                        if (bossSouls.TryGetValue(mainSource, out int bossAmt))
                        {
                            baseAmt = bossAmt;
                            if (multiPhase.ContainsKey(source))
                            {
                                // Could divvy up by phase difficulty.
                                // Inflating here is not great since it would cause recombined phases to have a different total.
                                baseAmt /= 2;
                            }
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
                        double dupeMult = ScalingEffects.GetXpModifier(dupeCount(target), presetOpt("multhp"));
                        double dupeAmt = baseAmt * dupeMult;
                        double scaleAmt, scalePhaseAmt;
                        // As with all things scaling, much logic is duplicated in different places
                        if (opt["scale"]
                            && infos.TryGetValue(source, out EnemyInfo sourceInfo)
                            && infos.TryGetValue(target, out EnemyInfo targetInfo))
                        {
                            getScalingSections(source, target, out int sourceSection, out int targetSection, true);
                            // Hacky hardcoding for Ekzykes to counteract manualEntityTiers
                            // if (source == 1048370800 && sourceSection == 15) sourceSection = 11;
                            double scaleMult = 1;
                            if (sourceSection > 0 && targetSection > 0)
                            {
                                scaleMult = ScalingEffects.EldenSoulScaling[targetSection - 1] / ScalingEffects.EldenSoulScaling[sourceSection - 1];
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
                            double amtLost = originalAmount - scalePhaseAmt;
                            double skewedAverage = scalePhaseAmt;
                            string comb = "";
                            if (amtLost > 1)
                            {
                                // At 4 (10k diff), give 1/2 weight to higher. e.g. 5k -> 10k
                                // At 5 (100k diff), give 1/100 weight. 5k -> 6k
                                // But the averaging amount should be based on the relative order of magnitudes of the numbers, not their diff.
                                // But this should not be done here, as it's comparing half phase against full phase
                                double logDiff = Math.Log10(originalAmount) - Math.Log10(scalePhaseAmt);
                                double highWeight;
                                if (logDiff < 1)
                                {
                                    // Can this skew higher up when they're close together?
                                    highWeight = 0.25;
                                }
                                else
                                {
                                    highWeight = 0.25 * Math.Pow(0.02, Math.Log10(amtLost));
                                }
                                highWeight = Math.Clamp(highWeight, 0, 1);
                                skewedAverage = originalAmount * highWeight + scalePhaseAmt * (1 - highWeight);
                                // comb = $", combined {roundBonusSoul((int)skewedAverage)}";
                            }
                            double hiddenMult = 1;
                            if (hiddenMult != 1)
                            {
                                comb = $" -> adjusted {roundBonusSoul((int)(hiddenMult * scalePhaseAmt))}";
                            }
                            Console.WriteLine($"Target {ename(target)} <- {ename(source)}: {baseAmt} -> dupe {dupeAmt:0} -> scale {scaleAmt:0} -> final {roundBonusSoul((int)scalePhaseAmt)} vs original {originalAmount}{comb}");
                        }
                        total += (int)scalePhaseAmt;
                    }
                    originalTotal += originalAmount;
                    results[row.ID] = total;
                    if (debugBonus && sourceMapping.Count > 1)
                    {
                        Console.WriteLine($"  Total: {roundBonusSoul(total)}");
                    }
                }
                // Even when not scaling, we lose 30k from the total (7534100), and when scaling it can be 100k to 200k.
                // So do one big correction step to at least match the vanilla total.
                int replaceTotal = results.Values.Sum();
                if (debugBonus)
                {
                    // Total amount: 7534100 -> 5615300, added -1918800
                    int totalParam = bossSouls.Values.Sum();
                    Console.WriteLine($"Total amount: ({totalParam} param) {originalTotal} -> {replaceTotal}, added {replaceTotal - originalTotal}");
                }
                // Merged mods (or added DLC) may be weird here, so check basic ranges.
                if (originalTotal > replaceTotal
                    && originalTotal > 7_000_000 && originalTotal < 10_000_000
                    && replaceTotal > 3_000_000 && replaceTotal < 7_200_000)
                {
                    double mult = (double)originalTotal / replaceTotal;
                    // If scaling is used, try to make up the difference in cases where it's less than the original.
                    // This is probably actually due to a bias around major bosses, but those will be noticed more regardless.
                    // Don't enable this quite yet as it might be a bit too extreme
                    if (opt["limitbonus"] && opt["scale"] && !opt["fog"])
                    {
                        int replaceDown = 0;
                        HashSet<int> downs = new HashSet<int>();
                        foreach ((int id, int total) in results)
                        {
                            int original = bossSouls[(uint)id];
                            if (original > total)
                            {
                                downs.Add(id);
                                replaceDown += total;
                            }
                        }
                        double limitMult = (double)(originalTotal - replaceTotal + replaceDown) / replaceDown;
                        results = results.ToDictionary(e => e.Key, e => downs.Contains(e.Key) ? (int)(e.Value * limitMult) : e.Value);
                        if (debugBonus) Console.WriteLine($"Simple multiplier {mult * 100}%, limit mult {limitMult * 100}%, new total {results.Values.Sum()}");
                    }
                    else
                    {
                        results = results.ToDictionary(e => e.Key, e => (int)(e.Value * mult));
                        if (debugBonus) Console.WriteLine($"Simple multiplier {mult * 100}%, new total {results.Values.Sum()}");
                    }
                }
                foreach (PARAM.Row row in Params["GameAreaParam"].Rows)
                {
                    if (!results.TryGetValue(row.ID, out int total)) continue;
                    uint val = (uint)roundBonusSoul(total);
                    row["bonusSoul_single"].Value = val;
                    row["bonusSoul_multi"].Value = val;
                }
            }

            // Dupe edits which should possibly go in FinalNpcPass
            if (game.EldenRing)
            {
                // Runes for Leda fight are not handled with this custom speffect, not GameAreaParam. Use her dupe count for this
                int ledaDupe = dupeCount(20010720);
                if (ledaDupe > 0)
                {
                    PARAM.Row row = Params["SpEffectParam"][20004820] ?? throw new Exception("SpEffectParam 20004820 not found for Leda fight");
                    int soul = (int)row["soul"].Value;
                    row["soul"].Value = soul * (ledaDupe + 1);
                }

                // Dupe speffects for holder. (Do this when initializing the speffects?)
                PARAM.Row baseSp = game.Params["SpEffectParam"][5020];
                for (int i = 0; i < Math.Max(10, maxDupeCount + 1) * 2; i++)
                {
                    GameEditor.CopyRow(baseSp, game.AddRow("SpEffectParam", dupeSpEffectBase + i));
                }
            }
#endif

            // --
            // -- AI script editing system, needed for DS1 and DS3 currently
            // --

            if (eventConfig.EnemyScripts != null)
            {
                foreach (ScriptSpec spec in eventConfig.EnemyScripts)
                {
                    if (!game.AI.TryGetValue(spec.Name, out string contents))
                    {
                        throw new Exception($"Internal error: Missing AI script {spec.Name}");
                    }
                    if (spec.Replaces == null || spec.Replaces.Count == 0) continue;
                    // TODO optimize this by making a mega-regex, but needs capture groups. Err on the side of safety for now.
                    foreach (ScriptReplaceCommand cmd in spec.Replaces)
                    {
                        Regex findRegex = new Regex(cmd.Regex ? cmd.From : Regex.Escape(cmd.From));
                        MatchCollection matches = findRegex.Matches(contents);
                        if (matches.Count == 0) throw new Exception($"Error editing AI script {spec.Name}: could not find {findRegex}");
                        contents = findRegex.Replace(contents, cmd.To);
                    }
                    game.AI[spec.Name] = contents;
                }
            }

#if DEBUG
            Console.WriteLine($"entity max {entityBase} event max {tmpBase} write max {writeBase} any {perm.AnyRandomized}");
#endif

            // Return item info
            Result itemLocs = new Result();

            foreach (KeyValuePair<uint, List<uint>> entry in mapping.Mapping)
            {
                string sourceName = infos[entry.Key].ItemName;
                foreach (uint target in entry.Value)
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

            if (game.EldenRing && mapping.GetTargets(11050850, out List<uint> gideonTarget) && gideonTarget.Count == 1)
            {
                itemLocs.Outfit = infos[gideonTarget[0]].Outfit;
                if (opt["testoutfit"])
                {
                    itemLocs.Outfit = string.Join("|", infos.Values.Where(i => i.DLC && i.Outfit != null).DistinctBy(i => i.Outfit).OrderBy(i => i.ID).Select(i => i.Outfit));
                }
            }

            return itemLocs;
        }

        public class Result
        {
            // Mapping from named enemy to other named enemies where they end up, for item placement
            public Dictionary<string, List<string>> Target = new Dictionary<string, List<string>>();

            // Boss outfit for characters to wear
            public string Outfit { get; set; }
        }

        private static readonly Regex condCmdRe = new Regex(@"^c\d_");

        public class BossPhaseLimit
        {
            public uint Entity { get; set; }
            public float Amount { get; set; }
        }

        public class RegionTarget
        {
            public int Distance { get; set; }
            public uint Region { get; set; }
            public uint Generator { get; set; }
            public static RegionTarget ID(uint id) => new RegionTarget { Region = id };
            public static RegionTarget Gen(uint id) => new RegionTarget { Generator = id };
            public static RegionTarget Dist(int id) => new RegionTarget { Distance = id };
        }
        
        // TODO: Add some state across multiple silos to improve enemy uniqueness. Making it ridiculously complicated
        public class RandomSources
        {
            public static RandomSources Create(Random random, List<PoolAssignment> pools, List<uint> defaultPool, int estimatedTargetCount)
            {
                if (pools == null || pools.Count == 0) return null;
                RandomSources ret = new RandomSources();
                foreach (PoolAssignment original in pools)
                {
                    PoolAssignment pool = original.Copy();
                    if (pool.Weight <= 0) continue;
                    if (pool.Pool == null || pool.Pool.ToLowerInvariant() == "default")
                    {
                        pool.PoolGroups = new List<List<uint>> { defaultPool.ToList() };
                    }
                    else if (pool.Norandom)
                    {
                        // A magic number here
                        pool.PoolGroups = new List<List<uint>> { new List<uint> { 0 } };
                    }
                    else if (pool.DefaultCount > 0)
                    {
                        if (pool.PoolGroups == null)
                        {
                            pool.PoolGroups = new List<List<uint>>();
                        }
                        for (int i = 0; i < pool.DefaultCount; i++)
                        {
                            pool.PoolGroups.Add(defaultPool.ToList());
                        }
                    }
                    if (pool.PoolGroups == null || pool.PoolGroups.All(p => p.Count == 0))
                    {
                        Console.WriteLine($"Ignoring empty source group of size {pool.PoolGroups?.Count ?? -1}");
                        continue;
                    }
                    if (!pool.RandomByType && pool.PoolGroups.Count > 1)
                    {
                        pool.PoolGroups = new List<List<uint>> { pool.PoolGroups.SelectMany(g => g).ToList() };
                    }
                    Shuffle(random, pool.PoolGroups);
                    foreach (List<uint> group in pool.PoolGroups)
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

            // Used for filtering in advance of creating sources, matching how pools are processed above
            public static bool IsPoolEmpty(PoolAssignment pool) => pool.DefaultCount == 0 && !pool.PoolGroups.Any(p => p.Any()) && !pool.Norandom;

            const int maxTries = 20;

            public uint Next(Dictionary<uint, bool> singletons, uint target)
            {
                int pool = PoolOrder.Count > 1 ? PoolOrder[PoolIndex] : 0;
                uint ret = 0;
                uint trueRet = 0;
                int tries;
                for (tries = 0; tries < maxTries; tries++)
                {
                    int group = GroupIndices[pool];
                    int chosen = Indices[pool][group];
                    ret = Pools[pool].PoolGroups[group][chosen];
                    trueRet = ret <= 0 ? target : ret;

                    Indices[pool][group] = (Indices[pool][group] + 1) % Pools[pool].PoolGroups[group].Count;
                    GroupIndices[pool] = (GroupIndices[pool] + 1) % Pools[pool].PoolGroups.Count;

                    if (singletons.TryGetValue(trueRet, out bool taken))
                    {
                        if (taken)
                        {
                            continue;
                        }
                        else
                        {
                            singletons[trueRet] = true;
                        }
                    }
                    break;
                }
                if (tries == maxTries) throw new Exception($"Can't select enemy: keep getting enemies like {trueRet} which can only appear once in the game");

                if (PoolOrder.Count > 1) PoolIndex = (PoolIndex + 1) % PoolOrder.Count;
                return ret;
            }

            public static uint Next(Dictionary<uint, bool> singletons, List<uint> sources, ref int sourceIndex)
            {
                int tries = 0;
                uint trueRet;
                do
                {
                    trueRet = sources[(sourceIndex++) % sources.Count];
                    if (singletons.TryGetValue(trueRet, out bool taken))
                    {
                        if (taken)
                        {
                            continue;
                        }
                        else
                        {
                            singletons[trueRet] = true;
                        }
                    }
                    return trueRet;
                }
                while (tries++ < maxTries);
                throw new Exception($"Can't select enemy: keep getting enemies like {trueRet} which can only appear once in the game");
            }

            public List<PoolAssignment> Pools = new List<PoolAssignment>();
            public List<int> PoolOrder = new List<int>();
            public int PoolIndex = 0;
            public List<int> GroupIndices = new List<int>();
            public List<List<int>> Indices = new List<List<int>>();
            public int Total { get; set; }
        }
    }
}
