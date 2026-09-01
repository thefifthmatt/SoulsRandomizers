using Org.BouncyCastle.Asn1.X509;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class EldenEnemyEditor : EnemyEditor<MSBE, MSBE.Part.Enemy, MSBE.Part.Asset, MSBE.Region, MSBE.Event.Generator>
    {
        public override List<MSBE.Part.Enemy> GetEnemies(MSBE msb) => msb.Parts.Enemies;
        public override void SetEntityID(MSBE.Part.Enemy e, uint id) => e.EntityID = (uint)id;
        // int == uint checks return true for the same value
        public override MSBE.Part.Enemy GetEnemy(MSBE msb, uint id) => msb.Parts.Enemies.Find(e => e.EntityID == id);
        public override MSBE.Region GetRegion(MSBE msb, uint id) => msb.Regions.GetEntries().Find(e => e.EntityID == id);

        public override EnemyData GetEnemyData(MSBE.Part.Enemy e, string map)
        {
            return new EnemyData
            {
                MainMap = map,
                Name = e.Name,
                ID = e.EntityID,
                Group = e.EntityGroupIDs.Where(g => g > 0).ToList(),
                Model = e.ModelName,
                NPC = e.NPCParamID,
                Think = e.ThinkParamID,
                Char = e.CharaInitID,
                Col = e.CollisionPartName,
                Anim = e.BackupEventAnimID,
                Pos = e.Position,
            };
        }

        public override ModelEditor GetModelEditor(MSBE msb)
        {
            return new ModelEditor
            {
                EnemyDecl = new HashSet<string>(msb.Models.Enemies.Select(model => model.Name)),
                EnemyUsed = new HashSet<string>(msb.Parts.GetEntries()
                    .Where(e => e is MSBE.Part.DummyEnemy || e is MSBE.Part.Enemy || e is MSBE.Part.Player)
                    .Select(e => e.ModelName)),
                ObjectDecl = new HashSet<string>(msb.Models.Assets.Select(model => model.Name)),
                RegisterEnemy = name =>
                {
                    msb.Models.Enemies.Add(new MSBE.Model.Enemy
                    {
                        Name = name,
                        SibPath = $@"N:\GR\data\Model\chr\{name}\sib\{name}.sib",
                    });
                },
                RegisterObject = name =>
                {
                    msb.Models.Assets.Add(new MSBE.Model.Asset
                    {
                        Name = name,
                        SibPath = $@"N:\GR\data\Asset\Environment\geometry\{name.Substring(0, 6)}\{name}\sib\{name}.sib",
                    });
                },
            };
        }

        public override MSBE.Part.Enemy TransplantEnemy(MSBE msb, EnemyData source, uint target)
        {
            MSBE.Part.Enemy e = msb.Parts.Enemies.Find(en => en.EntityID == target);
            e.ModelName = source.Model;
            e.NPCParamID = source.NPC;
            e.ThinkParamID = source.Think;
            e.CharaInitID = source.Char;
            // This could be source.Anim, but unfortunately some enemies are underground.
            // e.g. Small Oracle Envoy (#11000204) in Leyndell <- Grave Skeleton
            // So filter this out beforehand if undesired
            e.BackupEventAnimID = -1;
            // Used by Bell Bearing Hunters to hide themselves
            e.UnkT15 = false;
            // Enemies can have speffects (UnkT40 etc), which seems mostly fun to keep. (109000 on snail not cosmetic though)
            // Exceptions handled in EnemyRandomizer
            return e;
        }

        public override void SetNpcParam(MSBE.Part.Enemy e, int? npcParam = null, int? npcThinkParam = null)
        {
            if (npcParam is int npc) e.NPCParamID = npc;
            if (npcThinkParam is int think) e.ThinkParamID = think;
        }

        public override MSBE.Part.Enemy CloneEnemy(MSBE msb, MSBE.Part.Enemy sourcePart, EnemyData source, uint newTarget, int newPartId)
        {
            MSBE.Part.Enemy e2 = (MSBE.Part.Enemy)sourcePart.DeepCopy();
            e2.ModelName = source.Model;
            e2.NPCParamID = source.NPC;
            e2.ThinkParamID = source.Think;
            e2.CharaInitID = source.Char;
            e2.EntityID = (uint)newTarget;
            string prefix = "";
            if (sourcePart.Name.StartsWith("m") && sourcePart.Name.Contains("-"))
            {
                prefix = $"{sourcePart.Name.Split('-')[0]}-";
            }
            e2.Name = $"{prefix}{e2.ModelName}_{newPartId:d4}";
            // Needed to avoid sudden deaths, evidently. TODO see where this mismatches in vanilla
            e2.Unk08 = newPartId;
            // Transplanting talks seems to break most talk scripts
            e2.TalkID = 0;
            msb.Parts.Enemies.Add(e2);
            // Hardcode some basic events here, for the sake of caravan duplication. There are caravan trolls
            // I guess also night's cav for snowfield
            if (sourcePart.ModelName == "c4600" || sourcePart.ModelName == "c4602" || sourcePart.ModelName == "c3150")
            {
                foreach (MSBE.Event.PlatoonInfo info in msb.Events.PlatoonInfo.Where(p => p.GroupPartsNames.Contains(sourcePart.Name)))
                {
                    int blankIndex = Array.IndexOf(info.GroupPartsNames, null);
                    if (blankIndex >= 0)
                    {
                        info.GroupPartsNames[blankIndex] = e2.Name;
                    }
                }
            }
            return e2;
        }

        public override void CopyAssociations(
            MSBE fromMsb, EnemyData ownerSource, EnemyData helperSource,
            MSBE toMsb, MSBE.Part.Enemy owner, MSBE.Part.Enemy helper)
        {
            // Only Elden Ring for now
            MSBE.Event.Mount mount = fromMsb.Events.Mounts
                .Find(e => e.RiderPartName == ownerSource.Name && e.MountPartName == helperSource.Name);
            if (mount != null)
            {
                // Even if the two maps are the same, this event is called when the helper is removed
                toMsb.Events.Mounts.Add(new MSBE.Event.Mount
                {
                    Name = $"{owner.Name} on {helper.Name}",
                    RiderPartName = owner.Name,
                    MountPartName = helper.Name,
                });
            }
            // Find all platoons led by this enemy, and see which copies exist using EnemyRandomizer naming convention
            // Use static count in case toMsb == fromMsb
            int platoonCount = fromMsb.Events.PlatoonInfo.Count;
            for (int i = 0; i < platoonCount; i++)
            {
                MSBE.Event.PlatoonInfo sourcePlatoon = fromMsb.Events.PlatoonInfo[i];
                if (sourcePlatoon.GroupPartsNames[0] != ownerSource.Name) continue;
                string destName = $"{owner.Name}_{sourcePlatoon.Name}";
                int helperIndex = Array.IndexOf(sourcePlatoon.GroupPartsNames, helperSource.Name);
                if (helperIndex == -1) continue;
                // This is a platoon which contains both entities
                MSBE.Event.PlatoonInfo targetPlatoon = toMsb.Events.PlatoonInfo.Find(p => p.Name == destName);
                if (targetPlatoon == null)
                {
                    targetPlatoon = (MSBE.Event.PlatoonInfo)sourcePlatoon.DeepCopy();
                    targetPlatoon.Name = destName;
                    // 31053205 and 31003209 have entity ids, so just in case
                    targetPlatoon.EntityID = 0;
                    Array.Clear(targetPlatoon.GroupPartsNames);
                    targetPlatoon.GroupPartsNames[0] = owner.Name;
                    toMsb.Events.PlatoonInfo.Add(targetPlatoon);
                    // Console.WriteLine($"Making platoon {destName} from {ownerSource.MainMap}");
                }
                targetPlatoon.GroupPartsNames[helperIndex] = helper.Name;
                // Console.WriteLine($"  Adding {helperIndex}={helper.Name}");
            }
        }

        public override void SetEnemyCollision(MSBE.Part.Enemy enemy, string colName)
        {
            enemy.CollisionPartName = colName;
        }

        public override void ClearGroups(MSBE.Part.Enemy e, ICollection<uint> preserveGroups = null)
        {
            for (int i = 0; i < e.EntityGroupIDs.Length; i++)
            {
                if (preserveGroups == null || !preserveGroups.Contains(e.EntityGroupIDs[i]))
                {
                    e.EntityGroupIDs[i] = 0;
                }
            }
        }

        public override void CopyGroups(EnemyData source, Dictionary<uint, uint> groupMapping, MSBE.Part.Enemy target, uint removeTarget = 0)
        {
            CopyGroupsInternal(source, groupMapping, target.EntityGroupIDs, removeTarget);
        }

        public override void RemoveEnemies(MSBE msb, Predicate<uint> removePredicate)
        {
            msb.Parts.Enemies = msb.Parts.Enemies.Where(p => !removePredicate(p.EntityID)).ToList();
        }

        public override (MSBE.Part.Asset, uint, uint) MakeCrossMapObjectCopy(
            Func<uint> newEntity, string map, string toMap, string objId, EnemyData targetData, int newPartId)
        {
            throw new NotImplementedException();
        }

        public override void GarbageCollect(MSBE msb, HashSet<string> usedEnemies = null, HashSet<string> removedObjects = null)
        {
            // HashSet<string> dummiedEntities = usedEnemies;
            // Don't do removal of events, as this probably breaks some things. Instead, dummy things out
            HashSet<string> names = new HashSet<string>(msb.Parts.Enemies.Select(e => e.Name));
            void dummyParts(string[] parts)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] != null && !names.Contains(parts[i]))
                    {
                        parts[i] = null;
                    }
                }
            }
            // msb.Events.PlatoonInfo = msb.Events.PlatoonInfo.Where(t => t.GroupPartNames.All(n => n == null || names.Contains(n))).ToList();
            // msb.Events.Generators = msb.Events.Generators.Where(t => t.SpawnPartNames.All(n => n == null || names.Contains(n))).ToList();
            // msb.Events.Mounts = msb.Events.Mounts.Where(t => names.Contains(t.MountPartName) && names.Contains(t.RiderPartName)).ToList();
            foreach (MSBE.Event.PlatoonInfo t in msb.Events.PlatoonInfo)
            {
                dummyParts(t.GroupPartsNames);
            }
            foreach (MSBE.Event.Generator t in msb.Events.Generators)
            {
                dummyParts(t.SpawnPartNames);
            }
            foreach (MSBE.Event.Mount t in msb.Events.Mounts)
            {
                if (names.Contains(t.MountPartName) && names.Contains(t.RiderPartName))
                {
                    if (t.MountPartName.StartsWith("c8100")
                        || t.MountPartName.StartsWith("c8101")
                        || t.MountPartName.StartsWith("c8110"))
                    {
                        // This is probably fine? Helpers are deleted only in second pass.
                        continue;
                    }
                    else
                    {
                        continue;
                    }
                }
                t.MountPartName = t.RiderPartName = null;
            }
            foreach (MSBE.Region.GroupDefeatReward t in msb.Regions.GroupDefeatRewards)
            {
                dummyParts(t.PartNames);
            }
            if (removedObjects != null && removedObjects.Count > 0)
            {
                msb.Events.Treasures.RemoveAll(t => removedObjects.Contains(t.TreasurePartName));
                msb.Events.ObjActs.RemoveAll(t => removedObjects.Contains(t.ObjActPartName));
            }
        }

        public override GeneratorData GetCrossMapGeneratorData()
        {
            Dictionary<uint, (MSBE.Event.Generator, List<uint>)> generators = new();
            foreach (KeyValuePair<string, MSBE> entry in Game.EldenMaps)
            {
                MSBE msb = entry.Value;
                if (msb.Events.Generators.Count == 0) continue;
                Dictionary<string, uint> mapEnemies = msb.Parts.Enemies.Where(e => e.EntityID > 0).ToDictionary(e => e.Name, e => e.EntityID);
                foreach (MSBE.Event.Generator gen in msb.Events.Generators)
                {
                    List<uint> entities = gen.SpawnPartNames
                        .Select(n => n != null && mapEnemies.TryGetValue(n, out uint id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();
                    generators[gen.EntityID] = (gen, entities);
                }
            }
            return new GeneratorData { Data = generators };
        }

        public override (MSBE.Region, List<MSBE.Region>, uint) MakeCrossMapRegionCopy(
            Func<uint> newEntity, string map, string toMap, uint id, bool replace)
        {
            List<MSBE.Region> regions = Game.EldenMaps[map].Regions.GetEntries();
            MSBE.Region a = regions.Find(r => r.EntityID == id);
            if (a == null) throw new Exception($"Internal error: can't find region {id} in {map}");
            List<MSBE.Region> ret = new List<MSBE.Region>();
            MSBE.Region b = a.DeepCopy();
            MSBE msb = Game.EldenMaps[toMap];
            msb.Regions.Add(b);
            ret.Add(b);
            // Hopefully this isn't used much
            b.ActivationPartName = null;
            if (replace)
            {
                b.EntityID = a.EntityID;
                a.EntityID = 0;
            }
            else
            {
                b.EntityID = (uint)newEntity();
            }
            b.Name = $"Region {b.EntityID} from {id}";
            if (b.Shape is MSB.Shape.Composite comp)
            {
                for (int i = 0; i < comp.Children.Length; i++)
                {
                    string childName = comp.Children[i].RegionName;
                    if (childName == null) continue;
                    MSBE.Region c = regions.Find(r => r.Name == childName);
                    if (c == null) throw new Exception($"Internal error: can't find {id} child region {childName} in {map}");
                    MSBE.Region d = c.DeepCopy();
                    msb.Regions.Add(d);
                    ret.Add(d);
                    d.ActivationPartName = null;
                    d.EntityID = 0;
                    d.Name = $"Region {b.EntityID} Child {i} from {id}";
                    comp.Children[i].RegionName = d.Name;
                }
            }
            return (a, ret, b.EntityID);
        }

        public override (List<MSBE.Region>, uint) MakeCrossMapGeneratorCopy(
            Func<uint> newEntity, string map, string toMap, uint id,
            GeneratorData generators, RelocMap reloc)
        {
            MSBE fromMsb = Game.EldenMaps[map];
            MSBE msb = Game.EldenMaps[toMap];
            if (!generators.Data.ContainsKey(id)) throw new Exception($"Did not find generator {id}");
            (MSBE.Event.Generator gen, List<uint> enemyIds) = generators.Data[id];
            if (enemyIds.Count == 0) throw new Exception($"Generator {id} has no enemies associated with it");

            MSBE.Event.Generator gen2 = (MSBE.Event.Generator)gen.DeepCopy();
            Array.Clear(gen2.SpawnPartNames, 0, gen2.SpawnPartNames.Length);
            Array.Clear(gen2.SpawnRegionNames, 0, gen2.SpawnRegionNames.Length);
            gen2.PartName = gen2.RegionName = null;

            int enemyIndex = 0;
            foreach (uint fromEnemy in enemyIds)
            {
                if (!reloc.TryGetValue(EventValue.Enemy(fromEnemy), out EventValue target))
                {
                    // This is fine in some cases, like with mixed-count dupes.
                    // This generally shouldn't happen with bosses, however.
                    // throw new Exception($"No target found for {fromEnemy} from generator {id}");
                    continue;
                }
                int targetId = target.IntID;
                MSBE.Part.Enemy enemy = msb.Parts.Enemies.Find(e => e.EntityID == targetId);
                gen2.SpawnPartNames[enemyIndex++] = enemy.Name;
            }
            // When does this happen?
            if (enemyIndex == 0) return (null, 0);

            uint genId = newEntity();
            gen2.EntityID = genId;
            gen2.Name = $"Generator {genId} {id}";
            msb.Events.Generators.Add(gen2);

            List<MSBE.Region> regions = new List<MSBE.Region>();
            for (int i = 0; i < gen.SpawnRegionNames.Length; i++)
            {
                string regionName = gen.SpawnRegionNames[i];
                if (regionName == null)
                {
                    regions.Add(null);
                    continue;
                }
                MSBE.Region a = fromMsb.Regions.GetEntries().Find(r => r.Name == regionName);
                MSBE.Region b = a.DeepCopy();
                b.EntityID = 0;
                b.Name = $"Generator Point #{i} {genId} {id}";
                gen2.SpawnRegionNames[i] = b.Name;
                regions.Add(b);
                msb.Regions.Add(b);
            }
            return (regions, genId);
        }

        public override MSBE.Region MakeArenaRegion(MSBE msb, Arena arena, string name)
        {
            MSB.Shape.Box box = new MSB.Shape.Box();
            box.Width = arena.Box.X;
            box.Height = arena.Box.Y;
            box.Depth = arena.Box.Z;
            MSBE.Region.Other r = new MSBE.Region.Other();
            r.Shape = box;
            r.Position = arena.Pos;
            r.Rotation = arena.Rot;
            r.Name = name;
            msb.Regions.Add(r);
            return r;
        }

        public override void InitialMapPass(RandomizerOptions opt)
        {
            GameData game = Game;

            // Makes night's cav spawn at day or night. For the original location, it requires also setting position and removing speffect
            Dictionary<int, int> nightsCavThink = new()
            {
                [31500052] = 31500000,
                [31501052] = 31501000,
            };
            bool fixSnowfieldNightsCav(IMsbPart part)
            {
                if (part is not MSBE.Part.Enemy e) return false;
                // Glaive, Normal:    31500000: logicId=315000, battleGoalID=315000, backToHomeStuckAct=1
                // Glaive, Snowfield: 31500052: logicId=315010, battleGoalID=315000, backToHomeStuckAct=0
                // Flail, Normal:     31501000: logicId=315000, battleGoalID=315000, backToHomeStuckAct=1
                // Flail, Snowfield:  31501052: logicId=315010, battleGoalID=315000, backToHomeStuckAct=0
                // The logic script has some additional platoon AI, and also a time check based on speffect 11830
                // Different think, no speffect: boss always shows up <--
                // Different think, with speffect: boss never shows up
                // Same think, with or without speffect: boss appears at night
                if (nightsCavThink.TryGetValue(e.ThinkParamID, out int altThink))
                {
                    if (altThink == -1)
                    {
                        return false;
                    }
                    else
                    {
                        e.ThinkParamID = altThink;
                        // Console.WriteLine($"Set {e.EntityID} -> {e.ThinkParamID}");
                        return true;
                    }
                }
                PARAM.Row row = game.Params["NpcThinkParam"][e.ThinkParamID];
                if (row != null && (int)row["battleGoalID"].Value == 315000 && (int)row["logicId"].Value == 315010)
                {
                    // Not the original think, can be modified in place
                    row["logicId"].Value = 315000;
                    row["backToHomeStuckAct"].Value = (byte)1;
                    nightsCavThink[e.ThinkParamID] = e.ThinkParamID;
                    // Console.WriteLine($"Set {e.EntityID} -> {e.ThinkParamID} (alt)");
                    return true;
                }
                nightsCavThink[e.ThinkParamID] = -1;
                return false;
            }
            var getMap = game.EldenMaps.TryGetValue;
            // Bad hack to separate out main group for Rykard and a new buddy group, since we want to keep only the latter
            if (getMap("m16_00_00_00", out MSBE volcano))
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
            // Ymir generator has no id
            if (getMap("m61_51_45_00", out MSBE ymirMsb))
            {
                MSBE.Event.Generator gen = ymirMsb.Events.Generators.FirstOrDefault();
                if (gen != null)
                {
                    gen.EntityID = 2051453355;
                }
            }
            if (getMap("m61_50_48_00", out MSBE scadu))
            {
                MSBE.Part.Enemy flower = scadu.Parts.Enemies.Find(e => e.EntityID == 2050480800);
                if (flower != null)
                {
                    flower.Position = new Vector3(-113.847f, 190.017f, 132.324f);
                }
                flower = scadu.Parts.Enemies.Find(e => e.EntityID == 2050480801);
                if (flower != null)
                {
                    flower.Position = new Vector3(-108.142f, 190.017f, 158.620f);
                }
                // Needed even when not randomized
                game.WriteMSBs.Add("m61_50_48_00");
            }
            // TODO: Put in arena config
            if (getMap("m22_00_00_00", out MSBE fissure))
            {
                MSBE.Part.Enemy knight = fissure.Parts.Enemies.Find(e => e.EntityID == 22000800);
                if (knight != null)
                {
                    if (opt["fog"])
                    {
                        // Fog rando move condition is Y < -250 && X < -125
                        knight.Position = new Vector3(-118.098f, -294.269f, 65.881f);
                    }
                    else
                    {
                        // Not good for fog rando because the initial animation has a massive hitbox
                        knight.Position = new Vector3(-130.289f, -294.070f, 66.288f);
                    }
                }
            }
            if (getMap("m30_01_00_00", out MSBE impaler))
            {
                // Nerf generators
                HashSet<uint> impalerGenerators = new() { 30013318, 30013303, 30013306, 30013307, 30013308, 30013309, 30013310, 30013311, 30013312, 30013317, 30013319 };
                foreach (MSBE.Event.Generator gen in impaler.Events.Generators)
                {
                    if (impalerGenerators.Contains(gen.EntityID) && gen.MinInterval < 10)
                    {
                        gen.MinInterval = 20;
                        gen.MaxInterval = 30;
                    }
                }
            }
            if (getMap("m60_12_13_02", out MSBE snowfield))
            {
                // Critically - original enemies (blue) are broken with this and random onslaught, red are fine
                HashSet<string> unplatoon = new();
                foreach (MSBE.Part.Enemy e in snowfield.Parts.Enemies)
                {
                    if (e.EntityID / 100 != 12485508) continue;
                    unplatoon.Add(e.Name);
                    // Turn it into a regular night enemy, even if it means you can do one half without the other
                    e.ChrActivateCondParamID = 101000;
                    // Remove speffect 11830 which is used by emevd and AI to hide it
                    if (e.SpEffectSetParamID[0] == 31501000)
                    {
                        e.SpEffectSetParamID[0] = 0;
                    }
                    // This is just for the cavalry themselves
                    fixSnowfieldNightsCav(e);
                    if (e.EntityID == 1248550800 || e.EntityID == 1248550810)
                    {
                        e.Position = new Vector3(-273.800f, 1282.004f, 351.250f);
                        e.Rotation = new Vector3(0, 142.126f, 0);
                    }
                    else if (e.EntityID == 1248550801 || e.EntityID == 1248550811)
                    {
                        e.Position = new Vector3(-262.330f, 1282.637f, 364.850f);
                        e.Rotation = new Vector3(0, 142.126f, 0);
                    }
                }
                foreach (MSBE.Event.PlatoonInfo platoon in snowfield.Events.PlatoonInfo)
                {
                    // New platoon entries cause issues, so just remove all
                    for (int i = 0; i < platoon.GroupPartsNames.Length; i++)
                    {
                        if (unplatoon.Contains(platoon.GroupPartsNames[i]))
                        {
                            platoon.GroupPartsNames[i] = null;
                        }
                    }
                }
                // Also needed when not randomized
                game.WriteMSBs.Add("m60_12_13_02");
            }
        }

        public override NpcEdits InitialNpcPass(EnemyState state, RandomizerOptions opt)
        {
            GameData game = Game;
            ParamDictionary Params = game.Params;
            EnemyPermutation perm = state.Permutation;
            NpcEdits npcs = new(game, state);
            Enemies enemies = state.Enemies;
            Dictionary<uint, EnemyData> defaultData = enemies.DefaultData;
            Dictionary<uint, EnemyInfo> infos = enemies.Infos;
            EnemyPreset preset = state.Preset;

            // These edits apply to a very small portion of NPCs, so go through infos instead
            // Reduces by 0.2, 0.35, 0.03, 0.25, 0.5
            // Black Knights, Soldier/Perfumer, Ghostflame Dragon, Drake Boss, Other Drake
            HashSet<int> reduceDamageSps = new() { 20011664, 20011665, 20011666, 20012710, 20012711 };
            foreach (EnemyInfo info in infos.Values)
            {
                if (info.HasTag("fight"))
                {
                    if (!npcs.TryGetNpc(defaultData[info.ID].NPC, out PARAM.Row row)) continue;
                    for (int i = 0; i <= 31; i++)
                    {
                        // In various positions from 16-20
                        int sp = (int)row[$"spEffectID{i}"].Value;
                        if (reduceDamageSps.Contains(sp))
                        {
                            row[$"spEffectID{i}"].Value = -1;
                            AddMulti(npcs.NpcCopySpEffect, row.ID, sp);
                            // if (info.ID == 2049430800) Console.WriteLine($"Got speffect for dragon: {info.ID} -> {row.ID} -> {sp}");
                        }
                    }
                    // This could be a separate tag, but allow regular enemies to drop runes
                    if (info.Class == EnemyClass.Basic)
                    {
                        row["isSoulGetByBoss"].Value = (byte)0;
                    }
                }
                if (info.HasTag("wyvern"))
                {
                    if (!npcs.TryGetNpc(defaultData[info.ID].NPC, out PARAM.Row row)) continue;
                    int modelId = row.ID / 10000;
                    // Ghostflame Dragon 5860 has special soundtrack (22), but the Scadu Altus one uses regular Wyvern theme.
                    // Ancient Dragons are 28 as well
                    row["threatLv"].Value = (uint)25;
                }
                // TODO: Detect if enemies should have threatLv but don't
                if (info.HasTag("threat"))
                {
                    if (!npcs.TryGetNpc(defaultData[info.ID].NPC, out PARAM.Row row)) continue;
                    row["threatLv"].Value = (uint)1;
                }
            }

            // Poison and Rot. Lava added below
            npcs.ResistSps.AddRange(new[] { 90000, 90010 });
            List<EnemyClass> teamClasses = new List<EnemyClass>
            {
                EnemyClass.Basic, EnemyClass.Wildlife, EnemyClass.HostileNPC,
                EnemyClass.Miniboss, EnemyClass.NightMiniboss, EnemyClass.DragonMiniboss, EnemyClass.Evergaol,
            };
            if (enemies.AnyDupeEnabled)
            {
                // Be a bit more invasive if dupes present
                teamClasses.AddRange(new[] { EnemyClass.Boss, EnemyClass.MinorBoss, EnemyClass.CaravanTroll, EnemyClass.DupeOnly });
            }
            // TODO: Surely this is slower than just indexing NpcParam
            // Scadutree Avatar helpers need to transfer team as well
            HashSet<int> transferTeams = new HashSet<int>(
                infos.Values.Where(i => teamClasses.Contains(i.Class) || (i.Class == EnemyClass.Helper && (i.HasTag("mainhelper") || i.HasTag("teamhelper")))).Select(i => defaultData[i.ID].NPC));
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
                    npcs.NpcOriginalTeam[row.ID] = teamType;
                    row["teamType"].Value = (byte)6;
                }
                if (noDeadNpcs.Contains(row.ID))
                {
                    // if ((byte)row[noDead].Value == 0) Console.WriteLine($"making nodead: {infos.Values.Where(i => defaultData[i.ID].NPC == row.ID).Select(i => i.DebugText).FirstOrDefault()}");
                    // Make more enemies feasible for phase 1s, e.g. Godskin Duo
                    row["disableInitializeDead"].Value = (byte)1;
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
                    if (npcs.ResistSps.Contains(sp))
                    {
                        AddMulti(npcs.NpcCopySpEffect, row.ID, sp);
                    }
                }

                // Hack for lava dwellers, use debug (?) speffect
                // 99100: fireDamageCutRate = 0, effectTargetOpposeTarget = 1, effectTargetFriendlyTarget = 1
                if ((float)row["fireDamageCutRate"].Value < 0.01)
                {
                    AddMulti(npcs.NpcCopySpEffect, row.ID, 99100);
                }

                // This seems like an okay radius/height for bosses
                // 1f, 2.1f prevents slowdowns...
                int model = row.ID / 10000;
                if (!(opt["dlc"] && game.IsEldenDlcModel(model)))
                {
                    // Scadutree Avatars you fight
                    // if (row.ID == 52300396 || row.ID == 52300296 || row.ID == 52300096) continue;
                    // Healthbar Scadutree Avatars. Prevents crashing sound in vanilla arena, hopefully it's fine
                    if (row.ID == 52300596 || row.ID == 52300496 || row.ID == 52300196) continue;
                    npcs.ShrinkNpc(row, 2.5f, 5.1f);
                }
                // Similar to addSetEffect below, but do it for all Aging Untouchables in range
                // Could check behavior fields, not just id range, but it should be fine.
                if (opt["nerflantern"] && model == 5280)
                {
                    if (!game.AddNpcSpEffect(row, 20011471))
                    {
                        throw new Exception($"Not enough speffect slots in Aging Untouchable to remove parry-to-damage");
                    }
                }

                if (perm.AnyRandomized)
                {
                    row["dlcGameClearSpEffectID"].Value = -1;
                }
            }
            // Lava
            npcs.ResistSps.Add(99100);

            // This is needed for dupe. However, doing it across the board breaks Jori! Unacceptable.
            if (enemies.AnyDupeEnabled)
            {
                HashSet<int> noFriendlyFire = new HashSet<int>(
                    infos.Values.Where(i => i.HasTag("friendly")).Select(i => defaultData[i.ID].NPC / 10000));
                foreach (PARAM.Row row in Params["AtkParam_Npc"].Rows)
                {
                    int model = row.ID / 1000;
                    if (noFriendlyFire.Contains(model))
                    {
                        row["friendlyTarget"].Value = (byte)0;
                    }
                }
            }

            // Swap around >1 threatLvs if randomizing boss BGMs, and record it anyway in the case of cross-category boss placements.
            // At present, having infinite nose range is equivalent to having manual BGM. A new bool can be introduced if this changes.
            // threatLv may be read directly later (but event checks are generally preprocessed out).
            HashSet<int> worldBossNpcs =
                new HashSet<int>(infos.Values.Where(i => i.IsImportantTarget && !i.HasPerceptiveNose).Select(i => defaultData[i.ID].NPC));

            foreach (PARAM.Row row in Params["NpcParam"].Rows)
            {
                if (!worldBossNpcs.Contains(row.ID)) continue;
                uint threatLv = (uint)row["threatLv"].Value;
                if (threatLv > 1)
                {
                    npcs.ThreatLvs.Add(threatLv);
                    npcs.NpcThreatLvs[row.ID] = threatLv;
                }
            }
            if (opt["bossbgm"])
            {
                Shuffle(new Random((int)opt.Seed + 400), npcs.ThreatLvs);
                // Console.WriteLine($"threat levels: {string.Join(",", threatLvs)}");
                int index = 0;
                // This loop uses the same conditions as the above one, so it should be a full permutation
                foreach (PARAM.Row row in Params["NpcParam"].Rows)
                {
                    if (!worldBossNpcs.Contains(row.ID)) continue;
                    uint threatLv = (uint)row["threatLv"].Value;
                    if (threatLv > 1)
                    {
                        uint newThreatLv = npcs.ThreatLvs[index++ % npcs.ThreatLvs.Count];
                        // In other cases, this is done using npcThreatTargets below
                        row["threatLv"].Value = newThreatLv;
                    }
                }
            }

            // Elden Ring version of DS3 item stuff, with a few more constraints here too
            // Always do it for all eligible enemies so that restart isn't required between randomizations
            foreach (EnemyInfo info in infos.Values.Where(i => i.HasTag("npcitem")))
            {
                // Avoid it for vanilla onslaught so there aren't scadutree blessing duplicates
                if (!perm.AnyRandomized && !enemies.AnyDupeEnabled) break;
                // This can happen with ignored/missing enemies and merged mods
                if (!defaultData.ContainsKey(info.ID)) continue;
                int npc = defaultData[info.ID].NPC;
                if (npcs.NpcItemLots.ContainsKey(npc)) continue;
                if (!npcs.TryGetNpc(npc, out PARAM.Row row))
                {
                    throw new Exception($"Missing NpcParam {npc} required to rewrite item dropped by {enemies.Name(info.ID)}");
                }
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
                    // Flag uniqueness is checked when adding npcitem. But this can change if multiple items end up here, so allow it
                    if (flags.Count > 0)
                    {
                        npcs.NpcItemLots[npc] = (mapLot, (int)flags.First());
                        row[$"itemLotId_map"].Value = -1;
                    }
                }
                else if (enemyItems.Count > 0 && mapLot <= 0)
                {
                    List<uint> flags = enemyItems.Select(r => (uint)r["getItemFlagId"].Value).Distinct().ToList();
                    // if (flags.Count > 0) Console.WriteLine($"enemy flags {npc} = {string.Join(", ", flags)}");
                    // Flag uniqueness is checked when adding npcitem. But this can change if multiple items end up here, so allow it
                    // This previously used flags.Count == 1 || info.HasTag("multiflag"), which could still be useful during lot analysis
                    if (flags.Count > 0)
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
                        npcs.NpcItemLots[npc] = (newBaseMapLot, (int)flags.First());
                    }
                }
            }

            SortedSet<int> humanThinks = new(
                infos.Values.Where(i => i.Class == EnemyClass.HostileNPC).Select(i => defaultData[i.ID].Think));

            // TODO: Apply different values for arena bosses, world bosses, regular npcs.
            foreach (PARAM.Row row in Params["NpcThinkParam"].Rows)
            {
                // max out nose_dist and eye_dist at 100. (basic gets further manual edits)
                // Especially fix Tanith's Knight, which has 9999 nose_dist
                // Fire Giant is 100 eye_dist, 999 nose_dist
                npcs.ShrinkRange(row, 100, 200);
                // Further shrink Tanith's Knight to be close to other Crucible Knights (20)
                // Also Mini Midra with 100 nose_dist
                if (row.ID == 25009000 || row.ID == 50500000)
                {
                    npcs.ShrinkRange(row, 30);
                }
                // Moongrum, Miriam, and Flame Guardians exist out in the world, so limit their range particularly from invaders
                // All NPCs generally have eye_dist of 18 or 20, but invaders have 9999 nose_dist
                // maxBackhomeDist is 75 for regular NPCs, 9999 for invaders
                if (humanThinks.Contains(row.ID))
                {
                    // Make sure there's expanded versions (done in second pass)
                    // Could limit the original row, but trust the universal baseline will work.
                    // shrinkRange(row, 50);
                }
                // Dancer   think 524220000 has logicId=31000, battleGoalID=29999
                // Moongrum think 523590100 has logicId=10000, battleGoalID=29999
                // This causes chasing even with AI turned off.
                // Maybe turn it on the other way around, but at least turn it off here.
                if ((int)row["battleGoalID"].Value == 29999 && (int)row["logicId"].Value == 31000)
                {
                    row["logicId"].Value = 10000;
                }
                // Enemy Onslaught traditional mode
                if (opt["impolite"] || (preset != null && preset["impolite"]))
                {
                    row["TeamAttackEffectivity"].Value = (byte)0;
                }
            }
            // Also modify param itself to add new rows
            foreach (int thinkId in humanThinks)
            {
                {
                    PARAM.Row think = npcs.DuplicateAdjacentThink(thinkId);
                    think["nose_dist"].Value = (ushort)9999;
                    think["eye_dist"].Value = Math.Max((ushort)think["eye_dist"].Value, (ushort)20);
                    think["maxBackhomeDist"].Value = (ushort)100;
                    // Restore infinite chase AI
                    if ((int)think["battleGoalID"].Value == 29999 && (int)think["logicId"].Value == 10000)
                    {
                        think["logicId"].Value = 31000;
                    }
                    npcs.NpcAsInvaderThink[thinkId] = think.ID;
                }
                {
                    PARAM.Row think = npcs.DuplicateAdjacentThink(thinkId);
                    think["nose_dist"].Value = Math.Min((ushort)think["nose_dist"].Value, (ushort)0);
                    think["eye_dist"].Value = Math.Min((ushort)think["eye_dist"].Value, (ushort)20);
                    npcs.NpcAsWorldThink[thinkId] = think.ID;
                }
            }

            void addSetEffect(int npcId, int spId)
            {
                if (npcs.TryGetNpc(npcId, out PARAM.Row row))
                {
                    game.AddNpcSpEffect(row, spId);
                }
            }
            // Transplant Chapel's Grafted Scion behavior this way, from speffectset 46900030, entity id 10010800.
            addSetEffect(46900008, 16291);
            // Also Mariners from their speffectsets (also added by script)
            addSetEffect(49500032, 15346);
            addSetEffect(49500020, 15348);
            addSetEffect(49500010, 15347);
            // PCR p1 should not transform. This needs to be done in event as well, as it expires
            addSetEffect(52200089, 20011598);
            // Divine Beast Dancing Lion deathblight AI
            addSetEffect(52100094, 20011248);

            npcs.GetNpc(55600390)["disableRespawn"].Value = (byte)1;

            // Undo CreatedReferredDamagePair
            uint hp;
            // Hoarah Loux p2 (47210070, HP 3186 scaling 7160) <- p1 (47200070, HP 1721 scaling 7160) dead
            PARAM.Row hoarah1 = npcs.GetNpc(defaultData[11050801].NPC);
            PARAM.Row hoarah2 = npcs.GetNpc(defaultData[11050800].NPC);
            hp = (uint)hoarah2["hp"].Value - (uint)hoarah1["hp"].Value;
            if (hp > 500 && hp < 5000)
            {
                // Console.WriteLine($"hoarah: {hoarah2["hp"].Value}->{hp}");
                hoarah2["hp"].Value = hp;
            }
            // Maliketh p2 (21101072, HP 1588 scaling 7150) <- p1 (21100072, HP 1588 scaling 7150) at 55% health
            // TODO: When not randomized, the 55% health condition is used
            PARAM.Row maliketh1 = npcs.GetNpc(defaultData[13000801].NPC);
            PARAM.Row maliketh2 = npcs.GetNpc(defaultData[13000800].NPC);
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
            PARAM.Row giant1 = npcs.GetNpc(defaultData[1052520801].NPC);
            PARAM.Row giant2 = npcs.GetNpc(defaultData[1052520800].NPC);
            hp = ((uint)giant2["hp"].Value - (uint)giant1["hp"].Value) * 90 / 100;
            if (hp > 500 && hp < 5000)
            {
                // Console.WriteLine($"firegiant: {giant2["hp"].Value}->{hp}");
                giant2["hp"].Value = hp;
            }
            // defaultData not populated without DLC
            if (opt["dlc"])
            {
                // Messmer p2 (51301099 HP 2963 scaling 20007090) <- p1 (51300099, HP 2963 scaling 20007090) at 50% health
                PARAM.Row messmer1 = npcs.GetNpc(defaultData[21010801].NPC);
                PARAM.Row messmer2 = npcs.GetNpc(defaultData[21010800].NPC);
                hp = (uint)messmer1["hp"].Value * 50 / 100;
                messmer1["hp"].Value = hp;
                hp = (uint)messmer2["hp"].Value - hp;
                if (hp > 500 && hp < 5000)
                {
                    messmer2["hp"].Value = hp;
                }

                // PCR p2 (52201089, HP 3168 scaling 20007120) <- p1 (52200089, HP 3168 scaling 20007120) at 65% health
                PARAM.Row pcr1 = npcs.GetNpc(defaultData[20010801].NPC);
                PARAM.Row pcr2 = npcs.GetNpc(defaultData[20010800].NPC);
                hp = (uint)pcr1["hp"].Value * 35 / 100;
                pcr1["hp"].Value = hp;
                hp = (uint)pcr2["hp"].Value - hp;
                if (hp > 500 && hp < 5000)
                {
                    pcr2["hp"].Value = hp;
                }
            }

            // Set up dupe colors here. Can be done basically any time
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

                    npcs.DupeBossSpEffects.Add(sp.ID);
                }
            }
            return npcs;
        }

        public override void FinalNpcPass(EnemyState state, RandomizerOptions opt)
        {
            GameData game = Game;
            ParamDictionary Params = game.Params;
            Enemies enemies = state.Enemies;
            EnemyMapping mapping = state.Mapping;
            EnemyPermutation perm = state.Permutation;

            if (perm.AnyRandomized)
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
            if (mapping.IsRandomizedSource(16000800) || mapping.IsRandomizedSource(16000801))
            {
                // Removing either 4710600 or 4710601 seems to work. The former has fire damage
                Params["AtkParam_Npc"].Rows.RemoveAll(r => r.ID == 4710600);
            }
            // Progress speffects are added in MiscSetup instead

            if (opt["nightday"])
            {
                // Night's Cav and Deathbirds
                // Bats for e.g. Weeping Deathbird in m60_43_32_00 might use UnkT28 with hours/minutes as bytes
                // Could alternatively clear ChrActivateCondParamID
                // Snowfield Night's Cav has been edited to use this condition as well
                PARAM.Row nightCond = Params["ChrActivateConditionParam"][101000];
                if (nightCond != null)
                {
                    foreach (PARAM.Cell cell in nightCond.Cells)
                    {
                        if (cell.Def.InternalName.StartsWith("time")) cell.Value = (byte)0;
                    }
                }
                // Bell Bearing Hunters in config pass, though it's a bit silly
            }

            // Make bosses drop non-zero runes. This excludes dupe for now, probably fine.
            // Dane 2049440710 doesn't have an entry, Ymir 2051450720 does at least.
            // But Ymir has runes anyway?

            // Fix animation blending bug for bosses with lip sync, by disabling it if the source can't support it
            // This should mainly only apply when presets are present, but it does affect Fortissax, so check it for everyone.
            // TalkParam is pretty big and expensive to write, but that's mainly a PARAM impl problem.
            {
                HashSet<int> talkPrefixes = new HashSet<int>();
                foreach (uint target in enemies.TalkLineEdits)
                {
                    // This seems to be fine between major bosses? Makar is fine, but Theo is not.
                    if (mapping.GetSource(target, out uint source) && enemies.TryInfo(source, out EnemyInfo info)
                        && (info.HasTag("nospeak") || !(info.Class == EnemyClass.Boss || info.HasTag("speak") || info.Name.StartsWith("c0000"))))
                    {
                        foreach (string line in PhraseRe.Split(enemies[target].TalkLine))
                        {
                            talkPrefixes.Add(int.Parse(line) / 100);
                        }
                    }
                }
                if (talkPrefixes.Count > 0)
                {
                    foreach (PARAM.Row talk in Params["TalkParam"].Rows)
                    {
                        if (talkPrefixes.Contains(talk.ID / 100))
                        {
                            talk["lipSyncStart"].Value = (short)-1;
                            // Unclear what this does to blending, but better to remove possible conflicts if possible
                            talk["talkAnimationId"].Value = -1;
                        }
                    }
                }
            }

            // Look at deathline cases, probably just always
            // TODO: This doesn't work
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                bool modified = false;
                foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                {
                    ESD esd = esdEntry.Value;
                    int esdId = int.Parse(esdEntry.Key.Substring(1));
                    if (esdId == 324001105)
                    {
                        // Try to remove animations or whatever

                        // x35 RequestAnimation
                        if (AST.ParseMachine("x35", out int mid) && esd.StateGroups.TryGetValue(mid, out Dictionary<long, ESD.State> m))
                        {
                            // m[6].WhileCommands.RemoveRange(0, 1);
                            modified = true;
                        }
                        if (AST.ParseMachine("x30", out mid) && esd.StateGroups.TryGetValue(mid, out m))
                        {
                            // m[1].EntryCommands.RemoveRange(0, 3);
                            modified = true;
                        }
                        if (AST.ParseMachine("x28", out mid) && esd.StateGroups.TryGetValue(mid, out m))
                        {
                            // The actual talk command
                            // m[1].EntryCommands.RemoveRange(0, 1);
                            modified = true;
                        }
                        if (esd.StateGroups.TryGetValue(1102, out m))
                        {
                            m[3].WhileCommands.RemoveRange(0, 1);
                            modified = true;
                        }
                    }
                    if (!enemies.DeathLineFlags.TryGetValue(esdId, out int defeatFlag)) continue;
                    if (!esd.StateGroups.TryGetValue(1103, out Dictionary<long, ESD.State> machine)) continue;
                    if (!machine.TryGetValue(0, out ESD.State esdState) || esdState.Conditions.Count != 1) continue;
                    ESD.Condition cond = esdState.Conditions[0];
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

            // Invisible enemies hack
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
    }
}
