using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static RandomizerCommon.EnemyAnnotations;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class SekiroEnemyEditor : EnemyEditor<MSBS, MSBS.Part.Enemy, MSBS.Part.Object, MSBS.Region, MSBS.Event.Generator>
    {
        public override List<MSBS.Part.Enemy> GetEnemies(MSBS msb) => msb.Parts.Enemies;
        public override void SetEntityID(MSBS.Part.Enemy e, uint id) => e.EntityID = (int)id;
        public override MSBS.Part.Enemy GetEnemy(MSBS msb, uint id) => msb.Parts.Enemies.Find(e => e.EntityID == id);
        public override MSBS.Region GetRegion(MSBS msb, uint id) => msb.Regions.GetEntries().Find(e => e.EntityID == id);

        public override EnemyData GetEnemyData(MSBS.Part.Enemy e, string map)
        {
            return new EnemyData
            {
                MainMap = map,
                Name = e.Name,
                ID = (uint)e.EntityID,
                Group = e.EntityGroupIDs.Where(g => g > 0).Select(g => (uint)g).ToList(),
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

        public override MSBS.Part.Enemy TransplantEnemy(MSBS msb, EnemyData source, uint target)
        {
            MSBS.Part.Enemy e = msb.Parts.Enemies.Find(en => en.EntityID == target);
            e.ModelName = source.Model;
            e.NPCParamID = source.NPC;
            e.ThinkParamID = source.Think;
            return e;
        }

        public override void SetNpcParam(MSBS.Part.Enemy e, int? npcParam = null, int? npcThinkParam = null)
        {
            if (npcParam is int npc) e.NPCParamID = npc;
            if (npcThinkParam is int think) e.ThinkParamID = think;
        }
        public override MSBS.Part.Enemy CloneEnemy(MSBS msb, MSBS.Part.Enemy sourcePart, EnemyData source, uint newTarget, int newPartId)
        {
            MSBS.Part.Enemy e2 = (MSBS.Part.Enemy)sourcePart.DeepCopy();
            e2.ModelName = source.Model;
            e2.NPCParamID = source.NPC;
            e2.ThinkParamID = source.Think;
            e2.EntityID = (int)newTarget;
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

        public override void ClearGroups(MSBS.Part.Enemy e, ICollection<uint> preserveGroups = null)
        {
            for (int i = 0; i < e.EntityGroupIDs.Length; i++) e.EntityGroupIDs[i] = -1;
        }

        public override void CopyGroups(EnemyData source, Dictionary<uint, uint> groupMapping, MSBS.Part.Enemy target, uint removeTarget = 0)
        {
            CopyGroupsInternal(source, groupMapping, target.EntityGroupIDs, removeTarget);
        }

        public override void RemoveEnemies(MSBS msb, Predicate<uint> removePredicate)
        {
            msb.Parts.Enemies = msb.Parts.Enemies.Where(p => !removePredicate((uint)p.EntityID)).ToList();
        }

        public override (MSBS.Part.Object, uint, uint) MakeCrossMapObjectCopy(
            Func<uint> newEntity, string map, string toMap, string objId, EnemyData targetData, int newPartId)
        {
            MSBS sourceMsb = Game.SekiroMaps[map];
            MSBS targetMsb = Game.SekiroMaps[toMap];
            int sourceId = int.Parse(objId);
            MSBS.Part.Object o = sourceMsb.Parts.Objects.Find(o => o.EntityID == sourceId);
            if (o == null) return default;
            MSBS.Part.Object e2 = (MSBS.Part.Object)o.DeepCopy();
            uint target2 = newEntity();
            e2.EntityID = (int)target2;
            e2.Name = $"{e2.ModelName}_{newPartId:d4}";
            if (e2.ObjPartName3 != null)
            {
                e2.ObjPartName3 = targetData.Col;
            }
            for (int i = 0; i < e2.EntityGroupIDs.Length; i++) e2.EntityGroupIDs[i] = -1;
            targetMsb.Parts.Objects.Add(e2);
            return (e2, (uint)sourceId, target2);
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

        public override GeneratorData GetCrossMapGeneratorData()
        {
            Dictionary<uint, (MSBS.Event.Generator, List<uint>)> generators = new();
            foreach (KeyValuePair<string, MSBS> entry in Game.SekiroMaps)
            {
                MSBS msb = entry.Value;
                if (msb.Events.Generators.Count == 0) continue;
                Dictionary<string, int> mapEnemies = msb.Parts.Enemies.Where(e => e.EntityID > 0).ToDictionary(e => e.Name, e => e.EntityID);
                foreach (MSBS.Event.Generator gen in msb.Events.Generators)
                {
                    List<uint> entities = gen.SpawnPartNames
                        .Select(n => n != null && mapEnemies.TryGetValue(n, out int id) ? (uint)id : 0)
                        .Where(id => id > 0)
                        .ToList();
                    generators[(uint)gen.EntityID] = (gen, entities);
                }
            }
            return new GeneratorData { Data = generators };
        }

        public override (MSBS.Region, List<MSBS.Region>, uint) MakeCrossMapRegionCopy(
            Func<uint> newEntity, string map, string toMap, uint id, bool replace)
        {
            MSBS.Region a = Game.SekiroMaps[map].Regions.GetEntries().Find(r => r.EntityID == id);
            if (a == null) throw new Exception($"Internal error: can't find region {id} in {map}");
            // Shallow copy, but fine if no nested fields are modified
            MSBS.Region b = (MSBS.Region)Activator.CreateInstance(a.GetType());
            CopyAll(a, b);
            MSBS msb = Game.SekiroMaps[toMap];
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
                b.EntityID = (int)newEntity();
            }
            b.Name = $"Region {b.EntityID} from {id}";
            return (a, new List<MSBS.Region> { b }, (uint)b.EntityID);
        }

        public override (List<MSBS.Region>, uint) MakeCrossMapGeneratorCopy(
            Func<uint> newEntity, string map, string toMap, uint id,
            GeneratorData generators, RelocMap reloc)
        {
            MSBS fromMsb = Game.SekiroMaps[map];
            MSBS msb = Game.SekiroMaps[toMap];
            if (!generators.Data.ContainsKey(id)) throw new Exception($"Did not find generator {id}");
            (MSBS.Event.Generator gen, List<uint> enemyIds) = generators.Data[id];
            if (enemyIds.Count == 0) throw new Exception($"Generator {id} has no enemies associated with it");

            MSBS.Event.Generator gen2 = (MSBS.Event.Generator)gen.DeepCopy();
            Array.Clear(gen2.SpawnPartNames, 0, gen2.SpawnPartNames.Length);
            Array.Clear(gen2.SpawnRegionNames, 0, gen2.SpawnRegionNames.Length);
            gen2.PartName = gen2.RegionName = null;

            int enemyIndex = 0;
            foreach (uint fromEnemy in enemyIds)
            {
                if (!reloc.TryGetValue(EventValue.Enemy(fromEnemy), out EventValue target))
                {
                    throw new Exception($"No target found for {fromEnemy} from generator {id}");
                }
                int targetId = target.IntID;
                MSBS.Part.Enemy enemy = msb.Parts.Enemies.Find(e => e.EntityID == targetId);
                gen2.SpawnPartNames[enemyIndex++] = enemy.Name;
            }
            // When does this happen?
            if (enemyIndex == 0) return (null, 0);

            int genId = (int)newEntity();
            gen2.EntityID = genId;
            gen2.Name = $"Generator {genId} {id}";
            msb.Events.Generators.Add(gen2);

            List<MSBS.Region> regions = new List<MSBS.Region>();
            for (int i = 0; i < gen.SpawnRegionNames.Length; i++)
            {
                string regionName = gen.SpawnRegionNames[i];
                if (regionName == null)
                {
                    regions.Add(null);
                    continue;
                }
                MSBS.Region.Event a = fromMsb.Regions.Events.Find(r => r.Name == regionName);
                MSBS.Region.Event b = (MSBS.Region.Event)a.DeepCopy();
                b.EntityID = -1;
                b.Name = $"Generator Point #{i} {genId} {id}";
                gen2.SpawnRegionNames[i] = b.Name;
                regions.Add(b);
                msb.Regions.Add(b);
            }
            return (regions, (uint)genId);
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

        public override void CopyAssociations(MSBS fromMsb, EnemyData ownerSource, EnemyData helperSource, MSBS toMsb, MSBS.Part.Enemy ownerPart, MSBS.Part.Enemy helperPart)
        {
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

        public override NpcEdits InitialNpcPass(EnemyState state, RandomizerOptions opt)
        {
            GameData game = Game;
            ParamDictionary Params = game.Params;
            EnemyPermutation perm = state.Permutation;
            NpcEdits npcs = new(game, state);
            Enemies enemies = state.Enemies;
            Dictionary<uint, EnemyData> defaultData = enemies.DefaultData;
            Dictionary<uint, EnemyInfo> infos = enemies.Infos;

            foreach (PARAM.Row row in Params["NpcParam"].Rows)
            {
                byte teamType = (byte)row["teamType"].Value;
                if (teamType == 24 || teamType == 29)
                {
                    npcs.NpcOriginalTeam[row.ID] = teamType;
                    row["teamType"].Value = (byte)6;
                }
            }
            // Remove butterfly invisibility (3509210)
            Params["NpcParam"][50900001]["spEffectID25"].Value = -1;

            // Start Owl 2 with his sword out
            PARAM.Row owl2Npc = Params["NpcParam"][50601010];
            owl2Npc["modelDispMask2"].Value = (byte)1;
            owl2Npc["modelDispMask3"].Value = (byte)0;
            owl2Npc["modelDispMask4"].Value = (byte)1;

            // Mark Old Dragons so that they can have vanilla behavior in their boss fight
            Params["NpcParam"][53000010]["spEffectID24"].Value = 3530001;
            Params["NpcParam"][53000011]["spEffectID24"].Value = 3530001;
            Params["NpcParam"][53000012]["spEffectID24"].Value = 3530001;

            // Show Divine Dragon almost always. This will make it appear early in the divine realm as well.
            // Ideally this should check if Digine Dragon specifically is randomized first, maybe in post pass.
            if (perm.AnyRandomized)
            {
                Params["NpcParam"][52000000]["modelDispMask10"].Value = (byte)1;
            }

            return npcs;
        }

        public override void FinalNpcPass(EnemyState state, RandomizerOptions opt)
        {
            GameData game = Game;
            ParamDictionary Params = game.Params;
            Enemies enemies = state.Enemies;
            EnemyMapping mapping = state.Mapping;

            // An edit for roberto
            ESD warrior = game.Talk["m20_00_00_00"]["t200500"];
            List<ESD.State> mainloop = warrior.StateGroups[0x7FFFFFFF - 5].Values.ToList();
            foreach (ESD.State esdState in mainloop)
            {
                foreach (ESD.Condition cond in esdState.Conditions)
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
            if (!opt["scale"] && mapping.GetTargets(2500570, out List<uint> sakuraTarget) && enemies[sakuraTarget[0]].HasTag("early"))
            {
                Params["NpcParam"][13800000]["Hp"].Value = 2232;
            }

            // Can look into adding purple speffect/material for corrupted monk, if we can find it

            // Make Sword Saint Isshin deathblow not require Mortal Blade
            Params["ThrowKindParam"].Rows.Add(new PARAM.Row(250001, null, Params["ThrowKindParam"].AppliedParamdef));
            GameEditor.CopyRow(Params["ThrowKindParam"][250000], Params["ThrowKindParam"][250001]);
            // Previously Mask4 = 0xFF
            Params["ThrowKindParam"][250001]["checkAction_ActionUnlock_1"].Value = (sbyte)-1;
            Params["ThrowParam"][15400590]["throwKind"].Value = 250001u;
            // And also Divine Dragon
            Params["ThrowParam"][15200090]["throwKind"].Value = 250001u;
            // Make deathblow possible if moved elsewhere
            if (mapping.IsRandomizedSource(2500800))
            {
                Params["ThrowParam"][15200090]["Dist"].Value = (float)16;
                Params["ThrowParam"][15200090]["upperYRange"].Value = (float)20;
                Params["ThrowParam"][15200090]["lowerYRange"].Value = (float)20;
                // Plus remove Divine Dragon blowback behavior as it easily sends the player straight out of bounds, if dragon is moved anywhere
                Params["Bullet"].Rows.RemoveAll(r => r.ID == 52000830);
            }

            // Small convenience: don't disable boss bonfires when we can't always guarantee that boss helpers die.
            // DS3 is a bit more thorough here.
            HashSet<int> noDisableBonfires = new HashSet<int>
            {
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
    }
}
