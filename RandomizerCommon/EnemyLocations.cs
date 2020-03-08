using System.Collections.Generic;

namespace RandomizerCommon
{
    public class EnemyLocations
    {
        // Mapping from named enemy to other named enemies where they end up, for item placement
        public Dictionary<string, List<string>> Target { get; set; }
    }
}
