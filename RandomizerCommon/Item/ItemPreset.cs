using Org.BouncyCastle.Utilities.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Util;


namespace RandomizerCommon
{
    // This should not depend on AnnotationData, since it's used to create it in the first place
    public class ItemPreset
    {
        public enum PlacementType
        {
            Unspecified,
            KeyItems,
            QuestItems,
            CoreMechanics,
            SacredFlaskUpgrades,
            UpgradeBellBearings,
            ShadowRealmBlessings,
            SecondaryMechanics,
            BossTools,
            BaseOnlyItems,
            DlcOnlyItems,
            Custom,
        }
        public enum PlacementMode
        {
            Unspecified,
            Norandom,
            Anywhere,
            Important,
            // TODO: Can add PlacementGroup ref if needed, or just make that part of custom
            Custom,
        }
        [Localize]
        public static readonly Dictionary<PlacementType, Text> PlacementTypeNames = MapText<PlacementType>("ItemPreset_PlacementType", new()
        {
            [PlacementType.Custom] = "Custom item placement",
            [PlacementType.KeyItems] = "Major key items",
            [PlacementType.QuestItems] = "Quest items",
            [PlacementType.CoreMechanics] = "Core mechanics",
            [PlacementType.SacredFlaskUpgrades] = "Sacred Flask Upgrades",
            [PlacementType.UpgradeBellBearings] = "Upgrade Bell Bearings",
            [PlacementType.ShadowRealmBlessings] = "Shadow Realm Blessings",
            [PlacementType.SecondaryMechanics] = "Secondary mechanics",
            [PlacementType.BossTools] = "Boss tools",
            [PlacementType.BaseOnlyItems] = "Base-game-only items",
            [PlacementType.DlcOnlyItems] = "DLC-only items",
        });
        [Localize]
        public static readonly Dictionary<PlacementMode, Text> PlacementModeNames = MapText<PlacementMode>("ItemPreset_PlacementMode", new()
        {
            [PlacementMode.Norandom] = "Not randomized",
            [PlacementMode.Anywhere] = "Anywhere",
            [PlacementMode.Important] = "Important locations",
            [PlacementMode.Custom] = "Custom",
        });
        [Localize]
        public static readonly Dictionary<PlacementType, Text> PlacementTypeDesc = MapText<PlacementType>("ItemPreset_PlacementTypeDesc", new()
        {
            [PlacementType.KeyItems] = "Placement for key items which unlock areas containing unmissable items",
            [PlacementType.QuestItems] = "Placement for items which advance quests but are not otherwise required to access areas",
            [PlacementType.BaseOnlyItems] = "Items which are only placed in the base game when they are randomized. Cannot include key or quest items currently.",
            [PlacementType.DlcOnlyItems] = "Items which are only placed in the DLC when they are randomized. Cannot include key or quest items currently.",
        });
        [Localize]
        public static readonly Dictionary<PremiumGroupType, Text> PremiumGroupTypeNames = MapText<PremiumGroupType>("ItemPreset_PremiumGroupType", new()
        {
            [PremiumGroupType.Custom] = "Custom location",
            [PremiumGroupType.SorceryShop] = "Sorcery shop",
            [PremiumGroupType.MiracleShop] = "Incantation shop",
            [PremiumGroupType.PremiumShop] = "Premium currency shop",
        });
        // Not only part of preset data model, but only configurable in preset
        [Localize]
        public static readonly Dictionary<ItemSiloType, Text> ItemSiloTypeNames = MapText<ItemSiloType>("ItemPreset_ItemSiloType", new()
        {
            [ItemSiloType.Manual] = "Listed items",
            [ItemSiloType.WeaponType] = "Weapons by type",
            [ItemSiloType.ArmorType] = "Armor by type",
        });

        [Localize]
        public static readonly Text ImportantLocationsDesc = new Text(
            "Placement for items configured to appear in important locations. This is done in a pass before all other items in the game, to ensure they have highest priority for placement. If they can't be placed, randomization will fail.",
            "ItemPreset_PlacementModeDesc_Important");
        [Localize]
        public static readonly Text NorandomItemsText = new Text("Not randomized:", "ItemPreset_norandomItems");
        [Localize]
        public static readonly Text RemoveItemsText = new Text("Removed:", "ItemPreset_removeItems");
        [Localize]
        public static readonly Text RandomItemsText = new Text("Randomized:", "ItemPreset_randomItems");
        [Localize]
        public static readonly Text PlacementItemsText = new Text("Items:", "ItemPreset_placementItems");
        [Localize]
        public static readonly Text PlacementOverallText = new Text("Placed in:", "ItemPreset_placementOverall");
        [Localize]
        public static readonly Text PlacementRequiresText = new Text("Only placed in:", "ItemPreset_placementRequires");
        [Localize]
        public static readonly Text PlacementExcludesText = new Text("Never placed in:", "ItemPreset_placementExcludes");
        [Localize]
        public static readonly Text OverallSiloText = new Text("Overall item placement silo:", "ItemPreset_overallSilo");
        [Localize]
        public static readonly Text KeyItemSiloText = new Text("Key item placement silo:", "ItemPreset_keyItemSilo");
        [Localize]
        public static readonly Text ItemSiloText = new Text("Item silos:", "ItemPreset_itemSilo");
        [Localize]
        public static readonly Text ItemSiloNameText = new Text("Custom item silo", "ItemPreset_itemSiloName");
        [Localize]
        public static readonly Dictionary<AreaSiloType, Text> AreaSiloTypeNames = MapText<AreaSiloType>("ItemPreset_AreaSiloType", new()
        {
            [AreaSiloType.None] = "None",
            [AreaSiloType.DLC] = "Separate base game and DLC",
            // TODO: Is this included?
            [AreaSiloType.Region] = "",
        });

        public class Placement
        {
            // Category
            public PlacementType Type { get; set; }
            // For custom ones. Not used yet
            public string Name { get; set; }
            // Set if it should be ignored. Not currently used or exposed in UI.
            public bool Disabled { get; set; }
            // Placement mode
            public PlacementMode Mode { get; set; }
            // Item or item groups in scope. Should not include individual key items or quest items.
            public List<ItemRef> Items { get; set; } = new();
            // Eligible locations, if custom placement.
            public List<LocationRef> Requires { get; set; }
            // Excluded locations, in any mode (ignored in Norandom and Important)
            public List<LocationRef> Excludes { get; set; }

            // For builtins
            public List<PlacementModeSwitch> ModeSwitch { get; set; }

            [YamlIgnore]
            public IReadOnlyList<ItemKey> Keys { get; set; }

            public static Placement MakeCustom(Messages messages) => new Placement
            {
                Type = PlacementType.Custom,
                Name = messages.Get(PlacementTypeNames[PlacementType.Custom]),
                Mode = PlacementMode.Custom,
            };

            public bool ResolveModeSwitch(RandomizerOptions opt, out PlacementMode mode)
            {
                mode = PlacementMode.Unspecified;
                if (ModeSwitch == null) return false;
                foreach (PlacementModeSwitch sw in ModeSwitch)
                {
                    if (opt.IsSwitchEnabled(sw.Switch))
                    {
                        mode = sw.Mode;
                        return true;
                    }
                }
                return false;
            }

