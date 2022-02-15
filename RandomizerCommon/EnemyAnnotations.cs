using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
using YamlDotNet.Serialization;

namespace RandomizerCommon
{
    public class EnemyAnnotations
    {
        // YAML fields
        // Enemies which can be referenced by name in presets
        public List<EnemyCategory> Categories = new List<EnemyCategory>();
        // Enemies to exclude from Oops All UI because they don't work very well
        public List<string> Singletons { get; set; }
        public List<EnemyGroup> Groups = new List<EnemyGroup>();
        public List<EnemyInfo> Enemies = new List<EnemyInfo>();
        public List<ObjectInfo> Objects = new List<ObjectInfo>();
        public List<ChestInfo> Chests = new List<ChestInfo>();
        public Dictionary<int, int> ScalingSections = new Dictionary<int, int>();

        public SortedSet<int> GetGroup(string name)
        {
            EnemyGroup group = Groups.Find(g => g.Name == name);
            if (group == null) return new SortedSet<int>();
            return group.Entities;
        }

        public class EnemyCategory
        {
            // The main display name, which should match the model name or ExtraNames of enemies in the config
            public string Name { get; set; }
            // Whether the name is hidden because it's too granular
            public bool Hidden { get; set; }
            // The list of subcategory display names, when they are a partition of the enemies
            public List<string> Partition { get; set; }
            // The list of subcategory display names, when it is an unstructured subset of all of them
            public List<string> Partial { get; set; }
            // The list of names of individual instances of this enemy, usually for minibosses
            public List<string> Instance { get; set; }
            // When this category is defined as the union of two other categories, those categories
            public List<string> Contains { get; set; }
        }

        public class EnemyGroup
        {
            public string Name { get; set; }
            public List<string> Models { get; set; }
            [YamlIgnore]
            public SortedSet<int> Entities = new SortedSet<int>();
        }

