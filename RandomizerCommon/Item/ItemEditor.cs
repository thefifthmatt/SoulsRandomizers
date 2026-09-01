using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon
{
    // Game-specific utility currently just for use in PermutationWriter
    public abstract class ItemEditor
    {
        public abstract void EditLocations(RandomizerOptions opt, ItemFlagMapping mapping);
    }
}