            public Placement Clone()
            {
                Placement o = (Placement)MemberwiseClone();
                o.Items = Items?.Select(x => x.Clone()).ToList();
                o.Requires = Requires?.Select(x => x.Clone()).ToList();
                o.Excludes = Excludes?.Select(x => x.Clone()).ToList();
                o.ModeSwitch = ModeSwitch?.Select(x => x.Clone()).ToList();
                return o;
            }
        }
        public class PlacementModeSwitch
        {
            public PlacementMode Mode { get; set; }
            public string Switch { get; set; }

            public PlacementModeSwitch Clone() => (PlacementModeSwitch)MemberwiseClone();
        }
        public class LocationGroup
        {
            public List<LocationRef> Requires { get; set; } = new();
            public List<LocationRef> Excludes { get; set; } = new();

            public LocationGroup Clone()
            {
                LocationGroup o = (LocationGroup)MemberwiseClone();
                o.Requires = Requires?.Select(x => x.Clone()).ToList();
                o.Excludes = Excludes?.Select(x => x.Clone()).ToList();
                return o;
            }
        }
        // Not currently used. Would be opening a pandora's box alongside custom LocationGroup, as resolution
        // becomes much more thorny (recursive resolution should not be allowed)
        public class ItemGroup
        {
            public List<ItemRef> Items { get; set; } = new();

            [YamlIgnore]
            public IReadOnlyList<ItemKey> Keys { get; set; }

            public ItemGroup Clone()
            {
                ItemGroup o = (ItemGroup)MemberwiseClone();
                o.Items = Items?.Select(x => x.Clone()).ToList();
                return o;
            }
        }

        // Enum just for localizing common types
        public enum PremiumGroupType
        {
            Unspecified,
            SorceryShop,
            MiracleShop,
            PremiumShop,
            Custom,
        }
        public class PremiumGroup
        {
            public PremiumGroupType Type { get; set; }
            // For custom groups
            public string Name { get; set; }
            public bool Disabled { get; set; }
            public string Switch { get; set; }
            // The following locations may only contain the following items and no other ones.
            // This may be ignored if there are not enough items to fill the locations. Placement of major key items
            // and important items may also ignore these rules.
            // Locations may only be edited for non-custom groups.
            public List<LocationRef> Locations { get; set; } = new();
            public List<ItemRef> Items { get; set; } = new();

            [YamlIgnore]
            public IReadOnlyList<ItemKey> Keys { get; set; }

            public static PremiumGroup MakeCustom(Messages messages) => new PremiumGroup
            {
                Type = PremiumGroupType.Custom,
                Name = messages.Get(PremiumGroupTypeNames[PremiumGroupType.Custom]),
            };

            // The switch is for enabled/disabled for non-custom groups
            public void ProcessSwitch(RandomizerOptions opt)
            {
                if (Switch != null)
                {
                    Disabled = !opt.IsSwitchEnabled(Switch);
                    Switch = null;
                }
            }

            public PremiumGroup Clone()
            {
                PremiumGroup o = (PremiumGroup)MemberwiseClone();
                o.Locations = Locations?.Select(x => x.Clone()).ToList();
                o.Items = Items?.Select(x => x.Clone()).ToList();
                return o;
            }
        }
        public class CustomSilo
        {
            // User-given name
            public string Name { get; set; }
            public bool Disabled { get; set; }
            // Type (should not be None)
            public ItemSiloType Type { get; set; }
            // List of items when Type is Manual. Higher silos take higher priority.
            public List<ItemRef> Items { get; set; }

            [YamlIgnore]
            public IReadOnlySet<ItemKey> Keys { get; set; }

            public static CustomSilo MakeCustom(Messages messages) => new CustomSilo
            {
                Name = messages.Get(ItemSiloNameText),
                Type = ItemSiloType.Manual,
            };

            public CustomSilo Clone()
            {
                CustomSilo o = (CustomSilo)MemberwiseClone();
                o.Items = Items?.Select(x => x.Clone()).ToList();
                return o;
            }
        }
        public class ItemShard
        {
            // Item ref should be a single item
            public ItemRef Item { get; set; }
            public int Count { get; set; }
            public int ReqCount { get; set; }

            [YamlIgnore]
            public ItemKey Key { get; set; }

            public ItemShard Clone()
            {
                ItemShard o = (ItemShard)MemberwiseClone();
                o.Item = Item.Clone();
                return o;
            }
        }

        public abstract class SwitchRef
        {
            // Could also be interface. These fields are for builtins
            // Enables options based on form selection, but not binding
            public string Switch { get; set; }
            // If any are true, must be enabled
            public string SwitchOn { get; set; }
            // If any are true, must be disabled
            public string SwitchOff { get; set; }
            // Used by UI when the specified option is no longer switched on
            public string ManualSwitchOff { get; set; }

            // Switch can enable options, but ForceSwitch force-enables them
            public bool IsEnabled(RandomizerOptions opt)
            {
                if (IsForbidden(opt))
                {
                    return false;
                }
                else if (IsRequired(opt))
                {
                    return true;
                }
                else return Switch == null || opt.IsSwitchEnabled(Switch);
            }

            public bool FilterRemove(RandomizerOptions opt)
            {
                bool enabled = IsEnabled(opt);
                Switch = SwitchOn = SwitchOff = ManualSwitchOff = null;
                return !enabled;
            }

            public void RemoveSwitch()
            {
                Switch = SwitchOn = SwitchOff = ManualSwitchOff = null;
            }

            public bool IsRequired(RandomizerOptions opt) => SwitchOn != null && opt.IsSwitchEnabled(SwitchOn);
            public bool IsForbidden(RandomizerOptions opt) => SwitchOff != null && opt.IsSwitchEnabled(SwitchOff);
            public bool IsForced(RandomizerOptions opt) => IsRequired(opt) || IsForbidden(opt);

            public abstract SwitchRef Clone();

            // All valid SwitchRefs are expected to have a unique RefID, but this can maybe be avoided by subclasses implementing Equals/HashCode properly
            [YamlIgnore]
            public abstract string RefID { get; }
            public override string ToString() => RefID;
            public override bool Equals(object obj) => obj is SwitchRef o && RefID.Equals(o.RefID);
            public override int GetHashCode() => RefID.GetHashCode();
        }
        public sealed class ItemRef : SwitchRef
        {
            // First case, group of items, and additional filters, currently adhoc. Could replace with Filters list
            public string Group { get; set; }
            public string Rarity { get; set; }
            public string Content { get; set; }

            // Second case
            public string Name { get; set; }
            // Using same format as merge config
            public string ID { get; set; }

            public override string RefID => ID == null ? (Group + (Content == null ? null : $"|{Content}") + (Rarity == null ? null : $"|{Rarity}")) : ID;

            public override ItemRef Clone() => (ItemRef)MemberwiseClone();
            public ItemRef WithRarity(string val) => Group == null ? this : new ItemRef { Group = Group, Rarity = val, Content = Content };
            public ItemRef WithContent(string val) => Group == null ? this : new ItemRef { Group = Group, Rarity = Rarity, Content = val };
            public ItemRef NoFilter() => Group == null ? this : new ItemRef { Group = Group };

            [YamlIgnore]
            public IReadOnlyList<ItemKey> Keys { get; set; }
        }
        public sealed class LocationRef : SwitchRef
        {
            public string Tag { get; set; }

            public override string RefID => Tag;

            public override LocationRef Clone() => (LocationRef)MemberwiseClone();
        }

