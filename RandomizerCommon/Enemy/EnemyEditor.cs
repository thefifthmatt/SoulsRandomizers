using System;
using System.Collections.Generic;
using System.Linq;
using SoulsFormats;
using static SoulsIds.Events;
using static RandomizerCommon.EnemyAnnotations;

namespace RandomizerCommon
{
    // Abuse of C# lightly using SoulsFormats interfaces, which are not really sufficient for randomizer data needs.
    // This was previously stateless but now takes GameData to avoid passing whole-game state. Methods using GameData are named "CrossMap" by convention.
    // Some of this boilerplate can be alleviated by custom MSB generation.
    public abstract class EnemyEditor<TMap, TEnemy, TObject, TRegion, TGenerator>
        where TMap : IMsb where TEnemy : IMsbPart where TObject : IMsbPart where TRegion : IMsbRegion where TGenerator : IMsbEvent
    {
        public GameData Game { get; set; }

        // Generic enemy list access
        public abstract List<TEnemy> GetEnemies(TMap msb);
        // Fills in enemy data
        public abstract EnemyData GetEnemyData(TEnemy e, string map);
        // Get enemy by entity id (doable with GetEnemies + GetEnemyData - TODO add interface for EntityID?)
        public abstract TEnemy GetEnemy(TMap msb, uint id);
        // Remove some enemies by entity id
        public abstract void RemoveEnemies(TMap msb, Predicate<uint> filter);

        // TODO: Combine these
        // Initially populating entity id
        public abstract void SetEntityID(TEnemy e, uint id);
        // Override NPC param in particular
        // TODO: Should probably be separate
        public abstract void SetNpcParam(TEnemy e, int? npcParam = null, int? npcThinkParam = null);
        // Set enemy collision, which usually becomes invalid across maps
        public abstract void SetEnemyCollision(TEnemy enemy, string colName);

        // Move one enemy to another
        public abstract TEnemy TransplantEnemy(TMap msb, EnemyData source, uint target);
        // Clone one enemy to a new location
        public abstract TEnemy CloneEnemy(TMap msb, TEnemy sourcePart, EnemyData source, uint newTarget, int newPartId);
        public abstract void CopyAssociations(
            TMap fromMsb, EnemyData ownerSource, EnemyData helperSource,
            TMap toMsb, TEnemy ownerPart, TEnemy helperPart);
        // Clear entity groups for an enemy
        public abstract void ClearGroups(TEnemy e, ICollection<uint> preserveGroups = null);
        // Copy group data from a source enemy into its transplant
        public abstract void CopyGroups(EnemyData source, Dictionary<uint, uint> groupMapping, TEnemy target, uint removeTarget = 0);

        // Copy object from one location to a different one (keeps original object the same), returning old and new entity id
        // objId is entity id for DS1 and Sekiro, part name for DS3
        public abstract (TObject, uint, uint) MakeCrossMapObjectCopy(
            Func<uint> newEntity, string map, string toMap, string objId, EnemyData targetData, int newPartId);

        // Get region by entity id (currently a lot of allocation post-DS1)
        public abstract TRegion GetRegion(TMap msb, uint id);
        // Copy a region from one location to a different one
        public abstract (TRegion, List<TRegion>, uint) MakeCrossMapRegionCopy(
            Func<uint> newEntity, string map, string toMap, uint id, bool replace);
        // Add a standalone region based on arena data
        public abstract TRegion MakeArenaRegion(TMap msb, Arena arena, string name);

        // Collect all generator data before they are deleted due to stale enemy references
        public abstract GeneratorData GetCrossMapGeneratorData();
        // Copy a generator from one location to a different one
        public abstract (List<TRegion>, uint) MakeCrossMapGeneratorCopy(
            Func<uint> newEntity, string map, string toMap, uint id,
            GeneratorData generators, RelocMap reloc);

        public struct GeneratorData
        {
            // Map from generator id to (instance, source entity ids)
            internal Dictionary<uint, (TGenerator, List<uint>)> Data { get; set; }
        }

        // Keeping track of model declarations
        public abstract ModelEditor GetModelEditor(TMap msb);
        // Delete metadata that references unused enemies and objects
        public abstract void GarbageCollect(TMap msb, HashSet<string> usedEnemies = null, HashSet<string> removedObjects = null);

        // Highly game-specific passes
        public virtual void InitialMapPass(RandomizerOptions opt) { }
        public abstract NpcEdits InitialNpcPass(EnemyState state, RandomizerOptions opt);
        public virtual void FinalNpcPass(EnemyState state, RandomizerOptions opt) { }

        // Common logic
        protected static void CopyGroupsInternal(EnemyData source, Dictionary<uint, uint> groupMapping, int[] targetGroups, uint removeTarget)
        {
            // Weird adapter
            uint[] groups = targetGroups.Select(g => g == -1 ? 0 : (uint)g).ToArray();
            CopyGroupsInternal(source, groupMapping, groups, removeTarget);
            for (int i = 0; i < targetGroups.Length; i++)
            {
                targetGroups[i] = groups[i] == 0 ? -1 : (int)groups[i];
            }
        }

        protected static void CopyGroupsInternal(EnemyData source, Dictionary<uint, uint> groupMapping, uint[] targetGroups, uint removeTarget)
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
                    targetGroups[removeIndex] = 0;
                }
            }
            List<uint> addGroups = new();
            foreach (uint sourceGroup in source.Group.Concat(new uint[] { 0 }))
            {
                if (groupMapping.TryGetValue(sourceGroup, out uint g2))
                {
                    addGroups.Add(g2);
                }
            }
            foreach (uint groupToAdd in addGroups)
            {
                if (targetGroups.Contains(groupToAdd)) continue;
                int groupIndex = Array.IndexOf<uint>(targetGroups, 0);
                if (groupIndex == -1)
                {
                    // TODO: Pick hardcoded victims from ERR?
                    throw new Exception($"Ran out of group slots for {source.ID}. Want to add {string.Join(",", addGroups)} but stuck at {string.Join(",", targetGroups)}");
                }
                targetGroups[groupIndex] = groupToAdd;
            }
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
}
