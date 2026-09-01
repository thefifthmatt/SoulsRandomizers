using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Numerics;
using YamlDotNet.Serialization;
using SoulsFormats;
using static RandomizerCommon.Messages;
using System.Text.Json.Serialization;

namespace RandomizerCommon
{
    public class EnemyAnnotations
    {
        public List<ClassConfig> Classes { get; set; }
        // Enemies which can be referenced by name in presets
        public List<EnemyCategory> BaseCategories { get; set; } = new();
        public List<EnemyCategory> Categories { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<EnemyCategory> AllCategories => BaseCategories.Concat(Categories);
        public List<EnemyCategoryChange> CategoryChanges { get; set; }
        // Enemies to exclude from Oops All UI because they don't work very well
        public List<string> Singletons { get; set; }
        public List<EnemyGroup> Groups { get; set; } = new();
        public List<EnemyInfo> Enemies { get; set; } = new();
        public List<PassiveAdjustment> PassiveAdjustments { get; set; }
        // Corresponds to norandom tag. Non-category-based.
        public string DefaultDontRandomize { get; set; }
        public Dictionary<string, string> CustomNames { get; set; }
        public List<CustomCleverName> CustomNamesList { get; set; }
        // Generic info per model type, in theory for things like automatic classification
        public List<EnemyDefault> Defaults { get; set; }
        public List<ObjectInfo> Objects { get; set; }
        public List<ChestInfo> Chests { get; set; }
        // This is null-checked, does that matter?
        public Dictionary<uint, int> ScalingSections { get; set; } = new();

        public SortedSet<uint> GetGroup(string name)
        {
            EnemyGroup group = Groups.Find(g => g.Name == name);
            if (group == null) return new();
            return group.Entities;
        }

        public class EnemyCategory
        {
            // The main display name, which should match the model name or ExtraNames of enemies in the config
            public string Name { get; set; }
            // Parent info with other categories, with not directed nested, for purpose of redundant category detection
            public List<string> Parents { get; set; }
            // All parent classes, also for the purpose of redundant categories. This can be concatenated with the direct parent's list.
            public List<EnemyClass> ParentClasses { get; set; }
            // Space-separated keywords
            public string Keywords { get; set; }
            // The count of enemies here, when it should be displayed
            public int Count { get; set; }
            // Whether the name is hidden because it's too granular
            public bool Hidden { get; set; }
            // How to filter enemies. Currently supports base and dlc, for Classes only (requires filtering otherwise)
            public string Region { get; set; }
            // The list of subcategory display names, when they are a partition of the enemies
            public List<string> Partition { get; set; }
            public List<EnemyCategory> Partitions { get; set; }
            // The list of subcategory display names, when it is an unstructured subset of all of them
            public List<string> Partial { get; set; }
            public List<EnemyCategory> Partials { get; set; }
            // The list of names of individual instances of this enemy, usually for minibosses
            public List<string> Instance { get; set; }
            public List<EnemyCategory> Instances { get; set; }
            // Entire classes included here, for config-based EnemyClassGroup definitions
            public List<EnemyClass> Classes { get; set; }
            [YamlIgnore]
            public HashSet<EnemyClass> ClassSet { get; set; }
            // When this category is defined as the union of two other categories, those categories. This is only used for dragons.
            public List<string> Contains { get; set; }
        }

        public class EnemyCategoryChange
        {
            public string From { get; set; }
            public string To { get; set; }
            public List<string> Tos { get; set; }
            public int Version { get; set; }
        }

        public class EnemyDefault
        {
            public string Name { get; set; }
            public string DebugText { get; set; }
            public string Comment { get; set; }
            public EnemyClass Class { get; set; }
            public string PrevName { get; set; }
            public string Parent { get; set; }
            public string Keywords { get; set; }
            public string Tags { get; set; }
            // For DLC enemies which are equivalent to base game enemies. This is not used since RandomizerCrashFix "solved" that problem
            public string Clone { get; set; }
            // Requires infix-free tags
            public bool HasTag(string tag) => Tags != null && Tags.Contains(tag);
        }

        public class EnemyGroup
        {
            public string Name { get; set; }
            public List<string> Models { get; set; }
            [YamlIgnore]
            public SortedSet<uint> Entities = new();
        }

        // TODO: Is there some way to save memory on these? Nesting fields which only appear in bosses?
        // Entire config takes around 15MB
        public class EnemyInfo
        {
            // TODO: Check all uint fields for presence checks

            // Unique entity id
            public uint ID { get; set; }
            // Internal map id
            public string Map { get; set; }
            [YamlIgnore]
            public bool DLC { get; set; }
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
            public uint OwnedBy { get; set; }
            // For ER first-phase bosses, the second phase.
            public uint NextPhase { get; set; }
            // An enemy who will probably be aggro'd when a boss or miniboss is aggro'd.
            // This can be configured in Sekiro presets to avoid making powerful enemies into minions.
            public uint Add { get; set; }
            // Info for bosses and minibosses
            public uint DefeatEntity { get; set; }
            public int DefeatFlag { get; set; }
            public int AppearFlag { get; set; }
            public int StartFlag { get; set; }
            public int EncounterFlag { get; set; }
            public int MusicFlag { get; set; }
            public int Phases { get; set; }
            public int HealthbarIndex { get; set; }
            // Nested info for important enemies (boss, minibosses) to make this struct less memory-intensive
            // Needs migration in all games
            public ImportantInfo Important { get; set; }
            // <eventid> in <map>
            public string StartEvent { get; set; }
            public string EndEvent { get; set; }
            // ESD containing line said on death, which should be delayed until DefeatFlag is set (using machine 1103)
            public int DeathLine { get; set; }
            // TalkParam lines (includes range of 100 in Elden Ring) to exclude from animations for enemies without "speak" tag
            public string TalkLine { get; set; }
            // Cosplay
            public string Outfit { get; set; }
            // Tags and metadata
            public string Tags { get; set; }
            // Category for presets
            public string Category { get; set; }
            // Name of the enemy used in item randomizer, and also to identify some bosses and minibosses
            public string ItemName { get; set; }
            // Event groups used by event scripts which should be part of multichr
            public List<uint> Groups { get; set; }
            // Group to remove the entity from if it gets randomized to something else
            // Mainly for handling multi-phase bosses where one phase is randomized (stays in the group) and the other isn't
            public uint RemoveGroup { get; set; }
            // In Elden Ring, group used in a boss encounter to keep spirit summons around
            public uint BuddyGroup { get; set; }
            // If this is a derived randomized entity, the base non-randomized entity to copy it from. The derived entity is never created
            // if it's *not* randomized. This is for cases where something of an entity's behavior should be left behind when it's moved.
            public uint SplitFrom { get; set; }
            // Filled automatically in the case of dupes, matches SplitFrom when tag "dupe" is present
            public uint DupeFrom { get; set; }
            public int DupeIndex { get; set; }
            // If this otherwise non-randomized enemy can be duplicated, events to duplicate
            // If this is defined, other event templates can be used in dupe mode as well.
            public List<uint> DupeEvents { get; set; }
            // Change the enemy's collision to the given name
            public string SetColName { get; set; }
            // Full name for enemies (mainly bosses) when there is ambiguity vs the model, for spoiler log and presets
            public string ExtraName { get; set; }
            // Name template for name mashups, sometimes with multiple semicolon-separate parts (see below)
            public string FullName { get; set; }
            // Specific name for name mashups. If "title" tag is used, it's a proper name, otherwise an adjective-less part.
            public string PartName { get; set; }
            // Mapping from other part names to an even more custom combined name (deprecated)
            // public Dictionary<string, string> CustomNames { get; set; }
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
            public uint ArenaRegion { get; set; }
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
            private HashSet<string> TagSet = null;
            public void SetTags(string tags)
            {
                TagSet = new HashSet<string>(tags.Split(' '));
            }
            public void AddTag(string tag)
            {
                if (TagSet == null)
                {
                    TagSet = new HashSet<string> { tag };
                }
                else
                {
                    TagSet.Add(tag);
                }
            }
            public bool HasTag(string tag)
            {
                return TagSet?.Contains(tag) ?? false;
            }
            public bool GetNamedTag(string name, out string val)
            {
                val = null;
                if (TagSet == null) return false;
                string prefix = name + ":";
                foreach (string tag in TagSet)
                {
                    if (tag.StartsWith(prefix))
                    {
                        val = tag.Substring(prefix.Length);
                        return true;
                    }
                }
                return false;
            }
            [YamlIgnore]
            public IEnumerable<string> ExcludeTags => TagSet == null ? Array.Empty<string>() : TagSet.Where(t => t.StartsWith("exclude:"));

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

        public class ImportantInfo
        {
            // Name things
            // Full name for enemies (mainly bosses) when there is ambiguity vs the model, for spoiler log and presets
            // Actually, don't put this here, it's not just bosses, especially in Elden Ring
            public string ExtraName { get; set; }

            public NameTemplates Names { get; set; }

            // Npc name id for non-mashup substitutions
            public int NpcName { get; set; }
        }

        public class NameTemplates
        {
            // Language, filled in automatically
            [JsonPropertyName("lang")]
            public string Lang { get; set; }

            // Key for localization and used for reference. Not always equivalent to FullName (e.g. 'Tree Spirit' is used, never 'Ulcerated Tree Spirit')
            [JsonPropertyName("key")]
            public string Key { get; set; }

            // Source fields
            // The original boss name which can appear standalone in any context. Should be present unless ProperName is defined
            [JsonPropertyName("full_name")]
            public string FullName { get; set; }
            // Partial name. When PartialTemplate is not defined, FullName is used instead.
            [JsonPropertyName("partial_name")]
            public string PartialName { get; set; }
            // Proper noun (title). This selects ProperTemplate when defined.
            [JsonPropertyName("proper_name")]
            public string ProperName { get; set; }
            [YamlIgnore]
            [JsonIgnore]
            public string SourcePartialName => ProperName ?? PartialName ?? FullName;
            [YamlIgnore]
            [JsonIgnore]
            public string SourceFullName => ProperName ?? FullName;

            // Target fields (mostly)
            // Either FullTemplate or PartialTemplate is required for the target (TODO should combine them?)
            // When defined, this uses FullName or ProperName if defined, and the original NPC name otherwise.
            [JsonPropertyName("full_template")]
            public string FullTemplate { get; set; }
            // When defined, this uses PartialName, FullName, or ProperName.
            [JsonPropertyName("partial_template")]
            public string PartialTemplate { get; set; }
            // Template to use only when the source has a ProperName defined
            [JsonPropertyName("proper_template")]
            public string ProperTemplate { get; set; }
            // When defined, mashups with this enemy use the source enemy for the template, and the same fill rules as PartialTemplate.
            [JsonPropertyName("source_template")]
            public string SourceTemplate { get; set; }
            [YamlIgnore]
            [JsonIgnore]
            public string MainTemplate => FullTemplate ?? PartialTemplate;
            [YamlIgnore]
            [JsonIgnore]
            public string UniqueTemplate => ProperTemplate ?? FullTemplate ?? PartialTemplate;

            // Additional tag. Sources have a fixed case, targets can select cases.
            // TODO: Implement
            [JsonPropertyName("category")]
            public string Category { get; set; }

            // TODO: Should return "" values here, for verification? Should validate it at least
            internal List<string> GetConfigValues() => new[] { FullName, PartialName, ProperName, FullTemplate, PartialTemplate, ProperTemplate, SourceTemplate }
                .Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        public class CustomCleverName
        {
            [JsonPropertyName("old")]
            public string Old { get; set; }
            [JsonPropertyName("new")]
            public string New { get; set; }
            [JsonPropertyName("source")]
            public string Source { get; set; }
            [JsonPropertyName("target")]
            public string Target { get; set; }
        }

        public class CustomCleverNames
        {
            public Dictionary<string, string> Replacements { get; set; } = new();
            public Dictionary<string, List<CustomCleverName>> CondReplacements { get; set; } = new();

            // Temporary for migration
            public static CustomCleverNames FromDictionary(Dictionary<string, string> replacements)
            {
                return new CustomCleverNames { Replacements = replacements };
            }

            public static CustomCleverNames FromConfig(List<CustomCleverName> names)
            {
                CustomCleverNames ret = new();
                foreach (CustomCleverName name in names)
                {
                    if (name.Old == null || name.New == null)
                    {
                        continue;
                    }
                    if (name.Source == null && name.Target == null)
                    {
                        ret.Replacements[name.Old] = name.New;
                    }
                    else
                    {
                        Util.AddMulti(ret.CondReplacements, name.Old, name);
                    }
                }
                return ret;
            }

            public bool GetReplacement(NameTemplates sourceNames, NameTemplates targetNames, string oldName, out string newName)
            {
                if (CondReplacements.TryGetValue(oldName, out List<CustomCleverName> names))
                {
                    foreach (CustomCleverName name in names)
                    {
                        if (name.Source != null && sourceNames?.Key != name.Source)
                        {
                            continue;
                        }
                        if (name.Target != null && targetNames?.Key != name.Target)
                        {
                            continue;
                        }
                        newName = name.New;
                        return true;
                    }
                }
                return Replacements.TryGetValue(oldName, out newName);
            }

            public bool Empty() => Replacements.Count == 0 && CondReplacements.Count == 0;
        }

        // TODO instead of tags, BossNames
        private static (string, string) ParseFullName(string fullName)
        {
            string titleName = null;
            if (fullName.IndexOf(';') >= 0)
            {
                string[] parts = fullName.Split(';');
                if (parts.Length != 2) throw new Exception($"Internal error: invalid FullName {fullName}");
                fullName = parts[0].Trim();
                titleName = parts[1].Trim();
            }
            return (fullName, titleName);
        }

        // target should be non-null, source may be null (in which case fallbackPart should be non-null)
        public static string CalculateCleverName(NameTemplates sourceNames, NameTemplates targetNames, string fallbackPart)
        {
            // Filtered out earlier, but should be part of this logic for preview purposes
            if (targetNames?.MainTemplate == sourceNames?.MainTemplate)
            {
                return sourceNames.Key ?? sourceNames.FullName ?? fallbackPart;
            }
            // New implementation. Does not use npcName (FullName and PartialName can coexist)
            if (sourceNames?.SourceTemplate != null && targetNames.SourcePartialName is string targetPart)
            {
                return sourceNames.SourceTemplate.Replace("$1", targetPart);
            }
            if (targetNames.ProperTemplate != null && sourceNames?.ProperName != null)
            {
                return targetNames.ProperTemplate.Replace("$1", sourceNames.ProperName);
            }
            if (targetNames.FullTemplate != null)
            {
                string sourcePart = sourceNames?.SourceFullName ?? fallbackPart;
                return targetNames.FullTemplate.Replace("$1", sourcePart);
            }
            else
            {
                string sourcePart = sourceNames?.SourcePartialName ?? fallbackPart;
                return targetNames.PartialTemplate.Replace("$1", sourcePart);
            }
        }

        // Requires target.FullName to exist (or FullTemplate or PartialTemplate)
        // In the case of conditionally partial parts (source has "partial" tag), npcName is the full name.
        public static string CalculateCleverName(EnemyInfo source, EnemyInfo target, string fallbackPart, string npcName = null)
        {
            NameTemplates sourceNames = source.Important?.Names;
            NameTemplates targetNames = target.Important?.Names;
            if (targetNames?.MainTemplate != null)
            {
                // TODO: Per language
                return CalculateCleverName(sourceNames, targetNames, fallbackPart);
            }
            (string fullTemplate, string titleTemplate) = ParseFullName(target.FullName);
            // Special case where titleName is overloaded (TODO make a BossNames class), as custom names would be too tedious for this
            if (target.HasTag("sourcefull"))
            {
                titleTemplate = null;
            }
            if (source.HasTag("sourcefull"))
            {
                (string _, string sourceTemplate) = ParseFullName(source.FullName);
                if (sourceTemplate != null && target.PartName != null)
                {
                    return sourceTemplate.Replace("$1", target.PartName);
                }
            }
            bool allowPartial = target.HasTag("adjective") || target.HasTag("partial") || titleTemplate != null;
            bool hasPartial = source.HasTag("partial");
            bool hasProperName = source.HasTag("proper");
            string template = fullTemplate;
            string partStr = source.PartName ?? fallbackPart;
            if (titleTemplate != null && hasProperName)
            {
                template = titleTemplate;
            }
            if (hasPartial && !allowPartial)
            {
                // If hasPartial, npcName should be the full name
                partStr = npcName ?? partStr;
            }
            return template.Replace("$1", partStr);
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
        public static readonly Dictionary<EnemyClass, Text> ClassNames = MapText<EnemyClass>("EnemyClass", new()
        {
            [EnemyClass.Basic] = "Regular Enemies",
            [EnemyClass.Miniboss] = "World Minibosses",
            [EnemyClass.Boss] = "Major Bosses",
            [EnemyClass.HostileNPC] = "Hostile Humans",
            [EnemyClass.Wildlife] = "Passive Wildlife",
            [EnemyClass.Scarab] = "Scarabs",
            [EnemyClass.MinorBoss] = "Minor Bosses",
            [EnemyClass.NightMiniboss] = "Night Minibosses",
            [EnemyClass.DragonMiniboss] = "Dragon Minibosses",
            [EnemyClass.Evergaol] = "Evergaol Minibosses",
            [EnemyClass.Spectator] = "Spectators",
        });
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
            // If true, allows siloing DLC. Disabled for classes which only appear in one or the other.
            public bool DlcSilo { get; set; }
        }

        // Meta categories? Maybe have an All Bosses thing, and All Enemies, and Dragons
        // Buttons: Separate all categories / Merge boss categories / Merge all categories
        [Localize]
        public static readonly Dictionary<EnemyClassGroup, Text> ClassGroupNames = MapText<EnemyClassGroup>("EnemyClassGroup", new()
        {
            [EnemyClassGroup.AllEnemies] = "All Enemies and Bosses",
            [EnemyClassGroup.AllDragons] = "Dragons",
            [EnemyClassGroup.AllBosses] = "All Bosses",
            [EnemyClassGroup.BaseAllEnemies] = "All Base Game Enemies and Bosses",
            [EnemyClassGroup.BaseAllBosses] = "All Base Game Bosses",
            [EnemyClassGroup.BaseBasic] = "Base Game Regular Enemies",
            [EnemyClassGroup.BaseBoss] = "Base Game Major Bosses",
            [EnemyClassGroup.BaseMinorBoss] = "Base Game Minor Bosses",
            [EnemyClassGroup.BaseMiniboss] = "Base Game World Minibosses",
            [EnemyClassGroup.BaseDragonMiniboss] = "Base Game Dragon Minibosses",
            [EnemyClassGroup.BaseWildlife] = "Base Game Passive Wildlife",
            [EnemyClassGroup.DLCAllEnemies] = "All DLC Enemies and Bosses",
            [EnemyClassGroup.DLCAllBosses] = "All DLC Bosses",
            [EnemyClassGroup.DLCBasic] = "DLC Regular Enemies",
            [EnemyClassGroup.DLCBoss] = "DLC Major Bosses",
            [EnemyClassGroup.DLCMinorBoss] = "DLC Minor Bosses",
            [EnemyClassGroup.DLCMiniboss] = "DLC World Minibosses",
            [EnemyClassGroup.DLCDragonMiniboss] = "DLC Dragon Minibosses",
            [EnemyClassGroup.DLCWildlife] = "DLC Passive Wildlife",
        });
        public enum EnemyClassGroup
        {
            Default = 0,
            AllEnemies = 1,
            AllDragons = 2,
            AllBosses = 3,
            BaseAllEnemies = 101,
            BaseAllBosses = 103,
            BaseBasic = 110,
            BaseBoss = 111,
            BaseMinorBoss = 112,
            BaseMiniboss = 113,
            BaseDragonMiniboss = 114,
            BaseWildlife = 115,
            DLCAllEnemies = 201,
            DLCAllBosses = 203,
            DLCBasic = 210,
            DLCBoss = 211,
            DLCMinorBoss = 212,
            DLCMiniboss = 213,
            DLCDragonMiniboss = 214,
            DLCWildlife = 215,
        }

        public class PassiveAdjustment
        {
            public string Source { get; set; }
            public int Percent { get; set; }
        }

        // TODO: Make readonly record, use with keyword for edits and move them earlier during randomization
        public class EnemyData
        {
            // Do not get transplanted
            public string MainMap { get; set; }
            public string DupeMap { get; set; }
            public IEnumerable<string> Maps => DupeMap == null ? new[] { MainMap } : new[] { MainMap, DupeMap };
            public string Name { get; set; }
            public uint ID { get; set; }
            // Gets transplanted
            public string Model { get; set; }
            public int NPC { get; set; }
            public int Think { get; set; }
            public int Char { get; set; }  // DS3 only
            public int Anim { get; set; }  // DS1 and Elden Ring only
            // Info for placement
            public List<uint> Group { get; set; }
            public string Col { get; set; }
            // TODO: Is this worth it?
            public Vector3 Pos { get; set; }
        }

        public class SpecialAnim
        {
            // First the mechanics for setting animations. This is a type then id. The id can be an int or full param name with width.
            // map (special anim set in MSB), init (special anim set in emevd), disabled (disabled in emevd), or null
            public string InitType { get; set; }
            public int InitAnim { get; set; } = -1;
            public string InitStr { get; set; }
            // cancel (cancel anim set in emevd), force (ForceAnimationPlayback), or null
            public string CancelType { get; set; }
            public int CancelAnim { get; set; } = -1;
            public string CancelStr { get; set; }
            // Additional classification.
            // All events requiring edit have exactly one MAIN condition, and can either be added after start or after the first End command.
            // wakeup: Animations can be used elsewhere, both damage and non-damage conditions, and gravity is not scripted.
            // -> Add explicit AI enable/disable if no usable initial anim (some like 11215100 have it already)
            // root: Animations can't be used elsewhere, but otherwise like wakeup.
            // -> No edit required, all current cases use AI enable/disable.
            // climb: Animations can't be used elsewhere. Gravity is scripted, AI disabled, no damage condition.
            // -> Add damage condition (is bug in original game, like daggering hanging hollows)
            // invincible: No damage condition. Requires non-damage condition elsewhere.
            // -> Add damage condition
            // lunge: AI or character initially disabled, anim can only be used in other lunges (TODO see if 500 works in more cases)
            // dropdown: Gravity is scripted. Anim is safe to use elsewhere but don't.
            // -> Add explicit AI enable/disable, ignore anims
            // float: Animations can't be used elsewhere, needs AI/damage check.
            // passive: No scripting, just locked in animation until staggered out of it.
            // Can also have a type for indicating an enemy has chr animation management and to remove the event.
            public string Type { get; set; }

            public static SpecialAnim Default = new SpecialAnim() { Type = "none" };
        }
    }
}