        public class PlacementMeta
        {
            // Whether the group can be disabled altogether
            public bool Disable { get; set; }
            // If true, the item list can be changed
            public bool EditItems { get; set; }
            // If true, the location list *or* PlacementMode can be changed
            public bool EditLocs { get; set; }
            // If true, required locations can be edited. This is true for everything for now
            // public bool AllowRequires { get; set; }
            // If true, tags like dlc/nodlc cannot be used. Only if EditLocs
            public bool GlobalLocs { get; set; }

            // If true, missable tag won't be automatically added and can be removed. Not part of UI, enforced during resolve.
            public bool AllowMissable { get; set; }
            // This previously had a mode to always add a fixed set of tags (boss and racemode for key items and
            // important locations), but is no longer needed.
        }
        // All placements must have a meta tag
        public static readonly Dictionary<PlacementType, PlacementMeta> PlacementMetas = new()
        {
            [PlacementType.Custom] = new() { Disable = true, EditItems = true, EditLocs = true, AllowMissable = true },
            [PlacementType.KeyItems] = new() { EditLocs = true, GlobalLocs = true },
            [PlacementType.QuestItems] = new() { EditLocs = true, GlobalLocs = true },
            [PlacementType.CoreMechanics] = new() { Disable = true, EditItems = true, EditLocs = true },
            [PlacementType.SacredFlaskUpgrades] = new() { EditLocs = true },
            [PlacementType.UpgradeBellBearings] = new() { EditLocs = true, GlobalLocs = true },
            [PlacementType.ShadowRealmBlessings] = new() { EditLocs = true },
            [PlacementType.SecondaryMechanics] = new() { Disable = true, EditItems = true },
            [PlacementType.BossTools] = new() { Disable = true, EditItems = true },
            [PlacementType.BaseOnlyItems] = new() { EditItems = true, AllowMissable = true },
            [PlacementType.DlcOnlyItems] = new() { EditItems = true, AllowMissable = true },
        };
        public static readonly PlacementMeta ImportantMeta = new() { EditLocs = true, GlobalLocs = true };

        public AreaSiloType OverallSilo { get; set; }
        public AreaSiloType KeyItemSilo { get; set; }
        public List<ItemRef> NorandomItems { get; set; } = new();
        public List<LocationRef> NorandomLocations { get; set; } = new();
        public List<ItemRef> RemoveItems { get; set; } = new();
        public List<Placement> Placements { get; set; } = new();
        public List<PremiumGroup> PremiumGroups { get; set; } = new();
        public List<CustomSilo> CustomSilos { get; set; } = new();
        public List<LocationGroup> LocationGroups { get; set; } = new();
        public List<ItemShard> ItemShards { get; set; } = new();
        // When null, use bias from RandomizerOptions
        public RandomizerOptions.BiasWeights BiasWeights { get; set; }
        public List<string> Options { get; set; }
        public int Version { get; set; } = PresetVersion;

        public Placement GetPlacement(PlacementType type) => Placements.Find(p => p.Type == type);
        public PremiumGroup GetPremiumGroup(PremiumGroupType type) => PremiumGroups.Find(p => p.Type == type);
        [YamlIgnore]
        public LocationGroup ImportantGroup => LocationGroups.FirstOrDefault();

        public static readonly int PresetVersion = 2;

        public ItemPreset Clone()
        {
            ItemPreset o = (ItemPreset)MemberwiseClone();
            o.NorandomItems = NorandomItems?.Select(x => x.Clone()).ToList();
            o.NorandomLocations = NorandomLocations?.Select(x => x.Clone()).ToList();
            o.RemoveItems = RemoveItems?.Select(x => x.Clone()).ToList();
            o.Placements = Placements?.Select(x => x.Clone()).ToList();
            o.PremiumGroups = PremiumGroups?.Select(x => x.Clone()).ToList();
            o.CustomSilos = CustomSilos?.Select(x => x.Clone()).ToList();
            o.LocationGroups = LocationGroups?.Select(x => x.Clone()).ToList();
            return o;
        }

        public string ToYamlString()
        {
            StringWriter s = new StringWriter();
            GameData.Serializer.Serialize(s, this);
            return s.ToString();
        }

        private static string DebugYamlString<T>(T obj)
        {
            StringWriter s = new StringWriter();
            GameData.Serializer.Serialize(s, obj);
            return s.ToString();
        }