        public class EnemyInfo
        {
            // Unique entity id
            public int ID { get; set; }
            // Internal map id
            public string Map { get; set; }
            // Part name, unique within map
            public string Name { get; set; }
            // Metadata about models which is useful for placement logic
            [YamlIgnore]
            public string ModelID { get; set; }
            [YamlIgnore]
            public string ModelName { get; set; }
            // Debug info with full name and part references etc
            public string DebugText { get; set; }
            // Debug info about ESDs
            public string ESDs { get; set; }
            // Debug info about emevd commands used
            public string Events { get; set; }
            // The category
            public EnemyClass Class { get; set; }
            // For bosses, means a first separate phase. For helpers, always travel with the main enemy.
            public int OwnedBy { get; set; }
            // An enemy who will probably be aggro'd when a boss or miniboss is aggro'd.
            // This can be configured in Sekiro presets to avoid making powerful enemies into minions.
            public int Add { get; set; }
            // Info for bosses and minibosses
            public int DefeatFlag { get; set; }
            public int AppearFlag { get; set; }
            public int StartFlag { get; set; }
            public int MusicFlag { get; set; }
            public int Phases { get; set; }
            // Tags and metadata
            public string Tags { get; set; }
            // Category for presets
            public string Category { get; set; }
            // Name of the enemy used in item randomizer, and also to identify some bosses and minibosses
            public string ItemName { get; set; }
            // Event groups used by event scripts which should be part of multichr
            public List<int> Groups { get; set; }
            // Group to remove the entity from if it gets randomized to something else
            // Mainly for handling multi-phase bosses where one phase is randomized (stays in the group) and the other isn't
            public int RemoveGroup { get; set; }
            // If this is a derived randomized entity, the base non-randomized entity to copy it from. The derived entity is never created
            // if it's *not* randomized. This is for cases where something of an entity's behavior should be left behind when it's moved.
            public int SplitFrom { get; set; }
            // If this otherwise non-randomized enemy can be duplicated, events to duplicate
            // If this is defined, other event templates can be used in dupe mode as well.
            public List<int> DupeEvents { get; set; }
            // Change the enemy's collision to the given name
            public string SetColName { get; set; }
            // Full name for bosses and minibosses when there is ambiguity, for spoiler log and presets
            public string ExtraName { get; set; }
            // Name template for name mashups
            public string FullName { get; set; }
            // Specific name for name mashups
            public string PartName { get; set; }
            // Fun name for dupes
            public string DupeName { get; set; }
            // Npc name id for non-mashup substitutions
            public int NpcName { get; set; }
            // Category for minibosses, to avoid them replacing each other (Arena is also used for this, for bosses)
            public string EnemyType { get; set; }
            // Same as in event templates, except it will remove the original region. Meant for Lua hardcoded regions (avoid editing the scripts)
            public List<string> Regions { get; set; }
            // The region(s) for bosses and minibosses. Everything should be in-bounds.
            public string Arena { get; set; }
            // Areas within an area which are invalid teleport locations. Also used for Twin Princes
            public string ArenaExclude { get; set; }
            // Misc arenas used for custom specific placements. 'primary' will change the entity's position to that spot.
            // Others can be used as needed, like 'dupe' for dupe placements.
            // 'greatwood' and 'generators' are defined (for p2 greatwood changes and manual generator placements)
            // but not implemented.
            public Dictionary<string, string> ExtraArenas { get; set; }
            // A point (in region syntax, for code reuse) of where to relocate the boss to if Divine Dragon can go here.
            public string DragonArena { get; set; }
            // The Divine Dragon tree dragons which are supported in this area. Should be defined if DragonArena is also defined, for dragon to be placed here.
            public string DragonTrees { get; set; }
            // Predefined game region which is guaranteed to cover the entire boss's range, usually a BGM region
            public int ArenaRegion { get; set; }
            [YamlIgnore]
            public Arena ArenaData { get; set; }
            [YamlIgnore]
            public List<Arena> ArenaExcludeData { get; set; }
            [YamlIgnore]
            public Dictionary<string, List<Arena>> ExtraArenaData { get; set; }
            public bool TryGetArena(string name, out Arena arena)
            {
                arena = null;
                if (ExtraArenaData != null && ExtraArenaData.TryGetValue(name, out List<Arena> arenas) && arenas.Count > 0)
                {
                    arena = arenas[0];
                    return true;
                }
                return false;
            }
            [YamlIgnore]
            public Arena DragonArenaData { get; set; }
            [YamlIgnore]
            public List<int> DragonTreeList { get; set; }
            [YamlIgnore]
            public HashSet<string> TagSet = new HashSet<string>();
            public bool HasTag(string tag)
            {
                return TagSet.Contains(tag);
            }
            // Some convenience fields initialized dynamically
            // Is an enemy which only appears in a boss encounter
            [YamlIgnore]
            public bool IsBossTarget { get; set; }
            // Is a proper encounter with an arena and non-respawning enemy
            [YamlIgnore]
            public bool IsImportantTarget { get; set; }
            // Should immortality for this enemy always be on by default, even outside a boss arena?
            [YamlIgnore]
            public bool IsImmortal { get; set; }
            // Should immortality for this enemy always be removed, even inside a boss arena?
            // (only applies to Sekiro, since immortality is never applied by default otherwise)
            [YamlIgnore]
            public bool IsMortalSekiroBoss { get; set; }
            // Adjust phases for heuristics
            [YamlIgnore]
            public int SourcePhases => (Phases < 1 ? 1 : Phases) + (HasTag("buffphase") ? 1 : 0);
            [YamlIgnore]
            public int TargetPhases => (Phases < 1 ? 1 : Phases) + (HasTag("buffphase") ? 1 : 0) + (HasTag("nerfloc") ? -1 : 0);
        }

