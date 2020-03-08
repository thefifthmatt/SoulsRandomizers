using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomizerCommon
{
    public class LocationData
    {
        public static Dictionary<uint, ItemType> LotTypes = new Dictionary<uint, ItemType>
        {
            { 0x00000000, 0 },
            { 0x10000000, (ItemType) 1 },
            { 0x20000000, (ItemType) 2 },
            { 0x40000000, (ItemType) 3 }
        };
        public static Dictionary<ItemType, uint> RevLotTypes = LotTypes.ToDictionary(x => x.Value, x => x.Key);
        public static Dictionary<string, string> LocationNames = new Dictionary<string, string>
        {
            { "m30_00_00_00", "highwall" },
            { "m30_01_00_00", "lothric" },
            { "m34_01_00_00", "archives" },
            { "m31_00_00_00", "settlement" },
            { "m32_00_00_00", "archdragon" },
            { "m33_00_00_00", "farronkeep" },
            { "m35_00_00_00", "cathedral" },
            { "m37_00_00_00", "irithyll" },
            { "m38_00_00_00", "catacombs" },
            { "m39_00_00_00", "dungeon" },
            { "m40_00_00_00", "firelink" },
            { "m41_00_00_00", "kiln" },
            { "m45_00_00_00", "ariandel" },
            { "m50_00_00_00", "dregheap" },
            { "m51_00_00_00", "ringedcity" },
            { "m51_01_00_00", "filianore" }
        };
        public static Dictionary<string, string> RevLocationNames = LocationNames.ToDictionary(x => x.Value, x => x.Key);

        // Location information for all items in the game, with additional splitting by how to acquire the item.
        public readonly SortedDictionary<ItemKey, ItemLocations> Data = new SortedDictionary<ItemKey, ItemLocations>();
        // Index of locations by how/when they are available. If one slot in the set is available, all the others are too.
        public readonly SortedDictionary<LocationScope, List<SlotKey>> Locations = new SortedDictionary<LocationScope, List<SlotKey>>();
        public Dictionary<int, int> NewEntityLots = new Dictionary<int, int>();

        public LocationData() { }

        public void AddLocation(ItemKey item, ItemScope scope, LocationKey key) {
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
            GOOD = 3
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
            public bool Equals(ItemKey o) => Type == o.Type && ID == o.ID;
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
                    id = -1;
                }
                this.IdStr = $"{(int)Type}:{id}:{string.Join(",", ShopIds)}:{string.Join(",", ModelLots)}";
            }
            public override string ToString()
            {
                return IdStr;
            }
            private static List<string> desc = new List<string>() { "Unique location", "Unique drop", "Boss weapon", "Shop item", "Shop item", "Enemy drop" };
            public string Description()
            {
                return desc[(int)Type];
            }
            public override bool Equals(object obj) => obj is LocationScope o && Equals(o);
            public bool Equals(LocationScope o) => IdStr == o.IdStr;
            public override int GetHashCode() => IdStr.GetHashCode();
            public int CompareTo(LocationScope o) => IdStr.CompareTo(o.IdStr);
        }

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
            // For a lot, the base lot location, if this is an additional draw. Otherwise null
            public readonly LocationKey Base;
            public LocationKey(LocationType Type, int ID, string Text, List<EntityId> Entities, int Quantity, LocationKey Base)
            {
                this.Type = Type;
                this.ID = ID;
                this.Text = Text;
                this.Entities = Entities;
                this.Quantity = Quantity;
                this.Base = Base;
                if (Base != null && Base.Type != Type)
                {
                    throw new Exception($"Bad base {Base} for {Text}");
                }
                this.maxSlots = 1;
            }
            public int BaseID {
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
            public override string ToString() => Text;
            // Are these really needed?
            public override bool Equals(object obj) => obj is LocationKey o && Equals(o);
            public bool Equals(LocationKey o) => Type == o.Type && ID == o.ID;
            public override int GetHashCode() => ((int)Type) << 30 ^ ID;
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
            public override string ToString()
            {
                return $"({Item},{Scope})";
            }
            public override bool Equals(object obj) => obj is SlotKey o && Equals(o);
            public bool Equals(SlotKey o) => Item == o.Item && Scope == o.Scope;
            public override int GetHashCode() => Item.GetHashCode() ^ Scope.GetHashCode();
            public int CompareTo(SlotKey o) => Nest(Item.CompareTo(o.Item), Scope.CompareTo(o.Scope));
            public string ToSimpleId() => $"{Item.Type},{Item.ID},{Scope.Type},{Scope.ID}";
        }

        public class EntityId
        {
            // Map name, if extracted from a map. Empty string otherwise
            public readonly string MapName;
            // Entity name if extracted from a map. Any string otherwise
            public readonly string EntityName;

            public readonly int EventEntityID;
            public readonly int NPCParamID;
            public readonly int CharaInitID;
            public readonly List<int> GroupIds;
            public EntityId(string MapName, string EntityName, int EventEntityID=-1, int NPCParamID=-1, int CharaInitID=-1, List<int> GroupIds=null)
            {
                this.MapName = MapName;
                this.EntityName = EntityName;
                this.EventEntityID = EventEntityID;
                this.NPCParamID = NPCParamID;
                this.CharaInitID = CharaInitID;
                this.GroupIds = GroupIds;
            }
            public List<int> GetEntityIds()
            {
                List<int> ids = new List<int>();
                if (EventEntityID > 0)
                {
                    ids.Add(EventEntityID);
                }
                if (GroupIds != null)
                {
                    ids.AddRange(GroupIds);
                }
                return ids;
            }
            public string ModelName {
                get {
                    int sepIndex = EntityName.IndexOf('_');
                    if (MapName == "" || sepIndex <= 0) return null;
                    return EntityName.Substring(0, sepIndex);
                }
            }
            public int GetModelID()
            {
                string model = ModelName;
                if (model == null || !model.StartsWith("c"))
                {
                    return -1;
                }
                return int.Parse(model.Substring(1));
            }
            public override string ToString() => MapName == "" ? EntityName : (EntityName == "" ? "" : $"{EntityName} @ ") + MapName;
            public override bool Equals(object obj) => obj is EntityId o && Equals(o);
            public bool Equals(EntityId o) => MapName == o.MapName && EntityName == o.EntityName;
            public override int GetHashCode() => MapName.GetHashCode() ^ EntityName.GetHashCode();
        }

        private static int Nest(int first, int second)
        {
            return first != 0 ? first : second;
        }
    }
}