        // Populate runtime structures including some validation, but no changes for a valid preset
        public void Resolve(GameData game, IDictionary<string, List<ItemKey>> itemGroups, IDictionary<ItemKey, List<ItemKey>> keyAliases, ItemPreset def = null)
        {
            if (OverallSilo == AreaSiloType.Region)
            {
                OverallSilo = AreaSiloType.None;
            }
            if (KeyItemSilo == AreaSiloType.Region)
            {
                KeyItemSilo = AreaSiloType.None;
            }
            if (OverallSilo == AreaSiloType.DLC)
            {
                KeyItemSilo = AreaSiloType.DLC;
            }
            NorandomItems ??= new();
            NorandomItems.ForEach(i => ResolveItemRef(i, game, itemGroups, keyAliases));
            NorandomLocations ??= new();
            RemoveItems ??= new();
            RemoveItems.ForEach(i => ResolveItemRef(i, game, itemGroups, keyAliases));
            HashSet<ItemKey> cantFilterItems = new();
            cantFilterItems.UnionWith(itemGroups["keyitems"]);
            cantFilterItems.UnionWith(itemGroups["questitems"]);
            void addTag(List<LocationRef> locs, string tag)
            {
                if (!locs.Any(r => r.Tag == tag))
                {
                    locs.Insert(0, new LocationRef { Tag = tag });
                }
            }
            void checkLocationMeta(PlacementMeta meta, List<LocationRef> requires, List<LocationRef> excludes)
            {
                // Calling code must check these are nonnull if needed first
                // In this case silently edit I guess
                if (requires != null)
                {
                    requires.RemoveAll(r => !RequirableTags.Contains(r.Tag));
                }
                if (excludes != null)
                {
                    excludes.RemoveAll(r => r.Tag == null);
                    if (!meta.AllowMissable)
                    {
                        addTag(excludes, "missable");
                    }
                    if (meta.GlobalLocs)
                    {
                        excludes.RemoveAll(r => DlcTags.Contains(r.Tag));
                    }
                }
            }
            LocationGroup important = LocationGroups?.FirstOrDefault();
            LocationGroup defImportant = def?.LocationGroups?.FirstOrDefault().Clone();
            if (important == null)
            {
                if (defImportant == null)
                {
                    throw new Exception($"Internal error: default ItemPreset missing configuration for location groups");
                }
                important = defImportant;
            }
            important.Requires ??= new();
            important.Excludes ??= new();
            checkLocationMeta(ImportantMeta, important.Requires, important.Excludes);
            LocationGroups = new() { important };
            // Fill in undefined groups from default
            List<Placement> newPlacements = new();
            List<(PlacementType, Placement)> possiblePlacements = GetEnumValues<PlacementType>()
                .Except(new[] { PlacementType.Unspecified, PlacementType.Custom })
                .Select(type => (type, (Placement)null))
                .Concat(Placements.Where(p => p.Type == PlacementType.Custom).Select(p => (p.Type, p)))
                .ToList();
            foreach ((PlacementType type, Placement concrete) in possiblePlacements)
            {
                Placement place = concrete ?? GetPlacement(type);
                Placement defPlace = def?.GetPlacement(type)?.Clone();
                if (place == null)
                {
                    if (defPlace == null)
                    {
                        throw new Exception($"Internal error: default ItemPreset missing configuration for {type} placement");
                    }
                    place = defPlace;
                }
                PlacementMeta meta = PlacementMetas[type];
                if (!meta.Disable)
                {
                    place.Disabled = false;
                }
                if (!meta.EditItems && defPlace != null)
                {
                    place.Items = defPlace.Items;
                }
                place.Items ??= new();
                place.Items.ForEach(i => ResolveItemRef(i, game, itemGroups, keyAliases));
                // TODO: Should probably also exclude keyitems and questitems groups directly
                place.Items.RemoveAll(i => i.Group == null && i.Keys.Any(k => cantFilterItems.Contains(k)));
                place.Keys = place.Items.SelectMany(i => i.Keys).Distinct().ToList();
                if (!meta.EditLocs && defPlace != null)
                {
                    place.Mode = defPlace.Mode;
                    place.Requires = defPlace.Requires;
                    place.Excludes = defPlace.Excludes;
                }
                if (place.Mode == PlacementMode.Unspecified)
                {
                    place.Mode = PlacementMode.Anywhere;
                }
                if (place.Mode == PlacementMode.Anywhere)
                {
                    place.Requires = null;
                    place.Excludes ??= new();
                }
                else if (place.Mode == PlacementMode.Norandom)
                {
                    place.Requires = null;
                    place.Excludes = null;
                }
                else if (place.Mode == PlacementMode.Custom)
                {
                    place.Requires ??= new();
                    place.Excludes ??= new();
                }
                else if (place.Mode == PlacementMode.Important)
                {
                    // Denormalize this here I suppose
                    place.Requires = new(important.Requires);
                    // We could also merge this with category's excludes
                    place.Excludes = new(important.Excludes);
                }
                else throw new Exception($"Internal error: unknown placement {place.Mode}");
                checkLocationMeta(meta, place.Requires, place.Excludes);
                // Add even if Disabled, it's filtered out in AnnotationData
                newPlacements.Add(place);
            }
            Placements = newPlacements;
            List<PremiumGroup> newPremiumGroups = new();
            List<(PremiumGroupType, PremiumGroup)> possiblePremiumGroups = GetEnumValues<PremiumGroupType>()
                .Except(new[] { PremiumGroupType.Unspecified, PremiumGroupType.Custom })
                .Select(type => (type, (PremiumGroup)null))
                .Concat(PremiumGroups.Where(p => p.Type == PremiumGroupType.Custom).Select(p => (p.Type, p)))
                .ToList();
            foreach ((PremiumGroupType type, PremiumGroup concrete) in possiblePremiumGroups)
            {
                PremiumGroup group = concrete ?? GetPremiumGroup(type);
                PremiumGroup defGroup = def?.GetPremiumGroup(type)?.Clone();
                if (group == null)
                {
                    if (defGroup == null)
                    {
                        throw new Exception($"Internal error: default ItemPreset missing configuration for {type} premium location");
                    }
                    group = defGroup;
                }
                group.Items ??= new();
                group.Items.ForEach(i => ResolveItemRef(i, game, itemGroups, keyAliases));
                // Key items are fine here, as presence in a premium group cannot add more allowable locations
                group.Keys = group.Items.SelectMany(i => i.Keys).Distinct().ToList();
                group.Locations ??= new();
                if (type != PremiumGroupType.Custom && defGroup != null)
                {
                    group.Locations = defGroup.Locations;
                }
                newPremiumGroups.Add(group);
            }
            PremiumGroups = newPremiumGroups;
            if (CustomSilos != null)
            {
                foreach (CustomSilo silo in CustomSilos)
                {
                    if (silo.Type == ItemSiloType.Manual)
                    {
                        silo.Items ??= new();
                        silo.Items.ForEach(i => ResolveItemRef(i, game, itemGroups, keyAliases));
                        // I guess exclude manually specified key items for now.
                        silo.Items.RemoveAll(i => i.Group == null && i.Keys.Any(k => cantFilterItems.Contains(k)));
                        silo.Keys = new HashSet<ItemKey>(silo.Items.SelectMany(i => i.Keys));
                    }
                    else
                    {
                        silo.Items = null;
                        silo.Keys = null;
                    }
                }

                if (CustomSilos.Count == 0)
                {
                    CustomSilos = null;
                }
            }
            BiasWeights?.Clamp();
            if (ItemShards != null)
            {
                // Do a bunch of validation here which is also done in the UI
                if (!itemGroups.TryGetValue("multikey", out List<ItemKey> multiKeys))
                {
                    throw new Exception($"ItemShards is configured in item preset but multikey group does not exist");
                }
                foreach (ItemShard shard in ItemShards)
                {
                    if (shard.Item?.ID == null) throw new Exception($"Invalid item [{shard.Item}] in ItemShards");
                    ResolveItemRef(shard.Item, game, itemGroups, keyAliases);
                    if (shard.Item.Keys.Count != 1) throw new Exception($"Item [{shard.Item}] in ItemShards must be a single item, but found {shard.Item.Keys.Count}");
                    shard.Key = shard.Item.Keys[0];
                    if (!multiKeys.Contains(shard.Key)) throw new Exception($"Item [{shard.Item}] in ItemShards is not eligible for being split up");
                }
            }
        }