        private static readonly float radConvert = (float)Math.PI / 180;
        public class Arena
        {
            public Vector3 Pos { get; set; }
            public Vector3 Rot { get; set; }
            public Vector3 Box { get; set; }
            public Matrix4x4 RotMatrix { get; set; }
            public Matrix4x4 InvRotMatrix { get; set; }
            public static Arena Parse(string arena)
            {
                // This format is highly ad hoc, but it is all the needed numbers
                // Example: -18.27 114.3 558.11 -112 45 10 5 0 6.5
                // <x pos> <y pos> <z pos> <y rot> <x width> <y height> <z depth> <x rot> <z rot>
                // Following fromsoft convention, x and z pos are center of the box, and y pos is bottom of the box.
                List<float> c = arena.Split(' ').Select(w => float.Parse(w, CultureInfo.InvariantCulture)).ToList();
                if (c.Count != 4 && c.Count != 7 && c.Count != 9) throw new Exception($"Badly formatted arena bounds: {arena}");
                Arena res = new Arena
                {
                    Pos = new Vector3(c[0], c[1], c[2]),
                    Rot = c.Count == 9 ? new Vector3(c[7], c[3], c[8]) : new Vector3(0, c[3], 0),
                    Box = c.Count == 4 ? new Vector3() : new Vector3(c[4], c[5], c[6]),
                };
                res.RotMatrix = Matrix4x4.CreateFromYawPitchRoll(res.Rot.Y * radConvert, res.Rot.X * radConvert, res.Rot.Z * radConvert);
                Matrix4x4.Invert(res.RotMatrix, out Matrix4x4 inv);
                res.InvRotMatrix = inv;
                return res;
            }

            public Vector3 Transform(Vector3 point)
            {
                point = Vector3.Transform(point, RotMatrix);
                point = Vector3.Add(point, Pos);
                return point;
            }

            public Vector3 InverseTransform(Vector3 point)
            {
                point = Vector3.Subtract(point, Pos);
                point = Vector3.Transform(point, InvRotMatrix);
                return point;
            }
        }

        public class ObjectInfo
        {
            // int entity id, or part name, in OwnedBy map
            public string ID { get; set; }
            public int OwnedBy { get; set; }
        }

        public class ChestInfo
        {
            // Unique event entity id
            public int ID { get; set; }
            // Internal map id
            public string Map { get; set; }
            // Part name, unique within map
            public string Name { get; set; }
            // Misc information
            public string DebugInfo { get; set; }
            // Base item lot
            public int ItemLot { get; set; }
            // Item get event flag, used for showing mimic or not and awarding the item
            public int EventFlag { get; set; }
            // Mimic to use as a clone source, for scaling.
            public int ClosestMimic { get; set; }
            // Nearby enemy, another possible clone source. Currently unused.
            public int ClosestEnemy { get; set; }
            // Collision name to set
            public string ColName { get; set; }
            // Tags for mimic enemy, should at least include "hidden"
            public string Tags { get; set; }
        }

        public enum EnemyClass
        {
            // Do not randomize
            // TODO this shouldn't be 0, Default should be. 0 is hardcoded in some early comparisons.
            None = 0,
            // Regular enemy. Does not always need to be killable
            Basic = 1,
            // Miniboss. Named enemy, and should be killable.
            Miniboss = 2,
            // Boss. Named enemy, should be killable through EndConds
            Boss = 3,
            // Does not appear on its own, moved with another enemy
            Helper = 4,
            // Like miniboss, but behaves like 'none' if option is off
            Headless = 5,
            // One of the four folding screen monkeys. Other enemies can go here.
            FoldingMonkey = 6,
            // Genichiro in tutorial. Other enemies can go here.
            TutorialBoss = 7,
            // Remove source.
            // (Currently not implemented like this)
            Remove = 8,
            // An enemy which cannot be meaningfully randomized, but there is some spectacle involved in putting it in the world
            // (Also currently unused.)
            ChaosBoss = 9,
            // An old dragon, of which some can be randomized
            OldDragon = 10,
            // DS3 randomizable categories
            Mimic = 11,
            CrystalLizard = 12,
            HostileNPC = 13,
            // For enemy duplication, where mapping is manual
            DupeOnly = 14,
        }

        public class EnemyData
        {
            // Do not get transplanted
            public string Map { get; set; }
            public string Name { get; set; }
            public int ID { get; set; }
            // Gets transplanted
            public string Model { get; set; }
            public int NPC { get; set; }
            public int Think { get; set; }
            public int Char { get; set; }  // DS3 only
            // Info for placement
            public List<int> Group { get; set; }
            public string Col { get; set; }
        }
    }
}
