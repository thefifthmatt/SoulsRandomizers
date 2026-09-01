using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class DS3EnemyEditor : EnemyEditor<MSB3, MSB3.Part.Enemy, MSB3.Part.Object, MSB3.Region, MSB3.Event.Generator>
    {
        public override List<MSB3.Part.Enemy> GetEnemies(MSB3 msb) => msb.Parts.Enemies;
        public override void SetEntityID(MSB3.Part.Enemy e, uint id) => e.EntityID = (int)id;
        public override MSB3.Part.Enemy GetEnemy(MSB3 msb, uint id) => msb.Parts.Enemies.Find(e => e.EntityID == id);
        public override MSB3.Region GetRegion(MSB3 msb, uint id) => msb.Regions.GetEntries().Find(e => e.EntityID == id);

        public override EnemyData GetEnemyData(MSB3.Part.Enemy e, string map)
        {
            return new EnemyData
            {
                MainMap = map,
                Name = e.Name,
                ID = (uint)e.EntityID,
                Group = e.EntityGroups.Where(g => g > 0).Select(g => (uint)g).ToList(),
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

        public override MSB3.Part.Enemy TransplantEnemy(MSB3 msb, EnemyData source, uint target)
        {
            MSB3.Part.Enemy e = msb.Parts.Enemies.Find(en => en.EntityID == target);
            e.ModelName = source.Model;
            e.NPCParamID = source.NPC;
            e.ThinkParamID = source.Think;
            e.CharaInitID = source.Char;
            return e;
        }

        public override void SetNpcParam(MSB3.Part.Enemy e, int? npcParam = null, int? npcThinkParam = null)
        {
            if (npcParam is int npc) e.NPCParamID = npc;
            if (npcThinkParam is int think) e.ThinkParamID = think;
        }

        public override MSB3.Part.Enemy CloneEnemy(MSB3 msb, MSB3.Part.Enemy sourcePart, EnemyData source, uint newTarget, int newPartId)
        {
            MSB3.Part.Enemy e2 = (MSB3.Part.Enemy)sourcePart.DeepCopy();
            e2.ModelName = source.Model;
            e2.NPCParamID = source.NPC;
            e2.ThinkParamID = source.Think;
            e2.CharaInitID = source.Char;
            e2.EntityID = (int)newTarget;
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

        public override void ClearGroups(MSB3.Part.Enemy e, ICollection<uint> preserveGroups = null)
        {
            for (int i = 0; i < e.EntityGroups.Length; i++) e.EntityGroups[i] = -1;
        }

        public override void CopyGroups(EnemyData source, Dictionary<uint, uint> groupMapping, MSB3.Part.Enemy target, uint removeTarget = 0)
        {
            CopyGroupsInternal(source, groupMapping, target.EntityGroups, removeTarget);
        }

        public override void RemoveEnemies(MSB3 msb, Predicate<uint> removePredicate)
        {
            msb.Parts.Enemies = msb.Parts.Enemies.Where(p => !removePredicate((uint)p.EntityID)).ToList();
        }

        public override (MSB3.Part.Object, uint, uint) MakeCrossMapObjectCopy(
            Func<uint> newEntity, string map, string toMap, string objId, EnemyData targetData, int newPartId)
        {
            MSB3 sourceMsb = Game.DS3Maps[map];
            MSB3 targetMsb = Game.DS3Maps[toMap];
            MSB3.Part.Object o = sourceMsb.Parts.Objects.Find(o => o.Name == objId);
            if (o == null) return default;
            MSB3.Part.Object e2 = (MSB3.Part.Object)o.DeepCopy();
            if (o.EntityID > 0)
            {
                e2.EntityID = (int)newEntity();
            }
            e2.Name = $"{e2.ModelName}_{newPartId:d4}";
            if (e2.CollisionName != null)
            {
                e2.CollisionName = targetData.Col;
            }
            for (int i = 0; i < e2.EntityGroups.Length; i++) e2.EntityGroups[i] = -1;
            targetMsb.Parts.Objects.Add(e2);
            return (e2, o.EntityID > 0 ? (uint)o.EntityID : 0, e2.EntityID > 0 ? (uint)e2.EntityID : 0);
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

        public override GeneratorData GetCrossMapGeneratorData()
        {
            Dictionary<uint, (MSB3.Event.Generator, List<uint>)> generators = new();
            foreach (KeyValuePair<string, MSB3> entry in Game.DS3Maps)
            {
                MSB3 msb = entry.Value;
                if (msb.Events.Generators.Count == 0) continue;
                Dictionary<string, int> mapEnemies = msb.Parts.Enemies.Where(e => e.EntityID > 0).ToDictionary(e => e.Name, e => e.EntityID);
                foreach (MSB3.Event.Generator gen in msb.Events.Generators)
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

        public override (MSB3.Region, List<MSB3.Region>, uint) MakeCrossMapRegionCopy(
            Func<uint> newEntity, string map, string toMap, uint id, bool replace)
        {
            MSB3.Region a = Game.DS3Maps[map].Regions.GetEntries().Find(r => r.EntityID == id);
            if (a == null) throw new Exception($"Internal error: can't find region {id} in {map}");
            MSB3.Region b = a.DeepCopy();
            MSB3 msb = Game.DS3Maps[toMap];
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
                b.EntityID = (int)newEntity();
            }
            b.Name = $"Region {b.EntityID} from {id}";
            return (a, new List<MSB3.Region> { b }, (uint)b.EntityID);
        }

        public override (List<MSB3.Region>, uint) MakeCrossMapGeneratorCopy(
            Func<uint> newEntity, string map, string toMap, uint id,
            GeneratorData generators, RelocMap reloc)
        {
            MSB3 fromMsb = Game.DS3Maps[map];
            MSB3 msb = Game.DS3Maps[toMap];
            if (!generators.Data.ContainsKey(id)) throw new Exception($"Did not find generator {id}");
            (MSB3.Event.Generator gen, List<uint> enemyIds) = generators.Data[id];
            if (enemyIds.Count == 0) throw new Exception($"Generator {id} has no enemies associated with it");

            MSB3.Event.Generator gen2 = (MSB3.Event.Generator)gen.DeepCopy();
            Array.Clear(gen2.SpawnPartNames, 0, gen2.SpawnPartNames.Length);
            Array.Clear(gen2.SpawnPointNames, 0, gen2.SpawnPointNames.Length);
            gen2.PartName = gen2.PointName = null;

            int enemyIndex = 0;
            foreach (uint fromEnemy in enemyIds)
            {
                if (!reloc.TryGetValue(EventValue.Enemy(fromEnemy), out EventValue target))
                {
                    throw new Exception($"No target found for {fromEnemy} from generator {id}");
                }
                int targetId = target.IntID;
                MSB3.Part.Enemy enemy = msb.Parts.Enemies.Find(e => e.EntityID == targetId);
                gen2.SpawnPartNames[enemyIndex++] = enemy.Name;
            }
            // When does this happen?
            if (enemyIndex == 0) return (null, 0);

            int genId = (int)newEntity();
            gen2.EntityID = genId;
            gen2.Name = $"Generator {genId} {id}";
            msb.Events.Generators.Add(gen2);

            List<MSB3.Region> regions = new List<MSB3.Region>();
            for (int i = 0; i < gen.SpawnPointNames.Length; i++)
            {
                string regionName = gen.SpawnPointNames[i];
                if (regionName == null)
                {
                    regions.Add(null);
                    continue;
                }
                MSB3.Region.Event a = fromMsb.Regions.Events.Find(r => r.Name == regionName);
                MSB3.Region.Event b = (MSB3.Region.Event)a.DeepCopy();
                b.EntityID = -1;
                b.Name = $"Generator Point #{i} {genId} {id}";
                gen2.SpawnPointNames[i] = b.Name;
                regions.Add(b);
                msb.Regions.Add(b);
            }
            return (regions, (uint)genId);
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

        public override void CopyAssociations(MSB3 fromMsb, EnemyData ownerSource, EnemyData helperSource, MSB3 toMsb, MSB3.Part.Enemy ownerPart, MSB3.Part.Enemy helperPart)
        {
        }

        public override void InitialMapPass(RandomizerOptions opt)
        {
            var getMap = Game.DS3Maps.TryGetValue;
            if (getMap("m30_01_00_00", out MSB3 cathedral))
            {
                // Nerf generators
                HashSet<int> corpseGenerators = new() { 3503400, 3503401, 3503402, 3503403, 3503404, 3503405, 3503406, 3503407, 3503408, 3503409, 3503410, 3503411 };
                foreach (MSB3.Event.Generator gen in cathedral.Events.Generators)
                {
                    // Default interval is 0, but c1220 respawn animation 20004 takes 6.5 seconds
                    // Probably doesn't need to be as high as Elden Ring since there's no ladder, just need to get past enemies
                    if (corpseGenerators.Contains(gen.EntityID) && gen.MinInterval < 5)
                    {
                        gen.MinInterval = 10;
                        gen.MaxInterval = 15;
                    }
                }
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

            // Poison, Toxic, ignore Blood and Curse (lava seems to be managed by event script)
            npcs.ResistSps.AddRange(new[] { 30000, 30010 });
            // DS3 has a lot more variety in enemy types, and more dependency on this for specific boss fights.
            // So do it only for non-human NPCs (>=100000), for Enemy StrongEnemy Enemy2 ArchEnemyTeam (6 7 24 33), for Basic randomized
            List<EnemyClass> teamClasses = new List<EnemyClass> { EnemyClass.Basic, EnemyClass.Miniboss, EnemyClass.HostileNPC };
            HashSet<int> transferTeams = new HashSet<int>(
                infos.Values.Where(i => teamClasses.Contains(i.Class)).Select(i => defaultData[i.ID].NPC));
            foreach (PARAM.Row row in Params["NpcParam"].Rows)
            {
                byte teamType = (byte)row["teamType"].Value;
                if (transferTeams.Contains(row.ID) && (teamType == 7 || teamType == 24 || teamType == 33))
                {
                    npcs.NpcOriginalTeam[row.ID] = teamType;
                    row["teamType"].Value = (byte)6;
                }

                // Resists for Poison Toxic Blood Curse are only in 6 7 8 9 respectively
                for (int i = 6; i <= 7; i++)
                {
                    int sp = (int)row[$"spEffectId{i}"].Value;
                    if (npcs.ResistSps.Contains(sp))
                    {
                        AddMulti(npcs.NpcCopySpEffect, row.ID, sp);
                    }
                }

                // Non-boss enemies shouldn't have radius >2.5. This is mainly the giants, and a few are 2.8.
                // This must be like 1 for everyone to fit everywhere.
                // Regular miniboss height is limited by height of barracks basement, which is around 5.5.
                // The Profaned Dungeon hallways are like height 3-4 though and firelink chest is 4,
                // accounted for with 'exclude:large' label.
                npcs.ShrinkNpc(row, 2.5f, 5.1f);
            }
            // Handle NPC drops which shouldn't travel with the NPC. This should be independent of item randomizer.
            // Always do it for all eligible enemies so that restart isn't required between randomizations
            foreach (EnemyInfo info in infos.Values.Where(i => i.HasTag("npcitem")))
            {
                int npc = defaultData[info.ID].NPC;
                if (npcs.NpcItemLots.ContainsKey(npc)) continue;
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
                    npcs.NpcItemLots[npc] = (baseItemLot, flags.Count > 0 ? flags.Max() : info.DefeatFlag);
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
                row["disableInitializeDead"].Value = (byte)0;
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

            // Could include invaders here, but only Ringed City ones are enabled, and are pretty short range anyway
            // Also include None for ballista
            // Silly search syntax: proprange nose_dist 50 10000000000
            HashSet<int> bossThinks = new(
                infos.Values.Where(i => i.Class == EnemyClass.Boss || i.Class == EnemyClass.Helper || i.Class == EnemyClass.None).Select(i => defaultData[i.ID].Think));
            foreach (PARAM.Row row in Params["NpcThinkParam"].Rows)
            {
                if (!bossThinks.Contains(row.ID))
                {
                    // Skeletons need an extra nerf
                    if (row.ID / 1000 == 107 && perm.AnyRandomized)
                    {
                        // Some have 100 nose_dist, 40 eye_dist. Others have 0 nose_dist, 24 or 40 eye_dist. All have 100 maxBackhomeDist
                        // As a result, eliminate nose_dist specifically as it's very map-dependent
                        row["nose_dist"].Value = (ushort)0;
                    }
                    else
                    {
                        npcs.ShrinkRange(row, 50, 200);
                    }
                }
            }

            // Dragonslayer Armour, prevent Vordt cutscene with script interaction
            Params["NpcParam"][316000]["spEffectId21"].Value = 6968;

            // Don't skip first phase of multi-boss fight on quitout
            Params["NpcParam"][132000]["disableInitializeDead"].Value = (byte)1;  // Crystal Sage
            Params["NpcParam"][209000]["disableInitializeDead"].Value = (byte)1;  // Oceiros
            Params["NpcParam"][224000]["disableInitializeDead"].Value = (byte)1;  // Vordt
            Params["NpcParam"][526000]["disableInitializeDead"].Value = (byte)1;  // Yhorm

            // Make Wyvern suck less (starting HP is 6000 with very high resistance)
            Params["NpcParam"][314100]["hp"].Value = 300;

            // Gael 1, 620100 has 1031 base HP
            // Gael 2, 620000 has 2776 base HP
            // Put Gael 2 at 65% HP, matching second phase
            Params["NpcParam"][620000]["hp"].Value = (int)(2776 * 0.65);

            // Make guaranteed DLC invaders not invasion-color. Maybe should depend on randomization, if that's optional
            Params["NpcParam"][21200]["PhantomParamId"].Value = -1;
            Params["NpcParam"][22140]["PhantomParamId"].Value = -1;
            Params["NpcParam"][22240]["PhantomParamId"].Value = -1;
            Params["NpcParam"][22340]["PhantomParamId"].Value = -1;

            return npcs;
        }

        public override void FinalNpcPass(EnemyState state, RandomizerOptions opt)
        {
            GameData game = Game;
            ParamDictionary Params = game.Params;
            EnemyMapping mapping = state.Mapping;

            if (mapping.IsRandomizedSource(3200800))
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
            lightSp["vfxId"].Value = lightVfx.ID;

            // In case Giant AI breaks and crawls up, don't do 90% damage cut (12340)
            // Also, don't allow crab to heal (12044) ;_;
            Params["SpEffectParam"].Rows.RemoveAll(row => row.ID == 12340 || row.ID == 12044);

            // Make same Hollow Manservant edit as Fog Gate Randomizer
            PARAM.Row hollow = Params["ActionButtonParam"][3100000];
            hollow["regionType"].Value = (byte)0;
            hollow["radius"].Value = 5f;
            hollow["angle"].Value = 180;
            hollow["depth"].Value = 0f;
            hollow["width"].Value = 0f;
            hollow["height"].Value = 15f;
            hollow["baseHeightOffset"].Value = -10f;
            hollow["dummyPoly1"].Value = -1;
            hollow["angleCheckType"].Value = (byte)0;
            hollow["allowAngle"].Value = 180;

            // Use 5020 (logic speffects) as a base for various set/check statuses, at 6950.
            // This can go until 6980 but cap it until 6969.
            PARAM.Row baseSp = Params["SpEffectParam"][5020];
            for (int i = 0; i < 20; i++)
            {
                GameEditor.CopyRow(baseSp, game.AddRow("SpEffectParam", 6950 + i));
            }
        }
    }
}