        // Automatic UI updates and light fixing of invalid things
        // This is now only called with allSwitch false and no clickOption, because soft syncing is more trouble than it's worth.
        // Hard syncing is needed to make other options work like DLC Start
        public ItemPreset UpdateFromOptions(RandomizerOptions opt, ItemPreset def, bool allSwitch, string clickOption)
        {
            // Look at all ref switches in default, and always remove SwitchOff and add SwitchOn
            // If we can detect specific changes, SwitchOns can be switched off
            // If allSwitch, set all mode switches, and set all refs based on the current value of Switch
            ItemPreset ret = null;
            ItemPreset alt()
            {
                ret ??= Clone();
                return ret;
            }
            ItemPreset processedDef = null;
            // For v1, this synced KeyItemSilo from opt["dlckeysilo"]. Now they're independent, but still make it the default.
            if (Version == 1)
            {
                alt().KeyItemSilo = AreaSiloType.DLC;
            }
            bool updateRefs<T>(List<T> defRef, List<T> newRef, out List<T> rewrite, bool defaultFixed = false) where T : SwitchRef
            {
                rewrite = null;
                if (defRef == null) return false;
                Dictionary<string, bool> correctIds = null;
                foreach (T item in defRef)
                {
                    string id = item.RefID;
                    if (id == null) continue;
                    // SwitchOn/SwitchOff only apply if the switch is active
                    if (item.SwitchOff != null && opt.IsSwitchEnabled(item.SwitchOff))
                    {
                        bool actual = newRef != null && newRef.Any(i => i.RefID == id);
                        if (actual)
                        {
                            correctIds ??= new();
                            correctIds[id] = false;
                        }
                    }
                    else if (item.SwitchOn != null && opt.IsSwitchEnabled(item.SwitchOn) || defaultFixed)
                    {
                        bool actual = newRef != null && newRef.Any(i => i.RefID == id);
                        if (!actual)
                        {
                            correctIds ??= new();
                            correctIds[id] = true;
                        }
                    }
                    else if (allSwitch && item.Switch != null)
                    {
                        bool actual = newRef != null && newRef.Any(i => i.RefID == id);
                        bool correct = opt.IsSwitchEnabled(item.Switch);
                        if (actual != correct)
                        {
                            // Console.WriteLine($"For {id}, {item.Switch} evaluated to {correct}");
                            correctIds ??= new();
                            correctIds[id] = correct;
                        }
                    }
                    // clickOption is a direct expression of user intent, so treat it like a SwitchOff
                    // Assumption option name does not contain regex characters
                    else if (item.SwitchOn != null && clickOption != null && Regex.IsMatch(item.SwitchOn, $@"\b{clickOption}\b") && !opt.IsSwitchEnabled(item.SwitchOn))
                    {
                        bool actual = newRef != null && newRef.Any(i => i.RefID == id);
                        if (actual)
                        {
                            correctIds ??= new();
                            correctIds[id] = false;
                        }
                    }
                }
                if (correctIds == null) return false;
#if DEBUG
                Console.WriteLine($"Found diff: {string.Join(", ", correctIds)}");
#endif
                rewrite = new();
                newRef = newRef == null ? new() : newRef.ToList();
                // Preserve default order. A bit quadratic-y
                foreach (T item in defRef)
                {
                    string id = item.RefID;
                    if (id == null) continue;
                    T exist = null;
                    newRef.RemoveAll(i =>
                    {
                        if (i.RefID == id)
                        {
                            exist = i;
                            return true;
                        }
                        return false;
                    });
                    bool correct;
                    if (exist != null && !(correctIds.TryGetValue(id, out correct) && !correct))
                    {
                        rewrite.Add(exist);
                    }
                    else if (exist == null && correctIds.TryGetValue(id, out correct) && correct)
                    {
                        exist = (T)item.Clone();
                        exist.RemoveSwitch();
                        rewrite.Add(exist);
                    }
                }
                rewrite.AddRange(newRef);
                return true;
            }
            if (updateRefs(def.NorandomItems, NorandomItems, out List<ItemRef> norandomItems))
            {
                alt().NorandomItems = norandomItems;
            }
            if (updateRefs(def.NorandomLocations, NorandomLocations, out List<LocationRef> norandomLocs))
            {
                alt().NorandomLocations = norandomLocs;
            }
            if (updateRefs(def.RemoveItems, RemoveItems, out List<ItemRef> remove))
            {
                alt().RemoveItems = remove;
            }
            LocationGroup defImportant = def.ImportantGroup;
            LocationGroup important = ImportantGroup;
            if (defImportant != null && important != null)
            {
                if (updateRefs(defImportant.Requires, important.Requires, out List<LocationRef> requires))
                {
                    alt().ImportantGroup.Requires = requires;
                }
                if (updateRefs(defImportant.Excludes, important.Excludes, out List<LocationRef> excludes))
                {
                    alt().ImportantGroup.Excludes = excludes;
                }
            }
            for (int i = 0; i < def.Placements.Count; i++)
            {
                Placement defPlace = def.Placements[i];
                PlacementType type = defPlace.Type;
                Placement place = GetPlacement(type);
                if (place == null)
                {
                    // If it's entirely missing, make sure default recommended items are added. Ideally this is done using default options (could be in config?)
                    processedDef ??= def.ProcessBuiltins(opt);
                    place = processedDef.Placements[i].Clone();
                    // new Placement() { Type = defPlace.Type, Mode = defPlace.Mode };
                    // This might put it in a weird order, but still add any default groups
                    alt().Placements.Insert(i, place);
                }
                PlacementMeta meta = PlacementMetas[type];
                if (updateRefs(defPlace.Items, place.Items, out List<ItemRef> items, !meta.EditItems))
                {
                    alt().GetPlacement(type).Items = items;
                }
                if (allSwitch && defPlace.ResolveModeSwitch(opt, out PlacementMode switchMode) && switchMode != place.Mode)
                {
                    alt().GetPlacement(type).Mode = switchMode;
                }
                // Ignore mode for these, just update whatever lists are there
                if (updateRefs(defPlace.Requires, place.Requires, out List<LocationRef> requires, !meta.EditLocs))
                {
                    alt().GetPlacement(type).Requires = requires;
                }
                if (updateRefs(defPlace.Excludes, place.Excludes, out List<LocationRef> excludes, !meta.EditLocs))
                {
                    alt().GetPlacement(type).Excludes = excludes;
                }
            }
            for (int i = 0; i < def.PremiumGroups.Count; i++)
            {
                PremiumGroup defGroup = def.PremiumGroups[i];
                PremiumGroupType type = defGroup.Type;
                PremiumGroup group = GetPremiumGroup(type);
                if (group == null)
                {
                    processedDef ??= def.ProcessBuiltins(opt);
                    group = processedDef.PremiumGroups[i].Clone();
                    alt().PremiumGroups.Insert(i, group);
                }
                if (updateRefs(defGroup.Items, group.Items, out List<ItemRef> items))
                {
                    alt().GetPremiumGroup(type).Items = items;
                }
                // Locations are only editable for custom groups
                if (updateRefs(defGroup.Locations, group.Locations, out List<LocationRef> locations, type != PremiumGroupType.Custom))
                {
                    alt().GetPremiumGroup(type).Locations = locations;
                }
            }
            // Custom silos are not part of default, no need to handle them
            // At this point, do version update. If a preset is used without migrate, Resolve should bring up breaking errors
            if (Version != PresetVersion)
            {
                alt().Version = PresetVersion;
            }
            return ret;
        }

        // Converts the default preset with switches to a usable config preset
        public ItemPreset ProcessBuiltins(RandomizerOptions opt)
        {
            // Done during randomization if there's no specific preset defined, also used as backup if required fields are missing from a defined preset.
            // While editing, disabled builtins should be shown but disabled, but are mostly a suggestion at that point.
            ItemPreset o = Clone();
            bool deleteItem(ItemRef item) => item.FilterRemove(opt);
            bool deleteLoc(LocationRef loc) => loc.FilterRemove(opt);
            o.NorandomItems?.RemoveAll(deleteItem);
            o.NorandomLocations?.RemoveAll(deleteLoc);
            o.RemoveItems?.RemoveAll(deleteItem);
            o.Placements.ForEach(p =>
            {
                p.Items?.RemoveAll(deleteItem);
                p.Requires?.RemoveAll(deleteLoc);
                p.Excludes?.RemoveAll(deleteLoc);
                if (p.ResolveModeSwitch(opt, out PlacementMode switchMode))
                {
                    p.Mode = switchMode;
                }
                p.ModeSwitch = null;
            });
            o.LocationGroups.ForEach(p =>
            {
                p.Requires?.RemoveAll(deleteLoc);
                p.Excludes?.RemoveAll(deleteLoc);
            });
            o.PremiumGroups.ForEach(p =>
            {
                // Locations and Items do not have switches currently
                p.ProcessSwitch(opt);
            });
            // CustomSilos do not have switches
            return o;
        }

        [YamlIgnore]
        public const string FileName = @"presets\CustomItem.txt";

        public void SavePreset()
        {
            Directory.CreateDirectory("presets");
            using (var writer = File.CreateText(FileName))
            {
                GameData.Serializer.Serialize(writer, this);
            }
        }

        public static ItemPreset ParsePreset(string text)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            ItemPreset preset = deserializer.Deserialize<ItemPreset>(text);
            return preset;
        }

