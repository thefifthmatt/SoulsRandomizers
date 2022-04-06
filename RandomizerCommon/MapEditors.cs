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
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Preset;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class MapEditors
    {
        // Awful abuse of C# but we take an inch from SoulsFormats and go a mile, or however the saying goes.
        // At the very least, it has no state.
        public abstract class EnemyEditor<TMap, TEnemy, TRegion, TGenerator>
            where TMap : IMsb where TEnemy : IMsbPart where TRegion : IMsbRegion where TGenerator : IMsbEvent
        {
            // Generic enemy list access
            public abstract List<TEnemy> GetEnemies(TMap msb);
            // Fills in enemy data
            public abstract EnemyData GetEnemyData(TEnemy e, string map);
            // Initially populating entity id
            public abstract void SetEntityID(TEnemy e, int id);
            // Get enemy by entity id
            public abstract TEnemy GetEnemy(TMap msb, int id);
            // Get region by entity id
            public abstract TRegion GetRegion(TMap msb, int id);
            // Keeping track of model declarations
            public abstract ModelEditor GetModelEditor(TMap msb);
            // Move one enemy to another
            public abstract TEnemy TransplantEnemy(TMap msb, EnemyData source, int target, int? npcParam = null);
            // Clone one enemy to a new location
            public abstract TEnemy CloneEnemy(TMap msb, TEnemy sourcePart, EnemyData source, int newTarget, int newPartId);
            // Set enemy collision, which usually becomes invalid across maps
            public abstract void SetEnemyCollision(TEnemy enemy, string colName);
            // Clear entity groups for an enemy
            public abstract void ClearGroups(TEnemy e);
            // Copy group data from a source enemy into its transplant
            public abstract void CopyGroups(EnemyData source, Dictionary<int, int> groupMapping, TEnemy target, int removeTarget = 0);
            // Remove some enemies by entity id
            public abstract void RemoveEnemies(TMap msb, Predicate<int> filter);
            // Delete metadata that references unused enemies and objects
            public abstract void GarbageCollect(TMap msb, HashSet<string> usedEnemies = null, HashSet<string> removedObjects = null);
            // Collect all generator data before they are deleted due to stale enemy references
            public abstract Dictionary<int, (TGenerator, List<int>)> GetGeneratorData(Dictionary<string, TMap> msbs);
            // Copy a generator from one location to a different one
            public abstract (List<TRegion>, int) MakeGeneratorCopy(
                Dictionary<string, TMap> msbs, Func<int> newEntity, string map, string toMap, int id,
                Dictionary<int, (TGenerator, List<int>)> generators, Dictionary<int, int> reloc);
            // Copy a region from one location to a different one
            public abstract (TRegion, TRegion, int) MakeRegionCopy(
                Dictionary<string, TMap> msbs, Func<int> newEntity, string map, string toMap, int id,
                bool replace);
            // Add a standalone region based on arena data
            public abstract TRegion MakeArenaRegion(TMap msb, Arena arena, string name);
        }

        public class DS3EnemyEditor : EnemyEditor<MSB3, MSB3.Part.Enemy, MSB3.Region, MSB3.Event.Generator>
        {
            public override List<MSB3.Part.Enemy> GetEnemies(MSB3 msb) => msb.Parts.Enemies;
            public override void SetEntityID(MSB3.Part.Enemy e, int id) => e.EntityID = id;
            public override MSB3.Part.Enemy GetEnemy(MSB3 msb, int id) => msb.Parts.Enemies.Find(e => e.EntityID == id);
            public override MSB3.Region GetRegion(MSB3 msb, int id) => msb.Regions.GetEntries().Find(e => e.EntityID == id);

            public override EnemyData GetEnemyData(MSB3.Part.Enemy e, string map)
            {
                return new EnemyData
                {
                    Map = map,
                    Name = e.Name,
                    ID = e.EntityID,
                    Group = e.EntityGroups.Where(g => g > 0).ToList(),
                    Model = e.ModelName,
                    NPC = e.NPCParamID,
                    Think = e.ThinkParamID,
                    Char = e.CharaInitID,
                    Col = e.CollisionName,
                };
            }

            public override ModelEditor GetModelEditor(MSB3 msb)
            {
                return new ModelEditor
                {
                    EnemyDecl = new HashSet<string>(msb.Models.Enemies.Select(model => model.Name)),
                    EnemyUsed = new HashSet<string>(msb.Parts.GetEntries()
                        .Where(e => e is MSB3.Part.DummyEnemy || e is MSB3.Part.Enemy || e is MSB3.Part.Player)
                        .Select(e => e.ModelName)),
                    ObjectDecl = new HashSet<string>(msb.Models.Objects.Select(model => model.Name)),
                    RegisterEnemy = name =>
                    {
                        msb.Models.Enemies.Add(new MSB3.Model.Enemy
                        {
                            Name = name,
                            SibPath = $@"N:\FDP\data\Model\chr\{name}\sib\{name}.sib",
                        });
                    },
                    RegisterObject = name =>
                    {
                        msb.Models.Objects.Add(new MSB3.Model.Object
                        {
                            Name = name,
                            SibPath = $@"N:\FDP\data\Model\obj\{name.Substring(0, 3)}\{name}\sib\{name}.sib",
                        });
                    },
                };
            }

            public override MSB3.Part.Enemy TransplantEnemy(MSB3 msb, EnemyData source, int target, int? npcParam = null)
            {
                MSB3.Part.Enemy e = msb.Parts.Enemies.Find(en => en.EntityID == target);
                e.ModelName = source.Model;
                e.NPCParamID = npcParam ?? source.NPC;
                e.ThinkParamID = source.Think;
                e.CharaInitID = source.Char;
                return e;
            }

            public override MSB3.Part.Enemy CloneEnemy(MSB3 msb, MSB3.Part.Enemy sourcePart, EnemyData source, int newTarget, int newPartId)
            {
                MSB3.Part.Enemy e2 = (MSB3.Part.Enemy)sourcePart.DeepCopy();
                e2.ModelName = source.Model;
                e2.NPCParamID = source.NPC;
                e2.ThinkParamID = source.Think;
                e2.CharaInitID = source.Char;
                e2.EntityID = newTarget;
                e2.Name = $"{e2.ModelName}_{newPartId:d4}";
                // Transplanting talks seems to break most talk scripts
                e2.TalkID = -1;
                msb.Parts.Enemies.Add(e2);
                return e2;
            }

            public override void SetEnemyCollision(MSB3.Part.Enemy enemy, string colName)
            {
                enemy.CollisionName = colName;
            }

            public override void ClearGroups(MSB3.Part.Enemy e)
            {
                for (int i = 0; i < e.EntityGroups.Length; i++) e.EntityGroups[i] = -1;
            }

            public override void CopyGroups(EnemyData source, Dictionary<int, int> groupMapping, MSB3.Part.Enemy target, int removeTarget = 0)
            {
                CopyGroupsInternal(source, groupMapping, target.EntityGroups, removeTarget);
            }

            public override void RemoveEnemies(MSB3 msb, Predicate<int> removePredicate)
            {
                msb.Parts.Enemies = msb.Parts.Enemies.Where(p => !removePredicate(p.EntityID)).ToList();
            }

            public override void GarbageCollect(MSB3 msb, HashSet<string> usedEnemies = null, HashSet<string> removedObjects = null)
            {
                HashSet<string> names = new HashSet<string>(msb.Parts.Enemies.Select(e => e.Name));
                // These was a reason this doesn't use RemoveAll, but I forget what it was
                msb.Events.PlatoonInfo = msb.Events.PlatoonInfo.Where(t => t.GroupPartsNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.Generators = msb.Events.Generators.Where(t => t.SpawnPartNames.All(n => n == null || names.Contains(n))).ToList();
                // This appears to cause a write error in a DS3 map, as there's a map with an object model in the enemy model section?
                if (false && usedEnemies != null)
                {
                    msb.Models.Enemies = msb.Models.Enemies.Where(e => usedEnemies.Contains(e.Name)).OrderBy(e => e.Name).ToList();
                }
                if (removedObjects != null && removedObjects.Count > 0)
                {
                    msb.Events.Treasures.RemoveAll(t => removedObjects.Contains(t.TreasurePartName));
                    msb.Events.ObjActs.RemoveAll(t => removedObjects.Contains(t.ObjActPartName));
                }
            }

            public override Dictionary<int, (MSB3.Event.Generator, List<int>)> GetGeneratorData(Dictionary<string, MSB3> msbs)
            {
                Dictionary<int, (MSB3.Event.Generator, List<int>)> generators = new Dictionary<int, (MSB3.Event.Generator, List<int>)>();
                foreach (KeyValuePair<string, MSB3> entry in msbs)
                {
                    MSB3 msb = entry.Value;
                    if (msb.Events.Generators.Count == 0) continue;
                    Dictionary<string, int> mapEnemies = msb.Parts.Enemies.Where(e => e.EntityID > 0).ToDictionary(e => e.Name, e => e.EntityID);
                    foreach (MSB3.Event.Generator gen in msb.Events.Generators)
                    {
                        List<int> entities = gen.SpawnPartNames
                            .Select(n => n != null && mapEnemies.TryGetValue(n, out int id) ? id : -1)
                            .Where(id => id > 0)
                            .ToList();
                        generators[gen.EntityID] = (gen, entities);
                    }
                }
                return generators;
            }

            public override (MSB3.Region, MSB3.Region, int) MakeRegionCopy(
                Dictionary<string, MSB3> msbs, Func<int> newEntity, string map, string toMap, int id, bool replace)
            {
                MSB3.Region a = msbs[map].Regions.GetEntries().Find(r => r.EntityID == id);
                if (a == null) throw new Exception($"Internal error: can't find region {id} in {map}");
                MSB3.Region b = a.DeepCopy();
                MSB3 msb = msbs[toMap];
                msb.Regions.Add(b);
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
                return (a, b, b.EntityID);
            }

            public override (List<MSB3.Region>, int) MakeGeneratorCopy(
                Dictionary<string, MSB3> msbs, Func<int> newEntity, string map, string toMap, int id,
                Dictionary<int, (MSB3.Event.Generator, List<int>)> generators, Dictionary<int, int> reloc)
            {
                MSB3 fromMsb = msbs[map];
                MSB3 msb = msbs[toMap];
                if (!generators.ContainsKey(id)) throw new Exception($"Did not find generator {id}");
                (MSB3.Event.Generator gen, List<int> enemyIds) = generators[id];
                if (enemyIds.Count == 0) throw new Exception($"Generator {id} has no enemies associated with it");

                MSB3.Event.Generator gen2 = (MSB3.Event.Generator)gen.DeepCopy();
                Array.Clear(gen2.SpawnPartNames, 0, gen2.SpawnPartNames.Length);
                Array.Clear(gen2.SpawnPointNames, 0, gen2.SpawnPointNames.Length);
                gen2.PartName = gen2.PointName = null;

                int enemyIndex = 0;
                foreach (int fromEnemy in enemyIds)
                {
                    if (!reloc.TryGetValue(fromEnemy, out int target)) throw new Exception($"No target found for {fromEnemy} from generator {id}");
                    MSB3.Part.Enemy enemy = msb.Parts.Enemies.Find(e => e.EntityID == target);
                    gen2.SpawnPartNames[enemyIndex++] = enemy.Name;
                }
                // When does this happen?
                if (enemyIndex == 0) return (null, 0);

                int genId = newEntity();
                gen2.EntityID = genId;
                gen2.Name = $"Generator {genId} {id}";
                msb.Events.Generators.Add(gen2);

                List<MSB3.Region> regions = new List<MSB3.Region>();
                for (int i = 0; i < gen.SpawnPointNames.Length; i++)
                {
                    string regionName = gen.SpawnPointNames[i];
                    if (regionName == null) continue;
                    MSB3.Region.Event a = fromMsb.Regions.Events.Find(r => r.Name == regionName);
                    MSB3.Region.Event b = (MSB3.Region.Event)a.DeepCopy();
                    b.EntityID = -1;
                    b.Name = $"Generator Point #{i} {genId} {id}";
                    gen2.SpawnPointNames[i] = b.Name;
                    regions.Add(b);
                    msb.Regions.Add(b);
                }
                return (regions, genId);
            }

            public override MSB3.Region MakeArenaRegion(MSB3 msb, Arena arena, string name)
            {
                MSB.Shape.Box box = new MSB.Shape.Box();
                box.Width = arena.Box.X;
                box.Height = arena.Box.Y;
                box.Depth = arena.Box.Z;
                MSB3.Region.Event r = new MSB3.Region.Event();
                r.Shape = box;
                r.Position = arena.Pos;
                r.Rotation = arena.Rot;
                r.Name = name;
                msb.Regions.Add(r);
                return r;
            }
        }

        public class SekiroEnemyEditor : EnemyEditor<MSBS, MSBS.Part.Enemy, MSBS.Region, MSBS.Event.Generator>
        {
            public override List<MSBS.Part.Enemy> GetEnemies(MSBS msb) => msb.Parts.Enemies;
            public override void SetEntityID(MSBS.Part.Enemy e, int id) => e.EntityID = id;
            public override MSBS.Part.Enemy GetEnemy(MSBS msb, int id) => msb.Parts.Enemies.Find(e => e.EntityID == id);
            public override MSBS.Region GetRegion(MSBS msb, int id) => msb.Regions.GetEntries().Find(e => e.EntityID == id);

            public override EnemyData GetEnemyData(MSBS.Part.Enemy e, string map)
            {
                return new EnemyData
                {
                    Map = map,
                    Name = e.Name,
                    ID = e.EntityID,
                    Group = e.EntityGroupIDs.Where(g => g > 0).ToList(),
                    Model = e.ModelName,
                    NPC = e.NPCParamID,
                    Think = e.ThinkParamID,
                    Col = e.CollisionPartName,
                };
            }

            public override ModelEditor GetModelEditor(MSBS msb)
            {
                return new ModelEditor
                {
                    EnemyDecl = new HashSet<string>(msb.Models.Enemies.Select(model => model.Name)),
                    EnemyUsed = new HashSet<string>(msb.Parts.GetEntries()
                        .Where(e => e is MSBS.Part.DummyEnemy || e is MSBS.Part.Enemy || e is MSBS.Part.Player)
                        .Select(e => e.ModelName)),
                    ObjectDecl = new HashSet<string>(msb.Models.Objects.Select(model => model.Name)),
                    RegisterEnemy = name =>
                    {
                        msb.Models.Enemies.Add(new MSBS.Model.Enemy
                        {
                            Name = name,
                            SibPath = $@"N:\NTC\data\Model\chr\{name}\sib\{name}.sib",
                        });
                    },
                    RegisterObject = name =>
                    {
                        msb.Models.Objects.Add(new MSBS.Model.Object
                        {
                            Name = name,
                            SibPath = $@"N:\NTC\data\Model\obj\{name.Substring(0, 3)}\{name}\sib\{name}.sib",
                        });
                    },
                };
            }

            public override MSBS.Part.Enemy TransplantEnemy(MSBS msb, EnemyData source, int target, int? npcParam = null)
            {
                MSBS.Part.Enemy e = msb.Parts.Enemies.Find(en => en.EntityID == target);
                e.ModelName = source.Model;
                e.NPCParamID = npcParam ?? source.NPC;
                e.ThinkParamID = source.Think;
                return e;
            }

            public override MSBS.Part.Enemy CloneEnemy(MSBS msb, MSBS.Part.Enemy sourcePart, EnemyData source, int newTarget, int newPartId)
            {
                MSBS.Part.Enemy e2 = (MSBS.Part.Enemy)sourcePart.DeepCopy();
                e2.ModelName = source.Model;
                e2.NPCParamID = source.NPC;
                e2.ThinkParamID = source.Think;
                e2.EntityID = newTarget;
                e2.Name = $"{e2.ModelName}_{newPartId:d4}";
                // Clear out things that should never apply to helpers
                e2.UnkT24 = 0;  // Causes main boss to break
                msb.Parts.Enemies.Add(e2);
                return e2;
            }

            public override void SetEnemyCollision(MSBS.Part.Enemy enemy, string colName)
            {
                enemy.CollisionPartName = colName;
            }

            public override void ClearGroups(MSBS.Part.Enemy e)
            {
                for (int i = 0; i < e.EntityGroupIDs.Length; i++) e.EntityGroupIDs[i] = -1;
            }

            public override void CopyGroups(EnemyData source, Dictionary<int, int> groupMapping, MSBS.Part.Enemy target, int removeTarget = 0)
            {
                CopyGroupsInternal(source, groupMapping, target.EntityGroupIDs, removeTarget);
            }

            public override void RemoveEnemies(MSBS msb, Predicate<int> removePredicate)
            {
                msb.Parts.Enemies = msb.Parts.Enemies.Where(p => !removePredicate(p.EntityID)).ToList();
            }

            public override void GarbageCollect(MSBS msb, HashSet<string> usedEnemies = null, HashSet<string> removedObjects = null)
            {
                HashSet<string> names = new HashSet<string>(msb.Parts.Enemies.Select(e => e.Name));
                msb.Events.Talks = msb.Events.Talks.Where(t => t.EnemyNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.PlacementGroups = msb.Events.PlacementGroups.Where(t => t.Event21PartNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.PlatoonInfo = msb.Events.PlatoonInfo.Where(t => t.GroupPartNames.All(n => n == null || names.Contains(n))).ToList();
                msb.Events.Generators = msb.Events.Generators.Where(t => t.SpawnPartNames.All(n => n == null || names.Contains(n))).ToList();
                if (usedEnemies != null)
                {
                    msb.Models.Enemies = msb.Models.Enemies.Where(e => usedEnemies.Contains(e.Name)).OrderBy(e => e.Name).ToList();
                }
            }

            public override Dictionary<int, (MSBS.Event.Generator, List<int>)> GetGeneratorData(Dictionary<string, MSBS> msbs)
            {
                Dictionary<int, (MSBS.Event.Generator, List<int>)> generators = new Dictionary<int, (MSBS.Event.Generator, List<int>)>();
                foreach (KeyValuePair<string, MSBS> entry in msbs)
                {
                    MSBS msb = entry.Value;
                    if (msb.Events.Generators.Count == 0) continue;
                    Dictionary<string, int> mapEnemies = msb.Parts.Enemies.Where(e => e.EntityID > 0).ToDictionary(e => e.Name, e => e.EntityID);
                    foreach (MSBS.Event.Generator gen in msb.Events.Generators)
                    {
                        List<int> entities = gen.SpawnPartNames
                            .Select(n => n != null && mapEnemies.TryGetValue(n, out int id) ? id : -1)
                            .Where(id => id > 0)
                            .ToList();
                        generators[gen.EntityID] = (gen, entities);
                    }
                }
                return generators;
            }

            public override (MSBS.Region, MSBS.Region, int) MakeRegionCopy(
                Dictionary<string, MSBS> msbs, Func<int> newEntity, string map, string toMap, int id, bool replace)
            {
                MSBS.Region a = msbs[map].Regions.GetEntries().Find(r => r.EntityID == id);
                if (a == null) throw new Exception($"Internal error: can't find region {id} in {map}");
                // Shallow copy, but fine if no nested fields are modified
                MSBS.Region b = (MSBS.Region)Activator.CreateInstance(a.GetType());
                CopyAll(a, b);
                MSBS msb = msbs[toMap];
                if (b is MSBS.Region.Event et) msb.Regions.Events.Add(et);
                else if (b is MSBS.Region.SFX st) msb.Regions.SFX.Add(st);
                else if (b is MSBS.Region.PatrolRoute wt) msb.Regions.PatrolRoutes.Add(wt);
                else if (b is MSBS.Region.ActivationArea at) msb.Regions.ActivationAreas.Add(at);
                else if (b is MSBS.Region.Other ot) msb.Regions.Others.Add(ot);
                // else if (b is MSBS.Region.Region0 rt) msb.Regions.Region0s.Add(rt);
                else throw new Exception($"Internal error: unknown region type {b}, entity id {id}");
                // Except shape is a nested field which is modified, so do that
                MSB.Shape shape = (MSB.Shape)Activator.CreateInstance(b.Shape.GetType());
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
                return (a, b, b.EntityID);
            }

            public override (List<MSBS.Region>, int) MakeGeneratorCopy(
                Dictionary<string, MSBS> msbs, Func<int> newEntity, string map, string toMap, int id,
                Dictionary<int, (MSBS.Event.Generator, List<int>)> generators, Dictionary<int, int> reloc)
            {
                MSBS fromMsb = msbs[map];
                MSBS msb = msbs[toMap];
                if (!generators.ContainsKey(id)) throw new Exception($"Did not find generator {id}");
                (MSBS.Event.Generator gen, List<int> enemyIds) = generators[id];
                if (enemyIds.Count == 0) throw new Exception($"Generator {id} has no enemies associated with it");

                MSBS.Event.Generator gen2 = (MSBS.Event.Generator)gen.DeepCopy();
                Array.Clear(gen2.SpawnPartNames, 0, gen2.SpawnPartNames.Length);
                Array.Clear(gen2.SpawnRegionNames, 0, gen2.SpawnRegionNames.Length);
                gen2.PartName = gen2.RegionName = null;

                int enemyIndex = 0;
                foreach (int fromEnemy in enemyIds)
                {
                    if (!reloc.TryGetValue(fromEnemy, out int target))
                    {
                        throw new Exception($"No target found for {fromEnemy} from generator {id}");
                    }
                    MSBS.Part.Enemy enemy = msb.Parts.Enemies.Find(e => e.EntityID == target);
                    gen2.SpawnPartNames[enemyIndex++] = enemy.Name;
                }
                // When does this happen?
                if (enemyIndex == 0) return (null, 0);

                int genId = newEntity();
                gen2.EntityID = genId;
                gen2.Name = $"Generator {genId} {id}";
                msb.Events.Generators.Add(gen2);

                List<MSBS.Region> regions = new List<MSBS.Region>();
                for (int i = 0; i < gen.SpawnRegionNames.Length; i++)
                {
                    string regionName = gen.SpawnRegionNames[i];
                    if (regionName == null) continue;
                    MSBS.Region.Event a = fromMsb.Regions.Events.Find(r => r.Name == regionName);
                    MSBS.Region.Event b = (MSBS.Region.Event)a.DeepCopy();
                    b.EntityID = -1;
                    b.Name = $"Generator Point #{i} {genId} {id}";
                    gen2.SpawnRegionNames[i] = b.Name;
                    regions.Add(b);
                    msb.Regions.Add(b);
                }
                return (regions, genId);
            }

            public override MSBS.Region MakeArenaRegion(MSBS msb, Arena arena, string name)
            {
                MSB.Shape.Box box = new MSB.Shape.Box();
                box.Width = arena.Box.X;
                box.Height = arena.Box.Y;
                box.Depth = arena.Box.Z;
                MSBS.Region.Event r = new MSBS.Region.Event();
                r.Shape = box;
                r.Position = arena.Pos;
                r.Rotation = arena.Rot;
                r.Name = name;
                msb.Regions.Add(r);
                return r;
            }
        }

        public class ModelEditor
        {
            public HashSet<string> EnemyDecl;
            public HashSet<string> EnemyUsed;
            public HashSet<string> ObjectDecl;
            public Action<string> RegisterEnemy;
            public Action<string> RegisterObject;

            public void UseModel(string name)
            {
                EnemyUsed.Add(name);
                if (!EnemyDecl.Contains(name))
                {
                    RegisterEnemy(name);
                    EnemyDecl.Add(name);
                }
            }
            public void UseObjectModel(string name)
            {
                if (!ObjectDecl.Contains(name))
                {
                    RegisterObject(name);
                    ObjectDecl.Add(name);
                }
            }
        }

        private static void CopyGroupsInternal(EnemyData source, Dictionary<int, int> groupMapping, int[] targetGroups, int removeTarget)
        {
            if (groupMapping.Count == 0 && removeTarget == 0)
            {
                return;
            }
            if (removeTarget > 0)
            {
                int removeIndex = Array.IndexOf(targetGroups, removeTarget);
                if (removeIndex >= 0)
                {
                    targetGroups[removeIndex] = -1;
                }
            }
            foreach (int groupToAdd in source.Group.SelectMany(g => groupMapping.TryGetValue(g, out int g2) ? new int[] { g2 } : new int[] { }))
            {
                int groupIndex = Array.IndexOf(targetGroups, -1);
                if (groupIndex == -1)
                {
                    throw new Exception($"Ran out of group slots mapping {groupToAdd} from {source.ID} -> {targetGroups}");
                }
                targetGroups[groupIndex] = groupToAdd;
            }
        }

        // TODO: Use DeepCopy instead
        private static void CopyAll<T>(T source, T target)
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
