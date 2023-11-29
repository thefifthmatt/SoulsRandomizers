using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
using YamlDotNet.Serialization;
using SoulsFormats;
using static RandomizerCommon.Messages;

namespace RandomizerCommon
{
    public class EnemyAnnotations
    {
        public List<ClassConfig> Classes = new List<ClassConfig>();
        // Enemies which can be referenced by name in presets
        public List<EnemyCategory> Categories = new List<EnemyCategory>();
        // Enemies to exclude from Oops All UI because they don't work very well
        public List<string> Singletons { get; set; }
        public List<EnemyGroup> Groups = new List<EnemyGroup>();
        public List<PassiveAdjustment> PassiveAdjustments { get; set; }
        // Generic info per model type, used for generating actual config
        public List<EnemyInfo> Defaults = new List<EnemyInfo>();
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
            // Entire classes included here, for config-based EnemyClassGroup definitions
            public List<EnemyClass> Classes { get; set; }
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
            // Map and name of a duplicate version, as in Elden Ring map events.
            // (all other maps are copied over during write)
            public string DupeMap { get; set; }
            public string DupePartName { get; set; }
            // If there is no emevd for the map, a neighboring one which can be used instead
            public string NeighborMap { get; set; }
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
            // Manual note
            public string Comment { get; set; }
            // The category
            public EnemyClass Class { get; set; }
            // For helpers, always travel with the main enemy. For non-ER bosses, means a first separate phase. 
            public int OwnedBy { get; set; }
            // For ER first-phase bosses, the second phase.
            public int NextPhase { get; set; }
            // An enemy who will probably be aggro'd when a boss or miniboss is aggro'd.
            // This can be configured in Sekiro presets to avoid making powerful enemies into minions.
            public int Add { get; set; }
            // Info for bosses and minibosses
            public int DefeatEntity { get; set; }
            public int DefeatFlag { get; set; }
            public int AppearFlag { get; set; }
            public int StartFlag { get; set; }
            public int EncounterFlag { get; set; }
            public int MusicFlag { get; set; }
            public int Phases { get; set; }
            public int HealthbarIndex { get; set; }
            // <eventid> in <map>
            public string StartEvent { get; set; }
            public string EndEvent { get; set; }
            // ESD containing line said on death, which should be delayed until DefeatFlag is set (using machine 1103)
            public int DeathLine { get; set; }
            // Cosplay
            public string Outfit { get; set; }
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
            // In Elden Ring, group used in a boss encounter to keep spirit summons around
            public int BuddyGroup { get; set; }
            // If this is a derived randomized entity, the base non-randomized entity to copy it from. The derived entity is never created
            // if it's *not* randomized. This is for cases where something of an entity's behavior should be left behind when it's moved.
            public int SplitFrom { get; set; }
            // Filled automatically in the case of dupes, matches SplitFrom when tag "dupe" is present
            public int DupeFrom { get; set; }
            public int DupeIndex { get; set; }
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
            // Mapping from other part names to an even more custom combined name
            public Dictionary<string, string> CustomName { get; set; }
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
            // Is an enemy which only appears in a boss encounter, with a start flag and fog gate
            [YamlIgnore]
            public bool IsBossTarget { get; set; }
            // Is an enemy where basic enemies should be buffed in its spot
            [YamlIgnore]
            public bool IsBuffSource { get; set; }
            // Is an enemy which should be nerfed as a basic enemy, and given a soul drop.
            // It gets preemptive basic versions and does not have its soul rate randomized.
            [YamlIgnore]
            public bool IsFixedSource { get; set; }
            // Is a proper encounter with an healthbar, defeat flag, and non-respawning enemy
            [YamlIgnore]
            public bool IsImportantTarget { get; set; }
            // Is an encounter where the enemy is expected to smell very far away
            [YamlIgnore]
            public bool HasPerceptiveNose { get; set; }
            // Is expected to have an arena
            [YamlIgnore]
            public bool IsArenaTarget { get; set; }
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

            public EnemyInfo ShallowClone() => (EnemyInfo)MemberwiseClone();
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