        // This method may throw exceptions which the caller should probably catch.
        public static ItemPreset LoadPreset()
        {
            string path = FileName;

            ItemPreset preset;
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = File.OpenText(path))
            {
                preset = deserializer.Deserialize<ItemPreset>(reader);
            }
            return preset;
        }

        [Localize]
        private static readonly Text descItemLineText = new Text("- Items: {0}", "ItemPreset_descItemLine");
        [Localize]
        private static readonly Text descRequireExcludeText = new Text("Placed in {0}. Excludes {1}", "ItemPreset_descRequireExclude");
        [Localize]
        private static readonly Text descRequireExcludeLocText = new Text("{0}. Excludes {1}", "ItemPreset_descRequireExcludeLoc");
        [Localize]
        private static readonly Text descRequireText = new Text("Placed in {0}", "ItemPreset_descRequire");
        [Localize]
        private static readonly Text descExcludeText = new Text("Excludes {0}", "ItemPreset_descExclude");
        [Localize]
        private static readonly Text descEntryText = new Text("{0}: {1}", "ItemPreset_descEntry");
        [Localize]
        private static readonly Text descLineText = new Text("- {0}", "ItemPreset_descLine");
        [Localize]
        private static readonly Text descNoneText = new Text("None", "ItemPreset_descNone");
        [Localize]
        private static readonly Text shardDescTitle = new Text("Key item shards:", "ItemPreset_shardDescTitle");
        [Localize]
        private static readonly Text shardDescEntry = new Text("{0} Shard: {1}/{2} required", "ItemPreset_shardDescEntry");
        // Used by both UI and game edits
        [Localize]
        public static readonly Text ShardName = new Text("{0} Shard", "GameMenu_shardName");

        public static string GetItemGroupName(Messages messages, ItemRef item)
        {
            if (item.Group != null && ItemGroupNames.TryGetValue(item.Group, out Text text))
            {
                string name = messages.Get(text);
                if (item.Content == null && item.Rarity == null)
                {
                    return name;
                }
                string content = item.Content != null && ItemFilterGroupNames.TryGetValue(item.Content, out Text contentText) ? messages.Get(contentText) : item.Content;
                string rarity = item.Rarity != null && ItemFilterGroupNames.TryGetValue(item.Rarity, out Text rarityText) ? messages.Get(rarityText) : item.Rarity;
                if (content != null && rarity != null)
                {
                    content = messages.Get(ItemFilterMultiDesc, content, rarity);
                }
                return messages.Get(ItemFilterGroupDesc, name, content ?? rarity);
            }
            return null;
        }

        // This needs some preprocessing, including all item names (or getItemName), and ideally removing Excludes/Requires from categories which don't need them
        public string FullDescription(Messages messages, Func<ItemRef, string> getItemName = null)
        {
            // This function is only required to return names for items with IDs
            getItemName ??= item => item.Name;
            StringBuilder ret = new StringBuilder();
            int lastLen = 0;
            void newline()
            {
                if (lastLen != ret.Length)
                {
                    ret.AppendLine();
                }
                lastLen = ret.Length;
            }
            string sep = messages.Get(EnemyPreset.PoolDelimitEntryText);
            string itemText(ItemRef item)
            {
                if (item.ID != null)
                {
                    return getItemName(item);
                }
                return GetItemGroupName(messages, item);
            }
            string locText(LocationRef loc)
            {
                if (loc.Tag != null && LocationGroupNames.TryGetValue(loc.Tag, out Text text))
                {
                    return messages.Get(text);
                }
                return null;
            }
            string combine(string label, string text)
            {
                // TODO: Account for spaces
                return label + " " + text;
            }
            if (NorandomItems.Count > 0 || NorandomLocations.Count > 0)
            {
                ret.AppendLine(combine(messages.Get(NorandomItemsText), string.Join(sep, NorandomItems.Select(itemText).Concat(NorandomLocations.Select(locText)))));
            }
            if (RemoveItems.Count > 0)
            {
                ret.AppendLine(combine(messages.Get(RemoveItemsText), string.Join(sep, RemoveItems.Select(itemText))));
            }
            HashSet<string> norandomIds = new(NorandomItems.Select(i => i.RefID));
            // At the moment, no categories allow editing both items and locs, so return one or the other
            // Can return null if no explicit items/locs
            (string, string) groupText(List<ItemRef> items, List<LocationRef> requires, List<LocationRef> excludes, PlacementMeta meta)
            {
                string item = null, loc = null;
                bool locOnly = items == null;
                if (meta.EditItems)
                {
                    if (items != null && items.Count > 0)
                    {
                        item = string.Join(sep, items.ExceptBy(norandomIds, i => i.RefID).Select(itemText));
                    }
                }
                if (meta.EditLocs)
                {
                    // TODO: Is empty requires a nothing or anything? Does it matter?
                    requires ??= new();
                    excludes ??= new();
                    List<string> requireText = (requires ?? new()).Select(locText).ToList();
                    // If the only tag is missable and it must be there, don't include it
                    List<string> excludeText = (excludes ?? new()).Where(i => meta.AllowMissable || i.Tag != "missable").Select(locText).ToList();
                    if (requireText.Count > 0 && excludeText.Count > 0)
                    {
                        loc = messages.Get(locOnly ? descRequireExcludeLocText : descRequireExcludeText, string.Join(sep, requireText), string.Join(sep, excludeText));
                    }
                    else if (requireText.Count > 0)
                    {
                        loc = locOnly ? string.Join(sep, requireText) : messages.Get(descRequireText, string.Join(sep, requireText));
                    }
                    else if (excludeText.Count > 0)
                    {
                        loc = messages.Get(descExcludeText, string.Join(sep, excludeText));
                    }
                    else
                    {
                        // Empty requires now means anywhere
                        loc = messages.Get(PlacementModeNames[PlacementMode.Anywhere]);
                    }
                }
                return (item, loc);
            }
            newline();
            {
                string name = messages.Get(PlacementModeNames[PlacementMode.Important]);
                string desc = groupText(null, ImportantGroup.Requires, ImportantGroup.Excludes, ImportantMeta).Item2;
                desc ??= messages.Get(descNoneText);
                ret.AppendLine(messages.Get(descEntryText, name, desc));
            }
            newline();
            foreach (Placement place in Placements)
            {
                if (place.Disabled) continue;
                if (place.Mode == PlacementMode.Unspecified || place.Type == PlacementType.Unspecified) continue;
                string name = string.IsNullOrEmpty(place.Name) ? messages.Get(PlacementTypeNames[place.Type]) : place.Name;
                // For fixed items: '<name>: <locations>', either mode or list of locations, with non-default excludes
                // For fixed locations: '<name>: <items>'
                // Otherwise: '<name>: <locations>\n- Items: <items>'
                string desc;
                string extra = null;
                List<LocationRef> requires = place.Requires;
                List<LocationRef> excludes = place.Excludes;
                // Same logic as above for allowed types
                if (place.Mode != PlacementMode.Custom) requires = null;
                (string item, string loc) = groupText(place.Items, requires, excludes, PlacementMetas[place.Type]);
                if (place.Mode == PlacementMode.Norandom || place.Mode == PlacementMode.Important)
                {
                    // This ignores exclude tag and doesn't put "Anywhere" hardcoded above
                    loc = messages.Get(PlacementModeNames[place.Mode]);
                }
                desc = loc ?? item;
                if (desc == null)
                {
                    desc = messages.Get(descNoneText);
                }
                else if (loc != null && item != null)
                {
                    extra = messages.Get(descItemLineText, item);
                }
                ret.AppendLine(messages.Get(descEntryText, name, desc));
                if (extra != null) ret.AppendLine(extra);
            }
            newline();
            foreach (PremiumGroup group in PremiumGroups)
            {
                if (group.Disabled) continue;
                if (group.Type == PremiumGroupType.Unspecified) continue;
                string name = string.IsNullOrEmpty(group.Name) ? messages.Get(PremiumGroupTypeNames[group.Type]) : group.Name;
                // For fixed locations: '<name>: <items>'
                // Otherwise, '<name>: <locations>\n- Items: <items>'
                string item = string.Join(sep, group.Items.Select(itemText));
                if (group.Type == PremiumGroupType.Custom)
                {
                    string locations = string.Join(sep, group.Locations.Select(locText));
                    ret.AppendLine(messages.Get(descEntryText, name, locations));
                    ret.AppendLine(messages.Get(descItemLineText, item));
                }
                else
                {
                    ret.AppendLine(messages.Get(descEntryText, name, item));
                }
            }
            newline();
            if (ItemShards != null && ItemShards.Count > 0)
            {
                ret.AppendLine(messages.Get(shardDescTitle));
                foreach (ItemShard shard in ItemShards)
                {
                    ret.AppendLine(messages.Get(descLineText, messages.Get(shardDescEntry, itemText(shard.Item), shard.ReqCount, shard.Count)));
                }
            }
            newline();
            if (OverallSilo != AreaSiloType.None)
            {
                ret.AppendLine(combine(messages.Get(OverallSiloText), messages.Get(AreaSiloTypeNames[OverallSilo])));
            }
            if (KeyItemSilo != OverallSilo)
            {
                ret.AppendLine(combine(messages.Get(KeyItemSiloText), messages.Get(AreaSiloTypeNames[KeyItemSilo])));
            }
            newline();
            if (CustomSilos.Any(s => !s.Disabled))
            {
                ret.AppendLine(messages.Get(ItemSiloText));
                foreach (CustomSilo silo in CustomSilos)
                {
                    if (silo.Disabled)
                    {
                        continue;
                    }
                    if (silo.Type == ItemSiloType.None) continue;
                    if (silo.Type == ItemSiloType.Manual)
                    {
                        string item = string.Join(sep, silo.Items.Select(itemText));
                        ret.AppendLine(messages.Get(descLineText, item));
                    }
                    else
                    {
                        string name = messages.Get(ItemSiloTypeNames[silo.Type]);
                        ret.AppendLine(messages.Get(descLineText, name));
                    }
                }
            }
            return ret.ToString();
        }

        // From: cat itemslots.txt | dos2unix | grep Tags: | tr -s ' ' '\n' | sort | uniq -c | sort -nr
        // Also: sed -e 's/^\(.*\)$/["\1"] = "",/'
        // TODO: shop/nonshop?
        [Localize]
        public static readonly Dictionary<string, Text> LocationGroupNames = MapText<string>("ItemPreset_LocationGroup", new()
        {
            ["altboss"] = "Non-major bosses",
            ["basin"] = "Crystal Tear basins",
            ["bluegraves"] = "Night spiritgraves",
            ["boss"] = "Major bosses",
            ["chest"] = "Chests",
            ["church"] = "Sacred Tear churches",
            ["cookbook"] = "Cookbook locations",
            ["cross"] = "Miquella's Cross Scadutree Fragments",
            ["dlc"] = "DLC",
            ["enemyfragment"] = "Scadutree Fragment enemy drops",
            ["enemyrevered"] = "Revered Spirit Ash enemy drops",
            ["enemytalisman"] = "Talisman enemy drops",
            ["fragment"] = "Other Scadutree Fragment pickups",
            ["forgereward"] = "Forge rewards",
            ["furnacegolem"] = "Furnace Golems",
            ["rise"] = "Rises",
            ["minidungeon"] = "Minidungeon",
            ["missable"] = "Missable",
            ["night"] = "Night bosses",
            ["nodlc"] = "Base game",
            ["painting"] = "Paintings",
            ["racemode"] = "Major key item locations",
            ["raceshop"] = "NPC shops",
            ["revered"] = "Revered Spirit Ash pickups",
            ["seedtree"] = "Golden Seed trees",
            ["talisman"] = "Talisman pickups",
            ["upgradeshop"] = "Upgrade Bell Bearing locations",
            // Boss things added
            ["dragonboss"] = "Overworld dragon minibosses",
            ["evergaol"] = "Evergaol bosses",
            ["namelessmausoleum"] = "Nameless Mausoleum bosses",
            ["minidungeonboss"] = "Minidungeon bosses",
            ["overworldboss"] = "Overworld minibosses",
            ["caveboss"] = "Cave bosses",
            ["catacombboss"] = "Catacombs bosses",
            ["tunnelboss"] = "Tunnel bosses",
            ["graveboss"] = "Hero's Grave bosses",
            ["gaolboss"] = "Gaol bosses",
            ["cellarbosschest"] = "Chests after cellar bosses",
            ["remembranceboss"] = "Remembrance bosses",
            ["hangingpot"] = "Hanging pots",
            ["criticalpath"] = "Non-major critical path bosses",
            // Location things added
            ["cave"] = "Caves",
            ["catacomb"] = "Catacombs",
            ["tunnel"] = "Tunnels",
            ["grave"] = "Hero's Graves",
            ["gaol"] = "Gaols",
            ["forge"] = "Forges",
            // Requirable locations for v2 presets
            ["questreward"] = "Quest rewards",
            ["paintingreward"] = "Painting rewards",
            ["legendary"] = "Legendary item pickups",
            ["statue"] = "Glowing statues",
            ["sorceryshop"] = "Sorcery shops",
            ["incantationshop"] = "Incantation shops",
            ["gemshop"] = "Ash of War shops",
            ["dragonshop"] = "Dragon Communion shops",
            ["bosssoulshop"] = "Remembrance shop",
            ["bossarmorshop"] = "Boss armor shop",
            // Enemy and item-based locations for v2 presets
            ["enemy"] = "One-time enemy drops",
            ["enemyinf"] = "Farmable enemy drops",
            ["weapon"] = "Weapon pickups",
            ["sorcery"] = "Sorcery pickups",
            ["incantation"] = "Incantation pickups",
            ["gem"] = "Ash of War pickups",
            ["enemyweapon"] = "One-time weapon enemy drops",
            ["enemysorcery"] = "Sorcery enemy drops",
            ["enemyincantation"] = "Incantation enemy drops",
            ["enemygem"] = "Ash of War enemy drops",
            ["scarab"] = "Scarab drops",
            ["scroll"] = "Scroll locations",
            ["prayerbook"] = "Prayerbook locations",
            ["tarnished"] = "Tarnished Pack locations",
        });
        // There could be a system for grouping things together - especially Other Bosses under All Bosses
        // Gets a bit messier for other things, like Scadutree Fragments, which makes it no longer sorted under S
        public static readonly HashSet<string> RequirableTags = new()
        {
            "altboss", "bluegraves", "basin", "boss", "chest", "church", "cookbook", "cross",
            "enemyfragment", "enemyrevered", "enemytalisman", "fragment", "forgereward", "furnacegolem",
            "rise", "night", "painting", "racemode", "raceshop", "revered", "seedtree", "talisman", "upgradeshop",
            "dragonboss", "evergaol", "namelessmausoleum", "minidungeonboss", "overworldboss",
            "caveboss", "catacombboss", "tunnelboss", "graveboss", "gaolboss", "cellarbosschest", "remembranceboss", "hangingpot", "criticalpath",
            "questreward", "paintingreward", "legendary", "statue", "sorceryshop", "incantationshop", "gemshop", "dragonshop", "bosssoulshop", "bossarmorshop", "scarab", "scroll", "prayerbook",
            "enemyweapon", "enemysorcery", "enemyincantation", "enemygem",
            "weapon", "sorcery", "incantation", "gem",
            "tarnished",
        };
        public static readonly HashSet<string> DlcTags = new() { "dlc", "nodlc" };
        [Localize]
        public static readonly Dictionary<string, Text> ItemGroupNames = MapText<string>("ItemPreset_ItemGroup", new()
        {
            ["keyitems"] = "Major Key Items",
            ["greatrunes"] = "Great Runes",
            ["questitems"] = "Quest Items",
            ["paintings"] = "Paintings",
            ["scrolls"] = "Scrolls",
            ["prayerbooks"] = "Prayerbooks",
            ["bosssouls"] = "Remembrances",
            ["itemashes"] = "Material Bell Bearings",
            ["upgradeashes"] = "Upgrade Bell Bearings",
            // ["upgrades"] = "",
            ["whetblades"] = "Whetblades",
            ["cookbooks"] = "Cookbooks",
            ["containers"] = "Crafting Containers",
            ["crystaltears"] = "Crystal Tears",
            ["souls"] = "Consumable Runes",
            ["regularstones"] = "Regular Smithing Stones",
            ["somberstones"] = "Somber Smithing Stones",
            ["sorceries"] = "Sorceries",
            ["incantations"] = "Incantations",
            // More big classes
            ["weapons"] = "Weapons",
            ["arrows"] = "Arrows/Bolts",
            ["protectors"] = "Armor",
            ["accessories"] = "Talismans",
            ["gems"] = "Ashes of War",
            ["craftingmats"] = "Crafting Materials",
            ["spiritashes"] = "Spirit Ashes",
            ["tarnished"] = "Tarnished Pack Items",
        });
        // Filter groups based on item attributes (not availability/location)
        // In theory these could be standalone categories, but as this amounts to adding "all items", make sure that doesn't break things first.
        [Localize]
        public static readonly Dictionary<string, Text> ItemFilterGroupNames = MapText<string>("ItemPreset_ItemFilterGroup", new()
        {
            ["rare"] = "Rare",
            ["legendary"] = "Legendary",
            ["nodlc"] = "Base game",
            ["dlc"] = "DLC",
        });
        public static readonly HashSet<string> OtherConfigGroupNames = new() { "multikey" };
        // [Localize]
        // public static readonly Text ItemFilterNoneText = new Text("No filter", "ItemPreset_ItemFilterNone");
        [Localize]
        public static readonly Text ItemFilterGroupDesc = new Text("{0} ({1})", "ItemPreset_ItemFilterGroupDesc");
        [Localize]
        public static readonly Text ItemFilterMultiDesc = new Text("{0} & {1}", "ItemPreset_ItemFilterMultiDesc");
        // Groups and items in groups to not place. No longer includes furnace pots.
        // Also groups only including quest items. TODO make quest placement much better.
        // These are also not filterable at the moment.
        public static readonly HashSet<string> NoPlacementItemGroups = new() { "keyitems", "greatrunes", "questitems", "scrolls", "prayerbooks", "paintings" };
        public static readonly HashSet<string> RarityFilterItemGroups = new() { "rare", "legendary" };
        public static readonly HashSet<string> ContentFilterItemGroups = new() { "nodlc", "dlc" };

        // From other files
        public static bool TryParseItemID(string idStr, out ItemKey key)
        {
            key = null;
            string[] parts = idStr.Split(':');
            if (parts.Length == 2 && ItemNameTypes.TryGetValue(parts[0], out ItemType type) && int.TryParse(parts[1], out int id))
            {
                key = new ItemKey(type, id);
                return true;
            }
            return false;
        }

        // Logic for serialization. This comes from MergeModManifest, which should probably borrow it from here.
        // keyAliases is only used for custom weapons at present, as otherwise these are unaddressable in the config.
        public static void ResolveItemRef(ItemRef item, GameData game, IDictionary<string, List<ItemKey>> itemGroups, IDictionary<ItemKey, List<ItemKey>> keyAliases)
        {
            if (item.Group != null)
            {
                if (!ItemGroupNames.ContainsKey(item.Group) || !itemGroups.TryGetValue(item.Group, out List<ItemKey> groupKeys))
                {
                    // TODO: Should these error out? Or just get silently removed
                    throw new Exception($"Error in item preset: group '{item.Group}' is not defined or valid");
                }
                List<ItemKey> keys = groupKeys;
                if (item.Rarity != null)
                {
                    if (!RarityFilterItemGroups.Contains(item.Rarity) || !itemGroups.TryGetValue(item.Rarity, out List<ItemKey> filterKeys))
                    {
                        throw new Exception($"Error in item preset: rarity group '{item.Rarity}' is not defined or valid");
                    }
                    keys = keys.Intersect(filterKeys).ToList();
                }
                if (item.Content != null)
                {
                    if (!ContentFilterItemGroups.Contains(item.Content) || !itemGroups.TryGetValue(item.Content, out List<ItemKey> filterKeys))
                    {
                        throw new Exception($"Error in item preset: content group '{item.Content}' is not defined or valid");
                    }
                    keys = keys.Intersect(filterKeys).ToList();
                }
                if (keys.Count == 0)
                {
                    throw new Exception($"Error in item preset: no items in group {item.Group} with filter {item.Rarity}+{item.Content}");
                }
                item.Keys = keys;
            }
            else if (item.ID != null)
            {
                if (TryParseItemID(item.ID, out ItemKey key))
                {
                    List<ItemKey> keys = new() { key };
                    if (keyAliases != null && keyAliases.TryGetValue(key, out List<ItemKey> altKeys))
                    {
                        keys.AddRange(altKeys);
                    }
                    item.Keys = keys;
                }
                else throw new Exception($"Error in item preset: name '{item}' is not a vanilla item name or an item id.");
            }
            // TODO: Use different name mapping instead of game's mapping, for loading config files at least, or just require ID
            else if (item.Name != null && game.RevItemNames.TryGetValue(item.Name, out List<ItemKey> nameKeys))
            {
                if (nameKeys.Count != 1)
                {
                    throw new Exception(
                        $"Error in item preset: name '{item.Name}' is ambiguous "
                        + $"between {string.Join(" and ", nameKeys.Select(FormatItemKey))}. Use those IDs in the config instead.");
                }
                ItemKey key = nameKeys[0];
                item.ID = FormatItemKey(key);
                List<ItemKey> keys = new() { key };
                if (keyAliases != null && keyAliases.TryGetValue(key, out List<ItemKey> altKeys))
                {
                    keys.AddRange(altKeys);
                }
                item.Keys = keys;
            }
            else
            {
                throw new Exception($"Error in item preset: item given with no valid Group, ID, or Name");
            }
        }

        public static string FormatItemKey(ItemKey key)
        {
            if (!ItemTypeNames.TryGetValue(key.Type, out string typeName)) typeName = "invalid";
            return $"{typeName}:{key.ID}";
        }

        public static readonly Dictionary<string, ItemType> ItemNameTypes = new()
        {
            ["weapon"] = ItemType.Weapon,
            ["protector"] = ItemType.Protector,
            ["accessory"] = ItemType.Accessory,
            ["goods"] = ItemType.Goods,
            ["gem"] = ItemType.Gem,
            ["custom"] = ItemType.Custom,
        };
        public static readonly Dictionary<ItemType, string> ItemTypeNames = ItemNameTypes.ToDictionary(e => e.Value, e => e.Key);
    }
}
