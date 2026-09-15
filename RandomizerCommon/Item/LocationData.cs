using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    // These classes are awful and contain ugly Java-esque boilerplate (TODO rewrite)
    // But also C#'s lack of initialize-only fields is a travesty and both languages should feel bad.
    public class LocationData
    {
        // Location information for all items in the game, with additional splitting by how to acquire the item.
        public readonly SortedDictionary<ItemKey, ItemLocations> Items = new();
        // Index of locations by how/when they are available. If one slot in the set is available, all the others are too.
        public readonly SortedDictionary<LocationScope, List<ItemLocKey>> Locations = new();
        // Replacement lots, used to give treasure carps event drops, from entity id to lot id (Sekiro only)
        public Dictionary<uint, int> NewEntityLots = new();
        // Boss entity to flag mapping, for item locations with boss entities, for Elden Ring hints
        public Dictionary<uint, uint> BossFlags = new();

        public LocationData() { }

        public ItemLocation AddLocations(ItemKey item, ItemScope scope, LocationScope locScope, IEnumerable<Location> locs)
        {
            if (!Items.TryGetValue(item, out ItemLocations itemLocs))
            {
                Items[item] = itemLocs = new ItemLocations();
            }
            if (!itemLocs.Locations.TryGetValue(locScope, out ItemLocation itemLoc))
            {
                itemLocs.Locations[locScope] = itemLoc = new ItemLocation(item, scope, locScope);
            }
            itemLoc.Locs.AddRange(locs);
            if (!Locations.TryGetValue(locScope, out List<ItemLocKey> keys))
            {
                Locations[locScope] = keys = new List<ItemLocKey>();
            }
            keys.Add(itemLoc.Key);
            return itemLoc;
        }

        public ItemLocation AddLocationlessItem(ItemKey item)
        {
            LocationScope locScope = MakeSpecialScope();
            ItemScope scope = new ItemScope(ScopeType.Special, locScope.UniqueID);
            if (!Items.ContainsKey(item))
            {
                // This must be done after the data scraper adds all locations for an item
                Items[item] = new ItemLocations();
                Items[item].Unique = true;
            }
            // This is a bit awkward with multiple instances of the same item, as they will all have the same location,
            // just multiple SlotKeys. They are all added as distinct sources only because of ScopeType.Special.
            // Adding quantity to SlotKey would be weird, so these would probably be unique ItemScope/ItemLocation/LocationScopes.
            return AddLocations(item, scope, locScope, Array.Empty<Location>());
        }

        private int specialScopeId = 1;
        private LocationScope MakeSpecialScope()
        {
            // Use FromGame.ER for now as this only determines the serialization format, but these locations will not be serialized.
            return new LocationScope(FromGame.ER, ScopeType.Special, specialScopeId++, new SortedSet<int>(), new SortedSet<int>(), false);
        }

        public ItemLocation this[ItemLocKey key] => Items[key.Item].Locations[key.Scope];

        public ItemLocation GetItemLoc(ItemLocKey key)
        {
            if (!Items.TryGetValue(key.Item, out ItemLocations locs))
            {
                throw new Exception($"Internal error: {key.Item} has no known locations");
            }
            if (!locs.Locations.TryGetValue(key.Scope, out ItemLocation loc))
            {
                throw new Exception($"Internal error: {key.Item} has no locations for {key.Scope}");
            }
            return loc;
        }

        public List<ItemLocKey> GetBaseItemLocs(LocationScope scope)
        {
            // Is this actually correct? (Does it exclude items with different event flags in non-base lots?)
            Func<ItemLocKey, bool> isTarget = targetKey => GetItemLoc(targetKey).Locs.Any(k => k.OtherBase == null);
            List<ItemLocKey> bases = Locations[scope].Where(isTarget).ToList();
            if (bases.Count() == 0)
            {
                return new List<ItemLocKey> { Locations[scope][0] };
            }
            return bases;
        }

        // All ways to get a specific item.
        public class ItemLocations
        {
            // This used to be keyed by ItemScope, so the Locations dictionary had to use SlotKey <ItemKey, ItemScope> to look up an ItemLocation by config slot.
            // Now ItemLocKey <ItemKey, LocationScope> is used across the board and ItemScope is mostly obsolete as a key.
            public readonly SortedDictionary<LocationScope, ItemLocation> Locations;
            // For placement heuristic only
            public bool Unique { get; set; }
            public ItemLocations()
            {
                Locations = new();
                Unique = false;
            }
        }

        // One way to get a specific item. When the LocationKeys here are changed, they are all changed together.
        public class ItemLocation
        {
            public readonly ItemKey Item;
            public readonly List<Location> Locs;
            // Forms the ItemLocKey for this item (which is just the double-key for lookup in Items dict), and is used for slot annotations.
            public readonly LocationScope LocScope;
            // This is used within the data scraper but could be removed if desired.
            // At this point, it's only used for identifying the event flag for this location if one exists, as well as model id for Elden Ring DLC classification.
            public readonly ItemScope Scope;

            // Silo info goes here, though it's only filled in in the AnnotationData pass.
            // Sometimes the same LocationScope can be split across different silos, like in Elden Ring remembrance shop.
            public bool DLC { get; set; }
            public AreaSilo Silo { get; set; } = AreaSilo.Default;
            // Name for this source, if KeyCount is set for the item. If this is set for one location for a finite item, it's set for all of them.
            public string ItemName { get; set; }

            internal ItemLocation(ItemKey item, ItemScope Scope, LocationScope LocScope)
            {
                Item = item;
                Locs = new List<Location>();
                this.Scope = Scope;
                this.LocScope = LocScope;
            }

            // TODO: Use this
            public ItemLocKey Key => new ItemLocKey(Item, LocScope);

            public SortedSet<string> GetLocations()
            {
                return new SortedSet<string>(Locs.SelectMany(k => k.Entities.Select(e => e.MapName)).Where(m => m != ""));
            }

            public int Quantity => Math.Max(1, Locs.Select(k => k.Quantity).DefaultIfEmpty().Min());
            public override string ToString() => string.Join(", ", Locs);
        }

        // Silos
        // Additional config
        public enum AreaSiloType
        {
            None,
            DLC,
            Region,
            // Can add even lower level if needed
        }

        // In development. There's some code in Permutation but may still have weird interactions with key item placement and area silos.
        public enum ItemSiloType
        {
            None,
            // Items given manually
            Manual,
            // For all weapons, the weapon type
            WeaponType,
            // Which body part
            ArmorType,
        }

        // Area sub-silos, which always apply to all locations when enabled.
        // Index is index into region list, or 0/1 for DLC
        public record AreaSilo(AreaSiloType Type, int Index, string Name = null) : IComparable<AreaSilo>
        {
            public static readonly AreaSilo Default = new AreaSilo(AreaSiloType.None, 0);
            public static AreaSilo ForDlc(bool dlc) => new AreaSilo(AreaSiloType.DLC, dlc ? 1 : 0, dlc ? "dlc" : "base");

            public bool IsDefault() => Type == AreaSiloType.None;
            public bool IsBaseGame() => Type == AreaSiloType.DLC && Index == 0;
            public bool IsDlc() => Type == AreaSiloType.DLC && Index == 1;

            public int CompareTo(AreaSilo o) => (Type, Index).CompareTo((o.Type, o.Index));
        }

        // For items there can either by a manual sub-silo or item sub-silo, like weapon type.
        // Key items are not allowed to be in item silos.
        // The index is index into config for manual silos, or the param enum value for automatic ones.
        public record ItemSilo(ItemSiloType Type, int Index, string Name = null) : IComparable<ItemSilo>
        {
            public static readonly ItemSilo Default = new ItemSilo(ItemSiloType.None, 0);

            public bool IsDefault() => Type == ItemSiloType.None;

            public int CompareTo(ItemSilo o) => Type.CompareTo(o.Type);
        }

        // Keys
        // Do NOT cast this, use name lookup or GameData helpers. Remaining casts have a comment: Dependency on ItemType value
        public enum ItemType
        {
            Weapon = 0,
            Protector = 1,
            Accessory = 2,
            Goods = 3,
            Gem = 4,
            // This could also be CustomWeapon
            Custom = 5,
        }

        public class ItemKey : IComparable<ItemKey>
        {
            public readonly ItemType Type;
            public readonly int ID;
            public ItemKey(ItemType Type, int ID)
            {
                this.Type = Type;
                this.ID = ID;
            }

            public override string ToString()
            {
                return $"{Type}:{ID}";
            }
            public override bool Equals(object obj) => obj is ItemKey o && Equals(o);
            public bool Equals(ItemKey o) => o != null && Type == o.Type && ID == o.ID;
            public override int GetHashCode() => ((int)Type) << 28 ^ ID;
            public int CompareTo(ItemKey o) => Nest(Type.CompareTo(o.Type), ID.CompareTo(o.ID));
        }

        // Across all items, the conditions for acquiring it. <ItemKey, LocationScope> now *does* map 1:1 with <ItemKey, ItemScope>
        // It's the key used for slot annotations, and for identifying distinct ItemLocation for an item
        public class LocationScope : IComparable<LocationScope>
        {
            public readonly ScopeType Type;
            // Event id, or entity id, or material id.
            public readonly int UniqueID;
            public readonly SortedSet<int> ShopIDs;
            public readonly SortedSet<int> ModelLots;
            // Additional info
            public readonly bool OnlyShops;
            private readonly string IDStr;
            public LocationScope(FromGame game, ScopeType Type, int UniqueID, SortedSet<int> ShopIDs, SortedSet<int> ModelLots, bool OnlyShops)
            {
                this.Type = Type;
                this.UniqueID = UniqueID;
                this.ShopIDs = ShopIDs;
                this.ModelLots = ModelLots;
                this.OnlyShops = OnlyShops;
                // Group shops together as much as possible, removing events
                int id = UniqueID;
                // TODO: Migrate to Elden Ring format as it has better ordering
                if (OnlyShops)
                {
                    id = game == FromGame.ER ? 0 : -1;
                }
                IDStr = game == FromGame.ER
                    ? $"{(int)Type}:{id.ToString("0000000000")}:{string.Join(",", ShopIDs)}:{string.Join(",", ModelLots)}"
                    : $"{(int)Type}:{id}:{string.Join(",", ShopIDs)}:{string.Join(",", ModelLots)}";
            }
            public override string ToString()
            {
                return IDStr;
            }
            public string Description()
            {
                return scopeDesc[(int)Type];
            }
            public override bool Equals(object obj) => obj is LocationScope o && Equals(o);
            public bool Equals(LocationScope o) => IDStr == o.IDStr;
            public override int GetHashCode() => IDStr.GetHashCode();
            public int CompareTo(LocationScope o) => IDStr.CompareTo(o.IDStr);
        }

        public enum ScopeType
        {
            // Finite items, as enforced by having an event ID. (SHOP_INFINITE_EVENT can also have event IDs, use EventID property to check.)
            Event,
            // Finite items enforced by one drop per entity ID. Only present if no event flag in the item lot.
            // (These do also have event flags - record those too?)
            Entity,
            // Boss soul weapons, by boss soul item ID. These have no event flag, but all have quantity of 1.
            // The boss does need to be beaten (or area entered) for the item to appear in the shop.
            Material,
            // Infinite shop items. These have no event IDs.
            ShopInfinite,
            // Shop items which can start out as having an event, but become infinite. The ID is the event ID before becoming infinite.
            ShopInfiniteEvent,
            // Infinite items farmables from some enemy in the map. Only present if no event flag.
            // There are other ways to split up farming, but item + model seems pretty good.
            Model,
            // Unique item, not acquirable through normal means
            Special,
            // Resource collected from an asset.
            Asset,
        }

        private static List<string> scopeDesc = new List<string>() { "Unique location", "Unique drop", "Boss weapon", "Shop item", "Shop item", "Enemy drop", "Unknown location" };

        // *Within* a given item, the different ways of acquiring it. Mostly used within data scrapers and for location event flags.
        public class ItemScope : IComparable<ItemScope>
        {
            private readonly HashSet<ScopeType> eventTypes = new HashSet<ScopeType> { ScopeType.Event, ScopeType.ShopInfiniteEvent };
            public readonly ScopeType Type;
            public readonly int ID;
            public readonly int EventID;
            public ItemScope(ScopeType Type, int ID)
            {
                this.Type = Type;
                this.ID = ID;
                this.EventID = eventTypes.Contains(Type) ? ID : -1;
            }
            public override string ToString()
            {
                return $"{Type}:{ID}";
            }
            public override bool Equals(object obj) => obj is ItemScope o && Equals(o);
            public bool Equals(ItemScope o) => Type == o.Type && ID == o.ID;
            public override int GetHashCode() => ((int)Type) << 28 ^ ID;
            public int CompareTo(ItemScope o) => Nest(Type.CompareTo(o.Type), ID.CompareTo(o.ID));
        }

        public class Location
        {
            public enum LocationType
            {
                Lot, Shop, External
            }
            public readonly LocationType Type;
            public readonly int ID;
            // For debugging
            public readonly string Text;
            // Auxiliary
            public readonly List<Entity> Entities;
            public readonly int Quantity;
            public readonly float Chance;
            public readonly string Subtype;
            public readonly long ExternalID;
            // For a lot, the base lot location, if this is an additional draw. Otherwise null
            public readonly Location OtherBase;

            public Location(
                LocationType Type, int ID, string Text,
                List<Entity> Entities, int Quantity, float Chance, Location OtherBase, string Subtype = null, long ExternalID = 0)
            {
                this.Type = Type;
                this.ID = ID;
                this.Text = Text;
                this.Entities = Entities;
                this.Quantity = Quantity;
                this.Chance = Chance;
                this.OtherBase = OtherBase;
                this.Subtype = Subtype;
                this.ExternalID = ExternalID;
                if (OtherBase != null && OtherBase.Type != Type)
                {
                    throw new Exception($"Bad base {OtherBase} for {Text}");
                }
                _maxSlots = 1;
            }

            public int BaseID => BaseLocation.ID;
            public Location BaseLocation => OtherBase ?? this;

            // The available slots in this location. If this is a non-base item in an item lot, this forwards to the base lot.
            private int _maxSlots;
            public int MaxSlots
            {
                get
                {
                    if (OtherBase != null)
                    {
                        // Access field directly - these should only be one deep
                        return OtherBase._maxSlots;
                    }
                    return _maxSlots;
                }
                set
                {
                    if (Type == LocationType.Shop || OtherBase != null)
                    {
                        throw new Exception($"{this} can have no properties - not base lot");
                    }
                    if (value < 1)
                    {
                        throw new Exception($"Cannot set {this} to {value} slots");
                    }
                    _maxSlots = value;
                }
            }

            public string ParamName
            {
                get
                {
                    if (Type == LocationType.External) throw new Exception($"External location has no param has no param: {this}");
                    return (Type == LocationType.Lot ? "ItemLotParam" : "ShopLineupParam") + (Subtype == null ? "" : $"_{Subtype}");
                }
            }

            public override string ToString() => Text;
            // Are these really needed?
            public override bool Equals(object obj) => obj is Location o && Equals(o);
            public bool Equals(Location o) => Type == o.Type && ID == o.ID && Subtype == o.Subtype;
            public override int GetHashCode() => ((int)Type) << 28 ^ ID ^ (Subtype?.GetHashCode() ?? 0);
        }

        public class ItemLocKey : IComparable<ItemLocKey>
        {
            public readonly ItemKey Item;
            public readonly LocationScope Scope;
            public ItemLocKey(ItemKey Item, LocationScope Scope)
            {
                this.Item = Item;
                this.Scope = Scope;
            }
            public override string ToString() => $"({Item},{Scope})";

            public override bool Equals(object obj) => obj is ItemLocKey o && Equals(o);
            public bool Equals(ItemLocKey o) => Item == o.Item && Scope == o.Scope;
            public override int GetHashCode() => Item.GetHashCode() ^ Scope.GetHashCode();
            public int CompareTo(ItemLocKey o) => Nest(Item.CompareTo(o.Item), Scope.CompareTo(o.Scope));
            public string ToSimpleId() => $"{Item.Type},{Item.ID},{Scope.Type},{Scope.UniqueID}";
        }

        public class Entity : IComparable<Entity>
        {
            // Entity name if extracted from a map. Any string otherwise
            public string Name { get; set; }
            // Map name, if extracted from a map. Empty string otherwise
            public string MapName { get; set; }

            public uint EntityID { get; set; }
            public int CharaInitID { get; set; }
            
            // Mainly internal display info. Not defined for all games
            public int NPCParamID { get; set; }
            public int AssetID { get; set; }
            public List<uint> GroupIDs { get; set; }
            public string Type { get; set; }  // e.g. enemy, object
            public Vector3? Position { get; set; }
            public int NameID { get; set; }
            public int TalkID { get; set; }
            // This is a bit messy. Covers _01 and _02 maps in Elden Ring, for display purposes.
            public string OriginalMapName { get; set; }

            public Entity() { }
            public Entity(
                string MapName, string Name,
                uint EntityID=0, int NPCParamID=-1, int CharaInitID=-1, List<uint> GroupIDs=null,
                string Type=null)
            {
                this.MapName = MapName;
                this.Name = Name;
                this.EntityID = EntityID;
                this.NPCParamID = NPCParamID;
                this.CharaInitID = CharaInitID;
                this.GroupIDs = GroupIDs;
                this.Type = Type;
            }
            public List<uint> GetEntityIDs()
            {
                List<uint> ids = new();
                if (EntityID > 0)
                {
                    ids.Add(EntityID);
                }
                if (GroupIDs != null)
                {
                    ids.AddRange(GroupIDs);
                }
                return ids;
            }
            public string ModelName
            {
                get
                {
                    int sepIndex = Name.LastIndexOf('_');
                    if (MapName == "" || sepIndex <= 0) return null;
                    string name = Name;
                    if (name.StartsWith("m") && name.Contains("-"))
                    {
                        // Elden Ring big tile names
                        name = name.Split('-')[1];
                        sepIndex = name.LastIndexOf('_');
                    }
                    return name.Substring(0, sepIndex);
                }
            }
            public int GetModelID()
            {
                string model = ModelName;
                if (model == null) return -1;
                else if (model.StartsWith("c")) return int.Parse(model.Substring(1));
                else if (model.StartsWith("AEG")) return int.Parse(model.Substring(3, 3) + model.Substring(7, 3));
                return -1;
            }
            public override string ToString() => MapName == "" ? Name : (Name == "" ? "" : $"{Name} @ ") + MapName;
            public override bool Equals(object obj) => obj is Entity o && Equals(o);
            public bool Equals(Entity o) => MapName == o.MapName && Name == o.Name;
            public override int GetHashCode() => MapName.GetHashCode() ^ Name.GetHashCode();
            public int CompareTo(Entity o) => Nest(MapName.CompareTo(o.MapName), Name.CompareTo(o.Name));
            public Entity DeepCopy()
            {
                Entity other = (Entity)MemberwiseClone();
                other.GroupIDs = GroupIDs?.ToList();
                return other;
            }
        }

        private static int Nest(int first, int second)
        {
            return first != 0 ? first : second;
        }
    }
}
