using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.LocationData;

namespace RandomizerCommon
{
    // This is effectively all of the state from writing the item permutation which is used for game-specific edits
    public class ItemFlagMapping
    {
        // -- Initial state before writing the permutation
        // Flags to items for Elden Ring, for flags tracked in event config
        public Dictionary<int, ItemKey> TrackedFlagItems = new();
        // Items to track all slots for, currently just for hints
        public HashSet<ItemKey> TrackedSlotItems = new();
        // Gesture item in DS3 and Elden Ring
        public ItemKey GestureItem { get; set; }
        // Map from shop id to boss soul
        public Dictionary<int, ItemKey> BossShopItems = new();
        // Map from dupe shop id to boss soul (Elden Ring only)
        public Dictionary<int, ItemKey> BossDupeItems = new();

        // -- Main flag mapping, when items and flags change
        // Map from item to its final item get flag, for tracked items.
        public Dictionary<ItemKey, int> ItemEventFlags = new();
        // Other map from slot to final item get flag
        public Dictionary<ItemLocKey, int> SlotEventFlags = new();
        // When only location matters in script usage and not item, track flag changes
        public Dictionary<int, int> RewrittenFlags = new();

        // -- Other mappings
        // Gesture flag in DS3 and Elden Ring (this just be in ItemEventFlags?)
        public int GestureFlag { get; set; }
        // Sekiro mapping from 10-wide shop flag to single permanent flag for cross-NG+ items
        public Dictionary<int, int> ShopPermanentFlags = new();
        // Sekiro mapping from unique boss memory goods id to event flag
        public Dictionary<int, int> MemoryFlags = new();

        // -- Output
        // Elden Ring: Mapping for merchant gift feature from NpcName id to flag, to hide merchant locations after receiving it
        public Dictionary<int, int> MerchantGiftFlags = new();
    }
}