            public static Arena FromRegion(IMsbRegion region)
            {
                if (region.Shape is not MSB.Shape.Box box) throw new Exception($"Arena only supported for box regions, not {region} {region.Shape} {region.Name}");
                Arena res = new Arena
                {
                    Pos = region.Position,
                    Rot = region.Rotation,
                    Box = new Vector3(box.Width, box.Height, box.Depth),
                };
                res.RotMatrix = Matrix4x4.CreateFromYawPitchRoll(res.Rot.Y * radConvert, res.Rot.X * radConvert, res.Rot.Z * radConvert);
                Matrix4x4.Invert(res.RotMatrix, out Matrix4x4 inv);
                res.InvRotMatrix = inv;
                return res;
            }

            public bool ContainsXZ(Vector3 point)
            {
                Vector3 local = InverseTransform(point);
                // Is this correct??
                return Math.Abs(local.X) < Box.X / 2 && Math.Abs(local.Z) < Box.Z / 2;
            }

            public bool Contains(Vector3 point)
            {
                Vector3 local = InverseTransform(point);
                return Math.Abs(local.X) < Box.X / 2 && Math.Abs(local.Z) < Box.Z / 2 && local.Y > 0 && local.Y < Box.Y;
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

            public override string ToString() => $"Arena[Pos={Pos}, Rot={Rot}, Box={Box}]";
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

        [Localize]
        public static readonly Dictionary<EnemyClass, Text> ClassNames = new Dictionary<EnemyClass, Text>
        {
            [EnemyClass.Basic] = new Text("Regular Enemies", "EnemyClass_Basic"),
            [EnemyClass.Miniboss] = new Text("World Minibosses", "EnemyClass_Miniboss"),
            [EnemyClass.Boss] = new Text("Major Bosses", "EnemyClass_Boss"),
            [EnemyClass.HostileNPC] = new Text("Hostile Humans", "EnemyClass_HostileNPC"),
            [EnemyClass.Wildlife] = new Text("Passive Wildlife", "EnemyClass_Wildlife"),
            [EnemyClass.Scarab] = new Text("Scarabs", "EnemyClass_Scarab"),
            [EnemyClass.MinorBoss] = new Text("Minor Bosses", "EnemyClass_MinorBoss"),
            [EnemyClass.NightMiniboss] = new Text("Night Minibosses", "EnemyClass_NightMiniboss"),
            [EnemyClass.DragonMiniboss] = new Text("Dragon Minibosses", "EnemyClass_DragonMiniboss"),
            [EnemyClass.Evergaol] = new Text("Evergaol Minibosses", "EnemyClass_Evergaol"),
            [EnemyClass.Spectator] = new Text("Spectators", "EnemyClass_Spectator"),
        };
        // Don't localize this for now, use a different placeholder
        private static readonly Dictionary<EnemyClass, Text> ClassNamesOther = new Dictionary<EnemyClass, Text>
        {
            [EnemyClass.Basic] = new Text("Other Regular Enemies", "EnemyClass_BasicOther"),
            [EnemyClass.Miniboss] = new Text("Other World Minibosses", "EnemyClass_MinibossOther"),
            [EnemyClass.Boss] = new Text("Other Major Bosses", "EnemyClass_BossOther"),
            [EnemyClass.HostileNPC] = new Text("Other Hostile Humans", "EnemyClass_HostileNPCOther"),
            [EnemyClass.Wildlife] = new Text("Other Passive Wildlife", "EnemyClass_WildlifeOther"),
            [EnemyClass.Scarab] = new Text("Other Scarabs", "EnemyClass_ScarabOther"),
            [EnemyClass.MinorBoss] = new Text("Other Minor Bosses", "EnemyClass_MinorBossOther"),
            [EnemyClass.NightMiniboss] = new Text("Other Night Minibosses", "EnemyClass_NightMinibossOther"),
            [EnemyClass.DragonMiniboss] = new Text("Other Dragon Minibosses", "EnemyClass_DragonMinibossOther"),
            [EnemyClass.Evergaol] = new Text("Other Evergaol Minibosses", "EnemyClass_EvergaolOther"),
        };
        [Localize]
        public static readonly Dictionary<EnemyClass, Text> ClassDocs = new Dictionary<EnemyClass, Text>
        {
            [EnemyClass.Basic] = new Text("Enemies not covered by other categories", "EnemyClass_BasicDesc"),
            [EnemyClass.Miniboss] = new Text("Enemies with boss healthbars in the open world", "EnemyClass_MinibossDesc"),
            [EnemyClass.Boss] = new Text("Unescapable boss encounters with achievements or story importance", "EnemyClass_BossDesc"),
            [EnemyClass.HostileNPC] = new Text("Invaders and hostile NPCs without extensive quests", "EnemyClass_HostileNPCDesc"),
            [EnemyClass.MinorBoss] = new Text("Other unescapable boss encounters, including side dungeon bosses", "EnemyClass_MinorBossDesc"),
            [EnemyClass.NightMiniboss] = new Text("Minibosses which only spawn at night", "EnemyClass_NightMinibossDesc"),
            [EnemyClass.Spectator] = new Text("Special configuration for regular enemies who are nearby minibosses. This is meant to prevent unreasonably difficult open world fights.", "EnemyClass_SpectatorDesc"),
        };
        [Localize]
        public static readonly Text AdjustSourceDoc = new Text("Special configuration to reduce the frequency of certain enemies relative to others in the same category. Use the + button to add an adjustment. For example, setting crabs to 50% will cut their chance of appearing by half.", "EnemyClass_AdjustSourceDoc");
        public enum EnemyClass
        {
            // Unspecified
            Default = 0,
            // Do not randomize
            None = 100,
            // To be classified
            TODO = 999,
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
            // DS3 and Elden Ring randomizable categories (???)
            CrystalLizard = 12,
            HostileNPC = 13,
            // For enemy duplication, where mapping is manual
            DupeOnly = 14,
            // Elden Ring randomizable categories. (tags: needspace - for AOE, for fair spacing, for sky attacks)
            Wildlife = 15,
            Scarab = 16,
            // A minor boss is distinguished from a miniboss in being set up like a Boss (segments, healthbar)
            // but just in a different category.
            MinorBoss = 17,
            NightMiniboss = 18,
            DragonMiniboss = 19,
            Evergaol = 20,
            CaravanTroll = 21,
            // Special category used for "Add"
            Spectator = 22,
        }

        public class ClassConfig
        {
            public EnemyClass Class { get; set; }
            // Parent class to optionally merge into
            public EnemyClass Parent { get; set; }
            // Other possible parents
            public List<EnemyClass> AltParent { get; set; }
            // If true, cannot randomize among self, only merge into parent (e.g. scarabs)
            // This is redundant with randomizedTypes currently.
            public bool NoSelfRandom { get; set; }
            // If true, the default behavior is to inherit the parent's pool if none is specified.
            public bool DefaultInherit { get; set; }
            // If true, cannot merge into a parent or get merged into, or be used for Oops All.
            // Should not have a parent in this case.
            // This is mainly for hostile NPCs, but maybe they can go to other categories in the future
            public bool NoMerge { get; set; }
        }

        // Meta categories? Maybe have an All Bosses thing, and All Enemies, and Dragons
        // Buttons: Separate all categories / Merge boss categories / Merge all categories
        [Localize]
        public static readonly Dictionary<EnemyClassGroup, Text> ClassGroupNames = new Dictionary<EnemyClassGroup, Text>
        {
            [EnemyClassGroup.AllEnemies] = new Text("All Enemies and Bosses", "EnemyClassGroup_AllEnemies"),
            [EnemyClassGroup.AllDragons] = new Text("Dragons", "EnemyClassGroup_AllDragons"),
            [EnemyClassGroup.AllBosses] = new Text("All Bosses", "EnemyClassGroup_AllBosses"),
        };
        public enum EnemyClassGroup
        {
            Default = 0,
            AllEnemies = 1,
            AllDragons = 2,
            AllBosses = 3,
        }

        public class PassiveAdjustment
        {
            public string Source { get; set; }
            public int Percent { get; set; }
        }

        public class EnemyData
        {
            // Do not get transplanted
            public string MainMap { get; set; }
            public string DupeMap { get; set; }
            public List<string> Maps => DupeMap == null ? new List<string> { MainMap } : new List<string> { MainMap, DupeMap };
            public string Name { get; set; }
            public int ID { get; set; }
            // Gets transplanted
            public string Model { get; set; }
            public int NPC { get; set; }
            public int Think { get; set; }
            public int Char { get; set; }  // DS3 only
            public int Anim { get; set; }  // Elden Ring only
            // Info for placement
            public List<int> Group { get; set; }
            public string Col { get; set; }
        }
    }
}
