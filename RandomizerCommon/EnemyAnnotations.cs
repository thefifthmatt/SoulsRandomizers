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
        public List<EnemyCategory> Categories = new List<EnemyCategory>();
        public List<string> Singletons { get; set; }
        public List<string> NoDeathblow { get; set; }
        public List<EnemyInfo> Enemies = new List<EnemyInfo>();

        public class EnemyCategory
        {
            public string Name { get; set; }
            public bool Singleton { get; set; }
            public List<string> Partition { get; set; }
            public List<string> Partial { get; set; }
            public List<string> Instance { get; set; }
        }

        public class EnemyInfo
        {
            // Unique entity id
            public int ID { get; set; }
            // Name etc
            public string DebugText { get; set; }
            // Info about ESDs
            public string ESDs { get; set; }
            // Info about emevd commands used
            public string Events { get; set; }
            // The category
            public EnemyClass Class { get; set; }
            // For bosses, means a first separate phase. For helpers, always travel with the main enemy.
            public int OwnedBy { get; set; }
            // This enemy may get involved with a miniboss, meaning that it should not be a multi-phase enemy probably
            public int Add { get; set; }
            // Info for bosses and minibosses
            public int DefeatFlag { get; set; }
            public int AppearFlag { get; set; }
            public int StartFlag { get; set; }
            public int Phases { get; set; }
            // Tags and metadata
            public string Tags { get; set; }
            // Category for presets
            public string Category { get; set; }
            // Name of the enemy used in item randomizer, and also to identify some bosses and minibosses
            public string ItemName { get; set; }
            // Full name for bosses and minibosses when there is ambiguity, for spoiler log and presets
            public string ExtraName { get; set; }
            // Name template for name mashups
            public string FullName { get; set; }
            // Specific name for name mashups
            public string PartName { get; set; }
            // Category for minibosses, to avoid them replacing each other
            public string EnemyType { get; set; }
            // Same as in event templates, except it will remove the original region. Meant for Lua hardcoded regions (avoid editing the scripts)
            public List<string> Regions { get; set; }
            // The region(s) for bosses and minibosses. Everything should be in-bounds.
            public string Arena { get; set; }
            // Experiment for making Owl 2 owl behave better in certain arenas. Not used.
            public string OwlArena { get; set; }
            [YamlIgnore]
            public Arena ArenaData { get; set; }
            [YamlIgnore]
            public Arena OwlArenaData { get; set; }
            [YamlIgnore]
            public HashSet<string> TagSet { get; set; }
            public bool HasTag(string tag)
            {
                if (Tags == null) return false;
                if (TagSet == null) TagSet = new HashSet<string>(Tags.Split(' '));
                return TagSet.Contains(tag);
            }
            // Used in a few places, but
            public bool IsBossTarget => Class == EnemyClass.Boss || Class == EnemyClass.TutorialBoss;
            // Is an 'important' encounter with an arena and non-respawning enemy
            public bool IsNamedTarget => Class == EnemyClass.Boss || Class == EnemyClass.TutorialBoss || Class == EnemyClass.Miniboss || Class == EnemyClass.FoldingMonkey;
            // Should immortality for this enemy always be on by default, even outside a boss arena
            public bool IsImmortal => Class == EnemyClass.Boss && !HasTag("mortal");
            // Should immortality for this enemy always be removed, even inside a boss arena
            public bool IsMortalBoss => (Class == EnemyClass.Boss && HasTag("mortal")) || Class == EnemyClass.TutorialBoss;
            // Adjust phases for heuristics
            public int SourcePhases => (Phases < 1 ? 1 : Phases) + (HasTag("buffphase") ? 1 : 0);
            public int TargetPhases => (Phases < 1 ? 1 : Phases) + (HasTag("buffphase") ? 1 : 0) + (HasTag("nerfloc") ? -1 : 0);
        }

        private static readonly float radConvert = (float)Math.PI / 180;
        public class Arena
        {
            public Vector3 Pos { get; set; }
            public Vector3 Rot { get; set; }
            public Vector3 Box { get; set; }
            private bool matrixInit = false;
            private Matrix4x4 matrix;
            public Matrix4x4 RotMatrix
            {
                get
                {
                    if (!matrixInit)
                    {
                        matrix = Matrix4x4.CreateFromYawPitchRoll(Rot.Y * radConvert, Rot.X * radConvert, Rot.Z * radConvert);
                        matrixInit = true;
                    }
                    return matrix;
                }
            }
            public static Arena Parse(string arena)
            {
                // This format is highly ad hoc, but it is all the needed numbers
                // Example: -18.27 114.3 558.11 -112 45 10 5 0 6.5
                // <x pos> <y pos> <z pos> <y rot> <x width> <y height> <z depth> <opt: x rot> <opt: z rot>
                // following fromsoft convention, x and z pos are center of the box, and y pos is bottom of the box
                List<float> c = arena.Split(' ').Select(w => float.Parse(w, CultureInfo.InvariantCulture)).ToList();
                if (c.Count != 7 && c.Count != 9) throw new Exception($"Badly formatted arena bounds: {arena}");
                return new Arena
                {
                    Pos = new Vector3(c[0], c[1], c[2]),
                    Rot = c.Count == 9 ? new Vector3(c[7], c[3], c[8]) : new Vector3(0, c[3], 0),
                    Box = new Vector3(c[4], c[5], c[6]),
                };
            }
        }

        public enum EnemyClass
        {
            // Do not randomize
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
            // Remove source
            Remove = 8,
            // Randomizable when specifically referenced, but not generally
            SetNone = 9,
        }

        public class EnemyData
        {
            // Do not get transplanted
            public string Map { get; set; }
            public string Name { get; set; }
            // Gets transplanted
            public string Model { get; set; }
            public int NPC { get; set; }
            public int Think { get; set; }
            // Info for placement
            public List<int> Group { get; set; }
        }
    }
}
