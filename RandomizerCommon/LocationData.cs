using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace RandomizerCommon
{
    // These classes are awful and contain ugly Java-esque boilerplate (TODO rewrite)
    // But also C#'s lack of initialize-only fields is a travesty and both languages should feel bad.
    public class LocationData
    {
        // Location information for all items in the game, with additional splitting by how to acquire the item.
        public readonly SortedDictionary<ItemKey, ItemLocations> Data = new SortedDictionary<ItemKey, ItemLocations>();
        // Index of locations by how/when they are available. If one slot in the set is available, all the others are too.
        public readonly SortedDictionary<LocationScope, List<SlotKey>> Locations = new SortedDictionary<LocationScope, List<SlotKey>>();
        // Replacement lots, used to give treasure carps event drops
        public Dictionary<int, int> NewEntityLots = new Dictionary<int, int>();
        // Boss flags, for item locations with boss entities
        public Dictionary<int, int> BossFlags = new Dictionary<int, int>();

        public LocationData() { }

        public void AddLocation(ItemKey item, ItemScope scope, LocationKey key)
        {
            if (!Data.ContainsKey(item))
            {
                Data[item] = new ItemLocations();
            }
            if (!Data[item].Locations.ContainsKey(scope))
            {
                Data[item].Locations[scope] = new ItemLocation(scope);
            }
            Data[item].Locations[scope].Keys.Add(key);
        }

        public void AddLocationScope(ItemKey item, ItemScope scope, LocationScope locationScope)
        {
            SlotKey key = new SlotKey(item, scope);
            ItemLocation loc = Location(key);
            loc.LocScope = locationScope;
            if (!Locations.ContainsKey(locationScope))
            {
                Locations[locationScope] = new List<SlotKey>();
            }
            Locations[locationScope].Add(key);
        }

        public void AddLocationlessItem(ItemKey item)
        {
            ItemScope scope = new ItemScope(ItemScope.ScopeType.SPECIAL, -1);
            LocationScope locScope = new LocationScope(ItemScope.ScopeType.SPECIAL, -1, new SortedSet<int>(), new SortedSet<int>(), false);
            if (!Data.ContainsKey(item))
            {
                Data[item] = new ItemLocations();
                Data[item].Unique = true;
            }
            if (!Data[item].Locations.ContainsKey(scope))
            {
                Data[item].Locations[scope] = new ItemLocation(scope);
            }
            AddLocationScope(item, scope, locScope);
        }

        public ItemLocation Location(SlotKey key) {
            return Data[key.Item].Locations[key.Scope];
        }

        public List<SlotKey> Location(LocationScope scope)
        {
            Func<SlotKey, bool> isTarget = targetKey => Location(targetKey).Keys.Any(k => k.Base == null);
            List<SlotKey> bases = Locations[scope].Where(isTarget).ToList();
            if (bases.Count() == 0)
            {
                return new List<SlotKey> { Locations[scope][0] };
            }
            return bases;
        }

        // All ways to get a specific item.
        public class ItemLocations
        {
            public readonly SortedDictionary<ItemScope, ItemLocation> Locations;
            // For placement heuristic only
            public bool Unique { get; set; }
            public ItemLocations()
            {
                Locations = new SortedDictionary<ItemScope, ItemLocation>();
                Unique = false;
            }
        }

        // One way to get a specific item. Multiple locations are treated as equivalent.
        public class ItemLocation
        {
            public readonly List<LocationKey> Keys;
            public readonly ItemScope Scope;
            public LocationScope LocScope;
            public ItemLocation(ItemScope Scope)
            {
                this.Keys = new List<LocationKey>();
                this.Scope = Scope;
            }
            public SortedSet<string> GetLocations()
            {
                return new SortedSet<string>(Keys.SelectMany(k => k.Entities.Select(e => e.MapName)).Where(m => m != ""));
            }
            public int Quantity => Math.Max(1, Keys.Select(k => k.Quantity).DefaultIfEmpty().Min());
            public override string ToString() => String.Join(", ", Keys);
        }

        // Keys
        public enum ItemType
        {
            WEAPON = 0,
            ARMOR = 1,
            RING = 2,
            GOOD = 3,
            GEM = 4,
            CUSTOM = 6,
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

        // Across all items, the conditions for acquiring it. Does not map 1:1 with <ItemKey, ItemScope>
        public class LocationScope : IComparable<LocationScope>
        {
            public readonly ItemScope.ScopeType Type;
            // Event id, or entity id, or material id.
            public readonly int UniqueId;
            public readonly SortedSet<int> ShopIds;
            public readonly SortedSet<int> ModelLots;
            // Additional info
            public readonly bool OnlyShops;
            private string IdStr;
            public LocationScope(ItemScope.ScopeType Type, int UniqueId, SortedSet<int> ShopIds, SortedSet<int> ModelLots, bool OnlyShops)
            {
                this.Type = Type;
                this.UniqueId = UniqueId;
                this.ShopIds = ShopIds;
                this.ModelLots = ModelLots;
                this.OnlyShops = OnlyShops;
                // Group shops together as much as possible, removing events
                int id = UniqueId;
                if (OnlyShops)
                {
                    // id = -1;
                    id = 0;
                }
                // this.IdStr = $"{(int)Type}:{id}:{string.Join(",", ShopIds)}:{string.Join(",", ModelLots)}";
                this.IdStr = $"{(int)Type}:{id.ToString("0000000000")}:{string.Join(",", ShopIds)}:{string.Join(",", ModelLots)}";
            }
            public override string ToString()
            {
                return IdStr;
            }
            public string Description()
            {
                return scopeDesc[(int)Type];
            }
            public override bool Equals(object obj) => obj is LocationScope o && Equals(o);
            public bool Equals(LocationScope o) => IdStr == o.IdStr;
            public override int GetHashCode() => IdStr.GetHashCode();
            public int CompareTo(LocationScope o) => IdStr.CompareTo(o.IdStr);
        }

        private static List<string> scopeDesc = new List<string>() { "Unique location", "Unique drop", "Boss weapon", "Shop item", "Shop item", "Enemy drop", "Unknown location" };
        // *Within* a given item, the different ways of acquiring it
        public class ItemScope : IComparable<ItemScope>
        {
            public enum ScopeType
            {
                // Finite items, as enforced by having an event ID. (SHOP_INFINITE_EVENT can also have event IDs, use EventID property to check.)
                EVENT,
                // Finite items enforced by one drop per entity ID. Only present if no event flag in the item lot.
                // (These do also have event flags - record those too?)
                ENTITY,
                // Boss soul weapons, by boss soul item ID. These have no event flag, but all have quantity of 1.
                // The boss does need to be beaten (or area entered) for the item to appear in the shop.
                MATERIAL,
                // Infinite shop items. These have no event IDs.
                SHOP_INFINITE,
                // Shop items which can start out as having an event, but become infinite. The ID is the event ID before becoming infinite.
                SHOP_INFINITE_EVENT,
                // Infinite items farmables from some enemy in the map. Only present if no event flag.
                // There are other ways to split up farming, but item + model seems pretty good.
                MODEL,
                // Unique item, not acquirable through normal means
                SPECIAL,
                // Resource collected from an asset.
                ASSET,
            }
            private readonly HashSet<ScopeType> eventTypes = new HashSet<ScopeType> { ScopeType.EVENT, ScopeType.SHOP_INFINITE_EVENT };
            // Item scope is a union of these (only one may be filled in):
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

        public class LocationKey
        {
            public enum LocationType
            {
                LOT, SHOP
            }
            public readonly LocationType Type;
            public readonly int ID;
            // For debugging
            public readonly string Text;
            // Auxiliary
            public readonly List<EntityId> Entities;
            public readonly int Quantity;
            public readonly float Chance;
            public readonly string Subtype;
            // For a lot, the base lot location, if this is an additional draw. Otherwise null
            public readonly LocationKey Base;
            public LocationKey(
                LocationType Type, int ID, string Text,
                List<EntityId> Entities, int Quantity, float Chance, LocationKey Base, string Subtype = null)
            {
                this.Type = Type;
                this.ID = ID;
                this.Text = Text;
                this.Entities = Entities;
                this.Quantity = Quantity;
                this.Chance = Chance;
                this.Base = Base;
                this.Subtype = Subtype;
                if (Base != null && Base.Type != Type)
                {
                    throw new Exception($"Bad base {Base} for {Text}");
                }
                this.maxSlots = 1;
            }
            public int BaseID
            {
                get
                {
                    return Base == null ? ID : Base.ID;
                }
            }
            // The available slots in this location. If this is a non-base item in an item lot, this forwards to the base lot.
            private int maxSlots;
            public int MaxSlots
            {
                get
                {
                    if (Base != null)
                    {
                        // Access field directly - these should only be one deep
                        return Base.maxSlots;
                    }
                    return maxSlots;
                }
                set {
                    if (Type == LocationType.SHOP || Base != null)
                    {
                        throw new Exception($"{this} can have no properties - not base lot");
                    }
                    if (value < 1)
                    {
                        throw new Exception($"Cannot set {this} to {value} slots");
                    }
                    maxSlots = value;
                }
            }

            public string ParamName => (Type == LocationType.LOT ? "ItemLotParam" : "ShopLineupParam") + (Subtype == null ? "" : $"_{Subtype}");

            public override string ToString() => Text;
            // Are these really needed?
            public override bool Equals(object obj) => obj is LocationKey o && Equals(o);
            public bool Equals(LocationKey o) => Type == o.Type && ID == o.ID && Subtype == o.Subtype;
            public override int GetHashCode() => ((int)Type) << 28 ^ ID ^ (Subtype?.GetHashCode() ?? 0);
        }

        public class SlotKey : IComparable<SlotKey>
        {
            public readonly ItemKey Item;
            public readonly ItemScope Scope;
            public SlotKey(ItemKey Item, ItemScope Scope)
            {
                this.Item = Item;
                this.Scope = Scope;
            }
            public override string ToString() => $"({Item},{Scope})";

            public override bool Equals(object obj) => obj is SlotKey o && Equals(o);
            public bool Equals(SlotKey o) => Item == o.Item && Scope == o.Scope;
            public override int GetHashCode() => Item.GetHashCode() ^ Scope.GetHashCode();
            public int CompareTo(SlotKey o) => Nest(Item.CompareTo(o.Item), Scope.CompareTo(o.Scope));
            public string ToSimpleId() => $"{Item.Type},{Item.ID},{Scope.Type},{Scope.ID}";
        }

        public class EntityId : IComparable<EntityId>
        {
            // Entity name if extracted from a map. Any string otherwise
            public string EntityName { get; set; }
            // Map name, if extracted from a map. Empty string otherwise
            public string MapName { get; set; }

            public int EntityID { get; set; }
            public int CharaInitID { get; set; }
            
            // Mainly internal display info. Not defined for all games
            public int NPCParamID { get; set; }
            public int AssetID { get; set; }
            public List<int> GroupIds { get; set; }
            public string Type { get; set; }  // e.g. enemy, object
            public Vector3? Position { get; set; }
            public int NameID { get; set; }
            public int TalkID { get; set; }
            // This is a bit messy. Covers _01 and _02 maps in Elden Ring, for display purposes.
            public string OriginalMapName { get; set; }

            public EntityId() { }
            public EntityId(
                string MapName, string EntityName,
                int EntityID=-1, int NPCParamID=-1, int CharaInitID=-1, List<int> GroupIds=null,
                string Type=null)
            {
                this.MapName = MapName;
                this.EntityName = EntityName;
                this.EntityID = EntityID;
                this.NPCParamID = NPCParamID;
                this.CharaInitID = CharaInitID;
                this.GroupIds = GroupIds;
                this.Type = Type;
            }
            public List<int> GetEntityIds()
            {
                List<int> ids = new List<int>();
                if (EntityID > 0)
                {
                    ids.Add(EntityID);
                }
                if (GroupIds != null)
                {
                    ids.AddRange(GroupIds);
                }
                return ids;
            }
            public string ModelName
            {
                get
                {
                    int sepIndex = EntityName.LastIndexOf('_');
                    if (MapName == "" || sepIndex <= 0) return null;
                    string name = EntityName;
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
            public override string ToString() => MapName == "" ? EntityName : (EntityName == "" ? "" : $"{EntityName} @ ") + MapName;
            public override bool Equals(object obj) => obj is EntityId o && Equals(o);
            public bool Equals(EntityId o) => MapName == o.MapName && EntityName == o.EntityName;
            public override int GetHashCode() => MapName.GetHashCode() ^ EntityName.GetHashCode();
            public int CompareTo(EntityId o) => Nest(MapName.CompareTo(o.MapName), EntityName.CompareTo(o.EntityName));
            public EntityId DeepCopy()
            {
                EntityId other = (EntityId)MemberwiseClone();
                other.GroupIds = GroupIds == null ? null : GroupIds.ToList();
                return other;
            }
        }

        private static int Nest(int first, int second)
        {
            return first != 0 ? first : second;
        }
    }
}
