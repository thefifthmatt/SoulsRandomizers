using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.ItemLocEditor;
using static RandomizerCommon.LocationData;

namespace RandomizerCommon
{
    // Game-specific utility currently just for use in PermutationWriter
    public abstract class ItemEditor
    {
        // Edit shop or lot to add external item data. Because of other item edits done in EditLocations, this should avoid editing item params until then.
        public virtual void ExternalItemOverride(ItemLocation source, Location target, ItemRow row)
        {
        }

        // Edit maps and event scripts and create new items
        public abstract void EditLocations(RandomizerOptions opt, ItemFlagMapping mapping);
    }
}
